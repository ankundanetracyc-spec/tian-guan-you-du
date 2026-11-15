extends Node2D
## Ground-only art. These batches never participate in navigation, input, or
## actor depth sorting; a chunk owns each decoration exactly once.
const DATA_PATH := "res://resources/world/bai_chuan/ground_decorations_20261106.json"
const GroundDetailsShader = preload("res://shaders/bai_chuan_ground_details.gdshader")
const GROUND_DEPTH := 24
const REFRESH_INTERVAL := 0.12
const VIEW_MARGIN := 48.0

var map: Control
var metadata: Dictionary = {}
var catalog: Dictionary = {}
var assets: Dictionary = {}
var chunks: Dictionary = {}
var active: Dictionary = {}
var counts_by_asset: Dictionary = {}
var total := 0
var active_instances := 0
var visible_instances := 0
var draw_calls := 0
var refresh_count := 0
var last_refresh_usec := 0
var last_query_count := 0
var _grid_size := 640.0
var _max_extent := 0.0
var _elapsed := 0.0
var _last_rect := Rect2()
var _last_level := -1
var _atlas_textures: Dictionary = {}
var _asset_layers: Dictionary = {}
var _quad: QuadMesh

func configure(owner_map: Control, data_path: String = DATA_PATH) -> bool:
	map = owner_map
	_clear_batches()
	for layer: Node2D in _asset_layers.values():
		remove_child(layer)
		layer.queue_free()
	_asset_layers.clear()
	metadata.clear()
	catalog.clear()
	assets.clear()
	chunks.clear()
	counts_by_asset.clear()
	_atlas_textures.clear()
	total = 0
	_max_extent = 0.0
	_last_level = -1
	if not FileAccess.file_exists(data_path): return false
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(data_path))
	if not parsed is Dictionary: return false
	metadata = parsed
	if int(metadata.get("version", 0)) != 1: return false
	_grid_size = float(metadata.get("grid_size", 640.0))
	if _grid_size <= 0.0: return false
	var catalog_path := str(metadata.get("catalog_path", ""))
	if not FileAccess.file_exists(catalog_path): return false
	var parsed_catalog: Variant = JSON.parse_string(FileAccess.get_file_as_string(catalog_path))
	if not parsed_catalog is Dictionary: return false
	catalog = parsed_catalog
	var catalog_assets: Dictionary = catalog.get("assets", {})
	for asset_id: String in catalog_assets:
		var source: Dictionary = catalog_assets[asset_id]
		var texture_path := str(source.get("atlas", catalog.get("atlas", "")))
		var runtime_path := str(source.get("runtime_atlas", catalog.get("runtime_atlas", "") if texture_path == str(catalog.get("atlas", "")) else ""))
		var texture := _load_atlas(texture_path, runtime_path)
		var region: Array = source.get("region", [])
		if texture == null or region.size() != 4: return false
		var region_rect := Rect2(float(region[0]), float(region[1]), float(region[2]), float(region[3]))
		if region_rect.size.x <= 0.0 or region_rect.size.y <= 0.0: return false
		if not Rect2(Vector2.ZERO, texture.get_size()).encloses(region_rect): return false
		var material := ShaderMaterial.new()
		material.shader = GroundDetailsShader
		material.set_shader_parameter("atlas_region", Vector4(region_rect.position.x / texture.get_width(), region_rect.position.y / texture.get_height(), region_rect.size.x / texture.get_width(), region_rect.size.y / texture.get_height()))
		assets[asset_id] = {"texture": texture, "material": material}
		counts_by_asset[asset_id] = 0
		# Catalog order puts broad washes before low grass and stones across all
		# chunks, even when a newly activated wash extends over a chunk boundary.
		var layer := Node2D.new()
		layer.name = asset_id
		add_child(layer)
		_asset_layers[asset_id] = layer
	_quad = QuadMesh.new()
	_quad.size = Vector2.ONE
	var decorations: Array = metadata.get("decorations", [])
	for source: Dictionary in decorations:
		var asset_id := str(source.get("asset", ""))
		var position_values: Array = source.get("position", [source.get("x", 0.0), source.get("y", 0.0)])
		var bounds_values: Array = source.get("bounds", [])
		if not assets.has(asset_id) or position_values.size() != 2 or bounds_values.size() != 4: return false
		if str(source.get("layer", "")) != "ground_decals" or int(source.get("z", -1)) != GROUND_DEPTH: return false
		var point := Vector2(float(position_values[0]), float(position_values[1]))
		var bounds := Rect2(float(bounds_values[0]), float(bounds_values[1]), float(bounds_values[2]), float(bounds_values[3]))
		if not point.is_finite() or not bounds.position.is_finite() or not bounds.size.is_finite(): return false
		if bounds.size.x <= 0.0 or bounds.size.y <= 0.0: return false
		var tint_values: Array = source.get("tint", [1.0, 1.0, 1.0])
		if tint_values.size() != 3 and tint_values.size() != 4: return false
		var tint_rgb := Vector3(float(tint_values[0]), float(tint_values[1]), float(tint_values[2]))
		if not tint_rgb.is_finite(): return false
		var tint := Color(clampf(tint_rgb.x, 0.0, 1.0), clampf(tint_rgb.y, 0.0, 1.0), clampf(tint_rgb.z, 0.0, 1.0), 1.0)
		var entry := {"id": str(source.get("id", "")), "asset": asset_id, "position": point, "bounds": bounds, "flip": bool(source.get("flip", false)), "opacity": clampf(float(source.get("opacity", 1.0)), 0.0, 1.0), "tint": tint}
		var key := Vector2i((point / _grid_size).floor())
		if not chunks.has(key): chunks[key] = {}
		var groups: Dictionary = chunks[key]
		if not groups.has(asset_id): groups[asset_id] = []
		groups[asset_id].append(entry)
		var reach := (bounds.position - point).abs().max((bounds.end - point).abs())
		_max_extent = maxf(_max_extent, maxf(reach.x, reach.y))
		counts_by_asset[asset_id] += 1
		total += 1
	refresh(true)
	return total > 0

