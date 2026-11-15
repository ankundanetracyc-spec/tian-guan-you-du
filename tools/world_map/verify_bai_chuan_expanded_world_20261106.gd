extends SceneTree
## Independent geometry checks against the real expanded navigation and DDA.
## The runner supplies a minimal .NET project without game autoloads/scenes.
const Scale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const Navigation = preload("res://gdscripts/world/BaiChuanFreeNavigation.gd")
const Graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd")
const NpcGraph = preload("res://gdscripts/world/BaiChuanNpcNodeGraph.gd")
const Terrain = preload("res://gdscripts/world/BaiChuanTerrainTravel.gd")
var checks := 0
var failures: Array[String] = []
var metrics: Dictionary = {}
var report_prefix := "user://geometry_runtime_report"
var python_report_path := ""
var scene: Dictionary = {}
var nav = Navigation.new()

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--report-prefix="): report_prefix = argument.trim_prefix("--report-prefix=")
		if argument.begins_with("--python-report="): python_report_path = argument.trim_prefix("--python-report=")
		if argument.begins_with("--expected-appdata="):
			_check(OS.get_environment("APPDATA") == argument.trim_prefix("--expected-appdata="), "isolated APPDATA")
	call_deferred("_run")

func _read(path: String) -> Dictionary:
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	return data if data is Dictionary else {}

func _run() -> void:
	var began := Time.get_ticks_usec()
	_check(nav.initialize(), "real expanded C# packed navigation initializes")
	if not nav.ready:
		_finish(began)
		return
	scene = _read(Scale.LANDSCAPE_PATH)
	_check(not scene.is_empty(), "active landscape loads")
	_check(Scale.MAP_PIXELS == 22118.4 and Scale.WORLD_KM == 8000.0 and nav.columns == 1413, "world/nav scale")
	_check(is_equal_approx(nav.KM_PER_PIXEL * nav.world_size, 8000.0), "kilometre ruler")
	var terrain := Terrain.new()
	_check(terrain.initialize(), "separate 471-cell terrain initializes")
	_check(Scale.TERRAIN_COLUMNS == 471 and is_equal_approx(Scale.TERRAIN_CELL_SIZE, nav.cell_size * 3.0), "terrain/collision resolutions independent")
	_python_findings()
	_padding()
	_routes_and_identifiers()
	_bridges()
	_native_mountain_samples()
	_runtime_texture_identity()
	_finish(began)

func _python_findings() -> void:
	if python_report_path.is_empty(): return
	var report := _read(python_report_path)
	_check(not report.is_empty(), "independent Python geometry audit exists")
	metrics["python_audit"] = report
	# Reconfirm every unique identified cell through native image alpha and the
	# real navigation API, independently of the Python parsed grid.
	var banks := {}
	for sample: Dictionary in report.get("mountain_leak_cells", []):
		var asset: Dictionary = scene.mountain_catalog[str(sample.asset)]
		var bank: String = sample.bank
		if not banks.has(bank):
			var image := Image.new()
			image.load_png_from_buffer(FileAccess.get_file_as_bytes(str(scene.mountain_sources[bank].path)))
			banks[bank] = image
		var image: Image = banks[bank]
		var alpha := roundi(image.get_pixel(int(asset.region[0]) + int(sample.native_pixel[0]), int(asset.region[1]) + int(sample.native_pixel[1])).a * 255.0)
		_check(alpha == int(sample.native_alpha) and alpha >= 46, "real source alpha confirms mountain leak sample " + str(sample.id))
		var point := Vector2(sample.world[0], sample.world[1])
		_check(not nav.is_walkable(point), "native mountain alpha=%d point traversable %s world=%s cell=%s" % [alpha, sample.id, point, nav.cell(point)])

func _padding() -> void:
	var blocked_padding := 0
	var all_blocked := true
	for y: int in nav.columns:
		for x: int in nav.columns:
			var point: Vector2 = (Vector2(x, y) + Vector2.ONE * 0.5) * nav.cell_size
			if point.x < nav.world_size and point.y < nav.world_size: continue
			blocked_padding += 1
			if not nav.astar.is_point_solid(Vector2i(x, y)) or nav.is_walkable(point): all_blocked = false
	_check(all_blocked and blocked_padding > 0, "every allocated outside-world padding cell is blocked")
	for point: Vector2 in [Vector2(-0.01, 20), Vector2(20, -0.01), Vector2(nav.world_size, 20), Vector2(20, nav.world_size), Vector2(INF, 10), Vector2(NAN, 10)]:
		_check(not nav.is_walkable(point), "outside or malformed world coordinate rejected " + str(point))
	metrics["padding_cells_checked"] = blocked_padding

