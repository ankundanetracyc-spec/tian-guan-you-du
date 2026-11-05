extends Node2D

## Runtime importer for the local SpriteSkin export used only by the action battle trial.
## The local-reference appearance uses exported mesh weights; the project appearance uses
## the custom robe cutouts parented to the same exported animated bones.

const UNITY_PIXELS_PER_UNIT := 100.0
const BATTLE_DISPLAY_SCALE := 0.72
const EXPERIMENTAL_BILAN_DISPLAY_SCALE := 0.96
const EXPERIMENTAL_BILAN_WIDTH_FACTOR := 1.16
const EXPERIMENTAL_BILAN_HEIGHT_FACTOR := 0.95
const EXPERIMENTAL_CLOTH_WEIGHT_FRACTION := 0.72

var skeleton: Skeleton2D
var rig_root_node: Node2D
var rig_root_path := ""
var node_by_rig_path: Dictionary = {}
var bone_by_rig_path: Dictionary = {}
var source_sprite_bone_paths: Dictionary = {}
var rest_by_rig_path: Dictionary = {}
var texture_by_sprite_id: Dictionary = {}
var renderer_metadata: Dictionary = {}
var mesh_nodes: Array[Polygon2D] = []
var mesh_records: Array[Dictionary] = []
var bone_bound_art_nodes: Array[Node2D] = []
var experiment_weighted_textures: Dictionary = {}
var experiment_front_overlay: Polygon2D
var experiment_front_texture: Texture2D
var experiment_back_texture: Texture2D
var experiment_upper_leg_texture: Texture2D
var experiment_upper_leg_overlays: Dictionary = {}
var experiment_skirt_front_bone: Bone2D
var experiment_skirt_back_bone: Bone2D
var bone_bound_art_ready := false
var animation_tracks: Dictionary = {}
var animation_meta: Dictionary = {}
var active_skin := "project"
var active_visual_mode := "project"
var load_errors: Array[String] = []
var loaded := false
var custom_art_folder := "bilan_rig_parts_v1"
var wanderer_skin: RefCounted
var native_source_mode := false


func load_runtime(rig_path: String, skins_path: String, animations_path: String, art_root: String, art_folder: String = "bilan_rig_parts_v1") -> bool:
	custom_art_folder = art_folder
	load_errors.clear()
	if art_folder=="yuxu_fullbody_v15":
		# The latest approved route uses the male source directly, at its own
		# canvas sizes and 3D bind poses, rather than the female fitting rig.
		native_source_mode = true
		rig_root_path = "/HumanRootMan/Root"
		rig_root_node = Node2D.new()
		rig_root_node.name = "NativeMale101Root"
		add_child(rig_root_node)
		wanderer_skin = load(get_script().resource_path.get_base_dir().path_join("guigu_native_first_skin.gd")).new()
		bone_bound_art_ready = wanderer_skin.build(self)
		loaded = bone_bound_art_ready
		if not loaded: load_errors.append_array(wanderer_skin.validation_errors)
		return loaded
	var rig_data: Variant = _read_json(rig_path)
	var skins_data: Variant = _read_json(skins_path)
	var animations_data: Variant = _read_json(animations_path)
	if not rig_data is Dictionary or not skins_data is Dictionary or not animations_data is Dictionary:
		load_errors.append("骨架、网格或动画 JSON 无法读取")
		return false
	if not _build_character_hierarchy(rig_data):
		return false
	if not _load_texture_sets(art_root):
		return false
	if not _build_weighted_meshes(skins_data, art_root):
		return false
	if custom_art_folder in ["original_wanderer", "yuxu_unified_v14", "yuxu_fullbody_v15"]:
		var skin_script := "approved_outfit_crossing_skin.gd" if custom_art_folder == "yuxu_fullbody_v15" else ("yuxu_unified_skin.gd" if custom_art_folder == "yuxu_unified_v14" else "wanderer_skin.gd")
		wanderer_skin = load(get_script().resource_path.get_base_dir().path_join(skin_script)).new()
		bone_bound_art_ready = wanderer_skin.build(self, art_root.path_join(custom_art_folder))
		if not bone_bound_art_ready:
			for issue in wanderer_skin.validation_errors: load_errors.append(str(issue))
			return false
	else:
		bone_bound_art_ready = _build_bone_bound_art(art_root)
	_load_animation_tracks(animations_data)
	loaded = not mesh_nodes.is_empty() or bone_bound_art_ready
	if not loaded:
		load_errors.append("导出数据中没有可显示的网格或分件贴图")
	return loaded


func _read_json(path: String) -> Variant:
	if not FileAccess.file_exists(path):
		load_errors.append("缺少 %s" % path)
		return null
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if parsed == null:
		load_errors.append("JSON 解析失败：%s" % path)
	return parsed


