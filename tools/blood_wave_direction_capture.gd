extends SceneTree
## Eight simultaneous visual samples, each a single forward sector.
## Run with -- --capture-dir=E:/tmp/blood_wave_directions --frames=78 --fps=30
## Or -- --verify-only for geometry / lifecycle checks without image capture.

const Wave = preload("res://tools/action_battle_test/blood_wave_vfx.gd")
const VIEW_SIZE := Vector2i(1200, 850)
const DISPLAY_SCALE := 0.55
const Y_PROJECTION := 0.46
const CYCLE_SECONDS := 2.60
const CELLS := [Vector2i(0, 0), Vector2i(1, 0), Vector2i(2, 0),
	Vector2i(0, 1), Vector2i(2, 1), Vector2i(0, 2), Vector2i(1, 2), Vector2i(2, 2)]
const DIRECTIONS := [Vector2(-1, -1), Vector2.UP, Vector2(1, -1),
	Vector2.LEFT, Vector2.RIGHT, Vector2(-1, 1), Vector2.DOWN, Vector2(1, 1)]
const CAPTIONS := ["左上", "向上", "右上", "向左", "向右", "左下", "向下", "右下"]

var stage: Node2D
var waves: Array[Node2D] = []
var origins: Array[Vector2] = []
var capture_dir := ""
var capture_frames := 78
var capture_fps := 30.0
var verify_only := false
var ready_to_play := false
var preview_clock := 0.0
var _space_was_down := false
var _checks := 0
var _vertices := 0
var _failures: Array[String] = []


class Backdrop extends Node2D:
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, Vector2(VIEW_SIZE)), Color("e7decd"))
		for index in range(CELLS.size()):
			var cell: Vector2i = CELLS[index]
			var rect := Rect2(18 + cell.x * 394, 88 + cell.y * 242, 376, 226)
			draw_rect(rect, Color("f0e8d9"))
			draw_rect(rect, Color("c6b5a1"), false, 1.0)
			var origin := rect.position + Vector2(188, 126)
			var direction: Vector2 = DIRECTIONS[index].normalized()
			var projected := Vector2(direction.x, direction.y * Y_PROJECTION).normalized()
			draw_circle(origin, 2.5, Color("71564c"))
			draw_line(origin, origin + projected * 18.0, Color("71564c"), 1.0)
			var tip := origin + projected * 18.0
			draw_line(tip, tip - projected.rotated(0.5) * 5.0, Color("71564c"), 1.0)
			draw_line(tip, tip - projected.rotated(-0.5) * 5.0, Color("71564c"), 1.0)


func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			capture_dir = argument.trim_prefix("--capture-dir=").replace("\\", "/")
		elif argument.begins_with("--frames="):
			capture_frames = maxi(1, argument.trim_prefix("--frames=").to_int())
		elif argument.begins_with("--fps="):
			capture_fps = maxf(1.0, argument.trim_prefix("--fps=").to_float())
		elif argument == "--verify-only":
			verify_only = true
	call_deferred("setup")


func setup() -> void:
	root.size = VIEW_SIZE
	root.content_scale_size = VIEW_SIZE
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.title = "八向透视核验"
	if verify_only or not capture_dir.is_empty():
		root.position = Vector2i(-4096, -4096)
		root.gui_disable_input = true
	stage = Node2D.new()
	stage.name = "BloodWaveDirectionPreview"
	root.add_child(stage)
	current_scene = stage
	if not _verify_geometry_and_cleanup():
		quit(2)
		return
	if verify_only:
		quit(0)
		return
	stage.add_child(Backdrop.new())
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei", "Noto Sans CJK SC"])
	_add_label("八向透视核验", Vector2(24, 22), 27, font)
	for index in range(CELLS.size()):
		var cell: Vector2i = CELLS[index]
		var panel_origin := Vector2(18 + cell.x * 394, 88 + cell.y * 242)
		var origin := panel_origin + Vector2(188, 126)
		origins.append(origin)
		var wave: Node2D = Wave.new()
		wave.auto_advance = false
		wave.y_projection = Y_PROJECTION
		wave.visual_scale = DISPLAY_SCALE
		wave.crest_lift = 0.0
		_disable_particles(wave)
		stage.add_child(wave)
		waves.append(wave)
		_add_label(CAPTIONS[index], panel_origin + Vector2(14, 10), 18, font)
	_add_label("120°扇形血浪", Vector2(500, 420), 24, font)
	_add_label("各向独立放浪", Vector2(510, 462), 20, font)
	_add_label("空格重播 · Esc退出", Vector2(24, 822), 15, font)
	ready_to_play = true
	replay()
	if not capture_dir.is_empty():
		capture()


func _add_label(value: String, at: Vector2, size: int, font: Font) -> void:
	var label := Label.new()
	label.text = value
	label.position = at
	label.add_theme_font_override("font", font)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", Color("63443e"))
	stage.add_child(label)


func _check(condition: bool, message: String) -> void:
	_checks += 1
	if not condition:
		_failures.append(message)


func _disable_particles(wave: Node2D) -> void:
	# Keep this verifier usable before / after the optional ink-particle hook lands.
	for property in wave.get_property_list():
		if property.name == "particles_enabled":
			wave.set("particles_enabled", false)
			return


