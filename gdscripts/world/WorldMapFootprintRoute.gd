extends Node2D

const TILE_SIZE := 256.0
const FOOTPRINT_DISPLAY_SIZE := Vector2(28.0, 58.0)
const FOOTPRINT_STEP_SPACING := TILE_SIZE * 0.5
const FOOTPRINT_MIN_SCREEN_STEP_SPACING := FOOTPRINT_DISPLAY_SIZE.y * 1.35
const FOOTPRINT_START_OFFSET := TILE_SIZE * 0.25
const FOOTPRINT_LATERAL_OFFSET_SCREEN := 12.0

var _terrain_layer: TileMapLayer
var _footprint_texture: Texture2D
var _route_cells: Array[Vector2i] = []
var _display_zoom := 1.0

func configure(terrain_layer: TileMapLayer, source_texture: Texture2D) -> void:
	_terrain_layer = terrain_layer
	var footprint_image := source_texture.get_image()
	var footprint_bounds := footprint_image.get_used_rect()
	if footprint_bounds.size.x > 0 and footprint_bounds.size.y > 0:
		footprint_image = footprint_image.get_region(footprint_bounds)
	footprint_image.resize(int(FOOTPRINT_DISPLAY_SIZE.x), int(FOOTPRINT_DISPLAY_SIZE.y), Image.INTERPOLATE_LANCZOS)
	_footprint_texture = ImageTexture.create_from_image(footprint_image)
	position = _terrain_layer.position
	queue_redraw()

func set_route(cells: Array[Vector2i], zoom: float) -> void:
	_route_cells = cells.duplicate()
	_display_zoom = maxf(zoom, 0.01)
	queue_redraw()

func set_display_zoom(zoom: float) -> void:
	_display_zoom = maxf(zoom, 0.01)
	queue_redraw()

func _draw() -> void:
	if _terrain_layer == null or _footprint_texture == null or _route_cells.size() < 2:
		return

	var route_centers: Array[Vector2] = []
	var cumulative_lengths := PackedFloat32Array()
	var route_length := 0.0
	for cell in _route_cells:
		var center := _terrain_layer.map_to_local(cell)
		if not route_centers.is_empty():
			route_length += route_centers[route_centers.size() - 1].distance_to(center)
		route_centers.append(center)
		cumulative_lengths.append(route_length)
	if route_length <= 0.01:
		return

	# Counter-scale against the map canvas so each print keeps the same screen size at every zoom.
	var footprint_scale := 1.0 / _display_zoom
	var step_spacing := maxf(FOOTPRINT_STEP_SPACING, FOOTPRINT_MIN_SCREEN_STEP_SPACING / _display_zoom)
	var lateral_offset := FOOTPRINT_LATERAL_OFFSET_SCREEN / _display_zoom
	var footprint_rect := Rect2(-FOOTPRINT_DISPLAY_SIZE * 0.5, FOOTPRINT_DISPLAY_SIZE)
	var step_distance := FOOTPRINT_START_OFFSET
	var step_index := 0
	var segment_index := 1
	while step_distance < route_length:
		while segment_index < route_centers.size() - 1 and step_distance > cumulative_lengths[segment_index]:
			segment_index += 1
		var segment_start_distance := cumulative_lengths[segment_index - 1]
		var segment_end_distance := cumulative_lengths[segment_index]
		var segment_length := segment_end_distance - segment_start_distance
		if segment_length > 0.01:
			var segment_start := route_centers[segment_index - 1]
			var segment_end := route_centers[segment_index]
			var direction := (segment_end - segment_start).normalized()
			var progress := clampf((step_distance - segment_start_distance) / segment_length, 0.0, 1.0)
			var side_sign := 1.0 if step_index % 2 == 0 else -1.0
			var lateral := Vector2(-direction.y, direction.x) * side_sign
			var step_position := segment_start.lerp(segment_end, progress) + lateral * lateral_offset
			draw_set_transform(step_position, direction.angle() + PI * 0.5, Vector2.ONE * footprint_scale)
			draw_texture_rect(_footprint_texture, footprint_rect, false, Color.WHITE)
			draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
		step_distance += step_spacing
		step_index += 1
