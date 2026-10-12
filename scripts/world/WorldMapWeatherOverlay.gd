extends Control

const RAIN_PROFILES := {
	1: {"count": 190, "speed_min": 390.0, "speed_max": 540.0, "length_min": 10.0, "length_max": 17.0, "alpha": 0.42, "width": 1.15},
	2: {"count": 360, "speed_min": 520.0, "speed_max": 730.0, "length_min": 15.0, "length_max": 24.0, "alpha": 0.56, "width": 1.4},
	3: {"count": 560, "speed_min": 700.0, "speed_max": 980.0, "length_min": 21.0, "length_max": 34.0, "alpha": 0.68, "width": 1.7},
}

const SNOW_PROFILES := {
	1: {"count": 220, "speed_min": 24.0, "speed_max": 44.0, "size_min": 1.5, "size_max": 2.4, "sway_max": 11.0, "alpha": 0.76},
	2: {"count": 370, "speed_min": 34.0, "speed_max": 62.0, "size_min": 1.9, "size_max": 3.2, "sway_max": 18.0, "alpha": 0.88},
	3: {"count": 520, "speed_min": 46.0, "speed_max": 82.0, "size_min": 2.3, "size_max": 4.0, "sway_max": 26.0, "alpha": 0.96},
}

const LIGHTNING_FLASH_DURATION := 0.28

var _weather_manager: Node
var _cloud_material: ShaderMaterial
var _weather := "clear"
var _rain_level := 0
var _snow_level := 0
var _weather_darkness := 0.0
var _clear_sunlight := 0.0
var _wind_drift := 0.0
var _wind_direction := 1.0
var _rain_color := Color(0.60, 0.70, 0.76, 0.3)
var _rain_width := 1.0
var _drop_positions := PackedVector2Array()
var _drop_speeds := PackedFloat32Array()
var _drop_lengths := PackedFloat32Array()
var _snow_positions := PackedVector2Array()
var _snow_speeds := PackedFloat32Array()
var _snow_sizes := PackedFloat32Array()
var _snow_phases := PackedFloat32Array()
var _snow_sway := PackedFloat32Array()
var _rain_draw_segments := PackedVector2Array()
var _snow_draw_segments := PackedVector2Array()
var _snow_color := Color(0.60, 0.72, 0.80, 0.65)
var _lightning_bolt := PackedVector2Array()
var _lightning_branches: Array[PackedVector2Array] = []
var _lightning_flash_time := 0.0
var _lightning_cooldown := 0.0
var _elapsed_seconds := 0.0
var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rng.randomize()
	_weather_manager = get_node_or_null("/root/WeatherManager")
	var cloud_cover := get_parent().get_node_or_null("CloudCover") as ColorRect
	if cloud_cover != null and cloud_cover.material is ShaderMaterial:
		_cloud_material = (cloud_cover.material as ShaderMaterial).duplicate() as ShaderMaterial
		cloud_cover.material = _cloud_material
	if is_instance_valid(_weather_manager) and _weather_manager.has_signal("weather_changed"):
		var weather_changed_callback := Callable(self, "_on_weather_changed")
		if not _weather_manager.is_connected("weather_changed", weather_changed_callback):
			_weather_manager.connect("weather_changed", weather_changed_callback)
	_sync_weather()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED and is_node_ready() and (_rain_level > 0 or _snow_level > 0):
		_reset_precipitation()


func _process(delta: float) -> void:
	if not is_visible_in_tree():
		return
	_elapsed_seconds += delta
	_update_precipitation(delta)
	_update_lightning(delta)
	if _cloud_material != null:
		_cloud_material.set_shader_parameter("cloud_time", _elapsed_seconds)
	queue_redraw()


func _on_weather_changed(_new_weather: String, _intensity: float) -> void:
	_sync_weather()


func _sync_weather() -> void:
	var next_weather := "clear"
	var intensity := 0.0
	var wind_speed := 0.0
	if is_instance_valid(_weather_manager):
		next_weather = str(_weather_manager.get("weather"))
		intensity = float(_weather_manager.get("weather_intensity"))
		wind_speed = float(_weather_manager.get("wind_speed"))
	var next_level := _get_rain_level(next_weather, intensity)
	var next_snow_level := _get_snow_level(next_weather, intensity)
	_weather = next_weather
	_rain_level = next_level
	_snow_level = next_snow_level
	_weather_darkness = _get_cloud_darkness(next_weather, next_level, next_snow_level, intensity)
	_clear_sunlight = 0.16 if next_weather == "clear" else 0.0
	_wind_direction = -1.0 if _rng.randf() < 0.5 else 1.0
	if _weather == "thunderstorm":
		_lightning_cooldown = _rng.randf_range(2.5, 7.0)
	else:
		_lightning_cooldown = 0.0
		_lightning_flash_time = 0.0
		_lightning_bolt.clear()
		_lightning_branches.clear()
	_wind_drift = _wind_direction * clampf(wind_speed, 0.0, 8.0) * 5.5
	_configure_precipitation()
	if _cloud_material != null:
		_cloud_material.set_shader_parameter("weather_darkness", _weather_darkness)
		_cloud_material.set_shader_parameter("clear_sunlight", _clear_sunlight)
		_cloud_material.set_shader_parameter("cloud_time", _elapsed_seconds)
	_update_process_state()
	queue_redraw()


