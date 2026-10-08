extends Control

@export var tag_size := Vector2(84.0, 190.0)
@export var tag_gap := 58.0
@export_range(0.0, 1.0, 0.001) var tag_top_ratio := 0.365
@export_range(0.0, 1.0, 0.001) var rope_edge_height_ratio := 0.275
@export_range(0.0, 0.3, 0.001) var rope_sag_ratio := 0.09
const POINTER_INFLUENCE_RADIUS := 165.0
const POINTER_IMPULSE_SCALE := 0.0105

@export var action_container_path: NodePath = NodePath("..")

var _action_container: Node
var _last_visible_tag_ids: Array[int] = []

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_action_container = get_node_or_null(action_container_path)
	resized.connect(_on_resized)
	queue_redraw()
	_layout_tags()

func _process(_delta: float) -> void:
	if not is_visible_in_tree():
		return
	var current_tag_ids: Array[int] = []
	for button in _get_visible_tags():
		current_tag_ids.append(button.get_instance_id())
	if current_tag_ids != _last_visible_tag_ids:
		_last_visible_tag_ids = current_tag_ids
		_layout_tags()
		queue_redraw()

func _input(event: InputEvent) -> void:
	if not is_visible_in_tree() or not (event is InputEventMouseMotion):
		return
	var horizontal_motion := clampf(event.relative.x, -60.0, 60.0)
	if absf(horizontal_motion) < 0.1:
		return
	var mouse_position := get_global_mouse_position()
	for button in _get_visible_tags():
		var rect := button.get_global_rect()
		if mouse_position.y < rect.position.y - 24.0 or mouse_position.y > rect.end.y + 10.0:
			continue
		var distance := absf(mouse_position.x - rect.get_center().x)
		var influence := clampf(1.0 - distance / POINTER_INFLUENCE_RADIUS, 0.0, 1.0)
		if influence <= 0.0:
			continue
		var impulse := clampf(horizontal_motion * POINTER_IMPULSE_SCALE, -0.24, 0.24) * influence
		button.call("apply_cursor_impulse", impulse)

func _draw() -> void:
	if _get_visible_tags().is_empty() or size.x <= 0.0 or size.y <= 0.0:
		return
	var points := PackedVector2Array()
	var segment_count := 72
	for index in range(segment_count + 1):
		var t := float(index) / float(segment_count)
		var x := size.x * t
		points.append(Vector2(x, _rope_y(x)))
	draw_polyline(points, Color(0.34, 0.27, 0.2, 0.92), 4.0, true)
	var highlight_points := PackedVector2Array()
	for point in points:
		highlight_points.append(point + Vector2(0.0, -1.0))
	draw_polyline(highlight_points, Color(0.67, 0.56, 0.42, 0.72), 1.2, true)
	for button in _get_visible_tags():
		var tag_top := button.get_global_transform_with_canvas() * Vector2(button.size.x * 0.5, 0.0)
		var local_top := tag_top - global_position
		var rope_point := Vector2(local_top.x, _rope_y(local_top.x))
		if local_top.y > rope_point.y + 1.0:
			draw_line(rope_point, local_top, Color(0.42, 0.32, 0.23, 0.88), 2.0, true)
			draw_line(rope_point + Vector2(1.0, 0.0), local_top + Vector2(1.0, 0.0), Color(0.72, 0.61, 0.46, 0.55), 0.8, true)

func _get_visible_tags() -> Array[Button]:
	var result: Array[Button] = []
	if not is_instance_valid(_action_container):
		return result
	for child in _action_container.get_children():
		if child is Button and child.is_in_group("scene_action_tag") and child.is_visible_in_tree():
			result.append(child as Button)
	return result

func _layout_tags() -> void:
	if not is_instance_valid(_action_container):
		return
	var tags := _get_visible_tags()
	if tags.is_empty():
		return
	var row_width := float(tags.size()) * tag_size.x + float(tags.size() - 1) * tag_gap
	var start_x := (size.x - row_width) * 0.5
	if _action_container is Container:
		var container := _action_container as Control
		container.anchor_left = 0.0
		container.anchor_top = 0.0
		container.anchor_right = 0.0
		container.anchor_bottom = 0.0
		container.custom_minimum_size = Vector2(row_width, tag_size.y)
		container.size = Vector2(row_width, tag_size.y)
		container.position = Vector2(start_x, size.y * tag_top_ratio)
		for button in tags:
			button.custom_minimum_size = tag_size
		queue_redraw()
		return
	for index in range(tags.size()):
		var button := tags[index]
		button.anchor_left = 0.0
		button.anchor_top = 0.0
		button.anchor_right = 0.0
		button.anchor_bottom = 0.0
		button.custom_minimum_size = tag_size
		button.size = tag_size
		button.position = Vector2(start_x + float(index) * (tag_size.x + tag_gap), size.y * tag_top_ratio)
	queue_redraw()

func _on_resized() -> void:
	_last_visible_tag_ids.clear()
	_layout_tags()
	queue_redraw()

func _rope_y(local_x: float) -> float:
	var t := clampf(local_x / maxf(size.x, 1.0), 0.0, 1.0)
	return size.y * rope_edge_height_ratio + size.y * rope_sag_ratio * 4.0 * t * (1.0 - t)
