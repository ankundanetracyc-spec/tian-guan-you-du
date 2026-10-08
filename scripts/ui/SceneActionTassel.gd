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

func _ready() -> void:
	queue_redraw()

func apply_impulse(value: float) -> void:
	_sway_velocity = clampf(_sway_velocity + value, -4.0, 4.0)

func _process(delta: float) -> void:
	_sway_velocity += (-_sway_angle * SWAY_SPRING - _sway_velocity * SWAY_DAMPING) * delta
	_sway_angle = clampf(_sway_angle + _sway_velocity * delta, -MAX_SWAY_ANGLE, MAX_SWAY_ANGLE)
	rotation = _sway_angle
	if absf(_sway_angle) > 0.001 or absf(_sway_velocity) > 0.002:
		queue_redraw()

func _draw() -> void:
	for strand_index in range(7):
		var spread := float(strand_index - 3)
		var length := 28.0 + float(strand_index % 3) * 3.0
		var start := Vector2(spread * 1.15, 0.0)
		var middle := Vector2(spread * 2.1 + sin(_sway_angle) * 5.0, length * 0.55)
		var end := Vector2(spread * 3.0 + sin(_sway_angle) * 11.0, length)
		var points := PackedVector2Array([start, middle, end])
		draw_polyline(points, THREAD_COLORS[strand_index % THREAD_COLORS.size()], 1.8, true)
	draw_circle(Vector2.ZERO, 2.5, THREAD_COLORS[1])
