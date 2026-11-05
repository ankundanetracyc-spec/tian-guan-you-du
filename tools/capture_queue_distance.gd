extends SceneTree

const OUT = "res://docs/美术规范/行动队列局内形象_20261103/"

func _initialize():
	call_deferred("capture")

func capture():
	root.size = Vector2i(1440, 810)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	var scene = load("res://tools/QueueAvatarBattlePreview.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for i in range(45):
		await process_frame
	var controls = scene.get_node_or_null("QueueAvatarPreviewControls")
	if controls:
		controls.hide()
	var toggle = scene.find_child("PlanningToggle", true, false)
	if toggle is Button and "隐藏" in toggle.text:
		toggle.emit_signal("pressed")
		await process_frame
	var dolls = scene.get_node("DeployLayer").find_children("BattlePaperDoll", "", true, false)
	if dolls.size() != 2:
		push_error("Expected two actual battle figures")
		quit(1)
		return
	for doll in dolls:
		doll.set_process(false)
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUT + "distance_current.png")
	for doll in dolls:
		doll.scale *= 0.75
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUT + "distance_far25.png")
	print("DISTANCE_COMPARISON_OK: same scene, same UI, same foot positions; figure scales1.0 and0.75")
	quit()
