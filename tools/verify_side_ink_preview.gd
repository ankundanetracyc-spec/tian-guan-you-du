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
	_check(preview.sequence_frames != null, "Sequence frames resource missing")
	for animation_name in ["slash","impact"]:
		var count := 16 if animation_name == "slash" else 20
		_check(preview.sequence_frames.get_frame_count(animation_name) == count, "Frame count: " + animation_name)
		_check(not preview.sequence_frames.get_animation_loop(animation_name), "Effect must not loop")
		var signatures: Dictionary = {}
		for index in range(count):
			var frame_image := Image.load_from_file(ProjectSettings.globalize_path(preview.FX_ROOT + "frames/%s_%02d.png" % [animation_name,index]))
			_check(frame_image.get_size() == Vector2i(768,768) and frame_image.get_format() == Image.FORMAT_RGBA8, "Sequence frame dimensions/alpha")
			var border_clear := true
			for edge in range(768):
				if frame_image.get_pixel(edge,0).a > 0.0 or frame_image.get_pixel(edge,767).a > 0.0 or frame_image.get_pixel(0,edge).a > 0.0 or frame_image.get_pixel(767,edge).a > 0.0:
					border_clear = false
					break
			_check(border_clear, "Effect frame exterior alpha")
			var hashing := HashingContext.new()
			hashing.start(HashingContext.HASH_SHA256)
			hashing.update(frame_image.get_data())
			signatures[hashing.finish().hex_encode()] = true
			if index == count - 1:
				_check(frame_image.is_invisible(), "Last effect frame must clear")
		_check(signatures.size() >= count - 1, "Sequence lacks distinct frames")
	preview.cycle_time = preview.IMPACT_START + 0.1
	preview._refresh_cycle()
	var slash_hold: int = preview.actors[0]["slash"].frame
	var impact_hold: int = preview.actors[0]["impact"].frame
	preview.cycle_time += 0.1
	preview._refresh_cycle()
	_check(preview.actors[0]["slash"].frame == slash_hold and preview.actors[0]["impact"].frame == impact_hold, "Hit hold advances sequence")
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
	var registration: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(preview.ART_ROOT + "export_manifest.json"))
	for entry in registration["poses"]:
		var source_image := Image.load_from_file(ProjectSettings.globalize_path(preview.ART_ROOT + str(entry["source"])))
		source_image.convert(Image.FORMAT_RGBA8)
		var authoring_scale: float = float(entry["authoring_scale"])
		if not is_equal_approx(authoring_scale,1.0):
			source_image.resize(roundi(source_image.get_width()*authoring_scale),roundi(source_image.get_height()*authoring_scale),Image.INTERPOLATE_LANCZOS)
		var source_origin := Vector2(float(entry["source_origin"][0]),float(entry["source_origin"][1]))
		var expected := Image.create(2048,2048,false,Image.FORMAT_RGBA8)
		expected.fill(Color.TRANSPARENT)
		expected.blit_rect(source_image,Rect2i(Vector2i.ZERO,source_image.get_size()),Vector2i((preview.GROUND_ORIGIN-source_origin*authoring_scale).round()))
		var actual := Image.load_from_file(ProjectSettings.globalize_path(preview.ART_ROOT+str(entry["output"])))
		_check(expected.get_data() == actual.get_data(), "Registered pose pixels differ: " + str(entry["output"]))
	for actor in preview.actors:
		_check(actor["idle"].texture != null and actor["chop"].texture != null, "Imported textures missing: " + str(actor["id"]))
		_check(not actor["idle"].centered and actor["idle"].offset == -preview.GROUND_ORIGIN, "Idle ground anchor")
		_check(not actor["chop"].centered and actor["chop"].offset == -preview.GROUND_ORIGIN, "Chop ground anchor")
	for time in [0.0, preview.WINDUP_START + 0.1, preview.STRIKE_START + 0.06, preview.IMPACT_START + 0.1, preview.RECOVER_START + 0.1, preview.RECOVER_END + 0.01]:
		preview.cycle_time = time
		preview._refresh_cycle()
		for actor in preview.actors:
			var actor_root: Node2D = actor["root"]
			_check(is_equal_approx(absf(actor_root.scale.x), actor["base_scale"]) and is_equal_approx(actor_root.scale.y, actor["base_scale"]), "Pose changes scale")
			_check(is_equal_approx(actor_root.position.y, actor["base_position"].y), "Pose changes ground level")
	preview.cycle_time = (preview.IMPACT_START + 0.1)
	preview._refresh_cycle()
	var held: Transform2D = preview.actors[0]["root"].global_transform
	preview.paused = true
	var held_frame: int = preview.actors[0]["impact"].frame
	preview._process(0.25)
	_check(preview.actors[0]["impact"].frame == held_frame, "Pause advances effect")
	_check(is_equal_approx(preview.cycle_time, (preview.IMPACT_START + 0.1)) and preview.actors[0]["root"].global_transform == held, "Pause moves actor or time")
	preview.paused = false
	preview._process(0.01)
	_check(preview.cycle_time > (preview.IMPACT_START + 0.1), "Resume fails")
	preview.cycle_time = (preview.IMPACT_START + 0.1)
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
	var right_hit := overlay.to_global(preview.actors[0]["fx_hit"])
	preview._toggle_mirror()
	var left_hit := overlay.to_global(preview.actors[0]["fx_hit"])
	var ground: Vector2 = preview.actors[0]["base_position"]
	_check(is_equal_approx(right_hit.x + left_hit.x, 2.0 * ground.x) and is_equal_approx(right_hit.y, left_hit.y), "Ink mirror origin diverges")
	preview._toggle_mirror()
	preview.cycle_time = preview.RECOVER_END + 0.01
	preview._refresh_cycle()
	for actor in preview.actors:
		_check(actor["root"].position == actor["base_position"], "Recovery misses ground origin")
		_check(not actor["slash"].visible and not actor["impact"].visible, "Effects remain after recovery")
	_check(is_equal_approx(preview.cinematic_zoom,1.0), "Camera fails to return")
	for mode in [1, 2]:
		preview.display_size_mode = mode
		preview._layout()
		var wanted := 190.0 if mode == 1 else 320.0
		_check(is_equal_approx(preview.actors[0]["base_scale"] * preview.REFERENCE_HEIGHT, wanted), "Local display height differs")
	preview.display_size_mode = 0
	preview._layout()
	for actor in preview.actors:
		_check(actor["rest"].texture!=null,"New small rest pose missing")
		for pose in ["hurt","guard","dodge"]:
			var art_root: String=preview.response_playback.DODGE_ART_ROOT if pose=="dodge" else preview.response_playback.ART_ROOT
			var path: String=art_root+str(actor["id"])+"_"+str(pose)+".png"
			var response_image:=Image.load_from_file(ProjectSettings.globalize_path(path))
			_check(response_image.get_size()==Vector2i(2048,2048) and response_image.get_format()==Image.FORMAT_RGBA8,"Response pose canvas: "+path)
			_check(actor[pose].texture!=null,"Imported pose missing: "+path)
	for kind in ["hurt","guard","restore","dodge"]:
		preview._set_action(kind)
		preview.cycle_time=1.45 if kind!="restore" else 1.82
		preview._refresh_cycle()
		var effect: AnimatedSprite2D=preview.actors[0]["response_effects"][kind]
		_check(effect.visible if kind!="guard" else not effect.visible,"Response effect visibility: "+kind)
		_check(effect.z_index>preview.actors[0]["idle"].z_index and effect.z_index>preview.actors[0]["dodge"].z_index,"Response effect hidden behind pose: "+kind)
		if kind=="restore":
			_check(preview.actors[0]["rest"].visible and not preview.actors[0]["idle"].visible and is_equal_approx(preview.cinematic_zoom,1.0),"Rest must use small pose without camera push")
		var response_transform: Transform2D=preview.actors[0]["root"].global_transform
		var response_frame:=effect.frame
		var response_time: float=preview.cycle_time
		preview.paused=true
		preview._process(.30)
		_check(effect.frame==response_frame and preview.cycle_time==response_time and preview.actors[0]["root"].global_transform==response_transform,"Response pause diverges: "+kind)
		preview.paused=false
		preview._process(.05)
		_check(preview.cycle_time>response_time,"Response resume fails: "+kind)
		if kind in ["hurt","guard","restore","dodge"]:
			preview.simulate_missing_action=true
			preview._refresh_cycle()
			_check(preview.actors[0]["idle"].visible and (effect.visible or kind=="guard"),"Response missing-pose fallback: "+kind)
			preview.simulate_missing_action=false
			var action_sprite: Sprite2D=preview.actors[0]["rest" if kind=="restore" else kind]
			var pose_texture:=action_sprite.texture
			action_sprite.texture=null
			preview._refresh_cycle()
			_check(preview.actors[0]["idle"].visible and (effect.visible or kind=="guard"),"Actual response missing texture: "+kind)
			action_sprite.texture=pose_texture
		preview.cycle_time=3.10
		preview._refresh_cycle()
		_check(not effect.visible and preview.actors[0]["idle"].visible,"Response fails to clear: "+kind)
		_check(preview.actors[0]["root"].position==preview.actors[0]["base_position"] and is_zero_approx(preview.actors[0]["root"].rotation) and is_equal_approx(preview.cinematic_zoom,1.0),"Response recovery misses origin: "+kind)
	preview._set_action("attack")
	preview.cycle_time=0.0
	preview._refresh_cycle()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			var directory := argument.trim_prefix("--capture-dir=")
			DirAccess.make_dir_recursive_absolute(directory)
			await _capture(preview, directory, "preview_idle.png", 0.0)
			await _capture(preview, directory, "preview_slash.png", preview.STRIKE_START + 4.0 / 30.0)
			await _capture(preview, directory, "preview_chop.png", (preview.IMPACT_START + 0.1))
			preview.mirrored = true
			preview.show_guides = true
			await _capture(preview, directory, "preview_mirror_guides.png", (preview.IMPACT_START + 0.1))
			preview.show_guides = false
			preview.mirrored = false
			preview.display_size_mode = 1
			preview._layout()
			await _capture(preview, directory, "preview_190px.png", (preview.IMPACT_START + 0.1))
			preview.display_size_mode = 0
			preview._layout()
			preview.background_mode = 1
			preview._update_name_colors()
			await _capture(preview, directory, "preview_light.png", (preview.IMPACT_START + 0.1))
			preview.background_mode = 2
			preview._update_name_colors()
			await _capture(preview, directory, "preview_dark.png", 0.0)
			preview.selected_actor = 1
			preview._layout()
			await _capture(preview, directory, "preview_female_idle.png", 0.0)
			await _capture(preview, directory, "preview_female.png", preview.IMPACT_START + .1)
			preview.show_portrait_reference = true
			preview._update_portrait_reference()
			await _capture(preview, directory, "preview_female_reference.png", 0.0)
			preview.selected_actor = 0
			preview._layout()
			await _capture(preview, directory, "preview_male_reference.png", 0.0)
			preview.show_portrait_reference = false
			preview._update_portrait_reference()
			preview.comparison = true
			preview.background_mode = 0
			preview._layout()
			await _capture(preview, directory, "preview_comparison.png", preview.IMPACT_START + .1)
			preview.comparison = false
			preview.selected_actor = 0
			preview._layout()
			for index in range(82):
				await _capture(preview, directory, "motion_%03d.png" % index, .5 + index / 30.0)
			preview.selected_actor = 1
			preview._layout()
			var female_directory := directory.path_join("female_motion")
			DirAccess.make_dir_recursive_absolute(female_directory)
			for index in range(82):
				await _capture(preview, female_directory, "motion_%03d.png" % index, .5 + index / 30.0)
	preview.queue_free()
	await process_frame
	if failures.is_empty():
		print("SIDE_INK_PREVIEW_PASS: original/response/rest/dodge textures; deterministic original registration; revised36 attack frames; attack hold/pause/mirror/recovery/190-320; hurt/guard/rest/dodge visibility/pause/resume/fallback/cleanup/recovery; small rest pose with no camera push")
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
