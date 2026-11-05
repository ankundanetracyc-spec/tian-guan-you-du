extends RefCounted

const DOUBLE_TAP_WINDOW := 0.28
const SPEED_MULTIPLIER := 1.5
const STAMINA_PER_SECOND := 0.5

var pressed_keys: Dictionary = {}
var last_tap_direction := Vector2.ZERO
var last_tap_time := -INF
var fast_key := 0
var fast_direction := Vector2.ZERO

func key_direction(key: int) -> Vector2:
	match key:
		KEY_W, KEY_UP: return Vector2.UP
		KEY_S, KEY_DOWN: return Vector2.DOWN
		KEY_A, KEY_LEFT: return Vector2.LEFT
		KEY_D, KEY_RIGHT: return Vector2.RIGHT
	return Vector2.ZERO

func feed_key(event: InputEventKey, seconds: float) -> void:
	var key := int(event.physical_keycode if event.physical_keycode != 0 else event.keycode)
	var direction := key_direction(key)
	if direction == Vector2.ZERO: return
	if not event.pressed:
		pressed_keys.erase(key)
		if key == fast_key: stop_fast_step()
		return
	if event.echo or pressed_keys.has(key): return
	var same_direction_held := pressed_keys.values().has(direction)
	pressed_keys[key] = direction
	if not same_direction_held and direction == last_tap_direction and seconds-last_tap_time <= DOUBLE_TAP_WINDOW:
		fast_key = key
		fast_direction = direction
		last_tap_time = -INF
	else:
		last_tap_direction = direction
		last_tap_time = seconds
		if fast_key != 0 and direction.dot(fast_direction) < 0.0: stop_fast_step()

func wants_fast_step(direction: Vector2) -> bool:
	return fast_key != 0 and pressed_keys.has(fast_key) and direction.dot(fast_direction) > 0.0

func stop_fast_step() -> void:
	fast_key = 0
	fast_direction = Vector2.ZERO

func clear() -> void:
	pressed_keys.clear()
	last_tap_direction = Vector2.ZERO
	last_tap_time = -INF
	stop_fast_step()
