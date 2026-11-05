extends RefCounted
## Native paper-and-ink silhouettes for the independent round-hand pawn sample.
##
## Geometry is authored in an unprojected XZ plane, stored as Vector2(x, z).
## Widths are full ground-plane widths. Build the outline first, then call
## project_outline(): rotation precedes the fixed 0.42 depth compression.
## origin is already a screen position; height is an uncompressed screen lift.
## Returned outlines have no repeated closing vertex and can be passed directly
## to CanvasItem.draw_colored_polygon() or Polygon2D.polygon.
## The helper owns no clocks, random state, textures, nodes or combat rules.
##
## Example (inside a Node2D's _draw):
##   const Shapes = preload("pawn_paper_vfx_shapes.gd")
##   var path = Shapes.quadratic_bezier(Vector2(-22, 0), Vector2(4, -16), Vector2(42, 4))
##   var stroke = Shapes.stroke_strip(path, 12.0, 0.25, 0.04, 0.7)
##   draw_colored_polygon(Shapes.project_outline(stroke, aim_angle, hand_origin, 8.0), ink)
##   var front = Shapes.tapered_wave_front(64.0, 18.0)
##   draw_colored_polygon(Shapes.project_outline(front, aim_angle, release_origin), wash)

const VIEW_DEPTH := 0.42
const _EPSILON := 0.00001
const _CAP_STEPS := 6


## Uniform parameter samples, including both exact endpoints. steps is clamped
## to 2..160; no time-dependent perturbation is added to a sampled curve.
static func quadratic_bezier(p0: Vector2, p1: Vector2, p2: Vector2, steps: int = 24) -> PackedVector2Array:
	var result := PackedVector2Array()
	var count := clampi(steps, 2, 160)
	for index in range(count + 1):
		var t := float(index) / float(count)
		var u := 1.0 - t
		result.append(p0 * (u * u) + p1 * (2.0 * u * t) + p2 * (t * t))
	return result


## Uniform cubic samples, including both exact endpoints. Control points remain
## in the ground plane until the completed stroke is projected.
static func cubic_bezier(p0: Vector2, p1: Vector2, p2: Vector2, p3: Vector2, steps: int = 32) -> PackedVector2Array:
	var result := PackedVector2Array()
	var count := clampi(steps, 2, 160)
	for index in range(count + 1):
		var t := float(index) / float(count)
		var u := 1.0 - t
		result.append(p0 * (u * u * u) + p1 * (3.0 * u * u * t) + p2 * (3.0 * u * t * t) + p3 * (t * t * t))
	return result


## Fillable brush strip around a sampled centerline. start_scale/end_scale are
## width multipliers; belly adds one broad sin(PI * distance_fraction) swell.
## Taper follows distance, so uneven Bezier parameter spacing does not bunch it.
## Smooth joins use bounded miters; optional caps are rounded six-step arcs.
## Zero width or a path without two distinct points returns an empty outline.
static func stroke_strip(path: PackedVector2Array, width: float, start_scale: float = 0.18, end_scale: float = 0.04, belly: float = 0.25, round_caps: bool = true) -> PackedVector2Array:
	var result := PackedVector2Array()
	if width <= _EPSILON:
		return result
	var points := _clean_path(path)
	if points.size() < 2:
		return result
	var distances := PackedFloat32Array([0.0])
	var total := 0.0
	for index in range(1, points.size()):
		total += points[index].distance_to(points[index - 1])
		distances.append(total)
	if total <= _EPSILON:
		return result
	var first_scale := clampf(start_scale, 0.0, 2.0)
	var last_scale := clampf(end_scale, 0.0, 2.0)
	var swell := clampf(belly, 0.0, 2.0)
	var left := PackedVector2Array()
	var right := PackedVector2Array()
	var half_widths := PackedFloat32Array()
	for index in range(points.size()):
		var t := float(distances[index]) / total
		var profile := lerpf(first_scale, last_scale, t) + sin(PI * t) * swell
		var half_width := maxf(0.0, width * 0.5 * profile)
		var offset := _stroke_offset(points, index, half_width)
		left.append(points[index] + offset)
		right.append(points[index] - offset)
		half_widths.append(half_width)
	for point in left:
		_append_unique(result, point)
	var last := points.size() - 1
	if round_caps and half_widths[last] > _EPSILON:
		var tangent := (points[last] - points[last - 1]).normalized()
		_append_cap(result, points[last], tangent.angle() + PI * 0.5, half_widths[last])
	for index in range(last, -1, -1):
		_append_unique(result, right[index])
	if round_caps and half_widths[0] > _EPSILON:
		var tangent := (points[1] - points[0]).normalized()
		_append_cap(result, points[0], tangent.angle() - PI * 0.5, half_widths[0])
	_remove_closing_duplicate(result)
	return result


