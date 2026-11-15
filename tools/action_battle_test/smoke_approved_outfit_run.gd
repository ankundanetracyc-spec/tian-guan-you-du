extends Node2D

const Runtime = preload("approved_outfit_fitting_runtime.gd")
const BeforeRuntime = preload("res://output/approved_outfit_run_review/before/before_runtime.gd")
var failures: Array[String] = []
var checks := 0
var clock := 0.0
var frames := 0
var maximum_length_error := 0.0
var maximum_arm_length_error := 0.0
var maximum_scale_error := 0.0
var maximum_pitch_error := 0.0
var maximum_contact_height_error := 0.0
var maximum_floor_penetration := 0.0
var expected_leg := Vector2.ZERO
var expected_arm := Vector2.ZERO
var metrics: Dictionary = {}

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value and failures.size()<40: failures.append(message)

func _point(rig: Node2D, bone: Node2D) -> Vector2:
	return rig.rig_root_node.to_local(bone.global_position)

func _measure(rig: Node2D, group: String) -> void:
	var skin = rig.wanderer_skin
	if not metrics.has(group):
		metrics[group] = {"frames":0,"both_feet_airborne":0,"landed_samples":0,"supported_soft_knees":0,"supported_samples":0,"knee_flex_min":INF,"knee_flex_max":-INF,"pitch_min":INF,"pitch_max":-INF,"ankle_x_min":INF,"ankle_x_max":-INF,"max_knee_height":-INF,"max_sole_lift":0.0,"minimum_sole_clearance":INF,"minimum_both_feet_clearance":INF,"flight_samples":[],"pelvis_y_min":INF,"pelvis_y_max":-INF,"near_x":[],"far_x":[]}
	var m: Dictionary = metrics[group]
	m.frames += 1
	m.pelvis_y_min = minf(m.pelvis_y_min,skin.bones.pelvis.position.y)
	m.pelvis_y_max = maxf(m.pelvis_y_max,skin.bones.pelvis.position.y)
	var airborne := 0
	var clearances := []
	var flexes := []
	for side in ["near","far"]:
		var thigh: Bone2D = skin.bones[side+"_thigh"]
		var calf: Bone2D = skin.bones[side+"_calf"]
		var foot: Bone2D = skin.bones[side+"_foot"]
		var hip := _point(rig,thigh)
		var knee := _point(rig,calf)
		var ankle := _point(rig,foot)
		maximum_length_error = maxf(maximum_length_error,maxf(absf(hip.distance_to(knee)-expected_leg.x),absf(knee.distance_to(ankle)-expected_leg.y)))
		var shoulder := _point(rig,skin.bones[side+"_upper"])
		var elbow := _point(rig,skin.bones[side+"_forearm"])
		var wrist := _point(rig,skin.bones[side+"_hand"])
		maximum_arm_length_error = maxf(maximum_arm_length_error,maxf(absf(shoulder.distance_to(elbow)-expected_arm.x),absf(elbow.distance_to(wrist)-expected_arm.y)))
		for bone in [thigh,calf,foot,skin.bones[side+"_upper"],skin.bones[side+"_forearm"],skin.bones[side+"_hand"]]:
			_check(bone.position.is_finite() and is_finite(bone.rotation),group+": finite pose")
			maximum_scale_error = maxf(maximum_scale_error,bone.scale.distance_to(Vector2.ONE))
		var foot_axis: Vector2 = rig.rig_root_node.to_local(foot.to_global(Vector2.RIGHT))-ankle
		var pitch := foot_axis.angle()
		var sole: Vector2 = ankle+skin._sole_contact(side,pitch)
		var contact: Dictionary = skin.ground_contacts[side]
		maximum_pitch_error = maxf(maximum_pitch_error,absf(wrapf(pitch-float(contact.pitch),-PI,PI)))
		maximum_contact_height_error = maxf(maximum_contact_height_error,absf(sole.y-float(contact.center.y)))
		maximum_floor_penetration = maxf(maximum_floor_penetration,sole.y)
		var flex := absf((knee-hip).angle_to(ankle-knee))
		clearances.append(-sole.y)
		flexes.append(flex)
		m.minimum_sole_clearance = minf(m.minimum_sole_clearance,-sole.y)
		if absf(sole.y)<=0.1: m.landed_samples += 1
		if sole.y < -0.5: airborne += 1
		else:
			m.supported_samples += 1
			if flex>0.12: m.supported_soft_knees += 1
		m.knee_flex_min = minf(m.knee_flex_min,flex)
		m.knee_flex_max = maxf(m.knee_flex_max,flex)
		m.pitch_min = minf(m.pitch_min,pitch)
		m.pitch_max = maxf(m.pitch_max,pitch)
		m.ankle_x_min = minf(m.ankle_x_min,ankle.x-hip.x)
		m.ankle_x_max = maxf(m.ankle_x_max,ankle.x-hip.x)
		m.max_knee_height = maxf(m.max_knee_height,hip.y-knee.y)
		m.max_sole_lift = maxf(m.max_sole_lift,-sole.y)
		m[side+"_x"].append(ankle.x-hip.x)
	if airborne==2:
		m.both_feet_airborne += 1
		m.minimum_both_feet_clearance = minf(m.minimum_both_feet_clearance,minf(clearances[0],clearances[1]))
		m.flight_samples.append({"phase":skin.gait_phase,"sole_clearances":clearances,"knee_flex":flexes})
	frames += 1

