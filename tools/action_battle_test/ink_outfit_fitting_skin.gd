extends "guigu_model_copy_skin.gd"

## Isolated clothing prototype. Only the source crop-to-full-canvas affine is
## used; no previous HD registration, paint atlas or source-alpha fallback.
var template_path := ""
var outfit_material_paths: Dictionary = {}
var template_document: Dictionary = {}
var original_pose_document: Dictionary = {}
var selected_source_mesh_indices: Array[int] = []
var material_diagnostics: Dictionary = {}
var neutral_head: Node2D
var fixed_head: Sprite2D
var outfit_art_dir := ""
var closed_robe_carrier := true
var body_carrier_extension: Dictionary = {}
var near_hand_carrier_extension: Dictionary = {}
var hem_secondary_enabled := true
var hem_secondary: RefCounted
var sleeve_reconstruction_enabled := true
var sleeve_reconstruction: RefCounted
var leg_reconstruction_enabled := true
var leg_reconstruction: RefCounted
var sleeve_author_overrides: Dictionary = {}
var share_sleeve_master := true
var pants_waist_material: ShaderMaterial
var pants_waist_point := Vector2.ZERO
var pants_up_normal := Vector2.UP
var hand_variants_enabled := true
var require_hand_variants := true
var hand_variant_paths: Dictionary = {}
var hand_variant_textures: Dictionary = {}
var hand_variant_diagnostics: Dictionary = {}
var hand_materials: Dictionary = {}
var active_hand_poses: Dictionary = {}
var hand_pose_override := ""
var hand_regions := {
	"sleeve_near":{"cut_v":0.697,"edge_a":Vector2(458,925)/1254.0,"edge_b":Vector2(710,865)/1254.0,"feather":1.0/1254.0,"skin_margin":1.0/1254.0},
	"sleeve_far":{"cut_v":0.697,"edge_a":Vector2(458,925)/1254.0,"edge_b":Vector2(710,865)/1254.0,"feather":1.0/1254.0,"skin_margin":1.0/1254.0}}

const HAND_REGION_SHADER := """shader_type canvas_item;
render_mode unshaded;
uniform sampler2D hand_variant : filter_linear_mipmap, repeat_disable;
uniform bool variant_enabled = false;
uniform float wrist_cut_v = 0.662;
uniform float feather = 0.003;
uniform bool slanted_cuff = false;
uniform vec2 edge_a = vec2(0.0);
uniform vec2 edge_b = vec2(1.0,0.0);
uniform float skin_margin = 0.0;
uniform float hand_size = 1.0;
uniform vec2 hand_source_offset = vec2(0.0);
void fragment() {
	vec4 base = texture(TEXTURE,UV);
	if (variant_enabled) {
		float amount = smoothstep(wrist_cut_v-feather,wrist_cut_v+feather,UV.y);
		if (slanted_cuff) {
			vec2 edge = edge_b-edge_a;
			vec2 relative = UV-edge_a;
			float distance = (edge.x*relative.y-edge.y*relative.x)/max(length(edge),0.00001);
			amount = smoothstep(skin_margin,skin_margin+feather,distance);
		}
		// Register palms, not full silhouettes: an extended finger remains longer.
		float wrist_blend = smoothstep(890.0/1254.0,925.0/1254.0,UV.y);
		vec2 wrist = vec2(610.0,890.0)/1254.0;
		// Keep length while giving palms and curled digits a fuller small-sprite silhouette.
		vec2 hand_dimensions = vec2(hand_size);
		vec2 hand_uv = wrist + (UV-wrist)/mix(vec2(1.0),hand_dimensions,wrist_blend) + hand_source_offset*wrist_blend;
		COLOR = mix(base,texture(hand_variant,hand_uv),amount);
	} else COLOR = base;
	// Fade only the buried shoulder cap, eliminating the outlined joint ring.
	COLOR.a *= smoothstep(170.0/1254.0,280.0/1254.0,UV.y);
}
"""

