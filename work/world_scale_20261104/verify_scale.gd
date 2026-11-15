extends SceneTree

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var scale = load("res://gdscripts/world/BaiChuanWorldScale.gd")
	var legacy = load("res://gdscripts/world/BaiChuanNodeGraph.gd").new()
	var graph = load("res://gdscripts/world/BaiChuanNodeGraph.gd").new()
	graph.use_free_world = true
	assert(legacy.load_layout() and graph.load_layout())
	assert(graph.positions.size() == 444 and graph.edge_points.size() == 575)
	assert(is_equal_approx(graph.km_per_pixel, 8000.0 / 12288.0))
	assert(is_equal_approx(legacy.km_per_pixel, 200.0 / 3762.0))
	assert(is_equal_approx(graph.hours_for_km(40.0, 100.0), 24.0))
	var free_layout: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(scale.LAYOUT_PATH))
	var nav = load("res://gdscripts/world/BaiChuanFreeNavigation.gd").new()
	assert(nav.initialize())
	assert(nav.columns == 471 and is_equal_approx(nav.world_size, 12288.0))
	assert(is_equal_approx(nav.cell_size, 8.0 * 12288.0 / 3762.0))
	var invalid: Array[String] = []
	for edge_id: String in graph.edge_points:
		if not nav.validate_path(graph.edge_points[edge_id]): invalid.append(edge_id)
	print("SCALE_CORRIDORS ", graph.edge_points.size() - invalid.size(), "/", graph.edge_points.size(), " invalid=", invalid)
	assert(invalid.is_empty())
	var origin: Vector2 = graph.positions.bc_place_001
	var started := Time.get_ticks_msec()
	for place_id: String in free_layout.locations:
		var target: Vector2 = graph.positions[place_id]
		assert(nav.is_walkable(target))
		var path: PackedVector2Array = nav.find_path(origin, target)
		assert(not path.is_empty() and nav.validate_path(path))
	print("SCALE_PLACES ", free_layout.locations.size(), " ms=", Time.get_ticks_msec() - started)
	var npc = load("res://gdscripts/world/BaiChuanNpcNodeGraph.gd").new()
	assert(npc.initialize())
	var first: Dictionary = graph.adjacency.bc_place_001[0]
	var from: Vector2i = npc.node_id_to_cell("bc_place_001")
	var to: Vector2i = npc.node_id_to_cell(first.to)
	var ratio: float = npc.get_legacy_progress_scale(from, to, 100.0)
	assert(ratio > 0.0 and is_finite(ratio))
	var before: Vector2 = npc.get_map_position(from)
	var partial: Array[Vector2i] = [from, to]
	var projected: Vector2 = npc.project_map_position(from, partial, 1, npc.get_npc_edge_travel_hours(from, to, 100.0) * 0.5, 0.0, 100.0)
	assert(projected.distance_to(before) > 0.01 and projected.distance_to(npc.get_map_position(to)) > 0.01)
	print("SCALE_NPC_MIGRATION_RATIO ", ratio)
	npc.free()
	print("BAICHUAN_FREE_SCALE_OK")
	quit()
