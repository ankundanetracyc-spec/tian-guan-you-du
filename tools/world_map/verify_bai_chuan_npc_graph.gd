extends SceneTree

func _initialize() -> void:
	var expected_appdata := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--expected-appdata="): expected_appdata = arg.trim_prefix("--expected-appdata=")
	if expected_appdata.is_empty() or OS.get_environment("APPDATA") != expected_appdata:
		push_error("NPC graph check requires isolated APPDATA")
		quit(1)
		return
	var adapter = load("res://gdscripts/world/BaiChuanNpcNodeGraph.gd").new()
	if not adapter.initialize():
		push_error("NPC node graph failed to load")
		quit(1)
		return
	var origin: Vector2i = adapter.get_location_anchor_cell("yuxu_xianzong_core")
	var node: String = adapter.cell_to_node_id(origin)
	var neighbors: Array[String] = adapter.get_npc_location_neighbors("yuxu_xianzong_core")
	if node != "bc_place_001" or neighbors.is_empty():
		push_error("NPC node identities or neighbours missing")
		quit(1)
		return
	var destination: Vector2i = adapter.get_location_anchor_cell(neighbors[0])
	var path: Array[Vector2i] = adapter.get_npc_grid_path(origin, destination)
	if path.size() < 2:
		push_error("NPC node path missing")
		quit(1)
		return
	var hours: float = adapter.get_npc_edge_travel_hours(path[0],path[1],100.0)
	var node_path: Array[String] = [adapter.cell_to_node_id(path[0]), adapter.cell_to_node_id(path[1])]
	var points: PackedVector2Array = adapter.graph.points_between(node_path[0], node_path[1])
	var weighted_km: float = adapter.terrain_travel.path_cost_length(points) * adapter.graph.km_per_pixel
	var expected: float = adapter.graph.hours_for_km(weighted_km, 100.0)
	if not is_finite(hours) or absf(hours - expected) > 0.001:
		push_error("NPC node travel time incorrect")
		quit(1)
		return
	print("BAICHUAN_NPC_NODE_GRAPH_OK nodes=", adapter.ids.size(), " edge_hours=", hours)
	adapter.free()
	quit()
