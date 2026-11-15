extends Node2D
## Renderer-only scenery. Walkability is baked separately from these complete
## silhouettes. Runtime rendering never edits navigation or travel state.
const VisualScale = preload("res://gdscripts/world/WorldMapVisualScale.gd")
const WorldScale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const DATA_PATH := WorldScale.LANDSCAPE_PATH
const GroundShader = preload("res://shaders/bai_chuan_layered_ground.gdshader")
const ExpandedGroundShader = preload("res://shaders/bai_chuan_expanded_surface.gdshader")
const MountainShader = preload("res://shaders/bai_chuan_solid_mountain.gdshader")
const OverviewTreeShader = preload("res://shaders/bai_chuan_overview_trees.gdshader")
const GroundDetails = preload("res://gdscripts/world/BaiChuanGroundDetails.gd")
const GroundingPatch = preload("res://gdscripts/world/BaiChuanGroundingPatch.gd")
var metadata: Dictionary = {}
var entries: Dictionary = {}
var chunks: Dictionary = {}
var active: Dictionary = {}
var source_tiles: Dictionary = {}
var tree_textures: Dictionary = {}
var mountain_textures: Dictionary = {}
var bridge_textures: Dictionary = {}
var feature_texture: Texture2D
var tree_atlas: Texture2D
var overview: Node2D
var map: Control
var _last_rect := Rect2()
var _last_level := -1
var _elapsed := 1.0
var refresh_count := 0
var last_query_count := 0
var last_refresh_usec := 0
var mountain_material: ShaderMaterial
var sample_mountain_material: ShaderMaterial
var _sample_mountain_materials: Dictionary = {}
var _tree_atlases: Dictionary = {}
var grounding_patch: Dictionary = {}
var ground_details: Node2D
var grounding_details: Node2D
var mountain_ground_details: Node2D

func _load_texture(path: String, mipmaps := false) -> Texture2D:
	var base := str(metadata.get("runtime_texture_base", "res://textures/world/bai_chuan/layered_20261106/runtime/"))
	for resource_path: String in [path.get_basename() + ".res", base + path.get_file().get_basename() + ".res"]:
		if ResourceLoader.exists(resource_path):
			var texture := load(resource_path) as Texture2D
			if texture != null: return texture
	if not FileAccess.file_exists(path): return null
	var image := Image.new()
	if image.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) != OK: return null
	if mipmaps: image.generate_mipmaps()
	return ImageTexture.create_from_image(image)

func _load_tree_atlas(path: String, runtime_path: String) -> Texture2D:
	var cache_key := runtime_path + "|" + path
	if _tree_atlases.has(cache_key): return _tree_atlases[cache_key]
	var texture: Texture2D
	var candidates: Array[String] = []
	if not runtime_path.is_empty(): candidates.append(runtime_path)
	if not path.is_empty(): candidates.append(path.get_basename() + ".res")
	for candidate: String in candidates:
		if ResourceLoader.exists(candidate):
			texture = load(candidate) as Texture2D
			if texture != null: break
	if texture == null and FileAccess.file_exists(path):
		var image := Image.new()
		if image.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK:
			image.generate_mipmaps()
			texture = ImageTexture.create_from_image(image)
	if texture != null: _tree_atlases[cache_key] = texture
	return texture

