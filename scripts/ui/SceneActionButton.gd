extends Button

const MAX_SWAY_ANGLE := 0.14
const SWAY_SPRING := 22.0
const SWAY_DAMPING := 6.0

@onready var tassel: Node2D = $Tassel

var _sway_angle := 0.0
var _sway_velocity := 0.0
var _rendered_text := ""

func _ready() -> void:
	add_to_group("scene_action_tag")
	focus_mode = Control.FOCUS_NONE
	resized.connect(_sync_attachment)
	_sync_attachment()
	set_action_label(text)

func _process(delta: float) -> void:
	if text != _rendered_text:
		set_action_label(text)
	_sway_velocity += (-_sway_angle * SWAY_SPRING - _sway_velocity * SWAY_DAMPING) * delta
	_sway_angle = clampf(_sway_angle + _sway_velocity * delta, -MAX_SWAY_ANGLE, MAX_SWAY_ANGLE)
	rotation = _sway_angle
	if absf(_sway_angle) > 0.001 or absf(_sway_velocity) > 0.002:
		queue_redraw()

func set_action_label(value: String) -> void:
	var plain_text := value.replace("\n", "").replace(" ", "").replace("·", "")
	var vertical_lines := PackedStringArray()
	for index in range(plain_text.length()):
		vertical_lines.append(plain_text.substr(index, 1))
	_rendered_text = "\n".join(vertical_lines)
	text = _rendered_text
	var font_size := 25 if plain_text.length() <= 5 else (22 if plain_text.length() <= 8 else 19)
	add_theme_font_size_override("font_size", font_size)

func apply_cursor_impulse(impulse: float) -> void:
	_sway_velocity = clampf(_sway_velocity + impulse, -2.2, 2.2)
	if is_instance_valid(tassel) and tassel.has_method("apply_impulse"):
		tassel.call("apply_impulse", -impulse * 2.0)

func _sync_attachment() -> void:
	pivot_offset = Vector2(size.x * 0.5, 0.0)
	if is_instance_valid(tassel):
		tassel.position = Vector2(size.x * 0.5, size.y - 2.0)
	queue_redraw()

func _draw() -> void:
	var center_x := size.x * 0.5
	var ring_color := Color(0.39, 0.31, 0.22, 0.9)
	draw_arc(Vector2(center_x, 1.0), 6.5, 0.0, TAU, 18, ring_color, 1.8, true)
	draw_circle(Vector2(center_x, size.y - 1.0), 2.6, Color(0.57, 0.2, 0.16, 0.94))
