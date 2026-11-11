extends SceneTree
## Real-data migration QA. No Main, save files, editor changes or fabricated map
## data. The map subclass suppresses UI/boat drawing only; persistence, routing,
## graph loading, terrain integration and packed navigation remain production.
const Graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd")
const NpcGraph = preload("res://gdscripts/world/BaiChuanNpcNodeGraph.gd")
const Navigation = preload("res://gdscripts/world/BaiChuanFreeNavigation.gd")
const FreeMap = preload("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
const Registry = preload("res://gdscripts/world/WorldMapSourceRegistry.gd")
const Scale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const WorldNpc = preload("res://csharp/world/WorldNpcData.cs")
const OUT := "res://output/map_expansion3x_20261106/real_expanded_migration_report.json"
var checks := 0
var failures: Array[Dictionary] = []
var section := "setup"
var max_npc_error := 0.0
var source_reports: Array[Dictionary] = []
var data_hashes: Dictionary = {}
var old_terrain: Dictionary = {}
var nav = Navigation.new()
var npc = NpcGraph.new()
var rng := RandomNumberGenerator.new()

class PersistenceMap extends FreeMap:
	var requested_driver := ""
	func _ready() -> void: pass
	func _process(_delta: float) -> void: pass
	func _ensure_flight_boat(driver_id: String) -> bool:
		requested_driver = driver_id
		return true
	func _rebuild_hazards() -> void: pass
	func _update_context() -> void: pass
	func _refresh_actions() -> void: pass
	func _update_status(_message := "") -> void: pass
	func _update_route_line() -> void: pass
	func focus_player_node() -> void: pass
	func _update_flight_visual() -> void: pass

func _initialize() -> void:
	call_deferred("_run")

func _check(ok: bool, message: String, detail: Dictionary = {}) -> bool:
	checks += 1
	if ok: return true
	failures.append({"section": section, "message": message, "detail": detail})
	if failures.size() <= 20: push_error("REAL_EXPANDED_MIGRATION_FAILED: %s / %s / %s" % [section, message, JSON.stringify(detail)])
	return false

func _validate_owned_boat(id: String, speed: float) -> bool:
	return id == "qa_owned_spirit_boat" and is_equal_approx(speed, 2400.0)

func _make_map() -> PersistenceMap:
	var map := PersistenceMap.new()
	map.travel.graph = npc.graph
	map.travel.current_node = str(npc.graph.positions.keys()[0])
	map.navigation = nav
	map.terrain_travel = npc.terrain_travel
	root.add_child(map)
	map.add_child(map.world)
	map.world.add_child(map.places)
	map.player_mark = Sprite2D.new()
	map.world.add_child(map.player_mark)
	map.player_halo = map.PlayerPositionHalo.new()
	map.world.add_child(map.player_halo)
	map.configure_player_boat_callbacks(Callable(), Callable(), Callable(), _validate_owned_boat)
	return map

func _base_state(old_graph, point: Vector2, profile: String, size: float) -> Dictionary:
	return {"version": 2, "world_size": size, "world_km": 8000.0,
		"navigation_profile": profile, "terrain_speed_profile": "terrain-speed-v1",
		"current_node": str(old_graph.positions.keys()[0]), "remaining_route": [],
		"world_position": [point.x, point.y], "waypoints": [], "movement_mode": "free",
		"resume_movement": false, "target_npc": "qa_saved_target", "destination_node": "", "visible_encounters": []}

## Independent scalar reference integration over the immutable old terrain.
## Avoids consulting active terrain, current graph km scale, or registry costs.
func _old_cost(a: Vector2, b: Vector2, source_size: float, weighted := true) -> float:
	var length := float(a.distance_to(b))
	if not weighted or length < 0.00000001: return length
	var normalization := float(old_terrain.world_size) / source_size
	var ax := float(a.x) * normalization
	var ay := float(a.y) * normalization
	var bx := float(b.x) * normalization
	var by := float(b.y) * normalization
	var cell_size := float(old_terrain.cell_size)
	var cuts: Array[float] = [0.0, 1.0]
	for axis in range(2):
		var first_value := ax if axis == 0 else ay
		var last_value := bx if axis == 0 else by
		var change := last_value - first_value
		if absf(change) < 0.00000001: continue
		for boundary in range(floori(minf(first_value, last_value) / cell_size) + 1, floori(maxf(first_value, last_value) / cell_size) + 1):
			var amount := (float(boundary) * cell_size - first_value) / change
			if amount > 0.0 and amount < 1.0: cuts.append(amount)
	cuts.sort()
	var cost := 0.0
	for i in range(1, cuts.size()):
		var middle := (cuts[i - 1] + cuts[i]) * 0.5
		var x := floori(lerpf(ax, bx, middle) / cell_size)
		var y := floori(lerpf(ay, by, middle) / cell_size)
		var multiplier := 1.0
		if x >= 0 and y >= 0 and x < int(old_terrain.columns) and y < int(old_terrain.columns):
			var code := str(old_terrain.rows[y]).substr(x, 1)
			multiplier = float(old_terrain.types[code].multiplier)
		cost += length * (cuts[i] - cuts[i - 1]) / multiplier
	return cost

func _old_prefix(points: PackedVector2Array, fraction: float, source_size: float, weighted := true) -> Dictionary:
	var total := 0.0
	for i in range(1, points.size()): total += points[i - 1].distance_to(points[i])
	var remaining := total * fraction
	var cost := 0.0
	for i in range(1, points.size()):
		var span := points[i - 1].distance_to(points[i])
		if remaining <= span:
			var point := points[i - 1].move_toward(points[i], remaining)
			return {"point": point, "end_index": i, "cost": cost + _old_cost(points[i - 1], point, source_size, weighted)}
		cost += _old_cost(points[i - 1], points[i], source_size, weighted)
		remaining -= span
	return {"point": points[-1], "end_index": points.size() - 1, "cost": cost}

func _nearest_on_curve(points: PackedVector2Array, target: Vector2) -> Vector2:
	var nearest := points[0]
	var distance := INF
	for i in range(1, points.size()):
		var candidate := Geometry2D.get_closest_point_to_segment(target, points[i - 1], points[i])
		var squared := candidate.distance_squared_to(target)
		if squared < distance:
			distance = squared
			nearest = candidate
	return nearest

func _find_open_point(source: Dictionary, require_current_open: bool) -> Vector2:
	var columns := int(source.columns)
	var ratio := Scale.MAP_PIXELS / float(source.world_size)
	for y in range(columns / 2, columns - 1):
		for x in range(columns / 2, columns - 1):
			if str(source.rows[y]).substr(x, 1) != "0": continue
			var point := (Vector2(x, y) + Vector2.ONE * 0.5) * float(source.cell_size)
			if not Registry.contains(point, float(source.world_size)): continue
			if nav.is_walkable(point * ratio) == require_current_open: return point
	return Vector2(-1, -1)

func _find_source_blocked(source: Dictionary) -> Vector2:
	for y in range(50, int(source.columns) - 50, 3):
		for x in range(50, int(source.columns) - 50, 3):
			if str(source.rows[y]).substr(x, 1) == "0": continue
			var point := (Vector2(x, y) + Vector2.ONE * 0.5) * float(source.cell_size)
			if Registry.contains(point, float(source.world_size)): return point
	return Vector2(-1, -1)

func _legal_old_edge(old_graph, source: Dictionary) -> Dictionary:
	for key: String in old_graph.edge_points:
		var pair := key.split("~")
		var points: PackedVector2Array = old_graph.points_between(pair[0], pair[1])
		var valid := true
		for i in range(1, points.size()):
			if not Registry.segment_clear(points[i - 1], points[i], source.profile, source.world_size):
				valid = false
				break
		if valid: return {"from": pair[0], "to": pair[1], "points": points}
	return {}

func _check_repeat(map: PersistenceMap, label: String) -> void:
	var saved := map.get_node_state()
	var point := map.player_mark.position
	var height := map.flight_height
	var mode := map.movement_mode
	var checkpoints: Array[String] = []
	for row: Dictionary in map.waypoints: checkpoints.append(str(row.node))
	_check(map.restore_node_state(saved), "repeat current restore: " + label)
	_check(map.player_mark.position == point and map.flight_height == height and map.movement_mode == mode, "repeat preserves coordinate/altitude/mode: " + label)
	var after: Array[String] = []
	for row: Dictionary in map.waypoints: after.append(str(row.node))
	_check(checkpoints == after, "repeat preserves checkpoints: " + label)

func _npc_source_cases(old_graph, source: Dictionary) -> Dictionary:
	var initial := checks
	var size := float(source.world_size)
	var profile := str(source.profile)
	var keys: Array = old_graph.edge_points.keys()
	var max_points := 0
	for points: PackedVector2Array in npc.graph.edge_points.values(): max_points = maxi(max_points, points.size())
	for sample in range(26):
		var key: String = keys[rng.randi_range(0, keys.size() - 1)]
		for reverse in [false, true]:
			var pair := key.split("~")
			if reverse: pair.reverse()
			var from: Vector2i = npc.node_id_to_cell(pair[0])
			var to: Vector2i = npc.node_id_to_cell(pair[1])
			var points: PackedVector2Array = old_graph.points_between(pair[0], pair[1])
			var current: PackedVector2Array = npc.graph.points_between(pair[0], pair[1])
			for fraction: float in [0.02, 0.37, 0.81, rng.randf_range(0.001, 0.999)]:
				for weighted in [false, true]:
					var prefix := _old_prefix(points, fraction, size, weighted)
					var old_hours := float(prefix.cost) * (8000.0 / size) * 60.0 / 100.0
					var terrain := "terrain-speed-v1" if weighted else ""
					var converted: float = npc.upgrade_navigation_travel_progress(from, to, old_hours, 100.0, profile, size, terrain, 2)
					if not _check(is_finite(converted) and converted >= 0.0, "NPC old source converts", {"edge": key, "reverse": reverse, "weighted": weighted}): continue
					var actual: Vector2 = npc.project_map_position(from, [from, to], 1, converted, 0.0, 100.0)
					var expected := _nearest_on_curve(current, (prefix.point as Vector2) * (Scale.MAP_PIXELS / size))
					var error := actual.distance_to(expected)
					max_npc_error = maxf(max_npc_error, error)
					_check(error <= 0.05, "NPC old weighted point -> scaled -> nearest real current curve", {"edge": key, "reverse": reverse, "weighted": weighted, "error": error})
					_check(nav.is_walkable(actual), "converted NPC is on current legal ground", {"edge": key, "point": [actual.x, actual.y]})
					_check(npc.upgrade_navigation_travel_progress(from, to, converted, 100.0, Navigation.PROFILE_ID, Scale.MAP_PIXELS, "terrain-speed-v1", 2) == converted, "current NPC progress is idempotent")
					_check(npc.cell_to_node_id(from) == pair[0] and npc.cell_to_node_id(to) == pair[1], "opaque NPC indices stay unchanged")
	return {"checks": checks - initial, "max_current_curve_points": max_points}

func _player_source_cases(old_graph, source: Dictionary) -> Dictionary:
	var initial := checks
	var map := _make_map()
	var size := float(source.world_size)
	var profile := str(source.profile)
	var ratio := Scale.MAP_PIXELS / size
	var open := _find_open_point(source, true)
	if not _check(open.x >= 0.0, "real old/current common ground fixture exists"):
		map.free()
		return {"checks": checks - initial}
	var stationary := _base_state(old_graph, open, profile, size)
	stationary.visible_encounters = [{"id": "qa_old_hazard", "kind": "wildlife", "position": [open.x, open.y], "seed": 7301}]
	_check(map.restore_node_state(stationary), "stationary old save restores")
	_check(map.player_mark.position.distance_to(open * ratio) <= 0.01 and not map._resume_requested, "stationary world position scales once and stays stopped")
	_check(map.hazards.size() == 1 and map.hazards[0].id == "qa_old_hazard" and map.hazards[0].seed == 7301 and map.hazards[0].position.distance_to(open * ratio) <= 0.01, "saved encounter identity/payload/position survives")
	_check_repeat(map, "stationary encounter")
	var old_blocked_now := _find_open_point(source, false)
	if old_blocked_now.x >= 0:
		var expected := nav.nearest_walkable(old_blocked_now * ratio)
		var moved := _base_state(old_graph, old_blocked_now, profile, size)
		_check(map.restore_node_state(moved), "old legal point obstructed by new landscape migrates")
		_check(map.player_mark.position.distance_to(expected) <= 0.01 and nav.is_walkable(map.player_mark.position), "obstructed old point uses nearest real legal current ground")
	var edge := _legal_old_edge(old_graph, source)
	if _check(not edge.is_empty(), "real legal old route fixture exists"):
		var points: PackedVector2Array = edge.points
		var prefix := _old_prefix(points, 0.37, size)
		var moving := _base_state(old_graph, prefix.point, profile, size)
		moving.current_node = edge.from
		moving.destination_node = edge.to
		moving.resume_movement = true
		moving.waypoints = []
		for i in range(int(prefix.end_index), points.size()): moving.waypoints.append([points[i].x, points[i].y, ""])
		_check(map.restore_node_state(moving), "old free trip replans through real current navigation", {"from": edge.from, "to": edge.to})
		_check(map._resume_requested and not map.waypoints.is_empty() and map.waypoints[-1].position.distance_to(npc.graph.positions[edge.to]) <= 0.01, "moving trip retains resume flag and real destination")
		_check_repeat(map, "moving free trip")
		var assigned := _base_state(old_graph, points[0], profile, size)
		assigned.current_node = edge.from
		assigned.remaining_route = [edge.from, edge.to]
		assigned.destination_node = edge.to
		assigned.movement_mode = "assigned"
		assigned.waypoints = []
		for i in range(1, points.size()): assigned.waypoints.append([points[i].x, points[i].y, edge.to if i == points.size() - 1 else ""])
		_check(map.restore_node_state(assigned), "old paused escort route replans")
		_check(map.movement_mode == "assigned" and map.travel.route == [edge.from, edge.to] and map.waypoints[-1].node == edge.to and not map._resume_requested, "escort retains opaque route/checkpoint IDs and stopped flag")
		_check_repeat(map, "paused escort")
		var snapshot := JSON.stringify(map.get_node_state())
		var invalid := assigned.duplicate(true)
		invalid.waypoints[-1][0] += 20.0
		_check(not map.restore_node_state(invalid) and JSON.stringify(map.get_node_state()) == snapshot, "invalid old checkpoint cannot mutate current state")
	var blocked := _find_source_blocked(source)
	if _check(blocked.x >= 0, "airborne fixture is actually over old blocked ground"):
		var destination: String = old_graph.positions.keys()[1]
		var landing: Vector2 = old_graph.positions[destination]
		var flight := _base_state(old_graph, blocked, profile, size)
		flight.movement_mode = "flight"
		flight.destination_node = destination
		flight.waypoints = [[landing.x, landing.y, ""]]
		flight.flight_speed = 2400.0
		flight.flight_height = 73.25
		flight.flight_driver_id = "qa_real_saved_yuxu_driver"
		flight.resume_movement = true
		_check(map.restore_node_state(flight), "scripted reception flight restores over blocked ground")
		_check(map.player_mark.position.distance_to(blocked * ratio) <= 0.01 and map.flight_height == 73.25 and map.flight_speed == 2400.0 and map.requested_driver == "qa_real_saved_yuxu_driver", "flight scales XY once, preserves altitude/speed/real driver")
		_check(map.waypoints.size() == 1 and map.waypoints[0].position.distance_to(npc.graph.positions[destination]) <= 0.01 and map._resume_requested, "scripted flight keeps its current reception destination")
		_check_repeat(map, "reception flight")
		for travelling in [false, true]:
			var owned := _base_state(old_graph, blocked, profile, size)
			owned.movement_mode = "player_flight"
			owned.flight_speed = 2400.0
			owned.flight_height = 94.5
			owned.flight_driver_id = ""
			owned.player_boat_id = "qa_owned_spirit_boat"
			owned.waypoints = [[open.x, open.y, ""]] if travelling else []
			owned.resume_movement = travelling
			_check(map.restore_node_state(owned), "owned player flight restores with validated owned artifact")
			_check(map.movement_mode == "player_flight" and map.player_boat_id == "qa_owned_spirit_boat" and map.flight_height == 94.5 and map.player_mark.position.distance_to(blocked * ratio) <= 0.01 and map._resume_requested == travelling, "owned flight preserves boat/altitude/hover-or-travel state")
			_check_repeat(map, "owned flight")
			var snapshot := JSON.stringify(map.get_node_state())
			var denied := owned.duplicate(true)
			denied.player_boat_id = "unowned_artifact"
			_check(not map.restore_node_state(denied) and JSON.stringify(map.get_node_state()) == snapshot, "owned flight rejects unowned artifact without state changes")
	var snapshot := JSON.stringify(map.get_node_state())
	for field: String in ["navigation_profile", "world_size", "world_position"]:
		var corrupt := stationary.duplicate(true)
		if field == "navigation_profile": corrupt[field] = "qa_future_navigation"
		elif field == "world_size": corrupt[field] = 9000.0
		else: corrupt[field] = [size + 1.0, open.y]
		_check(not map.restore_node_state(corrupt) and JSON.stringify(map.get_node_state()) == snapshot, "unknown source/corrupt coordinate rejected atomically")
	map.free()
	return {"checks": checks - initial, "route": edge.get("from", "") + ">" + edge.get("to", ""), "old_obstructed_now_found": old_blocked_now.x >= 0}

func _legacy_cases() -> void:
	section = "v1 legacy node save"
	var legacy := Graph.new()
	_check(legacy.load_layout(), "real legacy node layout loads")
	_check(absf(legacy.km_per_pixel - 200.0 / 3762.0) < 0.00000001, "legacy NPC uses 200km ruler")
	var key: String = legacy.edge_points.keys()[0]
	var pair := key.split("~")
	for reverse in [false, true]:
		if reverse: pair.reverse()
		var points: PackedVector2Array = legacy.points_between(pair[0], pair[1])
		var from: Vector2i = npc.node_id_to_cell(pair[0])
		var to: Vector2i = npc.node_id_to_cell(pair[1])
		var prefix := _old_prefix(points, 0.37, 3762.0, false)
		var old_hours := float(prefix.cost) * (200.0 / 3762.0) * 60.0 / 100.0
		var converted: float = npc.upgrade_navigation_travel_progress(from, to, old_hours, 100.0, "", 0.0, "", 1)
		var actual: Vector2 = npc.project_map_position(from, [from, to], 1, converted, 0.0, 100.0)
		var expected := _nearest_on_curve(npc.graph.points_between(pair[0], pair[1]), (prefix.point as Vector2) * (Scale.MAP_PIXELS / 3762.0))
		_check(converted >= 0.0 and actual.distance_to(expected) <= 0.05, "v1 uniform NPC physical point migrates to real new curve")
	var map := _make_map()
	var saved := {"current_node": pair[0], "remaining_route": [pair[0], pair[1]], "legacy_assigned_route": true, "resume_movement": false}
	_check(map.restore_node_state(saved), "real v1 node escort restores")
	_check(map.movement_mode == "assigned" and not map._resume_requested and map.travel.route == [pair[0], pair[1]] and map.waypoints[-1].node == pair[1], "v1 escort keeps opaque checkpoint IDs")
	_check_repeat(map, "v1 escort upgraded current save")
	saved.legacy_assigned_route = false
	saved.resume_movement = true
	_check(map.restore_node_state(saved) and map.movement_mode == "free" and map.travel.route.is_empty() and map._resume_requested, "ordinary v1 node trip becomes editable free trip")
	_check_repeat(map, "v1 ordinary upgraded current save")
	map.free()

func _world_npc(id: String, from: Vector2i, to: Vector2i, hours: float):
	var person = WorldNpc.new()
	person.npc_id = id
	person.npc_name = id
	person.is_traveling = true
	person.has_world_grid_position = true
	person.current_grid_cell = from
	person.world_travel_path.append(from)
	person.world_travel_path.append(to)
	person.world_travel_next_cell_index = 1
	person.world_travel_cell_progress_hours = hours
	person.travel_days_remaining = 77
	person.dun_speed = 100.0
	return person

func _actual_batch_cases() -> void:
	section = "actual C# manager batch"
	var manager := root.get_node("NpcWorldManager")
	manager.set_tile_world_map(npc)
	manager.reset_npc_navigation_save_profile()
	var residents: Array = manager.get("npcs")
	residents.clear()
	var old_graph := Graph.new()
	old_graph.use_free_world = true
	_check(old_graph.load_layout("res://resources/world/bai_chuan/free_layout_3x3_20261104.json"), "batch old graph loads")
	var pair := str(old_graph.edge_points.keys()[0]).split("~")
	var from: Vector2i = npc.node_id_to_cell(pair[0])
	var to: Vector2i = npc.node_id_to_cell(pair[1])
	var prefix := _old_prefix(old_graph.points_between(pair[0], pair[1]), 0.37, 7372.8)
	var hours := float(prefix.cost) * (8000.0 / 7372.8) * 60.0 / 100.0
	var a = _world_npc("qa_real_batch_a", from, to, hours)
	var b = _world_npc("qa_real_batch_b", from, Vector2i(999, 999), hours)
	residents.append(a)
	residents.append(b)
	var saved_hours: float = a.world_travel_cell_progress_hours
	var saved_path: Array = a.world_travel_path.duplicate()
	_check(not manager.migrate_navigation_travel("", 7372.8, "terrain-speed-v1", 2), "late invalid opaque index rejects entire actual batch")
	_check(a.world_travel_cell_progress_hours == saved_hours and b.world_travel_cell_progress_hours == saved_hours and a.travel_days_remaining == 77 and a.world_travel_path == saved_path and a.current_grid_cell == from, "batch failure preserves earlier NPC progress, days, and opaque indices")
	_check(manager.get_npc_navigation_save_profile() == "" and manager.get_npc_navigation_save_world_size() == 7372.8, "failed batch preserves explicit empty source")
	var map := _make_map()
	var state := map.get_node_state()
	_check(state.npc_navigation_profile == "" and state.npc_world_size == 7372.8 and state.npc_terrain_speed_profile == "terrain-speed-v1", "real current player save cannot falsely stamp failed NPC migration")
	b.world_travel_path[1] = to
	_check(manager.migrate_navigation_travel("", 7372.8, "terrain-speed-v1", 2), "real valid batch migrates after fixing fixture")
	var converted: float = a.world_travel_cell_progress_hours
	_check(manager.get_npc_navigation_save_profile() == Navigation.PROFILE_ID and a.world_travel_path == saved_path and a.current_grid_cell == from and a.world_travel_next_cell_index == 1, "successful batch stamps current source and leaves opaque indices unchanged")
	_check(manager.migrate_navigation_travel(Navigation.PROFILE_ID, Scale.MAP_PIXELS, "terrain-speed-v1", 2) and a.world_travel_cell_progress_hours == converted, "actual manager current reload is idempotent")
	_check(not manager.migrate_navigation_travel("unknown_future", 7372.8, "terrain-speed-v1", 2) and a.world_travel_cell_progress_hours == converted and manager.get_npc_navigation_save_profile() == "unknown_future", "unknown source keeps actual NPC progress and exact failed source")
	residents.clear()
	_check(not manager.migrate_navigation_travel("unknown_future", 7372.8, "terrain-speed-v1", 2), "unknown source rejected even with empty population")
	map.free()

func _run() -> void:
	var began := Time.get_ticks_msec()
	rng.seed = 2026110603
	_check(get_nodes_in_group("main_scene").is_empty(), "Main is never instantiated")
	var clock := root.get_node_or_null("GameTimeManager")
	var hours_before: float = clock.call("get_precise_game_hours") if clock != null else 0.0
	old_terrain = JSON.parse_string(FileAccess.get_file_as_string(Registry.OLD_TERRAIN_PATH))
	for path: String in [Scale.LAYOUT_PATH, Scale.NAVIGATION_PATH, Scale.TERRAIN_PATH, Scale.LANDSCAPE_PATH,
		Registry.ROOT + "free_navigation_mountains_20261106.json", Registry.ROOT + "free_layout_mountains_20261106.json",
		Registry.ROOT + "free_navigation_3x3_20261104.json", Registry.ROOT + "free_layout_3x3_20261104.json",
		Registry.ROOT + "free_navigation_20261104.json", Registry.ROOT + "free_layout_20261104.json", Registry.OLD_TERRAIN_PATH]:
		data_hashes[path] = FileAccess.get_sha256(path)
	root.add_child(npc)
	if not _check(npc.initialize() and nav.initialize(), "production real expanded graph/navigation/terrain initialize"):
		_finish(began)
		return
	_check(Scale.MAP_PIXELS == 22118.4 and nav.columns == 1413 and npc.get_navigation_profile() == "expanded3x_20261106_v1", "actual active expanded scale/profile")
	var landscape: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(Scale.LANDSCAPE_PATH))
	_check(landscape.mountains.size() == 429 and landscape.trees.size() == 5509 and landscape.bridges.size() == 9, "final real scenery data is the reviewed final build")
	_check(npc.graph.edge_points.size() == 575, "all 575 actual current links remain")
	for config: Array in [["complete_mountains_20261106_v1", 7372.8], ["", 7372.8], ["", 12288.0]]:
		var profile: String = config[0]
		var size: float = config[1]
		section = "%s / %s" % [profile, size]
		print("REAL_EXPANDED_MIGRATION_SOURCE ", section)
		var source := Registry.resolve(profile, size)
		if not _check(not source.is_empty(), "real historical registered source loads"): continue
		_check(int(source.columns) == 471 and absf(float(source.cell_size) - 8.0 * size / 3762.0) < 0.000001, "historical grid shape/cell size remain source-owned")
		var old_graph := Graph.new()
		old_graph.use_free_world = true
		if not _check(old_graph.load_layout(source.layout_path), "real historical source graph loads"): continue
		_check(absf(old_graph.km_per_pixel - 8000.0 / size) < 0.000000001, "historical parsed worldSize determines old km ruler")
		var source_report := {"profile": profile, "world_size": size}
		source_report.npc = _npc_source_cases(old_graph, source)
		source_report.player = _player_source_cases(old_graph, source)
		source_reports.append(source_report)
	_legacy_cases()
	_actual_batch_cases()
	section = "read-only audit"
	for path: String in data_hashes:
		_check(FileAccess.get_sha256(path) == data_hashes[path], "production/old source data bytes unchanged: " + path)
	_check(clock == null or float(clock.call("get_precise_game_hours")) == hours_before, "migration does not settle or advance game time")
	_check(get_nodes_in_group("main_scene").is_empty(), "Main stayed absent throughout QA")
	_finish(began)

func _finish(began: int) -> void:
	var report := {"kind": "real production expanded and immutable historical data",
		"checks": checks, "failures": failures, "max_npc_position_error_world": max_npc_error,
		"elapsed_msec": Time.get_ticks_msec() - began, "sources": source_reports,
		"source_sha256": data_hashes, "main_scene_launched": false,
		"map_test_scope": "production persistence + real graph/nav/terrain; UI and boat drawing suppressed"}
	var file := FileAccess.open(OUT, FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(report, "\t"))
	print("REAL_EXPANDED_MIGRATION_%s: %d checks, %d failures, max NPC error %.6f world units" % ["OK" if failures.is_empty() else "FAILED", checks, failures.size(), max_npc_error])
	var manager := root.get_node_or_null("NpcWorldManager")
	if manager != null:
		var residents: Array = manager.get("npcs")
		residents.clear()
	if is_instance_valid(npc): npc.queue_free()
	quit(0 if failures.is_empty() else 1)
