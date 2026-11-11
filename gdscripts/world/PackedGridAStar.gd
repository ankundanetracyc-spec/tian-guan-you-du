extends RefCounted
## Binary, eight-way grid search without a native object for every map cell.
## Coordinates returned here are cell centres; the caller owns exact endpoints,
## world-edge checks, line-of-sight smoothing and terrain travel costs.

const SQRT_TWO := 1.4142135623730951
const DIAGONAL_EXTRA := SQRT_TWO - 1.0
const PRIORITY_EPSILON := 0.000000001
const MAX_GENERATION := 2147483647
const DIRECTIONS: Array[Vector2i] = [
	Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1),
	Vector2i(1, 1), Vector2i(-1, 1), Vector2i(1, -1), Vector2i(-1, -1),
]

var columns := 0
var cell_size := 0.0
var world_size := 0.0
var last_expanded_cells := 0
var last_heap_peak := 0
var last_search_usec := 0
var _configured := false
var _solid := PackedByteArray()
var _parents := PackedInt32Array()
var _g_scores := PackedFloat64Array()
var _stamps := PackedInt32Array()
var _generation := 0
var _heap_ids := PackedInt32Array()
var _heap_priorities := PackedFloat64Array()
var _heap_costs := PackedFloat64Array()
var _heap_count := 0

func configure(new_columns: int, new_cell_size: float, data_rows: Array, new_world_size: float) -> bool:
	_configured = false
	columns = 0
	_heap_count = 0
	if new_columns <= 0 or new_columns > 46340 or data_rows.size() != new_columns: return false
	if not is_finite(new_cell_size) or new_cell_size <= 0.0: return false
	if not is_finite(new_world_size) or new_world_size <= 0.0: return false
	if new_world_size > float(new_columns) * new_cell_size + 0.000001: return false
	var count := new_columns * new_columns
	var occupancy := PackedByteArray()
	occupancy.resize(count)
	for y: int in new_columns:
		if not data_rows[y] is String: return false
		# UTF-8 keeps malformed input validation quiet and lossless; valid terrain
		# still occupies exactly one byte per ASCII cell.
		var row: PackedByteArray = (data_rows[y] as String).to_utf8_buffer()
		if row.size() != new_columns: return false
		var offset := y * new_columns
		for x: int in new_columns:
			var code := int(row[x])
			if code < 48 or code > 50: return false
			occupancy[offset + x] = 0 if code == 48 else 1
	_solid = occupancy
	_parents.resize(count)
	_g_scores.resize(count)
	_stamps.resize(count)
	_stamps.fill(0)
	_generation = 0
	if _heap_ids.size() < 256:
		_heap_ids.resize(256)
		_heap_priorities.resize(256)
		_heap_costs.resize(256)
	columns = new_columns
	cell_size = new_cell_size
	world_size = new_world_size
	_configured = true
	return true

func is_in_boundsv(point: Vector2i) -> bool:
	return _configured and point.x >= 0 and point.y >= 0 and point.x < columns and point.y < columns

func is_point_solid(point: Vector2i) -> bool:
	return not is_in_boundsv(point) or _solid[point.y * columns + point.x] != 0

func get_storage_bytes() -> int:
	# Array payload only; excludes the small instance/array headers and caller rows.
	return _solid.size() + _parents.size() * 4 + _g_scores.size() * 8 + _stamps.size() * 4 + _heap_ids.size() * 20

