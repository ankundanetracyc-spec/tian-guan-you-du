extends Node
## Compatibility identifiers are opaque node indices, never physical grid coordinates.
const Graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd")
const TerrainTravel = preload("res://gdscripts/world/BaiChuanTerrainTravel.gd")
var graph = Graph.new()
var terrain_travel = TerrainTravel.new()
var ids: Array[String] = []
var cells: Dictionary = {}
var neighbor_cache: Dictionary = {}
var edge_cost_cache: Dictionary = {}
var _legacy_graph

func initialize() -> bool:
	graph.use_free_world = true
	if not graph.load_layout() or not terrain_travel.initialize(): return false
	ids.clear()
	cells.clear()
	neighbor_cache.clear()
	edge_cost_cache.clear()
	ids.assign(graph.positions.keys())
	ids.sort()
	for i in range(ids.size()): cells[ids[i]] = Vector2i(i % 200, i / 200)
	return true

func node_id_to_cell(id: String) -> Vector2i:
	return cells.get(id, Vector2i(-1, -1))

func cell_to_node_id(cell: Vector2i) -> String:
	var i := cell.y * 200 + cell.x
	if cell.x < 0 or cell.x >= 200 or cell.y < 0 or i >= ids.size(): return ""
	return ids[i]

func is_cell_traversable(cell: Vector2i) -> bool:
	return not cell_to_node_id(cell).is_empty()

func get_location_anchor_cell(id: String) -> Vector2i:
	return node_id_to_cell(graph.node_for_runtime_id(id))

func get_location_node_id_at_cell(cell: Vector2i) -> String:
	return graph.runtime_id_for_node(cell_to_node_id(cell))

func get_npc_grid_path(from: Vector2i, to: Vector2i) -> Array[Vector2i]:
	var path: Array[Vector2i] = []
	for id: String in graph.shortest_path(cell_to_node_id(from), cell_to_node_id(to)):
		path.append(node_id_to_cell(id))
	return path

func get_npc_edge_travel_hours(from: Vector2i, to: Vector2i, speed: float) -> float:
	var edge := _edge_cost_profile(from, to)
	if edge.is_empty(): return INF
	return graph.hours_for_km(float(edge.cost) * graph.km_per_pixel, speed)

func get_terrain_speed_profile() -> String:
	return TerrainTravel.PROFILE_ID if terrain_travel.ready else ""

func _edge_cost_profile(from: Vector2i, to: Vector2i) -> Dictionary:
	# Direction is part of the key: the same curve has different prefix costs in reverse.
	var from_id := cell_to_node_id(from)
	var to_id := cell_to_node_id(to)
	var key := from_id + ">" + to_id
	if edge_cost_cache.has(key): return edge_cost_cache[key]
	var points: PackedVector2Array = graph.points_between(from_id, to_id)
	if points.size() < 2:
		edge_cost_cache[key] = {}
		return {}
	var lengths := PackedFloat64Array([0.0])
	var costs := PackedFloat64Array([0.0])
	for i in range(1, points.size()):
		lengths.append(lengths[-1] + points[i - 1].distance_to(points[i]))
		costs.append(costs[-1] + terrain_travel.segment_cost_length(points[i - 1], points[i]))
	var result := {"points": points, "lengths": lengths, "costs": costs, "length": lengths[-1], "cost": costs[-1]}
	edge_cost_cache[key] = result
	return result

func upgrade_uniform_travel_progress(from: Vector2i, to: Vector2i, old_hours: float, speed: float) -> float:
	# Convert elapsed uniform time by physical arc length, never by total weighted-time ratio.
	if not is_finite(old_hours) or old_hours < 0.0 or not is_finite(speed): return -1.0
	var edge := _edge_cost_profile(from, to)
	if edge.is_empty(): return -1.0
	var pair: Array[String] = [cell_to_node_id(from), cell_to_node_id(to)]
	var uniform_hours: float = graph.hours_for_km(graph.path_km(pair), speed)
	if not is_finite(uniform_hours) or uniform_hours <= 0.0: return -1.0
	var distance := float(edge.length) * clampf(old_hours / uniform_hours, 0.0, 1.0)
	var points: PackedVector2Array = edge.points
	var lengths: PackedFloat64Array = edge.lengths
	var costs: PackedFloat64Array = edge.costs
	for i in range(1, points.size()):
		if distance > lengths[i]: continue
		var end := points[i - 1].move_toward(points[i], maxf(0.0, distance - lengths[i - 1]))
		var cost := costs[i - 1] + terrain_travel.segment_cost_length(points[i - 1], end)
		return graph.hours_for_km(cost * graph.km_per_pixel, speed)
	return graph.hours_for_km(float(edge.cost) * graph.km_per_pixel, speed)

func get_legacy_progress_scale(from: Vector2i, to: Vector2i, speed: float) -> float:
	if not is_instance_valid(_legacy_graph):
		_legacy_graph = Graph.new()
		if not _legacy_graph.load_layout():
			_legacy_graph = null
			return 1.0
	var pair: Array[String] = [cell_to_node_id(from), cell_to_node_id(to)]
	var current_km: float = graph.path_km(pair)
	var legacy_km: float = _legacy_graph.path_km(pair)
	if not is_finite(current_km) or not is_finite(legacy_km) or current_km <= 0.0 or legacy_km <= 0.0: return 1.0
	return graph.hours_for_km(current_km, speed) / _legacy_graph.hours_for_km(legacy_km, speed)

func get_map_position(cell_id: Vector2i) -> Vector2:
	return graph.positions.get(cell_to_node_id(cell_id), Vector2(-1, -1))

func project_map_position(current: Vector2i, path: Array, next_index: int, progress_hours: float, extra_hours: float, speed: float) -> Vector2:
	# Fractional projection reads actual travel data and never mutates history.
	var available := maxf(0.0, progress_hours + extra_hours)
	var from := current
	for i in range(maxi(1, next_index), path.size()):
		var to: Vector2i = path[i]
		var hours := get_npc_edge_travel_hours(from, to, speed)
		if not is_finite(hours) or hours <= 0.0: return get_map_position(from)
		if available >= hours:
			available -= hours
			from = to
			continue
		var edge := _edge_cost_profile(from, to)
		var points: PackedVector2Array = edge.points
		var costs: PackedFloat64Array = edge.costs
		var budget := float(edge.cost) * available / hours
		for j in range(1, points.size()):
			if budget > costs[j]: continue
			var distance: float = terrain_travel.distance_for_cost(points[j - 1], points[j], maxf(0.0, budget - costs[j - 1]))
			return points[j - 1].move_toward(points[j], distance)
		return get_map_position(to)
	return get_map_position(from)

func get_npc_location_neighbors(runtime_id: String) -> Array[String]:
	if neighbor_cache.has(runtime_id): return neighbor_cache[runtime_id]
	var start: String = graph.node_for_runtime_id(runtime_id)
	var result: Array[String] = []
	if start.is_empty(): return result
	var queue: Array[String] = [start]
	var seen := {start: true}
	while not queue.is_empty():
		var current: String = queue.pop_front()
		for link: Dictionary in graph.adjacency[current]:
			var id: String = link.to
			if seen.has(id): continue
			seen[id] = true
			var mapped: String = graph.runtime_id_for_node(id)
			if not mapped.is_empty(): result.append(mapped)
			elif id.begins_with("bc_place_"): continue
			else: queue.append(id)
	neighbor_cache[runtime_id] = result
	return result
