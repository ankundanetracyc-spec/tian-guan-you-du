extends RefCounted
## Immutable save sources. Grid indices always belong to the source's own
## cell_size/columns; only continuous positions may be scaled between worlds.
const Scale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const CURRENT_PROFILE_ID := Scale.NAVIGATION_PROFILE
const EXPANDED_PROFILE_ID := "expanded3x_20261106_v1"
const COMPACT_PEAKS_PROFILE_ID := "compact_peaks_20261109_v1"
const EXPANDED_WORLD_SIZE := 22118.4
const COMPACT_WORLD_SIZE := 7372.8
const ORIGINAL_WORLD_SIZE := 12288.0
const LEGACY_WORLD_SIZE := 3762.0
const LEGACY_LAYOUT := "res://textures/world/bai_chuan/acceptance_20261030/display_layout_v3.json"
const OLD_TERRAIN_PATH := "res://resources/world/bai_chuan/terrain_travel_20261105.json"
const TERRAIN_PROFILE_ID := "terrain-speed-v1"
const ROOT := "res://resources/world/bai_chuan/"
static var _navigation_cache: Dictionary = {}
static var _terrain_data: Dictionary = {}

static func _same_size(a: float, b: float) -> bool:
	return is_finite(a) and is_finite(b) and absf(a - b) < 0.001

static func descriptor(profile: String, world_size: float) -> Dictionary:
	var navigation := ""
	var layout := ""
	if profile == CURRENT_PROFILE_ID and _same_size(world_size, Scale.MAP_PIXELS):
		navigation = Scale.NAVIGATION_PATH
		layout = Scale.LAYOUT_PATH
	elif profile == EXPANDED_PROFILE_ID and _same_size(world_size, EXPANDED_WORLD_SIZE):
		navigation = ROOT + "free_navigation_expanded3x_20261106.json"
		layout = ROOT + "free_layout_expanded3x_20261106.json"
	elif profile == COMPACT_PEAKS_PROFILE_ID and _same_size(world_size, EXPANDED_WORLD_SIZE):
		navigation = ROOT + "free_navigation_compact_peaks_20261109.json"
		layout = ROOT + "free_layout_expanded3x_20261106.json"
	elif profile == "complete_mountains_20261106_v1" and _same_size(world_size, COMPACT_WORLD_SIZE):
		navigation = ROOT + "free_navigation_mountains_20261106.json"
		layout = ROOT + "free_layout_mountains_20261106.json"
	elif profile.is_empty() and _same_size(world_size, COMPACT_WORLD_SIZE):
		navigation = ROOT + "free_navigation_3x3_20261104.json"
		layout = ROOT + "free_layout_3x3_20261104.json"
	elif profile.is_empty() and _same_size(world_size, ORIGINAL_WORLD_SIZE):
		navigation = ROOT + "free_navigation_20261104.json"
		layout = ROOT + "free_layout_20261104.json"
	else:
		return {}
	return {"profile": profile, "world_size": world_size, "navigation_path": navigation, "layout_path": layout}

static func is_known_layout(path: String, world_size: float) -> bool:
	for source: Array in [[CURRENT_PROFILE_ID, Scale.MAP_PIXELS], [EXPANDED_PROFILE_ID, EXPANDED_WORLD_SIZE], [COMPACT_PEAKS_PROFILE_ID, EXPANDED_WORLD_SIZE], ["complete_mountains_20261106_v1", COMPACT_WORLD_SIZE], ["", COMPACT_WORLD_SIZE], ["", ORIGINAL_WORLD_SIZE]]:
		var entry := descriptor(source[0], source[1])
		if not entry.is_empty() and path == str(entry.layout_path) and _same_size(world_size, float(entry.world_size)): return true
	return false

