extends Control
## Isolated visual review; does not enter or alter a game save.

const MapRuntime = preload("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
const BoatVisual = preload("res://gdscripts/world/WorldSpiritBoatVisual.gd")

var map: Control
var boat: Node2D
var wake: Node2D
var origin := Vector2(3600, 3600)
var elapsed := 0.0
var capture := false
var captured_right := false
var captured_left := false


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	capture = OS.get_cmdline_user_args().has("--capture")
	map = MapRuntime.new()
	map.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(map)
	if not map.set_initial_runtime_id("white_stone_town"):
		push_error("尾流预览：地图不可用")
		get_tree().quit(1)
		return
	map.player_mark.hide()
	map.player_halo.hide()
	boat = BoatVisual.new()
	boat.z_index = 105
	map.world.add_child(boat)
	if not boat.build():
		push_error("尾流预览：船体或人物不可用")
		get_tree().quit(1)
		return
	wake = boat.wake
	boat.set_map_zoom(map.world_zoom)
	map.map_zoom_changed.connect(_rescale_boat)
	map.focus_world_point(origin)
	boat.set_flight_position(Vector3(origin.x - 50, origin.y, 140))


func _rescale_boat(zoom: float, _level: int) -> void:
	if is_instance_valid(boat):
		boat.set_map_zoom(zoom)


func _process(delta: float) -> void:
	if not is_instance_valid(wake):
		return
	var dt := minf(delta, 0.05)
	elapsed += dt
	var offset := 0.0
	if elapsed < 3.2:
		offset = -50.0 + elapsed * 25.0
		boat.set_facing(1)
	elif elapsed < 4.9:
		offset = 30.0
	else:
		offset = 30.0 - fmod(elapsed - 4.9, 3.2) * 25.0
		boat.set_facing(-1)
	boat.set_flight_position(Vector3(origin.x + offset, origin.y, 140))
	if capture and elapsed >= 2.5 and not captured_right:
		captured_right = true
		_save_capture("res://tmp/spirit_boat_wake_right_20261106.png", false)
	if capture and elapsed >= 6.6 and not captured_left:
		captured_left = true
		_save_capture("res://tmp/spirit_boat_wake_left_20261106.png", true)


func _save_capture(path: String, finish: bool) -> void:
	await RenderingServer.frame_post_draw
	var result := get_viewport().get_texture().get_image().save_png(path)
	if result != OK:
		push_error("尾流预览截图失败: " + path)
		get_tree().quit(1)
		return
	if finish:
		print("SPIRIT_BOAT_WAKE_MAP_PREVIEW_OK: right, stop, left")
		get_tree().quit(0)