## Filled rounded cloud puff, not an annular ring. radii are ellipse semiaxes.
## Three to seven soft lobes and a small broad asymmetry keep the silhouette
## calm. phase is a static shape variant; do not advance it each frame.
static func cloud_outline(center: Vector2, radii: Vector2, lobes: int = 5, bulge: float = 0.14, phase: float = 0.0, steps: int = 64) -> PackedVector2Array:
	var result := PackedVector2Array()
	if radii.x <= _EPSILON or radii.y <= _EPSILON:
		return result
	var lobe_count := clampi(lobes, 3, 7)
	var strength := clampf(bulge, 0.0, 0.20)
	var count := clampi(maxi(steps, lobe_count * 10), 30, 160)
	for index in range(count):
		var angle := TAU * float(index) / float(count)
		# Low-frequency radial curves make round cloud cells, without noisy teeth.
		var radial := 1.0 + cos(angle * float(lobe_count) + phase) * strength
		radial += sin(angle * 2.0 + phase * 0.5) * strength * 0.20
		result.append(center + Vector2(cos(angle) * radii.x, sin(angle) * radii.y) * radial)
	return result


## Open, tapered crescent facing local +X. radius is the centerline radius;
## width is its maximum middle thickness. Span is radians, limited to 225
## degrees so this helper cannot produce a closed circular glyph.
static func open_crescent(radius: float, width: float, span_radians: float = 2.1, steps: int = 48, tip_scale: float = 0.02) -> PackedVector2Array:
	if radius <= _EPSILON or width <= _EPSILON:
		return PackedVector2Array()
	var span := clampf(absf(span_radians), 0.12, PI * 1.25)
	var count := clampi(steps, 8, 160)
	var path := PackedVector2Array()
	for index in range(count + 1):
		var t := float(index) / float(count)
		path.append(Vector2.from_angle(lerpf(-span * 0.5, span * 0.5, t)) * radius)
	var tip := clampf(tip_scale, 0.0, 0.35)
	return stroke_strip(path, minf(width, radius * 1.30), tip, tip, 1.0 - tip, false)


## Open fan-shaped wave front facing local +X. The broad middle tapers into
## two clean tips. A fixed three-cell crest adds a hand-painted swell instead
## of high-frequency edge noise. phase selects a stable variant, not a clock.
## radius is the centerline radius; the outer edge includes half the width.
static func tapered_wave_front(radius: float, width: float, span_radians: float = 2.094395, steps: int = 56, phase: float = 0.0) -> PackedVector2Array:
	if radius <= _EPSILON or width <= _EPSILON:
		return PackedVector2Array()
	var span := clampf(absf(span_radians), 0.20, PI * 1.25)
	var count := clampi(steps, 12, 160)
	var brush_width := minf(width, radius * 0.80)
	var outer := PackedVector2Array()
	var inner := PackedVector2Array()
	var swell := minf(brush_width * 0.10, radius * 0.045)
	for index in range(count + 1):
		var t := float(index) / float(count)
		var envelope := sin(PI * t)
		var radial := radius + swell * envelope * cos((t - 0.5) * TAU * 1.5 + phase)
		var direction := Vector2.from_angle(lerpf(-span * 0.5, span * 0.5, t))
		var half_width := brush_width * 0.5 * envelope
		# Polar bands retain angular order even for a short, very broad front;
		# normal offsets on a wavy short arc can otherwise fold over themselves.
		outer.append(direction * (radial + half_width))
		inner.append(direction * (radial - half_width))
	var result := PackedVector2Array()
	for point in outer:
		_append_unique(result, point)
	for index in range(count, -1, -1):
		_append_unique(result, inner[index])
	_remove_closing_duplicate(result)
	return result


## Rotate local XZ first, flatten only global depth, then lift in screen Y.
## Never rotate the returned screen outline again to aim it.
static func project_ground(point: Vector2, angle_radians: float = 0.0, origin: Vector2 = Vector2.ZERO, height: float = 0.0) -> Vector2:
	var ground := point.rotated(angle_radians)
	return origin + Vector2(ground.x, ground.y * VIEW_DEPTH - height)


static func project_outline(points: PackedVector2Array, angle_radians: float = 0.0, origin: Vector2 = Vector2.ZERO, height: float = 0.0) -> PackedVector2Array:
	var result := PackedVector2Array()
	for point in points:
		result.append(project_ground(point, angle_radians, origin, height))
	return result


## Inverse mapping for a mouse point or presentation-only geometry inspection.
static func unproject_ground(point: Vector2, angle_radians: float = 0.0, origin: Vector2 = Vector2.ZERO, height: float = 0.0) -> Vector2:
	var screen := point - origin + Vector2(0.0, height)
	return Vector2(screen.x, screen.y / VIEW_DEPTH).rotated(-angle_radians)


