extends Node2D

## Behavioral checks for the newly authorized asymmetric, Guigu-like run.
## Source data inform the visual targets; original bone lengths and soles stay ours.
class TestRig extends "guigu_rig_runtime.gd":
	func load_skin(script_path: String) -> bool:
		custom_art_folder = "yuxu_fullbody_v15"
		if not _build_character_hierarchy(_read_json("res://guigu_rig/rig.json")): return false
		if not _load_texture_sets("res://art"): return false
		if not _build_weighted_meshes(_read_json("res://guigu_rig/skins.json"),"res://art"): return false
		wanderer_skin = load(script_path).new()
		bone_bound_art_ready = wanderer_skin.build(self,"res://art/yuxu_fullbody_v15")
		_load_animation_tracks(_read_json("res://guigu_rig/animations.json"))
		loaded = bone_bound_art_ready
		return loaded

var checks := 0
var failures: Array[String] = []
const BASELINE := "res://reference_walk_pre_guigu_baseline_20261104.gd"

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)
		push_error(message)

func _make_rig(script_path: String = "res://reference_walk_skin.gd") -> TestRig:
	var rig := TestRig.new()
	add_child(rig)
	assert(rig.load_skin(script_path))
	rig.set_skin_mode("experiment")
	return rig

func _point(rig, key: String) -> Vector2:
	return rig.rig_root_node.to_local(rig.wanderer_skin.bones[key].global_position)

func _noncloth_error(a, b) -> Vector2:
	var result := Vector2.ZERO
	for key in a.bones:
		if key.contains("cloth") or key.ends_with("hem") or key.ends_with("edge"): continue
		result.x = maxf(result.x,a.bones[key].position.distance_to(b.bones[key].position))
		result.y = maxf(result.y,absf(wrapf(a.bones[key].rotation-b.bones[key].rotation,-PI,PI)))
	return result