func _build_character_hierarchy(rig_data: Dictionary) -> bool:
	rig_root_path = str(rig_data.get("rigRootPath", ""))
	var nodes: Variant = rig_data.get("nodes", [])
	if rig_root_path.is_empty() or not nodes is Array or nodes.is_empty():
		load_errors.append("rig.json 缺少 rigRootPath 或 nodes")
		return false

	skeleton = Skeleton2D.new()
	skeleton.name = "OriginalSkeleton2D"
	add_child(skeleton)
	var node_records: Array[Dictionary] = []
	var bone_paths: Dictionary = {}
	for bone_value in rig_data.get("boneNodes", []):
		if bone_value is Dictionary and bone_value.has("path"):
			var path := _normalize_rig_path(str(bone_value.path))
			bone_paths[path] = true
			source_sprite_bone_paths[path] = true
		elif bone_value is String:
			var path := _normalize_rig_path(str(bone_value))
			bone_paths[path] = true
			source_sprite_bone_paths[path] = true

	for node_value in nodes:
		if not node_value is Dictionary or not node_value.has("path"):
			continue
		var path := _normalize_rig_path(str(node_value.path))
		if path == rig_root_path or path.begins_with(rig_root_path + "/"):
			var record: Dictionary = node_value.duplicate(true)
			record["path"] = path
			node_records.append(record)
	if not _has_rig_path(node_records, rig_root_path):
		load_errors.append("rig.json 未列出目标根节点：%s" % rig_root_path)
		return false

	node_records.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var depth_a := str(a.path).split("/", false).size()
		var depth_b := str(b.path).split("/", false).size()
		if depth_a == depth_b:
			return str(a.path) < str(b.path)
		return depth_a < depth_b
	)

	for index in range(node_records.size()):
		var record: Dictionary = node_records[index]
		var path := str(record.path)
		# Bone2D nodes preserve every Unity Transform in the hierarchy. Only the 32 paths
		# listed in rig.json.boneNodes receive skin weights; intermediary transforms stay
		# available for animation and for rigid, unskinned sprites such as the face.
		var bone := Bone2D.new()
		bone.set_autocalculate_length_and_angle(false)
		bone.length = 0.0
		var node: Node2D = bone
		bone_by_rig_path[path] = bone
		node.name = "RigNode_%04d" % index
		var parent_path := _normalize_rig_path(str(record.get("parentPath", "")))
		if path == rig_root_path:
			rig_root_node = node
			skeleton.add_child(node)
			node.transform = Transform2D.IDENTITY
			node.rest = node.transform
			node.bone_angle = 0.0
		else:
			var parent: Node = node_by_rig_path.get(parent_path, rig_root_node)
			if parent == null:
				parent = rig_root_node
			parent.add_child(node)
			node.transform = _transform_from_trs(record.get("localTRS", {}))
			if node is Bone2D:
				node.rest = node.transform
				node.bone_angle = node.rotation
		if not bool(record.get("activeInHierarchy", true)):
			node.visible = false
		node_by_rig_path[path] = node
		rest_by_rig_path[path] = node.transform
	if rig_root_node == null:
		load_errors.append("无法建立 rig root")
		return false
	if custom_art_folder not in ["original_wanderer", "yuxu_unified_v14", "yuxu_fullbody_v15"]:
		_build_experiment_skirt_bones()
	return true


func _build_experiment_skirt_bones() -> void:
	var parent_path := "/10002/Root/Body/yifu/yifu/shenti2/shenti2_1/shenti2_2"
	var parent: Node = node_by_rig_path.get(parent_path)
	if parent == null:
		load_errors.append("碧岚衣摆骨骼找不到下摆挂点：%s" % parent_path)
		return
	experiment_skirt_front_bone = _new_experiment_skirt_bone("BilanSkirtFront", parent)
	experiment_skirt_back_bone = _new_experiment_skirt_bone("BilanSkirtBack", parent)


func _new_experiment_skirt_bone(bone_name: String, parent: Node) -> Bone2D:
	var bone := Bone2D.new()
	bone.name = bone_name
	bone.set_autocalculate_length_and_angle(false)
	bone.transform = Transform2D.IDENTITY
	bone.length = 0.0
	bone.rotation = 0.0
	parent.add_child(bone)
	bone.rest = bone.transform
	bone.bone_angle = 0.0
	return bone


func _has_rig_path(records: Array[Dictionary], path: String) -> bool:
	for record in records:
		if str(record.get("path", "")) == path:
			return true
	return false


func _load_texture_sets(art_root: String) -> bool:
	var own_path := art_root.path_join("own_guigu_fit/manifest.json")
	var manifest: Variant = _read_json(own_path)
	if not manifest is Dictionary:
		return false
	var layout: Variant = _read_json(art_root.path_join("own_guigu_fit/own_layout.json"))
	if not layout is Array:
		return false
	for entry in layout:
		if entry is Dictionary:
			var renderer_path := _normalize_rig_path(str(entry.get("spriteRendererPath", entry.get("path", ""))))
			if not renderer_path.is_empty():
				renderer_metadata[renderer_path] = entry
	for part_value in manifest.get("parts", []):
		if not part_value is Dictionary:
			continue
		var sprite_id := _sprite_id_string(part_value.get("sourceSpriteId", ""))
		var own_png := art_root.path_join("own_guigu_fit").path_join(str(part_value.get("png", "")))
		var original_png := art_root.path_join("guigu_reference").path_join(str(part_value.get("sourceSprite", "")))
		var own_texture := _texture_from_file(own_png)
		var original_texture := _texture_from_file(original_png)
		if sprite_id.is_empty() or own_texture == null or original_texture == null:
			load_errors.append("贴图缺少 sourceSpriteId 或 PNG：%s" % sprite_id)
			continue
		texture_by_sprite_id[sprite_id] = {"project": own_texture, "reference": original_texture}
	return not texture_by_sprite_id.is_empty()


func _texture_from_file(path: String) -> Texture2D:
	var image := Image.new()
	if image.load(ProjectSettings.globalize_path(path)) != OK:
		load_errors.append("无法加载贴图：%s" % path)
		return null
	return ImageTexture.create_from_image(image)


