extends RefCounted
## Stable navigation data, independent of rendered pixels and camera scale.

const Scale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const SourceRegistry = preload("res://gdscripts/world/WorldMapSourceRegistry.gd")
const PackedNavigation = preload("res://csharp/world/PackedWorldNavigation.cs")
const DATA_PATH := Scale.NAVIGATION_PATH
const PROFILE_ID := Scale.NAVIGATION_PROFILE
const PREVIOUS_DATA_PATH := "res://resources/world/bai_chuan/free_navigation_3x3_20261104.json"
const KM_PER_PIXEL := Scale.KM_PER_PIXEL
var world_size := Scale.MAP_PIXELS
var cell_size := Scale.NAVIGATION_CELL_SIZE
var columns := 0
var rows: Array = []
var mainland_rows: Array = []
var previous_rows: Array = []
var astar
var ready := false
static var _shared: Dictionary = {}

func initialize() -> bool:
	ready = false
	if _shared.has(DATA_PATH):
		var cached: Dictionary = _shared[DATA_PATH]
		rows = cached.rows
		mainland_rows = cached.mainland_rows
		world_size = cached.world_size
		cell_size = cached.cell_size
		columns = cached.columns
		astar = cached.astar
		ready = true
		return true
	var data = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH))
	if not data is Dictionary or int(data.get("version", 0)) != 2 or str(data.get("navigation_profile", "")) != PROFILE_ID: return false
	rows = data.rows
	mainland_rows = data.get("mainland_rows", [])
	if mainland_rows.size() != rows.size(): return false
	columns = int(data.columns)
	cell_size = float(data.cell_size)
	world_size = float(data.world_size)
	if rows.size() != columns or columns != Scale.NAVIGATION_COLUMNS: return false
	if not is_equal_approx(world_size, Scale.MAP_PIXELS) or not is_equal_approx(cell_size, Scale.NAVIGATION_CELL_SIZE): return false
	if not is_equal_approx(float(data.get("world_km", 0.0)), Scale.WORLD_KM): return false
	astar = PackedNavigation.new()
	if not astar.configure(columns, cell_size, rows, world_size): return false
	_shared[DATA_PATH] = {"rows":rows,"mainland_rows":mainland_rows,"world_size":world_size,"cell_size":cell_size,"columns":columns,"astar":astar}
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
	# Traverse every crossed cell exactly. Spaced samples used to miss thin
	# corner intersections, allowing a planned path to stop midway through it.
	var current := cell(a)
	var finish := cell(b)
	var delta := b - a
	var step := Vector2i(int(signf(delta.x)), int(signf(delta.y)))
	var tx := INF if step.x == 0 else ((current.x + (1 if step.x > 0 else 0)) * cell_size - a.x) / delta.x
	var ty := INF if step.y == 0 else ((current.y + (1 if step.y > 0 else 0)) * cell_size - a.y) / delta.y
	var dx := INF if step.x == 0 else cell_size / absf(delta.x)
	var dy := INF if step.y == 0 else cell_size / absf(delta.y)
	for i in columns * 2 + 2:
		if current == finish: return true
		if current.x == finish.x: tx = INF
		if current.y == finish.y: ty = INF
		if absf(tx - ty) < 0.0000001:
			if astar.is_point_solid(current + Vector2i(step.x, 0)) or astar.is_point_solid(current + Vector2i(0, step.y)): return false
			current += step
			tx += dx
			ty += dy
		elif tx < ty:
			current.x += step.x
			tx += dx
		else:
			current.y += step.y
			ty += dy
		if not astar.is_in_boundsv(current) or astar.is_point_solid(current): return false
	return false

func previous_is_walkable(point: Vector2, source_profile := "", source_world_size := 7372.8) -> bool:
	return SourceRegistry.is_walkable(point, source_profile, source_world_size)

func nearest_walkable(point: Vector2) -> Vector2:
	if is_walkable(point): return point
	var origin := cell(point)
	var best := Vector2(-1, -1)
	var distance := INF
	# Preserve the reviewed geographic migration cap as display scale changes.
	var radius := ceili((660.0 * world_size / 7372.8) / cell_size)
	for y in range(maxi(0, origin.y - radius), mini(columns, origin.y + radius + 1)):
		for x in range(maxi(0, origin.x - radius), mini(columns, origin.x + radius + 1)):
			if str(mainland_rows[y])[x] != "1": continue
			var candidate := (Vector2(x, y) + Vector2.ONE * 0.5) * cell_size
			var candidate_distance := candidate.distance_squared_to(point)
			if candidate_distance < distance:
				distance = candidate_distance
				best = candidate
	return best

func find_path(from: Vector2, to: Vector2) -> PackedVector2Array:
	if not is_walkable(from) or not is_walkable(to): return PackedVector2Array()
	if from.distance_to(to) < 0.01: return PackedVector2Array([from])
	if segment_clear(from, to): return PackedVector2Array([from, to])
	var raw: PackedVector2Array = astar.get_point_path(cell(from), cell(to))
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