const PANTS_WAIST_SHADER := """shader_type canvas_item;
render_mode unshaded;
uniform vec2 waist_point = vec2(0.0);
uniform vec2 up_normal = vec2(0.0,-1.0);
varying vec2 rig_local_point;
void vertex() {
	rig_local_point = VERTEX;
}
void fragment() {
	vec4 pixel = texture(TEXTURE,UV);
	float knee_zone = smoothstep(0.42,0.46,UV.y)*(1.0-smoothstep(0.59,0.61,UV.y));
	vec4 nearby = pixel;
	for (int index=0; index<8; index++) {
		float angle = 6.2831853*float(index)/8.0;
		vec4 neighbour = texture(TEXTURE,UV+vec2(cos(angle),sin(angle))*TEXTURE_PIXEL_SIZE*8.0);
		if (neighbour.a>nearby.a) nearby = neighbour;
	}
	pixel = mix(pixel,nearby,knee_zone*(1.0-smoothstep(0.40,0.90,pixel.a)));
	float depth = -dot(rig_local_point-waist_point,up_normal);
	vec2 across = vec2(-up_normal.y,up_normal.x);
	float lateral = abs(dot(rig_local_point-waist_point,across));
	// A narrow opaque insertion widens under the belt. Cross-fading a whole
	// thigh back in over depth 6..10 created a conspicuous translucent band.
	// A lifted knee may pass the waist's projected plane. Restrict insertion
	// clipping to the painted hip rows, so it cannot sever a bent knee/boot.
	float hip_release = smoothstep(0.23,0.32,UV.y);
	float root_width = 3.0+max(depth,0.0)*1.75+hip_release*200.0;
	float root_depth = depth+hip_release*50.0;
	pixel.a *= smoothstep(0.0,0.25,root_depth)*(1.0-smoothstep(root_width-0.20,root_width,lateral));
	COLOR = pixel;
}
"""

class NeutralHead extends Node2D:
	var contour := PackedVector2Array()
	func _draw() -> void:
		if contour.size()<3: return
		draw_colored_polygon(contour,Color("c5c1b5"))
		var edge := contour.duplicate()
		edge.append(contour[0])
		draw_polyline(edge,Color("676b67"),0.42,true)

func _component_key(record: Dictionary) -> String:
	match str(record.category):
		"body": return "body"
		"leg": return "leg"
		"sleeve": return "sleeve_near" if str(record.rendererPath).contains("/youbi/") else "sleeve_far"
	return ""

func _full_canvas_uv(component: Dictionary, crop_uv: Vector2) -> Vector2:
	var rows: Array = component.cropUvToFullCanvasNormalized
	return Vector2(float(rows[0][0])*crop_uv.x+float(rows[0][1])*crop_uv.y+float(rows[0][2]),float(rows[1][0])*crop_uv.x+float(rows[1][1])*crop_uv.y+float(rows[1][2]))

func _load_outfit_image(key: String, path: String) -> Texture2D:
	if not FileAccess.file_exists(path):
		validation_errors.append("Missing first-outfit material "+key+": "+path)
		return null
	var source := Image.new()
	if source.load(ProjectSettings.globalize_path(path))!=OK:
		validation_errors.append("Missing first-outfit material "+key+": "+path)
		return null
	var component: Dictionary = template_document.components[key]
	var canvas: Array = component.templateCanvasSize
	material_diagnostics[key] = {"path":path,"native_size":[source.get_width(),source.get_height()],"template_size":canvas,"native_per_template_x":float(source.get_width())/float(canvas[0]),"native_per_template_y":float(source.get_height())/float(canvas[1]),"source_canvas":component.sourceCanvasSize,"target_ppu":template_document.targetPPU,"mapping":"source crop to complete Sprite canvas affine; no local registration"}
	source.generate_mipmaps()
	return ImageTexture.create_from_image(source)

func _filter_clothing_frames(raw: Dictionary) -> Dictionary:
	var filtered := raw.duplicate(false)
	var selected := []
	for index in range(raw.meshes.size()):
		var record: Dictionary = raw.meshes[index]
		if _component_key(record).is_empty(): continue
		selected_source_mesh_indices.append(index)
		selected.append(record)
	filtered.meshes = selected
	filtered.clips = {}
	for clip_name in raw.clips:
		var clip: Dictionary = raw.clips[clip_name].duplicate(false)
		for field in ["frames","secondaryFrames"]:
			if not clip.has(field): continue
			var frames := []
			for frame in clip[field]:
				var parts := []
				for index in selected_source_mesh_indices: parts.append(frame[index])
				frames.append(parts)
			clip[field] = frames
		filtered.clips[clip_name] = clip
	return filtered

