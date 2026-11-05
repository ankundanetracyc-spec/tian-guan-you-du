extends "capture_reference_walk.gd"

func _correlation(a: Array, b: Array) -> float:
	var mean_a := 0.0
	var mean_b := 0.0
	for index in range(a.size()):
		mean_a += float(a[index])/a.size()
		mean_b += float(b[index])/b.size()
	var covariance := 0.0
	var spread_a := 0.0
	var spread_b := 0.0
	for index in range(a.size()):
		var x := float(a[index])-mean_a
		var y := float(b[index])-mean_b
		covariance += x*y
		spread_a += x*x
		spread_b += y*y
	return covariance/sqrt(maxf(0.000001,spread_a*spread_b))

func _ready() -> void:
	var results := {}
	var passed := true
	for speed in [114.0,235.0]:
		var rig := ReferenceRig.new()
		add_child(rig)
		assert(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"))
		rig.set_skin_mode("experiment")
		var skin = rig.wanderer_skin
		var clock := 0.0
		var body_range := Vector2(INF,-INF)
		var phase_error := 0.0
		var length_error := 0.0
		var sides := {}
		for side in ["near","far"]:
			sides[side] = {"shoulder_degrees":[INF,-INF],"forearm_degrees":[INF,-INF],"elbow_flex_degrees":[INF,-INF],"hand_x":[INF,-INF],"hand_hip_x":[INF,-INF],"foot":[],"hand":[]}
		for frame in range(180):
			clock += 1.0/60.0
			skin.set_motion_context(1.0/60.0,Vector2(speed,0),clock,1.0)
			rig.set_character_pose("Run_New1",clock,1.0)
			if frame<90: continue
			var d: Dictionary = skin.get_garment_diagnostics()
			body_range.x = minf(body_range.x,rad_to_deg(d.reference_measured_body_lean))
			body_range.y = maxf(body_range.y,rad_to_deg(d.reference_measured_body_lean))
			phase_error = maxf(phase_error,absf(fposmod(float(d.reference_arm_samples.far.leg_phase)-float(d.reference_arm_samples.near.leg_phase),1.0)-0.5))
			for side in ["near","far"]:
				var shoulder: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_upper"].global_position)
				var elbow: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_forearm"].global_position)
				var hand: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_hand"].global_position)
				var hip: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_thigh"].global_position)
				var foot: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_foot"].global_position)
				var upper := (elbow-shoulder).angle()
				var lower := (hand-elbow).angle()
				var values := {"shoulder_degrees":rad_to_deg(upper),"forearm_degrees":rad_to_deg(lower),"elbow_flex_degrees":rad_to_deg(wrapf(upper-lower,-PI,PI)),"hand_x":hand.x-shoulder.x,"hand_hip_x":hand.x-hip.x}
				for key in values:
					sides[side][key][0] = minf(float(sides[side][key][0]),float(values[key]))
					sides[side][key][1] = maxf(float(sides[side][key][1]),float(values[key]))
				sides[side].foot.append(foot.x-hip.x)
				sides[side].hand.append(hand.x-shoulder.x)
				length_error = maxf(length_error,maxf(absf(elbow.distance_to(shoulder)-skin.bones[side+"_forearm"].rest.origin.length()),absf(hand.distance_to(elbow)-skin.bones[side+"_hand"].rest.origin.length())))
		for side in ["near","far"]:
			sides[side].same_side_hand_foot_correlation = _correlation(sides[side].foot,sides[side].hand)
			passed = passed and float(sides[side].same_side_hand_foot_correlation)< -0.9 and float(sides[side].hand_x[0])< -2.0 and float(sides[side].hand_x[1])>10.0
			sides[side].erase("foot")
			sides[side].erase("hand")
		passed = passed and phase_error<0.000001 and length_error<0.002 and body_range.x>14.0
		results[str(speed)] = {"measured_body_lean_degrees":[body_range.x,body_range.y],"arm_phase_offset_error":phase_error,"maximum_arm_length_error":length_error,"arms":sides}
		rig.queue_free()
	var file := FileAccess.open("res://output/reference_arm_phase_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"passed":passed,"results":results},"\t"))
	file.close()
	print("REFERENCE ARM PHASE ","PASS" if passed else "FAIL",": ",JSON.stringify(results))
	get_tree().quit(0 if passed else 1)