func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output"))
	var report := {"source_sha256":FileAccess.get_sha256("res://reference_walk_skin.gd"),"baseline_sha256":FileAccess.get_sha256(BASELINE)}
	var gait_reports := {}
	var maximum_leg_length_error := 0.0
	var maximum_arm_length_error := 0.0
	var maximum_sole_error := 0.0
	var maximum_floor_penetration := 0.0
	var maximum_cloth_y := -INF
	var contact_frames := 0
	for speed in [114.0,235.0]:
		var rig := _make_rig()
		var skin = rig.wanderer_skin
		var clock := 0.0
		var body_lean := Vector2(INF,-INF)
		var hand_ranges := {"near":Vector2(INF,-INF),"far":Vector2(INF,-INF)}
		var elbow_ranges := {"near":Vector2(INF,-INF),"far":Vector2(INF,-INF)}
		var knee_peak := 0.0
		var rear_trail := 0.0
		var front_trail := 0.0
		for frame in range(240):
			clock += 1.0/60.0
			rig.position += Vector2(speed,0.0)/60.0
			skin.set_motion_context(1.0/60.0,Vector2(speed,0.0),clock,1.0)
			rig.set_character_pose("Run_New1",clock,1.0)
			maximum_cloth_y = maxf(maximum_cloth_y,skin.garment_floor_max)
			for side in ["near","far"]:
				var hip := _point(rig,side+"_thigh")
				var knee := _point(rig,side+"_calf")
				var ankle := _point(rig,side+"_foot")
				var shoulder := _point(rig,side+"_upper")
				var elbow := _point(rig,side+"_forearm")
				var hand := _point(rig,side+"_hand")
				var upper_leg: float = skin.bones[side+"_calf"].rest.origin.length()
				var lower_leg: float = skin.bones[side+"_foot"].rest.origin.length()
				var upper_arm: float = skin.bones[side+"_forearm"].rest.origin.length()
				var lower_arm: float = skin.bones[side+"_hand"].rest.origin.length()
				maximum_leg_length_error = maxf(maximum_leg_length_error,maxf(absf(knee.distance_to(hip)-upper_leg),absf(ankle.distance_to(knee)-lower_leg)))
				maximum_arm_length_error = maxf(maximum_arm_length_error,maxf(absf(elbow.distance_to(shoulder)-upper_arm),absf(hand.distance_to(elbow)-lower_arm)))
				var pitch: float = skin.bones[side+"_thigh"].rotation+skin.bones[side+"_calf"].rotation+skin.bones[side+"_foot"].rotation
				var sole: Vector2 = ankle+skin._sole_contact(side,pitch)
				maximum_sole_error = maxf(maximum_sole_error,absf(sole.y+float(skin.ground_contacts[side].lift)))
				maximum_floor_penetration = maxf(maximum_floor_penetration,sole.y)
				if frame<120: continue
				var normalized_hand := (hand.x-shoulder.x)/(upper_arm+lower_arm)
				var hand_range: Vector2 = hand_ranges[side]
				hand_ranges[side] = Vector2(minf(hand_range.x,normalized_hand),maxf(hand_range.y,normalized_hand))
				var flex := absf(rad_to_deg(wrapf((elbow-shoulder).angle()-(hand-elbow).angle(),-PI,PI)))
				var elbow_range: Vector2 = elbow_ranges[side]
				elbow_ranges[side] = Vector2(minf(elbow_range.x,flex),maxf(elbow_range.y,flex))
				knee_peak = maxf(knee_peak,absf(rad_to_deg(wrapf((knee-hip).angle()-(ankle-knee).angle(),-PI,PI))))
			contact_frames += 1
			if frame>=120:
				var diagnostics: Dictionary = skin.get_garment_diagnostics()
				var lean := rad_to_deg(float(diagnostics.reference_measured_body_lean))
				body_lean = Vector2(minf(body_lean.x,lean),maxf(body_lean.y,lean))
				rear_trail += skin.cape_rear_tip.x-skin.cape_rear_anchor.x
				front_trail += skin.reference_front_tip.x-skin.reference_front_anchor.x
		var near_range: Vector2 = hand_ranges.near
		var far_range: Vector2 = hand_ranges.far
		var near_span := near_range.y-near_range.x
		var far_span := far_range.y-far_range.x
		_check(near_range.x>0.25 and near_span<0.45,"near hand stays in a forward held pose at "+str(speed))
		_check(far_range.x< -0.20 and far_span>0.65 and far_span>near_span*1.8,"far arm has the larger rear/front swing at "+str(speed))
		_check(body_lean.x>14.0 and body_lean.y<30.0,"running body axis stays visibly forward at "+str(speed))
		_check(knee_peak>80.0,"running recovery bends the knee at "+str(speed))
		_check(rear_trail/120.0< -10.0 and front_trail/120.0< -3.0,"both visible hem edges follow behind travel at "+str(speed))
		gait_reports[str(speed)] = {"hand_x_over_own_arm_length":{"near":[near_range.x,near_range.y],"far":[far_range.x,far_range.y]},"near_hand_span":near_span,"far_hand_span":far_span,"elbow_flex_degrees":{"near":[elbow_ranges.near.x,elbow_ranges.near.y],"far":[elbow_ranges.far.x,elbow_ranges.far.y]},"body_axis_lean_degrees":[body_lean.x,body_lean.y],"maximum_knee_flex_degrees":knee_peak,"mean_rear_trail":rear_trail/120.0,"mean_front_trail":front_trail/120.0}
		rig.free()
	# Cover both facings, backing away and diagonal travel using the actual soles.
	var contact_rig := _make_rig()
	var contact_skin = contact_rig.wanderer_skin
	var contact_clock := 0.0
	for facing in [1.0,-1.0]:
		for velocity in [Vector2(114,0),Vector2(-114,0),Vector2(0,114),Vector2(1,1).normalized()*114,Vector2.ZERO]:
			for frame in range(60):
				contact_clock += 1.0/60.0
				contact_rig.position += velocity/60.0
				contact_skin.set_motion_context(1.0/60.0,velocity,contact_clock,facing)
				var clip := "Idle" if velocity.is_zero_approx() else ("BackstepWalk" if velocity.x*facing<0.0 else "Run_New1")
				contact_rig.set_character_pose(clip,contact_clock,facing)
				for side in ["near","far"]:
					var hip := _point(contact_rig,side+"_thigh")
					var knee := _point(contact_rig,side+"_calf")
					var ankle := _point(contact_rig,side+"_foot")
					maximum_leg_length_error = maxf(maximum_leg_length_error,maxf(absf(knee.distance_to(hip)-contact_skin.bones[side+"_calf"].rest.origin.length()),absf(ankle.distance_to(knee)-contact_skin.bones[side+"_foot"].rest.origin.length())))
					var pitch: float = contact_skin.bones[side+"_thigh"].rotation+contact_skin.bones[side+"_calf"].rotation+contact_skin.bones[side+"_foot"].rotation
					var sole: Vector2 = ankle+contact_skin._sole_contact(side,pitch)
					maximum_sole_error = maxf(maximum_sole_error,absf(sole.y+float(contact_skin.ground_contacts[side].lift)))
					maximum_floor_penetration = maxf(maximum_floor_penetration,sole.y)
				maximum_cloth_y = maxf(maximum_cloth_y,contact_skin.garment_floor_max)
				contact_frames += 1
	_check(maximum_leg_length_error<0.002 and maximum_arm_length_error<0.002,"original arm and leg segment lengths stay fixed")
	_check(maximum_sole_error<0.06 and maximum_floor_penetration<0.06,"actual soles match reported contacts without floor penetration")
	_check(maximum_cloth_y<0.001,"opaque hem samples stay above the floor")
	contact_rig.free()
	# The changed support fraction needs its own mirror map. A planted late
	# stance must remain planted instead of being mapped into the swing arc.
	var turn_rig := _make_rig()
	var turn_skin = turn_rig.wanderer_skin
	turn_skin.set_motion_context(1.0/60.0,Vector2(114,0),1.0/60.0,1.0)
	turn_rig.set_character_pose("Run_New1",1.0/60.0,1.0)
	turn_skin.gait_phase = 0.45
	turn_skin.set_motion_context(1.0/60.0,Vector2(-114,0),2.0/60.0,-1.0)
	var expected_mirrored_phase := fposmod(float(turn_skin.WALK_SUPPORT)-0.45,1.0)
	var mapped_phase_error: float = absf(turn_skin.gait_phase-expected_mirrored_phase)
	var mapped_sample: Vector3 = turn_skin._foot_sample(turn_skin.gait_phase,0.0)
	turn_rig.set_character_pose("Run_New1",2.0/60.0,-1.0)
	var mapped_contact_lift: float = turn_skin.ground_contacts.near.lift
	_check(mapped_phase_error<0.000001 and absf(mapped_sample.y)<0.000001 and absf(mapped_contact_lift)<0.000001,"turn maps planted phase 0.45 within the current support interval")
	report["planted_turn"] = {"phase_before":0.45,"expected_mapped_phase":expected_mirrored_phase,"phase_mapping_error":mapped_phase_error,"mapped_sample_lift":mapped_sample.y,"actual_near_contact_lift":mapped_contact_lift}
	turn_rig.free()
	# Fresh histories make this an exact idle/dedicated-pose comparison.
	var pose_reports := {}
	for kind in ["Idle","AttackIdle","guard","preparing","Step1","combo1","combo2","combo3","combo4"]:
		var pair := [_make_rig(BASELINE),_make_rig()]
		var max_error := Vector2.ZERO
		for frame in range(60):
			var clock := float(frame+1)/60.0
			var clip: String = kind if kind in ["Idle","AttackIdle","Step1"] else "Idle"
			for current in pair:
				var skin = current.wanderer_skin
				skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,1.0)
				skin.set_action_context(kind=="guard",kind=="preparing")
				skin.set_combo_context(int(kind.trim_prefix("combo")) if kind.begins_with("combo") else 0,float(frame)/60.0)
				current.set_character_pose(clip,clock,1.0)
			var error := _noncloth_error(pair[0].wanderer_skin,pair[1].wanderer_skin)
			max_error.x = maxf(max_error.x,error.x)
			max_error.y = maxf(max_error.y,error.y)
		var pose_pass := max_error.x<0.00002 and max_error.y<0.00002
		if kind!="Idle": pose_pass = pose_pass and not pair[1].wanderer_skin.reference_pose_active
		_check(pose_pass,kind+" keeps its original joints and dedicated pose priority")
		pose_reports[kind] = {"passed":pose_pass,"maximum_position_error":max_error.x,"maximum_angle_error":max_error.y}
		for current in pair: current.free()
	# Momentum, pause, facing conversion, settling and reset behavior.
	var rig := _make_rig()
	var skin = rig.wanderer_skin
	var clock := 0.0
	for frame in range(180):
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
	var pause_pass: bool = phase_before==skin.gait_phase and steps_before==skin.garment_simulation_steps
	for key in frozen: pause_pass = pause_pass and frozen[key]==skin.bones[key].transform
	_check(pause_pass,"pause freezes body joints, phase and cloth integration")
	var before_front: Vector3 = skin.reference_front_angles
	var before_rear: Vector3 = skin.garment_angles
	var before_load: float = skin.reference_trailing_wind_load
	skin.set_motion_context(1.0/60.0,Vector2(114,0),clock+1.0/60.0,-1.0)
	var world_turn_error: float = maxf((before_front+skin.reference_front_angles).length(),(before_rear+skin.garment_angles).length())
	var world_load_error: float = absf(before_load+skin.reference_trailing_wind_load)
	_check(world_turn_error<0.000001 and world_load_error<0.000001,"facing conversion preserves existing world-space cloth state")
	clock += 1.0/60.0
	rig.set_character_pose("BackstepWalk",clock,-1.0)
	var stop_samples := {}
	var retained := 0.0
	for frame in range(240):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,-1.0)
		rig.set_character_pose("Idle",clock,-1.0)
		if frame==0: retained = absf(skin.cape_rear_tip.x-skin.cape_rear_anchor.x)
		if frame in [0,5,11,17,35,59,119,239]: stop_samples[str(frame+1)] = {"seconds":float(frame+1)/60.0,"rear_angle":skin.garment_angles.length(),"front_angle":skin.reference_front_angles.length(),"rear_tip_x":skin.cape_rear_tip.x-skin.cape_rear_anchor.x}
	var settled: bool = skin.garment_angles.length()<0.002 and skin.garment_angular_velocity.length()<0.01 and skin.reference_front_angles.length()<0.002 and skin.reference_front_angular_velocity.length()<0.01
	_check(retained>5.0,"release retains visible rear momentum")
	_check(settled and skin.gait_amount<0.001,"cloth and movement settle back to idle")
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,0.0,-1.0)
	rig.set_character_pose("Idle",0.0,-1.0)
	var reset_pass: bool = skin.garment_angles.length()<0.001 and skin.reference_front_angles.length()<0.001
	_check(reset_pass,"clock restart clears cloth history")
	rig.position += Vector2(1200,0)
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,1.0/60.0,-1.0)
	rig.set_character_pose("Idle",1.0/60.0,-1.0)
	var teleport_pass: bool = skin.garment_angles.length()<0.001 and skin.reference_front_angles.length()<0.001
	_check(teleport_pass,"teleport clears cloth history")
	rig.free()
	report.merge({"passed":failures.is_empty(),"checks":checks,"failures":failures,"gaits":gait_reports,"sampled_contact_frames":contact_frames,"maximum_leg_length_error":maximum_leg_length_error,"maximum_arm_length_error":maximum_arm_length_error,"maximum_actual_sole_error":maximum_sole_error,"maximum_floor_penetration":maximum_floor_penetration,"maximum_opaque_hem_y":maximum_cloth_y,"preserved_poses":pose_reports,"pause_frozen":pause_pass,"world_turn_error":world_turn_error,"world_load_error":world_load_error,"first_release_rear_trail":retained,"stop_samples":stop_samples,"settled":settled,"clock_reset":reset_pass,"teleport_reset":teleport_pass})
	var file := FileAccess.open("res://output/guigu_guard_run_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("GUIGU GUARD RUN ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks; ",JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