func _step(rig: Node2D, group: String, clip: String, velocity: Vector2, facing: float, dt: float = 1.0/60.0) -> void:
	clock += dt
	rig.wanderer_skin.set_motion_context(dt,velocity,clock,facing)
	rig.set_character_pose(clip,clock,facing)
	_measure(rig,group)

func _ready() -> void:
	var before := BeforeRuntime.new()
	add_child(before)
	_check(before.build(),"frozen baseline builds")
	expected_leg = before.wanderer_skin.approved_leg_lengths
	expected_arm = Vector2(before.wanderer_skin.bones.near_forearm.rest.origin.length(),before.wanderer_skin.bones.near_hand.rest.origin.length())
	var rig := Runtime.new()
	add_child(rig)
	_check(rig.build(),"current model builds")
	var skin = rig.wanderer_skin
	for side in ["near","far"]:
		_check(absf(skin.bones[side+"_calf"].rest.origin.length()-expected_leg.x)<0.00001,side+": thigh ruler unchanged from before")
		_check(absf(skin.bones[side+"_foot"].rest.origin.length()-expected_leg.y)<0.00001,side+": calf ruler unchanged from before")
	for frame in range(120): _step(rig,"idle_right","Idle",Vector2.ZERO,1.0)
	for frame in range(360): _step(rig,"run_right","Run_New1",Vector2(114,0),1.0)
	for frame in range(240): _step(rig,"run_left","Run_New1",Vector2(-114,0),-1.0)
	for frame in range(150): _step(rig,"backstep_left","BackstepWalk",Vector2(114,0),-1.0)
	for frame in range(240):
		var moving := frame<120
		_step(rig,"start_stop","Run_New1" if moving else "Idle",Vector2(114,0) if moving else Vector2.ZERO,1.0)
	for frame in range(90): _step(rig,"restart_left","Run_New1",Vector2(-114,0),-1.0)
	var frozen_bones := {}
	for name in skin.bones: frozen_bones[name] = skin.bones[name].transform
	var frozen_meshes := []
	for mesh in skin.meshes: frozen_meshes.append(mesh.polygon.duplicate())
	for frame in range(12):
		skin.set_motion_context(0.0,Vector2(-114,0),clock,-1.0)
		rig.set_character_pose("Run_New1",clock+2.0,-1.0)
	for name in frozen_bones: _check(skin.bones[name].transform==frozen_bones[name],"pause freezes "+name)
	for index in range(skin.meshes.size()): _check(skin.meshes[index].polygon==frozen_meshes[index],"pause freezes visible mesh "+str(index))
	_check(maximum_length_error<0.0002,"actual thigh and calf lengths unchanged in every sampled pose")
	_check(maximum_arm_length_error<0.0002,"held arms keep both segment lengths")
	_check(maximum_scale_error<0.00001,"limbs never stretch")
	_check(maximum_pitch_error<0.0001,"rendered independent foot pitch matches actual contact record")
	_check(maximum_contact_height_error<0.03,"actual visible sole height matches contact record")
	_check(maximum_floor_penetration<0.10,"actual sole stays above the floor within local joint precision")
	for group in ["run_right","run_left"]:
		var m: Dictionary = metrics[group]
		_check(m.both_feet_airborne>0,group+": visible soles have a real both-feet flight phase")
		_check(m.landed_samples>0,group+": actual visible soles also have landing/support frames")
		m.actual_both_feet_airborne_fraction = float(m.both_feet_airborne)/float(m.frames)
		_check(m.knee_flex_max>1.0,group+": rear leg visibly folds")
		_check(m.supported_soft_knees>m.supported_samples*0.20,group+": support is not a locked straight leg")
		_check(m.pitch_max-m.pitch_min>0.35,group+": foot rotates through recovery and landing")
		_check(m.ankle_x_max-m.ankle_x_min>(expected_leg.x+expected_leg.y)*0.4,group+": broad forward/backward ankle travel")
		_check(m.max_sole_lift>(expected_leg.x+expected_leg.y)*0.2,group+": foot recovery lifts clear of the floor")
		var near: Array = m.near_x
		var far: Array = m.far_x
		var opposite := 0
		for index in range(near.size()):
			if float(near[index])*float(far[index])<0.0: opposite += 1
		_check(opposite>near.size()*0.25,group+": near/far legs do not swing together")
		m.erase("near_x")
		m.erase("far_x")
	for group in metrics:
		metrics[group].erase("near_x")
		metrics[group].erase("far_x")
	var report := {"passed":failures.is_empty(),"checks":checks,"frames":frames,"failures":failures,"before_leg_lengths":[expected_leg.x,expected_leg.y],"before_arm_lengths":[expected_arm.x,expected_arm.y],"maximum_leg_length_error":maximum_length_error,"maximum_arm_length_error":maximum_arm_length_error,"maximum_scale_error":maximum_scale_error,"maximum_foot_pitch_error":maximum_pitch_error,"maximum_sole_record_height_error":maximum_contact_height_error,"maximum_floor_penetration":maximum_floor_penetration,"metrics":metrics,"scope":"actual fixed limb lengths and both-side ruler against frozen before; support flex, physical sole flight, pitch, direction, startup/stop/restart and pause","runtime":rig.get_runtime_summary()}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output/approved_outfit_run_review"))
	FileAccess.open("res://output/approved_outfit_run_review/run_smoke.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("APPROVED RUN REVIEW ","PASS" if failures.is_empty() else "FAIL"," ",checks," checks ",frames," frames ",JSON.stringify(failures)," LENGTH ",maximum_length_error," FLOOR ",maximum_floor_penetration)
	get_tree().quit(0 if failures.is_empty() else 1)