func configure(owner_map: Control) -> bool:
	map = owner_map
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH))
	if not parsed is Dictionary: return false
	metadata = parsed
	grounding_patch = GroundingPatch.merge(metadata)
	mountain_material = ShaderMaterial.new()
	mountain_material.shader = MountainShader
	sample_mountain_material = ShaderMaterial.new()
	sample_mountain_material.shader = MountainShader
	sample_mountain_material.set_shader_parameter("grounding_strength", 1.0)
	var expanded := metadata.has("water_distance")
	feature_texture = _load_texture(str(metadata.water_distance if expanded else metadata.feature_data))
	tree_atlas = _load_texture(str(metadata.tree_atlas), true)
	var ground := _load_texture(str(metadata.ground_texture), true)
	var tone := _load_texture(str(metadata.ground_tone_data))
	var water: Texture2D = _load_texture(str(metadata.water_texture), true) if expanded else null
	if feature_texture == null or tree_atlas == null or ground == null or tone == null: return false
	for child in map.world.get_children():
		if not child is Sprite2D or not str(child.name).begins_with("Background"): continue
		var tile := child as Sprite2D
		var index := Vector2i((tile.position / (float(metadata.world_size) / 3.0)).round())
		source_tiles[index] = tile.texture
		var material := ShaderMaterial.new()
		material.shader = ExpandedGroundShader if expanded else GroundShader
		if expanded:
			material.set_shader_parameter("water_distance", feature_texture)
			material.set_shader_parameter("water_texture", water)
			material.set_shader_parameter("source_scale", float(metadata.source_scale))
		else:
			material.set_shader_parameter("feature_data", feature_texture)
		material.set_shader_parameter("ground_texture", ground)
		material.set_shader_parameter("ground_tone", tone)
		material.set_shader_parameter("tile_origin", tile.position)
		material.set_shader_parameter("world_size", float(metadata.world_size))
		material.set_shader_parameter("tile_world_size", float(metadata.world_size) / 3.0)
		var average: Array = metadata.ground_texture_average
		material.set_shader_parameter("ground_average", Vector3(average[0], average[1], average[2]) / 255.0)
		tile.material = material
	for id: String in metadata.tree_catalog:
		var source: Dictionary = metadata.tree_catalog[id]
		var region: Array = source.region
		var atlas := AtlasTexture.new()
		atlas.atlas = _load_tree_atlas(str(source.atlas), str(source.get("runtime_atlas", ""))) if source.has("atlas") else tree_atlas
		if atlas.atlas == null: return false
		atlas.region = Rect2(region[0], region[1], region[2], region[3])
		atlas.filter_clip = true
		tree_textures[id] = atlas
	var banks := {}
	for bank: String in metadata.mountain_sources:
		banks[bank] = _load_texture(str(metadata.mountain_sources[bank].path), true)
	for id: String in metadata.mountain_catalog:
		var entry: Dictionary = metadata.mountain_catalog[id]
		var region: Array = entry.region
		var atlas := AtlasTexture.new()
		atlas.atlas = banks[str(entry.bank)]
		atlas.region = Rect2(region[0], region[1], region[2], region[3])
		atlas.filter_clip = true
		mountain_textures[id] = atlas
	if metadata.has("bridge_catalog") and not metadata.get("bridges", []).is_empty():
		var source := _load_texture(str(metadata.bridge_atlas), true)
		for key: String in metadata.bridge_catalog:
			var entry: Dictionary = metadata.bridge_catalog[key]
			var region: Array = entry.region
			var atlas := AtlasTexture.new()
			atlas.atlas = source
			atlas.region = Rect2(region[0], region[1], region[2], region[3])
			atlas.filter_clip = true
			bridge_textures[key] = atlas
	var collections: Array = [metadata.mountains, metadata.trees]
	if metadata.has("bridges"): collections.append(metadata.bridges)
	for rows: Array in collections:
		for entry: Dictionary in rows:
			if not entry.has("kind"): entry.kind = "bridge"
			var values: Array = entry.bounds
			var bounds := Rect2(values[0], values[1], values[2], values[3])
			entry["world_rect"] = bounds
			entries[str(entry.id)] = entry
			var lo := Vector2i((bounds.position / float(metadata.grid_size)).floor())
			var hi := Vector2i((bounds.end / float(metadata.grid_size)).floor())
			for y in range(lo.y, hi.y + 1):
				for x in range(lo.x, hi.x + 1):
					var key := Vector2i(x, y)
					if not chunks.has(key): chunks[key] = []
					chunks[key].append(str(entry.id))
	_build_overview()
	_prepare_depths()
	ground_details = GroundDetails.new()
	ground_details.name = "GroundDetails"
	add_child(ground_details)
	if not ground_details.configure(map):
		push_warning("百川地图：独立地面装饰加载失败")
		ground_details.queue_free()
		ground_details = null
	if bool(grounding_patch.get("active", false)):
		grounding_details = GroundDetails.new()
		grounding_details.name = "ForestGroundingDetails"
		add_child(grounding_details)
		# Both helpers use fixed ground Z24. Broad sample washes render before
		# the existing small grass and stones regardless of chunk activation.
		move_child(grounding_details, 0)
		if not grounding_details.configure(map, str(metadata.grounding_details_path)):
			push_warning("百川地图：林地样区地面装饰加载失败")
			grounding_details.queue_free()
			grounding_details = null
	var mountain_details_path := str(metadata.get("mountain_ground_details_path", ""))
	if not mountain_details_path.is_empty():
		mountain_ground_details = GroundDetails.new()
		mountain_ground_details.name = "MountainGroundDetails"
		add_child(mountain_ground_details)
		# Mountain earth washes precede the forest floor and existing low detail.
		# Each helper still draws in the shared absolute ground layer at Z24.
		move_child(mountain_ground_details, 0)
		if not mountain_ground_details.configure(map, mountain_details_path):
			push_warning("百川地图：山脚地面过渡加载失败")
			mountain_ground_details.queue_free()
			mountain_ground_details = null
	refresh(true)
	return source_tiles.size() == 9

