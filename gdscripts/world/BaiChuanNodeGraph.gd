extends RefCounted
## Runtime graph derived from the reviewed display layout; distances are kilometres.

const LAYOUT_PATH := "res://textures/world/bai_chuan/acceptance_20261030/display_layout_v3.json"
const MAP_PIXELS := 3762.0
const WORLD_KM := 200.0
const RUNTIME_IDS_PATH := "res://resources/world/bai_chuan/bai_chuan_node_runtime_ids_20261031.json"
var positions: Dictionary = {}
var adjacency: Dictionary = {}
var edge_points: Dictionary = {}
var edge_km: Dictionary = {}
var runtime_ids: Dictionary = {}

func load_layout() -> bool:
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(LAYOUT_PATH))
	if not parsed is Dictionary:
		return false
	positions.clear()
	adjacency.clear()
	edge_points.clear()
	edge_km.clear()
	var aliases = JSON.parse_string(FileAccess.get_file_as_string(RUNTIME_IDS_PATH))
	if not aliases is Dictionary: return false
	runtime_ids = aliases
	for id in parsed.locations:
		var p: Array = parsed.locations[id].drawPos
		positions[id] = Vector2(p[0],p[1])
	for id in parsed.transitPositions:
		var p: Array = parsed.transitPositions[id]
		positions[id] = Vector2(p[0],p[1])
	for id in positions: adjacency[id] = []
	for key in parsed.displayGraph.edges:
		var edge: Dictionary = parsed.displayGraph.edges[key]
		if not positions.has(edge.from) or not positions.has(edge.to) or not parsed.displayEdges.has(key): return false
		var points := PackedVector2Array()
		for p: Array in parsed.displayEdges[key]: points.append(Vector2(p[0],p[1]))
		if points.size() < 2 or points[0].distance_to(positions[edge.from]) > 0.01 or points[-1].distance_to(positions[edge.to]) > 0.01: return false
		var length := 0.0
		for i in range(1,points.size()): length += points[i-1].distance_to(points[i])
		length *= WORLD_KM / MAP_PIXELS
		if length <= 0.0: return false
		edge_points[key] = points
		edge_km[key] = length
		adjacency[edge.from].append({"to":edge.to,"edge":key,"km":length})
		adjacency[edge.to].append({"to":edge.from,"edge":key,"km":length})
	for runtime_id in runtime_ids:
		if not positions.has(runtime_ids[runtime_id]): return false
	return not positions.is_empty() and edge_km.size() == parsed.displayGraph.edges.size()

func node_for_runtime_id(runtime_id: String) -> String:
	return str(runtime_ids.get(runtime_id,""))

func runtime_id_for_node(node_id: String) -> String:
	for runtime_id: String in runtime_ids:
		if runtime_ids[runtime_id] == node_id: return runtime_id
	return ""

func hours_for_km(km: float, speed: float) -> float:
	return maxf(km,0.0) * 60.0 / maxf(speed,0.01)

func shortest_path(start: String, destination: String) -> Array[String]:
	if not positions.has(start) or not positions.has(destination): return []
	var distance := {start: 0.0}
	var previous := {}
	var pending := {start:true}
	while not pending.is_empty():
		var current := ""
		var best := INF
		for id: String in pending:
			if float(distance[id]) < best:
				current = id
				best = float(distance[id])
		pending.erase(current)
		if current == destination:
			var result: Array[String] = [current]
			while previous.has(current):
				current = previous[current]
				result.push_front(current)
			return result
		for link: Dictionary in adjacency[current]:
			var next: String = link.to
			var candidate := best + float(link.km)
			if candidate < float(distance.get(next,INF)):
				distance[next] = candidate
				previous[next] = current
				pending[next] = true
	return []

func path_km(path: Array[String]) -> float:
	var total := 0.0
	for i in range(1,path.size()):
		if not adjacency.has(path[i-1]): return INF
		var found := false
		for link: Dictionary in adjacency[path[i-1]]:
			if link.to == path[i]:
				total += float(link.km)
				found = true
				break
		if not found: return INF
	return total

func points_between(from_id: String, to_id: String) -> PackedVector2Array:
	if not adjacency.has(from_id): return PackedVector2Array()
	for link: Dictionary in adjacency[from_id]:
		if link.to != to_id: continue
		var points: PackedVector2Array = edge_points[link.edge].duplicate()
		if points[0].distance_to(positions[from_id]) > 0.01: points.reverse()
		return points
	return PackedVector2Array()
