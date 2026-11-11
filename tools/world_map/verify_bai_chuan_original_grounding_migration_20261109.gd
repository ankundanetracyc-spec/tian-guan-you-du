extends "res://tools/world_map/verify_bai_chuan_compact_peaks_migration_20261109.gd"
## Independent restored-navigation acceptance. Reuses only the prior read-only
## production persistence host and scalar terrain references; no Main or GPU.
const COMPACT := "compact_peaks_20261109_v1"
var fixtures_path := ""
var obstruction_cases: Array[String] = []
var npc_edges_checked := 0

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report-path="): report_path = argument.trim_prefix("--report-path=")
		if argument.begins_with("--python-report="): python_report_path = argument.trim_prefix("--python-report=")
		if argument.begins_with("--fixtures-path="): fixtures_path = argument.trim_prefix("--fixtures-path=")
		if argument.begins_with("--expected-appdata="): _check(OS.get_environment("APPDATA") == argument.trim_prefix("--expected-appdata="), "isolated APPDATA")
	call_deferred("_run")

func _v(raw: Array) -> Vector2:
	return Vector2(raw[0], raw[1])

func _legal_map_route(map: PersistenceMap) -> bool:
	var points := PackedVector2Array([map.player_mark.position])
	for row: Dictionary in map.waypoints: points.append(row.position)
	return nav.validate_path(points)

func _registry_restored() -> void:
	section = "restored source registry"
	var current := Registry.resolve(Navigation.PROFILE_ID, Scale.MAP_PIXELS)
	var compact := Registry.resolve(COMPACT, Scale.MAP_PIXELS)
	if not _check(not current.is_empty() and not compact.is_empty(), "restored and intermediate profiles resolve independently"): return
	_check(Navigation.PROFILE_ID == "expanded3x_20261106_v1" and current.navigation_path == Registry.ROOT + "free_navigation_expanded3x_20261106.json", "active navigation reads immutable original source")
	_check(compact.navigation_path == Registry.ROOT + "free_navigation_compact_peaks_20261109.json" and compact.layout_path == current.layout_path, "intermediate compact profile remains an immutable source with identical graph")
	var graph := Graph.new()
	graph.use_free_world = true
	_check(graph.load_layout(compact.layout_path), "actual intermediate source layout loads")
	_check(graph.positions == npc.graph.positions and graph.edge_points == npc.graph.edge_points, "all 444 positions and 575 original curves remain exactly identical")
	_check(Registry.resolve("unknown_future", Scale.MAP_PIXELS).is_empty(), "unknown same-size source is rejected")
	var paths_ok := true
	for key: String in npc.graph.edge_points:
		var curve: PackedVector2Array = npc.graph.edge_points[key]
		if not nav.validate_path(curve): paths_ok = false
		curve = curve.duplicate()
		curve.reverse()
		if not nav.validate_path(curve): paths_ok = false
	_check(paths_ok, "all 575 original routes pass production DDA in both directions")
	var ids_ok := true
	for id: String in npc.ids:
		if npc.cell_to_node_id(npc.node_id_to_cell(id)) != id or not nav.is_walkable(npc.graph.positions[id]): ids_ok = false
	_check(ids_ok and npc.ids.size() == 444, "all opaque IDs are unchanged and every anchor remains walkable")

func _npc_all_curves() -> void:
	section = "all NPC curve identities"
	for key: String in npc.graph.edge_points:
		var pair := key.split("~")
		for reverse: bool in [false, true]:
			if reverse: pair.reverse()
			var points: PackedVector2Array = npc.graph.points_between(pair[0], pair[1])
			var prefix := _old_prefix(points, .37, Scale.MAP_PIXELS, true)
			var hours := float(prefix.cost) * Scale.KM_PER_PIXEL * 60.0 / 100.0
			var from: Vector2i = npc.node_id_to_cell(pair[0])
			var to: Vector2i = npc.node_id_to_cell(pair[1])
			var exact := true
			for profile: String in [COMPACT, "expanded3x_20261106_v1"]:
				var converted: float = npc.upgrade_navigation_travel_progress(from, to, hours, 100.0, profile, Scale.MAP_PIXELS, "terrain-speed-v1", 2)
				if converted != hours: exact = false
				var actual: Vector2 = npc.project_map_position(from, [from, to], 1, converted, 0.0, 100.0)
				var error := actual.distance_to(prefix.point)
				max_npc_error = maxf(max_npc_error, error)
				if error > .05 or not nav.is_walkable(actual): exact = false
			_check(exact, "both intermediate and original profiles keep exact NPC hours on original curve", {"edge": key, "reverse": reverse})
			npc_edges_checked += 1
			if npc_edges_checked % 100 == 0:
				_check(npc.upgrade_navigation_travel_progress(from, to, hours, 100.0, COMPACT, 0.0, "terrain-speed-v1", 1) == hours, "tagged compact v1 infers its omitted size and preserves hours")