func _prepare_depths() -> void:
	VisualScale.flatten_avatar_layers(map.player_avatar)
	for id: String in map.place_views:
		var marker: Control = map.place_views[id]
		var foot: Vector2 = map.travel.graph.positions.get(id, marker.position)
		VisualScale.place_on_ground(marker.get_node("NativeIcon"), foot.y)
		VisualScale.overlay(marker.get_node("SelectPlace"), VisualScale.LABEL_DEPTH)
		VisualScale.overlay(marker.get_node("SelectIcon"), VisualScale.INPUT_DEPTH)
	for item in [map.enter_button, map.formal_details_panel, map.far_city_hover_hint, map.population_badges, map.scale_ruler, map.travel_controls.get_parent()]:
		if is_instance_valid(item): VisualScale.overlay(item)
	if is_instance_valid(map.destination_mark): VisualScale.overlay(map.destination_mark, 4040)
	# Preview chrome remains under the live HUD but above scenery.
	for node_name in ["MapInkWash", "MiniMap", "MapControls", "InfoBox"]:
		var item := map.get_node_or_null(node_name)
		if item is CanvasItem: VisualScale.overlay(item)

func _build_overview() -> void:
	# The whole-world view uses one draw call per tree asset, without thousands of
	# individual scenery nodes. NPCs are hidden at this distance already.
	overview = Node2D.new()
	overview.name = "OverviewForest"
	overview.z_index = 100
	add_child(overview)
	for asset: String in tree_textures:
		var rows: Array = metadata.trees.filter(func(row: Dictionary): return str(row.asset) == asset)
		if rows.is_empty(): continue
		var mesh := QuadMesh.new()
		mesh.size = Vector2.ONE
		var multimesh := MultiMesh.new()
		multimesh.transform_format = MultiMesh.TRANSFORM_2D
		multimesh.mesh = mesh
		multimesh.instance_count = rows.size()
		var texture: AtlasTexture = tree_textures[asset]
		for i in rows.size():
			var entry: Dictionary = rows[i]
			var height := float(entry.height)
			var width := height * texture.get_width() / texture.get_height()
			var point := Vector2(entry.position[0], entry.position[1]) + _tree_center_offset(entry, Vector2(width, height))
			var transform := Transform2D(0.0, Vector2(-width if bool(entry.flip) else width, height), 0.0, point)
			multimesh.set_instance_transform_2d(i, transform)
		var view := MultiMeshInstance2D.new()
		view.multimesh = multimesh
		var source: Texture2D = texture.atlas
		view.texture = source
		view.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		var material := ShaderMaterial.new()
		material.shader = OverviewTreeShader
		var region := texture.region
		material.set_shader_parameter("atlas_region", Vector4(region.position.x / source.get_width(), region.position.y / source.get_height(), region.size.x / source.get_width(), region.size.y / source.get_height()))
		view.material = material
		overview.add_child(view)