func _extend_closed_robe_carrier() -> bool:
	if not closed_robe_carrier: return true
	var body_index := -1
	for index in range(source_document.meshes.size()):
		if str(source_document.meshes[index].category)=="body": body_index = index
	if body_index<0: return false
	var original: Dictionary = source_document.meshes[body_index]
	if original.uv.size()!=40:
		validation_errors.append("Closed robe extension requires the verified male101 40-vertex body")
		return false
	var matrix: Array = template_document.components.body.cropUvToFullCanvasNormalized
	var affine := Transform2D(Vector2(float(matrix[0][0]),float(matrix[1][0])),Vector2(float(matrix[0][1]),float(matrix[1][1])),Vector2(float(matrix[0][2]),float(matrix[1][2])))
	var full_canvas := Vector2(12.2,94.5)
	var crop_uv := affine.affine_inverse()*(full_canvas/Vector2(60,100))
	var basis := [32,31,35]
	var a := Vector2(float(original.uv[basis[0]][0]),float(original.uv[basis[0]][1]))
	var b := Vector2(float(original.uv[basis[1]][0]),float(original.uv[basis[1]][1]))
	var c := Vector2(float(original.uv[basis[2]][0]),float(original.uv[basis[2]][1]))
	var barycentric := Transform2D(b-a,c-a,a).affine_inverse()*crop_uv
	var weights := [1.0-barycentric.x-barycentric.y,barycentric.x,barycentric.y]
	var extended: Dictionary = original.duplicate(false)
	extended.uv = original.uv.duplicate(false)
	extended.uv.append([crop_uv.x,crop_uv.y])
	extended.triangles = original.triangles.duplicate(false)
	var new_triangles := [[30,31,40],[31,32,40],[32,33,40]]
	for triangle in new_triangles: extended.triangles.append(triangle)
	source_document.meshes[body_index] = extended
	# This is an engineered carrier extension in the same full-canvas UV space.
	# It follows the local affine deformation of the adjacent source cloth, not
	# a new claimed source bone/vertex. Existing 40 source vertices stay exact.
	for clip in source_document.clips.values():
		for field in ["frames","secondaryFrames"]:
			if not clip.has(field): continue
			for frame in clip[field]:
				var vertices: Array = frame[body_index].duplicate(false)
				var point := Vector2.ZERO
				for index in range(3):
					var source: Array = vertices[basis[index]]
					point += Vector2(float(source[0]),float(source[1]))*float(weights[index])
				vertices.append([point.x,point.y])
				frame[body_index] = vertices
	body_carrier_extension = {"purpose":"cover the new continuous rear hem; original mesh had no topological slit","original_vertex_count":40,"carrier_vertex_count":41,"original_triangle_count":43,"carrier_triangle_count":46,"new_vertex":40,"full_canvas_px":[full_canvas.x,full_canvas.y],"source_basis":basis,"affine_weights":weights,"added_triangles":new_triangles,"original_vertices_unchanged":true,"original_source_document_unchanged":true,"motion":"engineered local affine extension of adjacent evaluated source cloth","uv":"unchanged crop-to-full-canvas affine"}
	return true

