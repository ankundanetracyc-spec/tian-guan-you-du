extends Node
## Compatibility identifiers are opaque node indices, never physical grid coordinates.
const Graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd")
const TerrainTravel = preload("res://gdscripts/world/BaiChuanTerrainTravel.gd")
const Navigation = preload("res://gdscripts/world/BaiChuanFreeNavigation.gd")
const Sources = preload("res://gdscripts/world/WorldMapSourceRegistry.gd")
var graph = Graph.new()
var terrain_travel = TerrainTravel.new()
var ids: Array[String] = []
var cells: Dictionary = {}
var neighbor_cache: Dictionary = {}
var edge_cost_cache: Dictionary = {}
var _legacy_graph
var _previous_free_graph
var _source_graphs: Dictionary = {}

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

func get_navigation_profile() -> String:
	return Navigation.PROFILE_ID

func get_navigation_world_size() -> float:
	return Navigation.Scale.MAP_PIXELS

func _migration_source(saved_profile: String, source_world_size: float, saved_version: int) -> Dictionary:
	if saved_version not in [1, 2] or not is_finite(source_world_size) or source_world_size < 0.0: return {}
	var source_size := source_world_size
	# Named navigation profiles identify a release even in tagged v1 node saves.
	if source_size <= 0.0:
		if saved_profile == Navigation.PROFILE_ID: source_size = Navigation.Scale.MAP_PIXELS
		elif saved_profile in [Sources.EXPANDED_PROFILE_ID, Sources.COMPACT_PEAKS_PROFILE_ID]: source_size = Sources.EXPANDED_WORLD_SIZE
		elif saved_profile == "complete_mountains_20261106_v1": source_size = Sources.COMPACT_WORLD_SIZE
		elif saved_profile.is_empty() and saved_version < 2: source_size = Sources.LEGACY_WORLD_SIZE
		else: return {}
	if saved_version < 2 and saved_profile.is_empty() and absf(source_size - Sources.LEGACY_WORLD_SIZE) < 0.001:
		return {"world_size": Sources.LEGACY_WORLD_SIZE, "world_km": Navigation.Scale.LEGACY_WORLD_KM, "layout_path": Sources.LEGACY_LAYOUT, "legacy": true}
	var source := Sources.resolve(saved_profile, source_size)
	if source.is_empty(): return {}
	return {"world_size": source_size, "world_km": Navigation.Scale.WORLD_KM, "layout_path": source.layout_path, "legacy": false}

func _source_graph(source: Dictionary):
	var path := str(source.layout_path)
	if _source_graphs.has(path): return _source_graphs[path]
	var old_graph = Graph.new()
	old_graph.use_free_world = not bool(source.legacy)
	if not old_graph.load_layout(path): return null
	_source_graphs[path] = old_graph
	return old_graph

func can_upgrade_navigation_source(saved_profile: String, source_world_size: float, saved_terrain_profile: String, saved_version: int = 2) -> bool:
	if not terrain_travel.ready or not Sources.terrain_available(saved_terrain_profile): return false
	var source := _migration_source(saved_profile, source_world_size, saved_version)
	return not source.is_empty() and _source_graph(source) != null

func upgrade_navigation_travel_progress(from: Vector2i, to: Vector2i, old_hours: float, speed: float, saved_profile: String = "", source_world_size: float = 7372.8, saved_terrain_profile: String = "terrain-speed-v1", saved_version: int = 2) -> float:
	if not is_finite(old_hours) or old_hours < 0.0 or not is_finite(speed) or speed <= 0.0: return -1.0
	if not terrain_travel.ready or not Sources.terrain_available(saved_terrain_profile): return -1.0
	var source := _migration_source(saved_profile, source_world_size, saved_version)
	if source.is_empty(): return -1.0
	var old_graph = _source_graph(source)
	if old_graph == null: return -1.0
	# Vector2i values are stable node identifiers. Resolve IDs unchanged in each
	# graph; never multiply cells, path entries, or next_index by a world ratio.
	var old_points: PackedVector2Array = old_graph.points_between(cell_to_node_id(from), cell_to_node_id(to))
	var edge := _edge_cost_profile(from, to)
	if old_points.size() < 2 or edge.is_empty(): return -1.0
	# Only explicitly weighted progress on identical source geometry can be
	# retained directly. Untagged uniform progress still takes the conversion.
	if absf(float(source.world_size) - Navigation.Scale.MAP_PIXELS) < 0.001 and str(source.layout_path) == Navigation.Scale.LAYOUT_PATH and saved_terrain_profile == TerrainTravel.PROFILE_ID and old_points == (edge.points as PackedVector2Array):
		return old_hours
	# Recover the original curve point using the OLD graph's kilometre ruler
	# and the OLD weighted field (or the old uniform rule if no terrain tag).
	var budget: float = old_hours * speed / (60.0 * old_graph.km_per_pixel)
	var old_point := old_points[-1]
	for i in range(1, old_points.size()):
		var cost := Sources.segment_cost_length(old_points[i - 1], old_points[i], float(source.world_size), saved_terrain_profile)
		if not is_finite(cost): return -1.0
		if budget <= cost:
			var distance := Sources.distance_for_cost(old_points[i - 1], old_points[i], budget, float(source.world_size), saved_terrain_profile)
			if distance < 0.0: return -1.0
			old_point = old_points[i - 1].move_toward(old_points[i], distance)
			break
		budget -= cost
	old_point *= Navigation.Scale.MAP_PIXELS / float(source.world_size)
	# Project onto the current legal edge, then encode its NEW weighted prefix.
	# Identity, route, calendar, and the already settled elapsed hours stay owned
	# by the NPC manager; this method has no live NPC side effects.
	var closest_distance := INF
	var migrated := 0.0
	var points: PackedVector2Array = edge.points
	for i in range(1, points.size()):
		var point := Geometry2D.get_closest_point_to_segment(old_point, points[i - 1], points[i])
		var squared := old_point.distance_squared_to(point)
		if squared < closest_distance:
			closest_distance = squared
			migrated = edge.costs[i - 1] + terrain_travel.segment_cost_length(points[i - 1], point)
	return graph.hours_for_km(migrated * graph.km_per_pixel, speed)

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
		# Prefix costs are monotonic. Locate the same first prefix >= budget as
		# the former linear scan without walking a long curve for every NPC tick.
		var lo := 1
		var hi := costs.size() - 1
		while lo < hi:
			var middle := (lo + hi) / 2
			if budget > costs[middle]: lo = middle + 1
			else: hi = middle
		var distance: float = terrain_travel.distance_for_cost(points[lo - 1], points[lo], maxf(0.0, budget - costs[lo - 1]))
		return points[lo - 1].move_toward(points[lo], distance)
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
