extends SceneTree

class ReferenceRig extends "guigu_rig_runtime.gd":
	func load_reference() -> bool:
		custom_art_folder = "yuxu_fullbody_v15"
		if not _build_character_hierarchy(_read_json("res://guigu_rig/rig.json")): return false
		if not _load_texture_sets("res://art"): return false
		if not _build_weighted_meshes(_read_json("res://guigu_rig/skins.json"),"res://art"): return false
		wanderer_skin = load("res://reference_walk_skin.gd").new()
		bone_bound_art_ready = wanderer_skin.build(self,"res://art/yuxu_fullbody_v15")
		_load_animation_tracks(_read_json("res://guigu_rig/animations.json"))
		loaded = bone_bound_art_ready
		return loaded

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var rig := ReferenceRig.new()
	root.add_child(rig)
	if not rig.load_reference():
		push_error("Reference walk contact rig did not load")
		quit(1)
		return
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	var maximum_length_error := 0.0
	var maximum_contact_error := 0.0
	var maximum_floor_penetration := 0.0
	var maximum_pelvis_drop := 0.0
	var frames := 0
	var clock := 0.0
	for facing in [1.0,-1.0]:
		for velocity in [Vector2(114,0),Vector2(235,0),Vector2(-114,0),Vector2(0,114),Vector2(1,1).normalized()*114,Vector2.ZERO]:
			for frame in range(90):
				var delta := 1.0/60.0
				clock += delta
				rig.position += velocity*delta
				skin.set_motion_context(delta,velocity,clock,facing)
				skin.set_action_context(false,false)
				var clip := "Idle" if velocity.is_zero_approx() else ("BackstepWalk" if velocity.x*facing<0.0 else "Run_New1")
				rig.set_character_pose(clip,clock,facing)
				for side in ["near","far"]:
					var hip: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_thigh"].global_position)
					var knee: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_calf"].global_position)
					var ankle: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_foot"].global_position)
					var upper: float = skin.bones[side+"_calf"].rest.origin.length()
					var lower: float = skin.bones[side+"_foot"].rest.origin.length()
					maximum_length_error = maxf(maximum_length_error,maxf(absf(knee.distance_to(hip)-upper),absf(ankle.distance_to(knee)-lower)))
					var pitch: float = skin.bones[side+"_thigh"].rotation+skin.bones[side+"_calf"].rotation+skin.bones[side+"_foot"].rotation
					var actual_sole: Vector2 = ankle+skin._sole_contact(side,pitch)
					var reported_lift: float = skin.ground_contacts[side].lift
					maximum_contact_error = maxf(maximum_contact_error,absf(actual_sole.y+reported_lift))
					maximum_floor_penetration = maxf(maximum_floor_penetration,actual_sole.y)
				maximum_pelvis_drop = maxf(maximum_pelvis_drop,skin.bones.pelvis.position.y-skin.bones.pelvis.rest.origin.y)
				frames += 1
	var passed := maximum_length_error<0.002 and maximum_contact_error<0.06 and maximum_floor_penetration<0.06
	var report := {"passed":passed,"sampled_frames":frames,"maximum_leg_length_error":maximum_length_error,"maximum_actual_sole_contact_error":maximum_contact_error,"maximum_floor_penetration":maximum_floor_penetration,"maximum_pelvis_drop":maximum_pelvis_drop}
	var output := FileAccess.open("res://output/reference_walk_actual_contact.json",FileAccess.WRITE)
	output.store_string(JSON.stringify(report,"\t"))
	output.close()
	print("REFERENCE WALK ACTUAL CONTACT: ",JSON.stringify(report))
	rig.queue_free()
	await process_frame
	quit(0 if passed else 1)
