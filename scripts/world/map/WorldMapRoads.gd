@tool
extends Node2D

## 百川世界地图的道路层。
##
## 道路不是节点之间的直线连线，而是由路线数据提供的山谷折线。这样
## 地图上的视觉路线和角色移动使用同一套点列，角色不会在山体上抄近路。

var map_data: Object
var selected_route_id: String = ""
var route_points: Dictionary = {}
var route_unlocked: Dictionary = {}
var node_positions: Dictionary = {}
var _terrain_occlusion_material: ShaderMaterial

const CORE_NODE_CLEARANCE := 64.0
const LOCATION_NODE_CLEARANCE := 24.0
const WILDERNESS_NODE_CLEARANCE := 18.0

const TERRAIN_OCCLUSION_SHADER := """
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec4 road = texture(TEXTURE, UV) * COLOR;
    vec4 terrain = textureLod(screen_texture, SCREEN_UV, 0.0);
    float luminance = dot(terrain.rgb, vec3(0.2126, 0.7152, 0.0722));
    float valley_mask = smoothstep(0.45, 0.64, luminance);
	// Keep the route readable over the pale ink terrain. The authored valley
	// points already keep it off mountain ridges, so this is only a very slight
	// blend rather than a transparent road.
	road.a *= mix(0.82, 1.0, valley_mask);
    COLOR = road;
}
"""

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	_setup_terrain_occlusion()
	_refresh_styles()
	# Keep the editor-facing map preview consistent with runtime: even without
	# a WorldNodeMap resource attached, the painted route stops at each marker.
	if Engine.is_editor_hint():
		for child in get_children():
			if not child is Line2D or not str(child.name).ends_with("_Ink"):
				continue
			var route_id := str(child.name).trim_suffix("_Ink")
			var display_points := _display_points_for_route(route_id, (child as Line2D).points)
			(child as Line2D).points = display_points
			var base := get_node_or_null(route_id + "_Base") as Line2D
			if base != null:
				base.points = display_points

func _setup_terrain_occlusion() -> void:
	# The editor has no reliable screen texture for a @tool Line2D. Applying
	# the runtime occlusion shader there makes every road look transparent in
	# the scene view, which also hides the authored route from level designers.
	if Engine.is_editor_hint():
		return
	if _terrain_occlusion_material != null:
		return
	var shader := Shader.new()
	shader.code = TERRAIN_OCCLUSION_SHADER
	_terrain_occlusion_material = ShaderMaterial.new()
	_terrain_occlusion_material.shader = shader
	for child in get_children():
		if child is Line2D:
			(child as Line2D).material = _terrain_occlusion_material

func set_map_data(value: Object) -> void:
	map_data = value
	route_points.clear()
	route_unlocked.clear()
	for child in get_children():
		if child is Line2D:
			(child as Line2D).visible = false
	if map_data == null:
		_refresh_styles()
		return
	for road in map_data.get_roads():
		var route_id := str(road.get("route_id", ""))
		if route_id.is_empty():
			continue
		var points := _static_points(route_id)
		# 没有作者明确绘制的折线就不显示道路，也不允许旅行标记，
		# 不退回节点间直线；这样漏配路线会在编辑器里显眼暴露出来。
		if points.size() < 2:
			continue
		route_points[route_id] = points
		route_unlocked[route_id] = bool(road.get("unlocked", true))
		_sync_static_line(route_id, points)
	_refresh_styles()
	_audit_route_node_links()
	_audit_route_intersections()
	queue_redraw()

func set_node_positions(values: Dictionary) -> void:
	## 运行时以场景中的可编辑节点位置为端点，避免移动节点后道路脱节。
	node_positions = values.duplicate(true)
	if map_data == null:
		return
	for road in map_data.get_roads():
		var route_id := str(road.get("route_id", ""))
		if route_id.is_empty() or not route_points.has(route_id):
			continue
		var points: PackedVector2Array = route_points[route_id]
		var from_id := str(road.get("from", ""))
		var to_id := str(road.get("to", ""))
		if node_positions.has(from_id):
			points[0] = node_positions[from_id]
		if node_positions.has(to_id):
			points[points.size() - 1] = node_positions[to_id]
		route_points[route_id] = points
		_sync_static_line(route_id, points)
	_audit_route_intersections()
	queue_redraw()

func set_selected_route(route_id: String) -> void:
	selected_route_id = route_id
	_refresh_styles()
	queue_redraw()

func clear_selected_route() -> void:
	set_selected_route("")

