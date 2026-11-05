extends RefCounted

## The source rig's evaluated boneWorld * bindpose vertices, sampled directly.
## Costume pigment is ours; its source alpha, triangles, proportions and clips
## stay together. No anatomical IK or project-authored gait curve is fitted.
const PIXEL_SCALE := 16.0/186.0
const ROBED_MOVE_SPEED := 114.0
var source_data_path := ""
var hd_texture_root := ""
var strict_required := false
var minimum_hd_long_edge := 1024
var target_native_height := 104.0
var display_scale := 0.72
var use_secondary_frames := true
var clip_blend_seconds := 0.12
var texture_overrides: Dictionary = {}
var paint_overrides: Dictionary = {}
var paint_regions: Dictionary = {}
var color_overrides: Dictionary = {}
var hd_registration: Dictionary = {}
var hd_registration_prepared: Dictionary = {}
var require_registered_hd := false
var registration_metadata: Dictionary = {}
var hd_registration_path := ""
var runtime: Node2D
var meshes: Array[Polygon2D] = []
var bones: Dictionary = {}
var validation_errors: Array[String] = []
var mesh_records: Array[Dictionary] = []
var pose_blending := true
var last_clip := ""
var locomotion_style: StringName = &"ground"
var motion_context := false
var motion_delta := 0.0
var motion_clock := 0.0
var motion_facing := 1.0
var previous_velocity := Vector2.ZERO
var action_guard := false
var action_preparing := false
var combo_stage := 0
var combo_phase := 0.0
var contact_shadow: Node2D
var source_document: Dictionary = {}
var source_scale := 1.0
var source_foot_y := 0.0
var source_center_x := 0.0
var sampled_source_clip := ""
var sampled_source_phase := 0.0
var sampled_frames := Vector2i.ZERO
var interpolation_weight := 0.0
var source_bone_nodes: Array[Node2D] = []
var blend_from_vertices: Array = []
var blend_from_bones: Array[Vector2] = []
var blend_from_bone_angles: Array[float] = []
var blend_age := 1.0
var last_combo_stage := 0
var transition_from_clip := ""
var transition_to_clip := ""
var release_cloth_transition := false
var release_hem_seconds := 0.58
var release_hair_seconds := 0.42
var last_motion_clock := -INF
var current_vertices: Array = []
var all_hd_materials := true
var rounded_shoulder_caps := true
var shoulder_patches: Array[Polygon2D] = []
var ground_contacts: Dictionary = {}
static var shared_sources: Dictionary = {}
static var shared_textures: Dictionary = {}

const INK_SHADER := """
shader_type canvas_item;
uniform sampler2D paint_texture : source_color, filter_linear_mipmap;
uniform bool paint_enabled = false;
uniform bool use_sprite_paint = false;
uniform vec4 paint_region = vec4(0.0,0.0,1.0,1.0);
uniform vec4 pigment : source_color = vec4(0.88,0.87,0.82,1.0);
uniform vec4 ink_color : source_color = vec4(0.20,0.25,0.26,1.0);
uniform float edge_strength = 0.22;
varying vec4 mesh_tint;
void vertex() { mesh_tint = COLOR; }
void fragment() {
    float a = texture(TEXTURE, UV).a;
    vec3 col = pigment.rgb;
    if (use_sprite_paint) {
        col = texture(TEXTURE, UV).rgb;
    } else if (paint_enabled) {
        vec4 paint = texture(paint_texture,paint_region.xy+UV*paint_region.zw);
        col = mix(col,paint.rgb,paint.a);
    }
    vec2 texel = TEXTURE_PIXEL_SIZE*0.65;
    float inner = min(min(texture(TEXTURE,UV+vec2(texel.x,0.0)).a,
                          texture(TEXTURE,UV-vec2(texel.x,0.0)).a),
                      min(texture(TEXTURE,UV+vec2(0.0,texel.y)).a,
                          texture(TEXTURE,UV-vec2(0.0,texel.y)).a));
    float edge = clamp((a-inner)/max(a,0.001),0.0,1.0);
    col = mix(col,ink_color.rgb,edge*edge_strength);
    COLOR = vec4(col,a*pigment.a)*mesh_tint;
}
"""

class SourceContactShadow extends Node2D:
	func _draw() -> void:
		var points := PackedVector2Array()
		for i in range(40): points.append(Vector2(cos(i*TAU/40.0)*14.0,sin(i*TAU/40.0)*2.5))
		draw_colored_polygon(points,Color(0.10,0.13,0.14,0.10))

func _base_dir() -> String:
	return get_script().resource_path.get_base_dir()

