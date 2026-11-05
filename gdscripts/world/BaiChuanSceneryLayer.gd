extends Node2D
## Independent presentation only: no input, terrain, travel, or save state.
## Add below the map's world, then call setup(map_owner) after _build_places().

const WorldScale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const DATA_PATH := "res://resources/world/bai_chuan/scenery_ink_20261104.json"
const CATALOG_PATH := "res://textures/world/bai_chuan/scenery_ink_20261104/catalog.json"
const CAMERA_REFRESH_SECONDS := 0.12

var audit: Dictionary = {}
var _map_owner: Control
var _textures: Dictionary = {}
var _entries: Array[Dictionary] = []
var _roads: Array[Dictionary] = []
var _avoid_rects: Array[Rect2] = []
var _footings: FootingWash
var _elapsed := 0.0
var _last_view := Rect2()
var _last_zoom := -1.0
var _initialized := false


class FootingWash extends Node2D:

	var rows: Array[Dictionary] = []

	func _draw() -> void:
		# Four very faint, short washes produce a contact shadow. There is no
		# bright plate, halo, outline, or large circular backing under a place.
		for row: Dictionary in rows:
			var at := Vector2(float(row.position[0]), float(row.position[1]))
			var width := float(row.width)
			var height := float(row.get("height", 6.5))
			var opacity := float(row.get("opacity", 0.055))
			for ring: int in range(4):
				var amount := 1.0 - float(ring) * 0.16
				draw_set_transform(at + Vector2(3, -1), 0.0, Vector2(width * 0.5 * amount, height * 0.5 * amount))
				draw_circle(Vector2.ZERO, 1.0, Color(0.28, 0.28, 0.22, opacity * 0.28))
			draw_set_transform(Vector2.ZERO)


func setup(map_owner: Control) -> void:
	if _initialized:
		return
	_map_owner = map_owner
	name = "BaiChuanSceneryLayer"
	z_index = 1
	y_sort_enabled = false
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	set_meta("visual_only", true)
	set_meta("changes_navigation", false)
	set_process_input(false)
	set_process_unhandled_input(false)
	set_process_unhandled_key_input(false)
	audit = {"visual_only": true, "interactive_nodes": 0, "navigation_mutations": 0,
		"placements_requested": 0, "placements_created": 0, "placements_avoided": 0,
		"roads_created": 0, "bridges_created": 0, "footings_created": 0,
		"missing_assets": [], "texture_count": 0, "visible_scenery": 0}
	var source: Variant = _read_dictionary(DATA_PATH)
	var catalog: Dictionary = _read_dictionary(CATALOG_PATH)
	if source.is_empty() or catalog.is_empty():
		audit["unavailable_data"] = true
		set_process(false)
		return
	var data: Dictionary = source
	if int(data.get("version", 0)) != 1 or not bool(data.get("visual_only", false)):
		audit["invalid_data"] = true
		set_process(false)
		return
	var bounds: Array = data.get("bounds", [])
	if bounds.size() != 4 or not is_equal_approx(float(bounds[2]), WorldScale.MAP_PIXELS) or not is_equal_approx(float(bounds[3]), WorldScale.MAP_PIXELS):
		audit["invalid_bounds"] = true
		set_process(false)
		return
	var sprites: Dictionary = catalog.get("sprites", {})
	_capture_place_rects()
	_footings = FootingWash.new()
	_footings.name = "BuildingContactShadows"
	_footings.z_index = 0
	var drawable_places: Dictionary = _drawable_place_ids()
	for row: Dictionary in data.get("footings", []):
		if drawable_places.has(str(row.get("place_id", ""))):
			_footings.rows.append(row)
	add_child(_footings)
	audit["footings_created"] = _footings.rows.size()
	_build_roads(data.get("roads", []), sprites)
	var vegetation := Node2D.new()
	vegetation.name = "SparseVegetationAndRocks"
	vegetation.z_index = 1
	# Only siblings inside this independent layer are sorted. Parent world,
	# buildings, pawns and HUD retain their own transforms and ordering.
	vegetation.y_sort_enabled = true
	add_child(vegetation)
	var placements: Array = data.get("placements", [])
	audit["placements_requested"] = placements.size()
	for row: Dictionary in placements:
		_build_scenery_sprite(row, sprites, vegetation)
	_build_bridges(data.get("bridges", []), sprites)
	audit["texture_count"] = _textures.size()
	_initialized = true
	refresh_visibility(true)
	set_process(true)