func _build_bone_bound_art(art_root: String) -> bool:
	var art_dir := art_root.path_join(custom_art_folder)
	var manifest: Variant = _read_json(art_dir.path_join("parts_manifest.json"))
	if not manifest is Dictionary:
		return false
	var bindings: Variant = manifest.get("bindings", {})
	var parts: Variant = manifest.get("parts", [])
	if not bindings is Dictionary or not parts is Array:
		load_errors.append("碧岚骨骼分件清单格式无效")
		return false
	experiment_front_texture = _texture_from_file(art_dir.path_join("parts/bilan_robe_front_panel.png"))
	experiment_back_texture = _texture_from_file(art_dir.path_join("parts/bilan_robe_back_panel.png"))
	experiment_upper_leg_texture = _texture_from_file(art_dir.path_join("parts/bilan_weighted_leg_upper.png"))
	var weighted_texture_paths: Variant = manifest.get("experimentWeightedTextures", {})
	if weighted_texture_paths is Dictionary:
		for sprite_id_value in weighted_texture_paths.keys():
			var sprite_id := _sprite_id_string(sprite_id_value)
			var weighted_texture_path := art_dir.path_join(str(weighted_texture_paths[sprite_id_value])).simplify_path()
			var weighted_texture := _texture_from_file(weighted_texture_path)
			if weighted_texture != null:
				experiment_weighted_textures[sprite_id] = weighted_texture
	var created := 0
	for part_value in parts:
		if not part_value is Dictionary:
			continue
		var part: Dictionary = part_value
		var part_name := str(part.get("name", ""))
		var binding_value: Variant = bindings.get(part_name, {})
		if part_name.is_empty() or not binding_value is Dictionary:
			load_errors.append("骨骼分件缺少绑定设置：%s" % part_name)
			continue
		var binding: Dictionary = binding_value
		var parent_path := _normalize_rig_path(str(binding.get("parentPath", "")))
		var parent: Node = node_by_rig_path.get(parent_path)
		if parent == null:
			load_errors.append("骨骼分件 %s 找不到挂点 %s" % [part_name, parent_path])
			continue
		var texture_path := art_dir.path_join(str(part.get("png", "")))
		var texture := _texture_from_file(texture_path)
		if texture == null:
			continue
		var scale_value: Variant = binding.get("scale", [1.0, 1.0])
		var offset_value: Variant = binding.get("position", [0.0, 0.0])
		var scale := _vector2_from_value(scale_value)
		var offset := _vector2_from_value(offset_value)
		var anchor_node := Node2D.new()
		anchor_node.name = "BilanArt_%s" % part_name
		anchor_node.position = offset
		anchor_node.rotation = deg_to_rad(float(binding.get("rotationDegrees", 0.0)))
		anchor_node.scale = scale
		anchor_node.z_index = int(binding.get("zIndex", 0))
		parent.add_child(anchor_node)
		var sprite := Sprite2D.new()
		sprite.name = "Texture"
		sprite.texture = texture
		sprite.centered = true
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		sprite.texture_repeat = CanvasItem.TEXTURE_REPEAT_DISABLED
		if str(binding.get("anchor", "center")) == "top_center":
			sprite.offset = Vector2(0.0, texture.get_height() * 0.5)
		anchor_node.add_child(sprite)
		bone_bound_art_nodes.append(anchor_node)
		created += 1
	return created == parts.size() and created > 0


func _build_weighted_meshes(skins_data: Dictionary, art_root: String) -> bool:
	var meshes: Variant = skins_data.get("meshes", [])
	if not meshes is Array:
		load_errors.append("skins.json 缺少 meshes")
		return false
	var built_count := 0
	for mesh_value in meshes:
		if not mesh_value is Dictionary:
			continue
		var mesh: Dictionary = mesh_value
		var sprite_id := _sprite_id_string(mesh.get("sourceSpriteId", ""))
		var renderer_path := _normalize_rig_path(str(mesh.get("rendererPath", "")))
		# The local comparison layout lists only the active character renderers. The source prefab
		# also contains a disabled second foot rig, shadow, weapon and transient effects.
		if not renderer_metadata.has(renderer_path):
			continue
		if not texture_by_sprite_id.has(sprite_id):
			load_errors.append("网格 %s 没有自有/原版贴图映射" % sprite_id)
			continue
		var raw_vertices: Variant = mesh.get("vertices", [])
		var uv_convention: Dictionary = mesh.get("uvConvention", {})
		var raw_uvs: Variant = uv_convention.get("uvCropLowerLeft", [])
		if not raw_vertices is Array or not raw_uvs is Array or raw_vertices.is_empty() or raw_vertices.size() != raw_uvs.size():
			load_errors.append("sourceSpriteId=%s 顶点与 UV 长度不一致" % sprite_id)
			continue
		var skinning: Dictionary = mesh.get("skinning", {})
		var weights: Variant = skinning.get("boneWeights", [])
		var has_weights := bool(skinning.get("isSkinned", false)) and _has_any_weights(weights)
		var polygon := Polygon2D.new()
		polygon.name = "Skin_%s_%02d" % [sprite_id, built_count]
		var vertices := PackedVector2Array()
		var normalized_uvs := PackedVector2Array()
		for vertex_index in range(raw_vertices.size()):
			var source_vertex := _vector2_from_value(raw_vertices[vertex_index])
			if has_weights:
				vertices.append(_matrix_point(mesh.get("rendererToRigRoot", []), source_vertex))
			else:
				vertices.append(_godot_pixel_point(source_vertex))
			var source_uv := _vector2_from_value(raw_uvs[vertex_index])
			normalized_uvs.append(Vector2(source_uv.x, 1.0 - source_uv.y))
		polygon.polygon = vertices
		polygon.texture = texture_by_sprite_id[sprite_id]["project"]
		polygon.uv = _polygon_uv_pixels(normalized_uvs, polygon.texture)
		polygon.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		polygon.texture_repeat = CanvasItem.TEXTURE_REPEAT_DISABLED
		polygon.polygons = _triangles_to_polygons(mesh.get("triangles", []), vertices.size())

		var skirt_weight_sets: Dictionary = {}
		if has_weights:
			rig_root_node.add_child(polygon)
			polygon.skeleton = polygon.get_path_to(skeleton)
			if sprite_id == "79450" and experiment_skirt_front_bone != null and experiment_skirt_back_bone != null and weights is Array:
				skirt_weight_sets = _redistribute_robe_hem_weights(weights, skinning.get("spriteBones", []), normalized_uvs)
			if not _apply_bone_weights(polygon, sprite_id, skinning, weights, raw_vertices.size()):
				polygon.queue_free()
				continue
		else:
			var renderer_node: Node = node_by_rig_path.get(renderer_path, rig_root_node)
			if renderer_node == null:
				renderer_node = rig_root_node
			renderer_node.add_child(polygon)
			# A renderer without SpriteSkin weights follows its original transform hierarchy.
			polygon.skeleton = NodePath()

		# Keep the rig render above the arena's full-screen background draw while
		# preserving the relative order from the original SpriteRenderer groups.
		polygon.z_index = _render_layer_for_path(renderer_path) + 40
		mesh_nodes.append(polygon)
		mesh_records.append({"node": polygon, "sprite_id": sprite_id, "renderer_path": renderer_path, "normalized_uvs": normalized_uvs, "skinning": skinning, "weights": weights, "vertex_count": raw_vertices.size(), "skirt_weights": skirt_weight_sets if has_weights else {}})
		built_count += 1
	return built_count > 0


