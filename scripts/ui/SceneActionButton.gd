extends Button

const PAPER: Texture2D = preload("res://textures/ui/world_ink_20261104/paper_panel.tres")
const MAX_SWAY_ANGLE := 0.16
const STEP := 1.0 / 120.0

@onready var tassel: Node2D = $Tassel

var _sway_angle := 0.0
var _sway_velocity := 0.0
var _rope_acceleration := 0.0
var _rendered_text := ""
var _plain_text := ""
var _hovered := false
var _batten := StyleBoxFlat.new()

func _ready() -> void:
	add_to_group("scene_action_tag")
	focus_mode = Control.FOCUS_ALL
	_build_skin()
	resized.connect(_sync_attachment)
	mouse_entered.connect(func() -> void: _hovered = true; queue_redraw())
	mouse_exited.connect(func() -> void: _hovered = false; queue_redraw())
	pressed.connect(func() -> void: apply_cursor_impulse(0.11))
	_sync_attachment()
	set_action_label(text)

func _build_skin() -> void:
	_batten.border_color = Color("39483d", 0.8)
	_batten.set_border_width_all(1)
	_batten.set_corner_radius_all(2)
	for state in ["normal", "hover", "pressed", "disabled", "focus"]:
		var style := StyleBoxTexture.new()
		style.texture = PAPER
		style.texture_margin_left = 26
		style.texture_margin_right = 26
		style.texture_margin_top = 18
		style.texture_margin_bottom = 38
		style.content_margin_left = 14
		style.content_margin_right = 14
		style.content_margin_top = 30
		style.content_margin_bottom = 26
		style.modulate_color = Color("e3ecda") if state in ["hover", "pressed", "focus"] else Color.WHITE
		if state == "disabled":
			style.modulate_color = Color("c8c8bb")
		add_theme_stylebox_override(state, style)
	for state in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		add_theme_color_override(state, Color("303d38"))
	add_theme_color_override("font_disabled_color", Color("777d71"))
	add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	add_theme_color_override("font_shadow_color", Color.TRANSPARENT)
	add_theme_constant_override("outline_size", 0)

func _process(delta: float) -> void:
	if not is_visible_in_tree():
		return
	if text != _rendered_text:
		set_action_label(text)
	var remaining := minf(delta, 0.05)
	while remaining > 0.00001:
		var dt := minf(STEP, remaining)
		var acceleration := -24.0 * sin(_sway_angle) - 6.4 * _sway_velocity \
			- _rope_acceleration / maxf(55.0, size.y * 0.55)
		_sway_velocity += acceleration * dt
		_sway_angle += _sway_velocity * dt
		if absf(_sway_angle) > MAX_SWAY_ANGLE:
			_sway_angle = clampf(_sway_angle, -MAX_SWAY_ANGLE, MAX_SWAY_ANGLE)
			_sway_velocity *= 0.35
		if is_instance_valid(tassel):
			tassel.set("parent_acceleration", acceleration)
		remaining -= dt
	rotation = _sway_angle

func set_action_label(value: String) -> void:
	_plain_text = value.replace("\n", "").replace(" ", "").replace("·", "")
	var vertical_lines := PackedStringArray()
	for i in range(_plain_text.length()):
		vertical_lines.append(_plain_text.substr(i, 1))
	_rendered_text = "\n".join(vertical_lines)
	text = _rendered_text
	_fit_text()

func _fit_text() -> void:
	var font := get_theme_font("font")
	var font_size := 25
	while font_size > 13 and font.get_multiline_string_size(text, HORIZONTAL_ALIGNMENT_CENTER, -1, font_size).y > size.y - 56.0:
		font_size -= 1
	add_theme_font_size_override("font_size", font_size)

func set_rope_acceleration(value: float) -> void:
	_rope_acceleration = clampf(value, -650.0, 650.0)

func apply_cursor_impulse(impulse: float) -> void:
	_sway_velocity = clampf(_sway_velocity + impulse, -1.8, 1.8)
	if is_instance_valid(tassel):
		tassel.call("apply_impulse", -impulse * 1.3)

func _sync_attachment() -> void:
	pivot_offset = Vector2(size.x * 0.5, 13.0)
	if is_instance_valid(tassel):
		tassel.position = Vector2(size.x * 0.5, size.y - 2.0)
	_fit_text()
	queue_redraw()

func _draw() -> void:
	var center_x := size.x * 0.5
	_batten.bg_color = Color("657065") if not _hovered else Color("496854")
	# Narrow wooden battens hold the paper; the cord passes through their eye.
	draw_style_box(_batten, Rect2(5, 5, size.x - 10, 16))
	draw_style_box(_batten, Rect2(5, size.y - 12, size.x - 10, 8))
	draw_line(Vector2(10, 7), Vector2(size.x - 10, 7), Color("b8beaa", 0.75), 0.8, true)
	draw_circle(Vector2(center_x, 13), 3.4, Color("3c443b"))
	draw_arc(Vector2(center_x, 13), 4.5, 0, TAU, 20, Color("b5b49b"), 1.1, true)
	draw_line(Vector2(center_x, 7), Vector2(center_x, 14), Color("a79e80"), 1.5, true)
	draw_line(Vector2(13, 28), Vector2(size.x - 13, 28), Color("58715d", 0.22), 0.8, true)
	draw_line(Vector2(13, size.y - 24), Vector2(size.x - 13, size.y - 24), Color("58715d", 0.22), 0.8, true)
