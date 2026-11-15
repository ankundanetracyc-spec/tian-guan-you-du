extends Node2D

const OutfitRuntime = preload("approved_outfit_fitting_runtime.gd")
var checks := 0
var failures: Array[String] = []
var samples := 0
var minimum_elbow_flex := INF
var maximum_elbow_flex := -INF
var maximum_bone_error := 0.0
var maximum_angle_step := 0.0
var rates: Array = []

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value: failures.append(message)

func _ready() -> void:
	var rig := OutfitRuntime.new()
	add_child(rig)
	_check(rig.build(),"approved outfit builds for actual bone/weapon motion sampling")
	if not rig.loaded:
		_finish()
		return
	var skin: Variant = rig.wanderer_skin
	var sword: Variant = skin.approved_sword
	skin.motion_context = false
	skin.pose_blending = false
	skin.motion_delta = 0.0
	_check(is_equal_approx(sword.swing_duration,0.72),"single motion duration remains 0.72 seconds")
	_check(sword.asset_dir==get_script().resource_path.get_base_dir().path_join("art/approved_outfit_sword_20261105"),"default sword asset directory follows the script resource root")
	for facing in [-1.0,1.0]:
		for zoom in [2.0,5.0]:
			rig.display_zoom = zoom
			sword.restart_swing(true)
			var previous_blade_angle := 0.0
			var peak_prepare := 0.0
			var peak_cut := 0.0
			var peak_recover := 0.0
			var first_grip := Vector2.ZERO
			var first_tip := Vector2.ZERO
			for frame in range(721):
				var phase := float(frame)/720.0
				sword.swing_elapsed = phase*sword.swing_duration
				rig.set_character_pose("AttackIdle",0.0,facing)
				var inverse: Transform2D = rig.rig_root_node.global_transform.affine_inverse()
				var shoulder: Vector2 = inverse*skin.bones.near_upper.global_position
				var elbow: Vector2 = inverse*skin.bones.near_forearm.global_position
				var wrist: Vector2 = inverse*skin.bones.near_hand.global_position
				var flex := -rad_to_deg((elbow-shoulder).angle_to(wrist-elbow))
				minimum_elbow_flex = minf(minimum_elbow_flex,flex)
				maximum_elbow_flex = maxf(maximum_elbow_flex,flex)
				maximum_bone_error = maxf(maximum_bone_error,absf(shoulder.distance_to(elbow)-skin.bones.near_forearm.rest.origin.length()))
				maximum_bone_error = maxf(maximum_bone_error,absf(elbow.distance_to(wrist)-skin.bones.near_hand.rest.origin.length()))
				var grip: Vector2 = inverse*sword.sword_sprite.to_global(sword.source_grip)
				var tip: Vector2 = inverse*sword.sword_sprite.to_global(sword.source_tip)
				var blade_angle := (tip-grip).angle()
				if frame==0:
					first_grip = grip
					first_tip = tip
				else:
					var step := absf(rad_to_deg(angle_difference(previous_blade_angle,blade_angle)))
					maximum_angle_step = maxf(maximum_angle_step,step)
					var speed: float = step*720.0/sword.swing_duration
					if phase<=0.30: peak_prepare = maxf(peak_prepare,speed)
					elif phase<=0.55: peak_cut = maxf(peak_cut,speed)
					elif phase>=0.60: peak_recover = maxf(peak_recover,speed)
				previous_blade_angle = blade_angle
				samples += 1
				if frame==720:
					_check(grip.distance_to(first_grip)<0.0001 and tip.distance_to(first_tip)<0.0001,"loop seam returns to the exact held blade transform")
			rates.append({"facing":facing,"zoom":zoom,"prepare_degrees_per_second":peak_prepare,"cut_degrees_per_second":peak_cut,"recovery_degrees_per_second":peak_recover})
			_check(peak_cut>peak_prepare*1.3,"actual blade cut is distinctly faster than its preparation")
			_check(peak_cut>peak_recover*3.0,"actual blade recovery has a slower tempo than its cut")
			_check(skin.bones.near_upper.scale.is_equal_approx(Vector2.ONE) and skin.bones.near_forearm.scale.is_equal_approx(Vector2.ONE) and skin.bones.near_hand.scale.is_equal_approx(Vector2.ONE),"motion preserves unit scales on the actual shoulder/elbow/wrist bones")
	_check(minimum_elbow_flex>=21.99 and maximum_elbow_flex<=47.01,"actual elbow stays on the safe bent side throughout mirrored/scaled cycles")
	_check(maximum_bone_error<0.0001,"actual arm segments keep their registered lengths")
	_check(maximum_angle_step<2.0,"dense blade samples have no angular jumps at stage boundaries or loop seam")
	_finish()

func _finish() -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"samples":samples,"actual_elbow_flex_degrees":[minimum_elbow_flex,maximum_elbow_flex],"maximum_arm_length_error":maximum_bone_error,"maximum_blade_angle_step_degrees":maximum_angle_step,"peak_blade_angular_speeds":rates,"scope":"Actual fixed-bone joint geometry and independent sword transform at 1000Hz, both facings and display scales. Checks safe elbow bend, lift/cut/recovery tempo and closed loop seam. No combat rules or artwork changes."}
	var directory: String = get_script().resource_path.get_base_dir().path_join("output")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(directory))
	FileAccess.open(directory.path_join("approved_outfit_sword_motion_smoke.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("APPROVED SWORD MOTION ","PASS" if failures.is_empty() else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if failures.is_empty() else 1)
