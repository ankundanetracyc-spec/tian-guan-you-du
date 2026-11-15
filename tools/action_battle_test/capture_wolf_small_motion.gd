extends "capture_forest_wolf_rig.gd"

## Actual scene translation at the approved small scale, with one moving human
## reference. The lower panel magnifies only the same wolf's local articulation.
const MOTION_FRAMES := 120
const SMALL_SCALE := 0.55
var detail_wolf: Node2D
var actor_wolf: Node2D
var detail_camera: Camera2D
var sizing_viewports: Dictionary = {}
var hero_phase := 0.0
var motion_log: Array[Dictionary] = []


func _art_dir() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--art-dir="):
			var path: String = argument.trim_prefix("--art-dir=")
			return path if path.begins_with("res://") or path.is_absolute_path() else _local(path)
	var pattern := RegEx.new()
	pattern.compile("art/forest_wolf_ingame_v[0-9]+")
	var found := pattern.search(FileAccess.get_file_as_string(_local("action_battle_feel_test.gd")))
	return _local(found.get_string()) if found != null else _local("art/forest_wolf_ingame_v3")


func _wolf(parent: Node, at: Vector2, zoom: float, debug: bool) -> Node2D:
	var holder := Node2D.new()
	parent.add_child(holder)
	holder.position = at
	holder.scale = Vector2.ONE * zoom
	var actor: Node2D = load(_local("forest_wolf_rig.gd")).new()
	holder.add_child(actor)
	assert(actor.build(_art_dir()), "current wolf parts build before capture")
	actor.debug_bones = debug
	wolves.append(actor)
	return actor


func _make_scene() -> void:
	viewport = SubViewport.new()
	viewport.size = Vector2i(WIDTH, HEIGHT)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	_panel(viewport, Vector2.ZERO, Vector2(WIDTH, HEIGHT), Color("e8e7dd"))
	_panel(viewport, Vector2(24, 90), Vector2(1392, 430), Color("b4beb5"))
	_panel(viewport, Vector2(24, 580), Vector2(1392, 250), Color("c4ccc4"))
	_label(viewport, "林地凶狼 · 缩小后的实际移动", Vector2(28, 22), 31)
	_label(viewport, "同一脚底线 · 局内实际尺寸", Vector2(44, 106), 19)
	_label(viewport, "同一动作局部放大 · 仅作脚步核对", Vector2(44, 594), 18)
	phase_label = _label(viewport, "", Vector2(30, 540), 22)
	var ground_image := Image.new()
	if ground_image.load(ProjectSettings.globalize_path(_local("art/battle_ground_stone_v1.png"))) == OK:
		var ground := TextureRect.new()
		ground.texture = ImageTexture.create_from_image(ground_image)
		ground.position = Vector2(24, 90)
		ground.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		ground.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		ground.size = Vector2(1392, 430)
		ground.modulate = Color(0.93, 0.96, 1.0)
		viewport.add_child(ground)
		viewport.move_child(ground, 2)
	hero = load(_local("guigu_rig_runtime.gd")).new()
	viewport.add_child(hero)
	assert(hero.load_runtime(_local("guigu_rig/rig.json"), _local("guigu_rig/skins.json"), _local("guigu_rig/animations.json"), _local("art"), "yuxu_fullbody_v15"))
	hero.set_skin_mode("experiment")
	hero.position = Vector2(830, 410)
	hero.set_character_pose("Idle", 0.0, 1.0)
	actor_wolf = _wolf(viewport, Vector2(570, 413), 1.0, false)
	actor_wolf.scale = Vector2.ONE * SMALL_SCALE
	var detail_viewport := SubViewport.new()
	detail_viewport.size = Vector2i(1392, 250)
	detail_viewport.transparent_bg = true
	detail_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(detail_viewport)
	detail_wolf = _wolf(detail_viewport, Vector2(570, 413), 1.0, false)
	detail_wolf.scale = Vector2.ONE * SMALL_SCALE
	detail_camera = Camera2D.new()
	detail_camera.zoom = Vector2.ONE * 3.0
	detail_viewport.add_child(detail_camera)
	detail_camera.position = detail_wolf.global_position + Vector2(0, -30)
	detail_camera.make_current()
	var detail_display := TextureRect.new()
	detail_display.position = Vector2(24, 580)
	detail_display.size = Vector2(1392, 250)
	detail_display.texture = detail_viewport.get_texture()
	viewport.add_child(detail_display)
	_make_sizing_views()
	_label(viewport, "右行 → 提速 → 减速落足 → 停留 → 左行", Vector2(30, 854), 20)
	var display := TextureRect.new()
	display.texture = viewport.get_texture()
	display.size = Vector2(WIDTH, HEIGHT)
	add_child(display)