func _extend_near_hand_carrier() -> bool:
	if not hand_variants_enabled: return true
	var near_index := -1
	for index in range(source_document.meshes.size()):
		if _component_key(source_document.meshes[index])=="sleeve_near": near_index = index
	if near_index<0: return false
	var original: Dictionary = source_document.meshes[near_index]
	if original.uv.size()!=35:
		validation_errors.append("Near open-hand carrier requires the verified 35-vertex source arm")
		return false
	var matrix: Array = template_document.components.sleeve_near.cropUvToFullCanvasNormalized
	var affine := Transform2D(Vector2(float(matrix[0][0]),float(matrix[1][0])),Vector2(float(matrix[0][1]),float(matrix[1][1])),Vector2(float(matrix[0][2]),float(matrix[1][2])))
	var full_canvas := Vector2(26.5,48.5)
	var crop_uv := affine.affine_inverse()*(full_canvas/60.0)
	var basis := [17,16,0]
	var a := Vector2(float(original.uv[basis[0]][0]),float(original.uv[basis[0]][1]))
	var b := Vector2(float(original.uv[basis[1]][0]),float(original.uv[basis[1]][1]))
	var c := Vector2(float(original.uv[basis[2]][0]),float(original.uv[basis[2]][1]))
	var local := Transform2D(b-a,c-a,a).affine_inverse()*crop_uv
	var weights := [1.0-local.x-local.y,local.x,local.y]
	var extended: Dictionary = original.duplicate(false)
	extended.uv = original.uv.duplicate(false)
	extended.uv.append([crop_uv.x,crop_uv.y])
	extended.triangles = original.triangles.duplicate(false)
	var triangles := [[15,16,35],[16,17,35]]
	for triangle in triangles: extended.triangles.append(triangle)
	source_document.meshes[near_index] = extended
	for clip in source_document.clips.values():
		for field in ["frames","secondaryFrames"]:
			if not clip.has(field): continue
			for frame in clip[field]:
				var vertices: Array = frame[near_index].duplicate(false)
				var point := Vector2.ZERO
				for index in range(3):
					var source: Array = vertices[basis[index]]
					point += Vector2(float(source[0]),float(source[1]))*float(weights[index])
				vertices.append([point.x,point.y])
				frame[near_index] = vertices
	near_hand_carrier_extension = {"purpose":"preserve the full natural open fingers beyond the old short source hand silhouette","original_vertex_count":35,"carrier_vertex_count":36,"original_triangle_count":47,"carrier_triangle_count":49,"new_vertex":35,"full_canvas_px":[full_canvas.x,full_canvas.y],"source_basis":basis,"affine_weights":weights,"added_triangles":triangles,"original_vertices_unchanged":true,"wrist_and_sleeve_unchanged":true,"original_source_document_unchanged":true,"motion":"local affine extension of the adjacent source hand segment"}
	return true

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	runtime = owner_runtime
	outfit_art_dir = art_dir
	rounded_shoulder_caps = false
	require_registered_hd = false
	use_secondary_frames = false
	if source_data_path.is_empty(): source_data_path = ProjectSettings.globalize_path(_base_dir()).path_join("../../resources/appearance/guigu_male101/model_frames_20261104.json").simplify_path()
	if template_path.is_empty(): template_path = art_dir.path_join("assets/template/fullcanvas_template_anchors.json")
	if not FileAccess.file_exists(template_path):
		validation_errors.append("Missing full-canvas outfit template: "+template_path)
		return false
	var template: Variant = JSON.parse_string(FileAccess.get_file_as_string(template_path))
	if not template is Dictionary or not template.has("components"):
		validation_errors.append("Invalid full-canvas outfit template")
		return false
	template_document = template
	if shared_sources.has(source_data_path): original_pose_document = shared_sources[source_data_path]
	else:
		if not FileAccess.file_exists(source_data_path):
			validation_errors.append("Missing verified source-pose data: "+source_data_path)
			return false
		var raw: Variant = JSON.parse_string(FileAccess.get_file_as_string(source_data_path))
		if not raw is Dictionary or not raw.has("clips"):
			validation_errors.append("Invalid source-pose document")
			return false
		original_pose_document = raw
		shared_sources[source_data_path] = original_pose_document
	# Establish one size/foot ruler using the complete source figure before
	# selecting clothing. Cropping off the head must not enlarge the clothes.
	source_document = original_pose_document
	_establish_fixed_envelope()
	source_document = _filter_clothing_frames(original_pose_document)
	if not _extend_closed_robe_carrier(): return false
	if not _extend_near_hand_carrier(): return false
	var native_textures := {}
	for key in ["body","sleeve_near","sleeve_far","leg"]:
		if not template_document.components.has(key):
			validation_errors.append("Template has no "+key)
			return false
		var path := str(outfit_material_paths.get(key,art_dir.path_join("assets").path_join(key+".png")))
		if key=="sleeve_far" and share_sleeve_master and hand_variants_enabled:
			path = str(outfit_material_paths.get("sleeve_near",art_dir.path_join("assets/sleeve_near.png")))
		var native := _load_outfit_image(key,path)
		if native==null: return false
		native_textures[key] = native
	if share_sleeve_master and hand_variants_enabled:
		native_textures.sleeve_far = native_textures.sleeve_near
	var idle: Array = source_document.clips.Idle.frames[0]
	for index in range(source_document.meshes.size()):
		var record: Dictionary = source_document.meshes[index]
		var key := _component_key(record)
		var component: Dictionary = template_document.components[key]
		var native: Texture2D = native_textures[key]
		var mesh := Polygon2D.new()
		mesh.name = "InkOutfit01_"+key+"_"+str(index)
		mesh.texture = native
		mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		mesh.z_index = 30+selected_source_mesh_indices[index]
		var uv := PackedVector2Array()
		for value in record.uv: uv.append(_full_canvas_uv(component,Vector2(float(value[0]),float(value[1])))*native.get_size())
		mesh.uv = uv
		var triangles := []
		for tri in record.triangles: triangles.append(PackedInt32Array(tri))
		mesh.polygons = triangles
		var vertices := PackedVector2Array()
		for point in idle[index]: vertices.append(_map_point(point))
		mesh.polygon = vertices
		runtime.rig_root_node.add_child(mesh)
		meshes.append(mesh)
		mesh_records.append({"mesh":mesh,"source":record,"category":record.category,"component":key,"source_mesh_index":selected_source_mesh_indices[index]})
		current_vertices.append(vertices)
	_build_source_attachments()
	var idle_data: Dictionary = source_document.clips.Idle
	for index in range(source_bone_nodes.size()):
		source_bone_nodes[index].position = _map_point(idle_data.boneFrames[0][index])
		source_bone_nodes[index].rotation = float(idle_data.boneTransforms[0][index][2])
	sampled_source_clip = "Idle"
	_build_pants_waist_mask()
	leg_reconstruction = load(get_script().resource_path.get_base_dir().path_join("ink_outfit_leg_mesh.gd")).new()
	if not leg_reconstruction.build(self):
		validation_errors.append("Cannot build fixed-size legs: "+str(leg_reconstruction.errors))
		return false
	leg_reconstruction.set_enabled(leg_reconstruction_enabled)
	if not _build_hand_variants(): return false
	hem_secondary = load(get_script().resource_path.get_base_dir().path_join("ink_outfit_hem_secondary.gd")).new()
	if not hem_secondary.build(self):
		validation_errors.append("Cannot build continuous robe secondary layer")
		return false
	hem_secondary.set_enabled(hem_secondary_enabled)
	if sleeve_reconstruction_enabled:
		sleeve_reconstruction = load(get_script().resource_path.get_base_dir().path_join("ink_outfit_sleeve_mesh.gd")).new()
		if not sleeve_reconstruction.build(self,sleeve_author_overrides):
			validation_errors.append("Cannot build continuous authored sleeves: "+str(sleeve_reconstruction.errors))
			return false
	_build_neutral_head()
	_build_fixed_head()
	contact_shadow = SourceContactShadow.new()
	contact_shadow.name = "OutfitMannequinFootShadow"
	contact_shadow.z_index = 1
	runtime.rig_root_node.add_child(contact_shadow)
	return meshes.size()==5 and validation_errors.is_empty()

