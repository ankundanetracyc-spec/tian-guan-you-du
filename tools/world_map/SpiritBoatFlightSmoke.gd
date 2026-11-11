extends SceneTree

const MapRuntime = preload("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
const KM_PER_PIXEL = preload("res://gdscripts/world/BaiChuanWorldScale.gd").KM_PER_PIXEL
var checks := 0
var ground_encounters := 0
var ground_distance := 0.0

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var map = MapRuntime.new()
	root.add_child(map)
	map.set_process(false)
	if not _check(map.set_initial_runtime_id("white_stone_town"), "白石起点"): return
	var start: Vector2 = map.player_mark.position
	var dest: Vector2 = map.travel.graph.positions[map.travel.graph.node_for_runtime_id("yuxu_xianzong_core")]
	var expected_km := start.distance_to(dest) * KM_PER_PIXEL
	map.distance_traveled.connect(func(km): ground_distance += km)
	map.visible_encounter_requested.connect(func(_event): ground_encounters += 1)
	if not _check(map.begin_spirit_boat_flight("yuxu_xianzong_core", 2400.0, "test_driver"), "接引启航"): return
	if not _check(map.movement_mode == "flight" and map.waypoints.size() == 1 and map.player_mark.visible == false, "飞行状态和舟上人物"): return
	if not _check(absf(map._remaining_hours() - expected_km * 60.0 / 2400.0) < 0.001, "预计时长使用舟遁速和直线距离"): return
	if not _check(not map.navigate_to_world(start) and map.stop_button.disabled, "接引航线不能被地面操作打断"): return
	var clock := root.get_node_or_null("GameTimeManager")
	if clock:
		clock.set("is_paused", true)
		var pause_point: Vector2 = map.player_mark.position
		var pause_visual_time: float = map.flight_boat.visual_time
		map._process(0.1)
		map.flight_boat._process(0.1)
		if not _check(map.player_mark.position == pause_point and map.flight_boat.visual_time == pause_visual_time, "暂停冻结位置与舟身动态"): return
		clock.set("is_paused", false)
	var initial_hours: float = clock.call("get_precise_game_hours") if clock else 0.0
	var slower = MapRuntime.new()
	root.add_child(slower)
	slower.set_process(false)
	if not _check(slower.set_initial_runtime_id("white_stone_town") and slower.begin_spirit_boat_flight("yuxu_xianzong_core", 1200.0, "slow_driver"), "不同舟速测试启航"): return
	var slower_start: Vector2 = slower.player_mark.position
	slower._advance_flight(0.1)
	map._advance_flight(0.1)
	if not _check(map.player_mark.position.distance_to(start) > slower.player_mark.position.distance_to(slower_start), "更高舟速应在画面中移动得更快"): return
	# Exclude the separate slow boat's game-time test from the fast route's total.
	initial_hours = float(clock.call("get_precise_game_hours")) - start.distance_to(map.player_mark.position) * KM_PER_PIXEL * 60.0 / 2400.0 if clock else 0.0
	for i in 20: map._advance_flight(0.1)
	var moved_km := start.distance_to(map.player_mark.position) * KM_PER_PIXEL
	if not _check(moved_km > 0.0 and map.flight_height > 0.0, "位置和高度同步推进"): return
	if clock and not _check(absf((float(clock.call("get_precise_game_hours")) - initial_hours) - moved_km * 60.0 / 2400.0) < 0.001, "真实时间随舟速结算"): return
	if not _check(ground_distance == 0.0 and ground_encounters == 0, "空中不推进地面遭遇"): return

	var obstructed := Vector2.ZERO
	var found := false
	for i in range(1, 99):
		var candidate := start.lerp(dest, float(i) / 100.0)
		if not map.navigation.is_walkable(candidate):
			obstructed = candidate
			found = true
			break
	if not _check(found, "测试航线须实际经过不可行走的山河"): return
	map.player_mark.position = obstructed
	map.player_halo.position = obstructed
	map._rebuild_remaining_metrics()
	map.hazards.append({"id": "ground_wolf_test", "kind": "wildlife", "position": obstructed})
	if not _check(not map._check_hazard_contact(), "正处地面野兽上方仍不触发遭遇"): return
	map.hazards.clear()
	var saved: Dictionary = map.get_node_state()
	var restored = MapRuntime.new()
	root.add_child(restored)
	restored.set_process(false)
	if not _check(restored.restore_node_state(saved), "山河上空的飞行状态应可恢复"): return
	if not _check(restored.movement_mode == "flight" and restored.flight_driver_id == "test_driver" and absf(restored.flight_speed - 2400.0) < 0.001 and restored.flight_height > 0.0, "保存恢复舟速、高度与驾舟人"): return
	if not _check(restored.player_mark.position.distance_to(obstructed) < 0.001, "读档保持空中地面投影"): return
	# A real pre-expansion escort can be suspended over terrain which was not
	# walkable in either map; reconstruct that source's profile and destination.
	var previous: Dictionary = saved.duplicate(true)
	var previous_graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd").new()
	previous_graph.use_free_world = true
	if not _check(previous_graph.load_layout("res://resources/world/bai_chuan/free_layout_mountains_20261106.json"), "旧山体版地图来源存在"): return
	previous.world_size = 7372.8
	previous.navigation_profile = "complete_mountains_20261106_v1"
	previous.world_position = [obstructed.x / 3.0, obstructed.y / 3.0]
	var previous_destination: Vector2 = previous_graph.positions[str(previous.destination_node)]
	previous.waypoints = [[previous_destination.x, previous_destination.y, ""]]
	var from_previous = MapRuntime.new()
	root.add_child(from_previous)
	from_previous.set_process(false)
	if not _check(from_previous.restore_node_state(previous), "旧山体版接引飞行存档应迁移"): return
	if not _check(from_previous.player_mark.position.distance_to(obstructed) < 0.01 and from_previous.flight_driver_id == "test_driver" and absf(from_previous.flight_height - map.flight_height) < 0.001, "旧接引飞行迁移保留位置、高度和驾舟人"): return
	var bad: Dictionary = saved.duplicate(true)
	bad.flight_speed = -1.0
	var invalid = MapRuntime.new()
	root.add_child(invalid)
	if not _check(not invalid.restore_node_state(bad), "拒绝无效舟速存档"): return
	restored.resume_saved_travel()
	var count := 0
	while restored.travel_busy and count < 2000:
		restored._advance_flight(0.1)
		count += 1
	if not _check(not restored.travel_busy and restored.movement_mode == "free" and restored.player_mark.position.distance_to(dest) < 0.001, "读档后落至玉虚山门"): return
	if not _check(restored.get_current_location_runtime_id() == "yuxu_xianzong_core" and restored.player_mark.visible, "落地恢复正常地点入口"): return
	if clock:
		var month_map = MapRuntime.new()
		root.add_child(month_map)
		month_map.set_process(false)
		if not _check(month_map.set_initial_runtime_id("white_stone_town") and month_map.begin_spirit_boat_flight("yuxu_xianzong_core", 2400.0, "month_driver"), "跨月行程启动"): return
		clock.call("restore_calendar", 1, 1, 30, 23, 0.95)
		month_map._advance_flight(0.1)
		if not _check(month_map._clock_waiting, "跨月飞行等待时间结算"): return
		for i in 8: await process_frame
		if not _check(not month_map._clock_waiting and int(clock.get("month")) == 2 and month_map.travel_busy, "跨月结算完成后继续飞行"): return
	print("SPIRIT_BOAT_FLIGHT_SMOKE_OK ", checks)
	quit()

func _check(ok: bool, label: String) -> bool:
	checks += 1
	if ok: return true
	push_error("SPIRIT_BOAT_FLIGHT_SMOKE_FAILED: " + label)
	quit(1)
	return false
