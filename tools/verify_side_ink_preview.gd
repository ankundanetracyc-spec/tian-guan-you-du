extends SceneTree

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("_verify")

func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error(message)

func _verify() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1440, 900)
	root.content_scale_size = Vector2i(1440, 900)
	var packed := load("res://tscn/scene/BattlePoseLibraryShowcase.tscn") as PackedScene
	if packed == null:
		push_error("Cannot load in-game test entry")
		quit(1)
		return
	var preview: Node2D = packed.instantiate()
	root.add_child(preview)
	preview.set_process(false)
	await process_frame
	_check(preview.actors.size() == 2, "Expected male and female at existing menu destination")
	for name in ["male_idle", "female_idle", "male_chop", "female_chop"]:
		var image := Image.load_from_file(ProjectSettings.globalize_path(preview.ART_ROOT + name + ".png"))
		_check(image != null and image.get_size() == Vector2i(2048, 2048), name + " canvas")
		if image == null:
			continue
		_check(image.get_format() == Image.FORMAT_RGBA8, name + " RGBA")
		var clear_border := true
		for coordinate in range(2048):
			if image.get_pixel(coordinate, 0).a > 0.0 or image.get_pixel(coordinate, 2047).a > 0.0 or image.get_pixel(0, coordinate).a > 0.0 or image.get_pixel(2047, coordinate).a > 0.0:
				clear_border = false
				break
		_check(clear_border, name + " clear exterior")
		for y in range(1885, 1920):
			for x in range(2048):
				if image.get_pixel(x, y).a > 0.06275 and y > 1900:
					_check(false, name + " foot below common baseline")
					break
	var idle_image := Image.load_from_file(ProjectSettings.globalize_path(preview.ART_ROOT + "male_idle.png"))
	var source_image := Image.load_from_file(ProjectSettings.globalize_path(preview.ART_ROOT + "source/male_idle.png"))
	_check(idle_image.get_region(Rect2i(512, 460, 1024, 1536)).get_data() == source_image.get_data(), "Male idle pixels changed during padding")
	for actor in preview.actors:
		_check(actor["idle"].texture != null and actor["chop"].texture != null, "Imported textures missing: " + str(actor["id"]))
		_check(not actor["idle"].centered and actor["idle"].offset == -preview.GROUND_ORIGIN, "Idle ground anchor")
		_check(not actor["chop"].centered and actor["chop"].offset == -preview.GROUND_ORIGIN, "Chop ground anchor")
	for time in [0.0, 1.19, 1.33, 1.44, 1.66, 1.84]:
		preview.cycle_time = time
		preview._refresh_cycle()
		for actor in preview.actors:
			var actor_root: Node2D = actor["root"]
			_check(is_equal_approx(absf(actor_root.scale.x), actor["base_scale"]) and is_equal_approx(actor_root.scale.y, actor["base_scale"]), "Pose changes scale")
			_check(is_equal_approx(actor_root.position.y, actor["base_position"].y), "Pose changes ground level")
	preview.cycle_time = 1.44
	preview._refresh_cycle()
	var held: Transform2D = preview.actors[0]["root"].global_transform
	preview.paused = true
	preview._process(0.25)
	_check(is_equal_approx(preview.cycle_time, 1.44) and preview.actors[0]["root"].global_transform == held, "Pause moves actor or time")
	preview.paused = false
	preview._process(0.01)
	_check(preview.cycle_time > 1.44, "Resume fails")
	preview.cycle_time = 1.44
	preview.simulate_missing_action = true
	preview._refresh_cycle()
	for actor in preview.actors:
		_check(actor["idle"].visible and not actor["chop"].visible, "Missing-pose simulation changes outfit")
	_check(preview.phase_name == "impact", "Fallback stops effect phase")
	preview.simulate_missing_action = false
	var saved: Texture2D = preview.actors[0]["chop"].texture
	preview.actors[0]["chop"].texture = null
	preview._refresh_cycle()
	_check(preview.actors[0]["idle"].visible and not preview.actors[0]["chop"].visible, "Actual absent texture does not fall back")
	preview.actors[0]["chop"].texture = saved
	preview._refresh_cycle()
	var overlay: Node2D = preview.actors[0]["overlay"]
	var right_hit := overlay.to_global(Vector2(650.0, -355.0))
	preview._toggle_mirror()
	var left_hit := overlay.to_global(Vector2(650.0, -355.0))
	var ground: Vector2 = preview.actors[0]["base_position"]
	_check(is_equal_approx(right_hit.x + left_hit.x, 2.0 * ground.x) and is_equal_approx(right_hit.y, left_hit.y), "Ink mirror origin diverges")
	preview._toggle_mirror()
	preview.cycle_time = preview.RECOVER_END + 0.01
	preview._refresh_cycle()
	for actor in preview.actors:
		_check(actor["root"].position == actor["base_position"], "Recovery misses ground origin")
	for mode in [1, 2]:
		preview.display_size_mode = mode
		preview._layout()
		var wanted := 190.0 if mode == 1 else 320.0
		_check(is_equal_approx(preview.actors[0]["base_scale"] * preview.REFERENCE_HEIGHT, wanted), "Local display height differs")
	preview.display_size_mode = 0
	preview._layout()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			var directory := argument.trim_prefix("--capture-dir=")
			DirAccess.make_dir_recursive_absolute(directory)
			await _capture(preview, directory, "preview_idle.png", 0.0)
			await _capture(preview, directory, "preview_chop.png", 1.44)
			preview.mirrored = true
			preview.show_guides = true
			await _capture(preview, directory, "preview_mirror_guides.png", 1.44)
			preview.show_guides = false
			preview.mirrored = false
			preview.display_size_mode = 1
			preview._layout()
			await _capture(preview, directory, "preview_190px.png", 1.44)
			preview.display_size_mode = 0
			preview._layout()
			preview.background_mode = 1
			preview._update_name_colors()
			await _capture(preview, directory, "preview_light.png", 1.44)
			preview.background_mode = 2
			preview._update_name_colors()
			await _capture(preview, directory, "preview_dark.png", 0.0)
	preview.queue_free()
	await process_frame
	if failures.is_empty():
		print("SIDE_INK_PREVIEW_PASS: 4 RGBA canvases; original pixels; fixed scale/ground; pause/resume; real/simulated missing poses; mirrored ink; recovery; 190/320 heights")
		quit(0)
	else:
		print("SIDE_INK_PREVIEW_FAIL: ", failures)
		quit(1)

func _capture(preview: Node2D, directory: String, filename: String, time: float) -> void:
	preview.cycle_time = time
	preview._refresh_cycle()
	await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	var error := image.save_png(directory.path_join(filename))
	_check(error == OK and image.get_width() > 0, "Capture failed: " + filename)