func _build_pants_waist_mask() -> void:
	var shader := Shader.new()
	shader.code = PANTS_WAIST_SHADER
	pants_waist_material = ShaderMaterial.new()
	pants_waist_material.shader = shader
	for record in mesh_records:
		if str(record.category)=="leg":
			record.mesh.material = pants_waist_material
	_update_pants_waist_mask()

func _update_pants_waist_mask() -> void:
	if pants_waist_material==null or not bones.has("pelvis") or not bones.has("head"): return
	pants_waist_point = bones.pelvis.position
	pants_up_normal = (bones.head.position-bones.pelvis.position).normalized()
	pants_waist_material.set_shader_parameter("waist_point",pants_waist_point)
	pants_waist_material.set_shader_parameter("up_normal",pants_up_normal)

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	if motion_context and motion_delta<=0.0 and not last_clip.is_empty(): return
	super.update_cloth(clip,time,duration,tracks)
	_update_pants_waist_mask()
	_apply_hand_variants()
	if leg_reconstruction!=null:
		leg_reconstruction.set_enabled(leg_reconstruction_enabled)
		leg_reconstruction.update_pose(motion_delta if motion_context else 0.0,sampled_source_clip)
	if sleeve_reconstruction!=null:
		sleeve_reconstruction.set_enabled(sleeve_reconstruction_enabled)
		sleeve_reconstruction.update_pose(motion_delta if motion_context else 0.0,sampled_source_clip)
	if hem_secondary!=null:
		hem_secondary.set_enabled(hem_secondary_enabled)
		hem_secondary.update_pose(motion_delta if motion_context else 0.0,sampled_source_clip)