func configure_materials(textures: Dictionary, paint: Dictionary = {}, regions: Dictionary = {}, pigments: Dictionary = {}) -> void:
	texture_overrides = textures.duplicate(true)
	paint_overrides = paint.duplicate(true)
	paint_regions = regions.duplicate(true)
	color_overrides = pigments.duplicate(true)

func configure_hd_registration(registrations: Dictionary, require_all: bool = true) -> void:
	hd_registration = registrations.get("sprites",registrations).duplicate(true)
	require_registered_hd = require_all
	hd_registration_prepared.clear()
	registration_metadata.clear()
	for raw_id in hd_registration:
		var id := str(raw_id)
		var entry: Dictionary = hd_registration[raw_id]
		var anchors: Array = entry.get("anchors",[])
		var source_size: Array = entry.get("source_size",[1,1])
		var from := PackedVector2Array()
		var target := PackedVector2Array()
		var source_widths := PackedFloat32Array()
		var target_widths := PackedFloat32Array()
		for anchor in anchors:
			var before: Array = anchor.get("source_uv",[])
			var after: Array = anchor.get("target_px",[])
			if before.size()!=2 or after.size()!=2: continue
			from.append(Vector2(float(before[0])*float(source_size[0]),float(before[1])*float(source_size[1])))
			target.append(Vector2(float(after[0]),float(after[1])))
			source_widths.append(float(anchor.get("source_width",1.0)))
			target_widths.append(float(anchor.get("target_width",1.0)))
		var mode := str(entry.get("mode","triangles"))
		var prepared := {"mode":mode,"source_size":Vector2(float(source_size[0]),float(source_size[1])),"from":from,"target":target,"source_widths":source_widths,"target_widths":target_widths,"joint_blend":float(entry.get("joint_blend",0.16))}
		var valid := mode in ["axis","piecewise_axis"] and from.size()>=2
		if mode in ["triangles","triangulated"] and from.size()>=3:
			prepared.triangles = Geometry2D.triangulate_delaunay(from)
			valid = prepared.triangles.size()>=3
		elif mode in ["affine","least_squares"] and from.size()>=3:
			var row0 := Vector3.ZERO
			var row1 := Vector3.ZERO
			var row2 := Vector3.ZERO
			var target_x := Vector3.ZERO
			var target_y := Vector3.ZERO
			for index in range(from.size()):
				var feature := Vector3(from[index].x,from[index].y,1.0)
				row0 += feature*feature.x
				row1 += feature*feature.y
				row2 += feature
				target_x += feature*target[index].x
				target_y += feature*target[index].y
			prepared.affine_x = _solve_registration_matrix(row0,row1,row2,target_x)
			prepared.affine_y = _solve_registration_matrix(row0,row1,row2,target_y)
			valid = absf(row0.dot(row1.cross(row2)))>0.0000001
		hd_registration_prepared[id] = prepared
		registration_metadata[id] = {"mode":mode,"anchors":from.size(),"source_size":source_size,"target_pixels":true,"valid":valid,"control_points":anchors}
	# Reconfigure existing geometry's UV only; pose samples and source vertex
	# arrays remain immutable. Shoulder underlays use the same registration.
	for record in mesh_records:
		var id := str(int(record.source.spriteId))
		var mesh: Polygon2D = record.mesh
		var coordinates := PackedVector2Array()
		for point in record.source.uv: coordinates.append(Vector2(float(point[0]),float(point[1]))*mesh.texture.get_size())
		mesh.uv = coordinates
		_apply_hd_registration_material(mesh.material,id)
	for patch in shoulder_patches:
		var id := str(patch.get_meta("sprite_id",""))
		var coordinates := PackedVector2Array()
		for point in patch.get_meta("source_uv",[]): coordinates.append(point*patch.texture.get_size())
		patch.uv = coordinates