func _assigned_obstructed_fixture() -> Dictionary:
	for key: String in npc.graph.edge_points:
		var pair := key.split("~")
		var a: Vector2 = npc.graph.positions[pair[0]]
		var b: Vector2 = npc.graph.positions[pair[1]]
		if nav.segment_clear(a, b) or not Registry.segment_clear(a, b, COMPACT, Scale.MAP_PIXELS): continue
		var count := ceili(a.distance_to(b) / (nav.cell_size * .4))
		for i in range(1, count):
			var point := a.lerp(b, float(i) / count)
			if not nav.is_walkable(point): return {"from": str(pair[0]), "to": str(pair[1]), "start": a, "end": b, "blocked": point}
	return {}

func _blocked_cases(source: Dictionary, fixtures: Dictionary) -> void:
	section = "compact source obstructed by original mountains"
	var a := _v(fixtures.corridor.start)
	var p := _v(fixtures.corridor.blocked)
	var b := _v(fixtures.corridor.end)
	_check(Registry.is_walkable(p, COMPACT, Scale.MAP_PIXELS) and not nav.is_walkable(p), "standing fixture is truly compact-legal and restored-blocked")
	_check(nav.is_walkable(a) and nav.is_walkable(b) and Registry.segment_clear(a, b, COMPACT, Scale.MAP_PIXELS) and not nav.segment_clear(a, b), "route fixture has legal endpoints and a newly restored obstruction between them")
	var map := _make_map()
	var base := _base_state(npc.graph, a, COMPACT, Scale.MAP_PIXELS)
	var stationary := base.duplicate(true)
	stationary.world_position = [p.x, p.y]
	_check(map.restore_node_state(stationary), "compact standing position inside restored old mountain migrates")
	_check(nav.is_walkable(map.player_mark.position) and map.player_mark.position == nav.nearest_walkable(p) and not map._resume_requested, "restored obstruction relocates standing player to exact nearest legal ground and remains stopped")
	_check(map.get_node_state().navigation_profile == Navigation.PROFILE_ID and map.travel.current_node == base.current_node, "standing migration upgrades source without altering opaque current node")
	_check_repeat(map, "restored-blocked standing")
	obstruction_cases.append("standing inside original mountain")
	for kind: String in ["blocked_start", "blocked_end", "blocked_segment", "blocked_midpoint"]:
		var saved := base.duplicate(true)
		saved.resume_movement = true
		saved.world_position = [p.x, p.y] if kind == "blocked_start" else [a.x, a.y]
		var end := p if kind == "blocked_end" else b
		saved.waypoints = [[end.x, end.y, ""]]
		if kind == "blocked_midpoint": saved.waypoints.push_front([p.x, p.y, ""])
		_check(map.restore_node_state(saved), "compact free trip migrates through restored obstruction: " + kind)
		_check(_legal_map_route(map) and map._resume_requested and not map.waypoints.is_empty(), "restored free trip replans as a legal resumed production route: " + kind)
		var expected_end: Vector2 = nav.nearest_walkable(end)
		_check(map.waypoints[-1].position == expected_end, "restored free trip retains legal destination or selects nearest legal former endpoint: " + kind)
		if kind != "blocked_start": _check(map.player_mark.position == a, "legal player XY stays exact while obstructed route changes: " + kind)
		_check_repeat(map, kind)
		obstruction_cases.append(kind)
	var hazard := base.duplicate(true)
	hazard.visible_encounters = [{"id": "qa_recovered_hazard", "kind": "wildlife", "position": [p.x, p.y], "seed": 9201}]
	_check(map.restore_node_state(hazard), "compact hazard inside restored old mountain migrates independently")
	_check(map.player_mark.position == a and map.hazards.size() == 1 and map.hazards[0].position == nav.nearest_walkable(p) and map.hazards[0].id == "qa_recovered_hazard" and map.hazards[0].seed == 9201, "hazard-only obstruction preserves player XY and encounter identity/payload")
	obstruction_cases.append("hazard-only obstruction")
	var assigned := _assigned_obstructed_fixture()
	if _check(not assigned.is_empty(), "real same-graph assigned source shortcut crosses a restored mountain"):
		for blocked_start: bool in [false, true]:
			var saved := base.duplicate(true)
			var point: Vector2 = assigned.blocked if blocked_start else assigned.start
			var end: Vector2 = assigned.end
			saved.world_position = [point.x, point.y]
			saved.current_node = assigned.from
			saved.remaining_route = [assigned.from, assigned.to]
			saved.destination_node = assigned.to
			saved.movement_mode = "assigned"
			saved.waypoints = [[end.x, end.y, assigned.to]]
			_check(map.restore_node_state(saved), "compact paused escort migrates around restored mountain", {"blocked_start": blocked_start})
			_check(_legal_map_route(map) and map.movement_mode == "assigned" and map.travel.route == [assigned.from, assigned.to] and map.waypoints[-1].node == assigned.to and not map._resume_requested, "escort preserves opaque route/checkpoint IDs and pause state after obstruction")
			_check_repeat(map, "restored escort")
			obstruction_cases.append("assigned blocked start" if blocked_start else "assigned blocked segment")
	var flight := base.duplicate(true)
	var air := _v(fixtures.source_illegal)
	flight.world_position = [air.x, air.y]
	flight.movement_mode = "player_flight"
	flight.flight_speed = 2400.0
	flight.flight_height = 94.5
	flight.flight_driver_id = ""
	flight.player_boat_id = "qa_owned_spirit_boat"
	flight.waypoints = [[p.x, p.y, ""]]
	flight.resume_movement = true
	_check(map.restore_node_state(flight), "owned flight with newly obstructed landing point migrates")
	_check(map.player_mark.position == air and map.flight_height == 94.5 and map.flight_speed == 2400.0 and map.player_boat_id == "qa_owned_spirit_boat" and map.waypoints[-1].position == nav.nearest_walkable(p) and map._resume_requested, "flight preserves airborne XY, altitude, speed and ownership while selecting legal landing")
	_check_repeat(map, "flight landing restored obstruction")
	obstruction_cases.append("owned flight blocked landing")
	var snapshot := JSON.stringify(map.get_node_state())
	for kind: String in ["standing", "waypoint", "hazard"]:
		var invalid := base.duplicate(true)
		if kind == "standing": invalid.world_position = fixtures.source_illegal
		elif kind == "waypoint": invalid.waypoints = [[air.x, air.y, ""]]
		else: invalid.visible_encounters = [{"id": "qa_source_illegal", "kind": "wildlife", "position": fixtures.source_illegal, "seed": 7}]
		_check(not map.restore_node_state(invalid) and JSON.stringify(map.get_node_state()) == snapshot, "source itself illegal is atomically rejected before any relocation: " + kind)
	map.free()

