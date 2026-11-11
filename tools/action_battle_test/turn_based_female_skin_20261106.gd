extends "guigu_native_first_skin.gd"

## Female basic appearance on the shared movement rig. Male assets are untouched.
const NATIVE_ART := "textures/characters/turn_based_pair_20261106/female_native_v3"
var female_ready := false
var head_record: Dictionary
var back_record: Dictionary
var front_hair_mesh: Polygon2D

func build(owner_runtime: Node2D, art_dir: String = "") -> bool:
	if not super.build(owner_runtime,art_dir): return false
	var root := ProjectSettings.globalize_path(_folder()).path_join("../..").simplify_path()
	var folder := root.path_join(NATIVE_ART)
	var front := Image.load_from_file(folder.path_join("hair_front.png"))
	if front == null or front.get_size()!=Vector2i(21,56):
		validation_errors.append("Female front hair must keep the male 21x56 canvas")
		return false
	front.generate_mipmaps()
	front_hair_mesh = Polygon2D.new()
	front_hair_mesh.name = "FemaleFrontHairOnly"
	front_hair_mesh.texture = ImageTexture.create_from_image(front)
	front_hair_mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	front_hair_mesh.z_index = 45
	runtime.rig_root_node.add_child(front_hair_mesh)
	meshes.append(front_hair_mesh)
	for record in mesh_records:
		var file := "hair_back.png" if int(record.sprite_id)==87679 else "sprite_%d.png"%int(record.sprite_id)
		var image := Image.load_from_file(folder.path_join(file))
		if image == null or image.get_size()!=Vector2i(record.texture_size):
			validation_errors.append("Female part canvas differs from male source: "+file)
			return false
		image.generate_mipmaps()
		record.paint_texture = ImageTexture.create_from_image(image)
		record.paint_path = folder.path_join(file)
		record.mesh.texture = record.paint_texture
		if int(record.sprite_id) == 65806:
			head_record = record
			# Use exactly the same independent 24x24 base head, UVs and neck
			# transform as the existing male; female identity lives in the hair.
			record.mesh.texture = record.paint_texture
			record.mesh.z_index = 44
		elif int(record.sprite_id) == 87679:
			back_record = record
			record.mesh.z_index = 20
	female_ready = true
	_finish_pose(0.0,"Idle",0.0)
	return true

func _hair_pixel(pixel: Vector2) -> Vector2:
	var uv: Array = back_record.source.uvConvention.sourceUvTransform
	var transform: Transform3D = world_transforms[back_record.source.rendererPath]
	var local := Vector3((pixel.x-float(uv[1]))/float(uv[0]),(56.0-pixel.y-float(uv[3]))/float(uv[2]),0.0)
	var point := transform*local
	return Vector2(point.x,-point.y)*PIXELS_PER_UNIT-ground_offset

func _render_mesh(record: Dictionary) -> void:
	if not female_ready or int(record.sprite_id) != 87679:
		super._render_mesh(record)
		return
	_draw_hair(record.mesh,true)
	_draw_hair(front_hair_mesh,false)

func _draw_hair(mesh: Polygon2D, back: bool) -> void:
	var points := PackedVector2Array()
	var texcoords := PackedVector2Array()
	var triangles: Array[PackedInt32Array] = []
	var rows := 10 if back else 1
	for row in range(rows+1):
		var t := float(row)/rows
		for col in range(2):
			var pixel := Vector2(21*col,56*t)
			if back:
				pixel.x += sin(sampled_time*2.0-t*2.2)*0.22*t*t
			points.append(_hair_pixel(pixel))
			texcoords.append(Vector2(21*col,56*t))
		if row<rows:
			var i := row*2
			triangles.append(PackedInt32Array([i,i+1,i+3]))
			triangles.append(PackedInt32Array([i,i+3,i+2]))
	mesh.polygon = points
	mesh.uv = texcoords
	mesh.polygons = triangles

func summary() -> Dictionary:
	var result := super.summary()
	result["appearance_gender"] = "female"
	result["appearance_art"] = NATIVE_ART
	result["hairstyle"] = "清簪垂鬓 + 清簪半束"
	result["female_native_skeleton"] = false
	result["base_head"] = "same original male 24x24 head texture/mesh/UV/neck transform; separate front/back female hair"
	result["hair_native_canvas"] = [21,56]
	result["shared_movement_rig"] = "male101, reused for a basic female costume; not a female source-rig reconstruction"
	return result