func _update_process_state() -> void:
	set_process(_weather_darkness > 0.0 or _rain_level > 0 or _snow_level > 0 or _weather == "thunderstorm")


func _get_rain_level(weather: String, intensity: float) -> int:
	if weather == "thunderstorm":
		return 3
	if weather != "rain":
		return 0
	# WeatherMgr samples rain intensity in [0.3, 0.9]; these bands make light rain common,
	# moderate rain occasional, and heavy rain distinctly uncommon.
	if intensity < 0.72:
		return 1
	if intensity < 0.87:
		return 2
	return 3


func _get_snow_level(weather: String, intensity: float) -> int:
	if weather != "snow":
		return 0
	# Snow intensity is sampled in [0.3, 1.0]; heavier snowfall remains uncommon.
	if intensity < 0.75:
		return 1
	if intensity < 0.94:
		return 2
	return 3


func _get_cloud_darkness(weather: String, rain_level: int, snow_level: int, intensity: float) -> float:
	if weather == "thunderstorm":
		return 0.38
	if weather == "rain":
		match rain_level:
			1: return 0.10
			2: return 0.17
			3: return 0.27
	if weather == "cloudy":
		return lerpf(0.16, 0.24, clampf((intensity - 0.1) / 0.4, 0.0, 1.0))
	if weather == "snow":
		match snow_level:
			1: return 0.14
			2: return 0.21
			3: return 0.28
	return 0.0


func _configure_precipitation() -> void:
	_configure_rain()
	_configure_snow()


func _configure_rain() -> void:
	_drop_positions.clear()
	_drop_speeds.clear()
	_drop_lengths.clear()
	if _rain_level <= 0 or size.x <= 0.0 or size.y <= 0.0:
		return
	var profile: Dictionary = RAIN_PROFILES[_rain_level]
	var scale_factor := clampf(size.x / 1440.0, 0.65, 1.5)
	var count := roundi(float(profile["count"]) * scale_factor)
	for index in range(count):
		_drop_positions.append(Vector2(_rng.randf_range(0.0, size.x), _rng.randf_range(-size.y, size.y)))
		_drop_speeds.append(_rng.randf_range(float(profile["speed_min"]), float(profile["speed_max"])))
		_drop_lengths.append(_rng.randf_range(float(profile["length_min"]), float(profile["length_max"])))
	_rain_color = Color(0.57, 0.68, 0.74, float(profile["alpha"]))
	_rain_width = float(profile["width"])


func _configure_snow() -> void:
	_snow_positions.clear()
	_snow_speeds.clear()
	_snow_sizes.clear()
	_snow_phases.clear()
	_snow_sway.clear()
	_snow_draw_segments.clear()
	if _snow_level <= 0 or size.x <= 0.0 or size.y <= 0.0:
		return
	var profile: Dictionary = SNOW_PROFILES[_snow_level]
	var scale_factor := clampf(size.x / 1440.0, 0.65, 1.5)
	var count := roundi(float(profile["count"]) * scale_factor)
	for index in range(count):
		_snow_positions.append(Vector2(_rng.randf_range(0.0, size.x), _rng.randf_range(-size.y, size.y)))
		_snow_speeds.append(_rng.randf_range(float(profile["speed_min"]), float(profile["speed_max"])))
		_snow_sizes.append(_rng.randf_range(float(profile["size_min"]), float(profile["size_max"])))
		_snow_phases.append(_rng.randf_range(0.0, TAU))
		_snow_sway.append(_rng.randf_range(3.0, float(profile["sway_max"])))
	_snow_color = Color(0.62, 0.74, 0.82, float(profile["alpha"]))
	var segment_count := _snow_positions.size() * 8
	for radius in _snow_sizes:
		if radius > 2.8:
			segment_count += 4
	_snow_draw_segments.resize(segment_count)
func _reset_precipitation() -> void:
	_configure_precipitation()


func _update_precipitation(delta: float) -> void:
	if _rain_level > 0:
		for index in range(_drop_positions.size()):
			var rain_position := _drop_positions[index]
			rain_position.y += _drop_speeds[index] * delta
			rain_position.x += _wind_drift * delta
			if rain_position.y > size.y + 36.0:
				rain_position.y = _rng.randf_range(-48.0, -8.0)
				rain_position.x = _rng.randf_range(0.0, size.x)
			elif rain_position.x > size.x + 24.0:
				rain_position.x = -24.0
			elif rain_position.x < -24.0:
				rain_position.x = size.x + 24.0
			_drop_positions[index] = rain_position
	if _snow_level > 0:
		for index in range(_snow_positions.size()):
			var snow_position := _snow_positions[index]
			var sway := sin(_elapsed_seconds * 0.8 + _snow_phases[index]) * _snow_sway[index]
			snow_position.y += _snow_speeds[index] * delta
			snow_position.x += (_wind_drift * 1.5 + sway) * delta
			if snow_position.y > size.y + 12.0:
				snow_position.y = _rng.randf_range(-32.0, -8.0)
				snow_position.x = _rng.randf_range(0.0, size.x)
			elif snow_position.x > size.x + 12.0:
				snow_position.x = -12.0
			elif snow_position.x < -12.0:
				snow_position.x = size.x + 12.0
			_snow_positions[index] = snow_position