func is_leg_pixel_below_waist(rig_local_point: Vector2) -> bool:
	return (rig_local_point-pants_waist_point).dot(pants_up_normal)<=0.0

func _build_hand_variants() -> bool:
	if not hand_variants_enabled: return true
	var shader := Shader.new()
	shader.code = HAND_REGION_SHADER
	for side in ["sleeve_near","sleeve_far"]:
		hand_variant_textures[side] = {}
		hand_variant_diagnostics[side] = {}
		for style in ["open","fist","sword","grip"]:
			if style=="open" and side=="sleeve_far" and not share_sleeve_master: continue
			if style in ["sword","grip"] and side=="sleeve_far": continue
			var paths: Dictionary = hand_variant_paths.get(side,{})
			var filename: String = side+".png" if style=="open" else side+"_"+style+".png"
			var path := str(paths.get(style,outfit_art_dir.path_join("assets").path_join(filename)))
			if not FileAccess.file_exists(path):
				if require_hand_variants and style in ["open","fist"]: validation_errors.append("Missing normal-hand variant: "+path)
				continue
			var image := Image.new()
			if image.load(ProjectSettings.globalize_path(path))!=OK:
				validation_errors.append("Cannot load hand variant: "+path)
				continue
			hand_variant_diagnostics[side][style] = {"path":path,"native_size":[image.get_width(),image.get_height()],"sha256":FileAccess.get_sha256(path),"sampling":"normalized complete-canvas UV, independent of base PNG pixel dimensions","scope":"wrist and hand only; shoulder and sleeve stay on the open base material"}
			image.generate_mipmaps()
			hand_variant_textures[side][style] = ImageTexture.create_from_image(image)
		var material := ShaderMaterial.new()
		material.shader = shader
		hand_materials[side] = material
		for record in mesh_records:
			if record.component==side: record.mesh.material = material
	configure_hand_regions({})
	_apply_hand_variants()
	return validation_errors.is_empty()

func configure_hand_regions(regions: Dictionary) -> void:
	for side in regions:
		if hand_regions.has(side): hand_regions[side].merge(regions[side],true)
	for side in hand_materials:
		hand_materials[side].set_shader_parameter("wrist_cut_v",float(hand_regions[side].cut_v))
		hand_materials[side].set_shader_parameter("feather",float(hand_regions[side].feather))
		var slanted: bool = hand_regions[side].has("edge_a") and hand_regions[side].has("edge_b")
		hand_materials[side].set_shader_parameter("slanted_cuff",slanted)
		if slanted:
			hand_materials[side].set_shader_parameter("edge_a",hand_regions[side].edge_a)
			hand_materials[side].set_shader_parameter("edge_b",hand_regions[side].edge_b)
			hand_materials[side].set_shader_parameter("skin_margin",float(hand_regions[side].get("skin_margin",0.0)))

func set_hand_pose_override(style: String) -> void:
	if style in ["","open","fist","sword_near","grip_near"]: hand_pose_override = style

func _apply_hand_variants() -> void:
	if not hand_variants_enabled: return
	var requested := hand_pose_override
	if requested.is_empty(): requested = "fist" if sampled_source_clip in ["Run_New1","Run_New2"] else "open"
	for side in hand_materials:
		var style := requested
		if requested in ["sword_near","grip_near"]:
			style = ("sword" if requested=="sword_near" else "grip") if side=="sleeve_near" else "open"
		var available: bool = hand_variant_textures[side].has(style)
		# Source male101 visible hands span roughly 5-8 source pixels; use a
		# common ~5.5px palm rather than normalizing everything to our tiny 4.2px palm.
		var sizes := {"open":0.76,"fist":0.76,"sword":0.76,"grip":0.76}
		hand_materials[side].set_shader_parameter("hand_size",float(sizes.get(style,1.0)))
		hand_materials[side].set_shader_parameter("hand_source_offset",Vector2.ZERO)
		hand_materials[side].set_shader_parameter("variant_enabled",available)
		if available: hand_materials[side].set_shader_parameter("hand_variant",hand_variant_textures[side][style])
		active_hand_poses[side] = style if available else "open"

