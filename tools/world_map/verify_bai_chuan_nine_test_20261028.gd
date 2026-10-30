extends SceneTree

var failures: Array[String] = []
const TEST_SCENE := "res://tscn/world/bai_chuan/BaiChuanNineTileTest.tscn"
const OUT := "res://output/bai_chuan_nine_test/"

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	print(("PASS " if ok else "FAIL ") + label)
	if not ok:
		failures.append(label)

func _run() -> void:
	var old_mode := root.content_scale_mode
	var old_size := root.content_scale_size
	var scene = load(TEST_SCENE).instantiate()
	root.add_child(scene)
	current_scene = scene
	for frame in 5:
		await process_frame
	check(scene._textures.size() == 9, "Nine original textures loaded")
	var sprites := 0
	for child in scene.world.get_children():
		if child is Sprite2D:
			sprites += 1
			check(child.scale == Vector2.ONE and child.texture.get_size() == Vector2(1254, 1254), "Native tile " + child.name)
	check(sprites == 9, "Nine directly adjoining tiles without runtime seam shader")
	check(scene.places.get_child_count() == 123, "123 named places")
	check(scene.ink.lines.size() == 498, "498 shared curved edges")
	check(scene.ink.marks.size() == 321, "321 transition nodes")
	check(root.content_scale_mode == Window.CONTENT_SCALE_MODE_DISABLED, "Window scaling disabled")
	check(root.content_scale_factor == 1.0 and scene.world.scale == Vector2.ONE, "Native 1:1 scale")
	var before: Vector2 = scene.camera_pos
	for direction in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
		var wheel := InputEventMouseButton.new()
		wheel.button_index = direction
		wheel.pressed = true
		Input.parse_input_event(wheel)
	var magnify := InputEventMagnifyGesture.new()
	magnify.factor = 2.0
	Input.parse_input_event(magnify)
	for frame in 3:
		await process_frame
	check(scene.world.scale == Vector2.ONE and scene.camera_pos == before, "Wheel and pinch cannot zoom")
	for place in ["玉虚仙宗", "玄都天府", "无相圣宗", "春水府"]:
		scene._focus_place(place)
		check(scene.title_label.text == place, "Focus " + place)
		check(scene.camera_pos.x >= 0 and scene.camera_pos.y >= 0, "Nonnegative camera bounds " + place)
		await process_frame
		scene._focus_place("玉虚仙宗")
	await process_frame
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(OUT + "v03_yuxu.png")
	scene._focus_place("春水府")
	await process_frame
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(OUT + "v03_central.png")
	scene.camera_pos = Vector2(1254, 1254) - root.get_visible_rect().size * 0.5
	scene._update_camera()
	await process_frame
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(OUT + "v03_four_tile_corner.png")
	scene._toggle_layer(false, "places")
	scene._toggle_layer(false, "paths")
	scene._toggle_layer(false, "marks")
	check(not scene.places.visible and not scene.ink.paths_visible and not scene.ink.marks_visible, "Layers toggle independently")
	for layer in ["places", "paths", "marks"]:
		scene._toggle_layer(true, layer)
	scene.camera_pos = Vector2(999999, 999999)
	scene._update_camera()
	check(scene.camera_pos.x <= maxf(0, scene.WORLD_SIZE - root.get_visible_rect().size.x), "Right camera boundary")
	check(scene.camera_pos.y <= maxf(0, scene.WORLD_SIZE - root.get_visible_rect().size.y), "Bottom camera boundary")
	scene.queue_free()
	await process_frame
	check(root.content_scale_mode == old_mode and root.content_scale_size == old_size, "Restored scaling on return")
	print("BAICHUAN_CHECK_DONE failures=" + str(failures.size()))
	quit(0 if failures.is_empty() else 1)
