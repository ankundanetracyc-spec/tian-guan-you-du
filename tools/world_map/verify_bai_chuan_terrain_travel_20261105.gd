extends SceneTree

const Terrain = preload("res://gdscripts/world/BaiChuanTerrainTravel.gd")
var checks := 0
var failures := 0

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		push_error("TERRAIN_COST_CHECK: " + message)

func near(actual: float, expected: float, message: String, tolerance: float = 0.00002) -> void:
	check(absf(actual - expected) <= tolerance, "%s: %.9f / %.9f" % [message, actual, expected])

func _initialize() -> void:
	var expected := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--expected-appdata="): expected = arg.trim_prefix("--expected-appdata=")
	check(not expected.is_empty() and OS.get_environment("APPDATA") == expected, "isolated data required")
	if failures:
		quit(1)
		return
	var terrain := Terrain.new()
	check(terrain.initialize(), "real profile validates")
	var actual_data: Dictionary = Terrain._shared
	var types: Dictionary = actual_data.types.duplicate(true)
	# Analytic stripes establish expected costs without sampling the production field.
	Terrain._shared = {"cell_size": 10.0, "columns": 4, "rows": ["PFHC", "PFHC", "PFHC", "PFHC"], "types": types}
	var start := Vector2(5, 5)
	var end := Vector2(35, 5)
	var expected_cost := 5.0 + 10.0 / 0.8 + 10.0 / 0.6 + 5.0
	near(terrain.segment_cost_length(start, end), expected_cost, "four surfaces exact integral")
	near(terrain.segment_cost_length(end, start), expected_cost, "reversing route preserves cost")
	near(terrain.segment_cost_length(start, Vector2(20, 5)) + terrain.segment_cost_length(Vector2(20, 5), end), expected_cost, "splitting at boundary preserves cost")
	near(terrain.segment_cost_length(Vector2(0, 0), Vector2(40, 40)), sqrt(2.0) * (10 + 10 / 0.8 + 10 / 0.6 + 10), "diagonal simultaneous grid crossings")
	near(terrain.segment_cost_length(Vector2(10, 5), Vector2(10, 35)), 30.0 / 0.8, "path exactly on forest boundary")
	near(terrain.segment_cost_length(start, start), 0, "zero length")
	near(terrain.distance_for_cost(start, end, 17.5), 15, "budget reaches hill boundary")
	near(terrain.distance_for_cost(start, end, 25), 19.5, "budget continues partway through hill")
	near(terrain.distance_for_cost(end, start, expected_cost), 30, "full reverse budget")
	near(terrain.distance_for_cost(start, end, 0), 0, "zero budget")
	near(terrain.distance_for_cost(start, end, -1), 0, "negative budget")
	near(terrain.distance_for_cost(start, end, 100), 30, "large budget clamps to endpoint")
	check(is_inf(terrain.segment_cost_length(Vector2(NAN, 0), end)), "non-finite position rejected")
	check(terrain.terrain_at(Vector2(10, 5)).id == "forest", "boundary assigns entered terrain")
	check(terrain.terrain_at(Vector2(25, 5)).id == "hill", "hill label")
	check(terrain.terrain_at(Vector2(35, 5)).id == "crossing", "crossing label")
	var position := start
	var spent := 0.0
	for budget: float in [0.5, 2.0, 5.0, 0.25, 8.0, 3.0, 1.5]:
		var distance := terrain.distance_for_cost(position, end, budget)
		var next := position.move_toward(end, distance)
		near(terrain.segment_cost_length(position, next), budget, "frame budget conserved", 0.00005)
		spent += budget
		position = next
	near(start.distance_to(position), terrain.distance_for_cost(start, end, spent), "different frame partitions preserve position", 0.00005)
	Terrain._shared = actual_data
	var seen := {}
	for y: int in int(actual_data.columns):
		for x: int in int(actual_data.columns):
			var code: String = str(actual_data.rows[y]).substr(x, 1)
			if code == "_" or seen.has(code): continue
			var center := (Vector2(x, y) + Vector2.ONE * 0.5) * float(actual_data.cell_size)
			var multiplier := float(actual_data.types[code].multiplier)
			var a := center - Vector2(2, 0)
			var b := center + Vector2(2, 0)
			near(terrain.segment_cost_length(a, b), 4.0 / multiplier, "real profile " + code, 0.0005)
			seen[code] = true
		if seen.size() == 4: break
	check(seen.size() == 4, "all four prototype surfaces exist")
	print("TERRAIN_COST_RESULT checks=", checks, " failed=", failures)
	quit(1 if failures else 0)
