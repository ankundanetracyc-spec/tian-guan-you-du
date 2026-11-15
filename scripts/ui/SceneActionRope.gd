extends Control

@export var tag_size := Vector2(84.0, 190.0)
@export var tag_gap := 58.0
@export_range(0.0, 1.0, 0.001) var tag_top_ratio := 0.365
@export_range(0.0, 1.0, 0.001) var rope_edge_height_ratio := 0.275
@export_range(0.0, 0.3, 0.001) var rope_sag_ratio := 0.09
@export var action_container_path: NodePath = NodePath("..")

const SEGMENTS := 64
const STEP := 1.0 / 120.0
const TENSION := 540.0
const RESTORE := 22.0
const DAMPING := 5.8

var _action_container: Node
var _tags: Array[Button] = []
var _last_visible_tag_ids: Array[int] = []
var _rest_positions: Dictionary = {}
var _hang_x: Dictionary = {}
var _offsets := PackedVector2Array()
var _velocities := PackedVector2Array()
var _accelerations := PackedVector2Array()
var _points := PackedVector2Array()
var _accumulator := 0.0
var _moving := false
var _drag_index := -1
var _drag_target := Vector2.ZERO

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_action_container = get_node_or_null(action_container_path)
	resized.connect(_on_resized)
	visibility_changed.connect(_on_visibility_changed)
	_reset_string()
	_refresh_tags()

func _process(delta: float) -> void:
	if not is_visible_in_tree():
		return
	_refresh_tags()
	if _tags.is_empty():
		return
	if _moving or _drag_index >= 0:
		_accumulator += minf(delta, 0.05)
		while _accumulator >= STEP:
			_step_string(STEP)
			_accumulator -= STEP
		_update_points()
		_update_attachments()
		queue_redraw()

func _refresh_tags() -> void:
	if _sync_tags():
		_layout_tags()

func _sync_tags() -> bool:
	var found: Array[Button] = []
	var ids: Array[int] = []
	if is_instance_valid(_action_container):
		for child in _action_container.get_children():
			if child is Button and not child.is_queued_for_deletion() and child.is_in_group("scene_action_tag") and child.is_visible_in_tree():
				found.append(child)
				ids.append(child.get_instance_id())
	if ids == _last_visible_tag_ids:
		return false
	_tags = found
	_last_visible_tag_ids = ids
	return true

func _reset_string() -> void:
	_offsets.resize(SEGMENTS + 1)
	_velocities.resize(SEGMENTS + 1)
	_accelerations.resize(SEGMENTS + 1)
	_points.resize(SEGMENTS + 1)
	_offsets.fill(Vector2.ZERO)
	_velocities.fill(Vector2.ZERO)
	_accelerations.fill(Vector2.ZERO)
	_accumulator = 0.0
	_moving = false
	_drag_index = -1
	_update_points()
	queue_redraw()

func _rest_point(index: int) -> Vector2:
	var x := size.x * float(index) / SEGMENTS
	return Vector2(x, _rope_y(x))

func _update_points() -> void:
	for i in range(SEGMENTS + 1):
		_points[i] = _rest_point(i) + _offsets[i]

func _step_string(dt: float) -> void:
	# Spring string around its hanging rest curve; endpoints remain pinned.
	for i in range(1, SEGMENTS):
		_accelerations[i] = (_offsets[i - 1] + _offsets[i + 1] - 2.0 * _offsets[i]) * TENSION \
			- _offsets[i] * RESTORE - _velocities[i] * DAMPING
	var energy := 0.0
	for i in range(1, SEGMENTS):
		if i == _drag_index:
			_offsets[i] = _drag_target
			_velocities[i] = Vector2.ZERO
		else:
			_velocities[i] += _accelerations[i] * dt
			_offsets[i] += _velocities[i] * dt
			_offsets[i].x = clampf(_offsets[i].x, -8.0, 8.0)
			_offsets[i].y = clampf(_offsets[i].y, -45.0, 45.0)
		energy += _offsets[i].length_squared() + _velocities[i].length_squared() * 0.03
	_moving = energy > 0.02
	if not _moving and _drag_index < 0:
		_offsets.fill(Vector2.ZERO)
		_velocities.fill(Vector2.ZERO)
		_accelerations.fill(Vector2.ZERO)

func _sample(values: PackedVector2Array, x: float) -> Vector2:
	var coordinate := clampf(x / maxf(size.x, 1.0), 0.0, 1.0) * SEGMENTS
	var index := mini(int(coordinate), SEGMENTS - 1)
	return values[index].lerp(values[index + 1], coordinate - index)

func _push_string(x: float, impulse: Vector2) -> void:
	var radius := maxf(52.0, size.x * 0.035)
	for i in range(1, SEGMENTS):
		var influence := maxf(0.0, 1.0 - absf(_rest_point(i).x - x) / radius)
		_velocities[i] += impulse * influence
	_moving = true