func _load_atlas(png_path: String, runtime_path: String) -> Texture2D:
	var cache_key := runtime_path + "|" + png_path
	if _atlas_textures.has(cache_key): return _atlas_textures[cache_key]
	var candidates: Array[String] = []
	if not runtime_path.is_empty(): candidates.append(runtime_path)
	if not png_path.is_empty(): candidates.append(png_path.get_basename() + ".res")
	var texture: Texture2D
	for path: String in candidates:
		if ResourceLoader.exists(path):
			texture = load(path) as Texture2D
			if texture != null: break
	if texture == null and FileAccess.file_exists(png_path):
		var image := Image.new()
		if image.load_png_from_buffer(FileAccess.get_file_as_bytes(png_path)) == OK:
			# Preserve the native RGBA pixels. Only the minification chain is added.
			image.generate_mipmaps()
			texture = ImageTexture.create_from_image(image)
	if texture != null: _atlas_textures[cache_key] = texture
	return texture

func update(delta: float) -> void:
	if not is_instance_valid(map) or not map.is_visible_in_tree(): return
	_elapsed += delta
	if _elapsed >= REFRESH_INTERVAL: refresh()

func refresh(force := false) -> void:
	_elapsed = 0.0
	if not is_instance_valid(map): return
	var level := int(map.zoom_level)
	var view_rect := Rect2(map.camera_pos, map.get_viewport_rect().size / maxf(float(map.world_zoom), 0.01))
	if not force and level == _last_level and view_rect == _last_rect: return
	var began := Time.get_ticks_usec()
	_last_level = level
	_last_rect = view_rect
	visible = level > 0
	if level == 0:
		_clear_batches()
		_finish_refresh(began)
		return
	# The anchor owns the batch; include its full possible overhang when finding
	# chunks so an offscreen anchor never clips visible grass at a chunk edge.
	var query_rect := view_rect.grow(_max_extent + VIEW_MARGIN)
	var lo := Vector2i((query_rect.position / _grid_size).floor())
	var hi := Vector2i((query_rect.end / _grid_size).floor())
	var wanted := {}
	visible_instances = 0
	last_query_count = 0
	for y in range(lo.y, hi.y + 1):
		for x in range(lo.x, hi.x + 1):
			var key := Vector2i(x, y)
			if not chunks.has(key): continue
			wanted[key] = true
			var groups: Dictionary = chunks[key]
			for rows: Array in groups.values():
				last_query_count += rows.size()
				for entry: Dictionary in rows:
					if view_rect.intersects(entry.bounds): visible_instances += 1
			if not active.has(key): _activate_chunk(key, groups)
	for key: Vector2i in active.keys():
		if not wanted.has(key): _release_chunk(key)
	_finish_refresh(began)

