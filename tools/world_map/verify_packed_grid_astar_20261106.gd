extends SceneTree
## Run from a minimal isolated project; this probe does not load game autoloads.
const PackedGrid = preload("res://gdscripts/world/PackedGridAStar.gd")
const SQRT_TWO := 1.4142135623730951
var checks := 0
var failures := 0
var samples: Array[Dictionary] = []
var report_path := "user://packed_grid_astar_report.json"

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report="): report_path = argument.trim_prefix("--report=")
		if argument.begins_with("--expected-appdata="):
			_check(OS.get_environment("APPDATA") == argument.trim_prefix("--expected-appdata="), "isolated APPDATA matches runner")
	call_deferred("_run")

func _run() -> void:
	_verify_inputs_and_ownership()
	_verify_corner_cases()
	_verify_random_grids()
	_benchmark_large_grid()
	var result := {
		"checks": checks, "failures": failures, "engine": Engine.get_version_info(),
		"random_seed": 20261106, "random_maps": 80, "random_queries": 960,
		"benchmarks": samples, "user_data_dir": OS.get_user_data_dir(),
		"scope": "Standalone packed-grid search only; no game scenes, autoloads or save data loaded.",
	}
	var directory := report_path.get_base_dir()
	if not directory.is_empty(): DirAccess.make_dir_recursive_absolute(directory)
	var file := FileAccess.open(report_path, FileAccess.WRITE)
	if file == null:
		_check(false, "report file opens " + report_path)
	else:
		file.store_string(JSON.stringify(result, "\t") + "\n")
		file.close()
	print("PACKED_GRID_CHECKS ", checks, " FAILURES ", failures)
	print("PACKED_GRID_REPORT ", report_path)
	quit(0 if failures == 0 else 1)

func _verify_inputs_and_ownership() -> void:
	var grid := PackedGrid.new()
	_check(grid.get_point_path(Vector2i.ZERO, Vector2i.ONE).is_empty(), "unconfigured search is empty")
	_check(grid.is_point_solid(Vector2i.ZERO), "unconfigured cells are solid")
	_check(not grid.configure(0, 1.0, [], 1.0), "zero columns rejected")
	_check(not grid.configure(2, 0.0, ["00", "00"], 2.0), "zero cell size rejected")
	_check(not grid.configure(2, NAN, ["00", "00"], 2.0), "NaN cell size rejected")
	_check(not grid.configure(2, 1.0, ["00", "00"], INF), "infinite world size rejected")
	_check(not grid.configure(2, 1.0, ["00", "00"], 3.0), "world beyond grid rejected")
	_check(not grid.configure(2, 1.0, ["00"], 2.0), "wrong row count rejected")
	_check(not grid.configure(2, 1.0, ["00", "0"], 2.0), "wrong row width rejected")
	_check(not grid.configure(2, 1.0, ["00", 0], 2.0), "non-string row rejected")
	_check(not grid.configure(2, 1.0, ["00", "0x"], 2.0), "unknown terrain rejected")
	_check(not grid.configure(2, 1.0, ["00", "０0"], 2.0), "non-ASCII terrain rejected")
	var caller_rows: Array = ["00", "00"]
	_check(grid.configure(2, 1.0, caller_rows, 1.9), "padded grid extent accepted")
	caller_rows[0] = "11"
	_check(not grid.is_point_solid(Vector2i.ZERO), "caller row mutation does not change configured occupancy")
	_check(grid.is_point_solid(Vector2i(-1, 0)) and grid.is_point_solid(Vector2i(2, 0)), "outside cells are solid")
	_check(not grid.is_in_boundsv(Vector2i(-1, 0)) and grid.is_in_boundsv(Vector2i.ONE), "bounds exclude negative and accept last cell")
	var second := PackedGrid.new()
	_check(second.configure(2, 1.0, ["01", "10"], 2.0), "second independent grid configures")
	_check(second.get_point_path(Vector2i.ZERO, Vector2i.ONE).is_empty(), "second instance has blocked corner")
	_check(grid.get_point_path(Vector2i.ZERO, Vector2i.ONE).size() == 2, "first instance retains its own free diagonal")
	grid._generation = PackedGrid.MAX_GENERATION
	_check(grid.get_point_path(Vector2i.ONE, Vector2i.ZERO).size() == 2 and grid._generation == 1, "epoch wrap resets stamps safely")
	_check(not grid.configure(2, 1.0, ["00", "03"], 2.0), "invalid reconfiguration rejected")
	_check(grid.get_point_path(Vector2i.ZERO, Vector2i.ONE).is_empty(), "failed reconfiguration cannot use stale grid")
	_check(grid.configure(2, 1.0, ["00", "00"], 2.0), "grid can configure after previous failure")