func _apply_hd_registration_material(material: ShaderMaterial, id: String) -> void:
	if not hd_registration_prepared.has(id): return
	var entry: Dictionary = hd_registration_prepared[id]
	if registration_metadata[id].valid!=true: return
	material.shader = load(_base_dir().path_join("guigu_hd_registered.gdshader"))
	var source := PackedVector2Array()
	var target := PackedVector2Array()
	var sw := PackedFloat32Array()
	var tw := PackedFloat32Array()
	for index in range(32):
		source.append(entry["from"][index] if index<entry["from"].size() else Vector2.ZERO)
		target.append(entry.target[index] if index<entry.target.size() else Vector2.ZERO)
		sw.append(entry.source_widths[index] if index<entry.source_widths.size() else 1.0)
		tw.append(entry.target_widths[index] if index<entry.target_widths.size() else 1.0)
	material.set_shader_parameter("source_size",entry.source_size)
	material.set_shader_parameter("source_points",source)
	material.set_shader_parameter("target_points",target)
	material.set_shader_parameter("source_widths",sw)
	material.set_shader_parameter("target_widths",tw)
	material.set_shader_parameter("control_count",entry["from"].size())
	material.set_shader_parameter("joint_blend",entry.joint_blend)
	material.set_shader_parameter("use_sprite_paint",true)
	material.set_shader_parameter("paint_enabled",false)
	var mode := str(entry.mode)
	if mode in ["axis","piecewise_axis"]:
		material.set_shader_parameter("registration_mode",2)
	elif mode in ["affine","least_squares"]:
		material.set_shader_parameter("registration_mode",3)
		material.set_shader_parameter("affine_x",entry.affine_x)
		material.set_shader_parameter("affine_y",entry.affine_y)
	else:
		material.set_shader_parameter("registration_mode",1)
		var triangles := PackedVector3Array()
		var indices: PackedInt32Array = entry.triangles
		for index in range(64):
			triangles.append(Vector3(indices[index*3],indices[index*3+1],indices[index*3+2]) if index*3+2<indices.size() else Vector3.ZERO)
		material.set_shader_parameter("control_triangles",triangles)
		material.set_shader_parameter("triangle_count",indices.size()/3)

func _solve_registration_matrix(a: Vector3, b: Vector3, c: Vector3, rhs: Vector3) -> Vector3:
	var determinant := a.dot(b.cross(c))
	if absf(determinant)<0.0000001: return Vector3.ZERO
	return (b.cross(c)*rhs.x+c.cross(a)*rhs.y+a.cross(b)*rhs.z)/determinant

func _registered_hd_uv(id: String, uv: Vector2, texture_size: Vector2) -> Vector2:
	if not hd_registration_prepared.has(id): return uv*texture_size
	var entry: Dictionary = hd_registration_prepared[id]
	var before: PackedVector2Array = entry["from"]
	var after: PackedVector2Array = entry.target
	var point: Vector2 = uv*entry.source_size
	var mode := str(entry.mode)
	if mode in ["affine","least_squares"] and entry.has("affine_x"):
		var feature := Vector3(point.x,point.y,1.0)
		return Vector2(entry.affine_x.dot(feature),entry.affine_y.dot(feature))
	if mode in ["triangles","triangulated"] and entry.has("triangles"):
		var triangles: PackedInt32Array = entry.triangles
		var best_penalty := INF
		var best_point := uv*texture_size
		for index in range(0,triangles.size(),3):
			var a := triangles[index]
			var b := triangles[index+1]
			var c := triangles[index+2]
			var basis := Transform2D(before[b]-before[a],before[c]-before[a],before[a])
			if absf(basis.determinant())<0.0000001: continue
			var coordinates := basis.affine_inverse()*point
			var first_weight := 1.0-coordinates.x-coordinates.y
			var penalty := maxf(0.0,-first_weight)+maxf(0.0,-coordinates.x)+maxf(0.0,-coordinates.y)
			if penalty<best_penalty:
				best_penalty = penalty
				best_point = after[a]*first_weight+after[b]*coordinates.x+after[c]*coordinates.y
			if penalty<0.000001: return best_point
		return best_point
	if mode in ["axis","piecewise_axis"] and before.size()>=2:
		var chosen := 0
		var chosen_parameter := 0.0
		var best_distance := INF
		for index in range(before.size()-1):
			var axis := before[index+1]-before[index]
			var parameter := (point-before[index]).dot(axis)/maxf(0.000001,axis.length_squared())
			var clamped := clampf(parameter,0.0,1.0)
			var distance := point.distance_squared_to(before[index]+axis*clamped)
			if distance<best_distance:
				best_distance = distance
				chosen = index
				chosen_parameter = parameter if (index==0 and parameter<0.0) or (index==before.size()-2 and parameter>1.0) else clamped
		var source_direction := (before[chosen+1]-before[chosen]).normalized()
		var target_direction := (after[chosen+1]-after[chosen]).normalized()
		var blend: float = entry.joint_blend
		if chosen>0 and chosen_parameter<blend:
			var source_average := ((before[chosen]-before[chosen-1]).normalized()+source_direction).normalized()
			var target_average := ((after[chosen]-after[chosen-1]).normalized()+target_direction).normalized()
			var amount := smoothstep(0.0,blend,chosen_parameter)
			source_direction = source_average.lerp(source_direction,amount).normalized()
			target_direction = target_average.lerp(target_direction,amount).normalized()
		elif chosen<before.size()-2 and chosen_parameter>1.0-blend:
			var source_average := ((before[chosen+2]-before[chosen+1]).normalized()+source_direction).normalized()
			var target_average := ((after[chosen+2]-after[chosen+1]).normalized()+target_direction).normalized()
			var amount := smoothstep(1.0-blend,1.0,chosen_parameter)
			source_direction = source_direction.lerp(source_average,amount).normalized()
			target_direction = target_direction.lerp(target_average,amount).normalized()
		var source_center := before[chosen].lerp(before[chosen+1],chosen_parameter)
		var target_center := after[chosen].lerp(after[chosen+1],chosen_parameter)
		var source_width := lerpf(entry.source_widths[chosen],entry.source_widths[chosen+1],clampf(chosen_parameter,0.0,1.0))
		var target_width := lerpf(entry.target_widths[chosen],entry.target_widths[chosen+1],clampf(chosen_parameter,0.0,1.0))
		var transverse := (point-source_center).dot(source_direction.orthogonal())
		return target_center+target_direction.orthogonal()*transverse*target_width/maxf(0.0001,source_width)
	return uv*texture_size