func update(delta: float) -> void:
	if not is_instance_valid(map) or not map.is_visible_in_tree(): return
	map.depth_order.set_view(map.camera_pos.y, map.world_zoom, map.get_viewport_rect().size.y, float(metadata.get("max_prop_height", 900.0)))
	map.depth_order.place(map.player_mark, map.player_mark.position.y)
	map.depth_order.place(map.player_halo, map.player_mark.position.y - 2.0)
	for item: Node2D in map.npc_views.values(): map.depth_order.place(item, item.position.y)
	for item: Node2D in map.party_views.values(): map.depth_order.place(item, item.position.y)
	for id: String in map.place_views:
		var icon: CanvasItem = map.place_views[id].get_node("NativeIcon")
		var foot: Vector2 = map.travel.graph.positions.get(id, Vector2.ZERO)
		map.depth_order.place(icon, float(icon.get_meta("logical_z", foot.y)))
	for id: String in active:
		var entry: Dictionary = entries[id]
		if str(entry.get("layer", "ground_props")) == "ground_decals":
			VisualScale.overlay(active[id], int(entry.z))
		else:
			map.depth_order.place(active[id], float(entry.get("z", entry.position[1])))
	if is_instance_valid(map.flight_boat): VisualScale.overlay(map.flight_boat, VisualScale.FLIGHT_DEPTH)
	_elapsed += delta
	if _elapsed >= 0.12: refresh()

func refresh(force := false) -> void:
	_elapsed = 0.0
	if is_instance_valid(ground_details): ground_details.refresh(force)
	if is_instance_valid(grounding_details): grounding_details.refresh(force)
	if is_instance_valid(mountain_ground_details): mountain_ground_details.refresh(force)
	var rect := Rect2(map.camera_pos, map.get_viewport_rect().size / maxf(map.world_zoom, 0.01)).grow(90.0)
	var level := int(map.zoom_level)
	if not force and rect == _last_rect and level == _last_level: return
	var began := Time.get_ticks_usec()
	_last_rect = rect
	_last_level = level
	overview.visible = level == 0
	var lo := Vector2i((rect.position / float(metadata.grid_size)).floor())
	var hi := Vector2i((rect.end / float(metadata.grid_size)).floor())
	var wanted := {}
	for y in range(lo.y, hi.y + 1):
		for x in range(lo.x, hi.x + 1):
			for id: String in chunks.get(Vector2i(x, y), []): wanted[id] = true
	last_query_count = wanted.size()
	for id: String in wanted.keys():
		var entry: Dictionary = entries[id]
		if (level == 0 and entry.kind == "tree") or not rect.intersects(entry.world_rect):
			wanted.erase(id)
			continue
		if active.has(id): continue
		var view: Node2D
		if entry.kind == "mountain": view = _mountain(entry)
		elif entry.kind == "bridge": view = _bridge(entry)
		else: view = _tree(entry)
		view.name = id
		view.position = Vector2(entry.position[0], entry.position[1])
		if str(entry.get("layer", "ground_props")) == "ground_decals": VisualScale.overlay(view, int(entry.z))
		else: map.depth_order.place(view, float(entry.get("z", view.position.y)))
		add_child(view)
		active[id] = view
	for id: String in active.keys():
		if not wanted.has(id):
			active[id].queue_free()
			active.erase(id)
	refresh_count += 1
	last_refresh_usec = Time.get_ticks_usec() - began