func _input(event: InputEvent) -> void:
	if not is_visible_in_tree() or not event is InputEventMouse:
		return
	_refresh_tags()
	if _tags.is_empty():
		return
	var pointer: Vector2 = get_global_transform_with_canvas().affine_inverse() * event.position
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if not event.pressed:
			if _drag_index >= 0:
				_drag_index = -1
				_moving = true
				get_viewport().set_input_as_handled()
		elif absf(pointer.y - _sample(_points, pointer.x).y) < 12.0:
			_drag_index = clampi(roundi(pointer.x / maxf(size.x, 1.0) * SEGMENTS), 2, SEGMENTS - 2)
			_drag_target = _offsets[_drag_index]
			get_viewport().set_input_as_handled()
		return
	if not event is InputEventMouseMotion:
		return
	if _drag_index >= 0:
		var wanted := pointer - _rest_point(_drag_index)
		_drag_target = Vector2(clampf(wanted.x, -8.0, 8.0), clampf(wanted.y, -42.0, 42.0))
		_moving = true
		get_viewport().set_input_as_handled()
		return
	var motion := Vector2(clampf(event.relative.x, -50.0, 50.0), clampf(event.relative.y, -50.0, 50.0))
	if absf(pointer.y - _sample(_points, pointer.x).y) < 22.0:
		_push_string(pointer.x, Vector2(motion.x * 0.65, motion.y * 1.8))
	for tag in _tags:
		var tag_pointer: Vector2 = tag.get_global_transform_with_canvas().affine_inverse() * event.position
		var rect := Rect2(Vector2.ZERO, tag.size).grow(22.0)
		if not rect.has_point(tag_pointer):
			continue
		var influence := clampf(1.0 - absf(tag_pointer.x - tag.size.x * 0.5) / 120.0, 0.0, 1.0)
		tag.call("apply_cursor_impulse", motion.x * 0.012 * influence)
		_push_string(float(_hang_x[tag.get_instance_id()]), Vector2(motion.x * 1.2, -absf(motion.x) * 0.35) * influence)

func _layout_tags() -> void:
	# Boards can replace their buttons and request layout before the next process frame.
	_sync_tags()
	_rest_positions.clear()
	_hang_x.clear()
	if _tags.is_empty() or size.x <= 0.0:
		queue_redraw()
		return
	var count := _tags.size()
	var margin := minf(360.0, size.x * 0.28)
	var available := maxf(180.0, size.x - margin * 2.0)
	var width := minf(tag_size.x, maxf(48.0, (available - float(count - 1) * 12.0) / count))
	var gap := 0.0 if count == 1 else minf(tag_gap, maxf(12.0, (available - width * count) / (count - 1)))
	var row_width := width * count + gap * (count - 1)
	var start_x := (size.x - row_width) * 0.5
	var top := maxf(size.y * tag_top_ratio, _rope_y(size.x * 0.5) + 30.0)
	var container := _action_container as Control
	if _action_container is Container:
		container.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
		container.custom_minimum_size = Vector2(row_width, tag_size.y)
		container.size = Vector2(row_width, tag_size.y)
		container.position = Vector2(start_x, top)
		if container is BoxContainer:
			container.add_theme_constant_override("separation", roundi(gap))
	for i in range(count):
		var tag := _tags[i]
		tag.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
		tag.custom_minimum_size = Vector2(width, tag_size.y)
		tag.size = Vector2(width, tag_size.y)
		var base := Vector2(i * (width + gap), 0.0) if _action_container is Container else Vector2(start_x + i * (width + gap), top)
		tag.position = base
		_rest_positions[tag.get_instance_id()] = base
		var anchor := get_global_transform_with_canvas().affine_inverse() * (tag.get_global_transform_with_canvas() * tag.pivot_offset)
		_hang_x[tag.get_instance_id()] = anchor.x
	_update_attachments()
	queue_redraw()

func _update_attachments() -> void:
	for tag in _tags:
		if not is_instance_valid(tag) or not tag.is_inside_tree() or tag.is_queued_for_deletion():
			continue
		var parent := tag.get_parent() as Control
		if parent == null:
			continue
		var id := tag.get_instance_id()
		if not _rest_positions.has(id):
			continue
		var x := float(_hang_x[id])
		var offset := _sample(_offsets, x)
		var parent_inverse := parent.get_global_transform_with_canvas().affine_inverse()
		var transform_to_parent := parent_inverse * get_global_transform_with_canvas()
		tag.position = _rest_positions[id] + (transform_to_parent * offset - transform_to_parent * Vector2.ZERO)
		tag.call("set_rope_acceleration", _sample(_accelerations, x).x)

func _draw() -> void:
	if _tags.is_empty() or _points.size() < 2:
		return
	_draw_cord(_points, 3.6)
	for tag in _tags:
		if not is_instance_valid(tag) or not tag.is_inside_tree() or tag.is_queued_for_deletion() or not _hang_x.has(tag.get_instance_id()):
			continue
		var anchor := get_global_transform_with_canvas().affine_inverse() * (tag.get_global_transform_with_canvas() * tag.pivot_offset)
		var rope_point := _sample(_points, float(_hang_x[tag.get_instance_id()]))
		_draw_cord(PackedVector2Array([rope_point, anchor]), 2.0)
		draw_arc(rope_point, 3.4, 0.0, TAU, 16, Color("645c48"), 1.2, true)

func _draw_cord(points: PackedVector2Array, thickness: float) -> void:
	var shifted := PackedVector2Array()
	for point in points:
		shifted.append(point + Vector2(0.0, 0.7))
	draw_polyline(shifted, Color("373d35", 0.38), thickness + 1.5, true)
	draw_polyline(points, Color("625b48"), thickness, true)
	shifted.clear()
	for point in points:
		shifted.append(point + Vector2(0.0, -0.8))
	draw_polyline(shifted, Color("b3aa8d", 0.85), maxf(0.7, thickness * 0.3), true)

func _on_resized() -> void:
	if not is_inside_tree() or _points.is_empty():
		return
	_reset_string()
	_layout_tags()

func _on_visibility_changed() -> void:
	if not is_visible_in_tree() and not _points.is_empty():
		_reset_string()
		_update_attachments()

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_WINDOW_FOCUS_OUT:
		_drag_index = -1

func _rope_y(local_x: float) -> float:
	var t := clampf(local_x / maxf(size.x, 1.0), 0.0, 1.0)
	return size.y * rope_edge_height_ratio + size.y * rope_sag_ratio * 4.0 * t * (1.0 - t)