func _load_texture(path: String) -> Texture2D:
	if shared_textures.has(path): return shared_textures[path]
	var source := Image.new()
	if source.load(ProjectSettings.globalize_path(path))!=OK: return null
	source.generate_mipmaps()
	var result := ImageTexture.create_from_image(source)
	shared_textures[path] = result
	return result

func _category(record: Dictionary) -> String:
	var declared := str(record.get("category",""))
	if not declared.is_empty(): return declared
	var path := str(record.get("rendererPath",record.get("name",""))).to_lower()
	if path.contains("toufa") or path.contains("/fa/"): return "hair"
	if path.ends_with("/lian") or path.contains("face"): return "face"
	if path.contains("youbi") or path.contains("zuobi"): return "sleeve"
	if path.contains("youtui") or path.contains("zuotui"): return "leg"
	if path.contains("yifu"): return "body"
	return "decoration"

func _default_pigment(category: String) -> Color:
	match category:
		"face": return Color(0.83,0.81,0.75,1.0)
		"hair": return Color(0.16,0.19,0.20,1.0)
		"leg": return Color(0.24,0.27,0.28,1.0)
		_: return Color(0.91,0.90,0.85,1.0)

func _material_for(record: Dictionary, _source_texture: Texture2D, is_hd_sprite: bool = false) -> ShaderMaterial:
	var category := _category(record)
	var id := str(int(record.spriteId))
	var material := ShaderMaterial.new()
	var shader := Shader.new()
	shader.code = INK_SHADER
	material.shader = shader
	material.set_shader_parameter("pigment",color_overrides.get(id,color_overrides.get(category,_default_pigment(category))))
	material.set_shader_parameter("use_sprite_paint",is_hd_sprite)
	if is_hd_sprite:
		# A new faceless head likewise samples only its new art. No original
		# face RGB or low-resolution alpha participates in this branch.
		material.set_shader_parameter("edge_strength",0.0)
		_apply_hd_registration_material(material,id)
		return material
	# Face RGB is never sampled: all source facial features disappear even
	# before a new faceless paint exists. Hair likewise uses a flat ink wash.
	var paint_path := ""
	if category not in ["face","hair"]:
		paint_path = str(paint_overrides.get(id,paint_overrides.get(category,"")))
		if paint_path.is_empty():
			var filename: String = {"body":"body.png","sleeve":"arm_relaxed_v19.png","leg":"leg.png"}.get(category,"")
			var candidate := _base_dir().path_join("art/yuxu_guigu_rebuild_v1").path_join(filename)
			if not filename.is_empty() and FileAccess.file_exists(candidate): paint_path = candidate
	if not paint_path.is_empty():
		var paint := _load_texture(paint_path)
		if paint!=null:
			var default_region: Array = {"body":[275,310,390,1030],"sleeve":[360,345,355,285],"leg":[325,660,195,830]}.get(category,[0,0,paint.get_width(),paint.get_height()])
			var region: Array = paint_regions.get(id,paint_regions.get(category,default_region))
			material.set_shader_parameter("paint_enabled",true)
			material.set_shader_parameter("paint_texture",paint)
			material.set_shader_parameter("paint_region",Vector4(float(region[0])/paint.get_width(),float(region[1])/paint.get_height(),float(region[2])/paint.get_width(),float(region[3])/paint.get_height()))
	material.set_shader_parameter("edge_strength",0.12 if category=="face" else 0.22)
	return material