func _build_neutral_head() -> void:
	if not bones.has("head"): return
	var face_index := -1
	for index in range(original_pose_document.meshes.size()):
		if str(original_pose_document.meshes[index].category)=="face": face_index = index
	if face_index<0: return
	var face: Dictionary = original_pose_document.meshes[face_index]
	var u0 := Vector2(float(face.uv[0][0]),float(face.uv[0][1]))
	var u1 := Vector2(float(face.uv[1][0]),float(face.uv[1][1]))
	var u2 := Vector2(float(face.uv[2][0]),float(face.uv[2][1]))
	var inverse := Transform2D(u1-u0,u2-u0,u0).affine_inverse()
	var vertices: Array = original_pose_document.clips.Idle.frames[0][face_index]
	var a := _map_point(vertices[0])
	var b := _map_point(vertices[1])
	var c := _map_point(vertices[2])
	var head := NeutralHead.new()
	head.name = "NeutralFacelessFittingMannequin"
	head.z_as_relative = false
	head.z_index = 34
	bones.head.add_child(head)
	# Analytical oval only: no source face texture, features or hair are read.
	for index in range(40):
		var angle := index*TAU/40.0
		var uv := Vector2(10.0/24.0+cos(angle)*5.6/24.0,14.2/24.0+sin(angle)*6.8/24.0)
		var local := inverse*uv
		var point := a+(b-a)*local.x+(c-a)*local.y
		head.contour.append(bones.head.to_local(runtime.rig_root_node.to_global(point)))
	neutral_head = head

func _build_fixed_head() -> void:
	if neutral_head==null: return
	var path := outfit_art_dir.path_join("assets/head_plain_v15_atlas.png")
	var source := Image.new()
	if source.load(ProjectSettings.globalize_path(path))!=OK:
		validation_errors.append("Missing approved simple v15 faceless head: "+path)
		return
	source.generate_mipmaps()
	var sprite := Sprite2D.new()
	sprite.name = "ApprovedSimpleBlackHairV15"
	sprite.texture = ImageTexture.create_from_image(source)
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	sprite.centered = false
	sprite.region_enabled = true
	sprite.region_rect = Rect2(335,75,245,280)
	var contour: PackedVector2Array = neutral_head.get("contour")
	var top := INF
	var bottom := -INF
	for point in contour:
		var rig_point: Vector2 = bones.head.transform*point
		top = minf(top,rig_point.y)
		bottom = maxf(bottom,rig_point.y)
	# Use the original approved main crown130 -> chin316 ruler (186px),
	# excluding the bun. Keep the neck fixed while restoring head/body ratio.
	var art_scale := (bottom-top)*1.10/186.0
	sprite.scale = Vector2.ONE*art_scale
	# The imported head bone has a bind-axis rotation. Cancel that once;
	# subsequent animation rotates the selected head by pose deltas only.
	sprite.rotation = -float(bones.head.rotation)
	sprite.position = contour[10]-(Vector2(470.0,330.0)-sprite.region_rect.position).rotated(sprite.rotation)*art_scale
	sprite.z_as_relative = false
	sprite.z_index = 34
	var shader := Shader.new()
	shader.code = "shader_type canvas_item; render_mode unshaded; void fragment(){ COLOR=texture(TEXTURE,UV); COLOR.a=smoothstep(0.08,0.92,COLOR.a)*(1.0-smoothstep(333.0/1536.0,355.0/1536.0,UV.y)); }"
	var material := ShaderMaterial.new()
	material.shader = shader
	sprite.material = material
	bones.head.add_child(sprite)
	fixed_head = sprite
	neutral_head.visible = false

