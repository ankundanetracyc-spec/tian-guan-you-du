extends Node2D

## Whole-animal walking sample only. The wolf renderer switches one complete
## atlas frame at a time, with one shared canvas and foot anchor for the loop.
## It creates no wolf bones, deformation, frame blending or combat integration.

const WIDTH := 1440
const HEIGHT := 900
const SAMPLE_COUNT := 24
const SAMPLE_FPS := 24.0

class WholeWolfFrame extends Node2D:
	var atlas: Texture2D
	var source_rect := Rect2()
	var canvas_size := Vector2.ONE
	var foot_anchor := Vector2.ZERO
	var pixels_to_world := 1.0
	var facing := 1.0
	func _draw() -> void:
		if atlas == null:
			return
		draw_set_transform(Vector2.ZERO, 0.0, Vector2(facing, 1.0))
		draw_texture_rect_region(atlas, Rect2(-foot_anchor * pixels_to_world, canvas_size * pixels_to_world), source_rect)

var manifest: Dictionary = {}
var validation_errors: Array[String] = []
var atlas: Texture2D
var frame_rects: Array[Rect2] = []
var canvas_size := Vector2.ZERO
var foot_anchor := Vector2.ZERO
var playback_fps := 8.0
var display_height := 78.0
var preview_speed := 1.0
var preview_facing := 1.0
var paused := false
var animation_time := 0.0
var current_frame := 0
var actors: Array[WholeWolfFrame] = []
var hero: Node2D
var viewport: SubViewport
var frame_label: Label
var status_label: Label
var output_path := ""
var font := SystemFont.new()
var capture_mode := false


func _local(relative: String) -> String:
	var base: String = get_script().resource_path.get_base_dir()
	return base.path_join(relative)


func _load_frames() -> bool:
	var folder := _local("art/forest_wolf_frames_v1")
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(folder.path_join("frames.json")))
	if not data is Dictionary:
		validation_errors.append("未找到走路图集说明 frames.json")
		return false
	manifest = data
	var entries: Variant = manifest.get("frames")
	if not entries is Array or entries.size() != 8:
		validation_errors.append("走路样片需要8张完整帧")
		return false
	var canvas: Array = manifest.get("canvas", entries[0].get("canvas", [entries[0].rect[2], entries[0].rect[3]]))
	canvas_size = Vector2(float(canvas[0]), float(canvas[1]))
	var anchor: Array = manifest.get("foot_anchor", [canvas_size.x * 0.5, canvas_size.y * 0.92])
	foot_anchor = Vector2(float(anchor[0]), float(anchor[1]))
	playback_fps = maxf(1.0, float(manifest.get("fps", 8.0)))
	display_height = maxf(1.0, float(manifest.get("display_height", 78.0)))
	var image := Image.new()
	var source_path := folder.path_join(str(manifest.get("source", "walk.png")))
	if image.load(ProjectSettings.globalize_path(source_path)) != OK:
		validation_errors.append("无法读取整狼走路图集")
		return false
	var image_bounds := Rect2(Vector2.ZERO, Vector2(image.get_size()))
	for entry in entries:
		var coordinates: Array = entry.get("rect", [])
		if coordinates.size() != 4:
			validation_errors.append("走路帧缺少有效rect")
			continue
		var rect := Rect2(float(coordinates[0]), float(coordinates[1]), float(coordinates[2]), float(coordinates[3]))
		if rect.size != canvas_size:
			validation_errors.append("8帧必须使用相同尺寸画布，不能按每帧包围框分别拉伸")
		if not image_bounds.encloses(rect):
			validation_errors.append("走路帧超出图集范围")
		if entry.has("canvas"):
			var per_frame_canvas: Array = entry.canvas
			if Vector2(float(per_frame_canvas[0]), float(per_frame_canvas[1])) != canvas_size:
				validation_errors.append("每帧canvas必须与公共画布一致")
		frame_rects.append(rect)
	if canvas_size.x <= 0.0 or canvas_size.y <= 0.0:
		validation_errors.append("走路画布尺寸必须大于零")
	if not validation_errors.is_empty():
		return false
	image.generate_mipmaps()
	atlas = ImageTexture.create_from_image(image)
	return true


func _label(parent: Node, text: String, at: Vector2, size: int, tint := Color("34413f")) -> Label:
	var label := Label.new()
	label.text = text
	label.position = at
	label.add_theme_font_override("font", font)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", tint)
	parent.add_child(label)
	return label


func _panel(at: Vector2, size: Vector2, color: Color) -> void:
	var panel := ColorRect.new()
	panel.position = at
	panel.size = size
	panel.color = color
	viewport.add_child(panel)


func _make_wolf(at: Vector2, magnification: float) -> void:
	var wolf := WholeWolfFrame.new()
	wolf.name = "WholeWolfActual" if actors.is_empty() else "WholeWolfEnlarged"
	wolf.atlas = atlas
	wolf.canvas_size = canvas_size
	wolf.foot_anchor = foot_anchor
	wolf.pixels_to_world = display_height / maxf(canvas_size.y, 1.0)
	wolf.position = at
	wolf.scale = Vector2.ONE * magnification
	wolf.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	wolf.texture_repeat = CanvasItem.TEXTURE_REPEAT_DISABLED
	viewport.add_child(wolf)
	actors.append(wolf)


