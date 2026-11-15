extends Node2D

const OutfitRuntime = preload("approved_outfit_fitting_runtime.gd")
const FullArmLayer = preload("approved_outfit_full_arm_layer.gd")
const ORIGINAL_SWORD := "dcc95466b3b9430131d73a4b2d99f89c1e5a8c64a25fd42f9c54bc3b37302721"
var failures: Array[String] = []
var checks := 0
var poses := 0
var maximum_wrist_drift := 0.0
var maximum_shape_error := 0.0
var maximum_palm_width_error := 0.0
var maximum_socket_error := 0.0
var maximum_bone_length_error := 0.0

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value and failures.size()<40: failures.append(message)

func _save_and_quit(layer: RefCounted = null) -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"poses":poses,"failures":failures,"maximum_wrist_drift":maximum_wrist_drift,"maximum_rigid_hand_shape_error":maximum_shape_error,"maximum_palm_width_error":maximum_palm_width_error,"maximum_weapon_socket_error":maximum_socket_error,"maximum_bone_length_error":maximum_bone_length_error,"scope":"Five actual complete arm sources, assigned whole-arm shader on arm and shoulder cap, direct fixed-bone wrist, equal registered physical palms and rigid distal hand across both facings and sampled motions, registered grip socket, protected v21 exact-source branch. Does not claim visual anatomy approval.","full_arm_layer":layer.get_diagnostics() if layer!=null else {}}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output"))
	FileAccess.open("res://output/approved_outfit_full_arms_smoke.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("APPROVED COMPLETE ARMS ","PASS" if failures.is_empty() else "FAIL"," ",checks," checks, ",poses," poses, ",JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)

func _ready() -> void:
	var rig := OutfitRuntime.new()
	add_child(rig)
	_check(rig.build(),"current approved outfit builds with complete arm assets")
	if not rig.loaded:
		_check(false,"build errors: "+str(rig.load_errors))
		_save_and_quit()
		return
	var skin: Variant = rig.wanderer_skin
	skin.motion_context = false
	skin.pose_blending = false
	var layer: Variant = skin.get("approved_hands")
	_check(layer!=null and layer.get_script()==FullArmLayer,"actual fitting runtime uses full-arm layer")
	if layer==null or layer.get_script()!=FullArmLayer:
		_save_and_quit()
		return
	_check(layer.assets.size()==5,"all five complete arm assets loaded")
	for pair in [["near_open","far_open"],["near_fist","far_fist"]]:
		_check(layer.assets[pair[0]].sha256!=layer.assets[pair[1]].sha256,"near/far are individually generated complete arms: "+str(pair))
	_check(FileAccess.get_sha256("res://art/yuxu_fullbody_v15/arm_sword_fingers_v21.png")==ORIGINAL_SWORD,"protected sword-fingers original exact checksum")
	var references := {}
	for style in ["open","fist","grip_near","sword_near","auto"]:
		layer.set_pose_override(style)
		for facing in [1.0,-1.0]:
			for zoom in [2.0,5.0]:
				rig.display_zoom = zoom
				for clip in ["Idle","Run_New1","Run_New2","BackstepWalk","Attack1","Step1"]:
					for frame in range(12):
						rig.set_character_pose(clip,float(frame)/12.0,facing)
						layer.update_pose()
						poses += 1
						var inverse: Transform2D = rig.rig_root_node.global_transform.affine_inverse()
						for side in ["near","far"]:
							var state: Dictionary = layer.hands[side]
							var socket: Sprite2D = state.node
							var material: ShaderMaterial = state.material
							_check(socket.texture==null and not socket.visible,"no bare-hand sprite overlays the complete arm")
							_check(state.mesh.material==material and material.shader.resource_path==FullArmLayer.SHADER_PATH,"arm uses complete-source registration shader")
							if state.cap!=null: _check(state.cap.material==material,"shoulder cap uses the same complete arm source")
							var upper: Bone2D = skin.bones[side+"_upper"]
							var elbow: Bone2D = skin.bones[side+"_forearm"]
							var wrist_bone: Bone2D = skin.bones[side+"_hand"]
							var a: Vector2 = inverse*upper.global_position
							var b: Vector2 = inverse*elbow.global_position
							var c: Vector2 = inverse*wrist_bone.global_position
							maximum_bone_length_error = maxf(maximum_bone_length_error,absf(a.distance_to(b)-elbow.rest.origin.length()))
							maximum_bone_length_error = maxf(maximum_bone_length_error,absf(b.distance_to(c)-wrist_bone.rest.origin.length()))
							_check(upper.scale.is_equal_approx(Vector2.ONE) and elbow.scale.is_equal_approx(Vector2.ONE) and wrist_bone.scale.is_equal_approx(Vector2.ONE),"pose does not scale arm or hand bones")
							maximum_wrist_drift = maxf(maximum_wrist_drift,(inverse*layer.get_wrist_transform(side).origin).distance_to(c))
							if style=="sword_near" and side=="near":
								_check(bool(material.get_shader_parameter("keep_original_sword")),"exact original sword branch enabled")
								_check(material.get_shader_parameter("sword_hand")==skin.textures.arm_sword_fingers_v21,"legacy branch samples protected v21 source")
								continue
							_check(not bool(material.get_shader_parameter("keep_original_sword")),"generated whole arm selected")
							_check(material.get_shader_parameter("full_arm")==layer.assets[state.key].texture,"actual complete variant source is bound")
							var registration: Dictionary = layer.registrations[state.key]
							var wrist := Vector2(registration.wrist_px[0],registration.wrist_px[1])
							var width := float(registration.palm_width_px)
							var first: Vector2 = inverse*layer.get_hand_point_global(side,wrist)
							var second: Vector2 = inverse*layer.get_hand_point_global(side,wrist+Vector2(width,0))
							var third: Vector2 = inverse*layer.get_hand_point_global(side,wrist+Vector2(-0.4*width,-1.3*width))
							maximum_palm_width_error = maxf(maximum_palm_width_error,absf(first.distance_to(second)-float(layer.target_palm_width)))
							maximum_wrist_drift = maxf(maximum_wrist_drift,first.distance_to(c))
							var lengths := Vector3(first.distance_to(second),second.distance_to(third),third.distance_to(first))
							if references.has(state.key): maximum_shape_error = maxf(maximum_shape_error,lengths.distance_to(references[state.key]))
							else: references[state.key] = lengths
							var grip := Vector2(registration.grip_px[0],registration.grip_px[1])
							var expected: Vector2 = inverse*layer.get_hand_point_global(side,grip)
							maximum_socket_error = maxf(maximum_socket_error,(inverse*layer.get_attachment_transform(side).origin).distance_to(expected))
	_check(maximum_bone_length_error<0.0001,"both arm bone lengths fixed across all poses and facings")
	_check(maximum_wrist_drift<0.0001,"registered source wrist stays directly on wrist bone without old bare-hand offset")
	_check(maximum_shape_error<0.0001,"distal hand registration remains rigid")
	_check(maximum_palm_width_error<0.0001,"every source palm shares the measured physical width selected from the smaller-hand master")
	_check(maximum_socket_error<0.0001,"weapon socket follows measured whole-arm grip point")
	_check(references.size()==5,"all five full-arm variants exercised")
	layer.set_visible(false)
	_check(not layer.hands.near.mesh.visible and not layer.hands.far.mesh.visible,"visibility hides both complete arm meshes")
	layer.set_visible(true)
	_check(layer.hands.near.mesh.visible and layer.hands.far.mesh.visible,"visibility restores both complete arm meshes")
	_check(FileAccess.get_sha256("res://art/yuxu_fullbody_v15/arm_sword_fingers_v21.png")==ORIGINAL_SWORD,"protected v21 remains exact after all simulations")
	_save_and_quit(layer)
