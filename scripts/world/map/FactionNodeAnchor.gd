extends Area2D

signal faction_clicked(faction_id: String)
signal faction_double_clicked(faction_id: String)

@export var faction_id: String = ""

var _last_click_ms := -10000

func _ready() -> void:
	input_pickable = true
	monitoring = true
	monitorable = true
	mouse_entered.connect(_on_mouse_entered)
	mouse_exited.connect(_on_mouse_exited)

func _input_event(_viewport: Node, event: InputEvent, _shape_idx: int) -> void:
	if not event is InputEventMouseButton:
		return
	var mouse := event as InputEventMouseButton
	if mouse.button_index != MOUSE_BUTTON_LEFT or not mouse.pressed:
		return
	get_viewport().set_input_as_handled()
	var now := Time.get_ticks_msec()
	var is_double := mouse.double_click or now - _last_click_ms <= 330
	_last_click_ms = now
	if is_double:
		faction_double_clicked.emit(faction_id)
	else:
		faction_clicked.emit(faction_id)

func _on_mouse_entered() -> void:
	Input.set_default_cursor_shape(Input.CURSOR_POINTING_HAND)

func _on_mouse_exited() -> void:
	Input.set_default_cursor_shape(Input.CURSOR_ARROW)