## Fill-only half-plane clip, after rotating into world XZ and before projection.
## front keeps world z >= 0 (Vector2.y); rear keeps z <= 0. The resulting closed
## polygon may contain a new boundary edge on z = 0. Do not draw its outline:
## use outline_depth_segments() for the source silhouette's visible border.
static func clip_depth(points: PackedVector2Array, front: bool) -> PackedVector2Array:
	var source := _depth_source(points)
	var result := PackedVector2Array()
	if source.size() < 3:
		return result
	var previous := source[source.size() - 1]
	var previous_inside := _depth_inside(previous, front)
	for current in source:
		var current_inside := _depth_inside(current, front)
		if current_inside != previous_inside:
			_append_unique(result, _depth_intersection(previous, current))
		if current_inside:
			_append_unique(result, current)
		previous = current
		previous_inside = current_inside
	_remove_closing_duplicate(result)
	if result.size() < 3:
		return PackedVector2Array()
	var area_twice := 0.0
	for index in range(result.size()):
		area_twice += result[index].cross(result[(index + 1) % result.size()])
	return result if absf(area_twice) > _EPSILON else PackedVector2Array()


## Clip each original closed-outline edge independently. Each returned item is
## exactly two points on one source edge; no artificial z = 0 closing line is
## added. Project these segments with angle 0 after applying world rotation.
static func outline_depth_segments(points: PackedVector2Array, front: bool) -> Array[PackedVector2Array]:
	var source := _depth_source(points)
	var result: Array[PackedVector2Array] = []
	if source.size() < 3:
		return result
	for index in range(source.size()):
		var first := source[index]
		var second := source[(index + 1) % source.size()]
		var first_inside := _depth_inside(first, front)
		var second_inside := _depth_inside(second, front)
		if not first_inside and not second_inside:
			continue
		if first_inside != second_inside:
			var crossing := _depth_intersection(first, second)
			if first_inside:
				second = crossing
			else:
				first = crossing
		if first.distance_squared_to(second) > _EPSILON * _EPSILON:
			result.append(PackedVector2Array([first, second]))
	return result


static func _depth_source(points: PackedVector2Array) -> PackedVector2Array:
	var result := PackedVector2Array()
	for point in points:
		# Cardinal rotations can leave an intended boundary at floating epsilon.
		var snapped := Vector2(point.x, 0.0) if absf(point.y) <= _EPSILON else point
		_append_unique(result, snapped)
	_remove_closing_duplicate(result)
	return result


static func _depth_inside(point: Vector2, front: bool) -> bool:
	return point.y >= 0.0 if front else point.y <= 0.0


static func _depth_intersection(first: Vector2, second: Vector2) -> Vector2:
	var t := clampf(-first.y / (second.y - first.y), 0.0, 1.0)
	return Vector2(lerpf(first.x, second.x, t), 0.0)


static func _clean_path(path: PackedVector2Array) -> PackedVector2Array:
	var result := PackedVector2Array()
	for point in path:
		_append_unique(result, point)
	return result


static func _stroke_offset(points: PackedVector2Array, index: int, half_width: float) -> Vector2:
	if index == 0:
		return _left_normal(points[1] - points[0]) * half_width
	if index == points.size() - 1:
		return _left_normal(points[index] - points[index - 1]) * half_width
	var before := _left_normal(points[index] - points[index - 1])
	var after := _left_normal(points[index + 1] - points[index])
	var bisector := before + after
	if bisector.length_squared() <= _EPSILON:
		return after * half_width
	bisector = bisector.normalized()
	var denominator := maxf(0.625, absf(bisector.dot(after)))
	return bisector * (half_width / denominator)


static func _left_normal(tangent: Vector2) -> Vector2:
	var direction := tangent.normalized()
	return Vector2(-direction.y, direction.x)


static func _append_cap(outline: PackedVector2Array, center: Vector2, start_angle: float, radius: float) -> void:
	for index in range(1, _CAP_STEPS + 1):
		var angle := start_angle - PI * float(index) / float(_CAP_STEPS)
		_append_unique(outline, center + Vector2.from_angle(angle) * radius)


static func _append_unique(points: PackedVector2Array, point: Vector2) -> void:
	if points.is_empty() or points[points.size() - 1].distance_squared_to(point) > _EPSILON * _EPSILON:
		points.append(point)


static func _remove_closing_duplicate(points: PackedVector2Array) -> void:
	if points.size() > 1 and points[0].distance_squared_to(points[points.size() - 1]) <= _EPSILON * _EPSILON:
		points.resize(points.size() - 1)
