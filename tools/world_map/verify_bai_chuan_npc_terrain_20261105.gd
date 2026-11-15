extends SceneTree

const Adapter = preload("res://gdscripts/world/BaiChuanNpcNodeGraph.gd")
const Scale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const REPORT := "res://output/bai_chuan_terrain_travel_20261105/npc_adapter_report.json"
var checks := 0
var failures := 0

class CountingTerrain extends RefCounted:
	var source
	var ready := true
	var integrations := 0
	func segment_cost_length(from: Vector2, to: Vector2) -> float:
		integrations += 1
		return source.segment_cost_length(from, to)
	func distance_for_cost(from: Vector2, to: Vector2, budget: float) -> float:
		return source.distance_for_cost(from, to, budget)

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		push_error("NPC_TERRAIN_CHECK: " + message)

func position_at_length(points: PackedVector2Array, distance: float) -> Vector2:
	for i in range(1, points.size()):
		var length := points[i - 1].distance_to(points[i])
		if distance <= length: return points[i - 1].move_toward(points[i], distance)
		distance -= length
	return points[-1]

func _initialize() -> void:
	var expected := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--expected-appdata="): expected = arg.trim_prefix("--expected-appdata=")
	check(not expected.is_empty() and OS.get_environment("APPDATA") == expected, "isolated data required")
	if failures:
		quit(1)
		return
	var adapter := Adapter.new()
	check(adapter.initialize(), "production NPC graph and terrain initialize")
	if failures:
		adapter.free()
		quit(1)
		return
	var terrain = adapter.terrain_travel
	var graph = adapter.graph
	var max_inverse_error := 0.0
	var max_cost_error := 0.0
	var max_reverse_error := 0.0
	var max_migration_error := 0.0
	var max_projection_speed_error := 0.0
	var min_delay_ratio := INF
	var max_delay_ratio := 0.0
	var directed_edges := 0
	var local_surfaces := {}
	var sample_from := Vector2i(-1, -1)
	var sample_to := Vector2i(-1, -1)
	var legacy_samples := []
	for from_id: String in graph.adjacency:
		for link: Dictionary in graph.adjacency[from_id]:
			var to_id: String = link.to
			var from: Vector2i = adapter.node_id_to_cell(from_id)
			var to: Vector2i = adapter.node_id_to_cell(to_id)
			var path: Array = [from, to]
			var pair: Array[String] = [from_id, to_id]
			var points: PackedVector2Array = graph.points_between(from_id, to_id)
			var uniform_hours: float = graph.hours_for_km(graph.path_km(pair), 100.0)
			var hours: float = adapter.get_npc_edge_travel_hours(from, to, 100.0)
			var fast_hours: float = adapter.get_npc_edge_travel_hours(from, to, 200.0)
			max_inverse_error = maxf(max_inverse_error, absf(hours / fast_hours - 2.0))
			var direct_cost: float = terrain.path_cost_length(points)
			max_cost_error = maxf(max_cost_error, absf(hours - graph.hours_for_km(direct_cost * graph.km_per_pixel, 100.0)))
			max_reverse_error = maxf(max_reverse_error, absf(hours - adapter.get_npc_edge_travel_hours(to, from, 100.0)))
			min_delay_ratio = minf(min_delay_ratio, hours / uniform_hours)
			max_delay_ratio = maxf(max_delay_ratio, hours / uniform_hours)
			var length := 0.0
			for i in range(1, points.size()): length += points[i - 1].distance_to(points[i])
			var migrated: float = adapter.upgrade_uniform_travel_progress(from, to, uniform_hours * 0.37, 100.0)
			var projected: Vector2 = adapter.project_map_position(from, path, 1, migrated, 0.0, 100.0)
			max_migration_error = maxf(max_migration_error, projected.distance_to(position_at_length(points, length * 0.37)))
			var faster: Vector2 = adapter.project_map_position(from, path, 1, migrated * 0.5, 0.0, 200.0)
			max_projection_speed_error = maxf(max_projection_speed_error, projected.distance_to(faster))
			if legacy_samples.size() < 4:
				legacy_samples.append({"from": from_id, "to": to_id, "scale": adapter.get_legacy_progress_scale(from, to, 100.0)})
			# Check positions on real, individual grid spans, so edge averaging cannot pass.
			var prefix := 0.0
			for i in range(1, points.size()):
				var a := points[i - 1]
				var b := points[i]
				var cuts: PackedFloat64Array = terrain._cuts(a, b)
				for j in range(1, cuts.size()):
					var p := a.lerp(b, float(cuts[j - 1]))
					var q := a.lerp(b, float(cuts[j]))
					var surface: Dictionary = terrain.terrain_at(p.lerp(q, 0.5))
					if local_surfaces.has(surface.id) or p.distance_to(q) < 2.0: continue
					var start := p.lerp(q, 0.25)
					var end := p.lerp(q, 0.75)
					var start_hours: float = graph.hours_for_km((prefix + terrain.segment_cost_length(a, start)) * graph.km_per_pixel, 100.0)
					var delta_hours: float = graph.hours_for_km(start.distance_to(end) / float(surface.multiplier) * graph.km_per_pixel, 100.0)
					var actual_start: Vector2 = adapter.project_map_position(from, path, 1, start_hours, 0.0, 100.0)
					var actual_end: Vector2 = adapter.project_map_position(from, path, 1, start_hours, delta_hours, 100.0)
					var error := maxf(actual_start.distance_to(start), actual_end.distance_to(end))
					check(error < 0.006, "local " + str(surface.id) + " projection follows span multiplier")
					local_surfaces[surface.id] = {"from": from_id, "to": to_id, "multiplier": surface.multiplier, "physical_distance": actual_start.distance_to(actual_end), "elapsed_hours": delta_hours, "max_position_error": error}
				prefix += terrain.segment_cost_length(a, b)
			directed_edges += 1
			sample_from = from
			sample_to = to
	check(directed_edges == graph.edge_km.size() * 2, "all directed existing edges audited")
	check(graph.edge_km.size() == 575, "575 original curves retained")
	check(max_inverse_error < 0.0000001, "same edge times inverse to speed")
	check(max_cost_error < 0.0000001, "cached cost equals independent whole-curve integral")
	check(max_reverse_error < 0.001, "reverse curve total time remains equal")
	check(min_delay_ratio >= 0.999999, "terrain never speeds up original travel")
	check(max_delay_ratio > 1.01, "real existing routes include slower terrain")
	check(max_migration_error < 0.006, "all directed curves preserve physical 37 percent arc length")
	check(max_projection_speed_error < 0.006, "same elapsed cost at twice speed projects identically")
	check(local_surfaces.has("plain") and local_surfaces.has("forest") and local_surfaces.has("hill"), "real plain, forest and hill spans covered")
	for sample: Dictionary in legacy_samples:
		check(absf(float(sample.scale) - 40.0) < 0.0001, "legacy scale remains base 40 before terrain")
	check(adapter.edge_cost_cache.size() == directed_edges, "one cache entry per direction independent of speed")
	var original_path: Array = [sample_from, sample_to]
	var original_path_copy: Array = original_path.duplicate()
	adapter.project_map_position(sample_from, original_path, 1, 0.1, 0.1, 100.0)
	check(original_path == original_path_copy, "fractional projection leaves original route unchanged")
	check(adapter.upgrade_uniform_travel_progress(Vector2i(-1, -1), sample_to, 1.0, 100.0) < 0.0, "invalid edge migration rejected")
	check(adapter.upgrade_uniform_travel_progress(sample_from, sample_to, -1.0, 100.0) < 0.0, "invalid elapsed migration rejected")
	var counter := CountingTerrain.new()
	counter.source = terrain
	adapter.terrain_travel = counter
	adapter.edge_cost_cache.clear()
	adapter.get_npc_edge_travel_hours(sample_from, sample_to, 100.0)
	var initial_integrations := counter.integrations
	for speed: float in [50.0, 100.0, 200.0]:
		for i in 20:
			adapter.get_npc_edge_travel_hours(sample_from, sample_to, speed)
			adapter.project_map_position(sample_from, original_path, 1, 0.1, 0.1, speed)
	check(initial_integrations > 0 and counter.integrations == initial_integrations, "repeat hours and projections reuse cached full-curve integrals")
	var report := {"checks": checks, "failures": failures, "profile": adapter.get_terrain_speed_profile(), "directed_edges": directed_edges, "max_inverse_error": max_inverse_error, "max_whole_curve_hours_error": max_cost_error, "max_reverse_hours_error": max_reverse_error, "max_37_percent_arc_position_error": max_migration_error, "max_speed_projection_error": max_projection_speed_error, "delay_ratio_range": [min_delay_ratio, max_delay_ratio], "local_surfaces": local_surfaces, "legacy_scale_samples": legacy_samples, "warm_cache_integrations": initial_integrations, "after_60_reads_integrations": counter.integrations}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(REPORT.get_base_dir()))
	var file := FileAccess.open(REPORT, FileAccess.WRITE)
	if file: file.store_string(JSON.stringify(report, "\t"))
	print("NPC_TERRAIN_RESULT ", JSON.stringify(report))
	adapter.free()
	quit(1 if failures else 0)
