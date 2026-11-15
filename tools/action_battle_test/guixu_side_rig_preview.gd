extends Node2D

const ART_DIR := "res://art/yuxu_guixu_side_rig_20261113"
const RIG_PATH := ART_DIR + "/rig.json"
const INCLUDE_PARTS := ["torso", "head", "hair", "sleeve_n", "sleeve_f", "mantle", "shoes_n", "shoes_f"]

var character_root: Node2D
var skeleton: Skeleton2D
var bone_nodes: Array[Bone2D] = []
var node_records: Array = []
var global_rest: Array[Transform2D] = []
var mesh_nodes: Array[Polygon2D] = []
var overlay: Node2D
var image_paths: Dictionary = {}
var character_bounds := Rect2()
var pose_mode := false


func _ready() -> void:
	print("Guixu preview boot")
	_build_canvas()
	var rig_data: Variant = JSON.parse_string(FileAccess.get_file_as_string(RIG_PATH))
	if not rig_data is Dictionary:
		push_error("Could not parse Guixu rig JSON")
		get_tree().quit(1)
		return
	node_records = rig_data.get("nodes", [])
	_build_skeleton()
	print("Guixu skeleton nodes: ", bone_nodes.size())
	_build_parts(rig_data)
	var weighted_count := 0
	for part_value in rig_data.get("parts", []):
		if bool(part_value.get("skinned", false)):
			weighted_count += 1
	print("Guixu source meshes: ", mesh_nodes.size(), " (", weighted_count, " weighted)")
	_build_waist_sash()
	_layout_actor()
	overlay = BoneOverlay.new()
	overlay.name = "BoneOverlay"
	overlay.z_index = 900
	overlay.node_records = node_records
	overlay.bone_nodes = bone_nodes
	overlay.skeleton = skeleton
	character_root.add_child(overlay)
	overlay.queue_redraw()
	queue_redraw()
	await get_tree().process_frame
	await get_tree().process_frame
	_save_preview("rigged_preview.png")
	overlay.visible = false
	await get_tree().process_frame
	_save_preview("assembled_clean.png")
	# A restrained bone change proves the imported meshes follow the original rig.
	bone_nodes[22].rotation += deg_to_rad(-4.0)
	bone_nodes[41].rotation += deg_to_rad(8.0)
	bone_nodes[42].rotation += deg_to_rad(10.0)
	bone_nodes[45].rotation += deg_to_rad(-7.0)
	bone_nodes[31].rotation += deg_to_rad(8.0)
	bone_nodes[32].rotation += deg_to_rad(6.0)
	bone_nodes[61].rotation += deg_to_rad(2.0)
	bone_nodes[62].rotation += deg_to_rad(-3.0)
	pose_mode = true
	overlay.visible = true
	overlay.queue_redraw()
	queue_redraw()
	await get_tree().process_frame
	await get_tree().process_frame
	_save_preview("bound_pose_preview.png")
	get_tree().quit(0)


func _build_canvas() -> void:
	character_root = Node2D.new()
	character_root.name = "YuxuGuixuCharacter"
	add_child(character_root)
	skeleton = Skeleton2D.new()
	skeleton.name = "GuixuMaleCloudRobeSkeleton"
	character_root.add_child(skeleton)


func _build_skeleton() -> void:
	for index in range(node_records.size()):
		var record: Dictionary = node_records[index]
		var bone := Bone2D.new()
		bone.name = str(record.get("name", "node_%02d" % index))
		bone.set_autocalculate_length_and_angle(false)
		bone.length = 0.0
		var position_value: Array = record.get("pos", [0.0, 0.0])
		var scale_value: Array = record.get("scale", [1.0, 1.0])
		# Guixu stores rig coordinates in a y-up basis; Godot's 2D canvas is y-down.
		bone.position = Vector2(float(position_value[0]), -float(position_value[1]))
		bone.rotation = -deg_to_rad(float(record.get("ang", 0.0)))
		bone.scale = Vector2(float(scale_value[0]), float(scale_value[1]))
		var parent_index := int(round(float(record.get("parent", -1))))
		var parent: Node = skeleton if parent_index < 0 else bone_nodes[parent_index]
		parent.add_child(bone)
		bone.z_index = roundi(float(record.get("z", 0.0)) * 100.0)
		bone.rest = bone.transform
		bone.bone_angle = bone.rotation
		bone_nodes.append(bone)
		var parent_transform := Transform2D.IDENTITY if parent_index < 0 else global_rest[parent_index]
		global_rest.append(parent_transform * bone.transform)