func _redistribute_robe_hem_weights(raw_weights: Array, sprite_bones: Variant, normalized_uvs: PackedVector2Array) -> Dictionary:
	var source_weights: Array = raw_weights.duplicate(true)
	var front_weights := PackedFloat32Array()
	var back_weights := PackedFloat32Array()
	front_weights.resize(source_weights.size())
	back_weights.resize(source_weights.size())
	var hem_bone_index := -1
	if sprite_bones is Array:
		for bone_index in range(sprite_bones.size()):
			if _bone_path_for_index(sprite_bones, bone_index).ends_with("/shenti2_2"):
				hem_bone_index = bone_index
				break
	if hem_bone_index < 0:
		return {}
	for vertex_index in range(mini(source_weights.size(), normalized_uvs.size())):
		var influences: Variant = source_weights[vertex_index]
		if not influences is Array:
			continue
		var u := normalized_uvs[vertex_index].x
		var front_share := smoothstep(0.35, 0.68, u)
		for influence in influences:
			if not influence is Dictionary or int(influence.get("boneIndex", -1)) != hem_bone_index:
				continue
			var old_weight := float(influence.get("weight", 0.0))
			var transferred := old_weight * EXPERIMENTAL_CLOTH_WEIGHT_FRACTION
			influence["weight"] = old_weight - transferred
			front_weights[vertex_index] += transferred * front_share
			back_weights[vertex_index] += transferred * (1.0 - front_share)
	return {"source": source_weights, "front": front_weights, "back": back_weights}


func _polygon_uv_pixels(normalized_uvs: PackedVector2Array, texture: Texture2D) -> PackedVector2Array:
	# Polygon2D stores UVs in texture pixels and normalizes them internally.
	# Keep the exported 0..1 UVs so skins with different texture sizes can share the mesh.
	var texture_size := Vector2(texture.get_size())
	var pixel_uvs := PackedVector2Array()
	pixel_uvs.resize(normalized_uvs.size())
	for index in range(normalized_uvs.size()):
		pixel_uvs[index] = normalized_uvs[index] * texture_size
	return pixel_uvs


func _has_any_weights(weights: Variant) -> bool:
	if not weights is Array:
		return false
	for vertex_weights in weights:
		if not vertex_weights is Array:
			continue
		for influence in vertex_weights:
			if influence is Dictionary and float(influence.get("weight", 0.0)) > 0.0:
				return true
	return false


func _apply_bone_weights(polygon: Polygon2D, sprite_id: String, skinning: Dictionary, weights: Array, vertex_count: int) -> bool:
	var sprite_bones: Variant = skinning.get("spriteBones", [])
	var used_bone_indices: Dictionary = {}
	for vertex_weights in weights:
		if not vertex_weights is Array:
			continue
		for influence in vertex_weights:
			if influence is Dictionary and float(influence.get("weight", 0.0)) > 0.0:
				used_bone_indices[int(influence.get("boneIndex", -1))] = true
	var indices: Array = used_bone_indices.keys()
	indices.sort()
	for bone_index in indices:
		var bone_path := _bone_path_for_index(sprite_bones, int(bone_index))
		var bone_node: Node = node_by_rig_path.get(bone_path)
		if not bone_node is Bone2D:
			load_errors.append("sourceSpriteId=%s 的骨骼索引 %d 无 Bone2D 路径 %s" % [sprite_id, bone_index, bone_path])
			return false
		var per_vertex := PackedFloat32Array()
		per_vertex.resize(vertex_count)
		for vertex_index in range(mini(weights.size(), vertex_count)):
			var influences: Variant = weights[vertex_index]
			if not influences is Array:
				continue
			for influence in influences:
				if influence is Dictionary and int(influence.get("boneIndex", -1)) == int(bone_index):
					per_vertex[vertex_index] += float(influence.get("weight", 0.0))
		# Polygon2D resolves bone paths from its Skeleton2D node, not from itself.
		polygon.add_bone(skeleton.get_path_to(bone_node), per_vertex)
	return true


func _bone_path_for_index(sprite_bones: Variant, bone_index: int) -> String:
	if sprite_bones is Array and bone_index >= 0 and bone_index < sprite_bones.size():
		var item: Variant = sprite_bones[bone_index]
		if item is Dictionary:
			return _normalize_rig_path(str(item.get("rigPath", item.get("path", ""))))
		return _normalize_rig_path(str(item))
	return ""


func _sprite_id_string(value: Variant) -> String:
	if value is int or value is float:
		return str(roundi(float(value)))
	return str(value)


func _render_layer_for_path(path: String) -> int:
	if "/Body/Foot/youtui/" in path:
		return -30
	if "/Body/Foot/zuotui/" in path:
		return -20
	if path.ends_with("/yifu/yifu"):
		return -10
	if "/shenti1/zuobi/" in path:
		return 0
	if "/shenti1/youbi/" in path and "/youbi1/youbi2/" not in path:
		return 10
	if "/youbi1/youbi2/" in path:
		return 15
	if path.ends_with("/lian"):
		return 20
	if "/toufa/" in path:
		return 30
	return 0


