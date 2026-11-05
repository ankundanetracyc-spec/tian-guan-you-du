extends "capture_cape_like_robe.gd"

var failures: Array[String] = []
var checks := 0

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)
		push_error(message)

func _arm_pose(skin, side: String, phase: float, style: StringName, attack_phase := -1.0, dash := 0.0) -> Dictionary:
	for key in [side+"_upper",side+"_forearm",side+"_hand"]: skin.bones[key].transform = skin.bones[key].rest
	skin.set_locomotion_style(style)
	skin._pose_guard_arm(side,phase,1.0,0.0,0.0,0.0,attack_phase,dash)
	var upper: Bone2D = skin.bones[side+"_upper"]
	var lower: Bone2D = skin.bones[side+"_forearm"]
	var hand: Bone2D = skin.bones[side+"_hand"]
	return {"angles":Vector3(upper.rotation+lower.rest.origin.angle(),lower.rotation+hand.rest.origin.angle()+upper.rotation,hand.rotation),"shoulder":upper.transform,"elbow":lower.transform,"hand":hand.transform,"correction":skin.arm_speed_correction}

func _ready() -> void:
	var rig := CapeRig.new()
	add_child(rig)
	_check(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"),"candidate loads")
	var skin = rig.wanderer_skin
	skin.last_clip = "Run_New1"
	skin.gesture_waist = 0.0
	skin.gesture_focus = 0.0
	skin.gesture_ready = 0.0
	var ranges := {}
	var worst_wrist_correction := 0.0
	for entry in [[114.0,18.48497034],[235.0,22.0],[352.5,25.06529489]]:
		skin.gait_stride_half = float(entry[1])
		var speed_ranges := {}
		for side in ["near","far"]:
			var minimum := Vector3(INF,INF,INF)
			var maximum := Vector3(-INF,-INF,-INF)
			for phase_index in range(40):
				var pose := _arm_pose(skin,side,float(phase_index)/40.0,&"ground")
				var angles: Vector3 = pose.angles
				minimum = Vector3(minf(minimum.x,angles.x),minf(minimum.y,angles.y),minf(minimum.z,angles.z))
				maximum = Vector3(maxf(maximum.x,angles.x),maxf(maximum.y,angles.y),maxf(maximum.z,angles.z))
				worst_wrist_correction = maxf(worst_wrist_correction,absf(pose.correction.z))
			speed_ranges[side] = _serial(maximum-minimum)
		ranges[str(entry[0])] = speed_ranges
	for side in ["near","far"]:
		_check(float(ranges["114.0"][side][0])<float(ranges["235.0"][side][0]) and float(ranges["235.0"][side][0])<float(ranges["352.5"][side][0]),side+" shoulder swing follows leg stride")
		_check(float(ranges["114.0"][side][1])<float(ranges["235.0"][side][1]) and float(ranges["235.0"][side][1])<float(ranges["352.5"][side][1]),side+" elbow swing follows leg stride")
	_check(worst_wrist_correction<0.009,"wrist correction remains below half a degree")
	var priority_results := {}
	for kind in ["attack","guard","preparing","combo","dash","ready_transition"]:
		skin.last_clip = "Step1" if kind=="dash" else ("AttackIdle" if kind=="attack" else "Run_New1")
		skin.action_guard = kind=="guard"
		skin.action_preparing = kind=="preparing"
		skin.combo_stage = 2 if kind=="combo" else 0
		skin.gesture_ready = 0.6 if kind=="ready_transition" else 0.0
		var unchanged := true
		for side in ["near","far"]:
			var original := _arm_pose(skin,side,0.27,&"future_non_ground",0.4 if kind=="attack" else -1.0,0.7 if kind=="dash" else 0.0)
			var coordinated := _arm_pose(skin,side,0.27,&"ground",0.4 if kind=="attack" else -1.0,0.7 if kind=="dash" else 0.0)
			unchanged = unchanged and original.shoulder==coordinated.shoulder and original.elbow==coordinated.elbow and original.hand==coordinated.hand and coordinated.correction==Vector3.ZERO
		_check(unchanged,kind+" dedicated arm pose retains priority")
		priority_results[kind] = unchanged
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"angular_ranges_radians":ranges,"maximum_wrist_correction":worst_wrist_correction,"priority_actions_unchanged":priority_results}
	var file := FileAccess.open("res://output/cape_arm_speed_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("CAPE ARM SPEED ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks")
	get_tree().quit(0 if failures.is_empty() else 1)