func _compact_manager_batch() -> void:
	section = "real C# compact-source batch"
	var manager := root.get_node("NpcWorldManager")
	manager.set_tile_world_map(npc)
	manager.reset_npc_navigation_save_profile()
	var residents: Array = manager.get("npcs")
	residents.clear()
	var pair := str(npc.graph.edge_points.keys()[0]).split("~")
	var from: Vector2i = npc.node_id_to_cell(pair[0])
	var to: Vector2i = npc.node_id_to_cell(pair[1])
	var first = _world_npc("qa_restored_batch_a", from, to, 13.75)
	var second = _world_npc("qa_restored_batch_b", from, Vector2i(999, 999), 13.75)
	residents.append(first)
	residents.append(second)
	_check(not manager.migrate_navigation_travel(COMPACT, Scale.MAP_PIXELS, "terrain-speed-v1", 2), "late invalid opaque index rejects whole real compact batch")
	_check(first.world_travel_cell_progress_hours == 13.75 and first.travel_days_remaining == 77 and manager.get_npc_navigation_save_profile() == COMPACT, "batch failure keeps earlier NPC hours, time and exact source")
	second.world_travel_path[1] = to
	_check(manager.migrate_navigation_travel(COMPACT, Scale.MAP_PIXELS, "terrain-speed-v1", 2), "valid actual compact NPC batch upgrades to restored profile")
	_check(first.world_travel_cell_progress_hours == 13.75 and first.current_grid_cell == from and first.world_travel_path == [from, to] and manager.get_npc_navigation_save_profile() == Navigation.PROFILE_ID, "successful compact batch preserves exact hours and every opaque index")
	_check(manager.migrate_navigation_travel(Navigation.PROFILE_ID, Scale.MAP_PIXELS, "terrain-speed-v1", 2) and first.world_travel_cell_progress_hours == 13.75, "restored actual manager reload is idempotent")
	residents.clear()

