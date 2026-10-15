extends Node2D

const ART_ROOT := "res://textures/characters/side_ink_chop_sample_20261015_v2/"
const CANVAS_SIZE := Vector2(2048.0, 2048.0)
const GROUND_ORIGIN := Vector2(1024.0, 1900.0)
const REFERENCE_HEIGHT := 1375.0
const MOTION_ENVELOPE_WIDTH := 1600.0
const CYCLE_DURATION := 3.0
const WINDUP_START := 1.10
const STRIKE_START := 1.28
const IMPACT_START := 1.38
const RECOVER_START := 1.50
const RECOVER_END := 1.82

class InkOverlay extends Node2D:
	var preview: Node2D
	var actor_index := 0
	func _draw() -> void:
		if is_instance_valid(preview):
			preview._paint_actor_effects(self, actor_index)

var actors: Array[Dictionary] = []
var hud_layer: CanvasLayer
var title_label: Label
var subtitle_label: Label
var status_label: Label
var name_labels: Array[Label] = []
var controls_label: Label
var buttons_row: HBoxContainer
var pause_button: Button
var cycle_time := 0.0
var phase_name := "idle"
var paused := false
var background_mode := 0
var simulate_missing_action := false
var mirrored := false
var show_guides := false
var display_size_mode := 0

func _ready() -> void:
	z_index = -20
	_create_actor("male", 0)
	_create_actor("female", 1)
	_create_hud()
	get_viewport().size_changed.connect(_layout)
	_layout()

func _process(delta: float) -> void:
	if paused:
		return
	cycle_time = fposmod(cycle_time + delta, CYCLE_DURATION)
	_refresh_cycle()

func _unhandled_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	match event.keycode:
		KEY_SPACE:
			_toggle_pause()
		KEY_R:
			_restart()
		KEY_B:
			_cycle_background()
		KEY_F:
			_toggle_missing()
		KEY_M:
			_toggle_mirror()
		KEY_G:
			_toggle_guides()
		KEY_V:
			_cycle_display_size()
		KEY_ESCAPE:
			_return_home()
		_:
			return
	get_viewport().set_input_as_handled()

func _load_pose(actor_id: String, pose: String) -> Texture2D:
	var path := ART_ROOT + actor_id + "_" + pose + ".png"
	if not ResourceLoader.exists(path):
		return null
	return load(path) as Texture2D

func _create_actor(actor_id: String, index: int) -> void:
	var root := Node2D.new()
	root.name = actor_id.capitalize() + "GroundOrigin"
	root.z_as_relative = false
	root.z_index = 0
	add_child(root)
	var idle_sprite := _make_sprite(actor_id.capitalize() + "Idle", _load_pose(actor_id, "idle"))
	var chop_sprite := _make_sprite(actor_id.capitalize() + "Chop", _load_pose(actor_id, "chop"))
	root.add_child(idle_sprite)
	root.add_child(chop_sprite)
	var overlay := InkOverlay.new()
	overlay.name = "IndependentInkEffects"
	overlay.preview = self
	overlay.actor_index = index
	overlay.z_index = 2
	root.add_child(overlay)
	actors.append({
		"id": actor_id, "root": root, "idle": idle_sprite, "chop": chop_sprite,
		"overlay": overlay, "base_position": Vector2.ZERO, "base_scale": 1.0
	})

func _make_sprite(node_name: String, texture: Texture2D) -> Sprite2D:
	var sprite := Sprite2D.new()
	sprite.name = node_name
	sprite.texture = texture
	sprite.centered = false
	sprite.offset = -GROUND_ORIGIN
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	sprite.modulate.a = 0.0
	return sprite

func _create_hud() -> void:
	hud_layer = CanvasLayer.new()
	hud_layer.layer = 5
	add_child(hud_layer)
	title_label = _make_label("局内水墨姿势样片 · 男女", 30)
	subtitle_label = _make_label("简化衣褶 · 留白侧脸 · 掌心朝镜头下劈", 18)
	status_label = _make_label("", 16)
	name_labels.append(_make_label("男修 · 碧岚云叶袍", 22))
	name_labels.append(_make_label("女修 · 墨青云叶衣", 22))
	controls_label = _make_label("Space 暂停　R 重播　M 镜像　B 背景　G 边界　V 局内尺寸　F 缺图回退", 15)
	buttons_row = HBoxContainer.new()
	buttons_row.add_theme_constant_override("separation", 8)
	hud_layer.add_child(buttons_row)
	_add_button("重播下劈", _replay_strike)
	pause_button = _add_button("暂停", _toggle_pause)
	_add_button("镜像朝向", _toggle_mirror)
	_add_button("浅／深背景", _cycle_background)
	_add_button("画布边界", _toggle_guides)
	_add_button("局内尺寸", _cycle_display_size)
	_add_button("返回首页", _return_home)