func _triangles_to_polygons(raw_triangles: Variant, vertex_count: int) -> Array:
	var polygons: Array = []
	if not raw_triangles is Array:
		return polygons
	if raw_triangles.is_empty():
		return polygons
	if raw_triangles[0] is Array:
		for triangle in raw_triangles:
			if triangle is Array and triangle.size() >= 3:
				var indices := PackedInt32Array()
				for index in triangle:
					var idx := int(index)
					if idx < 0 or idx >= vertex_count:
						indices.clear()
						break
					indices.append(idx)
				if indices.size() >= 3:
					polygons.append(indices)
	else:
		for offset in range(0, raw_triangles.size() - 2, 3):
			var indices := PackedInt32Array([int(raw_triangles[offset]), int(raw_triangles[offset + 1]), int(raw_triangles[offset + 2])])
			var valid := true
			for index in indices:
				if index < 0 or index >= vertex_count:
					valid = false
					break
			if valid:
				polygons.append(indices)
	return polygons


func _load_animation_tracks(animations_data: Dictionary) -> void:
	animation_tracks.clear()
	animation_meta.clear()
	var clips: Variant = animations_data.get("clips", [])
	if not clips is Array:
		return
	for clip_value in clips:
		if not clip_value is Dictionary:
			continue
		var clip: Dictionary = clip_value
		var clip_name := str(clip.get("name", ""))
		if clip_name.is_empty():
			continue
		animation_meta[clip_name] = {
			"duration": float(clip.get("duration", 0.0)),
			"loop": _is_looping_clip(clip),
		}
		var tracks: Dictionary = {}
		var source_tracks: Variant = clip.get("tracks", [])
		if not source_tracks is Array:
			continue
		for source_track in source_tracks:
			if not source_track is Dictionary:
				continue
			var path := _normalize_animation_path(str(source_track.get("targetRigPath", source_track.get("path", ""))))
			var attribute_id := int(source_track.get("attributeId", -1))
			if path.is_empty() or not node_by_rig_path.has(path) or not attribute_id in [1, 3, 4]:
				continue
			var samples: Variant = source_track.get("samples", [])
			if not samples is Array or samples.is_empty():
				continue
			if not tracks.has(path):
				tracks[path] = {}
			tracks[path][attribute_id] = samples
		if not tracks.is_empty():
			animation_tracks[clip_name] = tracks


func _is_looping_clip(clip: Dictionary) -> bool:
	var mode: Variant = clip.get("wrapMode", clip.get("loopMode", ""))
	return bool(clip.get("loopTime", clip.get("loop", false))) or str(mode).to_lower().contains("loop")


func set_character_pose(clip_name: String, time: float, facing: float, hit_tint: float = 0.0) -> void:
	if not loaded:
		return
	if native_source_mode:
		scale = Vector2(-1.0 if facing<0.0 else 1.0,1.0)*wanderer_skin.display_scale
		wanderer_skin.update_pose(clip_name,time,facing)
		modulate = Color(1.0,1.0-hit_tint*0.25,1.0-hit_tint*0.25)
		return
	var is_backstep := clip_name == "BackstepWalk"
	var source_clip := "Idle" if is_backstep else clip_name
	var clip_tracks: Dictionary = animation_tracks.get(source_clip, {})
	var meta: Dictionary = animation_meta.get(source_clip, {})
	var duration := float(meta.get("duration", 0.0))
	var sample_time := time
	if duration > 0.0:
		if bool(meta.get("loop", false)):
			sample_time = fposmod(time, duration)
		else:
			sample_time = clampf(time, 0.0, duration)
	for path in node_by_rig_path.keys():
		var node: Node2D = node_by_rig_path[path]
		var base: Transform2D = rest_by_rig_path[path]
		if clip_tracks.has(path):
			base = _sample_property_curves(clip_tracks[path], sample_time, base)
		node.transform = base
	if is_backstep:
		_apply_backstep_walk(time)
	if active_visual_mode == "experiment" and wanderer_skin == null:
		for leg_path in ["/10002/Root/Body/Foot/zuotui/zuotui/tui1", "/10002/Root/Body/Foot/youtui/youtui/tui1"]:
			var leg: Node2D = node_by_rig_path.get(leg_path)
			if leg != null:
				leg.scale *= Vector2(0.80, 0.80)
				if clip_name == "Run_New1":
					var leg_rest: Transform2D = rest_by_rig_path[leg_path]
					leg.rotation = leg_rest.get_rotation() + wrapf(leg.rotation - leg_rest.get_rotation(), -PI, PI) * 0.72
	if active_visual_mode == "experiment" and wanderer_skin == null and clip_name == "Run_New1":
		var torso_path := "/10002/Root/Body/yifu/yifu/shenti2"
		var torso: Node2D = node_by_rig_path.get(torso_path)
		if torso != null:
			var torso_rest: Transform2D = rest_by_rig_path[torso_path]
			var torso_delta := wrapf(torso.rotation - torso_rest.get_rotation(), -PI, PI)
			torso.rotation = torso_rest.get_rotation() + torso_delta * 0.35
	_update_experiment_skirt_motion(clip_name, sample_time, duration, clip_tracks)
	for side in experiment_upper_leg_overlays:
		var upper: Polygon2D = experiment_upper_leg_overlays[side]
		var leg_path := "/10002/Root/Body/Foot/%s/%s/tui1" % [side, side]
		var leg: Node2D = node_by_rig_path.get(leg_path)
		if leg != null:
			var leg_delta := wrapf(leg.rotation - rest_by_rig_path[leg_path].get_rotation(), -PI, PI)
			upper.modulate.a = smoothstep(0.12, 0.45, leg_delta) if active_visual_mode == "experiment" else 0.0
	# Run_New1's carrier rotation turns the small, side-profile battle sprite away
	# from the upright arena plane. Keep its translation and scale, but let the
	# limb and robe bones carry the run cycle so the head stays visually connected.
	var run_carrier_path := "/10002/Root/Body"
	if clip_name == "Run_New1" and node_by_rig_path.has(run_carrier_path):
		var run_carrier: Node2D = node_by_rig_path[run_carrier_path]
		run_carrier.rotation = rest_by_rig_path[run_carrier_path].get_rotation()
	var facing_sign := -1.0 if facing < 0.0 else 1.0
	var is_experiment := active_visual_mode == "experiment"
	var display_scale := EXPERIMENTAL_BILAN_DISPLAY_SCALE if is_experiment else BATTLE_DISPLAY_SCALE
	var aspect_scale := Vector2(EXPERIMENTAL_BILAN_WIDTH_FACTOR, EXPERIMENTAL_BILAN_HEIGHT_FACTOR) if is_experiment else Vector2.ONE
	if is_experiment and wanderer_skin != null:
		display_scale = wanderer_skin.display_scale
		aspect_scale = Vector2.ONE
	scale = Vector2(facing_sign * display_scale * aspect_scale.x, display_scale * aspect_scale.y)
	if is_experiment and wanderer_skin == null:
		var head: Node2D = node_by_rig_path.get("/10002/Root/Body/yifu/yifu/shenti1/tou")
		var shoulder: Node2D = node_by_rig_path.get("/10002/Root/Body/yifu/yifu/shenti1/youbi/youbi/youbi1")
		if head != null and shoulder != null:
			shoulder.global_position = head.global_position + global_transform.basis_xform(Vector2(-6.0, 16.5))
	modulate = Color(1.0, 1.0 - 0.22 * clampf(hit_tint, 0.0, 1.0), 1.0 - 0.22 * clampf(hit_tint, 0.0, 1.0), 1.0)
	# Fit visual-only chains after the carrier projection and facing transform;
	# their shoulder/forearm directions are solved in the final character plane.
	if wanderer_skin != null:
		wanderer_skin.update_cloth(clip_name, sample_time, duration, clip_tracks)
	if skeleton != null:
		skeleton.force_update_transform()


