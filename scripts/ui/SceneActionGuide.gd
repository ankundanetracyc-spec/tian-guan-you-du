extends Control

# Compact coachmark: native shapes and text, separate from the hanging sign art.
var _title: Label
var _detail: Label
var _step: Label
var _surface := StyleBoxFlat.new()
var _instruction := ""

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_surface.bg_color = Color("dce5df", 0.96)
	_surface.border_color = Color("72877d", 0.78)
	_surface.set_border_width_all(1)
	_surface.set_corner_radius_all(8)
	_step = _label("Step", 22, Color("396359"))
	_step.text = "1"
	_step.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_step.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_title = _label("Title", 20, Color("304e43"))
	_detail = _label("Detail", 16, Color("46564c"))
	resized.connect(_layout)
	_layout()

func _label(node_name: String, font_size: int, ink: Color) -> Label:
	var label := Label.new()
	label.name = node_name
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.clip_text = true
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", ink)
	add_child(label)
	return label

func set_instruction(value: String) -> void:
	_instruction = value
	var lines := value.split("\n", true, 1)
	_title.text = lines[0]
	_detail.text = lines[1] if lines.size() > 1 else ""

func get_instruction_text() -> String:
	return _instruction

func _layout() -> void:
	if not is_instance_valid(_title):
		return
	_step.position = Vector2(15, 17)
	_step.size = Vector2(36, 40)
	_title.position = Vector2(63, 12)
	_title.size = Vector2(maxf(0, size.x - 80), 28)
	_detail.position = Vector2(63, 45)
	_detail.size = Vector2(maxf(0, size.x - 80), 25)
	queue_redraw()

func _draw() -> void:
	draw_style_box(_surface, Rect2(Vector2.ZERO, size))
	draw_circle(Vector2(33, 37), 17, Color("c0d0c4"))
	draw_arc(Vector2(33, 37), 17, 0, TAU, 36, Color("829b8c"), 1, true)
	var x := size.x * 0.5
	var start := Vector2(x, size.y + 4)
	var tip := Vector2(x, size.y + 27)
	draw_line(start, tip, Color("496e5d"), 1.8, true)
	draw_polyline(PackedVector2Array([tip + Vector2(-5, -6), tip, tip + Vector2(5, -6)]), Color("496e5d"), 1.8, true)