static func resolve(profile: String, world_size: float) -> Dictionary:
	var source := descriptor(profile, world_size)
	if source.is_empty(): return {}
	var path: String = source.navigation_path
	var key := path + "|" + profile
	if _navigation_cache.has(key): return _navigation_cache[key]
	if not FileAccess.file_exists(path) or not FileAccess.file_exists(source.layout_path): return {}
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	var layout: Variant = JSON.parse_string(FileAccess.get_file_as_string(source.layout_path))
	if not parsed is Dictionary or not layout is Dictionary: return {}
	if int(parsed.get("version", 0)) != 2 or str(parsed.get("navigation_profile", "")) != profile: return {}
	if not _same_size(float(parsed.get("world_size", 0.0)), world_size) or not _same_size(float(layout.get("worldSize", 0.0)), world_size): return {}
	if not _same_size(float(parsed.get("world_km", 0.0)), Scale.WORLD_KM): return {}
	var columns := int(parsed.get("columns", 0))
	var cell_size := float(parsed.get("cell_size", 0.0))
	var rows: Variant = parsed.get("rows")
	if columns <= 0 or not is_finite(cell_size) or cell_size <= 0.0 or cell_size * columns < world_size: return {}
	if not rows is Array or rows.size() != columns: return {}
	for row: Variant in rows:
		if not row is String or row.length() != columns: return {}
		for i: int in row.length():
			if row.substr(i, 1) not in ["0", "1", "2"]: return {}
	source["columns"] = columns
	source["cell_size"] = cell_size
	source["rows"] = rows
	_navigation_cache[key] = source
	return source

static func contains(point: Vector2, world_size: float) -> bool:
	return point.is_finite() and point.x >= 0.0 and point.y >= 0.0 and point.x < world_size and point.y < world_size

static func is_walkable(point: Vector2, profile: String, source_world_size: float) -> bool:
	var source := resolve(profile, source_world_size)
	if source.is_empty() or not contains(point, source_world_size): return false
	var cell := Vector2i((point / float(source.cell_size)).floor())
	return cell.x >= 0 and cell.y >= 0 and cell.x < int(source.columns) and cell.y < int(source.columns) and str(source.rows[cell.y]).substr(cell.x, 1) == "0"

static func segment_clear(a: Vector2, b: Vector2, profile: String, source_world_size: float) -> bool:
	if not is_walkable(a, profile, source_world_size) or not is_walkable(b, profile, source_world_size): return false
	var source := resolve(profile, source_world_size)
	var cell_size := float(source.cell_size)
	var current := Vector2i((a / cell_size).floor())
	var finish := Vector2i((b / cell_size).floor())
	var delta := b - a
	var step := Vector2i(int(signf(delta.x)), int(signf(delta.y)))
	var tx := INF if step.x == 0 else ((current.x + (1 if step.x > 0 else 0)) * cell_size - a.x) / delta.x
	var ty := INF if step.y == 0 else ((current.y + (1 if step.y > 0 else 0)) * cell_size - a.y) / delta.y
	var dx := INF if step.x == 0 else cell_size / absf(delta.x)
	var dy := INF if step.y == 0 else cell_size / absf(delta.y)
	for i in int(source.columns) * 2 + 2:
		if current == finish: return true
		if current.x == finish.x: tx = INF
		if current.y == finish.y: ty = INF
		if absf(tx - ty) < 0.0000001:
			if not _source_cell_open(source, current + Vector2i(step.x, 0)) or not _source_cell_open(source, current + Vector2i(0, step.y)): return false
			current += step
			tx += dx
			ty += dy
		elif tx < ty:
			current.x += step.x
			tx += dx
		else:
			current.y += step.y
			ty += dy
		if not _source_cell_open(source, current): return false
	return false

static func _source_cell_open(source: Dictionary, cell: Vector2i) -> bool:
	return cell.x >= 0 and cell.y >= 0 and cell.x < int(source.columns) and cell.y < int(source.columns) and str(source.rows[cell.y]).substr(cell.x, 1) == "0"

