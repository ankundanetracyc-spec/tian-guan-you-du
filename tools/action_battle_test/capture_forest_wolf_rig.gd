extends Node2D

## Deterministic GPU capture in an isolated SubViewport. This does not run AI,
## alter player data, or depend on the main project's autoloads.
const WIDTH := 1440
const HEIGHT := 900
const FPS := 30.0
const FRAMES := 330
var output := ""
var viewport: SubViewport
var transparent_viewport: SubViewport
var wolves: Array[Node2D] = []
var hero: Node2D
var phase_label: Label
var font := SystemFont.new()


func _local(relative: String) -> String:
	return get_script().resource_path.get_base_dir().path_join(relative)


func _label(parent: Node, value: String, at: Vector2, size: int, tint := Color("34413f")) -> Label:
	var node := Label.new()
	node.text = value
	node.position = at
	node.add_theme_font_override("font", font)
	node.add_theme_font_size_override("font_size", size)
	node.add_theme_color_override("font_color", tint)
	parent.add_child(node)
	return node


func _panel(parent: Node, at: Vector2, size: Vector2, tint: Color) -> void:
	var node := ColorRect.new()
	node.position = at
	node.size = size
	node.color = tint
	parent.add_child(node)


func _wolf(parent: Node, at: Vector2, zoom: float, debug: bool) -> Node2D:
	var actor: Node2D = load(_local("forest_wolf_rig.gd")).new()
	parent.add_child(actor)
	assert(actor.build(_local("art/forest_wolf_ingame_v2")), "wolf rig must build before capture")
	actor.position = at
	actor.debug_bones = debug
	# A parent owns review magnification because the actor owns its mirror scale.
	var holder := Node2D.new()
	parent.add_child(holder)
	actor.reparent(holder, false)
	holder.position = at
	holder.scale = Vector2.ONE * zoom
	actor.position = Vector2.ZERO
	wolves.append(actor)
	return actor


func _make_scene() -> void:
	viewport = SubViewport.new()
	viewport.size = Vector2i(WIDTH, HEIGHT)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.transparent_bg = false
	add_child(viewport)
	_panel(viewport, Vector2.ZERO, Vector2(WIDTH, HEIGHT), Color("e8e7dd"))
	_panel(viewport, Vector2(24, 86), Vector2(1392, 282), Color("b4beb5"))
	_panel(viewport, Vector2(24, 410), Vector2(684, 414), Color("c4ccc4"))
	_panel(viewport, Vector2(732, 410), Vector2(684, 414), Color("c4ccc4"))
	_label(viewport, "林地凶狼 · 局内骨骼与动作", Vector2(28, 20), 32)
	_label(viewport, "同屏实际比例", Vector2(42, 102), 19)
	_label(viewport, "局部放大 · 连续蒙皮", Vector2(44, 426), 21)
	_label(viewport, "同一动作 · 骨骼叠线", Vector2(754, 426), 21)
	phase_label = _label(viewport, "", Vector2(30, 846), 23)
	var ground := TextureRect.new()
	var ground_image := Image.new()
	if ground_image.load(ProjectSettings.globalize_path(_local("art/battle_ground_stone_v1.png"))) == OK:
		ground.texture = ImageTexture.create_from_image(ground_image)
		ground.position = Vector2(24, 86)
		ground.size = Vector2(1392, 282)
		ground.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		ground.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		ground.modulate = Color(0.93, 0.96, 1.0)
		viewport.add_child(ground)
		viewport.move_child(ground, 2)
	hero = load(_local("guigu_rig_runtime.gd")).new()
	viewport.add_child(hero)
	assert(hero.load_runtime(_local("guigu_rig/rig.json"), _local("guigu_rig/skins.json"), _local("guigu_rig/animations.json"), _local("art"), "yuxu_fullbody_v15"))
	hero.set_skin_mode("experiment")
	hero.position = Vector2(560, 307)
	hero.set_character_pose("Idle", 0.0, 1.0)
	_wolf(viewport, Vector2(850, 306), 0.85, false)
	_wolf(viewport, Vector2(365, 735), 3.5, false)
	_wolf(viewport, Vector2(1075, 735), 3.5, true)
	transparent_viewport = SubViewport.new()
	transparent_viewport.size = Vector2i(640, 400)
	transparent_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	transparent_viewport.transparent_bg = true
	add_child(transparent_viewport)
	_wolf(transparent_viewport, Vector2(350, 340), 3.5, false)
	var display := TextureRect.new()
	display.texture = viewport.get_texture()
	display.size = Vector2(WIDTH, HEIGHT)
	add_child(display)


