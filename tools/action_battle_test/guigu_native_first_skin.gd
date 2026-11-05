extends RefCounted

## Original male101 TRS, bind poses, weights and full-canvas UV at 100 PPU.
## All simulation is 3D Unity-space; only final vertices are projected to 2D.
const PIXELS_PER_UNIT := 100.0
# Source Run1 has two steps per0.583333s. Its ankle excursion supports the
# project's235px/s ordinary pace, rather than the old114px/s short-gait fixture.
const ROBED_MOVE_SPEED := 235.0
var runtime: Node2D
var display_scale := 1.0
var validation_errors: Array[String] = []
var rig_data: Dictionary = {}
var skins_data: Dictionary = {}
var animation_data: Dictionary = {}
var clips: Dictionary = {}
var curve_sets: Dictionary = {}
var rest_trs: Dictionary = {}
var animated_world: Dictionary = {}
var world_transforms: Dictionary = {}
var mesh_records: Array[Dictionary] = []
var meshes: Array[Polygon2D] = []
var bones: Dictionary = {}
var attachment_paths: Dictionary = {}
var native_physics: RefCounted
var physics_enabled := true
var use_original_textures := false
var motion_context := false
var motion_delta := 0.0
var motion_clock := 0.0
var motion_facing := 1.0
var motion_velocity := Vector2.ZERO
var last_motion_clock := -INF
var last_clip := ""
var sampled_time := 0.0
var native_height := 1.0
var ground_offset := Vector2.ZERO
var approved_sword: RefCounted
var native_hand_socket: RefCounted
var contact_shadow: Node2D
var physics_initialized := false
var physics_actor_frame := Transform3D.IDENTITY
var physics_facing := 1.0
var last_pose_input_time := -INF
var grounding_profile: Dictionary = {}
var locomotion_time := 0.0
var locomotion_layer_enabled := true
var locomotion_was_active := false
var stop_blend_elapsed := 0.12
var stopped_locomotion_locals: Dictionary = {}
var compose_state: Dictionary = {}
var action_guard := false
var action_preparing := false
var combo_stage := 0
var combo_phase := 0.0
var pre_weapon_world: Dictionary = {}
var last_weapon_revision := -1
var last_action_signature := ""
var stopped_ground_offset := 0.0
var ground_contact_enabled := true
var contact_elevation := 0.0
var contact_measurements: Array[Dictionary] = []
var contact_state: Dictionary = {}

func _folder() -> String:
	return get_script().resource_path.get_base_dir()

func _data_folder() -> String:
	return ProjectSettings.globalize_path(_folder()).path_join("../../resources/appearance/guigu_native_first_20261105").simplify_path()

func _v(value: Array) -> Vector3:
	return Vector3(float(value[0]),float(value[1]),float(value[2]))

func _matrix(rows: Array) -> Transform3D:
	return Transform3D(Basis(Vector3(rows[0][0],rows[1][0],rows[2][0]),Vector3(rows[0][1],rows[1][1],rows[2][1]),Vector3(rows[0][2],rows[1][2],rows[2][2])),Vector3(rows[0][3],rows[1][3],rows[2][3]))

