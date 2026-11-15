extends Control
## Isolated visual review on the actual free-world map. No game progress saved.

const MapRuntime = preload("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
const BoatVisual = preload("res://gdscripts/world/WorldSpiritBoatVisual.gd")

var map: Control
var boat: Node2D
var origin := Vector2.ZERO
var elapsed := 0.0
var capture := false
var preview_height := 0.0
var target_height := 140.0

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	capture = OS.get_cmdline_user_args().has("--capture")
	map = MapRuntime.new()
	map.name = "ActualFreeWorldMap"
	map.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(map)
	if not map.set_initial_runtime_id("white_stone_town"):
		push_error("灵舟预览：白石地图入口不可用")
		get_tree().quit(1)
		return
	map.player_mark.hide()
	map.player_halo.hide()
	# Center the composition well within the actual map, away from edge clamping.
	origin = Vector2(3600, 3600)
	boat = BoatVisual.new()
	boat.name = "LayeredSpiritBoat"
	boat.z_index = 105
	map.world.add_child(boat)
	if not boat.build():
		push_error("灵舟预览：分层素材或角色加载失败")
		get_tree().quit(1)
		return
	boat.set_flight_position(Vector3(origin.x, origin.y, 0))
	_rescale_boat(map.world_zoom, map.zoom_level)
	map.map_zoom_changed.connect(_rescale_boat)
	map.focus_world_point(origin)
	var label := Label.new()
	label.text = "灵舟飞行预览  ·  F 转向  ·  G 升降"
	label.add_theme_font_size_override("font_size", 22)
	label.add_theme_color_override("font_color", Color("35463c"))
	label.position = Vector2(30, 70)
	label.z_index = 180
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(label)
	if capture:
		if not _require(boat.ready_to_draw and boat.player_avatar.loaded and boat.driver_avatar.sprite_frames != null, "现有人物或分层素材未就绪"): return
		if not _require(boat.hull.z_index < boat.player_avatar.z_index and boat.player_avatar.z_index < boat.rail.z_index, "人物没有位于船身和前景之间"): return
		if not _require(boat.driver_avatar.z_index == boat.player_avatar.z_index and boat.rail.region_enabled, "驾驶者层级或前景裁切错误"): return
		if not _require(boat.scale.is_equal_approx(Vector2.ONE), "地图缩放保持固定世界尺寸"): return
		var shadow_position: Vector2 = boat.ground_shadow.position
		var flat_position: Vector2 = boat.position
		boat.set_flight_position(Vector3(flat_position.x, flat_position.y, 120.0))
		if not _require(boat.position == flat_position and boat.get_flight_position().z == 120.0 and boat.visual_root.position.y < -1.0 and boat.ground_shadow.position == shadow_position, "高度未独立投影或影子离开地面"): return
		boat.set_flight_position(Vector3(flat_position.x, flat_position.y, 0))
		var initial_position: Vector2 = boat.position
		await _capture_frames(45)
		if not _require(boat.position.distance_to(initial_position) > 0.1, "预览没有实际移动"): return
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png("res://tmp/spirit_boat_layered_map_right_20261106.png")
		boat.set_facing(-1)
		if not _require(boat.visual_root.scale.x == -1.0, "左向没有翻转整艘舟和人物"): return
		await _capture_frames(15)
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png("res://tmp/spirit_boat_layered_map_left_20261106.png")
		print("SPIRIT_BOAT_PREVIEW_OK: actual map, real avatars, layers, scaling, movement and both directions")
		get_tree().quit()

func _require(ok: bool, message: String) -> bool:
	if ok: return true
	push_error("灵舟分层检查失败：" + message)
	get_tree().quit(1)
	return false

func _capture_frames(count: int) -> void:
	for i in count: await get_tree().process_frame

func _rescale_boat(zoom: float, _level: int) -> void:
	if is_instance_valid(boat): boat.set_map_zoom(zoom)

func _process(delta: float) -> void:
	if not is_instance_valid(boat): return
	elapsed += minf(delta, 0.05)
	preview_height = move_toward(preview_height, target_height, minf(delta, 0.05) * 90.0)
	var point := origin + Vector2(sin(elapsed * 0.35) * 50.0, 0)
	boat.set_flight_position(Vector3(point.x, point.y, preview_height))

func _unhandled_key_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_F:
		boat.set_facing(-boat.facing)
		get_viewport().set_input_as_handled()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_G:
		target_height = 0.0 if target_height > 0.0 else 140.0
		get_viewport().set_input_as_handled()