func _map_point(value: Array) -> Vector2:
	return Vector2((float(value[0])-source_center_x)*source_scale,(float(value[1])-source_foot_y)*source_scale)

func _establish_fixed_envelope() -> void:
	var idle: Dictionary = source_document.clips.get("Idle",source_document.clips.values()[0])
	var initial: Array = idle.frames[0]
	var top := INF
	var bottom := -INF
	var left := INF
	var right := -INF
	var leg_bottom := -INF
	var source_meshes: Array = source_document.meshes
	for index in range(initial.size()):
		for point in initial[index]:
			top = minf(top,float(point[1]))
			bottom = maxf(bottom,float(point[1]))
			left = minf(left,float(point[0]))
			right = maxf(right,float(point[0]))
			if _category(source_meshes[index])=="leg": leg_bottom = maxf(leg_bottom,float(point[1]))
	source_foot_y = leg_bottom if is_finite(leg_bottom) else bottom
	source_center_x = (left+right)*0.5
	source_scale = target_native_height/maxf(1.0,source_foot_y-top)

func build(owner_runtime: Node2D, _art_dir: String) -> bool:
	runtime = owner_runtime
	if source_data_path.is_empty():
		# Resolve to the same project-root artifact from either the main game
		# or the separately runnable action-battle preview project.
		source_data_path = ProjectSettings.globalize_path(_base_dir()).path_join("../../resources/appearance/guigu_male101/model_frames_20261104.json").simplify_path()
	if hd_texture_root.is_empty(): hd_texture_root = _base_dir().path_join("art/guigu_male101_ink_hd")
	if not FileAccess.file_exists(source_data_path):
		validation_errors.append("Missing verified source model samples: "+source_data_path)
		return false
	if shared_sources.has(source_data_path): source_document = shared_sources[source_data_path]
	else:
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(source_data_path))
		if not parsed is Dictionary or not parsed.has("meshes") or not parsed.has("clips"):
			validation_errors.append("Invalid source model frame schema")
			return false
		source_document = parsed
		shared_sources[source_data_path] = source_document
	if hd_registration.is_empty():
		var registration_path := hd_registration_path if not hd_registration_path.is_empty() else source_data_path.get_base_dir().path_join("hd_registration_20261104.json")
		if FileAccess.file_exists(registration_path):
			var registration: Variant = JSON.parse_string(FileAccess.get_file_as_string(registration_path))
			if registration is Dictionary: configure_hd_registration(registration,true)
	_establish_fixed_envelope()
	var initial: Array = source_document.clips.get("Idle",source_document.clips.values()[0]).frames[0]
	for index in range(source_document.meshes.size()):
		var record: Dictionary = source_document.meshes[index]
		var id := str(int(record.spriteId))
		var image_path := str(texture_overrides.get(id,""))
		var is_hd_sprite := not image_path.is_empty()
		if image_path.is_empty():
			var hd_path := hd_texture_root.path_join("sprite_"+id+".png")
			if FileAccess.file_exists(hd_path):
				image_path = hd_path
				is_hd_sprite = true
		if strict_required and (image_path.is_empty() or not FileAccess.file_exists(image_path)):
			validation_errors.append("Required high-definition ink sprite missing: "+hd_texture_root.path_join("sprite_"+id+".png"))
			return false
		if image_path.is_empty():
			var relative := str(record.get("sourceImage",str(source_document.get("textureRoot","textures")).path_join("sprite_"+id+".png")))
			image_path = source_data_path.get_base_dir().path_join(relative)
			if not FileAccess.file_exists(image_path): image_path = _base_dir().path_join("art/guigu_reference/sprite_"+id+".png")
		all_hd_materials = all_hd_materials and is_hd_sprite
		var source_texture := _load_texture(image_path)
		if source_texture==null:
			validation_errors.append("Missing source alpha silhouette: "+image_path)
			return false
		if strict_required and maxi(source_texture.get_width(),source_texture.get_height())<minimum_hd_long_edge:
			validation_errors.append("Ink sprite is below the required native resolution: "+image_path)
			return false
		if strict_required and require_registered_hd and (not registration_metadata.has(id) or registration_metadata[id].valid!=true):
			validation_errors.append("HD sprite has no verified source-to-paint anchor registration: "+id)
			return false
		var mesh := Polygon2D.new()
		var category := _category(record)
		mesh.name = "GuiguSource_"+str(record.name)+( "_head" if category in ["face","hair"] else "_"+str(index))
		mesh.texture = source_texture
		mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		mesh.z_index = 30+index
		var uv := PackedVector2Array()
		for point in record.uv:
			var original_uv := Vector2(float(point[0]),float(point[1]))
			uv.append(original_uv*source_texture.get_size())
		mesh.uv = uv
		var triangles := []
		for tri in record.triangles: triangles.append(PackedInt32Array(tri))
		mesh.polygons = triangles
		var vertices := PackedVector2Array()
		for point in initial[index]: vertices.append(_map_point(point))
		mesh.polygon = vertices
		mesh.material = _material_for(record,source_texture,is_hd_sprite)
		runtime.rig_root_node.add_child(mesh)
		meshes.append(mesh)
		mesh_records.append({"mesh":mesh,"source":record,"category":category,"texture_path":image_path,"high_definition_sprite":is_hd_sprite,"texture_size":[source_texture.get_width(),source_texture.get_height()]})
		current_vertices.append(vertices)
	_build_source_attachments()
	var idle_data: Dictionary = source_document.clips.get("Idle",source_document.clips.values()[0])
	if idle_data.has("boneFrames"):
		for index in range(source_bone_nodes.size()):
			source_bone_nodes[index].position = _map_point(idle_data.boneFrames[0][index])
			if idle_data.has("boneTransforms"): source_bone_nodes[index].rotation = float(idle_data.boneTransforms[0][index][2])
	if rounded_shoulder_caps: _build_rounded_shoulder_caps()
	sampled_source_clip = "Idle" if source_document.clips.has("Idle") else str(source_document.clips.keys()[0])
	contact_shadow = SourceContactShadow.new()
	contact_shadow.name = "SourceModelContactShadow"
	contact_shadow.z_index = 1
	runtime.rig_root_node.add_child(contact_shadow)
	return validation_errors.is_empty() and not meshes.is_empty()

