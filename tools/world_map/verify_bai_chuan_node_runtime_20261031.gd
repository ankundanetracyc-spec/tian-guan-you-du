extends SceneTree

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var map = load("res://gdscripts/world/BaiChuanNodeMapRuntime.gd").new()
	root.add_child(map)
	current_scene = map
	for i in 3: await process_frame
	assert(map.travel.current_node == "bc_place_001")
	var neighbour: String = map.travel.graph.adjacency["bc_place_001"][0].to
	assert(map.places.get_node_or_null("Transit_"+neighbour) != null)
	map.places.get_node("Transit_"+neighbour).pressed.emit()
	assert(map.selected_node == neighbour)
	var outward: PackedVector2Array = map.travel.graph.points_between("bc_place_001",neighbour)
	var backward: PackedVector2Array = map.travel.graph.points_between(neighbour,"bc_place_001")
	assert(outward.size() > 1 and outward[0] == backward[-1] and outward[-1] == backward[0])
	var preview: Dictionary = map.travel.preview(neighbour,100)
	var time = root.get_node("GameTimeManager")
	var before: float = float(time.get_total_game_hours())+float(time.get_pending_hours())
	map.selected_node = neighbour
	map._travel_selected()
	await map.node_travel_finished
	var after: float = float(time.get_total_game_hours())+float(time.get_pending_hours())
	assert(map.travel.current_node == neighbour)
	assert(absf(after-before-float(preview.hours)) < 0.001)
	var state: Dictionary = map.get_node_state()
	assert(map.restore_node_state(state))
	print("BAICHUAN_NODE_RUNTIME_OK")
	current_scene = null
	map.queue_free()
	await process_frame
	quit()
