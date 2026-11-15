extends RefCounted
## One converter belongs to one map. Authored logical Z remains the ground
## foot's world Y; only the short-lived CanvasItem z_index is view-dependent.
## Pass the same instance to every ground prop and actor in that map, and place
## visible items again after set_view when the camera or zoom changes.

const MIN_RENDER_DEPTH := 128
const MAX_RENDER_DEPTH := 3880
const TOP_MARGIN_SCREEN := 64.0
const MAX_INDICES_PER_SCREEN_PIXEL := 2.0
const LOGICAL_Z_META := &"logical_z"

var _camera_y := 0.0
var _zoom := 1.0
var _viewport_height := 1080.0
var _max_prop_height := 0.0
var _screen_span := 1144.0
var _indices_per_screen_pixel := 2.0

## camera_y is the viewport's top world Y, viewport_height is in screen pixels,
## and max_prop_height is the tallest currently drawable ground prop in world
## units. A prop whose foot is below the viewport can still cover its lower
## edge, so its full height is included in the sortable interval.
## Invalid input preserves the previous view and returns false.
func set_view(camera_y: float, zoom: float, viewport_height: float, max_prop_height: float) -> bool:
	if not is_finite(camera_y) or not is_finite(zoom) or not is_finite(viewport_height) or not is_finite(max_prop_height): return false
	if zoom <= 0.0 or viewport_height <= 0.0 or max_prop_height < 0.0: return false
	var screen_span := TOP_MARGIN_SCREEN + viewport_height + max_prop_height * zoom
	if not is_finite(screen_span): return false
	_camera_y = camera_y
	_zoom = zoom
	_viewport_height = viewport_height
	_max_prop_height = max_prop_height
	_screen_span = screen_span
	_indices_per_screen_pixel = minf(MAX_INDICES_PER_SCREEN_PIXEL, float(MAX_RENDER_DEPTH - MIN_RENDER_DEPTH) / screen_span)
	return true

## The mapping is monotonic across all potentially visible feet. The usual
## two indices per screen pixel distinguish feet >= 0.5 screen pixels apart.
## For unusually tall views/props, the finite render range lowers the factor;
## get_view_summary reports the resulting minimum guaranteed separation.
## Only feet outside the configured interval may be clipped to an end depth.
func render_depth(logical_z: float) -> int:
	if not is_finite(logical_z): return MIN_RENDER_DEPTH
	var screen_y := (logical_z - _camera_y) * _zoom
	var index_offset := clampf((screen_y + TOP_MARGIN_SCREEN) * _indices_per_screen_pixel, 0.0, float(MAX_RENDER_DEPTH - MIN_RENDER_DEPTH))
	return MIN_RENDER_DEPTH + floori(index_offset)

## Keep the authored world-space foot separately for inspection/reprojection.
## This changes no X/Y coordinate, scale, height, or gameplay state.
func place(item: CanvasItem, logical_z: float) -> void:
	if not is_instance_valid(item) or not is_finite(logical_z): return
	item.set_meta(LOGICAL_Z_META, logical_z)
	item.z_as_relative = false
	item.z_index = render_depth(logical_z)

func get_view_summary() -> Dictionary:
	return {
		"camera_y": _camera_y,
		"zoom": _zoom,
		"viewport_height": _viewport_height,
		"max_prop_height": _max_prop_height,
		"visible_foot_min": _camera_y - TOP_MARGIN_SCREEN / _zoom,
		"visible_foot_max": _camera_y + _viewport_height / _zoom + _max_prop_height,
		"screen_span": _screen_span,
		"indices_per_screen_pixel": _indices_per_screen_pixel,
		"minimum_distinct_screen_separation": 1.0 / _indices_per_screen_pixel,
	}