func route_at(map_position: Vector2, max_distance: float = 24.0) -> String:
	var nearest_id := ""
	var nearest_distance := max_distance
	for route_id in route_points:
		var points: PackedVector2Array = route_points[route_id]
		for index in range(points.size() - 1):
			var distance := _distance_to_segment(map_position, points[index], points[index + 1])
			if distance < nearest_distance:
				nearest_distance = distance
				nearest_id = str(route_id)
	return nearest_id

func points_between(from_id: String, to_id: String) -> PackedVector2Array:
	if map_data == null:
		return PackedVector2Array()
	for road in map_data.get_roads():
		var road_from := str(road.get("from", ""))
		var road_to := str(road.get("to", ""))
		var route_id := str(road.get("route_id", ""))
		if route_id.is_empty() or not route_points.has(route_id):
			continue
		if road_from == from_id and road_to == to_id:
			return route_points[route_id]
		if road_from == to_id and road_to == from_id:
			return _reversed(route_points[route_id])
	return PackedVector2Array()

func _static_points(route_id: String) -> PackedVector2Array:
	var ink := get_node_or_null(route_id + "_Ink") as Line2D
	if ink != null and ink.points.size() >= 2:
		return ink.points
	var base := get_node_or_null(route_id + "_Base") as Line2D
	return base.points if base != null else PackedVector2Array()

func _reversed(points: PackedVector2Array) -> PackedVector2Array:
	var result := PackedVector2Array()
	for index in range(points.size() - 1, -1, -1):
		result.append(points[index])
	return result

func _sync_static_line(route_id: String, points: PackedVector2Array) -> void:
	var base := get_node_or_null(route_id + "_Base") as Line2D
	var ink := get_node_or_null(route_id + "_Ink") as Line2D
	var display_points := _display_points_for_route(route_id, points)
	if base != null:
		base.points = display_points
		base.visible = true
	if ink != null:
		ink.points = display_points
		ink.visible = true

func _display_points_for_route(route_id: String, points: PackedVector2Array) -> PackedVector2Array:
	# Keep exact node-to-node points for travel and hit-testing, but stop the
	# painted road at each circle's edge so it never runs through a marker.
	if points.size() < 2:
		return points
	var from_id := ""
	var to_id := ""
	if map_data != null:
		for road in map_data.get_roads():
			if str(road.get("route_id", "")) == route_id:
				from_id = str(road.get("from", ""))
				to_id = str(road.get("to", ""))
				break
	var display := PackedVector2Array()
	for point in points:
		display.append(point)
	var from_clearance := _node_clearance(from_id)
	var to_clearance := _node_clearance(to_id)
	if map_data == null and route_id.ends_with("_core"):
		# The standalone editor preview has no route resource to identify which
		# endpoint is the faction core, so use the larger radius on both ends.
		from_clearance = CORE_NODE_CLEARANCE
		to_clearance = CORE_NODE_CLEARANCE
	display[0] = _trim_endpoint(display[0], display[1], from_clearance)
	display[display.size() - 1] = _trim_endpoint(display[display.size() - 1], display[display.size() - 2], to_clearance)
	return display

func _node_clearance(node_id: String) -> float:
	if node_id.ends_with("_core"):
		return CORE_NODE_CLEARANCE
	if node_id.ends_with("_wilds") or node_id == "misty_ridge":
		return WILDERNESS_NODE_CLEARANCE
	return LOCATION_NODE_CLEARANCE

func _trim_endpoint(position: Vector2, neighbor: Vector2, clearance: float) -> Vector2:
	var delta := neighbor - position
	var length := delta.length()
	if length <= 0.001:
		return position
	# Leave at least a short visible segment on very short routes such as the
	# final path into a first-level faction core.
	return position + delta * minf(clearance / length, 0.42)

func _refresh_styles() -> void:
	for child in get_children():
		if not child is Line2D:
			continue
		var line := child as Line2D
		var route_id := line.name.trim_suffix("_Base").trim_suffix("_Ink")
		var unlocked := bool(route_unlocked.get(route_id, true))
		var selected := route_id == selected_route_id
		if line.name.ends_with("_Base"):
			line.width = 9.0 if selected else 8.0
			line.default_color = Color(0.69, 0.72, 0.69, 0.30 if unlocked else 0.14)
		else:
			line.width = 5.5 if selected else 4.5
			line.default_color = Color(0.82, 0.84, 0.80, 0.90 if unlocked else 0.42)