# Backward gait: the foot reaches behind during swing, touches down toe first,
# then supports the body until the heel lifts. See the backward gait events in
# https://pmc.ncbi.nlm.nih.gov/articles/PMC10223441/ (Fig. 1).
func _apply_backstep_walk(time: float) -> void:
	for side_index in range(2):
		var phase := fposmod(time / 0.7 + float(side_index) * 0.5, 1.0)
		var side := "zuotui" if side_index == 0 else "youtui"
		var hip_path := "/10002/Root/Body/Foot/%s/%s/tui1" % [side, side]
		var knee_path := hip_path + "/tui2"
		var ankle_path := knee_path + "/tui3"
		var hip: Node2D = node_by_rig_path.get(hip_path)
		var knee: Node2D = node_by_rig_path.get(knee_path)
		var ankle: Node2D = node_by_rig_path.get(ankle_path)
		# Stance lasts 60% of a cycle; during swing the knee flexes for
		# clearance and extends again before the next toe contact.
		var swing := 0.0
		var hip_angle := 0.0
		if phase < 0.6:
			hip_angle = lerpf(0.25, -0.25, phase / 0.6)
		else:
			var progress := (phase - 0.6) / 0.4
			hip_angle = lerpf(-0.25, 0.25, smoothstep(0.0, 1.0, progress))
			swing = sin(progress * PI)
		if hip != null:
			hip.rotation += hip_angle
		if knee != null:
			knee.rotation += swing * 0.50
		if ankle != null:
			# Keep the sole approximately level during support, with a small
			# toe-down roll at contact and just before the heel lifts.
			var toe_roll := 0.0
			if phase < 0.6:
				toe_roll = 0.10 * (1.0 - smoothstep(0.0, 0.12, phase)) + 0.10 * smoothstep(0.48, 0.6, phase)
			else:
				toe_roll = 0.10
			ankle.rotation += -hip_angle - swing * 0.50 + toe_roll


func _update_experiment_skirt_motion(clip_name: String, sample_time: float, duration: float, clip_tracks: Dictionary) -> void:
	if experiment_skirt_front_bone == null or experiment_skirt_back_bone == null:
		return
	experiment_skirt_front_bone.rotation = 0.0
	experiment_skirt_back_bone.rotation = 0.0
	if active_visual_mode != "experiment":
		return
	var response := 0.0
	match clip_name:
		"Run_New1": response = 0.18
		"Step1": response = 0.14
		"AttackIdle": response = 0.035
		_: response = 0.015
	var near_leg_path := "/10002/Root/Body/Foot/zuotui/zuotui/tui1"
	var far_leg_path := "/10002/Root/Body/Foot/youtui/youtui/tui1"
	var near_leg: Node2D = node_by_rig_path.get(near_leg_path)
	var far_leg: Node2D = node_by_rig_path.get(far_leg_path)
	if near_leg == null or far_leg == null:
		return
	var near_rest: Transform2D = rest_by_rig_path[near_leg_path]
	var far_rest: Transform2D = rest_by_rig_path[far_leg_path]
	var near_delta := wrapf(near_leg.rotation - near_rest.get_rotation(), -PI, PI)
	var far_delta := wrapf(far_leg.rotation - far_rest.get_rotation(), -PI, PI)
	if clip_name == "Run_New1" and duration > 0.0 and clip_tracks.has(far_leg_path):
		var lagged_time := fposmod(sample_time - 0.06, duration)
		var lagged_far := _sample_property_curves(clip_tracks[far_leg_path], lagged_time, far_rest)
		far_delta = wrapf(lagged_far.get_rotation() - far_rest.get_rotation(), -PI, PI)
	# The front cloth follows the near leg; the back panel lags behind the far
	# leg. The original run curves therefore set both gait and cloth timing.
	experiment_skirt_front_bone.rotation = near_delta * response
	experiment_skirt_back_bone.rotation = -far_delta * response * 0.85


func _sample_property_curves(curves: Dictionary, time: float, fallback: Transform2D) -> Transform2D:
	var position := fallback.origin
	var scale_value := fallback.get_scale()
	var angle := fallback.get_rotation()
	if curves.has(1):
		var position_samples: Array = curves[1]
		position = Vector2(_sample_curve_component(position_samples, time, 0), -_sample_curve_component(position_samples, time, 1)) * UNITY_PIXELS_PER_UNIT
	if curves.has(3):
		var scale_samples: Array = curves[3]
		scale_value = Vector2(_sample_curve_component(scale_samples, time, 0), _sample_curve_component(scale_samples, time, 1))
	if curves.has(4):
		var rotation_samples: Array = curves[4]
		angle = -deg_to_rad(_sample_curve_component(rotation_samples, time, 2))
	var result := Transform2D(angle, position)
	result.x *= scale_value.x
	result.y *= scale_value.y
	return result