func _build_parts(rig_data: Dictionary) -> void:
	var part_records: Array = rig_data.get("parts", [])
	var draw_order: Array = rig_data.get("draw", [])
	var draw_layer_by_part: Dictionary = {}
	for draw_index in range(draw_order.size()):
		var part_index := int(draw_order[draw_index])
		if part_index >= 0 and part_index < part_records.size():
			draw_layer_by_part[str(part_records[part_index].get("name", ""))] = draw_index
	var draw_parts: Array = part_records.duplicate()
	draw_parts.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return int(draw_layer_by_part.get(str(a.get("name", "")), 0)) < int(draw_layer_by_part.get(str(b.get("name", "")), 0))
	)
	for part_value in draw_parts:
		if not part_value is Dictionary:
			continue
		var part: Dictionary = part_value
		var part_name := str(part.get("name", ""))
		if not INCLUDE_PARTS.has(part_name):
			continue
		var texture_path := ART_DIR.path_join(str(part.get("tex", part_name + ".png")))
		var texture := _load_texture(texture_path)
		if texture == null:
			push_error("Missing generated texture for " + part_name)
			continue
		image_paths[part_name] = texture_path
		var local_points: PackedVector2Array = _pair_array_to_vectors(part.get("local", []), true)
		var uv_points: PackedVector2Array = _pair_array_to_vectors(part.get("uv", []), false)
		var polygons: Array[PackedInt32Array] = _triangles(part.get("tris", []))
		if local_points.is_empty() or local_points.size() != uv_points.size():
			push_error("Bad vertex/UV pair for " + part_name)
			continue
		var mesh := Polygon2D.new()
		mesh.name = "Part_" + part_name
		mesh.texture = texture
		mesh.polygon = local_points
		mesh.uv = uv_points
		mesh.polygons = polygons
		mesh.z_as_relative = false
		mesh.z_index = int(draw_layer_by_part.get(part_name, 0)) * 10
		mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		mesh.texture_repeat = CanvasItem.TEXTURE_REPEAT_DISABLED
		var holder_index := int(part.get("holder", 0))
		if bool(part.get("skinned", false)):
			var holder_transform: Transform2D = global_rest[holder_index]
			var root_points := PackedVector2Array()
			for point in local_points:
				root_points.append(holder_transform * point)
			mesh.polygon = root_points
			character_root.add_child(mesh)
			mesh.skeleton = mesh.get_path_to(skeleton)
			_apply_weights(mesh, part)
		else:
			bone_nodes[holder_index].add_child(mesh)
		mesh_nodes.append(mesh)


func _apply_weights(mesh: Polygon2D, part: Dictionary) -> void:
	var bone_indices: Array = part.get("bones", [])
	var influence_bones: Array = part.get("bi", [])
	var influence_weights: Array = part.get("w", [])
	var vertex_count: int = mesh.polygon.size()
	if influence_bones.size() != vertex_count * 4 or influence_weights.size() != vertex_count * 4:
		push_error("Bad source weights for " + str(part.get("name", "part")))
		return
	for local_bone_index in range(bone_indices.size()):
		var weights := PackedFloat32Array()
		weights.resize(vertex_count)
		for vertex_index in range(vertex_count):
			var sum := 0.0
			for influence_index in range(4):
				var offset := vertex_index * 4 + influence_index
				if int(round(float(influence_bones[offset]))) == local_bone_index:
					sum += float(influence_weights[offset])
			weights[vertex_index] = sum
		var source_node_index := int(bone_indices[local_bone_index])
		mesh.add_bone(skeleton.get_path_to(bone_nodes[source_node_index]), weights)


func _build_waist_sash() -> void:
	var texture := _load_texture(ART_DIR.path_join("waist_sash.png"))
	if texture == null:
		return
	var sprite := Sprite2D.new()
	sprite.name = "Part_waist_sash"
	sprite.texture = texture
	sprite.centered = false
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	sprite.texture_repeat = CanvasItem.TEXTURE_REPEAT_DISABLED
	sprite.scale = Vector2(0.105, 0.105)
	sprite.position = Vector2(-18.0, -8.0)
	sprite.z_as_relative = false
	sprite.z_index = 800
	bone_nodes[1].add_child(sprite)
	image_paths["waist_sash"] = ART_DIR.path_join("waist_sash.png")


func _layout_actor() -> void:
	var points: Array[Vector2] = []
	for mesh in mesh_nodes:
		for point in mesh.polygon:
			points.append(character_root.to_local(mesh.to_global(point)))
	if points.is_empty():
		return
	var left := INF
	var right := -INF
	var top := INF
	var bottom := -INF
	for point in points:
		left = minf(left, point.x)
		right = maxf(right, point.x)
		top = minf(top, point.y)
		bottom = maxf(bottom, point.y)
	character_bounds = Rect2(Vector2(left, top), Vector2(right - left, bottom - top))
	var target_height := 440.0
	var scale_factor := target_height / maxf(character_bounds.size.y, 1.0)
	character_root.scale = Vector2.ONE * scale_factor
	character_root.position = Vector2(435.0, 618.0) - Vector2(character_bounds.position.x, character_bounds.end.y) * scale_factor


func _load_texture(path: String) -> Texture2D:
	var image := Image.new()
	if image.load(ProjectSettings.globalize_path(path)) != OK:
		return null
	return ImageTexture.create_from_image(image)


