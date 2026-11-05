extends "reference_walk_skin.gd"

## Project-authored ink costume, rebuilt around a continuous garment surface.
## Guigu supplies the movement relationship; no source costume is installed.
## Configure before build. Missing new parts fall back individually to the
## existing faceless Yuxu art. A registration maps old UV pixels onto a new PNG:
## {"body":{"from":[275,310,390,1030],"to":[275,310,390,1030]}}.
var reconstructed_materials: Dictionary = {}
var reconstructed_registration: Dictionary = {}
var reconstructed_loaded_materials: Dictionary = {}
var reconstructed_arm_points: Dictionary = {}
var reconstructed_arm_ratios := {"near":0.77,"far":0.45}
var reconstructed_sleeve_width := {"near":0.92,"far":0.78}
var reconstructed_shared_body: Polygon2D

func configure_reconstructed_materials(paths: Dictionary, registrations: Dictionary = {}) -> void:
	reconstructed_materials = paths.duplicate(true)
	reconstructed_registration = registrations.duplicate(true)

func _material_path(key: String, fallback_dir: String) -> String:
	if reconstructed_materials.has(key): return str(reconstructed_materials[key])
	var rebuilt: String = get_script().resource_path.get_base_dir().path_join("art/yuxu_guigu_rebuild_v1").path_join(key+".png")
	if FileAccess.file_exists(rebuilt): return rebuilt
	var supplied := fallback_dir.path_join(key+".png")
	if FileAccess.file_exists(supplied): return supplied
	return get_script().resource_path.get_base_dir().path_join("art/yuxu_fullbody_v15").path_join(key+".png")

func _load_reconstructed_texture(key: String, fallback_dir: String) -> bool:
	var path := _material_path(key,fallback_dir)
	var source := Image.new()
	if source.load(ProjectSettings.globalize_path(path))!=OK:
		validation_errors.append("Missing reconstructed material: "+path)
		return false
	source.generate_mipmaps()
	textures[key] = ImageTexture.create_from_image(source)
	reconstructed_loaded_materials[key] = path
	return true

func _registered_uv(key: String, point: Vector2) -> Vector2:
	if not reconstructed_registration.has(key): return point
	var mapping: Dictionary = reconstructed_registration[key]
	if not mapping.has("from") or not mapping.has("to"): return point
	var before: Array = mapping["from"]
	var after: Array = mapping["to"]
	return Vector2(float(after[0])+(point.x-float(before[0]))*float(after[2])/maxf(1.0,float(before[2])),float(after[1])+(point.y-float(before[1]))*float(after[3])/maxf(1.0,float(before[3])))