func build(owner_runtime: Node2D, _art_dir: String = "") -> bool:
	runtime = owner_runtime
	var data_folder := _data_folder()
	for key in ["rig","skins","animations"]:
		var path := data_folder.path_join(key+".json")
		if not FileAccess.file_exists(path):
			validation_errors.append("Missing original male source "+path)
			return false
	rig_data = JSON.parse_string(FileAccess.get_file_as_string(data_folder.path_join("rig.json")))
	skins_data = JSON.parse_string(FileAccess.get_file_as_string(data_folder.path_join("skins.json")))
	animation_data = JSON.parse_string(FileAccess.get_file_as_string(data_folder.path_join("animations.json")))
	grounding_profile = JSON.parse_string(FileAccess.get_file_as_string(data_folder.path_join("grounding_profile.json")))
	for node in rig_data.nodes: rest_trs[node.path] = node.localTRS
	for clip in animation_data.clips:
		clips[clip.name] = clip
		var curves := {}
		for frame in clip.decodedStreamFrames:
			for row in frame[1]:
				var index := int(row[0])
				if not curves.has(index): curves[index] = []
				curves[index].append([float(frame[0]),float(row[1]),float(row[2]),float(row[3]),float(row[4])])
		curve_sets[clip.name] = curves
		if int(clip.rawDenseClip.curveCount)!=0: validation_errors.append("Unexpected dense channels in selected source")
	animated_world = evaluate_animation("Idle",0.0)
	var selected := []
	for mesh in skins_data.meshes:
		if mesh.activeInHierarchy and mesh.enabled and mesh.category=="character": selected.append(mesh)
	selected.sort_custom(func(a: Dictionary,b: Dictionary)->bool: return float(a.rendererToRigRoot[2][3])>float(b.rendererToRigRoot[2][3]))
	for index in range(selected.size()):
		var source: Dictionary = selected[index]
		var id := str(int(source.sourceSpriteId))
		var art_folder := _folder().path_join("art/guigu_native_first_20261105")
		var source_path := art_folder.path_join("source/sprite_"+id+".png")
		var paint_path := art_folder.path_join("sprite_"+id+".png")
		var original := Image.load_from_file(source_path)
		var painted := Image.load_from_file(paint_path)
		if original==null or painted==null or original.get_size()!=painted.get_size():
			validation_errors.append("Original canvas mismatch "+id)
			return false
		var polygon := Polygon2D.new()
		polygon.name = "NativeMale101_"+id+"_"+str(index)+("_head" if id in ["65806","87679"] else "")
		polygon.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		original.generate_mipmaps()
		painted.generate_mipmaps()
		var original_texture := ImageTexture.create_from_image(original)
		var painted_texture := ImageTexture.create_from_image(painted)
		polygon.texture = original_texture if use_original_textures else painted_texture
		var uv := PackedVector2Array()
		for point in source.uvConvention.uvTextureLowerLeft:
			uv.append(Vector2(float(point[0])*original.get_width(),(1.0-float(point[1]))*original.get_height()))
		polygon.uv = uv
		var triangles := []
		for triangle in source.triangles: triangles.append(PackedInt32Array(triangle))
		polygon.polygons = triangles
		polygon.z_index = 30+index
		runtime.rig_root_node.add_child(polygon)
		var bindposes: Array[Transform3D] = []
		for matrix in source.skinning.bindposeMatrices: bindposes.append(_matrix(matrix))
		mesh_records.append({"mesh":polygon,"source":source,"sprite_id":int(source.sourceSpriteId),"renderer_path":str(source.rendererPath),"bindposes":bindposes,"source_texture":original_texture,"paint_texture":painted_texture,"texture_size":original.get_size(),"source_path":source_path,"paint_path":paint_path,"original_uv":uv,"original_triangles":triangles,"own_hair_override":id=="87679"})
		meshes.append(polygon)
	var bounds := Rect2()
	var first := true
	var leg_bottom := -INF
	for record in mesh_records:
		var points := deform_mesh(record,animated_world,false)
		for point in points:
			if first: bounds = Rect2(point,Vector2.ZERO);first = false
			else: bounds = bounds.expand(point)
			if record.sprite_id==87557: leg_bottom = maxf(leg_bottom,point.y)
	ground_offset = Vector2(0.0,leg_bottom)
	native_height = leg_bottom-bounds.position.y
	_build_attachments()
	native_physics = load(_folder().path_join("guigu_native_dynamic_bone.gd")).new()
	var config: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(data_folder.path_join("dynamic_bone_config.json")))
	if not native_physics.setup(config,rig_data.nodes): validation_errors.append_array(native_physics.setup_errors)
	contact_shadow = load(_folder().path_join("guigu_native_contact_shadow.gd")).new()
	contact_shadow.name = "NativeFootAnchor"
	# Ground shadows stay at the actor foot anchor while the rig bobs above it.
	runtime.add_child(contact_shadow)
	update_pose("Idle",0.0,1.0)
	native_hand_socket = load(_folder().path_join("guigu_native_hand_socket.gd")).new()
	native_hand_socket.build(self)
	approved_sword = load(_folder().path_join("approved_outfit_sword_layer.gd")).new()
	if not approved_sword.build(self,native_hand_socket,_folder().path_join("art/approved_outfit_sword_20261105")):
		validation_errors.append_array(approved_sword.errors)
	return validation_errors.is_empty() and meshes.size()==7

