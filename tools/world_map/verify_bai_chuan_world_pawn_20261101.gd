extends SceneTree

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var map = load("res://gdscripts/world/BaiChuanNodeMapRuntime.gd").new()
	root.add_child(map)
	current_scene = map
	for i in 3: await process_frame
	assert(map.restore_node_state({"current_node":"bc_place_021","remaining_route":[]}))
	var pawn: Sprite2D = map.player_mark
	assert(pawn != null and pawn.visible and pawn.z_index > 0)
	assert(pawn.texture.resource_path == "res://textures/ui/world/player_pawn_marker_v6.tres")
	var start: Vector2 = map.travel.graph.positions["bc_place_021"]
	assert(pawn.position.distance_to(start) < 0.01)
	var expected: Vector2 = (start - root.size * 0.5).clamp(Vector2.ZERO, (Vector2.ONE * map.WORLD_SIZE - Vector2(root.size)).max(Vector2.ZERO)).round()
	assert(map.camera_pos.distance_to(expected) < 1.1)
	var neighbour: String = map.travel.graph.adjacency["bc_place_021"][0].to
	var finish: Vector2 = map.travel.graph.positions[neighbour]
	map.selected_node = neighbour
	map._travel_selected()
	await create_timer(0.12).timeout
	assert(map.travel_busy and pawn.position.distance_to(start) > 0.01 and pawn.position.distance_to(finish) > 0.01)
	await map.node_travel_finished
	assert(pawn.position.distance_to(finish) < 0.1)
	assert(map.restore_node_state({"current_node":"bc_place_021","remaining_route":[]}))
	assert(pawn.position.distance_to(start) < 0.01)
	print("BAICHUAN_WORLD_PAWN_OK")
	current_scene = null
	map.queue_free()
	await process_frame
	quit()