func _register_mesh_uv(mesh: Polygon2D, key: String) -> void:
	if not reconstructed_registration.has(key): return
	var coordinates := PackedVector2Array()
	for point in mesh.uv: coordinates.append(_registered_uv(key,point))
	mesh.uv = coordinates

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	runtime = owner_runtime
	display_scale = 0.72
	garment_mode = SPLIT_OUTFIT
	var profile_path: String = get_script().resource_path.get_base_dir().path_join("guigu_run_pose_profile_20261104.json")
	var profile: Variant = JSON.parse_string(FileAccess.get_file_as_string(profile_path))
	if not profile is Dictionary or not profile.has("clips"):
		validation_errors.append("Missing inspected movement profile: "+profile_path)
		return false
	guigu_pose_profile = profile
	for key in ["cutout","body","arm","leg","arm_relaxed_v19","arm_open_v19","arm_sword_fingers_v21"]:
		if not _load_reconstructed_texture(key,art_dir): return false
	_new_bone("pelvis","",_world(Vector2(475,760)))
	_new_bone("torso","pelvis",_world(Vector2(490,635)))
	_new_bone("head","torso",_world(Vector2(470,330)))
	var source_upper: Vector2 = arm_rest_points[1]-arm_rest_points[0]
	var source_lower: Vector2 = arm_rest_points[2]-arm_rest_points[1]
	var total_length: float = source_upper.length()+source_lower.length()
	for side in ["near","far"]:
		var ratio: float = reconstructed_arm_ratios[side]
		var upper_length: float = total_length/(1.0+ratio)
		var lower_length: float = total_length-upper_length
		var offset := Vector2.ZERO if side=="near" else Vector2(43,-9)
		var shoulder: Vector2 = arm_rest_points[0]+offset
		var elbow: Vector2 = shoulder+source_upper.normalized()*upper_length
		var wrist: Vector2 = elbow+source_lower.normalized()*lower_length
		reconstructed_arm_points[side] = [shoulder,elbow,wrist]
		_new_bone(side+"_upper","torso",_world(shoulder))
		_new_bone(side+"_forearm",side+"_upper",_world(elbow))
		_new_bone(side+"_hand",side+"_forearm",_world(wrist))
		arm_chains[side] = [bones[side+"_upper"],bones[side+"_forearm"],bones[side+"_hand"]]
		var leg_points := _leg_points(side)
		_new_bone(side+"_thigh","pelvis",_world(leg_points[0]))
		_new_bone(side+"_calf",side+"_thigh",_world(leg_points[1]))
		_new_bone(side+"_foot",side+"_calf",_world(leg_points[2]))
		leg_chains[side] = [bones[side+"_thigh"],bones[side+"_calf"],bones[side+"_foot"]]
	# Keep legacy front handles available to unchanged runtime diagnostics.
	# Only the shared back chain has surface weights in this reconstruction.
	for side in ["front","back"]:
		_new_bone(side+"_cloth","pelvis",_world(Vector2(475,820)))
		_new_bone(side+"_hem",side+"_cloth",_world(Vector2(475,1040)))
		_new_bone(side+"_edge",side+"_hem",_world(Vector2(475,1220)))
	cloth_chains = {"front":[bones.back_cloth,bones.back_hem,bones.back_edge],"back":[bones.back_cloth,bones.back_hem,bones.back_edge]}
	_build_approved_head()
	_build_approved_body()
	for side in ["far","near"]:
		_build_approved_arm(side)
		_build_approved_leg(side)
		_cache_sole_profile(side)
	contact_shadow = ContactShadow.new()
	contact_shadow.skin = self
	contact_shadow.z_index = 1
	runtime.rig_root_node.add_child(contact_shadow)
	for mesh in meshes:
		if mesh.name in ["YuxuUnified_near_leg","YuxuUnified_far_leg"]: visible_leg_meshes.append(mesh)
	for record in mesh_records:
		if record.name=="robe_back": cape_body_records.append(record)
	cape_source_image = textures.body.get_image()
	return validation_errors.is_empty() and meshes.size()==6 and visible_leg_meshes.size()==2 and cape_body_records.size()==1

func _build_approved_head() -> void:
	super._build_approved_head()
	_register_mesh_uv(meshes.back(),"cutout")

func _build_approved_body() -> void:
	var rect := [275,310,390,1030]
	var vertices := PackedVector2Array()
	var coordinates := PackedVector2Array()
	var sets := [PackedFloat32Array(),PackedFloat32Array(),PackedFloat32Array(),PackedFloat32Array()]
	for row in range(ROWS+1):
		var y := float(rect[1])+float(rect[3])*row/ROWS
		for column in range(COLUMNS+1):
			var x := float(rect[0])+float(rect[2])*column/COLUMNS
			var front_edge := smoothstep(365.0,610.0,x)
			# Both edges share the same chain. The foreground releases a little
			# later across the width; no cut separates the facing or cloud motif.
			var lower := smoothstep(710.0+front_edge*20.0,955.0+front_edge*55.0,y)
			var middle := smoothstep(990.0+front_edge*20.0,1190.0+front_edge*25.0,y)
			var tip := smoothstep(1185.0+front_edge*15.0,1340.0,y)
			vertices.append(_world(Vector2(x,y)))
			coordinates.append(_registered_uv("body",Vector2(x,y)))
			sets[0].append(1.0-lower)
			sets[1].append(lower*(1.0-middle))
			sets[2].append(lower*middle*(1.0-tip))
			sets[3].append(lower*middle*tip)
	texture = textures.body
	_create_mesh("robe_back",rect,vertices,["torso","back_cloth","back_hem","back_edge"],sets,60,coordinates)
	reconstructed_shared_body = meshes.back()
	garment_body = reconstructed_shared_body
	garment_record = mesh_records.back()