func _build_attachments() -> void:
	var body := str(rig_data.rigRootPath)+"/Body"
	var chest := body+"/yifu/yifu/shenti1"
	attachment_paths = {"head":chest+"/tou","torso":chest,"pelvis":body+"/yifu/yifu/shenti2"}
	for side in ["near","far"]:
		var arm := "youbi" if side=="near" else "zuobi"
		var arm_path := chest+"/"+arm+"/"+arm+"/"+arm+"1"
		attachment_paths[side+"_upper"] = arm_path
		attachment_paths[side+"_forearm"] = arm_path+"/"+arm+"2"
		attachment_paths[side+"_hand"] = arm_path+"/"+arm+"2/"+arm+"3"
		var leg := "zuotui" if side=="near" else "youtui"
		var leg_path := body+"/Foot/"+leg+"/"+leg+"/tui1"
		attachment_paths[side+"_thigh"] = leg_path
		attachment_paths[side+"_calf"] = leg_path+"/tui2"
		attachment_paths[side+"_foot"] = leg_path+"/tui2/tui3"
	for key in attachment_paths:
		var anchor := Node2D.new()
		anchor.name = "NativeSourceAnchor_"+key
		runtime.rig_root_node.add_child(anchor)
		bones[key] = anchor

func _rotate_source_branch(path: String, pivot: Vector3, rotation: Quaternion) -> void:
	var basis := Basis(rotation)
	var delta := Transform3D(basis,pivot-basis*pivot)
	for candidate in world_transforms:
		if candidate==path or str(candidate).begins_with(path+"/"):
			world_transforms[candidate] = delta*(world_transforms[candidate] as Transform3D)

func _refresh_attachments() -> void:
	for key in attachment_paths:
		var transform: Transform3D = world_transforms[attachment_paths[key]]
		bones[key].position = Vector2(transform.origin.x,-transform.origin.y)*PIXELS_PER_UNIT-ground_offset
		bones[key].rotation = atan2(-transform.basis.x.y,transform.basis.x.x)

func _aim_arm(side: String, upper_angle: float, lower_angle: float, wrist_angle: float) -> void:
	var upper: String = attachment_paths[side+"_upper"]
	var lower: String = attachment_paths[side+"_forearm"]
	var hand: String = attachment_paths[side+"_hand"]
	var a: Vector3 = (world_transforms[upper] as Transform3D).origin
	var b: Vector3 = (world_transforms[lower] as Transform3D).origin
	_rotate_source_branch(upper,a,Quaternion((b-a).normalized(),Vector3(cos(upper_angle),-sin(upper_angle),0)))
	b = (world_transforms[lower] as Transform3D).origin
	var c: Vector3 = (world_transforms[hand] as Transform3D).origin
	_rotate_source_branch(lower,b,Quaternion((c-b).normalized(),Vector3(cos(lower_angle),-sin(lower_angle),0)))
	c = (world_transforms[hand] as Transform3D).origin
	var hand_axis: Vector3 = (world_transforms[hand] as Transform3D).basis.x
	var current_axis := atan2(-hand_axis.y,hand_axis.x)
	_rotate_source_branch(hand,c,Quaternion(Vector3.BACK,-angle_difference(current_axis,lower_angle+wrist_angle)))
	_refresh_attachments()

