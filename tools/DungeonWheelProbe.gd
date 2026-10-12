extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var panel = load("res://csharp/panel/EastWoodsDungeonPanel.cs").new()
	root.add_child(panel)
	panel.show_dungeon()
	for i in range(4):
		await process_frame
	var mouse := root.get_visible_rect().get_center()
	var motion := InputEventMouseMotion.new()
	motion.position = mouse
	motion.global_position = mouse
	root.push_input(motion, true)
	var wheel := InputEventMouseButton.new()
	wheel.position = mouse
	wheel.global_position = mouse
	wheel.button_index = MOUSE_BUTTON_WHEEL_UP
	wheel.pressed = true
	root.push_input(wheel, true)
	for i in range(4):
		await process_frame
	var scale: Vector2 = panel.get_node("MapViewport/OrthogonalNodeMap").scale
	print("DUNGEON_WHEEL_PROBE: ", scale, " hovered=", root.gui_get_hovered_control())
	if not is_equal_approx(scale.x, 1.12):
		quit(1)
		return
	var map: Control = panel.get_node("MapViewport/OrthogonalNodeMap")
	var viewport: Control = panel.get_node("MapViewport")
	var last: Vector2 = (viewport.size * 0.5 - map.position) / map.scale
	var step := InputEventKey.new()
	step.keycode = KEY_D
	step.pressed = true
	root.push_input(step, true)
	var maximum_jump := 0.0
	for i in range(24):
		await create_timer(0.02).timeout
		var focus: Vector2 = (viewport.size * 0.5 - map.position) / map.scale
		maximum_jump = maxf(maximum_jump, focus.distance_to(last))
		last = focus
	if panel.get_current_route_node_id() != "leaf_creek" or maximum_jump >= 75.0:
		push_error("行进或镜头连续性检查失败: " + str(maximum_jump))
		quit(1)
		return
	step = InputEventKey.new()
	step.keycode = KEY_A
	step.pressed = true
	root.push_input(step, true)
	await process_frame
	var pawn := map.get_node_or_null("TravelPawn")
	if pawn == null or not pawn.get_child(0).flip_h:
		push_error("左行棋子未翻转")
		quit(1)
		return
	await create_timer(0.5).timeout
	if not map.get_node("Node_forest_edge/Pawns/PlayerPawn").get_child(0).flip_h:
		push_error("落格朝向丢失")
		quit(1)
		return
	print("DUNGEON_CAMERA_FACING_PROBE_PASS: full-screen wheel, smooth camera max=", maximum_jump, ", left movement and settled facing")
	quit()