func set_visible(value: bool) -> void:
	super.set_visible(value)
	if neutral_head!=null: neutral_head.visible = value and fixed_head==null
	if fixed_head!=null: fixed_head.visible = value
	if sleeve_reconstruction!=null: sleeve_reconstruction.set_visible(value)
	if leg_reconstruction!=null: leg_reconstruction.set_visible(value)
	if hem_secondary!=null: hem_secondary.set_visible(value)

func get_garment_diagnostics() -> Dictionary:
	var result := {"mode":"isolated_outfit_full_canvas_affine","outfit":"ink_outfit_01_20261104","skin_sha256":FileAccess.get_sha256(get_script().resource_path),"template":template_path,"source_data":source_data_path,"source_ppu":template_document.get("sourcePPU",100),"template_ppu":template_document.get("targetPPU",2400),"density_multiplier":template_document.get("densityMultiplier",24),"selected_source_meshes":selected_source_mesh_indices,"clothing_meshes":meshes.size(),"native_materials":material_diagnostics,"source_clip":sampled_source_clip,"source_phase":sampled_source_phase,"neutral_head_only":true,"hair_in_scope":false,"old_hd_registration_used":false,"default_entry_modified":false,"source_frames":"original evaluated bone/skin frames plus disclosed rear carrier extension","body_carrier_extension":body_carrier_extension,"pants_upper_insertion_mask":{"scope":"leg fragments only; hide insertion above source pelvis/waist center","point":[pants_waist_point.x,pants_waist_point.y],"up_normal":[pants_up_normal.x,pants_up_normal.y],"source_anchor":"pelvis / shenti2","normal":"head-pelvis","source_vertices_unchanged":true,"native_uv_unchanged":true,"frozen_when_paused":true},"secondary_surrogate":use_secondary_frames,"release_cloth_proxy":release_cloth_transition}
	result.hand_pose_variants = {"active":active_hand_poses,"regions":hand_regions,"materials":hand_variant_diagnostics,"base":"open sleeve remains unchanged; variants sampled only at wrist/hand","sampling":"normalized full-canvas coordinates per native texture","ordinary_idle":"open","ordinary_run":"fist","unspecified_special_action":"open","sword":"explicit single near-hand override only; no skill mapping","override":hand_pose_override,"strict_required":require_hand_variants}
	result.neutral_head_only = fixed_head==null
	result.hair_in_scope = fixed_head!=null
	result.selected_head = {"path":outfit_art_dir.path_join("assets/head_plain_v15_atlas.png"),"style":"approved simple v15 black hair, no long ribbon, featureless side face","neck_source_px":[470,330],"main_crown_to_chin_source_px":186,"main_head_to_old_oval":1.10,"region":[335,75,245,280],"source_asset":"art/yuxu_fullbody_v15/cutout.png","new_generation":false}
	result.pants_upper_insertion_mask.scope = "opaque hip insertion only; cutoffs release over painted V=0.23..0.32, exempting lifted knees and boots"
	result.pants_upper_insertion_mask.hip_release_uv = [0.23,0.32]
	result.pants_upper_insertion_mask.knee_alpha_closure = {"native_radius_pixels":8.0,"uv_band":[0.42,0.46,0.59,0.61],"scope":"fill tiny transparent inner-fold sliver; original PNG, UV, mesh and boot proportions unchanged"}
	result.near_hand_carrier_extension = near_hand_carrier_extension
	result.hem_secondary = hem_secondary.get_diagnostics() if hem_secondary!=null else {}
	result.sleeve_reconstruction = sleeve_reconstruction.get_diagnostics() if sleeve_reconstruction!=null else {}
	result.leg_reconstruction = leg_reconstruction.get_diagnostics() if leg_reconstruction!=null else {}
	result.shared_sleeve_master = {"enabled":share_sleeve_master,"cloth":"both sides always use near master","far":"open/fist variant contributes skin beyond slanted cuff only"}
	result.optional_near_hand_poses = {"sword":"explicit sword_near","grip":"explicit grip_near","far":"ordinary open hand remains","skill_binding":null,"ordinary_idle_and_run_unchanged":true}
	return result

func summary() -> Dictionary:
	return {"source":"first independent ink outfit fitting prototype","meshes":meshes.size(),"errors":validation_errors,"garment":get_garment_diagnostics()}