func _read_dictionary(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	return parsed if parsed is Dictionary else {}


func _capture_place_rects() -> void:
	var presentation: Variant = _map_owner.get("presentation")
	if not presentation is Dictionary:
		return
	for row: Dictionary in presentation.get("placements", []):
		for key: String in ["rect", "labelRect"]:
			var values: Array = row.get(key, [])
			if values.size() == 4:
				_avoid_rects.append(Rect2(float(values[0]), float(values[1]), float(values[2]), float(values[3])).grow(12.0))
		var point: Array = row.get("drawPos", [])
		if point.size() == 2:
			_avoid_rects.append(Rect2(Vector2(float(point[0]), float(point[1])) - Vector2(34, 24), Vector2(68, 60)))


func _drawable_place_ids() -> Dictionary:
	var result: Dictionary = {}
	var presentation: Variant = _map_owner.get("presentation")
	if presentation is Dictionary:
		for row: Dictionary in presentation.get("placements", []):
			if bool(row.get("drawIcon", true)):
				result[str(row.get("id", ""))] = true
	return result


func _asset_texture(asset: String, sprites: Dictionary) -> Texture2D:
	if _textures.has(asset):
		return _textures[asset] as Texture2D
	var description: Dictionary = sprites.get(asset, {})
	var path := str(description.get("path", ""))
	if path.is_empty() or not FileAccess.file_exists(path):
		if not audit.missing_assets.has(asset):
			audit.missing_assets.append(asset)
		return null
	# These are small, shared native sprite crops. The layer never reads terrain
	# tile images or creates CPU copies of the full mother painting.
	var pixels := Image.new()
	var status := pixels.load_png_from_buffer(FileAccess.get_file_as_bytes(path))
	if status != OK or pixels.is_empty():
		if not audit.missing_assets.has(asset):
			audit.missing_assets.append(asset)
		return null
	var texture := ImageTexture.create_from_image(pixels)
	_textures[asset] = texture
	return texture


func _build_scenery_sprite(row: Dictionary, sprites: Dictionary, parent: Node2D) -> void:
	var asset := str(row.get("asset", ""))
	var texture := _asset_texture(asset, sprites)
	if texture == null:
		return
	var position_data: Array = row.get("position", [])
	if position_data.size() != 2:
		return
	var at := Vector2(float(position_data[0]), float(position_data[1]))
	var description: Dictionary = sprites.get(asset, {})
	var height := float(row.get("height", description.get("world_height", 40.0)))
	var base_scale := height / maxf(texture.get_height(), 1.0)
	var pivot_data: Array = description.get("pivot", [0.5, 1.0])
	var pivot := Vector2(float(pivot_data[0]), float(pivot_data[1]))
	var is_group := asset in ["pine_group", "mixed_group", "willow_group"]
	var is_small := asset in ["reeds", "rock_group", "shrub", "roadside_stones"]
	var maximum_compensation := 2.0 if is_group else 1.72
	var dimensions := Vector2(texture.get_size()) * base_scale * maximum_compensation
	var world_rect := Rect2(at - dimensions * pivot, dimensions)
	for obstacle: Rect2 in _avoid_rects:
		if world_rect.intersects(obstacle):
			audit["placements_avoided"] = int(audit.placements_avoided) + 1
			return
	var sprite := Sprite2D.new()
	sprite.name = str(row.get("id", "Scenery"))
	sprite.texture = texture
	sprite.centered = false
	sprite.offset = -Vector2(texture.get_size()) * pivot
	sprite.position = at
	sprite.flip_h = bool(row.get("flip_h", false))
	sprite.modulate.a = float(row.get("opacity", 0.88))
	sprite.scale = Vector2.ONE * base_scale
	parent.add_child(sprite)
	_entries.append({"node": sprite, "position": at, "base_scale": base_scale,
		"group": is_group, "small": is_small, "rank": int(row.get("lod_rank", 0)),
		"max_compensation": maximum_compensation, "radius": dimensions.length()})
	audit["placements_created"] = int(audit.placements_created) + 1


func _build_roads(rows: Array, sprites: Dictionary) -> void:
	var road_root := Node2D.new()
	road_root.name = "SelectedTownApproaches"
	road_root.z_index = 0
	add_child(road_root)
	for row: Dictionary in rows:
		var texture := _asset_texture(str(row.get("asset", "road_dirt")), sprites)
		if texture == null:
			continue
		var points := PackedVector2Array()
		for point: Array in row.get("points", []):
			points.append(Vector2(float(point[0]), float(point[1])))
		if points.size() < 2:
			continue
		var line := Line2D.new()
		line.name = str(row.get("id", "LocalDirtRoad"))
		line.points = points
		line.width = float(row.get("width", 7.5))
		line.texture = texture
		line.texture_mode = Line2D.LINE_TEXTURE_TILE
		line.texture_repeat = CanvasItem.TEXTURE_REPEAT_ENABLED
		line.default_color = Color(1, 1, 1, float(row.get("opacity", 0.48)))
		line.begin_cap_mode = Line2D.LINE_CAP_ROUND
		line.end_cap_mode = Line2D.LINE_CAP_ROUND
		line.joint_mode = Line2D.LINE_JOINT_ROUND
		line.antialiased = true
		var taper := Curve.new()
		taper.add_point(Vector2(0, 0.2))
		taper.add_point(Vector2(0.12, 1.0))
		taper.add_point(Vector2(0.88, 1.0))
		taper.add_point(Vector2(1.0, 0.2))
		line.width_curve = taper
		road_root.add_child(line)
		var bounds := Rect2(points[0], Vector2.ZERO)
		for point: Vector2 in points:
			bounds = bounds.expand(point)
		_roads.append({"node": line, "bounds": bounds.grow(18), "min_zoom": float(row.get("min_zoom", 0.28))})
		audit["roads_created"] = int(audit.roads_created) + 1


func _build_bridges(rows: Array, sprites: Dictionary) -> void:
	var bridge_root := Node2D.new()
	bridge_root.name = "SelectedVisualCrossings"
	bridge_root.z_index = 0
	add_child(bridge_root)
	for row: Dictionary in rows:
		var texture := _asset_texture(str(row.get("asset", "bridge_wood")), sprites)
		if texture == null:
			continue
		var sprite := Sprite2D.new()
		sprite.name = str(row.get("id", "VisualBridge"))
		sprite.texture = texture
		sprite.position = Vector2(float(row.position[0]), float(row.position[1]))
		sprite.rotation = float(row.get("rotation", 0.0))
		sprite.scale = Vector2(float(row.get("width", 80.0)) / texture.get_width(), float(row.get("height", 24.0)) / texture.get_height())
		sprite.modulate.a = float(row.get("opacity", 0.9))
		bridge_root.add_child(sprite)
		var radius := maxf(float(row.get("width", 80.0)), float(row.get("height", 24.0)))
		_roads.append({"node": sprite, "bounds": Rect2(sprite.position - Vector2.ONE * radius, Vector2.ONE * radius * 2), "min_zoom": 0.22})
		audit["bridges_created"] = int(audit.bridges_created) + 1


func _process(delta: float) -> void:
	if not _initialized or not is_instance_valid(_map_owner) or not _map_owner.is_visible_in_tree():
		return
	_elapsed += delta
	if _elapsed < CAMERA_REFRESH_SECONDS:
		return
	_elapsed = 0.0
	refresh_visibility()


func refresh_visibility(force := false) -> void:
	if not is_instance_valid(_map_owner):
		return
	var zoom_value: Variant = _map_owner.get("world_zoom")
	var camera_value: Variant = _map_owner.get("camera_pos")
	var zoom := maxf(float(zoom_value) if zoom_value != null else 1.0, 0.01)
	var camera: Vector2 = camera_value if camera_value is Vector2 else Vector2.ZERO
	var view := Rect2(camera, _map_owner.get_viewport_rect().size / zoom)
	if not force and view == _last_view and is_equal_approx(zoom, _last_zoom):
		return
	_last_view = view
	_last_zoom = zoom
	var cull_rect := view.grow(160.0 / maxf(zoom, 0.25))
	var visible_count := 0
	for entry: Dictionary in _entries:
		var sprite: Sprite2D = entry.node
		var in_view := cull_rect.has_point(entry.position)
		var detail_visible := true
		if zoom < 0.26:
			detail_visible = bool(entry.group) and int(entry.rank) % 3 == 0
		elif zoom < 0.65:
			detail_visible = not bool(entry.small) and (bool(entry.group) or int(entry.rank) % 3 != 0)
		sprite.visible = in_view and detail_visible
		if sprite.visible:
			var compensation := minf(float(entry.max_compensation), 1.0 / zoom)
			sprite.scale = Vector2.ONE * float(entry.base_scale) * compensation
			visible_count += 1
	for entry: Dictionary in _roads:
		var item: CanvasItem = entry.node
		var bounds: Rect2 = entry.bounds
		item.visible = zoom >= float(entry.min_zoom) and bounds.intersects(cull_rect)
	if is_instance_valid(_footings):
		_footings.visible = zoom >= 0.3
	audit["visible_scenery"] = visible_count
	audit["last_zoom"] = zoom


func get_visual_audit() -> Dictionary:
	return audit.duplicate(true)