func _build_rounded_shoulder_caps() -> void:
	var body_layer := 33
	for record in mesh_records:
		if record.category=="body": body_layer = record.mesh.z_index
	for mesh_index in range(mesh_records.size()):
		var record: Dictionary = mesh_records[mesh_index]
		if record.category!="sleeve": continue
		var path := str(record.source.rendererPath)
		var side := "near" if path.contains("/youbi/") else "far"
		if not bones.has(side+"_upper"): continue
		var shoulder: Node2D = bones[side+"_upper"]
		var mesh: Polygon2D = record.mesh
		var uv: Array = record.source.uv
		var candidates: Array[int] = []
		for index in range(uv.size()):
			if float(uv[index][1])<0.245: candidates.append(index)
		var selected: Array[int] = []
		var best_area := 0.0
		for a in candidates:
			for b in candidates:
				if b<=a: continue
				for c in candidates:
					if c<=b: continue
					var first := Vector2(float(uv[a][0]),float(uv[a][1]))
					var second := Vector2(float(uv[b][0]),float(uv[b][1]))
					var third := Vector2(float(uv[c][0]),float(uv[c][1]))
					var area := absf((second-first).cross(third-first))
					if area>best_area:
						best_area = area
						selected = [a,b,c]
		if selected.size()!=3: continue
		var first := Vector2(float(uv[selected[0]][0]),float(uv[selected[0]][1]))
		var second := Vector2(float(uv[selected[1]][0]),float(uv[selected[1]][1]))
		var third := Vector2(float(uv[selected[2]][0]),float(uv[selected[2]][1]))
		var uv_inverse := Transform2D(second-first,third-first,first).affine_inverse()
		var source_points: PackedVector2Array = current_vertices[mesh_index]
		var patch := Polygon2D.new()
		patch.name = "RoundedShoulderUnderBody_"+side
		var points := PackedVector2Array()
		var coordinates := PackedVector2Array()
		for corner in [Vector2(0,0),Vector2(1,0),Vector2(1,0.24),Vector2(0,0.24)]:
			var barycentric: Vector2 = uv_inverse*corner
			var source_point: Vector2 = source_points[selected[0]]+(source_points[selected[1]]-source_points[selected[0]])*barycentric.x+(source_points[selected[2]]-source_points[selected[0]])*barycentric.y
			points.append(shoulder.to_local(runtime.rig_root_node.to_global(source_point)))
			coordinates.append(Vector2(corner.x*mesh.texture.get_width(),corner.y*mesh.texture.get_height()))
		patch.polygon = points
		var original_uv := [Vector2(0,0),Vector2(1,0),Vector2(1,0.24),Vector2(0,0.24)]
		var registered_coordinates := PackedVector2Array()
		var id := str(int(record.source.spriteId))
		for point in original_uv: registered_coordinates.append(point*mesh.texture.get_size())
		patch.uv = registered_coordinates
		patch.set_meta("sprite_id",id)
		patch.set_meta("source_uv",original_uv)
		patch.texture = mesh.texture
		patch.texture_filter = mesh.texture_filter
		patch.material = mesh.material
		patch.z_as_relative = false
		patch.z_index = body_layer-1
		shoulder.add_child(patch)
		shoulder_patches.append(patch)