func _verify_corner_cases() -> void:
	var examples: Array = [
		{"rows": ["0"], "from": Vector2i.ZERO, "to": Vector2i.ZERO},
		{"rows": ["01", "10"], "from": Vector2i.ZERO, "to": Vector2i.ONE},
		{"rows": ["00", "10"], "from": Vector2i.ZERO, "to": Vector2i.ONE},
		{"rows": ["00", "02"], "from": Vector2i.ZERO, "to": Vector2i.ONE},
		{"rows": ["00000", "01110", "00010", "01010", "00000"], "from": Vector2i.ZERO, "to": Vector2i(4, 4)},
		{"rows": ["00000", "22222", "00000", "00000", "00000"], "from": Vector2i.ZERO, "to": Vector2i(4, 4)},
	]
	for index: int in examples.size():
		var example: Dictionary = examples[index]
		var grid := PackedGrid.new()
		_check(grid.configure(example.rows.size(), 3.5, example.rows, example.rows.size() * 3.5), "manual grid configures " + str(index))
		var reference := _native_grid(example.rows, 3.5)
		_compare(grid, reference, example.from, example.to, "manual " + str(index))
		_compare(grid, reference, example.to, example.from, "manual reverse " + str(index))

func _verify_random_grids() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 20261106
	var grid := PackedGrid.new()
	for sample: int in 80:
		var n := rng.randi_range(2, 36)
		var probability := rng.randf_range(0.08, 0.45)
		var rows: Array = []
		for y: int in n:
			var bytes := PackedByteArray()
			bytes.resize(n)
			for x: int in n: bytes[x] = rng.randi_range(49, 50) if rng.randf() < probability else 48
			rows.append(bytes.get_string_from_ascii())
		_check(grid.configure(n, 3.5, rows, n * 3.5), "random grid configures " + str(sample))
		var reference := _native_grid(rows, 3.5)
		for query: int in 12:
			var from := Vector2i(rng.randi_range(0, n - 1), rng.randi_range(0, n - 1))
			var to := Vector2i(rng.randi_range(0, n - 1), rng.randi_range(0, n - 1))
			_compare(grid, reference, from, to, "random %d query %d" % [sample, query])
	print("PACKED_GRID_RANDOM_COMPLETE maps=80 queries=960 checks=", checks, " failures=", failures)

func _native_grid(rows: Array, step: float) -> AStarGrid2D:
	var reference := AStarGrid2D.new()
	reference.region = Rect2i(0, 0, rows.size(), rows.size())
	reference.cell_size = Vector2.ONE * step
	reference.offset = Vector2.ONE * step * 0.5
	reference.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES
	reference.default_compute_heuristic = AStarGrid2D.HEURISTIC_OCTILE
	reference.default_estimate_heuristic = AStarGrid2D.HEURISTIC_OCTILE
	reference.update()
	for y: int in rows.size():
		for x: int in rows.size():
			if str(rows[y])[x] != "0": reference.set_point_solid(Vector2i(x, y))
	return reference

func _compare(grid: RefCounted, reference: AStarGrid2D, from: Vector2i, to: Vector2i, label: String) -> void:
	var actual: PackedVector2Array = grid.get_point_path(from, to)
	var expected := PackedVector2Array()
	if not reference.is_point_solid(from) and not reference.is_point_solid(to): expected = reference.get_point_path(from, to)
	_check(actual.is_empty() == expected.is_empty(), "native reachability " + label)
	_check(_valid_path(grid, actual, from, to), "legal centres and no corner cuts " + label)
	_check(absf(_path_cost(actual, grid.cell_size) - _path_cost(expected, grid.cell_size)) < 0.00000001, "native shortest cost " + label)
	_check(grid.last_expanded_cells <= grid.columns * grid.columns, "each cell expands at most once " + label)