func _make_sizing_views() -> void:
	for kind in ["hero", "wolf"]:
		var isolated := SubViewport.new()
		isolated.size = Vector2i(384, 256)
		isolated.transparent_bg = true
		isolated.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		add_child(isolated)
		sizing_viewports[kind] = isolated
		if kind == "hero":
			var figure: Node2D = load(_local("guigu_rig_runtime.gd")).new()
			isolated.add_child(figure)
			assert(figure.load_runtime(_local("guigu_rig/rig.json"), _local("guigu_rig/skins.json"), _local("guigu_rig/animations.json"), _local("art"), "yuxu_fullbody_v15"))
			figure.set_skin_mode("experiment")
			figure.position = Vector2(192, 220)
			figure.set_character_pose("Idle", 0.0, 1.0)
		else:
			var figure := _wolf(isolated, Vector2(192, 223), 1.0, false)
			figure.scale = Vector2.ONE * SMALL_SCALE
			figure.set_motion_context(1.0 / FPS, Vector2.ZERO, "idle", 0.0, 1.0, true)


func _alpha_bounds(bitmap: Image) -> Rect2i:
	var left := bitmap.get_width()
	var top := bitmap.get_height()
	var right := -1
	var bottom := -1
	for y in range(bitmap.get_height()):
		for x in range(bitmap.get_width()):
			if bitmap.get_pixel(x, y).a > 0.08:
				left = mini(left, x)
				top = mini(top, y)
				right = maxi(right, x)
				bottom = maxi(bottom, y)
	return Rect2i(left, top, right - left + 1, bottom - top + 1)


func _motion_context(frame: int) -> Dictionary:
	var velocity := 47.5
	var label := "慢走"
	var facing := 1.0
	if frame >= 30 and frame < 60:
		velocity = 95.0
		label = "追逐"
	elif frame >= 60 and frame < 75:
		velocity = lerpf(95.0, 0.0, float(frame - 60) / 14.0)
		label = "减速 · 落足"
	elif frame >= 75 and frame < 90:
		velocity = 0.0
		label = "停步 · 足点稳定"
	elif frame >= 90:
		velocity = -95.0 * minf(1.0, float(frame - 89) / 12.0)
		facing = -1.0
		label = "反向起步" if frame < 102 else "左向追逐"
	return {"velocity": Vector2(velocity, 0), "facing": facing, "label": label}


func _output_folder() -> String:
	return "forest_wolf_small_bones_20261104"


func _detail_camera_offset() -> Vector2:
	return Vector2(0, -30)

func _motion_capture_fps() -> float:
	return FPS

func _motion_capture_count() -> int:
	return MOTION_FRAMES