func _pair_array_to_vectors(values: Array, flip_y: bool) -> PackedVector2Array:
	var result := PackedVector2Array()
	for index in range(0, values.size(), 2):
		if index + 1 >= values.size():
			break
		var y := float(values[index + 1])
		result.append(Vector2(float(values[index]), -y if flip_y else y))
	return result


func _triangles(values: Array) -> Array[PackedInt32Array]:
	var result: Array[PackedInt32Array] = []
	for index in range(0, values.size(), 3):
		if index + 2 >= values.size():
			break
		result.append(PackedInt32Array([int(values[index]), int(values[index + 1]), int(values[index + 2])]))
	return result


func _save_preview(file_name: String) -> void:
	var output_dir := ProjectSettings.globalize_path(ART_DIR)
	var output_path := output_dir.path_join(file_name)
	var error := get_viewport().get_texture().get_image().save_png(output_path)
	if error != OK:
		push_error("Unable to save preview: " + output_path)
	else:
		print("Guixu rig preview: ", output_path)


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, Vector2(1280, 720)), Color("edf0f2"), true)
	draw_rect(Rect2(24, 96, 840, 580), Color("ffffff"), true)
	draw_rect(Rect2(24, 96, 840, 580), Color("cbd3d9"), false, 2.0)
	draw_line(Vector2(52, 619), Vector2(835, 619), Color("d3d9dd"), 2.0)
	var font := ThemeDB.fallback_font
	draw_string(font, Vector2(32, 43), "玉虚内门 · 归墟行侧面骨骼绑定", HORIZONTAL_ALIGNMENT_LEFT, -1, 30, Color("24333d"))
	var pose_text := "原骨骼轻微摆位示例" if pose_mode else "骨骼静置绑定"
	draw_string(font, Vector2(34, 72), "新生成部件 / 原包 67 节点骨架 / 7 个权重网格 + 肩饰与腰带骨挂件 / " + pose_text, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color("5c6b75"))
	draw_string(font, Vector2(60, 645), "淡蓝线：上身与袖骨    金线：发束骨    青线：衣摆骨    紫线：鞋骨", HORIZONTAL_ALIGNMENT_LEFT, -1, 17, Color("53636d"))
	draw_rect(Rect2(900, 96, 350, 580), Color("ffffff"), true)
	draw_rect(Rect2(900, 96, 350, 580), Color("cbd3d9"), false, 2.0)
	draw_string(font, Vector2(930, 139), "本次拆件", HORIZONTAL_ALIGNMENT_LEFT, -1, 24, Color("24333d"))
	var labels := ["头部轮廓", "守真发型（含发带）", "闭合长袍", "肩部外层", "腰带与玉饰", "近侧袖", "远侧袖", "布靴 × 2"]
	for index in range(labels.size()):
		var y := 190.0 + float(index) * 42.0
		draw_circle(Vector2(937, y - 5), 4.0, Color("5c8c9a"))
		draw_string(font, Vector2(956, y), labels[index], HORIZONTAL_ALIGNMENT_LEFT, -1, 19, Color("3d4d57"))
	draw_string(font, Vector2(930, 618), "其余原骨骼保留供动作驱动", HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color("7c898f"))


class BoneOverlay extends Node2D:
	var node_records: Array = []
	var bone_nodes: Array[Bone2D] = []
	var skeleton: Skeleton2D
	var part_bones := {
		"hair": {29: true, 30: true, 31: true, 32: true, 33: true, 34: true, 35: true},
		"sleeve": {40: true, 41: true, 42: true, 44: true, 45: true, 46: true, 50: true, 51: true, 52: true, 54: true, 55: true, 56: true},
		"hem": {21: true, 22: true, 60: true, 61: true, 62: true, 63: true},
		"shoes": {4: true, 5: true, 6: true, 7: true, 12: true, 13: true, 14: true, 15: true}
	}

	func _draw() -> void:
		for index in range(node_records.size()):
			var record: Dictionary = node_records[index]
			var parent_index := int(round(float(record.get("parent", -1))))
			if parent_index < 0 or parent_index >= bone_nodes.size():
				continue
			var color := _color_for(index)
			if color.a <= 0.0:
				continue
			var child_point := to_local(bone_nodes[index].global_position)
			var parent_point := to_local(bone_nodes[parent_index].global_position)
			if child_point.distance_to(parent_point) < 1.5:
				continue
			draw_line(parent_point, child_point, color, 1.65, true)
			draw_circle(child_point, 2.5, Color(color.r, color.g, color.b, 0.95))

	func _color_for(index: int) -> Color:
		for index_value in part_bones.hair:
			if int(index_value) == index:
				return Color(0.86, 0.62, 0.30, 0.85)
		for index_value in part_bones.sleeve:
			if int(index_value) == index:
				return Color(0.34, 0.67, 0.76, 0.82)
		for index_value in part_bones.hem:
			if int(index_value) == index:
				return Color(0.38, 0.75, 0.69, 0.78)
		for index_value in part_bones.shoes:
			if int(index_value) == index:
				return Color(0.64, 0.55, 0.76, 0.78)
		return Color(0.0, 0.0, 0.0, 0.0)