func _routes_and_identifiers() -> void:
	var graph := Graph.new()
	graph.use_free_world = true
	_check(graph.load_layout(), "real expanded graph initializes")
	var layout := _read(Scale.LAYOUT_PATH)
	_check(layout.locations.size() == 123 and graph.positions.size() == 444 and graph.edge_points.size() == 575, "123 places / 444 opaque anchors / 575 edges")
	for id: String in graph.positions:
		_check(nav.is_walkable(graph.positions[id]), "anchor on real walkable ground " + id)
	var seen := {str(graph.positions.keys()[0]): true}
	var queue: Array[String] = [str(graph.positions.keys()[0])]
	var head := 0
	while head < queue.size():
		var id: String = queue[head]
		head += 1
		for link: Dictionary in graph.adjacency[id]:
			if not seen.has(str(link.to)):
				seen[str(link.to)] = true
				queue.append(str(link.to))
	_check(seen.size() == 444, "all 444 anchors including 123 places share one connected route network")
	var subdivisions := 0
	var directed := 0
	for edge: String in graph.edge_points:
		var points: PackedVector2Array = graph.edge_points[edge]
		for reverse: bool in [false, true]:
			var path := points.duplicate()
			if reverse: path.reverse()
			var label := edge + (" reverse" if reverse else " forward")
			_check(nav.validate_path(path), "real Nav validates route " + label)
			var subdivided := true
			var first_bad := ""
			for i: int in range(1, path.size()):
				for j: int in 20:
					var a := path[i - 1].lerp(path[i], float(j) / 20.0)
					var b := path[i - 1].lerp(path[i], float(j + 1) / 20.0)
					subdivisions += 1
					if not nav.segment_clear(a, b):
						subdivided = false
						if first_bad.is_empty(): first_bad = " segment=%d part=%d cells=%s->%s" % [i, j, nav.cell(a), nav.cell(b)]
			_check(subdivided, "20-way subsegment DDA/corner traversal " + label + first_bad)
			directed += 1
	var npc := NpcGraph.new()
	_check(npc.initialize(), "real NPC opaque graph initializes")
	var old := _read("res://resources/world/bai_chuan/free_layout_mountains_20261106.json")
	var previous_ids: Array = old.locations.keys() + old.transitPositions.keys()
	previous_ids.sort()
	_check(npc.ids == previous_ids, "opaque anchor ordering retains all previous save identities")
	for id: String in npc.ids:
		var token: Vector2i = npc.node_id_to_cell(id)
		_check(npc.cell_to_node_id(token) == id and token.x < 200 and token.y < 3, "opaque index stays independent of 1413 navigation grid " + id)
	npc.free()
	metrics["directed_routes_validated"] = directed
	metrics["real_dda_subsegments_checked"] = subdivisions
	metrics["connected_anchors"] = seen.size()

func _bridges() -> void:
	var water := _read("res://resources/world/bai_chuan/expanded_water_20261106.json")
	_check(scene.bridges.size() == 9, "nine explicit native bridges")
	var opened_cells := 0
	for bridge: Dictionary in scene.bridges:
		var count := 0
		var all_open := true
		var actual_water := true
		for run: Array in bridge.open_runs:
			for x: int in range(int(run[1]), int(run[2])):
				var y := int(run[0])
				var point: Vector2 = (Vector2(x, y) + Vector2.ONE * 0.5) * nav.cell_size
				if not nav.is_walkable(point): all_open = false
				if str(water.water_rows[y])[x] != "2" or str(water.bridge_open_rows[y])[x] != "1": actual_water = false
				count += 1
		_check(all_open and actual_water and count == int(bridge.open_water_cells), "bridge open_runs match real water/runtime openings " + str(bridge.id))
		opened_cells += count
		var a := Vector2(bridge.bank_contacts[0].position[0], bridge.bank_contacts[0].position[1])
		var b := Vector2(bridge.bank_contacts[1].position[0], bridge.bank_contacts[1].position[1])
		var path: PackedVector2Array = nav.find_path(a, b)
		_check(not path.is_empty() and nav.validate_path(path), "real C# path and exact DDA cross native bridge " + str(bridge.id))
	metrics["bridge_water_cells_checked"] = opened_cells