func _build_source_attachments() -> void:
	var paths: Array = source_document.get("bonePaths",[])
	for path in paths:
		var attachment := Node2D.new()
		attachment.name = "Source_"+str(path).get_file()
		runtime.rig_root_node.add_child(attachment)
		source_bone_nodes.append(attachment)
	var endings := {"near_upper":"/youbi1","near_forearm":"/youbi2","near_hand":"/youbi3","far_upper":"/zuobi1","far_forearm":"/zuobi2","far_hand":"/zuobi3","head":"/tou","torso":"/shenti1","pelvis":"/shenti2","back_cloth":"/shenti2","back_hem":"/shenti2_1","back_edge":"/shenti2_2"}
	for role in endings:
		for index in range(paths.size()):
			if str(paths[index]).ends_with(endings[role]) and not str(paths[index]).contains("/Foot2/"):
				bones[role] = source_bone_nodes[index]
				break
	for side in ["near","far"]:
		var limb := "youtui" if side=="near" else "zuotui"
		for piece in [["thigh","tui1"],["calf","tui2"],["foot","tui3"]]:
			for index in range(paths.size()):
				var path := str(paths[index])
				if path.contains("/Foot/"+limb+"/") and path.ends_with("/"+piece[1]) and not path.contains("/Foot2/"):
					bones[side+"_"+piece[0]] = source_bone_nodes[index]
					break

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	motion_context = true
	motion_delta = clampf(delta,0.0,0.05)
	if delta<=0.0: return
	if clock<motion_clock or clock-motion_clock>0.25:
		blend_from_vertices.clear()
		blend_from_bones.clear()
		blend_from_bone_angles.clear()
		blend_age = 1.0
	motion_clock = clock
	previous_velocity = velocity
	motion_facing = facing

func set_action_context(guarding: bool, preparing: bool) -> void:
	action_guard = guarding
	action_preparing = preparing

func set_combo_context(stage: int, progress: float) -> void:
	combo_stage = stage
	combo_phase = progress

func set_locomotion_style(value: StringName) -> void:
	locomotion_style = value

func _select_clip(requested: String) -> String:
	var selected := "Run_New2" if requested=="BackstepWalk" else requested
	if combo_stage>0 or action_guard or action_preparing:
		selected = "AttackRun" if previous_velocity.length()>1.0 else "AttackIdle"
	if not source_document.clips.has(selected): selected = "AttackIdle" if selected.begins_with("Attack") and source_document.clips.has("AttackIdle") else "Idle"
	return selected

func _vertex_transition_weight(mesh_index: int, vertex: int) -> float:
	if not pose_blending or not motion_context: return 1.0
	var duration := clip_blend_seconds
	if release_cloth_transition:
		var record: Dictionary = mesh_records[mesh_index]
		var v := float(record.source.uv[vertex][1])
		if record.category=="body": duration = lerpf(clip_blend_seconds,release_hem_seconds,smoothstep(0.40,0.98,v))
		elif record.category=="hair": duration = lerpf(clip_blend_seconds,release_hair_seconds,smoothstep(0.30,0.98,v))
	return smoothstep(0.0,maxf(0.0001,duration),blend_age)