func _update_lightning(delta: float) -> void:
	if _weather != "thunderstorm":
		return
	if _lightning_flash_time > 0.0:
		_lightning_flash_time = maxf(0.0, _lightning_flash_time - delta)
		if _lightning_flash_time == 0.0:
			_lightning_bolt.clear()
			_lightning_branches.clear()
			return
	_lightning_cooldown -= delta
	if _lightning_cooldown <= 0.0:
		_start_lightning()


func _start_lightning() -> void:
	_lightning_flash_time = LIGHTNING_FLASH_DURATION
	_lightning_cooldown = _rng.randf_range(9.0, 22.0)
	_lightning_bolt.clear()
	_lightning_branches.clear()
	if size.x < 2.0 or size.y < 2.0:
		return
	var point := Vector2(_rng.randf_range(size.x * 0.2, size.x * 0.8), -8.0)
	_lightning_bolt.append(point)
	for segment in range(1, 8):
		point += Vector2(_rng.randf_range(-58.0, 58.0), size.y / 8.0)
		point.x = clampf(point.x, 12.0, size.x - 12.0)
		_lightning_bolt.append(point)
	var branch_start := _lightning_bolt[_rng.randi_range(2, 5)]
	var branch := PackedVector2Array([branch_start])
	var branch_point := branch_start
	for step in range(3):
		branch_point += Vector2(_rng.randf_range(-44.0, 44.0), size.y * 0.08)
		branch.append(branch_point)
	_lightning_branches.append(branch)


func _draw() -> void:
	var flash_strength := _get_lightning_flash_strength()
	if flash_strength > 0.0:
		draw_rect(Rect2(Vector2.ZERO, size), Color(0.78, 0.86, 0.95, flash_strength), true)
		_draw_bolt(_lightning_bolt, Color(0.70, 0.83, 1.0, 0.72), 7.0)
		_draw_bolt(_lightning_bolt, Color(0.96, 0.99, 1.0, 0.98), 2.2)
		for branch in _lightning_branches:
			_draw_bolt(branch, Color(0.84, 0.91, 1.0, 0.82), 1.5)
	if _rain_level > 0:
		_rain_draw_segments.resize(_drop_positions.size() * 2)
		for index in range(_drop_positions.size()):
			var start := _drop_positions[index]
			var end := start + Vector2(_wind_drift * _drop_lengths[index] / maxf(1.0, _drop_speeds[index]) * 2.0, _drop_lengths[index])
			_rain_draw_segments[index * 2] = start
			_rain_draw_segments[index * 2 + 1] = end
		if not _rain_draw_segments.is_empty():
			draw_multiline(_rain_draw_segments, _rain_color, _rain_width, true)
	if _snow_level > 0:
		var segment_index := 0
		for index in range(_snow_positions.size()):
			var position := _snow_positions[index]
			var radius := _snow_sizes[index]
			var top := position + Vector2(0.0, -radius)
			var right := position + Vector2(radius, 0.0)
			var bottom := position + Vector2(0.0, radius)
			var left := position + Vector2(-radius, 0.0)
			_snow_draw_segments[segment_index] = top
			_snow_draw_segments[segment_index + 1] = right
			_snow_draw_segments[segment_index + 2] = right
			_snow_draw_segments[segment_index + 3] = bottom
			_snow_draw_segments[segment_index + 4] = bottom
			_snow_draw_segments[segment_index + 5] = left
			_snow_draw_segments[segment_index + 6] = left
			_snow_draw_segments[segment_index + 7] = top
			segment_index += 8
			if radius > 2.8:
				var horizontal_arm := Vector2(radius * 0.7, 0.0)
				_snow_draw_segments[segment_index] = position - horizontal_arm
				_snow_draw_segments[segment_index + 1] = position + horizontal_arm
				segment_index += 2
				var vertical_arm := Vector2(0.0, radius)
				_snow_draw_segments[segment_index] = position - vertical_arm
				_snow_draw_segments[segment_index + 1] = position + vertical_arm
				segment_index += 2
		if not _snow_draw_segments.is_empty():
			draw_multiline(_snow_draw_segments, _snow_color, 0.9, true)


func _get_lightning_flash_strength() -> float:
	if _lightning_flash_time <= 0.0:
		return 0.0
	var elapsed := LIGHTNING_FLASH_DURATION - _lightning_flash_time
	if elapsed < 0.055:
		return 0.34
	if elapsed > 0.12 and elapsed < 0.175:
		return 0.17
	return 0.0


func _draw_bolt(points: PackedVector2Array, color: Color, width: float) -> void:
	for index in range(points.size() - 1):
		draw_line(points[index], points[index + 1], color, width, true)