func _channel(clip: Dictionary, index: int, time: float) -> float:
	var stream_count := int(clip.rawStreamedClip.curveCount)
	if index>=stream_count:
		var constant: Array = clip.rawConstantClip.data
		return float(constant[index-stream_count]) if index-stream_count<constant.size() else 0.0
	var values: Array = curve_sets[clip.name].get(index,[])
	if values.is_empty(): return 0.0
	var chosen: Array = values[0]
	for value in values:
		if float(value[0])>time: break
		chosen = value
	var dt := maxf(0.0,time-float(chosen[0]))
	return ((float(chosen[1])*dt+float(chosen[2]))*dt+float(chosen[3]))*dt+float(chosen[4])

func _evaluate_locals(name: String, time: float) -> Dictionary:
	var clip: Dictionary = clips.get(name,clips.get("Idle",{}))
	if clip.is_empty(): return {}
	var locals := {}
	for node in rig_data.nodes:
		var trs: Dictionary = node.localTRS
		var q: Array = trs.rotationQuaternionXYZW
		locals[node.path] = {"position":_v(trs.position),"scale":_v(trs.scale),"basis":Basis(Quaternion(q[0],q[1],q[2],q[3]).normalized())}
	for binding in clip.bindings:
		var path := str(binding.targetRigPath) if binding.targetRigPath!=null else ""
		if not locals.has(path): continue
		var values := []
		for component in range(int(binding.width)): values.append(_channel(clip,int(binding.curveIndex)+component,time))
		match int(binding.attributeId):
			1: locals[path].position = _v(values)
			2: locals[path].basis = Basis(Quaternion(values[0],values[1],values[2],values[3]).normalized())
			3: locals[path].scale = _v(values)
			4:
				locals[path].basis = Basis(Vector3.UP,deg_to_rad(values[1]))*Basis(Vector3.RIGHT,deg_to_rad(values[0]))*Basis(Vector3.BACK,deg_to_rad(values[2]))
	return locals

func _compose_world(locals: Dictionary) -> Dictionary:
	var world := {}
	for node in rig_data.nodes:
		var local: Dictionary = locals[node.path]
		var transform := Transform3D((local.basis as Basis).scaled_local(local.scale),local.position)
		world[node.path] = (world[node.parentPath] as Transform3D)*transform if node.parentPath!=null and world.has(node.parentPath) else transform
	return world

func evaluate_animation(name: String, time: float) -> Dictionary:
	return _compose_world(_evaluate_locals(name,time))

func _sample_time(name: String, time: float) -> float:
	var clip: Dictionary = clips[name]
	return fposmod(time,float(clip.duration)) if bool(clip.loopTime) and float(clip.duration)>0.0 else clampf(time,0.0,float(clip.duration))

func _mix_locals(from: Dictionary, to: Dictionary, weight: float) -> Dictionary:
	var result := {}
	for path in to:
		var a: Dictionary = from[path]
		var b: Dictionary = to[path]
		result[path] = {"position":(a.position as Vector3).lerp(b.position,weight),"scale":(a.scale as Vector3).lerp(b.scale,weight),"basis":Basis((a.basis as Basis).get_rotation_quaternion().slerp((b.basis as Basis).get_rotation_quaternion(),weight))}
	return result

