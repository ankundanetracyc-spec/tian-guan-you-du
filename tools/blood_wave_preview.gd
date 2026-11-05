extends SceneTree
## Standalone visual comparison; no battle scene / resource data are instantiated.
## Example: --script res://tools/blood_wave_preview.gd -- --capture-dir=E:/tmp/blood_wave --frames=96

const Wave = preload("res://tools/action_battle_test/blood_wave_vfx.gd")
const VIEW_SIZE := Vector2i(1280, 720)
const CYCLE_SECONDS := 2.55
var stage: Node2D
var backdrop: Node2D
var waves: Array[Node2D] = []
var origins: Array[Vector2] = []
var directions: Array[Vector2] = []
var capture_dir := ""
var capture_frames := 90
var capture_fps := 30.0
var preview_clock := 0.0
var ready_to_play := false
var _space_was_down := false
var _b_was_down := false


class PreviewBackdrop extends Node2D:
	var dark_only := false
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, Vector2(VIEW_SIZE)), Color("28272a"))
		for row in range(2):
			for column in range(2):
				var panel := Rect2(24 + column * 628, 96 + row * 284, 604, 264)
				var light := row == 0 and not dark_only
				var ink := Color("e8dfd0") if light else Color("25282e")
				draw_rect(panel, ink)
				draw_rect(panel, Color("8e7d70") if light else Color("4c4b51"), false, 1.0)
				var ground_y := panel.position.y + 148
				var release_x := panel.position.x + 76 if column == 0 else panel.end.x - 76
				draw_circle(Vector2(release_x, ground_y), 3.0, Color("826860") if light else Color("a08d86"))
				draw_line(Vector2(release_x, ground_y), Vector2(release_x + (17 if column == 0 else -17), ground_y),
					Color("826860") if light else Color("a08d86"), 1.0)


func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			capture_dir = argument.trim_prefix("--capture-dir=").replace("\\", "/")
		elif argument.begins_with("--frames="):
			capture_frames = maxi(1, argument.trim_prefix("--frames=").to_int())
		elif argument.begins_with("--fps="):
			capture_fps = maxf(1.0, argument.trim_prefix("--fps=").to_float())
	call_deferred("setup")


func setup() -> void:
	root.size = VIEW_SIZE
	root.content_scale_size = VIEW_SIZE
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.title = "血浪 · 水墨美术候选"
	if not capture_dir.is_empty():
		root.position = Vector2i(-4096, -4096)
		root.gui_disable_input = true
	stage = Node2D.new()
	stage.name = "BloodWaveArtPreview"
	root.add_child(stage)
	current_scene = stage
	backdrop = PreviewBackdrop.new()
	stage.add_child(backdrop)
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei", "Noto Sans CJK SC"])
	_add_label("血浪 · 水墨美术候选", Vector2(28, 22), 27, Color("e9ddce"), font)
	for row in range(2):
		for column in range(2):
			var panel_origin := Vector2(24 + column * 628, 96 + row * 284)
			var origin := panel_origin + Vector2(76 if column == 0 else 528, 148)
			origins.append(origin)
			directions.append(Vector2.RIGHT if column == 0 else Vector2.LEFT)
			var wave: Node2D = Wave.new()
			wave.auto_advance = capture_dir.is_empty()
			wave.y_projection = 1.0 if row == 0 else 0.65
			wave.visual_scale = 0.56
			stage.add_child(wave)
			waves.append(wave)
			var label := ("俯视" if row == 0 else "浅斜视") + " · 120°" + (" · 向右" if column == 0 else " · 向左")
			_add_label(label, panel_origin + Vector2(16, 12), 17,
				Color("6c4e48") if row == 0 else Color("c6aaa2"), font)
	_add_label("空格重播   B切换底色   Esc退出", Vector2(28, 677), 16, Color("bdaea5"), font)
	ready_to_play = true
	replay()
	if not capture_dir.is_empty():
		capture()


func _add_label(text_value: String, at: Vector2, size: int, color: Color, font: Font) -> void:
	var label := Label.new()
	label.position = at
	label.text = text_value
	label.add_theme_font_override("font", font)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	stage.add_child(label)


func replay() -> void:
	preview_clock = 0.0
	for i in range(waves.size()):
		waves[i].play(origins[i], directions[i])


func _process(delta: float) -> bool:
	if not ready_to_play or not capture_dir.is_empty():
		return false
	preview_clock += delta
	if Input.is_physical_key_pressed(KEY_ESCAPE):
		quit()
	var space_down := Input.is_physical_key_pressed(KEY_SPACE)
	var b_down := Input.is_physical_key_pressed(KEY_B)
	if (space_down and not _space_was_down) or preview_clock >= CYCLE_SECONDS:
		replay()
	if b_down and not _b_was_down:
		backdrop.dark_only = not backdrop.dark_only
		backdrop.queue_redraw()
	_space_was_down = space_down
	_b_was_down = b_down
	return false


func capture() -> void:
	var result := DirAccess.make_dir_recursive_absolute(capture_dir)
	if result != OK:
		push_error("Cannot create capture directory: " + capture_dir)
		quit(3)
		return
	for i in range(3):
		await process_frame
	for frame in range(capture_frames):
		if frame > 0:
			for wave in waves:
				wave.advance(1.0 / capture_fps)
		await process_frame
		await RenderingServer.frame_post_draw
		var shot := root.get_texture().get_image()
		if shot == null or shot.is_empty():
			push_error("A real rendering window is required for blood-wave capture")
			quit(4)
			return
		result = shot.save_png(capture_dir.path_join("frame_%03d.png" % frame))
		if result != OK:
			push_error("Cannot save blood-wave capture frame")
			quit(5)
			return
	for wave in waves:
		wave.clear_visuals()
	print("BLOOD_WAVE_CAPTURE_OK frames=%d fps=%s directions=left,right backgrounds=paper,dark" % [capture_frames, capture_fps])
	quit(0)