func _tree_center_offset(entry: Dictionary, size: Vector2) -> Vector2:
	var source: Dictionary = metadata.tree_catalog[str(entry.asset)]
	# Keep the four original trees' exact 0.49-height placement. The sample
	# atlas carries a native foot pivot, including its mirrored horizontal foot.
	if not source.has("atlas"): return Vector2(0.0, -size.y * 0.49)
	var pivot: Array = source.get("pivot", [0.5, 0.99])
	var offset := (Vector2(0.5, 0.5) - Vector2(float(pivot[0]), float(pivot[1]))) * size
	if bool(entry.flip): offset.x = -offset.x
	return offset

func _tree(entry: Dictionary) -> Node2D:
	var view := Sprite2D.new()
	view.texture = tree_textures[str(entry.asset)]
	view.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	view.offset = _tree_center_offset(entry, view.texture.get_size())
	view.scale = Vector2.ONE * float(entry.height) / view.texture.get_height()
	view.flip_h = bool(entry.flip)
	return view

func _mountain(entry: Dictionary) -> Node2D:
	var root := Sprite2D.new()
	root.texture = mountain_textures[str(entry.asset)]
	var pivot: Array = metadata.mountain_catalog[str(entry.asset)].get("pivot", [0.5, 1.0])
	root.offset = Vector2((0.5-float(pivot[0])) * root.texture.get_width(), (0.5-float(pivot[1])) * root.texture.get_height())
	root.scale = Vector2(entry.bounds[2], entry.bounds[3]) / root.texture.get_size()
	root.flip_h = bool(entry.flip)
	root.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	root.material = mountain_material
	if bool(metadata.get("all_mountains_grounded", false)) or metadata.get("grounded_mountain_ids", {}).has(str(entry.id)):
		var asset_id := str(entry.asset)
		if not _sample_mountain_materials.has(asset_id):
			var material := sample_mountain_material.duplicate() as ShaderMaterial
			var texture: AtlasTexture = root.texture
			var region := texture.region
			var atlas_size := texture.atlas.get_size()
			material.set_shader_parameter("grounding_region", Vector4(region.position.x / atlas_size.x, region.position.y / atlas_size.y, region.size.x / atlas_size.x, region.size.y / atlas_size.y))
			_sample_mountain_materials[asset_id] = material
		root.material = _sample_mountain_materials[asset_id]
	return root

func _bridge(entry: Dictionary) -> Node2D:
	var view := Sprite2D.new()
	view.texture = bridge_textures[str(entry.asset)]
	var pivot: Array = metadata.bridge_catalog[str(entry.asset)].pivot
	view.offset = Vector2((0.5 - float(pivot[0])) * view.texture.get_width(), (0.5 - float(pivot[1])) * view.texture.get_height())
	view.scale = Vector2(float(entry.world_width), float(entry.world_height)) / view.texture.get_size()
	view.rotation = float(entry.rotation)
	view.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	return view

func get_debug_summary() -> Dictionary:
	return {"mountains": metadata.mountains.size(), "trees": metadata.trees.size(), "bridges": metadata.get("bridges", []).size(), "active": active.size(), "chunks": chunks.size(), "last_query": last_query_count, "refresh_count": refresh_count, "last_refresh_usec": last_refresh_usec, "ground_only": true, "depth":map.depth_order.get_view_summary(), "ground_details": ground_details.get_debug_summary() if is_instance_valid(ground_details) else {}, "grounding_patch": grounding_patch.duplicate(), "grounding_details": grounding_details.get_debug_summary() if is_instance_valid(grounding_details) else {}, "mountain_ground_details": mountain_ground_details.get_debug_summary() if is_instance_valid(mountain_ground_details) else {}}