func _composed_animation(source_name: String, time: float, delta: float) -> Dictionary:
	var moving := motion_context and motion_velocity.length()>0.5 and source_name!="Step1"
	var attacking := source_name in ["AttackIdle","AttackRun"] or action_guard or action_preparing
	var base_name := source_name
	var upper_name := source_name
	var local_pose := _evaluate_locals(source_name,time)
	if source_name=="Step1":
		# Dodge owns its whole-body clip, including its own landing offset.
		# Entering it from a run must not spend most of the dodge collecting a step.
		locomotion_was_active = false
		stop_blend_elapsed = 0.12
		stopped_locomotion_locals.clear()
		compose_state = {"locomotion_active":false,"moving_attack":false,"locomotion_time":locomotion_time,"base_clip":source_name,"upper_clip":source_name,"stop_blend":1.0}
		return _compose_world(local_pose)
	if moving:
		# Advance by travelled source-space distance, independently of attack
		# phase and actor display scale. Slow casts must not keep sprinting feet.
		locomotion_time += delta*motion_velocity.length()/ROBED_MOVE_SPEED
		base_name = "Run_New2" if source_name=="Run_New2" else "Run_New1"
		local_pose = _evaluate_locals(base_name,_sample_time(base_name,locomotion_time))
		stopped_locomotion_locals = local_pose.duplicate(true)
		stop_blend_elapsed = 0.0
	elif locomotion_was_active or stop_blend_elapsed<0.12:
		stop_blend_elapsed = minf(0.12,stop_blend_elapsed+delta)
		# Stop advancing the stride immediately; collect the last planted pose.
		# A lingering caller walk_amount must not restart a full-body run.
		base_name = "Idle" if source_name in ["Run_New1","Run_New2"] else source_name
		local_pose = _evaluate_locals(base_name,_sample_time(base_name,time))
		if not stopped_locomotion_locals.is_empty():
			local_pose = _mix_locals(stopped_locomotion_locals,local_pose,smoothstep(0.0,0.12,stop_blend_elapsed))
	elif motion_context and source_name in ["Run_New1","Run_New2"]:
		base_name = "Idle"
		local_pose = _evaluate_locals("Idle",_sample_time("Idle",time))
	if attacking and (moving or stop_blend_elapsed<0.12):
		upper_name = source_name if source_name in ["AttackIdle","AttackRun"] else "AttackIdle"
		var upper_pose := _evaluate_locals(upper_name,_sample_time(upper_name,time if upper_name==source_name else 0.0))
		var chest: String = attachment_paths.torso
		# Keep the run carrier, pelvis, cloth and complete Foot branch together.
		# Overlay rotations only: no attack can lengthen a limb or shift its seam.
		for path in local_pose:
			if path==chest or str(path).begins_with(chest+"/"):
				local_pose[path].basis = upper_pose[path].basis
	locomotion_was_active = moving
	compose_state = {"locomotion_active":moving,"moving_attack":moving and attacking,"locomotion_time":locomotion_time,"locomotion_rate":motion_velocity.length()/ROBED_MOVE_SPEED if moving else 0.0,"gait_reference_speed":ROBED_MOVE_SPEED,"base_clip":base_name,"upper_clip":upper_name,"stop_blend":clampf(stop_blend_elapsed/0.12,0.0,1.0)}
	return _compose_world(local_pose)

func _visible_sole_bottom(world: Dictionary) -> float:
	var bottom := -INF
	for contact in _measure_sole_contacts(world): bottom = maxf(bottom,float(contact.sole_y))
	return bottom

func _measure_sole_contacts(world: Dictionary) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for record in mesh_records:
		if record.sprite_id!=87557: continue
		var points := deform_mesh(record,world)
		var hull := PackedVector2Array()
		var bottom := -INF
		for sample in grounding_profile.source_solid_sole_sample_hull:
			var indices: Array = sample.triangle_vertices
			var weights: Array = sample.barycentric_weights
			var point := Vector2.ZERO
			for i in range(3): point += points[int(indices[i])]*float(weights[i])
			hull.append(point)
			bottom = maxf(bottom,point.y)
		var min_x := INF
		var max_x := -INF
		for point in hull:
			if point.y>=bottom-0.35:
				min_x = minf(min_x,point.x)
				max_x = maxf(max_x,point.x)
		var side := "near" if str(record.source.skinning.spriteBones[0].rigPath).contains("/zuotui/") else "far"
		result.append({"side":side,"x":(min_x+max_x)*0.5,"sole_y":bottom,"width":max_x-min_x})
	return result