func _context(frame: int) -> Dictionary:
	var dt := 1.0 / FPS
	var context := {"state": "idle", "clock": 0.0, "velocity": Vector2.ZERO, "facing": 1.0, "alive": true, "delta": dt, "label": "待机 · 四足固定 / 胸腹呼吸"}
	if frame >= 45 and frame < 105:
		context.merge({"state": "chase", "velocity": Vector2(95, 0), "label": "追击 · 实际位移驱动四足"}, true)
	elif frame >= 105 and frame < 135:
		context.merge({"state": "chase", "velocity": Vector2(-95, 0), "facing": -1.0, "label": "转向 · 完整骨骼镜像"}, true)
	elif frame >= 135 and frame < 159:
		context.merge({"state": "windup", "clock": (159 - frame) / FPS, "label": "蓄势 · 压低重心"}, true)
	elif frame >= 159 and frame < 165:
		context.merge({"state": "lunge", "clock": (165 - frame) / FPS, "velocity": Vector2(300, 0), "label": "扑咬 · 伸展与下颌开合"}, true)
	elif frame >= 165 and frame < 195:
		context.merge({"state": "recover", "clock": (210 - frame) / FPS, "label": "收势 · 回落与支撑"}, true)
	elif frame >= 195 and frame < 210:
		context.merge({"state": "recover", "clock": (210 - frame) / FPS, "label": "受击 · 身体与耳尾响应"}, true)
	elif frame >= 210 and frame < 234:
		context.merge({"delta": 0.0, "label": "暂停 · 外部时间冻结"}, true)
	elif frame >= 234 and frame < 294:
		context.merge({"alive": false, "label": "倒地 · 连续骨骼动作与末帧保持"}, true)
	elif frame >= 294:
		context.label = "重来 · 恢复待机"
	return context


func _capture_output_folder() -> String:
	return "forest_wolf_ingame_v2_20261104"


func _sync_capture_actors() -> void:
	pass

func _capture_source_name() -> String:
	return "forest_wolf_ingame_v2"


func _ready() -> void:
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	output = ProjectSettings.globalize_path(_local("../../output/" + _capture_output_folder())).simplify_path()
	DirAccess.make_dir_recursive_absolute(output)
	DirAccess.make_dir_recursive_absolute(output.path_join("frames"))
	get_window().size = Vector2i(WIDTH, HEIGHT)
	get_window().content_scale_size = Vector2i(WIDTH, HEIGHT)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	_make_scene()
	await get_tree().process_frame
	var journal := FileAccess.open(output.path_join("capture_frames.jsonl"), FileAccess.WRITE)
	var probes := [0, 30, 60, 90, 120, 145, 158, 162, 180, 199, 220, 246, 276, 293, 305, 329]
	var probe_only := OS.get_cmdline_user_args().has("--probe-only")
	var saved_frames := 0
	var alpha_checks := {}
	for frame in range(FRAMES):
		var context := _context(frame)
		for actor in wolves:
			if frame == 195:
				actor.trigger_hurt()
			if frame == 294:
				actor.reset_pose()
			actor.set_motion_context(float(context.delta), context.velocity, str(context.state), float(context.clock), float(context.facing), bool(context.alive))
		_sync_capture_actors()
		var time := frame / FPS
		if hero.wanderer_skin != null:
			hero.wanderer_skin.set_motion_context(float(context.delta), Vector2.ZERO, time, 1.0)
		hero.set_character_pose("Idle", time, 1.0)
		phase_label.text = "%05.2f 秒    %s" % [time, context.label]
		journal.store_line(JSON.stringify({"frame": frame, "seconds": time, "phase": context.label, "runtime": wolves[0].get_runtime_summary()}))
		if probe_only and frame not in probes:
			continue
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var capture := viewport.get_texture().get_image()
		assert(capture != null and not capture.is_empty(), "GPU rendered a real capture frame")
		assert(capture.save_png(output.path_join("frames/frame_%04d.png" % frame)) == OK)
		saved_frames += 1
		if frame in probes:
			assert(capture.save_png(output.path_join("pose_%04d.png" % frame)) == OK)
		if frame == 60:
			assert(capture.save_png(output.path_join("wolf_rig_showcase.png")) == OK)
		if frame in [0, 162, 276]:
			var transparent := transparent_viewport.get_texture().get_image()
			assert(transparent.get_pixel(0, 0).a < 0.01 and transparent.get_pixel(639, 399).a < 0.01, "GPU wolf capture preserves transparent corners")
			assert(transparent.detect_alpha() != Image.ALPHA_NONE, "GPU wolf capture has actual alpha")
			assert(transparent.save_png(output.path_join("transparent_%04d.png" % frame)) == OK)
			alpha_checks[str(frame)] = {"alpha_mode": transparent.detect_alpha(), "corners_transparent": true}
		if frame % 60 == 0:
			print("FOREST WOLF CAPTURE ", frame, "/", FRAMES)
	journal.close()
	var report := FileAccess.open(output.path_join("capture_report.json"), FileAccess.WRITE)
	report.store_string(JSON.stringify({"complete": true, "frames": saved_frames, "fps": FPS, "probe_only": probe_only,
		"source": _capture_source_name(), "transparent_render_checks": alpha_checks}, "\t"))
	report.close()
	print("FOREST WOLF CAPTURE COMPLETE: ", output)
	get_tree().quit()
