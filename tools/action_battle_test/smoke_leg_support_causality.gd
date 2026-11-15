extends "capture_leg_supported_robe.gd"

## Surface causality tests intentionally bypass gait animation: unchanged bones
## must give unchanged support, and edited bones must move the local cloth.
var failures: Array = []

func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error(message)

func _delta_stats(a: PackedVector2Array, b: PackedVector2Array, uv: PackedVector2Array) -> Dictionary:
	var peak := 0.0
	var upper_peak := 0.0
	var changed := 0
	var squared := 0.0
	for index in range(a.size()):
		var distance := a[index].distance_to(b[index])
		peak = maxf(peak,distance)
		squared += distance*distance
		if uv[index].y < 735.0: upper_peak = maxf(upper_peak,distance)
		if distance > 0.01: changed += 1
	return {"peak":peak,"upper_peak":upper_peak,"changed_vertices":changed,"rms":sqrt(squared/a.size())}

func _ready() -> void:
	var output_name := "leg_support_causality"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output_name = argument.trim_prefix("--output=")
	var output := ProjectSettings.globalize_path("res://output/"+output_name)
	DirAccess.make_dir_recursive_absolute(output)
	var rig := ReviewRig.new()
	add_child(rig)
	_check(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"),"candidate rig loads")
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	skin.set_motion_context(1.0/30.0,Vector2.ZERO,0.1,1.0)
	rig.set_character_pose("Idle",0.1,1.0)
	skin.solve_leg_supported_surface(1.0/30.0,true)
	await get_tree().process_frame
	var baseline: PackedVector2Array = skin.garment_body.polygon.duplicate()
	var uv: PackedVector2Array = skin.garment_body.uv
	var fixed_diagnostics: Dictionary = skin.get_garment_diagnostics().duplicate(true)
	var bone_transforms := {}
	for key in skin.bones: bone_transforms[key] = skin.bones[key].transform
	var fixed_peak := 0.0
	for step in range(90):
		# The clock and world position change; every local leg transform stays.
		rig.position = Vector2(step*3.8,step*0.5)
		skin.set_motion_context(1.0/30.0,Vector2(114,15),0.2+step/30.0,1.0)
		skin.solve_leg_supported_surface(1.0/30.0,true)
		var difference := _delta_stats(baseline,skin.garment_body.polygon,uv)
		fixed_peak = maxf(fixed_peak,float(difference.peak))
	_check(fixed_peak < 0.002,"fixed local legs generate no alternating surface under translation/clock")
	# Keep the root still and alter the actual articulated leg bones only.
	rig.position = Vector2.ZERO
	for key in bone_transforms: skin.bones[key].transform = bone_transforms[key]
	skin.set_motion_context(1.0/30.0,Vector2.ZERO,3.3,1.0)
	skin.bones.near_calf.rotation += 0.42
	skin.bones.far_calf.rotation -= 0.20
	skin.solve_leg_supported_surface(1.0/30.0,true)
	var posed: PackedVector2Array = skin.garment_body.polygon.duplicate()
	var changed_diagnostics: Dictionary = skin.get_garment_diagnostics().duplicate(true)
	var static_pose_delta := _delta_stats(baseline,posed,uv)
	_check(float(static_pose_delta.peak)>0.20,"static actor's leg-only pose change pushes local surface")
	_check(float(static_pose_delta.upper_peak)<0.002,"leg-only support keeps fixed upper waist/torso unchanged")
	# A repeated zero delta render request must preserve the posed surface.
	var paused: PackedVector2Array = skin.garment_body.polygon.duplicate()
	for step in range(12):
		skin.set_motion_context(0.0,Vector2.ZERO,3.3,1.0)
		rig.set_character_pose("Run_New1",9.0+step,1.0)
	var pause_delta := _delta_stats(paused,skin.garment_body.polygon,uv)
	_check(float(pause_delta.peak)<0.00001,"paused skin freezes surface despite requested pose changes")
	# Restore the original bones and allow the released fabric to settle.
	for key in bone_transforms: skin.bones[key].transform = bone_transforms[key]
	var release_samples := []
	for step in range(90):
		skin.set_motion_context(1.0/30.0,Vector2.ZERO,3.4+step/30.0,1.0)
		skin.solve_leg_supported_surface(1.0/30.0,false)
		if step in [0,2,5,14,29,59,89]:
			release_samples.append({"step":step,"surface_delta":_delta_stats(baseline,skin.garment_body.polygon,uv),"garment":_serial(skin.get_garment_diagnostics())})
	var final_delta := _delta_stats(baseline,skin.garment_body.polygon,uv)
	_check(float(final_delta.peak) < float(static_pose_delta.peak)*0.30,"released surface decays back toward original supported pose")
	var report := {"passed":failures.is_empty(),"failures":failures,"fixed_legs_90_frames_translation_and_clock_peak":fixed_peak,"static_leg_only_pose_delta":static_pose_delta,"pause_12_frames_surface_delta":pause_delta,"released_90_frames_final_delta":final_delta,"baseline_garment":_serial(fixed_diagnostics),"changed_leg_garment":_serial(changed_diagnostics),"release_samples":release_samples}
	var file := FileAccess.open(output.path_join("causality.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("LEG SUPPORT CAUSALITY ","PASS" if failures.is_empty() else "FAIL",": ",output)
	get_tree().quit(0 if failures.is_empty() else 1)
