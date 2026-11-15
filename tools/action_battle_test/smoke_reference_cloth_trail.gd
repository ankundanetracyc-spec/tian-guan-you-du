extends "capture_reference_walk.gd"

class ClothRig extends "guigu_rig_runtime.gd":
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

func _ready() -> void:
	var rig := ClothRig.new()
	var baseline := ClothRig.new()
	add_child(rig)
	add_child(baseline)
	assert(rig.load_skin("res://reference_walk_skin.gd"))
	assert(baseline.load_skin("res://reference_walk_run_v3_cloth_baseline_20261104.gd"))
	rig.set_skin_mode("experiment")
	baseline.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	var old = baseline.wanderer_skin
	var clock := 0.0
	var maximum_floor := -INF
	var noncloth_position_error := 0.0
	var noncloth_angle_error := 0.0
	var maximum_turn_error := 0.0
	var maximum_turn_load_error := 0.0
	var forward_stop_samples := {}
	var backward_stop_samples := {}
	var rear_forward := 0.0
	var front_forward := 0.0
	var rear_backward := 0.0
	var front_backward := 0.0
	var retained_after_stop := 0.0
	var forward_samples := 0
	var backward_samples := 0
	var pause_pass := true
	var stopped_pass := false
	var frozen_front := Vector3.ZERO
	var frozen_rear := Vector3.ZERO
	var frozen_steps := 0
	for frame in range(582):
		var delta := 0.0 if frame>=450 and frame<462 else 1.0/60.0
		clock += delta
		var facing := 1.0 if frame<210 else -1.0
		var velocity := Vector2.ZERO
		var clip := "Idle"
		var pose_time := clock
		if frame>=30 and frame<150:
			velocity = Vector2(114,0)
			clip = "Run_New1"
		if frame>=210 and frame<270:
			velocity = Vector2(-114,0)
			clip = "Run_New1"
		if frame>=270 and frame<330:
			velocity = Vector2(114,0)
			clip = "BackstepWalk"
		if frame>=120 and frame<126:
			clip = "AttackIdle"
			pose_time = float(frame-120)/60.0
		if frame>=240 and frame<246:
			velocity = Vector2(-500,0)
			clip = "Step1"
			pose_time = float(frame-240)/60.0
		var previous_front: Vector3 = skin.reference_front_angles
		var previous_rear: Vector3 = skin.garment_angles
		var previous_facing: float = skin.motion_facing
		var previous_load: float = skin.reference_trailing_wind_load
		for current in [baseline,rig]:
			current.position += velocity*delta
			current.wanderer_skin.set_motion_context(delta,velocity,clock,facing)
			current.wanderer_skin.set_action_context(frame>=130 and frame<138,frame>=140 and frame<146)
			if current==rig and previous_facing!=facing and delta>0.0:
				maximum_turn_error = maxf(maximum_turn_error,maxf((previous_front*previous_facing-skin.reference_front_angles*facing).length(),(previous_rear*previous_facing-skin.garment_angles*facing).length()))
				maximum_turn_load_error = maxf(maximum_turn_load_error,absf(previous_load*previous_facing-skin.reference_trailing_wind_load*facing))
			current.set_character_pose(clip,pose_time,facing)
		for key in skin.bones:
			if key.contains("cloth") or key.ends_with("hem") or key.ends_with("edge"): continue
			noncloth_position_error = maxf(noncloth_position_error,skin.bones[key].position.distance_to(old.bones[key].position))
			noncloth_angle_error = maxf(noncloth_angle_error,absf(wrapf(skin.bones[key].rotation-old.bones[key].rotation,-PI,PI)))
		maximum_floor = maxf(maximum_floor,skin.garment_floor_max)
		if frame>=90 and frame<120:
			rear_forward += skin.cape_rear_tip.x-skin.cape_rear_anchor.x
			front_forward += skin.reference_front_tip.x-skin.reference_front_anchor.x
			forward_samples += 1
		if frame==150: retained_after_stop = absf(skin.cape_rear_tip.x-skin.cape_rear_anchor.x)
		if frame in [149,155,161,167,185,209]:
			forward_stop_samples[str(maxf(0.0,float(frame-149)/60.0))] = {"rear_tip_x":skin.cape_rear_tip.x-skin.cape_rear_anchor.x,"front_tip_x":skin.reference_front_tip.x-skin.reference_front_anchor.x,"wind_load":skin.reference_trailing_wind_load}
		if frame in [335,341,347,365,389,449]:
			backward_stop_samples[str(float(frame-329)/60.0)] = {"rear_tip_x":skin.cape_rear_tip.x-skin.cape_rear_anchor.x,"front_tip_x":skin.reference_front_tip.x-skin.reference_front_anchor.x,"wind_load":skin.reference_trailing_wind_load}
		if frame>=310 and frame<330:
			rear_backward += skin.cape_rear_tip.x-skin.cape_rear_anchor.x
			front_backward += skin.reference_front_tip.x-skin.reference_front_anchor.x
			backward_samples += 1
		if frame==449:
			frozen_front = skin.reference_front_angles
			frozen_rear = skin.garment_angles
			frozen_steps = skin.garment_simulation_steps
		if frame>=450 and frame<462:
			pause_pass = pause_pass and frozen_front==skin.reference_front_angles and frozen_rear==skin.garment_angles and frozen_steps==skin.garment_simulation_steps
	rear_forward /= forward_samples
	front_forward /= forward_samples
	rear_backward /= backward_samples
	front_backward /= backward_samples
	stopped_pass = skin.garment_angles.length()<0.002 and skin.garment_angular_velocity.length()<0.01 and skin.reference_front_angles.length()<0.002 and skin.reference_front_angular_velocity.length()<0.01
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,0.0,-1.0)
	rig.set_character_pose("Idle",0.0,-1.0)
	var reset_pass: bool = skin.garment_angles.length()<0.001 and skin.reference_front_angles.length()<0.001
	rig.position += Vector2(1200,0)
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,1.0/60.0,-1.0)
	rig.set_character_pose("Idle",1.0/60.0,-1.0)
	var teleport_pass: bool = skin.garment_angles.length()<0.001 and skin.reference_front_angles.length()<0.001
	var passed: bool = noncloth_position_error<0.00001 and noncloth_angle_error<0.00001 and maximum_floor<0.001 and rear_forward< -10.0 and rear_forward> -24.0 and front_forward< -1.0 and rear_backward>10.0 and front_backward>8.0 and retained_after_stop>10.0 and maximum_turn_error<0.000001 and maximum_turn_load_error<0.000001 and pause_pass and stopped_pass and reset_pass and teleport_pass
	var report := {"passed":passed,"noncloth_position_error":noncloth_position_error,"noncloth_angle_error":noncloth_angle_error,"maximum_sampled_opaque_cloth_y":maximum_floor,"steady_forward_rear_tip_x":rear_forward,"steady_forward_front_tip_x":front_forward,"steady_backward_rear_tip_x":rear_backward,"steady_backward_front_tip_x":front_backward,"rear_trail_first_stopped_frame":retained_after_stop,"world_turn_angle_error":maximum_turn_error,"world_turn_load_error":maximum_turn_load_error,"forward_stop_samples":forward_stop_samples,"backward_stop_samples":backward_stop_samples,"pause_frozen":pause_pass,"stopped_settled":stopped_pass,"clock_reset":reset_pass,"teleport_reset":teleport_pass}
	var file := FileAccess.open("res://output/reference_cloth_trail_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("REFERENCE CLOTH TRAIL ","PASS" if passed else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if passed else 1)