static func terrain_available(profile: String) -> bool:
	if profile.is_empty(): return true
	if profile != TERRAIN_PROFILE_ID: return false
	if not _terrain_data.is_empty(): return true
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(OLD_TERRAIN_PATH))
	if not data is Dictionary or int(data.get("version", 0)) != 1 or str(data.get("profile_id", "")) != TERRAIN_PROFILE_ID: return false
	if not _same_size(float(data.get("world_size", 0.0)), COMPACT_WORLD_SIZE): return false
	var rows: Variant = data.get("rows")
	var types: Variant = data.get("types")
	var columns := int(data.get("columns", 0))
	var cell_size := float(data.get("cell_size", 0.0))
	if columns <= 0 or not is_finite(cell_size) or cell_size <= 0.0 or not rows is Array or rows.size() != columns or not types is Dictionary: return false
	for row: Variant in rows:
		if not row is String or row.length() != columns: return false
		for i: int in row.length():
			if not types.has(row.substr(i, 1)): return false
	for entry: Variant in types.values():
		if not entry is Dictionary: return false
		var multiplier := float(entry.get("multiplier", 0.0))
		if not is_finite(multiplier) or multiplier <= 0.0: return false
	_terrain_data = data
	return true

static func _terrain_cuts(from: Vector2, to: Vector2, source_world_size: float) -> PackedFloat64Array:
	var cuts := PackedFloat64Array([0.0, 1.0])
	var cell_size := float(_terrain_data.cell_size) * source_world_size / float(_terrain_data.world_size)
	for axis: int in 2:
		var change := float(to[axis]) - float(from[axis])
		if absf(change) < 0.00000001: continue
		var first := floori(minf(from[axis], to[axis]) / cell_size) + 1
		var last := floori(maxf(from[axis], to[axis]) / cell_size)
		for boundary: int in range(first, last + 1):
			var amount := (float(boundary) * cell_size - float(from[axis])) / change
			if amount > 0.000000001 and amount < 0.999999999: cuts.append(amount)
	cuts.sort()
	var unique := PackedFloat64Array()
	for amount: float in cuts:
		if unique.is_empty() or amount - unique[-1] > 0.000000001: unique.append(amount)
	return unique

static func _old_multiplier(point: Vector2, source_world_size: float) -> float:
	var mapped := point * float(_terrain_data.world_size) / source_world_size
	var cell := Vector2i((mapped / float(_terrain_data.cell_size)).floor())
	if cell.x < 0 or cell.y < 0 or cell.x >= int(_terrain_data.columns) or cell.y >= int(_terrain_data.columns): return 1.0
	var code := str(_terrain_data.rows[cell.y]).substr(cell.x, 1)
	return float(_terrain_data.types[code].multiplier)

static func segment_cost_length(from: Vector2, to: Vector2, source_world_size: float, terrain_profile: String) -> float:
	if not from.is_finite() or not to.is_finite() or source_world_size <= 0.0 or not terrain_available(terrain_profile): return INF
	var length := float(from.distance_to(to))
	if terrain_profile.is_empty() or length < 0.00000001: return length
	var cuts := _terrain_cuts(from, to, source_world_size)
	var cost := 0.0
	for i: int in range(1, cuts.size()):
		cost += length * (cuts[i] - cuts[i - 1]) / _old_multiplier(from.lerp(to, (cuts[i] + cuts[i - 1]) * 0.5), source_world_size)
	return cost

static func distance_for_cost(from: Vector2, to: Vector2, budget: float, source_world_size: float, terrain_profile: String) -> float:
	if not is_finite(budget) or budget < 0.0 or not terrain_available(terrain_profile): return -1.0
	var length := float(from.distance_to(to))
	if terrain_profile.is_empty() or length < 0.00000001: return minf(length, budget)
	var cuts := _terrain_cuts(from, to, source_world_size)
	var distance := 0.0
	var remaining := budget
	for i: int in range(1, cuts.size()):
		var multiplier := _old_multiplier(from.lerp(to, (cuts[i] + cuts[i - 1]) * 0.5), source_world_size)
		var span := length * (cuts[i] - cuts[i - 1])
		var cost := span / multiplier
		if remaining <= cost: return minf(length, distance + remaining * multiplier)
		remaining -= cost
		distance += span
	return length