@warning_ignore("integer_division")
func get_point_path(from: Vector2i, to: Vector2i) -> PackedVector2Array:
	var began := Time.get_ticks_usec()
	last_expanded_cells = 0
	last_heap_peak = 0
	_heap_count = 0
	if is_point_solid(from) or is_point_solid(to):
		last_search_usec = Time.get_ticks_usec() - began
		return PackedVector2Array()
	# Positive stamps mean open/discovered; negative stamps mean closed. An epoch
	# avoids clearing millions of scores or parents between ordinary searches.
	if _generation >= MAX_GENERATION:
		_stamps.fill(0)
		_generation = 0
	_generation += 1
	var start := from.y * columns + from.x
	var finish := to.y * columns + to.x
	_stamps[start] = _generation
	_parents[start] = start
	_g_scores[start] = 0.0
	var first_dx := absi(from.x - to.x)
	var first_dy := absi(from.y - to.y)
	_heap_push(start, 0.0, maxi(first_dx, first_dy) + DIAGONAL_EXTRA * mini(first_dx, first_dy))
	while _heap_count > 0:
		var current := _heap_pop()
		if _stamps[current] != _generation: continue
		# Octile distance is consistent for these unit/diagonal edge costs. The
		# cheapest heap entry closes a cell once; lazy duplicates then expire.
		_stamps[current] = -_generation
		last_expanded_cells += 1
		if current == finish:
			var result := _reconstruct_path(start, finish)
			last_search_usec = Time.get_ticks_usec() - began
			return result
		var x := current % columns
		var y := current / columns
		var cost := _g_scores[current]
		for direction: Vector2i in DIRECTIONS:
			var next_x := x + direction.x
			var next_y := y + direction.y
			if next_x < 0 or next_y < 0 or next_x >= columns or next_y >= columns: continue
			var next := next_y * columns + next_x
			if _solid[next] != 0 or _stamps[next] == -_generation: continue
			var diagonal := direction.x != 0 and direction.y != 0
			if diagonal and (_solid[y * columns + next_x] != 0 or _solid[next_y * columns + x] != 0): continue
			var next_cost := cost + (SQRT_TWO if diagonal else 1.0)
			if _stamps[next] == _generation and next_cost >= _g_scores[next]: continue
			_stamps[next] = _generation
			_parents[next] = current
			_g_scores[next] = next_cost
			var dx := absi(next_x - to.x)
			var dy := absi(next_y - to.y)
			var heuristic := maxi(dx, dy) + DIAGONAL_EXTRA * mini(dx, dy)
			_heap_push(next, next_cost, next_cost + heuristic)
	last_search_usec = Time.get_ticks_usec() - began
	return PackedVector2Array()

@warning_ignore("integer_division")
func _reconstruct_path(start: int, finish: int) -> PackedVector2Array:
	var result := PackedVector2Array()
	var current := finish
	while true:
		result.append((Vector2(current % columns, current / columns) + Vector2.ONE * 0.5) * cell_size)
		if current == start: break
		current = _parents[current]
	result.reverse()
	return result

@warning_ignore("integer_division")
func _heap_push(id: int, cost: float, priority: float) -> void:
	if _heap_count == _heap_ids.size():
		var capacity := maxi(256, _heap_count * 2)
		_heap_ids.resize(capacity)
		_heap_priorities.resize(capacity)
		_heap_costs.resize(capacity)
	var at := _heap_count
	_heap_count += 1
	last_heap_peak = maxi(last_heap_peak, _heap_count)
	while at > 0:
		var parent := (at - 1) / 2
		var parent_priority := _heap_priorities[parent]
		var parent_cost := _heap_costs[parent]
		if priority > parent_priority + PRIORITY_EPSILON: break
		# Equal octile totals accumulate in different floating-point orders. Treat
		# only roundoff as a tie so an empty diagonal/straight region follows the
		# deeper frontier instead of expanding every equivalent permutation.
		if absf(priority - parent_priority) <= PRIORITY_EPSILON and (cost < parent_cost or (cost == parent_cost and id >= _heap_ids[parent])): break
		_heap_ids[at] = _heap_ids[parent]
		_heap_priorities[at] = parent_priority
		_heap_costs[at] = parent_cost
		at = parent
	_heap_ids[at] = id
	_heap_priorities[at] = priority
	_heap_costs[at] = cost

func _heap_pop() -> int:
	var result := _heap_ids[0]
	_heap_count -= 1
	if _heap_count == 0: return result
	var id := _heap_ids[_heap_count]
	var priority := _heap_priorities[_heap_count]
	var cost := _heap_costs[_heap_count]
	var at := 0
	while true:
		var child := at * 2 + 1
		if child >= _heap_count: break
		var right := child + 1
		if right < _heap_count and _heap_precedes(right, child): child = right
		var child_priority := _heap_priorities[child]
		var child_cost := _heap_costs[child]
		if child_priority > priority + PRIORITY_EPSILON: break
		if absf(child_priority - priority) <= PRIORITY_EPSILON and (child_cost < cost or (child_cost == cost and _heap_ids[child] >= id)): break
		_heap_ids[at] = _heap_ids[child]
		_heap_priorities[at] = child_priority
		_heap_costs[at] = child_cost
		at = child
	_heap_ids[at] = id
	_heap_priorities[at] = priority
	_heap_costs[at] = cost
	return result

func _heap_precedes(a: int, b: int) -> bool:
	if absf(_heap_priorities[a] - _heap_priorities[b]) > PRIORITY_EPSILON: return _heap_priorities[a] < _heap_priorities[b]
	if _heap_costs[a] != _heap_costs[b]: return _heap_costs[a] > _heap_costs[b]
	return _heap_ids[a] < _heap_ids[b]