func _ready() -> void:
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	output = ProjectSettings.globalize_path(_local("../../output/" + _output_folder())).simplify_path()
	DirAccess.make_dir_recursive_absolute(output.path_join("frames"))
	get_window().size = Vector2i(WIDTH, HEIGHT)
	get_window().content_scale_size = Vector2i(WIDTH, HEIGHT)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	_make_scene()
	await get_tree().process_frame
	var journal := FileAccess.open(output.path_join("motion_frames.jsonl"), FileAccess.WRITE)
	var initial_position := actor_wolf.global_position
	var expected_displacement := Vector2.ZERO
	var capture_fps := _motion_capture_fps()
	var capture_count := _motion_capture_count()
	for frame in range(capture_count):
		var context := _motion_context(floori(frame*FPS/capture_fps))
		var dt := 1.0 / capture_fps
		var velocity: Vector2 = context.velocity
		# The actor's own transform moves; the diagnostic rig below does not
		# receive fictitious world displacement to disguise a sliding contact.
		expected_displacement += velocity * dt
		actor_wolf.global_position += velocity * dt
		actor_wolf.set_motion_context(dt, velocity, "chase", 0.0, float(context.facing), true)
		detail_wolf.global_position += velocity * dt
		detail_wolf.set_motion_context(dt, velocity, "chase", 0.0, float(context.facing), true)
		detail_camera.position = detail_wolf.global_position + _detail_camera_offset()
		var hero_velocity := velocity * (114.0 / 95.0)
		hero.global_position += hero_velocity * dt
		hero_phase += hero_velocity.length() / 114.0 * dt
		if hero.wanderer_skin != null:
			hero.wanderer_skin.set_motion_context(dt, hero_velocity, frame / capture_fps, float(context.facing))
		hero.set_character_pose("Run_New1" if velocity.length() > 0.1 else "Idle", hero_phase, float(context.facing))
		phase_label.text = "%04.2f 秒    %s    %.1f / 秒" % [frame / capture_fps, context.label, velocity.length()]
		var record := {"frame": frame, "seconds": frame / capture_fps, "label": context.label,
			"velocity": [velocity.x, velocity.y], "wolf_world": [actor_wolf.global_position.x, actor_wolf.global_position.y],
			"hero_world": [hero.global_position.x, hero.global_position.y], "facing": context.facing,
			"runtime": actor_wolf.get_runtime_summary()}
		journal.store_line(JSON.stringify(record))
		motion_log.append(record)
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var capture := viewport.get_texture().get_image()
		assert(capture.save_png(output.path_join("frames/frame_%04d.png" % frame)) == OK)
		if frame in [0, 35, 60, 74, 89, 105, 119]:
			assert(capture.save_png(output.path_join("pose_%04d.png" % frame)) == OK)
		if frame == floori(35*capture_fps/FPS):
			assert(capture.save_png(output.path_join("preview.png")) == OK)
		if frame % 30 == 0:
			print("SMALL WOLF MOTION ", frame, "/", capture_count)
	journal.close()
	var sizing := {}
	for kind in sizing_viewports:
		var bitmap: Image = sizing_viewports[kind].get_texture().get_image()
		var bounds := _alpha_bounds(bitmap)
		assert(bounds.size.y > 0 and bounds.position.y > 0, "isolated alpha captures contain the whole painted actor")
		bitmap.save_png(output.path_join("sizing_%s.png" % kind))
		sizing[kind] = {"bounds": [bounds.position.x, bounds.position.y, bounds.size.x, bounds.size.y], "height": bounds.size.y}
	sizing["wolf_to_human_height"] = float(sizing.wolf.height) / float(sizing.hero.height)
	var actual_displacement := actor_wolf.global_position - initial_position
	var displacement_error := actual_displacement.distance_to(expected_displacement)
	assert(displacement_error < 0.01, "actual world displacement matches the integrated velocity supplied to the gait")
	var report := FileAccess.open(output.path_join("capture_report.json"), FileAccess.WRITE)
	report.store_string(JSON.stringify({"complete": true, "frames": capture_count, "fps": capture_fps,
		"wolf_scale": SMALL_SCALE, "actual_world_displacement": [actual_displacement.x, actual_displacement.y],
		"expected_world_displacement": [expected_displacement.x, expected_displacement.y], "world_displacement_error": displacement_error,
		"source": _art_dir(), "hero_reference": "yuxu_fullbody_v15", "sizing": sizing}, "\t"))
	report.close()
	print("SMALL WOLF MOTION COMPLETE: ", output)
	get_tree().quit()