func _verify_geometry_and_cleanup() -> bool:
	var probe: Node2D = Wave.new()
	probe.auto_advance = false
	probe.crest_lift = 0.0
	probe.y_projection = Y_PROJECTION
	probe.visual_scale = DISPLAY_SCALE
	_disable_particles(probe)
	stage.add_child(probe)
	var source := Vector2(37, -61)
	var times := [0.10, 0.30, 0.60, 0.80, 1.10, 1.50]
	for direction_index in range(DIRECTIONS.size()):
		var direction: Vector2 = DIRECTIONS[direction_index].normalized()
		probe.play(source, direction)
		var previous := 0.0
		for at_age in times:
			probe.advance(float(at_age) - previous)
			previous = float(at_age)
			var largest_radius := 0.0
			var largest_angle := 0.0
			var case_vertices := 0
			for child in probe.get_children():
				if not child is MeshInstance2D or not child.visible:
					continue
				var mesh := child.mesh as ArrayMesh
				if mesh == null:
					continue
				for surface in range(mesh.get_surface_count()):
					var arrays := mesh.surface_get_arrays(surface)
					var points: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
					for point in points:
						var screen: Vector2 = (child.global_transform * Vector2(point.x, point.y) - source) / DISPLAY_SCALE
						var ground := Vector2(screen.x, screen.y / Y_PROJECTION)
						largest_radius = maxf(largest_radius, ground.length())
						if ground.length_squared() > 0.00001:
							largest_angle = maxf(largest_angle, absf(direction.angle_to(ground.normalized())))
						case_vertices += 1
			_vertices += case_vertices
			var context := "%s at %.2fs" % [CAPTIONS[direction_index], at_age]
			_check(case_vertices > 0, context + " has no visible geometry")
			_check(largest_radius <= probe.max_radius + 0.03,
				context + " radius exceeds 220: %.4f" % largest_radius)
			_check(largest_angle <= deg_to_rad(probe.fan_degrees * 0.5) + 0.0001,
				context + " exceeds forward half-angle: %.4f°" % rad_to_deg(largest_angle))
		probe.stop()
		_check(_is_clear(probe), CAPTIONS[direction_index] + " stop leaves visible geometry")
		probe.play(source, direction)
		probe.advance(0.40)
		probe.clear_visuals()
		_check(_is_clear(probe) and is_zero_approx(probe.age),
			CAPTIONS[direction_index] + " clear does not reset")
		probe.play(source, direction)
		probe.advance(probe.duration() + 0.10)
		_check(_is_clear(probe), CAPTIONS[direction_index] + " completion leaves visible geometry")
	probe.free()
	if not _failures.is_empty():
		for failure in _failures:
			push_error(failure)
		print("BLOOD_WAVE_DIRECTION_CHECKS_FAIL checks=%d failures=%d vertices=%d" % [_checks, _failures.size(), _vertices])
		return false
	print("BLOOD_WAVE_DIRECTION_CHECKS_OK directions=8 samples_per_direction=6 checks=%d vertices=%d radius=220 fan=120 projection=0.46" % [_checks, _vertices])
	return true


func _is_clear(wave: Node2D) -> bool:
	if wave.playing or wave.visible:
		return false
	for child in wave.get_children():
		if child is MeshInstance2D and child.visible:
			return false
	return true


func replay() -> void:
	preview_clock = 0.0
	for index in range(waves.size()):
		waves[index].play(origins[index], DIRECTIONS[index].normalized())


func _process(delta: float) -> bool:
	if not ready_to_play or not capture_dir.is_empty():
		return false
	for wave in waves:
		wave.advance(delta)
	preview_clock += delta
	if Input.is_physical_key_pressed(KEY_ESCAPE):
		quit()
	var space_down := Input.is_physical_key_pressed(KEY_SPACE)
	if (space_down and not _space_was_down) or preview_clock >= CYCLE_SECONDS:
		replay()
	_space_was_down = space_down
	return false


func capture() -> void:
	if DirAccess.make_dir_recursive_absolute(capture_dir) != OK:
		push_error("Cannot create direction capture directory: " + capture_dir)
		quit(3)
		return
	var info := {
		"view_size": [VIEW_SIZE.x, VIEW_SIZE.y],
		"frames": capture_frames,
		"fps": capture_fps,
		"fan_degrees": 120.0,
		"crest_lift": 0.0,
		"particles_enabled": false,
		"cases": [],
	}
	for index in range(CELLS.size()):
		var cell: Vector2i = CELLS[index]
		var panel := Rect2(18 + cell.x * 394, 88 + cell.y * 242, 376, 226)
		var direction: Vector2 = DIRECTIONS[index].normalized()
		info.cases.append({
			"name": CAPTIONS[index],
			"origin": [origins[index].x, origins[index].y],
			"direction": [direction.x, direction.y],
			"direction_angle": direction.angle(),
			"direction_degrees": rad_to_deg(direction.angle()),
			"visual_scale": DISPLAY_SCALE,
			"y_projection": Y_PROJECTION,
			"max_radius": waves[index].max_radius,
			"panel_rect": [panel.position.x, panel.position.y, panel.size.x, panel.size.y],
		})
	var metadata := FileAccess.open(capture_dir.path_join("capture_info.json"), FileAccess.WRITE)
	if metadata == null:
		push_error("Cannot save direction capture metadata")
		quit(6)
		return
	metadata.store_string(JSON.stringify(info, "\t"))
	metadata.close()
	for warmup in range(3):
		await process_frame
	for frame in range(capture_frames):
		if frame > 0:
			for wave in waves:
				wave.advance(1.0 / capture_fps)
		await process_frame
		await RenderingServer.frame_post_draw
		var shot := root.get_texture().get_image()
		if shot == null or shot.is_empty():
			push_error("A rendering window is required for direction capture")
			quit(4)
			return
		if shot.save_png(capture_dir.path_join("frame_%03d.png" % frame)) != OK:
			push_error("Cannot save direction capture frame")
			quit(5)
			return
	for wave in waves:
		wave.clear_visuals()
	print("BLOOD_WAVE_DIRECTION_CAPTURE_OK frames=%d fps=%s directions=8 projection=0.46" % [capture_frames, capture_fps])
	quit(0)