func _native_mountain_samples() -> void:
	var banks := {}
	for key: String in scene.mountain_sources:
		var source := Image.new()
		_check(source.load_png_from_buffer(FileAccess.get_file_as_bytes(str(scene.mountain_sources[key].path))) == OK, "native mountain PNG decodes " + key)
		banks[key] = source
	var tested := 0
	var failed_objects := 0
	for entry: Dictionary in scene.mountains:
		var asset: Dictionary = scene.mountain_catalog[str(entry.asset)]
		var region: Array = asset.region
		var image: Image = banks[str(asset.bank)]
		var clear := false
		var sampled := 0
		for y: int in range(3, int(region[3]), 17):
			for x: int in range(3, int(region[2]), 17):
				if roundi(image.get_pixel(int(region[0]) + x, int(region[1]) + y).a * 255.0) < 46: continue
				var u := (float(x) + 0.5) / float(region[2])
				if bool(entry.flip): u = 1.0 - u
				var point := Vector2(float(entry.bounds[0]) + u * float(entry.bounds[2]), float(entry.bounds[1]) + (float(y) + 0.5) / float(region[3]) * float(entry.bounds[3]))
				if nav.is_walkable(point): clear = true
				sampled += 1
				tested += 1
		_check(sampled > 0 and not clear, "complete native mountain alpha>=46 samples block real navigation " + str(entry.id))
		if clear: failed_objects += 1
	metrics["native_mountain_runtime_samples"] = tested
	metrics["mountains_with_walkable_opaque_samples"] = failed_objects

func _runtime_texture_identity() -> void:
	var paths: Array[String] = [str(scene.ground_texture), str(scene.ground_tone_data), str(scene.tree_atlas), str(scene.water_distance), str(scene.water_texture), str(scene.bridge_atlas)]
	for source: Dictionary in scene.mountain_sources.values(): paths.append(str(source.path))
	var catalog := _read(str(_read(Scale.LAYOUT_PATH).native_node_catalog))
	for source: Dictionary in catalog.atlases.values(): paths.append(str(source.path))
	for path: String in paths:
		var resource_path := str(scene.runtime_texture_base) + path.get_file().get_basename() + ".res"
		var texture := load(resource_path) as Texture2D
		_check(texture != null, "native runtime texture exists " + resource_path)
		if texture == null: continue
		var native := Image.new()
		_check(native.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK, "native source image decodes " + path)
		var actual := texture.get_image()
		actual.clear_mipmaps()
		_check(actual.get_size() == native.get_size() and actual.get_format() == native.get_format() and actual.get_data() == native.get_data(), "runtime texture preserves every native base pixel " + path)
	metrics["runtime_native_textures_checked"] = paths.size()

func _check(condition: bool, label: String) -> void:
	checks += 1
	if condition: return
	failures.append(label)
	if failures.size() <= 12: push_error("EXPANDED_GEOMETRY_FAIL " + label)

func _finish(began: int) -> void:
	metrics["elapsed_seconds"] = float(Time.get_ticks_usec() - began) / 1000000.0
	var result := {"checks": checks, "failures": failures, "metrics": metrics, "full_main_started": false, "production_files_modified": false}
	var file := FileAccess.open(report_prefix + ".json", FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(result, "\t") + "\n")
		file.close()
	var text_report := FileAccess.open(report_prefix + ".txt", FileAccess.WRITE)
	if text_report != null:
		text_report.store_string("CHECKS %d FAILURES %d\n" % [checks, failures.size()] + "\n".join(failures) + "\n")
		text_report.close()
	print("EXPANDED_GEOMETRY_CHECKS ", checks, " FAILURES ", failures.size(), " SECONDS ", metrics.elapsed_seconds)
	quit(0 if failures.is_empty() else 1)