func _valid_path(grid: RefCounted, points: PackedVector2Array, from: Vector2i, to: Vector2i) -> bool:
	if points.is_empty(): return true
	var start := Vector2i((points[0] / grid.cell_size).floor())
	var finish := Vector2i((points[-1] / grid.cell_size).floor())
	if start != from or finish != to: return false
	var previous := start
	for i: int in points.size():
		var cell := Vector2i((points[i] / grid.cell_size).floor())
		if grid.is_point_solid(cell): return false
		if points[i].distance_to((Vector2(cell) + Vector2.ONE * 0.5) * grid.cell_size) > 0.0001: return false
		if i > 0:
			var direction := cell - previous
			if direction == Vector2i.ZERO or absi(direction.x) > 1 or absi(direction.y) > 1: return false
			if direction.x != 0 and direction.y != 0:
				if grid.is_point_solid(Vector2i(previous.x, cell.y)) or grid.is_point_solid(Vector2i(cell.x, previous.y)): return false
		previous = cell
	return true

func _path_cost(points: PackedVector2Array, step: float) -> float:
	var result := 0.0
	for i: int in range(1, points.size()):
		var delta := Vector2i(((points[i] - points[i - 1]) / step).round())
		result += SQRT_TWO if delta.x != 0 and delta.y != 0 else 1.0
	return result

func _benchmark_large_grid() -> void:
	var n := 1413
	var step := 15.678468899521532
	var rows: Array = []
	for y: int in n: rows.append("0".repeat(n))
	var before := OS.get_static_memory_usage()
	var grid := PackedGrid.new()
	var began := Time.get_ticks_usec()
	_check(grid.configure(n, step, rows, 22118.4), "1413 by 1413 grid configures")
	var construction := {
		"name": "configure_open_1413", "columns": n, "cells": n * n,
		"milliseconds": float(Time.get_ticks_usec() - began) / 1000.0,
		"packed_storage_bytes": grid.get_storage_bytes(),
		"godot_static_memory_delta_bytes": OS.get_static_memory_usage() - before,
	}
	samples.append(construction)
	print("PACKED_GRID_BENCHMARK ", JSON.stringify(construction))
	_measure_path(grid, Vector2i(20, 20), Vector2i(80, 55), "near_open", 5)
	_measure_path(grid, Vector2i(10, 10), Vector2i(1402, 1402), "far_open_diagonal", 3)
	_measure_path(grid, Vector2i(1402, 1300), Vector2i(10, 10), "far_open_reverse", 3)
	var barrier := "0".repeat(706) + "1" + "0".repeat(n - 707)
	for y: int in n: rows[y] = "0".repeat(n) if y >= 900 and y <= 908 else barrier
	_check(grid.configure(n, step, rows, 22118.4), "1413 detour grid configures")
	_measure_path(grid, Vector2i(120, 120), Vector2i(1290, 120), "far_wall_detour", 1)
	_check(grid.get_storage_bytes() < 48 * 1024 * 1024, "packed persistent arrays and heap remain below 48 MiB in measured cases")

func _measure_path(grid: RefCounted, from: Vector2i, to: Vector2i, name: String, repeats: int) -> void:
	var times: Array[float] = []
	var count := 0
	var cost := 0.0
	var expanded := 0
	var heap_peak := 0
	for iteration: int in repeats:
		var began := Time.get_ticks_usec()
		var path: PackedVector2Array = grid.get_point_path(from, to)
		times.append(float(Time.get_ticks_usec() - began) / 1000.0)
		_check(not path.is_empty() and _valid_path(grid, path, from, to), "large legal path " + name + " " + str(iteration))
		count = path.size()
		cost = _path_cost(path, grid.cell_size)
		expanded = grid.last_expanded_cells
		heap_peak = grid.last_heap_peak
	times.sort()
	var sample := {
		"name": name, "repeats": repeats, "min_ms": times[0], "median_ms": times[times.size() / 2], "max_ms": times[-1],
		"path_cells": count, "cost_in_cells": cost, "expanded_cells": expanded, "heap_peak_entries": heap_peak,
		"packed_storage_bytes": grid.get_storage_bytes(),
	}
	samples.append(sample)
	print("PACKED_GRID_BENCHMARK ", JSON.stringify(sample))

func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		push_error("PACKED_GRID_FAIL " + label)
