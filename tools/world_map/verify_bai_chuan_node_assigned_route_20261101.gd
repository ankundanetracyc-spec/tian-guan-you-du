extends SceneTree

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var map = load("res://gdscripts/world/BaiChuanNodeMapRuntime.gd").new()
	root.add_child(map)
	current_scene = map
	var adapter = load("res://gdscripts/world/BaiChuanNpcNodeGraph.gd").new()
	root.add_child(adapter)
	assert(adapter.initialize())
	await process_frame
	var route: Array[String] = map.get_assigned_route_to_node("east_woods")
	assert(route.size() > 2 and route[0] == "bc_place_001" and route[-1] == "bc_place_070")
	for id in route:
		assert(adapter.cell_to_node_id(adapter.node_id_to_cell(id)) == id)
	var expected: float = map.get_path_travel_hours(route)
	assert(expected > 0.0)
	assert(map.start_assigned_route(route, "east_woods"))
	await map.node_step_committed
	assert(map.is_traveling())
	var reached: String = map.get_current_node()
	assert(reached == route[1])
	assert(map.get_remaining_travel_path()[0] == reached)
	map.interrupt_assigned_route()
	await map.node_travel_finished
	assert(map.get_current_node() == reached and not map.is_traveling())
	var remaining: Array[String] = map.get_assigned_route_to_node("east_woods")
	assert(remaining[0] == reached and remaining[-1] == "bc_place_070")
	assert(map.get_path_travel_hours(remaining) < expected)
	print("BAICHUAN_NODE_ASSIGNED_ROUTE_OK")
	current_scene = null
	map.queue_free()
	adapter.queue_free()
	await process_frame
	quit()