func update_cloth(clip: String, time: float, duration: float, _tracks: Dictionary) -> void:
	if motion_context and motion_delta<=0.0 and not last_clip.is_empty(): return
	var selected := _select_clip(clip)
	var data: Dictionary = source_document.clips[selected]
	var source_duration := maxf(0.0001,float(data.duration))
	var sample_time := time
	if combo_stage>0: sample_time = clampf(combo_phase,0.0,1.0)*source_duration
	elif action_guard or action_preparing: sample_time = source_duration*0.15
	elif selected in ["Run_New1","Run_New2","Idle"] and motion_context: sample_time = motion_clock
	elif duration>0.0: sample_time = time/duration*source_duration
	var looping: bool = bool(data.get("loop",selected in ["Idle","Run_New1","Run_New2"]))
	var phase := fposmod(sample_time/source_duration,1.0) if looping else clampf(sample_time/source_duration,0.0,1.0)
	var frames: Array = data.secondaryFrames if use_secondary_frames and data.has("secondaryFrames") else data.frames
	var frame_position := phase*frames.size() if looping else phase*maxi(1,frames.size()-1)
	var first := mini(floori(frame_position),frames.size()-1)
	var second := (first+1)%frames.size() if looping else mini(first+1,frames.size()-1)
	var weight := frame_position-floorf(frame_position)
	if selected!=sampled_source_clip or combo_stage!=last_combo_stage:
		transition_from_clip = sampled_source_clip
		transition_to_clip = selected
		release_cloth_transition = sampled_source_clip in ["Run_New1","Run_New2"] and selected=="Idle"
		blend_from_vertices.clear()
		for vertices in current_vertices: blend_from_vertices.append(vertices.duplicate())
		blend_from_bones.clear()
		blend_from_bone_angles.clear()
		for node in source_bone_nodes:
			blend_from_bones.append(node.position)
			blend_from_bone_angles.append(node.rotation)
		blend_age = 0.0
	if motion_context and motion_clock!=last_motion_clock:
		blend_age += motion_delta
		last_motion_clock = motion_clock
	else:
		blend_age = clip_blend_seconds
	var blend := smoothstep(0.0,maxf(0.0001,clip_blend_seconds),blend_age) if pose_blending and motion_context else 1.0
	for mesh_index in range(meshes.size()):
		var vertices := PackedVector2Array()
		var a: Array = frames[first][mesh_index]
		var b: Array = frames[second][mesh_index]
		for index in range(a.size()):
			var point := _map_point(a[index]).lerp(_map_point(b[index]),weight)
			var vertex_blend := _vertex_transition_weight(mesh_index,index)
			if vertex_blend<1.0 and mesh_index<blend_from_vertices.size(): point = blend_from_vertices[mesh_index][index].lerp(point,vertex_blend)
			vertices.append(point)
		meshes[mesh_index].polygon = vertices
		current_vertices[mesh_index] = vertices
	if data.has("boneFrames"):
		var bone_frames: Array = data.boneFrames
		for index in range(source_bone_nodes.size()):
			var position := _map_point(bone_frames[first][index]).lerp(_map_point(bone_frames[second][index]),weight)
			if blend<1.0 and index<blend_from_bones.size(): position = blend_from_bones[index].lerp(position,blend)
			source_bone_nodes[index].position = position
			if data.has("boneTransforms"):
				var a: Array = data.boneTransforms[first][index]
				var b: Array = data.boneTransforms[second][index]
				var angle := lerp_angle(float(a[2]),float(b[2]),weight)
				if blend<1.0 and index<blend_from_bone_angles.size(): angle = lerp_angle(blend_from_bone_angles[index],angle,blend)
				source_bone_nodes[index].rotation = angle
	sampled_source_clip = selected
	sampled_source_phase = phase
	sampled_frames = Vector2i(first,second)
	interpolation_weight = weight
	last_clip = clip
	last_combo_stage = combo_stage

func set_visible(value: bool) -> void:
	for mesh in meshes: mesh.visible = value
	for patch in shoulder_patches: patch.visible = value
	if contact_shadow!=null: contact_shadow.visible = value

func has_contact_shadow() -> bool:
	return contact_shadow!=null

func get_native_visual_height() -> float:
	return target_native_height*display_scale

func get_garment_diagnostics() -> Dictionary:
	return {"mode":"source_model_direct_vertex_sampling","source_data":source_data_path,"source_method":source_document.get("method",""),"sampled_source_clip":sampled_source_clip,"source_phase":sampled_source_phase,"sampled_frames":[sampled_frames.x,sampled_frames.y],"interpolation_weight":interpolation_weight,"fixed_source_scale":source_scale,"source_foot_y":source_foot_y,"native_height":target_native_height,"secondary_surrogate":use_secondary_frames,"secondary_limit":source_document.get("secondaryLimit",""),"source_mesh_count":meshes.size(),"source_attach_points":source_bone_nodes.size(),"high_definition_materials":all_hd_materials,"strict_hd_required":strict_required,"hd_texture_root":hd_texture_root,"hd_registration":registration_metadata,"hd_registration_required":require_registered_hd,"registration_sampling":"per_fragment_original_uv","release_cloth_proxy":release_cloth_transition,"transition_from_clip":transition_from_clip,"transition_to_clip":transition_to_clip,"release_hem_seconds":release_hem_seconds,"release_hair_seconds":release_hair_seconds,"rounded_shoulder_underlays":shoulder_patches.size(),"faceless":true,"gameplay_modified":false}

func summary() -> Dictionary:
	return {"source":"verified source model topology and pose frames, original ink pigment","meshes":meshes.size(),"bones":source_bone_nodes.size(),"errors":validation_errors,"garment":get_garment_diagnostics()}