func _audit_route_node_links() -> void:
	if map_data == null or not map_data.has_method("get_all_nodes"):
		return
	var degree := {}
	for node in map_data.get_all_nodes():
		degree[str(node.get("node_id"))] = 0
	for road in map_data.get_roads():
		var route_id := str(road.get("route_id", ""))
		if route_id.is_empty() or not route_points.has(route_id):
			continue
		var from_id := str(road.get("from", ""))
		var to_id := str(road.get("to", ""))
		if degree.has(from_id):
			degree[from_id] += 1
		if degree.has(to_id):
			degree[to_id] += 1
	var disconnected: Array[String] = []
	for node_id in degree:
		if int(degree[node_id]) <= 0:
			disconnected.append(str(node_id))
	if disconnected.is_empty():
		print("bai_chuan_node_links_ok nodes=%d routes=%d" % [degree.size(), route_points.size()])
	else:
		push_error("百川存在未接入道路的节点：" + "、".join(disconnected))

func _audit_route_intersections() -> void:
	if route_points.size() < 2:
		return
	var route_ids := route_points.keys()
	var issues: Array[String] = []
	for first_index in range(route_ids.size()):
		var first_id := str(route_ids[first_index])
		var first_points: PackedVector2Array = route_points[first_id]
		for second_index in range(first_index + 1, route_ids.size()):
			var second_id := str(route_ids[second_index])
			var second_points: PackedVector2Array = route_points[second_id]
			for first_segment in range(first_points.size() - 1):
				var first_start := first_points[first_segment]
				var first_end := first_points[first_segment + 1]
				for second_segment in range(second_points.size() - 1):
					var second_start := second_points[second_segment]
					var second_end := second_points[second_segment + 1]
					if not _segments_intersect(first_start, first_end, second_start, second_end):
						continue
					if not _shares_endpoint(first_start, first_end, second_start, second_end):
						issues.append(first_id + "×" + second_id)
						break
				if not issues.is_empty() and issues.back() == first_id + "×" + second_id:
					break
			if not issues.is_empty() and issues.back() == first_id + "×" + second_id:
				break
	if issues.is_empty():
		print("bai_chuan_route_geometry_ok routes=%d" % route_points.size())
	else:
		push_error("百川道路存在非节点交叉：" + "、".join(issues))

func _shares_endpoint(first_start: Vector2, first_end: Vector2, second_start: Vector2, second_end: Vector2) -> bool:
	const ENDPOINT_EPSILON := 2.0
	return first_start.distance_to(second_start) <= ENDPOINT_EPSILON \
		or first_start.distance_to(second_end) <= ENDPOINT_EPSILON \
		or first_end.distance_to(second_start) <= ENDPOINT_EPSILON \
		or first_end.distance_to(second_end) <= ENDPOINT_EPSILON

func _segments_intersect(first_start: Vector2, first_end: Vector2, second_start: Vector2, second_end: Vector2) -> bool:
	var first_a := _cross(first_end - first_start, second_start - first_start)
	var first_b := _cross(first_end - first_start, second_end - first_start)
	var second_a := _cross(second_end - second_start, first_start - second_start)
	var second_b := _cross(second_end - second_start, first_end - second_start)
	if ((first_a > 0.001 and first_b < -0.001) or (first_a < -0.001 and first_b > 0.001)) \
		and ((second_a > 0.001 and second_b < -0.001) or (second_a < -0.001 and second_b > 0.001)):
		return true
	return (absf(first_a) <= 0.001 and _point_on_segment(first_start, first_end, second_start)) \
		or (absf(first_b) <= 0.001 and _point_on_segment(first_start, first_end, second_end)) \
		or (absf(second_a) <= 0.001 and _point_on_segment(second_start, second_end, first_start)) \
		or (absf(second_b) <= 0.001 and _point_on_segment(second_start, second_end, first_end))

func _point_on_segment(start: Vector2, finish: Vector2, point: Vector2) -> bool:
	return point.x >= minf(start.x, finish.x) - 0.001 and point.x <= maxf(start.x, finish.x) + 0.001 \
		and point.y >= minf(start.y, finish.y) - 0.001 and point.y <= maxf(start.y, finish.y) + 0.001

func _cross(first: Vector2, second: Vector2) -> float:
	return first.x * second.y - first.y * second.x

func _distance_to_segment(point: Vector2, start: Vector2, finish: Vector2) -> float:
	var delta := finish - start
	var length_squared := delta.length_squared()
	if length_squared <= 0.001:
		return point.distance_to(start)
	var factor := clampf((point - start).dot(delta) / length_squared, 0.0, 1.0)
	return point.distance_to(start + delta * factor)