func _sample_curve_component(samples: Array, time: float, component_index: int) -> float:
	if samples.is_empty():
		return 0.0
	if time <= float(samples[0].get("time", 0.0)):
		return _sample_key_component(samples[0], component_index)
	for index in range(samples.size() - 1):
		var current: Dictionary = samples[index]
		var next: Dictionary = samples[index + 1]
		var start_time := float(current.get("time", 0.0))
		if time <= float(next.get("time", start_time)):
			var dt := time - start_time
			var coefficients: Variant = current.get("rawCoefficients", [])
			if coefficients is Array and component_index < coefficients.size() and coefficients[component_index] is Array and coefficients[component_index].size() >= 4:
				var c: Array = coefficients[component_index]
				return ((float(c[0]) * dt + float(c[1])) * dt + float(c[2])) * dt + float(c[3])
			var next_time := float(next.get("time", start_time))
			var mix := clampf((time - start_time) / maxf(0.000001, next_time - start_time), 0.0, 1.0)
			return lerpf(_sample_key_component(current, component_index), _sample_key_component(next, component_index), mix)
	return _sample_key_component(samples[-1], component_index)


func _sample_key_component(sample: Dictionary, component_index: int) -> float:
	var components: Variant = sample.get("components", [])
	if components is Array and component_index >= 0 and component_index < components.size():
		return float(components[component_index])
	return 0.0


func set_skin_mode(mode: String) -> void:
	active_visual_mode = mode if mode in ["project", "reference", "experiment"] else "project"
	active_skin = "reference" if mode == "reference" else "project"
	if native_source_mode:
		wanderer_skin.set_source_textures(mode=="reference")
		wanderer_skin.set_visible(true)
		return
	var use_custom_art := active_visual_mode == "experiment" and bone_bound_art_ready
	if wanderer_skin != null:
		wanderer_skin.set_visible(use_custom_art)
		for record in mesh_records:
			var node: Polygon2D = record.node
			node.visible = not use_custom_art
			node.texture = texture_by_sprite_id[str(record.sprite_id)][active_skin]
			node.uv = _polygon_uv_pixels(record.normalized_uvs, node.texture)
		return
	var use_weighted_experiment_parts := use_custom_art and experiment_weighted_textures.has("79450") and experiment_weighted_textures.has("61872")
	for record in mesh_records:
		var node: Polygon2D = record.node
		var sprite_id := str(record.sprite_id)
		var use_weighted_experiment_texture := use_weighted_experiment_parts and experiment_weighted_textures.has(sprite_id)
		var skirt_weights: Dictionary = record.skirt_weights
		if not skirt_weights.is_empty():
			node.clear_bones()
			var body_weights: Variant = skirt_weights.source if use_weighted_experiment_texture else record.weights
			_apply_bone_weights(node, sprite_id, record.skinning, body_weights, record.vertex_count)
			if use_weighted_experiment_texture:
				node.add_bone(skeleton.get_path_to(experiment_skirt_front_bone), skirt_weights.front)
				node.add_bone(skeleton.get_path_to(experiment_skirt_back_bone), skirt_weights.back)
				if experiment_front_overlay != null:
					experiment_front_overlay.queue_free()
				experiment_front_overlay = node.duplicate() as Polygon2D
				rig_root_node.add_child(experiment_front_overlay)
				experiment_front_overlay.texture = experiment_front_texture
				experiment_front_overlay.uv = _polygon_uv_pixels(record.normalized_uvs, experiment_front_texture)
				experiment_front_overlay.z_index = 3
				experiment_front_overlay.visible = true
			elif experiment_front_overlay != null:
				experiment_front_overlay.visible = false
		node.visible = not use_custom_art or use_weighted_experiment_texture
		var texture: Texture2D
		if use_weighted_experiment_texture:
			texture = experiment_weighted_textures[sprite_id]
			if sprite_id == "79450":
				texture = experiment_back_texture
			# Keep the shirt behind the project-made head/arms. The far leg stays
			# behind the hem; the near leg gets a small foreground layer so its
			# original three-joint gait remains readable under the split robe.
			if sprite_id == "79450":
				node.z_index = 0
			elif "/Foot/youtui/" in str(record.renderer_path):
				node.z_index = 1
			else:
				node.z_index = 2
		else:
			var textures: Dictionary = texture_by_sprite_id.get(sprite_id, {})
			texture = textures.get(active_skin)
			node.z_index = _render_layer_for_path(str(record.renderer_path)) + 40
		node.texture = texture
		if texture != null:
			node.uv = _polygon_uv_pixels(record.normalized_uvs, texture)
		if sprite_id == "61872" and use_weighted_experiment_texture:
			var side := "youtui" if "/Foot/youtui/" in str(record.renderer_path) else "zuotui"
			if not experiment_upper_leg_overlays.has(side):
				var upper := node.duplicate() as Polygon2D
				rig_root_node.add_child(upper)
				upper.texture = experiment_upper_leg_texture
				upper.uv = _polygon_uv_pixels(record.normalized_uvs, experiment_upper_leg_texture)
				upper.z_index = 4
				upper.modulate.a = 0.0
				experiment_upper_leg_overlays[side] = upper
			experiment_upper_leg_overlays[side].visible = true
		elif sprite_id == "61872":
			for upper in experiment_upper_leg_overlays.values():
				upper.visible = false
	for node in bone_bound_art_nodes:
		var part_name := str(node.name).trim_prefix("BilanArt_")
		var weighted_replacement := use_weighted_experiment_parts and (part_name in ["far_lower_leg", "near_lower_leg"] or (part_name == "robe_body" and custom_art_folder != "original_wanderer"))
		node.visible = use_custom_art and not weighted_replacement


