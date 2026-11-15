extends Node2D

const MAX_SWAY_ANGLE := 0.48
const SWAY_SPRING := 17.0
const SWAY_DAMPING := 4.4
const THREAD_COLORS := [
	Color(0.46, 0.16, 0.13, 0.96),
	Color(0.61, 0.24, 0.18, 0.98),
	Color(0.72, 0.35, 0.24, 0.9),
]

var _sway_angle := 0.0
var _sway_velocity := 0.0
var parent_acceleration := 0.0

func _ready() -> void:
	queue_redraw()

func apply_impulse(value: float) -> void:
	_sway_velocity = clampf(_sway_velocity + value, -4.0, 4.0)

func _process(delta: float) -> void:
	if not is_visible_in_tree():
		return
	var remaining := minf(delta, 0.05)
	while remaining > 0.00001:
		var dt := minf(remaining, 1.0 / 120.0)
		_sway_velocity += (-sin(_sway_angle) * SWAY_SPRING - _sway_velocity * SWAY_DAMPING - parent_acceleration * 0.32) * dt
		_sway_angle = clampf(_sway_angle + _sway_velocity * dt, -MAX_SWAY_ANGLE, MAX_SWAY_ANGLE)
		remaining -= dt
	rotation = _sway_angle
	if absf(_sway_angle) > 0.001 or absf(_sway_velocity) > 0.002:
		queue_redraw()

func _draw() -> void:
	draw_line(Vector2(0, -4), Vector2(0, 7), THREAD_COLORS[0], 2.4, true)
	var knot := PackedVector2Array([Vector2(0, 0), Vector2(-4, 4), Vector2(0, 8), Vector2(4, 4), Vector2(0, 0)])
	draw_polyline(knot, THREAD_COLORS[1], 2.2, true)
	for strand_index in range(7):
		var spread := float(strand_index - 3)
		var length := 28.0 + float(strand_index % 3) * 3.0
		var start := Vector2(spread * 0.55, 8.0)
		var middle := Vector2(spread * 0.85 + sin(_sway_angle) * 3.0, length * 0.64)
		var end := Vector2(spread * 1.05 + sin(_sway_angle) * 7.0, length + 5.0)
		var points := PackedVector2Array([start, middle, end])
		draw_polyline(points, THREAD_COLORS[strand_index % THREAD_COLORS.size()], 1.8, true)
	draw_circle(Vector2.ZERO, 2.5, THREAD_COLORS[1])