func _contact_ground_offset(base_clip: String, ground_y: float, active: bool) -> float:
	contact_measurements = _measure_sole_contacts(animated_world)
	var bottom := -INF
	for contact in contact_measurements: bottom = maxf(bottom,float(contact.sole_y))
	var raw_height := maxf(0.0,-(bottom+ground_y))
	var flight_height := raw_height
	if active:
		var source_max := float(grounding_profile.clips[base_clip].remaining_source_flight_range_pixels[1])
		var onset := source_max*(8.0/11.969227382003757)
		var flight_ratio := clampf((raw_height-onset)/maxf(0.001,source_max-onset),0.0,1.0)
		# Keep a brief flight, but collect the long low-hover part into support.
		# This single display translation cannot stretch a shoe or a limb.
		flight_height = minf(2.0,source_max*0.25)*flight_ratio*flight_ratio
	var correction := raw_height-flight_height if active else 0.0
	contact_state = {"enabled":ground_contact_enabled,"active":active,"raw_flight_height":raw_height,"flight_height":flight_height,"render_correction_y":correction,"base_clip":base_clip}
	return ground_y+correction

func _update_contact_shadow() -> void:
	var contacts: Array[Dictionary] = []
	var local_elevation := contact_elevation/maxf(0.0001,runtime.global_transform.y.length())
	for measurement in contact_measurements:
		var contact: Dictionary = measurement.duplicate()
		contact.sole_y = float(contact.sole_y)+runtime.rig_root_node.position.y-local_elevation
		contacts.append(contact)
	contact_state.contacts = contacts
	if is_instance_valid(contact_shadow): contact_shadow.update_contacts(contacts,local_elevation)

func has_contact_shadow() -> bool:
	return is_instance_valid(contact_shadow)

func set_ground_elevation(height: float) -> void:
	contact_elevation = maxf(0.0,height)
	# A caller can move an elevated actor at a paused/same motion clock.
	# Only the floor attachment needs refreshing; never integrate a pose here.
	if not contact_measurements.is_empty(): _update_contact_shadow()

func get_contact_diagnostics() -> Dictionary:
	var result := contact_state.duplicate(true)
	result.shadow = contact_shadow.get_diagnostics() if is_instance_valid(contact_shadow) else {}
	return result

func deform_mesh(record: Dictionary, world: Dictionary, offset: bool = true) -> PackedVector2Array:
	var source: Dictionary = record.source
	var matrices: Array[Transform3D] = []
	if source.skinning.isSkinned:
		for index in range(source.skinning.spriteBones.size()):
			matrices.append((world[source.skinning.spriteBones[index].rigPath] as Transform3D)*record.bindposes[index])
	var result := PackedVector2Array()
	for index in range(source.vertices.size()):
		var vertex := _v(source.vertices[index])
		var posed := Vector3.ZERO
		if source.skinning.isSkinned:
			for influence in source.skinning.boneWeights[index]: posed += (matrices[int(influence.boneIndex)]*vertex)*float(influence.weight)
		else: posed = (world[source.rendererPath] as Transform3D)*vertex
		result.append(Vector2(posed.x,-posed.y)*PIXELS_PER_UNIT-(ground_offset if offset else Vector2.ZERO))
	return result

func own_hair_points(record: Dictionary, world: Dictionary) -> PackedVector2Array:
	var uv: Array = record.source.uvConvention.sourceUvTransform
	var transform: Transform3D = world[record.source.rendererPath]
	var points := PackedVector2Array()
	for pixel in [Vector2(0,0),Vector2(21,0),Vector2(21,56),Vector2(0,56)]:
		var local := Vector3((pixel.x-float(uv[1]))/float(uv[0]),(56.0-pixel.y-float(uv[3]))/float(uv[2]),0.0)
		var point := transform*local
		points.append(Vector2(point.x,-point.y)*PIXELS_PER_UNIT-ground_offset)
	return points

