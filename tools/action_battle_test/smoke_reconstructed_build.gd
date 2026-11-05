extends "smoke_guigu_guard_run.gd"

func _ready() -> void:
	var rig := _make_rig("res://guigu_reconstructed_skin.gd")
	var skin = rig.wanderer_skin
	var maximum_length_error := 0.0
	var maximum_sole_error := 0.0
	for frame in range(240):
		var clock := float(frame+1)/60.0
		var velocity := Vector2(114,0) if frame<180 else Vector2.ZERO
		skin.set_motion_context(1.0/60.0,velocity,clock,1.0)
		rig.set_character_pose("Run_New1" if frame<180 else "Idle",clock,1.0)
		for side in ["near","far"]:
			var shoulder := _point(rig,side+"_upper")
			var elbow := _point(rig,side+"_forearm")
			var hand := _point(rig,side+"_hand")
			maximum_length_error = maxf(maximum_length_error,maxf(absf(shoulder.distance_to(elbow)-skin.bones[side+"_forearm"].rest.origin.length()),absf(elbow.distance_to(hand)-skin.bones[side+"_hand"].rest.origin.length())))
			var ankle := _point(rig,side+"_foot")
			var pitch: float = skin.bones[side+"_thigh"].rotation+skin.bones[side+"_calf"].rotation+skin.bones[side+"_foot"].rotation
			var sole: Vector2 = ankle+skin._sole_contact(side,pitch)
			maximum_sole_error = maxf(maximum_sole_error,absf(sole.y+float(skin.ground_contacts[side].lift)))
	var actual_ratios := {}
	for side in ["near","far"]: actual_ratios[side] = skin.bones[side+"_hand"].rest.origin.length()/skin.bones[side+"_forearm"].rest.origin.length()
	var passed: bool = skin.meshes.size()==6 and skin.cape_body_records.size()==1 and skin.reconstructed_shared_body.get_child_count()==0 and maximum_length_error<0.002 and maximum_sole_error<0.06 and absf(actual_ratios.near-0.77)<0.0001 and absf(actual_ratios.far-0.45)<0.0001 and skin.validation_errors.is_empty()
	var report := {"passed":passed,"mesh_count":skin.meshes.size(),"body_mesh_count":skin.cape_body_records.size(),"body_copied_children":skin.reconstructed_shared_body.get_child_count(),"actual_arm_lower_upper_ratios":actual_ratios,"maximum_arm_length_error":maximum_length_error,"maximum_sole_error":maximum_sole_error,"materials":skin.reconstructed_loaded_materials,"errors":skin.validation_errors}
	var file := FileAccess.open("res://output/reconstructed_build_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("RECONSTRUCTED BUILD ","PASS" if passed else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if passed else 1)
