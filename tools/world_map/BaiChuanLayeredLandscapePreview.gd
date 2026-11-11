extends Control
## Uses the production map and visual classes in an isolated user directory.
const MapRuntime = preload("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
const BoatVisual = preload("res://gdscripts/world/WorldSpiritBoatVisual.gd")
const VisualScale = preload("res://gdscripts/world/WorldMapVisualScale.gd")
const NpcVisual = preload("res://gdscripts/world/WorldNpcTraveler.gd")
const PreviousGraph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd")
const OUT := "res://output/bai_chuan_layered_20261106/"
var map: Control
var boat: Node2D
var checks := 0
var failures := 0
var results: Array[String] = []

func _ready() -> void:
	if not OS.get_environment("APPDATA").replace("\\", "/").contains("bai_chuan_layered_20261106/"):
		push_error("分层验收必须使用隔离存档目录")
		get_tree().quit(2)
		return
	get_window().content_scale_size = Vector2i(1920, 1080)
	get_window().size = Vector2i(1920, 1080)
	map = MapRuntime.new()
	map.name = "ActualFreeWorldMap"
	map.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(map)
	await _frames(6)
	_check(is_instance_valid(map.landscape) and map.landscape.active.size() > 0, "正式地图加载独立山树")
	_check(map.place_views.size() == 123, "123具名地点保持")
	_check(map.landscape.source_tiles.size() == 9, "保留九片源图水系采样")
	var data: Dictionary = map.landscape.metadata
	for path: String in data.source_files:
		_check(FileAccess.get_sha256("res://" + path) == str(data.source_files[path]), "原地理和导航数据保持 " + path)
	map._set_zoom_level(2, get_viewport_rect().size * 0.5)
	var point := Vector2(3600, 3600)
	map.player_mark.position = point
	map.player_halo.position = point
	map.focus_world_point(point)
	map.set_player_travel_party([{"id":"qa_layered_companion", "name":"同行者"}])
	boat = BoatVisual.new()
	boat.name = "ClarityReferenceBoat"
	map.world.add_child(boat)
	_check(boat.build(), "现有灵舟与真实舟上人物加载")
	VisualScale.overlay(boat, VisualScale.FLIGHT_DEPTH)
	boat.set_map_zoom(map.world_zoom)
	boat.set_flight_position(Vector3(point.x + 185.0, point.y + 10.0, 0))
	await _frames(8)
	await _capture("near_landscape.png")
	_check(absf(map.player_mark.scale.y * map.PLAYER_PAWN_TEXTURE.get_height() * map.world_zoom - VisualScale.PLAYER_HEIGHT) < 0.001, "主角40屏幕像素")
	_check(absf(boat.driver_avatar.scale.y * boat.driver_avatar.native_height - VisualScale.NPC_HEIGHT) < 0.001, "舟上NPC与主角共用40屏幕像素")
	_check(absf(boat.player_avatar.scale.y * boat.player_avatar.native_height - VisualScale.PLAYER_HEIGHT) < 0.001, "舟上主角40屏幕像素")
	_check(boat.player_avatar.z_index < boat.rail.z_index, "前栏遮挡舟上人物")
	var native_part: Node = boat.player_avatar.find_child("NativeMale101Root", true, false)
	if native_part != null:
		for child in native_part.get_children():
			if child is CanvasItem: _check(child.z_index == 0, "舟上原生人物部件保持同一深度 " + str(child.name))
	boat.hide()
	map.set_player_travel_party([])
	await _occlusion_checks()
	for level in [0, 1, 2]:
		map._set_zoom_level(level, get_viewport_rect().size * 0.5)
		map.focus_world_point(point)
		await _frames(7)
		map.landscape.refresh(true)
		_check(map.landscape.overview.visible == (level == 0), "全域树群四次批绘切换 " + str(level))
		var all_intersect := true
		var view_rect: Rect2 = map.landscape._last_rect
		for id: String in map.landscape.active:
			all_intersect = all_intersect and view_rect.intersects(map.landscape.entries[id].world_rect)
		_check(all_intersect and map.landscape.active.size() < map.landscape.entries.size() * 0.6, "仅实例化视口附近景物 " + str(level))
		results.append("ZOOM %d %s" % [level, JSON.stringify(map.landscape.get_debug_summary())])
		await _capture("landscape_zoom_%d.png" % level)
	map._set_zoom_level(2, get_viewport_rect().size * 0.5)
	map.focus_world_point(point)
	map.landscape.hide()
	await _frames(3)
	await _capture("ground_and_water_only.png")
	map.landscape.show()
	await _migration_checks()
	results.append("SCENERY " + JSON.stringify(map.landscape.get_debug_summary()))
	results.append("CHECKS %d FAILURES %d" % [checks, failures])
	var report := FileAccess.open(OUT + "visual_report.txt", FileAccess.WRITE)
	report.store_string("\n".join(results) + "\n")
	print(results[-1])
	get_tree().quit(0 if failures == 0 else 1)

func _migration_checks() -> void:
	var anchor: Vector2 = map.travel.graph.positions["bc_place_001"]
	_check(map.restore_node_state({"current_node":"bc_place_001","remaining_route":[]}), "旧节点存档仍可恢复")
	var current: Dictionary = map.get_node_state()
	_check(map.restore_node_state(current) and map.player_mark.position == anchor, "当前存档精确原位恢复")
	var legacy := current.duplicate(true)
	legacy.erase("navigation_profile")
	legacy.erase("npc_navigation_profile")
	var formerly_clear := Vector2(-1,-1)
	for y in map.navigation.columns:
		for x in map.navigation.columns:
			if str(map.navigation.previous_rows[y])[x] != "0" or str(map.navigation.rows[y])[x] != "1": continue
			var point: Vector2 = (Vector2(x,y)+Vector2.ONE*.5)*map.navigation.cell_size
			if point.distance_to(anchor) < 500.0 and map.navigation.nearest_walkable(point).x >= 0:
				formerly_clear = point
				break
		if formerly_clear.x >= 0: break
	_check(formerly_clear.x >= 0, "存在旧档落在新完整山面上的真实案例")
	legacy.world_position = [formerly_clear.x,formerly_clear.y]
	legacy.waypoints = [[anchor.x,anchor.y,""]]
	legacy.destination_node = "bc_place_001"
	legacy.resume_movement = true
	legacy.visible_encounters = [{"id":"qa_old_mountain_event","kind":"wildlife","position":[formerly_clear.x,formerly_clear.y]}]
	_check(map.restore_node_state(legacy), "旧连续在途档迁移至山脚并重算绕山路线")
	_check(map.navigation.is_walkable(map.player_mark.position) and map.player_mark.position != formerly_clear, "旧山内位置迁到附近合法地面")
	var upgraded: Dictionary = map.get_node_state()
	_check(str(upgraded.navigation_profile) == map.Navigation.PROFILE_ID and upgraded.visible_encounters.size() == 1, "迁移记录新导航版本并保留旧遭遇")
	var points := PackedVector2Array([map.player_mark.position])
	for row in upgraded.waypoints: points.append(Vector2(row[0],row[1]))
	_check(map.navigation.validate_path(points), "迁移后的剩余行程全部避山")
	var restored: Vector2 = map.player_mark.position
	_check(map.restore_node_state(upgraded) and map.player_mark.position == restored, "新档二次读取不重复挪位")
	legacy.resume_movement = false
	legacy.waypoints = []
	legacy.visible_encounters = []
	_check(map.restore_node_state(legacy) and not map.travel_busy and map.waypoints.is_empty(), "旧驻足档迁移后保持驻足")
	var invalid: Dictionary = map.get_node_state()
	invalid.navigation_profile = "unknown_navigation"
	var held: Vector2 = map.player_mark.position
	_check(not map.restore_node_state(invalid) and map.player_mark.position == held, "未知导航版本拒绝且不改动当前状态")
	legacy.world_position = [-2.0,3.0]
	_check(not map.restore_node_state(legacy) and map.player_mark.position == held, "损坏旧坐标拒绝且不改动当前状态")
	var previous := PreviousGraph.new()
	previous.use_free_world = true
	_check(previous.load_layout("res://resources/world/bai_chuan/free_layout_3x3_20261104.json"), "旧押送几何来源可加载")
	var route: Array = map.travel.graph.shortest_path("bc_place_001", "bc_place_070")
	var assigned := current.duplicate(true)
	assigned.erase("navigation_profile")
	assigned.movement_mode = "assigned"
	assigned.remaining_route = route
	assigned.destination_node = route[-1]
	assigned.resume_movement = true
	assigned.waypoints = []
	for i in range(1, route.size()):
		var old_path: PackedVector2Array = previous.points_between(route[i-1],route[i])
		for j in range(1, old_path.size()): assigned.waypoints.append([old_path[j].x,old_path[j].y,route[i] if j==old_path.size()-1 else ""])
	_check(map.restore_node_state(assigned), "旧连续押送档按新导航完整恢复")
	var checkpoint_ids: Array = []
	var legal_points := PackedVector2Array([map.player_mark.position])
	for waypoint: Dictionary in map.waypoints:
		legal_points.append(waypoint.position)
		if not str(waypoint.node).is_empty(): checkpoint_ids.append(str(waypoint.node))
	_check(checkpoint_ids == route.slice(1), "旧押送检查点顺序和身份全部保持")
	_check(map.navigation.validate_path(legal_points), "迁移后的押送几何绕山且不穿水")
	_check(not map.travel_busy and map._resume_requested, "押送迁移只恢复进度，未自行推进时间")
	map.interrupt_assigned_route()

func _occlusion_checks() -> void:
	var probe_path := OUT + "occlusion_probes.json"
	if not FileAccess.file_exists(probe_path):
		_check(false, "山树像素探针存在")
		return
	var probes: Array = JSON.parse_string(FileAccess.get_file_as_string(probe_path))
	map.player_halo.hide()
	map.player_avatar.set_process(false)
	map.set_process(false)
	for probe: Dictionary in probes:
		var entry: Dictionary = map.landscape.entries[str(probe.id)]
		var point := Vector2(probe.point[0], probe.point[1])
		map.player_mark.position = point
		map.focus_world_point(point)
		map.landscape.refresh(true)
		VisualScale.place_on_ground(map.player_mark, point.y)
		await _frames(4)
		var scenery: Node2D = map.landscape.active.get(str(probe.id))
		_check(is_instance_valid(scenery), "遮挡探针景物可见 " + str(probe.id))
		if not is_instance_valid(scenery): continue
		for other: Node2D in map.landscape.active.values(): other.visible = other == scenery
		_check(map.player_mark.z_index < scenery.z_index, "人物位于景物后方 " + str(probe.id))
		var region := Rect2i(Vector2i(map.player_mark.get_global_transform_with_canvas().origin) - Vector2i(35, 52), Vector2i(70, 57))
		map.player_mark.hide()
		var hidden := await _screen()
		map.player_mark.show()
		var behind := await _screen()
		var covered := _changed(hidden, behind, region)
		scenery.hide()
		map.player_mark.hide()
		hidden = await _screen()
		map.player_mark.show()
		var uncovered := await _screen()
		var visible := _changed(hidden, uncovered, region)
		scenery.show()
		_check(visible > 80 and covered < visible * float(probe.max_visible_fraction), "真实像素遮挡 %s 后方%d/无遮挡%d" % [probe.id, covered, visible])
		await _capture(str(probe.id) + "_behind.png")
		# Keep the same world position and art, changing only the foot depth.
		# This proves alpha compositing, rather than comparing different ground.
		VisualScale.overlay(map.player_mark, scenery.z_index + 1)
		map.player_mark.hide()
		hidden = await _screen()
		map.player_mark.show()
		var front := await _screen()
		var foreground := _changed(hidden, front, region)
		_check(foreground > visible * 0.85, "真实像素前景 %s 前方%d/无遮挡%d" % [probe.id, foreground, visible])
		await _capture(str(probe.id) + "_front.png")
		for other: Node2D in map.landscape.active.values(): other.show()
	map.set_process(true)
	map.player_halo.show()

func _changed(a: Image, b: Image, region: Rect2i) -> int:
	var count := 0
	var clipped := region.intersection(Rect2i(Vector2i.ZERO, a.get_size()))
	for y in range(clipped.position.y, clipped.end.y):
		for x in range(clipped.position.x, clipped.end.x):
			var av := a.get_pixel(x, y)
			var bv := b.get_pixel(x, y)
			if absf(av.r-bv.r)+absf(av.g-bv.g)+absf(av.b-bv.b) > 0.04: count += 1
	return count

func _screen() -> Image:
	await _frames(2)
	await RenderingServer.frame_post_draw
	return get_viewport().get_texture().get_image()

func _frames(count: int) -> void:
	for i in count: await get_tree().process_frame

func _capture(file: String) -> void:
	var image := await _screen()
	image.save_png(OUT + file)

func _check(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures += 1
	var line := ("PASS " if ok else "FAIL ") + message
	results.append(line)
	print(line)
