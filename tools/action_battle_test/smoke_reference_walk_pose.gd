extends "capture_reference_walk.gd"

var checks := 0
var failures: Array[String] = []

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)
		push_error(message)

func _bone_error(a, b) -> Dictionary:
	var position_error := 0.0
	var angle_error := 0.0
	for key in a.bones:
		if key.contains("cloth") or key.ends_with("hem") or key.ends_with("edge"): continue
		position_error = maxf(position_error,a.bones[key].position.distance_to(b.bones[key].position))
		angle_error = maxf(angle_error,absf(wrapf(a.bones[key].rotation-b.bones[key].rotation,-PI,PI)))
	return {"position":position_error,"angle":angle_error}

func _ready() -> void:
	var old_rig := ReferenceRig.new()
	old_rig.comparison_mode = "old"
	var rig := ReferenceRig.new()
	add_child(old_rig)
	add_child(rig)
	var paths := ["res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"]
	_check(old_rig.load_runtime(paths[0],paths[1],paths[2],paths[3],paths[4]) and rig.load_runtime(paths[0],paths[1],paths[2],paths[3],paths[4]),"comparison rigs build")
	old_rig.set_skin_mode("experiment")
	rig.set_skin_mode("experiment")
	var original = old_rig.wanderer_skin
	var skin = rig.wanderer_skin
	var clock := 0.0
	var idle_position_error := 0.0
	var idle_angle_error := 0.0
	for frame in range(120):
		clock += 1.0/60.0
		for current in [old_rig,rig]:
			current.wanderer_skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,1.0)
			current.set_character_pose("Idle",clock,1.0)
		var error := _bone_error(original,skin)
		idle_position_error = maxf(idle_position_error,float(error.position))
		idle_angle_error = maxf(idle_angle_error,float(error.angle))
	_check(idle_position_error<0.00002 and idle_angle_error<0.00002,"original idle joint pose and breathing remain unchanged")
	var action_results := {}
	for kind in ["AttackIdle","guard","preparing","Step1"]:
		clock += 1.0/60.0
		var clip: String = "Idle" if kind in ["guard","preparing"] else kind
		for current in [old_rig,rig]:
			current.wanderer_skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,1.0)
			current.wanderer_skin.set_action_context(kind=="guard",kind=="preparing")
			current.set_character_pose(clip,0.10,1.0)
		var error := _bone_error(original,skin)
		var pass_action: bool = not skin.reference_pose_active and float(error.position)<0.00002 and float(error.angle)<0.00002
		_check(pass_action,kind+" retains dedicated pose without ordinary-walk override")
		action_results[kind] = {"passed":pass_action,"errors":error}
	skin.set_action_context(false,false)
	for frame in range(90):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(114,0),clock,1.0)
		rig.set_character_pose("Run_New1",clock,1.0)
	var frozen := {}
	for key in skin.bones: frozen[key] = skin.bones[key].transform
	var phase_before: float = skin.gait_phase
	var steps_before: int = skin.garment_simulation_steps
	for frame in range(12):
		skin.set_motion_context(0.0,Vector2.ZERO,clock,-1.0)
		rig.set_character_pose("AttackIdle",0.12,-1.0)
	var unchanged: bool = phase_before==skin.gait_phase and steps_before==skin.garment_simulation_steps
	for key in frozen: unchanged = unchanged and frozen[key]==skin.bones[key].transform
	_check(unchanged,"pause freezes all joints, gait and rear inertia")
	var stop_peak := 0.0
	var brake_lean_minimum := INF
	for frame in range(240):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,1.0)
		rig.set_character_pose("Idle",clock,1.0)
		if frame<30: stop_peak = maxf(stop_peak,skin.garment_angles.length())
		brake_lean_minimum = minf(brake_lean_minimum,float(skin.cloth_positions.get("chest",Vector2.ZERO).x))
	var settled_angle: float = skin.garment_angles.length()
	var settled_speed: float = skin.garment_angular_velocity.length()
	_check(stop_peak>0.01,"rear retains visible momentum immediately after stopping")
	_check(settled_angle<0.002 and settled_speed<0.01,"rear settles after four seconds")
	_check(skin.gait_amount<0.001,"ordinary walk returns to idle after stopping")
	var final_chest: float = skin.cloth_positions.get("chest",Vector2.ZERO).x
	var final_chest_speed: float = skin.cloth_speeds.get("chest",Vector2.ZERO).x
	_check(brake_lean_minimum< -0.002 and absf(final_chest)<0.002 and absf(final_chest_speed)<0.01,"braking gives a small reverse lean then settles")
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"idle_maximum_position_error":idle_position_error,"idle_maximum_angle_error":idle_angle_error,"dedicated_actions":action_results,"stop_angle_peak":stop_peak,"settled_angle":settled_angle,"settled_angular_speed":settled_speed,"final_gait_amount":skin.gait_amount,"brake_lean_minimum":brake_lean_minimum,"final_chest_angle":final_chest,"final_chest_speed":final_chest_speed}
	var file := FileAccess.open("res://output/reference_walk_pose_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("REFERENCE WALK POSE ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks")
	get_tree().quit(0 if failures.is_empty() else 1)
