extends "capture_cape_like_robe.gd"

var checks := 0
var failures: Array[String] = []

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)
		push_error(message)

func _ready() -> void:
	var rig := CapeRig.new()
	add_child(rig)
	_check(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"),"current candidate builds")
	if not rig.loaded:
		get_tree().quit(1)
		return
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	_check(skin.bones.size()==21 and skin.meshes.size()==7,"complete original skeleton and original split robe")
	_check(skin.visible_leg_meshes.size()==2 and skin.visible_leg_meshes[0].material==null and skin.visible_leg_meshes[1].material==null,"both original full legs retain rendering and skinning")
	var source_error := 0.0
	for record in skin.cape_body_records:
		var mesh: Polygon2D = record.mesh
		for index in range(mesh.polygon.size()):
			source_error = maxf(source_error,mesh.polygon[index].distance_to(skin._world(mesh.uv[index])))
	_check(source_error<=0.241,"standing source geometry retains only original subpixel seam overlap")
	var clock := 0.0
	var speeds := {}
	var maximum_phase_error := 0.0
	for speed in [57.0,114.0,117.5,171.0,235.0,352.5,400.0]:
		for frame in range(90):
			var phase_before: float = skin.gait_phase
			clock += 1.0/60.0
			skin.set_motion_context(1.0/60.0,Vector2(speed,0.0),clock,1.0)
			rig.set_character_pose("Run_New1",clock,1.0)
			var expected := fposmod(phase_before+skin.motion_delta*skin.gait_cadence_hz*clampf(skin.gait_amount/0.35,0.0,1.0),1.0)
			maximum_phase_error = maxf(maximum_phase_error,absf(wrapf(skin.gait_phase-expected,-0.5,0.5)))
		var d: Dictionary = skin.get_garment_diagnostics()
		_check(d.front_bones_resting and is_finite(d.lowest_cloth_y) and d.lowest_cloth_y<0.0,"front stable and cloth above floor at %s"%speed)
		speeds[str(speed)] = {"frequency":d.gait_cadence_hz,"stride_half":d.gait_stride_half,"lift":d.gait_lift,"angles":_serial(d.angles)}
	_check(maximum_phase_error<0.00001,"phase advances exactly once per simulation step")
	_check(speeds["57.0"].frequency<speeds["114.0"].frequency and speeds["114.0"].frequency<speeds["117.5"].frequency and speeds["117.5"].frequency<speeds["235.0"].frequency,"actual speed continuously increases frequency")
	_check(speeds["57.0"].stride_half<speeds["114.0"].stride_half and speeds["114.0"].stride_half<speeds["117.5"].stride_half and speeds["117.5"].stride_half<speeds["235.0"].stride_half,"actual speed continuously increases amplitude")
	_check(speeds["352.5"].frequency>speeds["235.0"].frequency and speeds["400.0"].frequency>speeds["352.5"].frequency and speeds["400.0"].stride_half>speeds["352.5"].stride_half,"ordinary fast walk above 310 remains a walking gait")
	var frozen := {}
	for key in skin.bones: frozen[key] = skin.bones[key].transform
	var steps_before: int = skin.garment_simulation_steps
	for frame in range(12):
		skin.set_motion_context(0.0,Vector2.ZERO,clock,-1.0)
		rig.set_character_pose("Idle",clock+frame,-1.0)
	var bones_unchanged := true
	for key in frozen: bones_unchanged = bones_unchanged and skin.bones[key].transform==frozen[key]
	_check(bones_unchanged and skin.garment_simulation_steps==steps_before,"pause freezes bones and cloth simulation")
	for frame in range(240):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,1.0)
		rig.set_character_pose("Idle",clock,1.0)
	var settled_angles: float = skin.garment_angles.length()
	var settled_speed: float = skin.garment_angular_velocity.length()
	_check(settled_angles<0.002 and settled_speed<0.01,"rear cloth returns after four stopped seconds")
	for frame in range(60):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(114,0),clock,1.0)
		rig.set_character_pose("Run_New1",clock,1.0)
	var before_dash: float = skin.gait_speed_smoothed
	for frame in range(12):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(600,0),clock,1.0)
		rig.set_character_pose("Step1",float(frame)/60.0,1.0)
	_check(absf(skin.gait_speed_smoothed-before_dash)<0.01,"dedicated dash does not inflate subsequent walk parameters")
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,0.0,1.0)
	rig.set_character_pose("Idle",0.0,1.0)
	_check(skin.garment_angles.length()<0.001 and skin.garment_angular_velocity.length()<0.01,"clock restart resets rear momentum")
	rig.position += Vector2(1200,0)
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,1.0/60.0,1.0)
	rig.set_character_pose("Idle",1.0/60.0,1.0)
	_check(skin.garment_angles.length()<0.001 and skin.garment_drive==0.0,"instant relocation resets rear momentum")
	var output := ProjectSettings.globalize_path("res://output/cape_like_robe_smoke.json")
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"speeds":speeds,"maximum_phase_error":maximum_phase_error,"settled_angles":settled_angles,"settled_angular_speed":settled_speed,"source_geometry_error":source_error,"diagnostics":_serial(skin.get_garment_diagnostics())}
	var file := FileAccess.open(output,FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("CAPE ROBE SMOKE ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks; ",output)
	get_tree().quit(0 if failures.is_empty() else 1)
