extends Node
## Compatibility identifiers are opaque node indices, never physical grid coordinates.
const Graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd")
var graph = Graph.new()
var ids: Array[String] = []
var cells: Dictionary = {}
var neighbor_cache: Dictionary = {}

func initialize() -> bool:
	if not graph.load_layout(): return false
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
	var path: Array[String] = [cell_to_node_id(from), cell_to_node_id(to)]
	return graph.hours_for_km(graph.path_km(path), speed)

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
