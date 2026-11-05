extends SceneTree

var checks := 0
var failed := false

class MissingConnectorNavigation extends RefCounted:
	func find_path(_from: Vector2, _to: Vector2) -> PackedVector2Array:
		return PackedVector2Array()

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failed = true
		push_error("FREE_WORLD_CHECK: " + message)

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var expected := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--expected-appdata="): expected = arg.trim_prefix("--expected-appdata=")
	check(not expected.is_empty() and OS.get_environment("APPDATA") == expected, "isolated data required")
	if failed:
		quit(1)
		return
	var script = load("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
	check(script != null and script.can_instantiate(), "free map parses")
	if failed:
		quit(1)
		return
	var map = script.new()
	root.add_child(map)
	current_scene = map
	for i in 3: await process_frame
	map.set_process(false)
	check(map._free_ready, "navigation ready")
	check(map.transit_views.is_empty(), "no empty node controls")
	check(map.place_views.size() == 123, "all named places retained")
	check(not map.ink.paths_visible and not map.ink.marks_visible, "road/node web hidden")
	var nav = map.navigation
	var scale = load("res://gdscripts/world/BaiChuanWorldScale.gd")
	check(is_equal_approx(nav.world_size, scale.MAP_PIXELS), "navigation matches free-world canvas")
	check(is_equal_approx(nav.KM_PER_PIXEL * nav.world_size, 8000.0), "free-world spans 8000 kilometres")
	check(is_equal_approx(map.travel.graph.km_per_pixel, nav.KM_PER_PIXEL), "free navigation and authored routes share distance scale")
	_verify_display_scale(map, nav, scale)
	var origin: Vector2 = map.player_mark.position
	var time = root.get_node("GameTimeManager")
	await _verify_terrain_motion(map, time)
	var blocked_samples := {}
	for y in range(2, nav.columns - 2):
		for x in range(2, nav.columns - 2):
			var kind: String = str(nav.rows[y])[x]
			if kind in ["1", "2"] and not blocked_samples.has(kind):
				blocked_samples[kind] = (Vector2(x, y) + Vector2.ONE * 0.5) * nav.cell_size
		if blocked_samples.size() == 2: break
	for kind: String in ["1", "2"]:
		var has_sample := blocked_samples.has(kind)
		var point: Vector2 = blocked_samples.get(kind, Vector2(-1, -1))
		check(has_sample and not nav.is_walkable(point) and nav.find_path(origin, point).is_empty(), "terrain rejects blocked destination " + ("mountain" if kind == "1" else "water"))
	var valid_roads := 0
	for edge_key in map.travel.graph.edge_points:
		if nav.validate_path(map.travel.graph.edge_points[edge_key]): valid_roads += 1
		else: print("FREE_WORLD_ILLEGAL_ROAD ", edge_key)
	check(valid_roads == map.travel.graph.edge_points.size(), "all existing crossing corridors remain legal: %d/%d" % [valid_roads, map.travel.graph.edge_points.size()])
	var started := Time.get_ticks_msec()
	for id in map._all_place_ids():
		var path: PackedVector2Array = nav.find_path(origin, map.travel.graph.positions[id])
		check(not path.is_empty(), "place reachable " + str(id))
		check(nav.validate_path(path), "path avoids blocked terrain " + str(id))
	print("FREE_WORLD_ALL_PLACES_MS ", Time.get_ticks_msec() - started)
	var target := Vector2.ZERO
	for radius: float in [nav.cell_size * 8.0, nav.cell_size * 10.0, nav.cell_size * 12.0]:
		for direction in 16:
			var point: Vector2 = origin + Vector2.from_angle(TAU * direction / 16.0) * radius
			var candidate_path: PackedVector2Array = nav.find_path(origin, point)
			var candidate_length: float = nav.path_length(candidate_path)
			if candidate_path.size() > 1 and candidate_length > 180.0 and candidate_length < nav.cell_size * 24.0:
				target = point
				break
		if target != Vector2.ZERO: break
	check(target != Vector2.ZERO, "open ground target exists")
	var before := float(time.get_precise_game_hours())
	check(map.navigate_to_world(target), "start free travel")
	var initial_cost: float = _live_path_cost(map)
	map._advance_motion(35.0)
	var spent_cost: float = initial_cost - _live_path_cost(map)
	var stopped_at: Vector2 = map.player_mark.position
	check(stopped_at.distance_to(origin) > 1 and stopped_at.distance_to(target) > 1, "mid-path continuous position")
	map.stop_free_travel()

	check(not map.is_traveling() and map.waypoints.is_empty(), "stop clears auto-resume")
	check(map.player_mark.position == stopped_at, "stop never snaps to anchor")
	check(absf(float(time.get_precise_game_hours()) - before - map.travel.graph.hours_for_km(spent_cost * nav.KM_PER_PIXEL, map.dun_speed)) < 0.003, "only terrain cost of completed movement advances time")
	check(not map.get_node_state().resume_movement, "stopped save remains stopped")
	check(map.navigate_to_world(target), "redirect from mid-path")
	map._advance_motion(19.0)
	var saved: Dictionary = map.get_node_state()
	check(is_equal_approx(float(saved.world_size), nav.world_size) and is_equal_approx(float(saved.world_km), 8000.0), "new saves record exact world scale")
	check(saved.version == 2 and saved.terrain_speed_profile == "terrain-speed-v1", "terrain profile tag preserves version 2 coordinate schema")
	var saved_point: Vector2 = map.player_mark.position
	map.stop_free_travel()
	check(map.restore_node_state(saved), "restore moving continuous state")
	check(map.player_mark.position == saved_point, "restore exact mid-path position")
	map.resume_saved_travel()
	check(map.is_traveling(), "saved movement resumes")
	var remaining_cost: float = _live_path_cost(map)
	before = float(time.get_precise_game_hours())
	await _advance_to_completion(map)
	check(map.player_mark.position.distance_to(target) < 0.001, "free target exact arrival")
	var elapsed := float(time.get_precise_game_hours()) - before
	var expected_hours: float = map.travel.graph.hours_for_km(remaining_cost * nav.KM_PER_PIXEL, map.dun_speed)
	print("FREE_WORLD_RESTORED_TIME observed=", elapsed, " expected=", expected_hours, " delta=", elapsed - expected_hours)
	check(absf(elapsed - expected_hours) < maxf(0.003, expected_hours * 0.000001), "restore does not replay distance")
	var invalid := saved.duplicate(true)
	invalid.world_position = [-20, 50]
	check(not map.restore_node_state(invalid) and map.player_mark.position == target, "reject corrupt save atomically")
	invalid = saved.duplicate(true)
	invalid.world_size = nav.world_size * 0.5
	check(not map.restore_node_state(invalid) and map.player_mark.position == target, "reject wrong canvas without replacing position")
	invalid = saved.duplicate(true)
	invalid.world_km = 200.0
	check(not map.restore_node_state(invalid) and map.player_mark.position == target, "reject wrong distance scale without replacing position")
	check(map.restore_node_state({"current_node": "bc_place_001", "remaining_route": []}), "legacy node save accepted")
	check(map.player_mark.position.distance_to(origin) < 0.001, "legacy location mapped")
	check(not map.navigate_to_world(Vector2(-5, -5)), "out-of-map target rejected")
	# Visible events are saved before contact and do not force an immediate popup.
	check(map.navigate_to_world(target), "hazard route")
	var event := {"id": "free_smoke_hazard", "kind": "wildlife", "overshoot_km": 0.0}
	check(map.spawn_visible_encounter(event), "spawn visible event ahead")
	check(not map.encounter_blocked and map.is_traveling(), "visible event permits a detour")
	var hazard_point: Vector2 = map.hazards[0].position
	check(hazard_point.distance_to(map.player_mark.position) > 100, "event gives room to avoid")
	var hazard_save: Dictionary = map.get_node_state()
	map.stop_free_travel()
	check(map.restore_node_state(hazard_save) and map.hazards.size() == 1, "event position survives restore")
	map.resume_saved_travel()
	map._advance_motion(map._remaining_cost_length() + 1.0)
	while map._clock_waiting: await process_frame
	check(map.encounter_blocked and not map.is_traveling(), "proximity contact pauses")
	check(not map.waypoints.is_empty(), "contact preserves remaining path")
	map.resolve_visible_encounter("free_smoke_hazard")
	map.set_encounter_blocked(false)
	map.resume_saved_travel()
	check(map.hazards.is_empty() and map.is_traveling(), "resolved event resumes without retrigger")
	map.stop_free_travel()
	await _verify_previous_canvas_saves(map, saved, hazard_save, scale, time)
	check(map.restore_node_state({"current_node": "bc_place_001", "remaining_route": []}), "reset at named place")
	check(map.get_current_location_runtime_id() == "yuxu_xianzong_core", "nearby gate identity")
	var path: Array[String] = map.get_assigned_route_to_node("east_woods")
	check(path.size() > 1, "escort route available")
	var escort_estimate: float = map.get_path_travel_hours(path)
	check(map.start_assigned_route(path, "east_woods"), "assigned route start")
	check(absf(map._remaining_hours() - escort_estimate) < 0.003, "escort estimate uses the actual assigned terrain path")
	map._advance_motion(7.0)
	while map._clock_waiting: await process_frame
	var assigned: Dictionary = map.get_node_state()
	var last_waypoint: Array = assigned.waypoints[-1]
	assigned.visible_encounters = [{"id": "previous_canvas_assigned", "kind": "bandit", "position": [last_waypoint[0], last_waypoint[1]]}]
	var assigned_old := _previous_canvas_state(assigned, scale)
	var assigned_km: float = map._remaining_length() * nav.KM_PER_PIXEL
	before = float(time.get_precise_game_hours())
	map.set_encounter_blocked(true)
	map.set_encounter_blocked(false)
	check(map.restore_node_state(assigned_old), "previous canvas assigned route and encounter migrate together")
	_check_restored_coordinates(map, assigned, "assigned")
	check(map.get_node_state().remaining_route == assigned.remaining_route and map.movement_mode == "assigned", "migration preserves assigned checkpoint identities and order")
	check(absf(map._remaining_length() * nav.KM_PER_PIXEL - assigned_km) < 0.002, "assigned remaining kilometres unchanged by canvas migration")
	check(is_equal_approx(float(time.get_precise_game_hours()), before), "assigned migration does not advance time")
	map.resolve_visible_encounter("previous_canvas_assigned")
	map.resume_saved_travel()
	var escort_hours: float = map.travel.graph.hours_for_km(_live_path_cost(map) * nav.KM_PER_PIXEL, map.dun_speed)
	var escort_clock: float = time.get_precise_game_hours()
	var escort_km_signal := [0.0]
	var on_escort_distance := func(km: float): escort_km_signal[0] += km
	map.distance_traveled.connect(on_escort_distance)
	var guard := 0
	while map.is_traveling() and guard < 5000:
		map._advance_motion(300)
		while map._clock_waiting: await process_frame
		guard += 1
	check(not map.is_traveling() and map.get_current_node() == path[-1], "escort checkpoints reach destination")
	check(map.get_current_location_runtime_id() == "east_woods", "escort ends at real location")
	check(absf(float(time.get_precise_game_hours()) - escort_clock - escort_hours) < maxf(0.01, escort_hours * 0.000001), "escort elapsed hours integrate terrain through every checkpoint")
	check(is_zero_approx(escort_km_signal[0]), "assigned terrain travel does not emit ordinary encounter mileage")
	map.distance_traveled.disconnect(on_escort_distance)
	print("BAICHUAN_FREE_WORLD_OK " if not failed else "BAICHUAN_FREE_WORLD_FAILED ", checks)
	current_scene = null
	map.queue_free()
	await process_frame
	quit(1 if failed else 0)

func _verify_display_scale(map, nav, scale) -> void:
	var previous: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://resources/world/bai_chuan/free_navigation_20261104.json"))
	check(nav.rows == previous.rows, "display compaction preserves every previous navigation cell")
	check(is_equal_approx(scale.MAP_PIXELS / scale.PREVIOUS_FREE_MAP_PIXELS, 0.6), "canvas width and height are exactly 60 percent")
	var previous_layout: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://resources/world/bai_chuan/free_layout_20261104.json"))
	var max_anchor_delta := 0.0
	for id: String in previous_layout.locations:
		var p: Array = previous_layout.locations[id].drawPos
		max_anchor_delta = maxf(max_anchor_delta, (map.travel.graph.positions[id] as Vector2).distance_to(Vector2(p[0], p[1]) * 0.6))
	check(max_anchor_delta < 0.002, "all 123 named anchors preserve geographic positions at 60 percent display scale")
	var max_km_delta := 0.0
	var previous_edge_km := {}
	for edge: String in previous_layout.displayEdges:
		var points: Array = previous_layout.displayEdges[edge]
		var length := 0.0
		for i in range(1, points.size()):
			var dx: float = float(points[i][0]) - float(points[i - 1][0])
			var dy: float = float(points[i][1]) - float(points[i - 1][1])
			length += sqrt(dx * dx + dy * dy)
		var km: float = length * scale.WORLD_KM / scale.PREVIOUS_FREE_MAP_PIXELS
		previous_edge_km[edge] = km
		max_km_delta = maxf(max_km_delta, absf(float(map.travel.graph.edge_km[edge]) - km))
	check(max_km_delta < 0.005, "all 575 authored edge distances retain previous kilometres")
	var adapter = load("res://gdscripts/world/BaiChuanNpcNodeGraph.gd").new()
	check(adapter.initialize(), "NPC graph initializes at compact display scale")
	var edge: String = previous_layout.displayGraph.edges.keys()[0]
	var ends: Dictionary = previous_layout.displayGraph.edges[edge]
	var hours: float = adapter.get_npc_edge_travel_hours(adapter.node_id_to_cell(ends.from), adapter.node_id_to_cell(ends.to), 100.0)
	var edge_points: PackedVector2Array = map.travel.graph.points_between(str(ends.from), str(ends.to))
	if not nav.validate_path(edge_points): edge_points = nav.find_path(map.travel.graph.positions[str(ends.from)], map.travel.graph.positions[str(ends.to)])
	var terrain_hours: float = map.travel.graph.hours_for_km(map.terrain_travel.path_cost_length(edge_points) * nav.KM_PER_PIXEL, 100.0)
	check(absf(hours - terrain_hours) < 0.005, "NPC edge travel hours integrate terrain at the unchanged 8000 km scale")
	adapter.free()
	var png := FileAccess.open(scale.ART_BASE + scale.MASTER_FILE, FileAccess.READ)
	png.big_endian = true
	png.seek(16)
	var pixels := png.get_32()
	var tile_pixels: int = pixels / scale.ART_COLUMNS
	var backgrounds := 0
	var correct_textures := true
	for child in map.world.get_children():
		if not child is Sprite2D or not str(child.name).begins_with("Background"): continue
		backgrounds += 1
		correct_textures = correct_textures and child.texture.get_size() == Vector2.ONE * tile_pixels
	check(backgrounds == scale.ART_COLUMNS * scale.ART_COLUMNS, "configured background tile count loaded")
	check(correct_textures and tile_pixels >= 1254, "configured master is loaded as nine exact native-detail crops")
	print("FREE_WORLD_COMPACT_SCALE max_anchor_delta=", max_anchor_delta, " max_edge_km_delta=", max_km_delta, " tiles=", backgrounds, " tile_pixels=", tile_pixels)

func _previous_canvas_state(current: Dictionary, scale) -> Dictionary:
	var previous := current.duplicate(true)
	var factor: float = scale.PREVIOUS_FREE_MAP_PIXELS / scale.MAP_PIXELS
	previous.world_size = scale.PREVIOUS_FREE_MAP_PIXELS
	previous.world_km = scale.WORLD_KM
	for axis in 2: previous.world_position[axis] *= factor
	for row: Array in previous.waypoints:
		for axis in 2: row[axis] *= factor
	for hazard: Dictionary in previous.visible_encounters:
		for axis in 2: hazard.position[axis] *= factor
	return previous

func _check_restored_coordinates(map, expected: Dictionary, label: String) -> void:
	var state: Dictionary = map.get_node_state()
	check(map.player_mark.position.distance_to(Vector2(expected.world_position[0], expected.world_position[1])) < 0.002, label + " migration preserves precise position")
	var route_matches: bool = state.waypoints.size() == expected.waypoints.size()
	if route_matches:
		for i in state.waypoints.size():
			var a: Array = state.waypoints[i]
			var b: Array = expected.waypoints[i]
			route_matches = route_matches and Vector2(a[0], a[1]).distance_to(Vector2(b[0], b[1])) < 0.002 and a[2] == b[2]
	check(route_matches, label + " migration preserves geometric path and checkpoint identities")
	var hazards_match: bool = state.visible_encounters.size() == expected.visible_encounters.size()
	if hazards_match:
		for i in state.visible_encounters.size():
			var a: Dictionary = state.visible_encounters[i]
			var b: Dictionary = expected.visible_encounters[i]
			hazards_match = hazards_match and Vector2(a.position[0], a.position[1]).distance_to(Vector2(b.position[0], b.position[1])) < 0.002 and a.id == b.id and a.kind == b.kind
	check(hazards_match, label + " migration preserves visible encounter positions and identity")
	check(state.resume_movement == expected.resume_movement and state.destination_node == expected.destination_node and state.target_npc == expected.target_npc, label + " migration preserves resume and target state")

func _verify_previous_canvas_saves(map, saved: Dictionary, hazard_save: Dictionary, scale, clock) -> void:
	var previous_layout: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://resources/world/bai_chuan/free_layout_20261104.json"))
	var before: float = clock.get_precise_game_hours()
	for id: String in ["bc_place_001", "bc_place_021", "bc_place_070"]:
		var anchor := {"version": 2, "world_size": scale.PREVIOUS_FREE_MAP_PIXELS, "world_km": scale.WORLD_KM,
			"current_node": id, "remaining_route": [], "world_position": previous_layout.locations[id].drawPos,
			"waypoints": [], "visible_encounters": [], "resume_movement": false}
		check(map.restore_node_state(anchor) and map.player_mark.position.distance_to(map.travel.graph.positions[id]) < 0.002, "previous canvas named anchor migrates " + id)
	var current := saved.duplicate(true)
	current.visible_encounters = hazard_save.visible_encounters.duplicate(true)
	current.target_npc = "previous_canvas_target"
	var previous := _previous_canvas_state(current, scale)
	var original_input := previous.duplicate(true)
	check(map.restore_node_state(previous), "previous canvas wilderness moving save migrates")
	_check_restored_coordinates(map, current, "moving wilderness")
	check(previous == original_input, "migration leaves original save dictionary unchanged")
	check(is_equal_approx(float(map.get_node_state().world_size), scale.MAP_PIXELS), "migrated save writes compact world scale")
	check(is_equal_approx(float(clock.get_precise_game_hours()), before), "restoring prior canvas does not charge travel time")
	var km_before: float = map._remaining_length() * map.navigation.KM_PER_PIXEL
	var paused := current.duplicate(true)
	paused.resume_movement = false
	check(map.restore_node_state(_previous_canvas_state(paused, scale)), "previous canvas paused route migrates")
	map.resume_saved_travel()
	check(not map.is_traveling() and is_equal_approx(float(clock.get_precise_game_hours()), before), "paused previous save stays paused without clock advance")
	_check_restored_coordinates(map, paused, "paused wilderness")
	var unchanged: Dictionary = map.get_node_state()
	for wrong_size: float in [scale.MAP_PIXELS * 0.5, scale.PREVIOUS_FREE_MAP_PIXELS * 2.0]:
		var invalid := previous.duplicate(true)
		invalid.world_size = wrong_size
		check(not map.restore_node_state(invalid) and map.get_node_state() == unchanged, "unknown canvas rejects position route and hazard changes atomically")
	var unidentified := previous.duplicate(true)
	unidentified.erase("world_km")
	check(not map.restore_node_state(unidentified) and map.get_node_state() == unchanged, "previous canvas without explicit kilometres rejects atomically")
	var stopped := paused.duplicate(true)
	stopped.waypoints = []
	check(map.restore_node_state(_previous_canvas_state(stopped, scale)), "previous canvas stopped wilderness save migrates")
	map.resume_saved_travel()
	check(not map.is_traveling() and map.waypoints.is_empty(), "stopped previous save cannot restart itself")
	check(map.restore_node_state(previous), "previous canvas moving state restores for continued travel")
	check(absf(map._remaining_length() * map.navigation.KM_PER_PIXEL - km_before) < 0.002, "wilderness remaining kilometres are invariant through migration")
	map.resume_saved_travel()
	check(map.is_traveling(), "previous moving save resumes explicitly")
	var cost_before: float = _live_path_cost(map)
	map._advance_motion(7.0)
	while map._clock_waiting: await process_frame
	var cost_spent: float = cost_before - _live_path_cost(map)
	var expected_hours: float = map.travel.graph.hours_for_km(cost_spent * map.navigation.KM_PER_PIXEL, map.dun_speed)
	check(absf(float(clock.get_precise_game_hours()) - before - expected_hours) < 0.003, "resumed migrated movement charges only the new terrain cost")
	map.stop_free_travel()

func _live_path_cost(map) -> float:
	var points := PackedVector2Array([map.player_mark.position])
	for waypoint: Dictionary in map.waypoints: points.append(waypoint.position)
	return map.terrain_travel.path_cost_length(points)

func _advance_to_completion(map) -> void:
	var guard := 0
	while map.is_traveling() and guard < 5000:
		map._advance_motion(map._remaining_cost_length() + 1.0)
		while map._clock_waiting: await process_frame
		guard += 1
	check(guard < 5000, "travel completes within bounded terrain steps")

func _terrain_test_state(base: Dictionary, from: Vector2, to: Vector2) -> Dictionary:
	var state := base.duplicate(true)
	state.world_position = [from.x, from.y]
	state.waypoints = [[to.x, to.y, ""]]
	state.visible_encounters = []
	state.remaining_route = []
	state.movement_mode = "free"
	state.destination_node = ""
	state.target_npc = ""
	state.resume_movement = true
	return state

func _verify_terrain_motion(map, clock) -> void:
	var base: Dictionary = map.get_node_state()
	var nav = map.navigation
	var terrain = map.terrain_travel
	check(terrain.ready, "terrain speed profile initialized")
	var samples := {}
	var mixed := PackedVector2Array()
	var ulp_sample := Vector2.ZERO
	for y in range(1, nav.columns - 1):
		for x in range(1, nav.columns - 1):
			if str(nav.rows[y])[x] != "0": continue
			var point: Vector2 = (Vector2(x, y) + Vector2.ONE * 0.5) * nav.cell_size
			var kind: String = str(terrain.terrain_at(point).id)
			if not samples.has(kind): samples[kind] = point
			if ulp_sample == Vector2.ZERO and point.x > nav.world_size * 0.75: ulp_sample = point
			if mixed.is_empty() and str(nav.rows[y])[x + 1] == "0":
				var adjacent: Vector2 = point + Vector2(nav.cell_size, 0.0)
				if not is_equal_approx(terrain.speed_multiplier_at(point), terrain.speed_multiplier_at(adjacent)) and nav.segment_clear(point, adjacent):
					mixed = PackedVector2Array([point, adjacent])
			if samples.size() == 4 and mixed.size() == 2 and ulp_sample != Vector2.ZERO: break
		if samples.size() == 4 and mixed.size() == 2 and ulp_sample != Vector2.ZERO: break
	check(samples.size() == 4, "real navigation contains plain forest hill and bridge samples")
	check(mixed.size() == 2, "real adjacent cells expose a traversable speed boundary")
	var actual_km := [0.0]
	var on_distance := func(km: float): actual_km[0] += km
	map.distance_traveled.connect(on_distance)
	var normalized_hours := {}
	for kind: String in ["plain", "forest", "hill", "crossing"]:
		if not samples.has(kind): continue
		var point: Vector2 = samples[kind]
		var from := point - Vector2(nav.cell_size * 0.2, 0.0)
		var to := point + Vector2(nav.cell_size * 0.2, 0.0)
		var multiplier: float = terrain.speed_multiplier_at(point)
		var state := _terrain_test_state(base, from, to)
		check(map.restore_node_state(state), "restore real surface sample " + kind)
		map.resume_saved_travel()
		map._update_status()
		var surface: Dictionary = terrain.terrain_at(from)
		check(map.travel_status.text.contains("%s · 速度%d%%" % [surface.name, roundi(multiplier * 100.0)]), "HUD identifies current terrain speed " + kind)
		actual_km[0] = 0.0
		var before: float = clock.get_precise_game_hours()
		map._advance_motion(2.0)
		while map._clock_waiting: await process_frame
		var moved: float = from.distance_to(map.player_mark.position)
		check(absf(moved - 2.0 * multiplier) < 0.003, "equal frame budget moves at terrain multiplier " + kind)
		check(absf(actual_km[0] - moved * nav.KM_PER_PIXEL) < 0.003, "encounter clock receives true kilometres " + kind)
		check(absf(float(clock.get_precise_game_hours()) - before - map.travel.graph.hours_for_km(terrain.segment_cost_length(from, map.player_mark.position) * nav.KM_PER_PIXEL, map.dun_speed)) < 0.003, "partial surface time integrates actual cost " + kind)
		await _advance_to_completion(map)
		var elapsed: float = clock.get_precise_game_hours() - before
		var length := from.distance_to(to)
		var expected: float = map.travel.graph.hours_for_km(length * nav.KM_PER_PIXEL / multiplier, map.dun_speed)
		normalized_hours[kind] = elapsed / length
		check(map.player_mark.position == to and absf(elapsed - expected) < 0.003, "same-distance terrain hours match reciprocal speed " + kind)
		check(absf(actual_km[0] - length * nav.KM_PER_PIXEL) < 0.003, "completed terrain route never multiplies true mileage " + kind)
		check(is_zero_approx(map._remaining_length()) and is_zero_approx(map._remaining_cost_length()), "arrival clears terrain ETA caches " + kind)
	if normalized_hours.size() == 4:
		check(absf(normalized_hours.forest / normalized_hours.plain - 1.25) < 0.003, "forest equal-distance duration is 1.25 times plain")
		check(absf(normalized_hours.hill / normalized_hours.plain - 1.0 / 0.6) < 0.003, "hill equal-distance duration is 1.667 times plain")
		check(absf(normalized_hours.crossing / normalized_hours.plain - 1.0) < 0.003, "bridge retains plain-ground duration")
	if mixed.size() == 2:
		var from := mixed[0]
		var to := mixed[1]
		var cost: float = terrain.segment_cost_length(from, to)
		var budget := cost * 0.8
		var state := _terrain_test_state(base, from, to)
		check(map.restore_node_state(state), "restore mixed-terrain segment")
		map.resume_saved_travel()
		var before: float = clock.get_precise_game_hours()
		actual_km[0] = 0.0
		map._advance_motion(budget)
		while map._clock_waiting: await process_frame
		var once: Vector2 = map.player_mark.position
		var once_hours: float = clock.get_precise_game_hours() - before
		var spent: float = terrain.segment_cost_length(from, once)
		check(spent <= budget + 0.003 and absf(spent - budget) < 0.003, "mixed cell step respects its terrain cost budget")
		check(absf(from.distance_to(once) - terrain.distance_for_cost(from, to, budget)) < 0.003, "mixed cell step reaches the integrated distance")
		check(absf(actual_km[0] - from.distance_to(once) * nav.KM_PER_PIXEL) < 0.003, "cross-cell encounters receive actual distance")
		check(absf(map._remaining_cost_length() - _live_path_cost(map)) < 0.003 and absf(map._remaining_length() - once.distance_to(to)) < 0.003, "partial mixed route preserves ETA and distance caches")
		map.stop_free_travel()
		check(map.restore_node_state(state), "restore mixed segment for split-frame movement")
		map.resume_saved_travel()
		before = clock.get_precise_game_hours()
		for frame in 20:
			map._advance_motion(budget / 20.0)
			while map._clock_waiting: await process_frame
		check(map.player_mark.position.distance_to(once) < 0.01, "terrain boundary distance is independent of frame partition")
		check(absf(float(clock.get_precise_game_hours()) - before - once_hours) < 0.01, "terrain boundary hours are independent of frame partition")
		map.stop_free_travel()
	if samples.has("forest"):
		var point: Vector2 = samples.forest
		var from := point - Vector2(nav.cell_size * 0.2, 0.0)
		var to := point + Vector2(nav.cell_size * 0.2, 0.0)
		var state := _terrain_test_state(base, from, to)
		state.erase("terrain_speed_profile")
		var before: float = clock.get_precise_game_hours()
		check(map.restore_node_state(state) and map.player_mark.position == from and not map.is_traveling(), "old terrain-untagged save preserves coordinates and waits for explicit resume")
		check(absf(map._remaining_cost_length() - terrain.segment_cost_length(from, to)) < 0.003 and is_equal_approx(float(clock.get_precise_game_hours()), before), "old player save prices only future terrain without replaying time")
		state.terrain_speed_profile = "future-profile"
		check(map.restore_node_state(state) and map.player_mark.position == from and is_equal_approx(float(clock.get_precise_game_hours()), before), "unknown profile does not relocate player or replay elapsed time")
		var assigned: Array[String] = map.get_assigned_route_to_node("east_woods")
		if assigned.size() < 2: assigned = map.get_assigned_route_to_node("yuxu_xianzong_core")
		check(assigned.size() > 1 and from.distance_to(map.travel.graph.positions[assigned[0]]) > 0.01, "disconnected connector test has a distinct assigned-route origin")
		var original_navigation = map.navigation
		map.navigation = MissingConnectorNavigation.new()
		check(not is_finite(map.get_path_travel_hours(assigned)), "missing connector makes assigned terrain estimate unreachable")
		check(not map.start_assigned_route(assigned, map.get_runtime_id(assigned[-1])) and map.waypoints.is_empty() and map.player_mark.position == from, "missing connector rejects assigned movement without teleporting")
		map.navigation = original_navigation
		check(map.restore_node_state(state), "valid terrain route restores after disconnected connector rejection")
		map.resume_saved_travel()
		clock.is_paused = true
		map._process(0.1)
		clock.is_paused = false
		check(map.player_mark.position == from and is_equal_approx(float(clock.get_precise_game_hours()), before), "paused terrain travel consumes neither movement nor time")
		map._map_press_active = true
		map._process(0.1)
		map._map_press_active = false
		check(map.player_mark.position == from and is_equal_approx(float(clock.get_precise_game_hours()), before), "pressed map consumes neither movement nor time")
		map.dragging = true
		map._process(0.1)
		map.dragging = false
		check(map.player_mark.position == from and is_equal_approx(float(clock.get_precise_game_hours()), before), "dragged map consumes neither movement nor time")
		map.hide()
		map._process(0.1)
		map.show()
		check(map.player_mark.position == from and is_equal_approx(float(clock.get_precise_game_hours()), before), "hidden terrain travel consumes neither movement nor time")
		map.stop_free_travel()
		check(map.waypoints.is_empty() and is_zero_approx(map._remaining_cost_length()), "stop clears future terrain cost without charging time")
	check(ulp_sample != Vector2.ZERO, "far-map sample exposes Vector2 rounding at tiny frame budgets")
	if ulp_sample != Vector2.ZERO:
		var state := _terrain_test_state(base, ulp_sample, ulp_sample + Vector2(nav.cell_size * 0.2, 0.0))
		check(map.restore_node_state(state), "restore tiny-budget terrain route")
		map.resume_saved_travel()
		var before: float = clock.get_precise_game_hours()
		var remaining: float = map._remaining_cost_length()
		map._advance_motion(0.00011)
		check(map.player_mark.position == ulp_sample and map.is_traveling() and not map.waypoints.is_empty(), "sub-ULP terrain budget leaves the route pending without looping")
		check(is_equal_approx(float(clock.get_precise_game_hours()), before) and is_equal_approx(map._remaining_cost_length(), remaining), "zero representable displacement never charges time or terrain cost")
		map.stop_free_travel()
	map.distance_traveled.disconnect(on_distance)
	check(map.restore_node_state(base), "terrain checks restore original player state")
