extends "capture_reference_walk.gd"

func _contact(skin, rig, side: String) -> Dictionary:
	var ankle: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_foot"].global_position)
	var pitch: float = skin.bones[side+"_thigh"].rotation+skin.bones[side+"_calf"].rotation+skin.bones[side+"_foot"].rotation
	return {"sole_y":(ankle+skin._sole_contact(side,pitch)).y,"lift":skin.ground_contacts[side].lift}

func _ready() -> void:
	var rig := ReferenceRig.new()
	add_child(rig)
	assert(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"))
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	skin.set_motion_context(1.0/60.0,Vector2(114,0),1.0/60.0,1.0)
	rig.set_character_pose("Run_New1",1.0/60.0,1.0)
	skin.gait_phase = 0.60
	skin.gait_amount = 1.0
	skin.gait_speed_smoothed = 114.0
	skin.gait_lift = 4.0
	skin.gait_stride_half = 17.88
	rig.set_character_pose("Run_New1",1.0/60.0,1.0)
	var before := {"near":_contact(skin,rig,"near"),"far":_contact(skin,rig,"far")}
	skin.cloth_positions["chest"] = Vector2(0.08,0.0)
	skin.cloth_speeds["chest"] = Vector2(0.20,0.0)
	skin.set_motion_context(1.0/60.0,Vector2(114,0),2.0/60.0,-1.0)
	var mapped_phase: float = skin.pending_gait_phase
	var world_lean_error: float = absf(float(skin.cloth_positions["chest"].x)*skin.motion_facing-0.08)
	var world_lean_speed_error: float = absf(float(skin.cloth_speeds["chest"].x)*skin.motion_facing-0.20)
	rig.set_character_pose("Run_New1",2.0/60.0,-1.0)
	var after := {"near":_contact(skin,rig,"near"),"far":_contact(skin,rig,"far")}
	var passed := absf(mapped_phase-0.02)<0.000001 and world_lean_error<0.000001 and world_lean_speed_error<0.000001
	for side in ["near","far"]:
		passed = passed and absf(float(after[side].sole_y))<0.002 and float(after[side].lift)<0.002
	var report := {"passed":passed,"old_phase":0.60,"support_fraction":skin.WALK_SUPPORT,"mapped_phase":mapped_phase,"before":before,"after":after,"rendered_phase":skin.gait_phase,"world_lean_turn_error":world_lean_error,"world_lean_speed_turn_error":world_lean_speed_error}
	var destination := "res://output/reference_walk_turn_check.json" if passed else "res://output/reference_walk_turn_before_fix.json"
	var file := FileAccess.open(destination,FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("REFERENCE TURN CHECK: ",JSON.stringify(report))
	get_tree().quit(0 if passed else 1)
