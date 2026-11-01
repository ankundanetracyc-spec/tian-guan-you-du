extends RefCounted
## Node travel state. The caller owns time advancement and commits one edge at a time.

const Graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd")
var graph = Graph.new()
var current_node := ""
var route: Array[String] = []
var next_index := 0

func initialize(start: String) -> bool:
	if not graph.load_layout() or not graph.positions.has(start): return false
	current_node = start
	route.clear()
	next_index = 0
	return true

func preview(destination: String, speed: float) -> Dictionary:
	var path: Array[String] = graph.shortest_path(current_node,destination)
	if path.is_empty(): return {}
	var km: float = graph.path_km(path)
	return {"path":path,"kilometres":km,"hours":graph.hours_for_km(km,speed)}

func begin(path: Array[String]) -> bool:
	if path.size() < 2 or path[0] != current_node or graph.path_km(path) == INF: return false
	route = path.duplicate()
	next_index = 1
	return true

func next_step(speed: float) -> Dictionary:
	if next_index <= 0 or next_index >= route.size(): return {}
	var a := route[next_index-1]
	var b := route[next_index]
	var pair: Array[String] = [a,b]
	var km: float = graph.path_km(pair)
	if km == INF: return {}
	return {"from":a,"to":b,"kilometres":km,"hours":graph.hours_for_km(km,speed)}

func commit_step(expected_to: String) -> bool:
	if next_index <= 0 or next_index >= route.size() or route[next_index] != expected_to: return false
	current_node = expected_to
	next_index += 1
	if next_index >= route.size():
		route.clear()
		next_index = 0
	return true

func persistent_state() -> Dictionary:
	return {"current_node":current_node,"remaining_route":route.slice(next_index-1) if next_index > 0 else []}

func restore_state(state: Dictionary) -> bool:
	var node := str(state.get("current_node",""))
	if not graph.positions.has(node): return false
	var raw: Array = state.get("remaining_route",[])
	var saved: Array[String] = []
	for id in raw: saved.append(str(id))
	if not saved.is_empty() and (saved.size() < 2 or saved[0] != node or graph.path_km(saved) == INF): return false
	current_node = node
	route = saved
	next_index = 1 if not saved.is_empty() else 0
	return true
