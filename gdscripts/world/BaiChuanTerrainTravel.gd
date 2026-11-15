extends RefCounted
## Positive travel costs only. Walkability remains owned by FreeNavigation.

const PROFILE_ID := "terrain-speed-v1"
const Scale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const DATA_PATH := Scale.TERRAIN_PATH
static var _shared: Dictionary = {}
static var _load_attempted := false
var ready := false

func initialize() -> bool:
	if not _load_attempted:
		_load_attempted = true
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH))
		if parsed is Dictionary and _valid_data(parsed):
			_shared = parsed
	ready = not _shared.is_empty()
	return ready

static func _valid_data(data: Dictionary) -> bool:
	if int(data.get("version", 0)) != 1 or str(data.get("profile_id", "")) != PROFILE_ID: return false
	if not is_equal_approx(float(data.get("world_size", 0.0)), Scale.MAP_PIXELS): return false
	if not is_equal_approx(float(data.get("cell_size", 0.0)), Scale.TERRAIN_CELL_SIZE): return false
	if int(data.get("columns", 0)) != Scale.TERRAIN_COLUMNS: return false
	var rows: Variant = data.get("rows")
	var types: Variant = data.get("types")
	if not rows is Array or rows.size() != Scale.TERRAIN_COLUMNS or not types is Dictionary: return false
	for row: Variant in rows:
		if not row is String or row.length() != Scale.TERRAIN_COLUMNS: return false
		for index: int in row.length():
			if not types.has(row.substr(index, 1)): return false
	for entry: Variant in types.values():
		if not entry is Dictionary: return false
		var multiplier := float(entry.get("multiplier", 0.0))
		if not is_finite(multiplier) or multiplier < 0.05 or multiplier > 3.0: return false
	return true

func _code_at(point: Vector2) -> String:
	if not ready or not point.is_finite(): return "P"
	var cell := Vector2i((point / float(_shared.cell_size)).floor())
	if cell.x < 0 or cell.y < 0 or cell.x >= int(_shared.columns) or cell.y >= int(_shared.columns): return "P"
	return str(_shared.rows[cell.y]).substr(cell.x, 1)

func terrain_at(point: Vector2) -> Dictionary:
	if not ready: return {"id": "plain", "name": "平地", "multiplier": 1.0}
	return (_shared.types.get(_code_at(point), _shared.types.P) as Dictionary).duplicate()

func speed_multiplier_at(point: Vector2) -> float:
	if not ready: return 1.0
	return float(_shared.types.get(_code_at(point), _shared.types.P).multiplier)

func _cuts(from: Vector2, to: Vector2) -> PackedFloat64Array:
	var result := PackedFloat64Array([0.0, 1.0])
	if not ready: return result
	var cell_size := float(_shared.cell_size)
	for axis: int in 2:
		var start: float = from[axis]
		var change: float = float(to[axis]) - start
		if absf(change) < 0.00000001: continue
		var first := floori(minf(start, float(to[axis])) / cell_size) + 1
		var last := floori(maxf(start, float(to[axis])) / cell_size)
		for boundary: int in range(first, last + 1):
			var amount := (float(boundary) * cell_size - start) / change
			if amount > 0.000000001 and amount < 0.999999999: result.append(amount)
	result.sort()
	var unique := PackedFloat64Array()
	for amount: float in result:
		if unique.is_empty() or amount - unique[-1] > 0.000000001: unique.append(amount)
	return unique

func segment_cost_length(from: Vector2, to: Vector2) -> float:
	if not from.is_finite() or not to.is_finite(): return INF
	var length := float(from.distance_to(to))
	if length < 0.00000001: return 0.0
	if not ready: return length
	var cuts := _cuts(from, to)
	var cost := 0.0
	for index: int in range(1, cuts.size()):
		var multiplier := speed_multiplier_at(from.lerp(to, (cuts[index - 1] + cuts[index]) * 0.5))
		cost += length * (cuts[index] - cuts[index - 1]) / multiplier
	return cost

func path_cost_length(points: PackedVector2Array) -> float:
	var cost := 0.0
	for index: int in range(1, points.size()): cost += segment_cost_length(points[index - 1], points[index])
	return cost

func distance_for_cost(from: Vector2, to: Vector2, cost_budget: float) -> float:
	if not from.is_finite() or not to.is_finite() or not is_finite(cost_budget) or cost_budget <= 0.0: return 0.0
	var length := float(from.distance_to(to))
	if length < 0.00000001: return 0.0
	if not ready: return minf(length, cost_budget)
	var cuts := _cuts(from, to)
	var distance := 0.0
	var remaining := cost_budget
	for index: int in range(1, cuts.size()):
		var multiplier := speed_multiplier_at(from.lerp(to, (cuts[index - 1] + cuts[index]) * 0.5))
		var span := length * (cuts[index] - cuts[index - 1])
		var cost := span / multiplier
		if remaining < cost: return minf(length, distance + remaining * multiplier)
		remaining -= cost
		distance += span
	return length
