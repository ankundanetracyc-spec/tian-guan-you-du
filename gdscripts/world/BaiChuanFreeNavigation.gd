extends RefCounted
## Stable navigation data, independent of rendered pixels and camera scale.

const Scale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const DATA_PATH := Scale.NAVIGATION_PATH
const KM_PER_PIXEL := Scale.KM_PER_PIXEL
var world_size := Scale.MAP_PIXELS
var cell_size := Scale.NAVIGATION_CELL_SIZE
var columns := 0
var rows: Array = []
var astar := AStarGrid2D.new()
var ready := false

func initialize() -> bool:
	ready = false
	var data = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH))
	if not data is Dictionary or int(data.get("version", 0)) != 2: return false
	rows = data.rows
	columns = int(data.columns)
	cell_size = float(data.cell_size)
	world_size = float(data.world_size)
	if rows.size() != columns or columns != Scale.NAVIGATION_COLUMNS: return false
	if not is_equal_approx(world_size, Scale.MAP_PIXELS) or not is_equal_approx(cell_size, Scale.NAVIGATION_CELL_SIZE): return false
	if not is_equal_approx(float(data.get("world_km", 0.0)), Scale.WORLD_KM): return false
	astar.region = Rect2i(0, 0, columns, columns)
	astar.cell_size = Vector2.ONE * cell_size
	astar.offset = Vector2.ONE * cell_size * 0.5
	astar.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES
	astar.default_compute_heuristic = AStarGrid2D.HEURISTIC_OCTILE
	astar.default_estimate_heuristic = AStarGrid2D.HEURISTIC_OCTILE
	astar.update()
	for y in columns:
		if str(rows[y]).length() != columns: return false
		for x in columns:
			var terrain: String = str(rows[y])[x]
			if terrain not in ["0", "1", "2"]: return false
			if terrain != "0": astar.set_point_solid(Vector2i(x, y))
	ready = true
	return true

func cell(point: Vector2) -> Vector2i:
	return Vector2i((point / cell_size).floor())

func is_walkable(point: Vector2) -> bool:
	if not ready or not point.is_finite() or point.x < 0 or point.y < 0 or point.x >= world_size or point.y >= world_size: return false
	var c := cell(point)
	return astar.is_in_boundsv(c) and not astar.is_point_solid(c)

func terrain_name(point: Vector2) -> String:
	if not ready: return "通行数据未就绪"
	if not point.is_finite() or point.x < 0 or point.y < 0 or point.x >= world_size or point.y >= world_size: return "地图之外"
	var c := cell(point)
	return {"0": "平地与通道", "1": "山体", "2": "水域"}.get(str(rows[c.y])[c.x], "不可通行")

func segment_clear(a: Vector2, b: Vector2) -> bool:
	if not is_walkable(a) or not is_walkable(b): return false
	var previous := cell(a)
	var count := maxi(1, ceili(a.distance_to(b) / (cell_size * 0.25)))
	for i in range(1, count + 1):
		var point := a.lerp(b, float(i) / count)
		if not is_walkable(point): return false
		var current := cell(point)
		if current.x != previous.x and current.y != previous.y:
			if astar.is_point_solid(Vector2i(previous.x, current.y)) or astar.is_point_solid(Vector2i(current.x, previous.y)): return false
		previous = current
	return true

func find_path(from: Vector2, to: Vector2) -> PackedVector2Array:
	if not is_walkable(from) or not is_walkable(to): return PackedVector2Array()
	if from.distance_to(to) < 0.01: return PackedVector2Array([from])
	if segment_clear(from, to): return PackedVector2Array([from, to])
	var raw := astar.get_point_path(cell(from), cell(to))
	if raw.is_empty(): return raw
	# Exact endpoints are retained, including points on opposite sides of a cell.
	raw.insert(0, from)
	raw.append(to)
	var result := PackedVector2Array([from])
	var at := 0
	while at < raw.size() - 1:
		var next := at + 1
		# Bound lookahead; a long trans-map route must not stall the UI.
		for j in range(at + 2, mini(at + 70, raw.size())):
			if segment_clear(raw[at], raw[j]): next = j
		if not segment_clear(result[-1], raw[next]): return PackedVector2Array()
		if result[-1].distance_to(raw[next]) > 0.001: result.append(raw[next])
		at = next
	return result

func path_length(points: PackedVector2Array) -> float:
	var length := 0.0
	for i in range(1, points.size()): length += points[i - 1].distance_to(points[i])
	return length

func validate_path(points: PackedVector2Array) -> bool:
	if points.is_empty(): return false
	for point in points:
		if not is_walkable(point): return false
	for i in range(1, points.size()):
		if not segment_clear(points[i - 1], points[i]): return false
	return true