func _arm_reproject(point: Vector2, side: String, forearm: float, hand: float, seam: float) -> Vector2:
	var targets: Array = reconstructed_arm_points[side]
	var source_upper: Vector2 = (arm_rest_points[1]-arm_rest_points[0]).normalized()
	var source_lower: Vector2 = (arm_rest_points[2]-arm_rest_points[1]).normalized()
	var upper_normal := source_upper.orthogonal()
	var lower_normal := source_lower.orthogonal()
	var upper_scale: float = targets[1].distance_to(targets[0])/(arm_rest_points[1]-arm_rest_points[0]).length()
	var lower_scale: float = targets[2].distance_to(targets[1])/(arm_rest_points[2]-arm_rest_points[1]).length()
	var width: float = reconstructed_sleeve_width[side]
	var from_upper: Vector2 = point-arm_rest_points[0]
	var from_lower: Vector2 = point-arm_rest_points[1]
	var upper_point: Vector2 = targets[0]+source_upper*from_upper.dot(source_upper)*upper_scale+upper_normal*from_upper.dot(upper_normal)*width
	var lower_point: Vector2 = targets[1]+source_lower*from_lower.dot(source_lower)*lower_scale+lower_normal*from_lower.dot(lower_normal)*width
	# The painted hand and cuff keep their own pixel dimensions. Segment
	# length changes occur along the sleeve, rather than scaling wrist marks.
	var hand_point: Vector2 = targets[2]+point-arm_rest_points[2]
	var mapped := upper_point.lerp(lower_point,forearm).lerp(hand_point,forearm*hand)
	var source_offset := Vector2.ZERO if side=="near" else Vector2(43,-9)
	return mapped.lerp(point+source_offset,seam)

func _build_approved_arm(side: String) -> void:
	var rect := [360,345,355,285]
	var vertices := PackedVector2Array()
	var coordinates := PackedVector2Array()
	var sets := [PackedFloat32Array(),PackedFloat32Array(),PackedFloat32Array(),PackedFloat32Array()]
	var upper: Vector2 = (arm_rest_points[1]-arm_rest_points[0]).normalized()
	var lower: Vector2 = (arm_rest_points[2]-arm_rest_points[1]).normalized()
	var elbow_axis := (upper+lower).normalized()
	var material_key := "arm_relaxed_v19"
	var side_key := "arm_"+side
	if reconstructed_materials.has(side_key):
		if _load_reconstructed_texture(side_key,get_script().resource_path.get_base_dir().path_join("art/yuxu_fullbody_v15")): material_key = side_key
	for row in range(ROWS+1):
		for column in range(COLUMNS+1):
			var point := Vector2(float(rect[0])+float(rect[2])*column/COLUMNS,float(rect[1])+float(rect[3])*row/ROWS)
			var forearm := smoothstep(-42.0,42.0,(point-arm_rest_points[1]).dot(elbow_axis))
			var hand := smoothstep(-18.0,20.0,(point-arm_rest_points[2]).dot(lower))
			var seam := (1.0-smoothstep(375.0,420.0,point.y))*(1.0-smoothstep(470.0,540.0,point.x))
			vertices.append(_world(_arm_reproject(point,side,forearm,hand,seam)))
			coordinates.append(_registered_uv(material_key,point))
			sets[0].append((1.0-forearm)*(1.0-seam))
			sets[1].append(forearm*(1.0-hand)*(1.0-seam))
			sets[2].append(forearm*hand*(1.0-seam))
			sets[3].append(seam)
	texture = textures[material_key]
	_create_mesh(side+"_arm",rect,vertices,[side+"_upper",side+"_forearm",side+"_hand","torso"],sets,70 if side=="near" else 40,coordinates)
	var mesh: Polygon2D = meshes.back()
	if side=="far": mesh.modulate = Color(0.90,0.93,0.94,1.0)
	# Existing gesture variants only replace their hand region, as defined by
	# the shared shader mask; the new ink sleeve remains the base texture.
	var material := ShaderMaterial.new()
	material.shader = load(get_script().resource_path.get_base_dir().path_join("yuxu_hand_blend.gdshader"))
	material.set_shader_parameter("open_hand",textures.arm_open_v19)
	material.set_shader_parameter("sword_hand",textures.arm_sword_fingers_v21)
	mesh.material = material
	arm_materials[side] = material

func _build_approved_leg(side: String) -> void:
	super._build_approved_leg(side)
	_register_mesh_uv(meshes.back(),"leg")