func _run() -> void:
	var began := Time.get_ticks_msec()
	var clock := root.get_node("GameTimeManager")
	var hours_before: float = clock.get_precise_game_hours()
	_check(get_nodes_in_group("main_scene").is_empty(), "Main remains absent")
	var geometry: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(python_report_path))
	_check(geometry.failures.is_empty() and int(geometry.original_mountains) == 429, "independent original mountain/ground audit passes")
	var fixtures: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(fixtures_path))
	for p: String in geometry.source_sha256: data_hashes[p] = FileAccess.get_sha256(p)
	data_hashes[Registry.ROOT + "free_navigation_compact_peaks_20261109.json"] = FileAccess.get_sha256(Registry.ROOT + "free_navigation_compact_peaks_20261109.json")
	old_terrain = JSON.parse_string(FileAccess.get_file_as_string(Registry.OLD_TERRAIN_PATH))
	root.add_child(npc)
	if not _check(npc.initialize() and nav.initialize(), "real production graph, restored terrain and C# packed navigation initialize"):
		_finish(began)
		return
	_check(nav.astar.get_script().resource_path == "res://csharp/world/PackedWorldNavigation.cs", "actual C# packed production grid is used")
	_registry_restored()
	_npc_all_curves()
	for profile: String in [COMPACT, "expanded3x_20261106_v1"]:
		section = "legal saved geometry " + profile
		var source := Registry.resolve(profile, Scale.MAP_PIXELS)
		source_reports.append({"profile": profile, "world_size": Scale.MAP_PIXELS, "player": _player_source_cases(npc.graph, source)})
	_blocked_cases(Registry.resolve(COMPACT, Scale.MAP_PIXELS), fixtures)
	_compact_manager_batch()
	section = "read only audit"
	for p: String in data_hashes: _check(FileAccess.get_sha256(p) == data_hashes[p], "production source unchanged: " + p)
	_check(float(clock.get_precise_game_hours()) == hours_before, "no already-settled game time is advanced or modified")
	_check(get_nodes_in_group("main_scene").is_empty(), "Main was never launched")
	_finish(began)

func _finish(began: int) -> void:
	var report := {"checks": checks, "failures": failures, "sources": source_reports,
		"directed_npc_curves_checked": npc_edges_checked, "npc_profiles": [COMPACT, "expanded3x_20261106_v1"],
		"max_npc_position_error_world": max_npc_error, "obstruction_cases": obstruction_cases,
		"source_sha256": data_hashes, "elapsed_msec": Time.get_ticks_msec() - began,
		"main_scene_started": false, "gpu_started": false, "production_files_modified": false,
		"scope": "production persistence, real graph, terrain and C# packed navigation; UI drawing suppressed"}
	var file := FileAccess.open(report_path, FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(report, "\t"))
	print("ORIGINAL_GROUNDING_MIGRATION_%s checks=%d failures=%d npc_curves=%d max_error=%.6f" % ["OK" if failures.is_empty() else "FAILED", checks, failures.size(), npc_edges_checked, max_npc_error])
	if is_instance_valid(npc): npc.queue_free()
	quit(0 if failures.is_empty() else 1)