func _render_mesh(record: Dictionary) -> void:
	if record.own_hair_override and not use_original_textures:
		record.mesh.polygons = [PackedInt32Array([0,1,2]),PackedInt32Array([0,2,3])]
		record.mesh.uv = PackedVector2Array([Vector2(0,0),Vector2(21,0),Vector2(21,56),Vector2(0,56)])
		record.mesh.polygon = own_hair_points(record,world_transforms)
	else:
		record.mesh.polygons = record.original_triangles
		record.mesh.uv = record.original_uv
		record.mesh.polygon = deform_mesh(record,world_transforms)

func _actor_transform() -> Transform3D:
	var origin: Vector2 = runtime.rig_root_node.global_position/PIXELS_PER_UNIT
	# A reflected 2D basis cannot represent Unity's quaternion rotation.
	# Rotate 180 around Y for physics; render-facing remains an X mirror.
	return Transform3D(Basis(Vector3.UP,PI if motion_facing<0.0 else 0.0),Vector3(origin.x,-origin.y,0.0))

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	# Building at the origin and then placing the actor is not movement.
	if not motion_context or clock<last_motion_clock: reset_secondary_motion()
	motion_context = true
	motion_delta = maxf(0.0,delta)
	motion_velocity = velocity
	motion_clock = clock
	motion_facing = -1.0 if facing<0.0 else 1.0

func update_pose(clip_name: String, time: float, facing: float) -> void:
	motion_facing = -1.0 if facing<0.0 else 1.0
	var weapon_revision := int(approved_sword.pose_revision) if approved_sword!=null else -1
	var action_signature := str([action_guard,action_preparing,combo_stage,combo_phase])
	var same_frame := motion_context and motion_clock==last_motion_clock
	var paused_pose := motion_context and motion_delta<=0.0 and last_clip==clip_name
	var cached_pose := same_frame and last_clip==clip_name and time==last_pose_input_time and action_signature==last_action_signature
	if (paused_pose or cached_pose) and not pre_weapon_world.is_empty() and (not physics_enabled or physics_initialized):
		if weapon_revision!=last_weapon_revision:
			# Equipment can change while paused. Reapply from the solved body,
			# never from a previously rotated arm, and do not integrate cloth.
			world_transforms = pre_weapon_world.duplicate()
			_finish_pose(0.0,clip_name,time)
		return
	var source_name := clip_name
	if clip_name=="BackstepWalk": source_name = "Run_New1"
	if not clips.has(source_name): source_name = "Idle"
	var step_delta := motion_delta if motion_context and not same_frame else 0.0
	var use_locomotion_layer := motion_context and locomotion_layer_enabled
	sampled_time = _sample_time(source_name,time)
	animated_world = _composed_animation(source_name,sampled_time,step_delta) if use_locomotion_layer else evaluate_animation(source_name,sampled_time)
	var ground_name: String = compose_state.base_clip if use_locomotion_layer else source_name
	var ground_y := float(grounding_profile.clips[ground_name].render_offset_y)
	if use_locomotion_layer and not bool(compose_state.locomotion_active) and float(compose_state.stop_blend)<1.0:
		ground_y = lerpf(stopped_ground_offset,ground_y,smoothstep(0.0,1.0,float(compose_state.stop_blend)))
		# A transition between original poses can dip a sole below the ground.
		# Lift the complete model only; leave every bone and running air phase intact.
		ground_y = minf(ground_y,-_visible_sole_bottom(animated_world))
	var collect_run := ground_contact_enabled and use_locomotion_layer and bool(compose_state.locomotion_active) and ground_name in ["Run_New1","Run_New2"]
	ground_y = _contact_ground_offset(ground_name,ground_y,collect_run)
	if use_locomotion_layer and bool(compose_state.locomotion_active): stopped_ground_offset = ground_y
	runtime.rig_root_node.position.y = ground_y
	world_transforms = animated_world.duplicate()
	if physics_enabled and native_physics!=null:
		var actor := _actor_transform()
		if not physics_initialized:
			native_physics.reset(animated_world,actor)
			physics_initialized = true
			physics_actor_frame = actor
			physics_facing = motion_facing
		elif physics_facing!=motion_facing:
			# A visual turn changes the coordinate frame, not cloth velocity.
			# Rebase around the previous origin so this frame's actual travel
			# is still received as object motion by the unmodified solver.
			var turned_frame := Transform3D(actor.basis,physics_actor_frame.origin)
			native_physics.reframe_state(turned_frame*physics_actor_frame.affine_inverse())
			physics_facing = motion_facing
		if motion_context:
			world_transforms = native_physics.step(step_delta,animated_world,actor)
			physics_actor_frame = actor
	pre_weapon_world = world_transforms.duplicate()
	_finish_pose(step_delta,clip_name,time)
	last_clip = clip_name
	last_motion_clock = motion_clock
	last_pose_input_time = time
	last_action_signature = action_signature