func _cache_sole_profile(side: String) -> void:
	var source: Image = textures.leg.get_image()
	var points := _leg_points(side)
	var sole := PackedVector2Array()
	for x in range(330,515,3):
		for y in range(1488,1435,-1):
			var uv := _registered_uv("leg",Vector2(x,y))
			if source.get_pixel(clampi(roundi(uv.x),0,source.get_width()-1),clampi(roundi(uv.y),0,source.get_height()-1)).a<0.92: continue
			var source_axis := _interpolate(float(y),[760.0,1080.0,1417.0],[410.0,386.0,384.0])
			var target_axis := _interpolate(float(y),[760.0,1080.0,1417.0],[points[0].x,points[1].x,points[2].x])
			sole.append(Vector2(float(x)+target_axis-source_axis-points[2].x,float(y)-1417.0)*PIXEL_SCALE)
			break
	sole_profiles[side] = sole
	if sole.is_empty(): validation_errors.append("No opaque registered sole for "+side)

func _integrate_garment() -> void:
	var anchor_drive := _sample_anchor_drive()
	var source_cloth: float = _source_sample("Run_New1",_source_phase(gait_phase),"cloth_root_angle")
	reference_cloth_base = clampf(0.62+(source_cloth-0.75)*0.70,0.52,0.76)
	reference_target_wind_load = signf(cape_wind)*sqrt(absf(cape_wind))
	var opposite_load := reference_target_wind_load*reference_trailing_wind_load<0.0
	var unloading := absf(reference_target_wind_load)<absf(reference_trailing_wind_load)
	var load_time := 0.14 if opposite_load else (0.20 if unloading else 0.08)
	reference_trailing_wind_load = lerpf(reference_trailing_wind_load,reference_target_wind_load,1.0-exp(-motion_delta/load_time))
	var dt := motion_delta/8.0
	for substep in range(8):
		var parent_angle := 0.0
		var parent_speed := 0.0
		for segment in range(3):
			var stiffness: float = [65.0,39.0,22.0][segment]
			var gravity: float = [29.0,24.0,17.0][segment]
			var damping: float = [10.2,8.4,4.6][segment]
			var inertia: float = [4.0,7.0,10.0][segment]
			var root_target := reference_cloth_base+29.0/65.0*sin(reference_cloth_base)
			var target := reference_trailing_wind_load*float([root_target,0.23,0.28][segment])
			var angle := garment_angles[segment]
			var torque := (target-angle)*stiffness-gravity*sin(parent_angle+angle)
			torque += garment_drive*inertia*cos(parent_angle+angle)-parent_speed*1.8
			torque += anchor_drive*float([0.0,1.2,3.0][segment])
			garment_angular_velocity[segment] += (torque-garment_angular_velocity[segment]*damping)*dt
			angle += garment_angular_velocity[segment]*dt
			var limit: float = [0.90,0.34,0.38][segment]
			if absf(angle)>limit:
				angle = clampf(angle,-limit,limit)
				garment_angular_velocity[segment] *= 0.25
			garment_angles[segment] = angle
			parent_angle += angle
			parent_speed += garment_angular_velocity[segment]
	# Compatibility mirrors describe the one shared physical chain.
	reference_front_angles = garment_angles
	reference_front_angular_velocity = garment_angular_velocity
	garment_simulation_steps += 1

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	super.update_cloth(clip,time,duration,tracks)
	if clip=="BindPose": return
	reference_front_anchor = cape_rear_anchor
	reference_front_tip = cape_rear_tip

func get_garment_diagnostics() -> Dictionary:
	var result := super.get_garment_diagnostics()
	result.mode = "reconstructed_continuous_ink_garment"
	result.continuous_mesh = true
	result.original_split_panels = false
	result.shared_cloth_chain = ["back_cloth","back_hem","back_edge"]
	result.reconstructed_arm_lower_upper_ratios = reconstructed_arm_ratios
	result.reconstructed_sleeve_width = reconstructed_sleeve_width
	result.reconstructed_loaded_materials = reconstructed_loaded_materials
	result.reconstructed_registration = reconstructed_registration
	result.faceless_art_policy = "faceless_cutout_only"
	return result

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "Yuxu ink reconstruction / continuous garment and individual arm proportions"
	return result