func _activate_chunk(key: Vector2i, groups: Dictionary) -> void:
	var chunk_origin := Vector2(key) * _grid_size
	var views: Array[MultiMeshInstance2D] = []
	var instance_count := 0
	for asset_id: String in groups:
		var rows: Array = groups[asset_id]
		var multimesh := MultiMesh.new()
		multimesh.transform_format = MultiMesh.TRANSFORM_2D
		multimesh.use_colors = true
		multimesh.mesh = _quad
		multimesh.instance_count = rows.size()
		for i in rows.size():
			var entry: Dictionary = rows[i]
			var bounds: Rect2 = entry.bounds
			var scale_value := Vector2(-bounds.size.x if entry.flip else bounds.size.x, bounds.size.y)
			var center := bounds.get_center() - chunk_origin
			multimesh.set_instance_transform_2d(i, Transform2D(0.0, scale_value, 0.0, center))
			var tint: Color = entry.tint
			multimesh.set_instance_color(i, Color(tint.r, tint.g, tint.b, float(entry.opacity)))
		var view := MultiMeshInstance2D.new()
		view.name = "Chunk_%d_%d" % [key.x, key.y]
		view.position = chunk_origin
		view.multimesh = multimesh
		view.texture = assets[asset_id].texture
		view.material = assets[asset_id].material
		view.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		view.texture_repeat = CanvasItem.TEXTURE_REPEAT_DISABLED
		view.z_as_relative = false
		view.z_index = GROUND_DEPTH
		view.set_meta("layer", "ground_decals")
		_asset_layers[asset_id].add_child(view)
		views.append(view)
		instance_count += rows.size()
	active[key] = {"views": views, "instance_count": instance_count, "draw_calls": groups.size()}
	active_instances += instance_count
	draw_calls += groups.size()

func _release_chunk(key: Vector2i) -> void:
	var chunk: Dictionary = active[key]
	active_instances -= int(chunk.instance_count)
	draw_calls -= int(chunk.draw_calls)
	# Remove immediately so a forced camera change cannot render the old chunk
	# for another frame while its queued deletion is waiting.
	for view: MultiMeshInstance2D in chunk.views:
		view.get_parent().remove_child(view)
		view.queue_free()
	active.erase(key)

func _clear_batches() -> void:
	for key: Vector2i in active.keys(): _release_chunk(key)
	active_instances = 0
	visible_instances = 0
	draw_calls = 0
	last_query_count = 0

func _finish_refresh(began: int) -> void:
	refresh_count += 1
	last_refresh_usec = Time.get_ticks_usec() - began

func get_debug_summary() -> Dictionary:
	return {"total": total, "chunks": chunks.size(), "active_chunks": active.size(), "draw_calls": draw_calls, "active_instances": active_instances, "visible_instances": visible_instances, "last_query": last_query_count, "counts_by_asset": counts_by_asset.duplicate(), "refresh_count": refresh_count, "last_refresh_usec": last_refresh_usec, "grid_size": _grid_size, "max_extent": _max_extent, "ground_only": true, "layer": "ground_decals", "z": GROUND_DEPTH, "visible": visible, "atlases": _atlas_textures.size()}