func _make_label(text_value: String, font_size: int) -> Label:
	var label := Label.new()
	label.text = text_value
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", Color8(232, 225, 207))
	var font := load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf") as Font
	if font:
		label.add_theme_font_override("font", font)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud_layer.add_child(label)
	return label

func _add_button(text_value: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = text_value
	button.custom_minimum_size = Vector2(110.0, 38.0)
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.focus_mode = Control.FOCUS_NONE
	button.add_theme_font_size_override("font_size", 16)
	button.add_theme_color_override("font_color", Color8(34, 57, 50))
	var style := StyleBoxFlat.new()
	style.bg_color = Color8(221, 211, 187)
	style.border_color = Color8(87, 108, 93)
	style.set_border_width_all(1)
	style.set_corner_radius_all(4)
	button.add_theme_stylebox_override("normal", style)
	var hover := style.duplicate() as StyleBoxFlat
	hover.bg_color = Color8(241, 232, 210)
	button.add_theme_stylebox_override("hover", hover)
	button.pressed.connect(action)
	buttons_row.add_child(button)
	return button

func _layout() -> void:
	var size := get_viewport_rect().size
	if size.x <= 0.0 or size.y <= 0.0:
		return
	# The idle body height controls scale. Canvas padding and pose bounds do not.
	var target_height := minf(clampf(size.y - 350.0, 190.0, 590.0), maxf(190.0, (size.x * 0.5 - 100.0) * REFERENCE_HEIGHT / MOTION_ENVELOPE_WIDTH))
	if display_size_mode == 1:
		target_height = 190.0
	elif display_size_mode == 2:
		target_height = 320.0
	var scale_factor := target_height / REFERENCE_HEIGHT
	for index in range(actors.size()):
		actors[index]["base_position"] = Vector2(size.x * (0.25 if index == 0 else 0.75), size.y - 180.0)
		actors[index]["base_scale"] = scale_factor
	title_label.position = Vector2(0.0, 15.0)
	title_label.size = Vector2(size.x, 44.0)
	subtitle_label.position = Vector2(0.0, 62.0)
	subtitle_label.size = Vector2(size.x, 30.0)
	status_label.position = Vector2(0.0, 101.0)
	status_label.size = Vector2(size.x, 30.0)
	for index in range(name_labels.size()):
		name_labels[index].position = Vector2(size.x * float(index) * 0.5, size.y - 155.0)
		name_labels[index].size = Vector2(size.x * 0.5, 36.0)
	buttons_row.position = Vector2(30.0, size.y - 109.0)
	buttons_row.size = Vector2(size.x - 60.0, 38.0)
	controls_label.position = Vector2(0.0, size.y - 58.0)
	controls_label.size = Vector2(size.x, 38.0)
	_update_name_colors()
	_refresh_cycle()

func _refresh_cycle() -> void:
	if cycle_time < WINDUP_START:
		phase_name = "idle"
	elif cycle_time < STRIKE_START:
		phase_name = "windup"
	elif cycle_time < IMPACT_START:
		phase_name = "strike"
	elif cycle_time < RECOVER_START:
		phase_name = "impact"
	elif cycle_time < RECOVER_END:
		phase_name = "recover"
	else:
		phase_name = "idle"
	_apply_actor_poses()
	_refresh_status()
	queue_redraw()
	for actor in actors:
		actor["overlay"].queue_redraw()

func _apply_actor_poses() -> void:
	for actor in actors:
		var root: Node2D = actor["root"]
		var idle: Sprite2D = actor["idle"]
		var chop: Sprite2D = actor["chop"]
		var scale_factor: float = actor["base_scale"]
		var direction := -1.0 if mirrored else 1.0
		var local_offset := Vector2.ZERO
		var use_chop := phase_name in ["strike", "impact"]
		match phase_name:
			"windup":
				var t := clampf((cycle_time - WINDUP_START) / (STRIKE_START - WINDUP_START), 0.0, 1.0)
				local_offset.x = -30.0 * t
			"strike":
				var t := clampf((cycle_time - STRIKE_START) / (IMPACT_START - STRIKE_START), 0.0, 1.0)
				local_offset.x = lerpf(-30.0, 12.0, 1.0 - pow(1.0 - t, 3.0))
			"impact":
				local_offset.x = 12.0
			"recover":
				var t := clampf((cycle_time - RECOVER_START) / (RECOVER_END - RECOVER_START), 0.0, 1.0)
				local_offset.x = lerpf(12.0, 0.0, t)
				use_chop = t < 0.55
		var available := chop.texture != null and not simulate_missing_action
		idle.visible = not (use_chop and available)
		chop.visible = use_chop and available
		idle.modulate.a = 1.0
		chop.modulate.a = 1.0
		root.position = actor["base_position"] + Vector2(local_offset.x * direction, local_offset.y) * scale_factor
		root.scale = Vector2(direction * scale_factor, scale_factor)

func _refresh_status() -> void:
	var phase_text: String = {"idle":"待机", "windup":"短促蓄势", "strike":"手刀下劈", "impact":"命中停顿", "recover":"回到待机"}.get(phase_name, "待机")
	var size_text := "自由预览" if display_size_mode == 0 else ("190 像素" if display_size_mode == 1 else "320 像素")
	status_label.text = ("已暂停 · " if paused else "") + phase_text + "　｜　" + size_text
	if simulate_missing_action:
		status_label.text += "　｜　缺图回退"
	if pause_button:
		pause_button.text = "继续" if paused else "暂停"

func _toggle_pause() -> void:
	paused = not paused
	_refresh_status()

func _restart() -> void:
	cycle_time = 0.0
	paused = false
	_refresh_cycle()

func _replay_strike() -> void:
	cycle_time = WINDUP_START
	paused = false
	_refresh_cycle()

func _cycle_background() -> void:
	background_mode = (background_mode + 1) % 3
	_update_name_colors()
	_refresh_cycle()

func _toggle_missing() -> void:
	simulate_missing_action = not simulate_missing_action
	_refresh_cycle()

func _toggle_mirror() -> void:
	mirrored = not mirrored
	_refresh_cycle()

func _toggle_guides() -> void:
	show_guides = not show_guides
	_refresh_cycle()

func _cycle_display_size() -> void:
	display_size_mode = (display_size_mode + 1) % 3
	_layout()

func _return_home() -> void:
	get_tree().change_scene_to_file("res://tscn/scene/StartMenu.tscn")

func _panel_dark(index: int) -> bool:
	return background_mode == 2 or (background_mode == 0 and index == 1)

func _update_name_colors() -> void:
	for index in range(name_labels.size()):
		name_labels[index].add_theme_color_override("font_color", Color8(231, 226, 208) if _panel_dark(index) else Color8(35, 52, 46))

func _draw() -> void:
	var size := get_viewport_rect().size
	draw_rect(Rect2(Vector2.ZERO, size), Color8(20, 26, 26))
	for index in range(2):
		var color := Color8(24, 30, 30) if _panel_dark(index) else Color8(229, 222, 206)
		draw_rect(Rect2(size.x * 0.5 * index, 145.0, size.x * 0.5, maxf(0.0, size.y - 265.0)), color)
		var line_color := Color(0.8, 0.79, 0.72, 0.35) if _panel_dark(index) else Color(0.15, 0.23, 0.2, 0.28)
		draw_line(Vector2(size.x * index * 0.5 + 50.0, size.y - 180.0), Vector2(size.x * (index + 1) * 0.5 - 50.0, size.y - 180.0), line_color, 1.0, true)
	draw_line(Vector2(size.x * 0.5, 145.0), Vector2(size.x * 0.5, size.y - 120.0), Color(0.5, 0.54, 0.48, 0.45), 1.0)

func _paint_actor_effects(target: CanvasItem, index: int) -> void:
	var ink := Color(0.86, 0.83, 0.73, 0.96) if _panel_dark(index) else Color(0.07, 0.13, 0.12, 0.96)
	var accent := Color(0.49, 0.65, 0.57, 0.85) if _panel_dark(index) else Color(0.28, 0.42, 0.35, 0.85)
	if show_guides:
		target.draw_rect(Rect2(-GROUND_ORIGIN, CANVAS_SIZE), Color(0.6, 0.48, 0.2, 0.7), false, 3.0)
		target.draw_line(Vector2(-36.0, 0.0), Vector2(36.0, 0.0), Color(0.8, 0.35, 0.16), 5.0)
		target.draw_line(Vector2(0.0, -36.0), Vector2(0.0, 36.0), Color(0.8, 0.35, 0.16), 5.0)
	if phase_name not in ["strike", "impact", "recover"]:
		return
	var progress := 1.0
	var alpha := 1.0
	if phase_name == "strike":
		progress = clampf((cycle_time - STRIKE_START) / (IMPACT_START - STRIKE_START), 0.0, 1.0)
	elif phase_name == "recover":
		alpha = 1.0 - clampf((cycle_time - RECOVER_START) / (RECOVER_END - RECOVER_START), 0.0, 1.0)
	var start := Vector2(170.0, -1060.0)
	var hit := Vector2(650.0, -355.0)
	_paint_slash(target, start, hit, progress, ink, accent, alpha)
	if phase_name in ["impact", "recover"]:
		_paint_impact(target, hit, ink, accent, alpha)

func _slash_point(start: Vector2, hit: Vector2, t: float) -> Vector2:
	var inverse := 1.0 - t
	var a := start + Vector2(360.0, 30.0)
	var b := hit + Vector2(135.0, -325.0)
	return inverse * inverse * inverse * start + 3.0 * inverse * inverse * t * a + 3.0 * inverse * t * t * b + t * t * t * hit

func _paint_slash(target: CanvasItem, start: Vector2, hit: Vector2, progress: float, ink: Color, accent: Color, alpha: float) -> void:
	if progress <= 0.001:
		return
	var left := PackedVector2Array()
	var right := PackedVector2Array()
	for index in range(97):
		var t := progress * index / 96.0
		var point := _slash_point(start, hit, t)
		var tangent := (_slash_point(start, hit, minf(t + 0.01, 1.0)) - _slash_point(start, hit, maxf(0.0, t - 0.01))).normalized()
		var normal := Vector2(-tangent.y, tangent.x)
		var width := (18.0 + 88.0 * sin(PI * t)) * (0.96 + 0.04 * sin(t * 91.0))
		left.append(point + normal * width)
		right.append(point - normal * width * 0.48)
	right.reverse()
	left.append_array(right)
	target.draw_colored_polygon(left, _with_alpha(ink, alpha))
	for strand in range(4):
		var points := PackedVector2Array()
		for index in range(23):
			var t := progress * index / 22.0
			if strand == 2 and index in [8, 9, 14]:
				continue
			points.append(_slash_point(start, hit, t) + Vector2(-33.0 + strand * 19.0, sin(index * 2.2 + strand) * 7.0))
		if points.size() > 1:
			target.draw_polyline(points, _with_alpha(accent, alpha * (0.4 if strand == 1 else 0.72)), 5.0 + strand * 2.0, true)

func _paint_impact(target: CanvasItem, center: Vector2, ink: Color, accent: Color, alpha: float) -> void:
	if alpha <= 0.0:
		return
	var blot := PackedVector2Array()
	for index in range(24):
		var angle := TAU * index / 24.0
		var radius := 38.0 + float((index * 17) % 32)
		blot.append(center + Vector2.from_angle(angle) * radius)
	target.draw_colored_polygon(blot, _with_alpha(ink, alpha * 0.9))
	for index in range(13):
		var angle := TAU * index / 13.0 - 0.4
		var ray := Vector2.from_angle(angle)
		var normal := Vector2(-ray.y, ray.x)
		var length := 96.0 + float((index * 47) % 145)
		var triangle := PackedVector2Array([center + ray * 36.0 + normal * 8.0, center + ray * length, center + ray * 45.0 - normal * 5.0])
		target.draw_colored_polygon(triangle, _with_alpha(ink if index % 3 else accent, alpha * 0.78))
		var drop := center + ray * (length + 29.0)
		target.draw_circle(drop, 5.0 + index % 3 * 3.0, _with_alpha(ink, alpha * 0.8))
	target.draw_arc(center, 81.0, -0.3, 2.2, 18, _with_alpha(accent, alpha * 0.72), 8.0, true)

func _with_alpha(color: Color, multiplier: float) -> Color:
	var result := color
	result.a *= multiplier
	return result