func _finish_pose(delta: float, clip_name: String, time: float) -> void:
	_refresh_attachments()
	if approved_sword!=null: approved_sword.update_pose(delta,clip_name,time)
	for record in mesh_records: _render_mesh(record)
	if native_hand_socket!=null: native_hand_socket.update_pose()
	if approved_sword!=null: approved_sword.update_attachment()
	_update_contact_shadow()
	last_weapon_revision = int(approved_sword.pose_revision) if approved_sword!=null else -1

func reset_secondary_motion() -> void:
	physics_initialized = false
	locomotion_time = 0.0
	locomotion_was_active = false
	stop_blend_elapsed = 0.12
	stopped_locomotion_locals.clear()
	pre_weapon_world.clear()

func set_action_context(guard: bool, preparing: bool) -> void:
	action_guard = guard
	action_preparing = preparing

func set_combo_context(stage: int, phase: float) -> void:
	combo_stage = stage
	combo_phase = phase

func set_visible(value: bool) -> void:
	for mesh in meshes: mesh.visible = value
	if is_instance_valid(contact_shadow): contact_shadow.visible = value
	if approved_sword!=null: approved_sword.set_visible(value)

func set_source_textures(original: bool) -> void:
	use_original_textures = original
	for record in mesh_records:
		record.mesh.texture = record.source_texture if original else record.paint_texture
		if not world_transforms.is_empty(): _render_mesh(record)

func get_native_visual_height() -> float:
	return native_height

func summary() -> Dictionary:
	return get_garment_diagnostics()

func get_garment_diagnostics() -> Dictionary:
	return {"mode":"original_male101_native_canvas_3d","errors":validation_errors,"rig_root":rig_data.get("rigRootPath",""),"transforms":rest_trs.size(),"mesh_count":meshes.size(),"native_height":native_height,"pixels_per_unit":PIXELS_PER_UNIT,"project_bone_length_retargeting":false,"source_body_alpha_and_uv":true,"source_alpha_and_uv":use_original_textures,"own_hairstyle_override":not use_original_textures,"visible_hair_motion":"original source DynamicBone" if use_original_textures else "own tied bun rigidly follows original renderer/head anchor","original_textures":use_original_textures,"sampled_clip":last_clip,"sampled_time":sampled_time,"locomotion_layer_enabled":locomotion_layer_enabled,"composition":compose_state.duplicate(),"physics_enabled":physics_enabled,"physics_source":"local Mod SDK DynamicBone.cs + serialized male101 cloth/hair components","physics":native_physics.get_diagnostics() if native_physics!=null else {},"state_machine_scope":"six original clips with continuous Run lower-body and attack upper rotations in actual movement; source evaluator remains separate; 102-only hair binding remains absent on source101"}