func get_runtime_summary() -> Dictionary:
	if native_source_mode:
		return {"loaded":loaded,"rig_root_path":rig_root_path,"skeleton_transform_nodes":wanderer_skin.rig_data.nodes.size(),"source_sprite_bones":wanderer_skin.rig_data.boneNodes.size(),"meshes":wanderer_skin.meshes.size(),"bone_bound_art_ready":bone_bound_art_ready,"clips":wanderer_skin.clips.keys(),"anatomical_skin":wanderer_skin.summary(),"errors":load_errors,"native_source_mode":true}
	var weighted_meshes := 0
	var weighted_bones := 0
	for node in mesh_nodes:
		if node.get_bone_count() > 0:
			weighted_meshes += 1
			weighted_bones += node.get_bone_count()
	return {
		"loaded": loaded,
		"rig_root_path": rig_root_path,
		"skeleton_transform_nodes": skeleton.get_bone_count() if skeleton != null and skeleton.is_inside_tree() else bone_by_rig_path.size(),
		"source_sprite_bones": source_sprite_bone_paths.size(),
		"meshes": mesh_nodes.size(),
		"bone_bound_art_ready": bone_bound_art_ready,
		"bone_bound_art_parts": bone_bound_art_nodes.size(),
		"experiment_weighted_textures": experiment_weighted_textures.keys(),
		"weighted_meshes": weighted_meshes,
		"polygon_bone_bindings": weighted_bones,
		"clips": animation_tracks.keys(),
		"anatomical_skin": wanderer_skin.summary() if wanderer_skin != null else {},
		"errors": load_errors.duplicate(),
	}


func _normalize_rig_path(path: String) -> String:
	var normalized := path.strip_edges().replace("\\", "/")
	while normalized.ends_with("/") and normalized.length() > 1:
		normalized = normalized.substr(0, normalized.length() - 1)
	return normalized


func _normalize_animation_path(path: String) -> String:
	var normalized := _normalize_rig_path(path)
	if normalized.begins_with("/"):
		return normalized
	if normalized.is_empty():
		return normalized
	if normalized == rig_root_path or normalized.begins_with(rig_root_path + "/"):
		return normalized
	return rig_root_path.path_join(normalized)


func _vector2_from_value(value: Variant) -> Vector2:
	if value is Vector2:
		return value
	if value is Array and value.size() >= 2:
		return Vector2(float(value[0]), float(value[1]))
	if value is Dictionary:
		return Vector2(float(value.get("x", 0.0)), float(value.get("y", 0.0)))
	return Vector2.ZERO


func _godot_pixel_point(unity_point: Vector2) -> Vector2:
	return Vector2(unity_point.x, -unity_point.y) * UNITY_PIXELS_PER_UNIT


func _matrix_point(raw_matrix: Variant, unity_point: Vector2) -> Vector2:
	var rows: Variant = raw_matrix
	if raw_matrix is Dictionary:
		rows = raw_matrix.get("rows", raw_matrix.get("matrix", []))
	if rows is Array and rows.size() == 4 and rows[0] is Array:
		var x := float(rows[0][0]) * unity_point.x + float(rows[0][1]) * unity_point.y + float(rows[0][3])
		var y := float(rows[1][0]) * unity_point.x + float(rows[1][1]) * unity_point.y + float(rows[1][3])
		return Vector2(x, -y) * UNITY_PIXELS_PER_UNIT
	if rows is Array and rows.size() == 16:
		var x := float(rows[0]) * unity_point.x + float(rows[1]) * unity_point.y + float(rows[3])
		var y := float(rows[4]) * unity_point.x + float(rows[5]) * unity_point.y + float(rows[7])
		return Vector2(x, -y) * UNITY_PIXELS_PER_UNIT
	load_errors.append("rendererToRigRoot 矩阵格式不受支持")
	return _godot_pixel_point(unity_point)


func _transform_from_trs(raw_trs: Variant) -> Transform2D:
	if not raw_trs is Dictionary:
		return Transform2D.IDENTITY
	var position_value: Variant = raw_trs.get("position", raw_trs.get("translation", [0.0, 0.0, 0.0]))
	var scale_value: Variant = raw_trs.get("scale", [1.0, 1.0, 1.0])
	var position := _vector3_xy(position_value)
	var scale_2d := _vector2_from_value(scale_value)
	if scale_value is Array and scale_value.size() >= 2:
		scale_2d = Vector2(float(scale_value[0]), float(scale_value[1]))
	if scale_value is Dictionary:
		scale_2d = Vector2(float(scale_value.get("x", 1.0)), float(scale_value.get("y", 1.0)))
	var unity_angle := _unity_z_angle(raw_trs.get("rotation", raw_trs.get("rotationQuaternionXYZW", raw_trs.get("rotationQuaternion", raw_trs.get("euler", 0.0)))))
	var result := Transform2D(-unity_angle, Vector2(position.x, -position.y) * UNITY_PIXELS_PER_UNIT)
	result.x *= scale_2d.x
	result.y *= scale_2d.y
	return result


func _vector3_xy(value: Variant) -> Vector2:
	if value is Array and value.size() >= 2:
		return Vector2(float(value[0]), float(value[1]))
	if value is Dictionary:
		return Vector2(float(value.get("x", 0.0)), float(value.get("y", 0.0)))
	return Vector2.ZERO


func _unity_z_angle(value: Variant) -> float:
	if value is float or value is int:
		# Unity export rotations are documented as degrees when scalar.
		return deg_to_rad(float(value))
	if value is Array:
		if value.size() >= 4:
			# Unity quaternion [x, y, z, w].
			return 2.0 * atan2(float(value[2]), float(value[3]))
		if value.size() >= 3:
			return deg_to_rad(float(value[2]))
	if value is Dictionary:
		if value.has("w"):
			return 2.0 * atan2(float(value.get("z", 0.0)), float(value.get("w", 1.0)))
		if value.has("z"):
			return deg_to_rad(float(value.get("z", 0.0)))
	return 0.0