func _make_scene() -> void:
	viewport = SubViewport.new()
	viewport.size = Vector2i(WIDTH, HEIGHT)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	_panel(Vector2.ZERO, Vector2(WIDTH, HEIGHT), Color("e8e7dd"))
	_label(viewport, "林地凶狼 · 走路样片", Vector2(30, 22), 31)
	_panel(Vector2(24, 92), Vector2(1392, 306), Color("b4beb5"))
	var ground_image := Image.new()
	if ground_image.load(ProjectSettings.globalize_path(_local("art/battle_ground_stone_v1.png"))) == OK:
		var ground := TextureRect.new()
		ground.texture = ImageTexture.create_from_image(ground_image)
		ground.position = Vector2(24, 92)
		ground.size = Vector2(1392, 306)
		ground.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		ground.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		ground.modulate = Color(0.93, 0.96, 1.0)
		viewport.add_child(ground)
	_panel(Vector2(24, 420), Vector2(1392, 378), Color("c4ccc4"))
	_label(viewport, "与玉虚人物同屏 · 实际比例", Vector2(44, 106), 21)
	_label(viewport, "整狼走路 · 放大2.5倍", Vector2(44, 434), 22)
	frame_label = _label(viewport, "", Vector2(1035, 436), 20)
	status_label = _label(viewport, "", Vector2(30, 815), 22)
	_label(viewport, "空格 暂停 / 继续    ← → 朝向    − + 调速    R 重新播放", Vector2(30, 852), 20)
	if atlas != null:
		hero = load(_local("guigu_rig_runtime.gd")).new()
		viewport.add_child(hero)
		if hero.load_runtime(_local("guigu_rig/rig.json"), _local("guigu_rig/skins.json"), _local("guigu_rig/animations.json"), _local("art"), "yuxu_fullbody_v15"):
			hero.set_skin_mode("experiment")
			hero.position = Vector2(550, 330)
			hero.set_character_pose("Idle", 0.0, 1.0)
		_make_wolf(Vector2(855, 330), 1.0)
		_make_wolf(Vector2(740, 731), 2.5)
	else:
		_label(viewport, "\n".join(validation_errors), Vector2(60, 560), 23)
	var display := TextureRect.new()
	display.texture = viewport.get_texture()
	display.size = Vector2(WIDTH, HEIGHT)
	add_child(display)


func _sample(time: float) -> void:
	current_frame = floori(time * playback_fps) % maxi(frame_rects.size(), 1)
	for actor in actors:
		actor.source_rect = frame_rects[current_frame]
		actor.facing = preview_facing
		actor.queue_redraw()
	if hero != null:
		hero.set_character_pose("Idle", time, 1.0)
	frame_label.text = "第%d / 8帧" % (current_frame + 1)
	status_label.text = "%s    %.2f倍速度    每秒%.1f帧" % ["已暂停" if paused else "循环播放", preview_speed, playback_fps * preview_speed]


func _ready() -> void:
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	output_path = ProjectSettings.globalize_path(_local("../../output/forest_wolf_frames_v1_20261104")).simplify_path()
	capture_mode = OS.get_cmdline_user_args().has("--capture")
	var loaded := _load_frames()
	_make_scene()
	get_window().size = Vector2i(WIDTH, HEIGHT)
	get_window().content_scale_size = Vector2i(WIDTH, HEIGHT)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	set_process(loaded and not capture_mode)
	if loaded:
		_sample(0.0)
	if capture_mode:
		if loaded:
			_capture_samples()
		else:
			push_error("Whole wolf preview failed: " + str(validation_errors))
			get_tree().quit(1)


func _process(delta: float) -> void:
	if not paused:
		animation_time += delta * preview_speed
	_sample(animation_time)


func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo or capture_mode:
		return
	match event.keycode:
		KEY_SPACE:
			paused = not paused
		KEY_LEFT:
			preview_facing = -1.0
		KEY_RIGHT:
			preview_facing = 1.0
		KEY_MINUS, KEY_KP_SUBTRACT:
			preview_speed = maxf(0.25, preview_speed - 0.25)
		KEY_EQUAL, KEY_PLUS, KEY_KP_ADD:
			preview_speed = minf(2.0, preview_speed + 0.25)
		KEY_R:
			animation_time = 0.0
		KEY_ESCAPE:
			get_tree().quit()
	if not actors.is_empty():
		_sample(animation_time)


func _capture_samples() -> void:
	DirAccess.make_dir_recursive_absolute(output_path.path_join("frames"))
	var indices: Array[int] = []
	var sample_data: Array[Dictionary] = []
	for index in range(SAMPLE_COUNT):
		var time := index / SAMPLE_FPS
		_sample(time)
		indices.append(current_frame)
		sample_data.append({"sample": index, "time": time, "frame_index": current_frame})
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var image := viewport.get_texture().get_image()
		assert(image != null and not image.is_empty(), "Whole wolf preview must render through the GPU")
		assert(image.save_png(output_path.path_join("frames/frame_%04d.png" % index)) == OK)
		if index == 0:
			assert(image.save_png(output_path.path_join("preview.png")) == OK)
	var report := {"complete": true, "samples": SAMPLE_COUNT, "sample_fps": SAMPLE_FPS, "loop_fps": playback_fps,
		"frame_count": frame_rects.size(), "sample_frame_indices": indices, "sample_data": sample_data,
		"canvas": [canvas_size.x, canvas_size.y], "foot_anchor": [foot_anchor.x, foot_anchor.y],
		"display_height": display_height, "magnification": 2.5, "wolf_bones": 0,
		"frame_blending": false, "per_frame_scaling": false, "source": manifest.get("source"), "validation_errors": validation_errors}
	var file := FileAccess.open(output_path.path_join("preview_report.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "\t"))
	file.close()
	print("WHOLE WOLF FRAME PREVIEW COMPLETE: ", output_path)
	get_tree().quit()
