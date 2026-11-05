extends RefCounted

## Shared framing for every alive participant. Presentation only.
const MAX_ZOOM := 1.35
const ZOOM_DEADBAND := 0.025
const POSITION_DEADBAND := 5.0
var initialized := false
var center := Vector2.ZERO
var zoom := MAX_ZOOM
var last_target := {}

func reset() -> void:
	initialized = false

func solve(fallback_anchor: Vector2, records: Array, viewport: Vector2, world: Vector2) -> Dictionary:
	var resolution_scale := maxf(0.2, minf(viewport.x / 1280.0, viewport.y / 720.0))
	var bounds := Rect2()
	var count := 0
	for record in records:
		if not record is Dictionary or not record.get("alive", true): continue
		var point: Vector2 = record.get("position", Vector2.INF)
		if not point.is_finite(): continue
		var head := clampf(float(record.get("head_height", 100.0)), 20.0, 250.0)
		var unit_bounds := Rect2(point - Vector2(40.0, head), Vector2(80.0, head + 22.0))
		bounds = unit_bounds if count==0 else bounds.merge(unit_bounds)
		count += 1
	if count==0:
		bounds = Rect2(fallback_anchor-Vector2(40.0,130.0),Vector2(80.0,152.0))
	# Screen-space HUD margins do not change combat reach or movement limits.
	var safe := Rect2(Vector2(viewport.x*0.075,viewport.y*0.20),Vector2(viewport.x*0.85,viewport.y*0.66))
	bounds = bounds.grow(40.0)
	var fit := minf(safe.size.x/maxf(bounds.size.x,1.0),safe.size.y/maxf(bounds.size.y,1.0))
	# Map containment has priority when the map/aspect cannot fit every margin.
	var map_min := minimum_map_zoom(viewport,world)
	var fitted_zoom := maxf(map_min,minf(fit,MAX_ZOOM*resolution_scale))
	var camera_center := bounds.get_center()-(safe.get_center()-viewport*0.5)/fitted_zoom
	camera_center=clamp_center(camera_center,fitted_zoom,viewport,world)
	var saturated := fitted_zoom>fit+0.0001 or fitted_zoom>safe_zoom_at_center(bounds,camera_center,viewport,safe)+0.0001
	return {"center":camera_center,"zoom":fitted_zoom,"unit_count":count,"bounds":bounds,"safe_rect":safe,"viewport":viewport,"resolution_scale":resolution_scale,"map_min_zoom":map_min,"framing_saturated":saturated}

func minimum_map_zoom(viewport: Vector2,world: Vector2) -> float:
	return maxf(viewport.x/maxf(1.0,world.x),viewport.y/maxf(1.0,world.y))

func clamp_center(point: Vector2,value: float,viewport: Vector2,world: Vector2) -> Vector2:
	var half_view := viewport/(2.0*maxf(value,0.0001))
	return Vector2(clampf(point.x,minf(half_view.x,world.x*0.5),maxf(world.x-half_view.x,world.x*0.5)),clampf(point.y,minf(half_view.y,world.y*0.5),maxf(world.y-half_view.y,world.y*0.5)))

func safe_zoom_at_center(bounds: Rect2, point: Vector2, viewport: Vector2, safe: Rect2) -> float:
	var limit := INF
	var left_distance := point.x-bounds.position.x
	var right_distance := bounds.end.x-point.x
	var top_distance := point.y-bounds.position.y
	var bottom_distance := bounds.end.y-point.y
	if left_distance>0.0: limit=minf(limit,(viewport.x*0.5-safe.position.x)/left_distance)
	if right_distance>0.0: limit=minf(limit,(safe.end.x-viewport.x*0.5)/right_distance)
	if top_distance>0.0: limit=minf(limit,(viewport.y*0.5-safe.position.y)/top_distance)
	if bottom_distance>0.0: limit=minf(limit,(safe.end.y-viewport.y*0.5)/bottom_distance)
	return maxf(0.0001,limit)

func update(anchor: Vector2, records: Array, viewport: Vector2, world: Vector2, delta: float, snap := false) -> Dictionary:
	last_target = solve(anchor,records,viewport,world)
	var target_center: Vector2 = last_target.center
	var target_zoom: float = last_target.zoom
	if not initialized or snap:
		center=target_center
		zoom=target_zoom
		initialized=true
	elif delta>0.0:
		var step:=minf(delta,0.05)
		if center.distance_to(target_center)>POSITION_DEADBAND/maxf(zoom,0.01):
			center=center.lerp(target_center,1.0-exp(-7.0*step))
		if absf(target_zoom-zoom)>zoom*ZOOM_DEADBAND:
			zoom=lerpf(zoom,target_zoom,1.0-exp(-(7.0 if target_zoom<zoom else 1.7)*step))
		# Newly separated units must remain visible during camera interpolation.
		zoom=minf(zoom,safe_zoom_at_center(last_target.bounds,center,viewport,last_target.safe_rect))
	# This geometric safety also runs during pause/resizing, without advancing easing.
	zoom=maxf(zoom,minimum_map_zoom(viewport,world))
	center=clamp_center(center,zoom,viewport,world)
	var saturated := zoom>safe_zoom_at_center(last_target.bounds,center,viewport,last_target.safe_rect)+0.0001
	return {"center":center,"zoom":zoom,"target":last_target,"framing_saturated":saturated}
