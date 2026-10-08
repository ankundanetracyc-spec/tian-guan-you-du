extends Node2D

@onready var logical_grid_lines: TileMapLayer = $LogicalGridLines
@onready var grid_toggle: CheckButton = $SampleControls/GridToggle
@onready var sample_camera: Camera2D = $SampleCamera
@onready var reset_view_button: Button = $SampleControls/ResetView

const MIN_ZOOM := 0.016
const MAX_ZOOM := 2.0
const ZOOM_FACTOR := 1.18

var _home_position: Vector2
var _home_zoom: Vector2
var _dragging := false


func _ready() -> void:
	grid_toggle.toggled.connect(_on_grid_toggle_changed)
	reset_view_button.pressed.connect(_reset_view)
	logical_grid_lines.visible = grid_toggle.button_pressed
	_home_position = sample_camera.global_position
	_home_zoom = sample_camera.zoom


func _on_grid_toggle_changed(enabled: bool) -> void:
	logical_grid_lines.visible = enabled


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP and event.pressed:
			_zoom_at_mouse(ZOOM_FACTOR)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN and event.pressed:
			_zoom_at_mouse(1.0 / ZOOM_FACTOR)
		elif event.button_index == MOUSE_BUTTON_RIGHT:
			_dragging = event.pressed
	elif event is InputEventMouseMotion and _dragging:
		sample_camera.global_position -= event.relative / sample_camera.zoom


func _zoom_at_mouse(factor: float) -> void:
	var anchor_before := sample_camera.get_global_mouse_position()
	var next_zoom := clampf(sample_camera.zoom.x * factor, MIN_ZOOM, MAX_ZOOM)
	sample_camera.zoom = Vector2(next_zoom, next_zoom)
	var anchor_after := sample_camera.get_global_mouse_position()
	sample_camera.global_position += anchor_before - anchor_after


func _reset_view() -> void:
	sample_camera.global_position = _home_position
	sample_camera.zoom = _home_zoom
