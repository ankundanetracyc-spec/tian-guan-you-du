extends Node2D

const ApprovedRuntime = preload("approved_outfit_fitting_runtime.gd")
var failures: Array[String] = []
var checks := 0
var clock := 0.0
var metrics: Dictionary = {}
var maximum_length_error := 0.0
var maximum_scale_error := 0.0
var maximum_contact_error := 0.0
var maximum_contact_height_error := 0.0
var maximum_pitch_error := 0.0
var maximum_floor_penetration := 0.0
var maximum_floor_error := 0.0
var frames := 0

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value: failures.append(message)

func _measure(rig: Node2D, key: String) -> void:
	var skin: RefCounted = rig.wanderer_skin
	if not metrics.has(key):
		metrics[key] = {"frames":0,"ankle_min_x":INF,"ankle_max_x":-INF,"sole_min_y":INF,"sole_max_y":-INF,"knee_flex_min":INF,"knee_flex_max":-INF,"pelvis_min_y":INF,"pelvis_max_y":-INF,"max_front_thigh_elevation":0.0,"max_front_knee_above_hip":0.0,"max_foot_lift":0.0,"both_feet_airborne":0}
	var m: Dictionary = metrics[key]
	m.frames += 1
	m.pelvis_min_y = minf(m.pelvis_min_y,skin.bones.pelvis.position.y)
	m.pelvis_max_y = maxf(m.pelvis_max_y,skin.bones.pelvis.position.y)
	var airborne := 0
	for side in ["near","far"]:
		var thigh: Bone2D = skin.bones[side+"_thigh"]
		var calf: Bone2D = skin.bones[side+"_calf"]
		var foot: Bone2D = skin.bones[side+"_foot"]
		var hip: Vector2 = rig.rig_root_node.to_local(thigh.global_position)
		var knee: Vector2 = rig.rig_root_node.to_local(calf.global_position)
		var ankle: Vector2 = rig.rig_root_node.to_local(foot.global_position)
		maximum_length_error = maxf(maximum_length_error,maxf(absf(hip.distance_to(knee)-skin.approved_leg_lengths.x),absf(knee.distance_to(ankle)-skin.approved_leg_lengths.y)))
		for bone in [thigh,calf,foot,skin.bones[side+"_upper"],skin.bones[side+"_forearm"],skin.bones[side+"_hand"]]:
			maximum_scale_error = maxf(maximum_scale_error,bone.scale.distance_to(Vector2.ONE))
			_check(bone.position.is_finite() and is_finite(bone.rotation),key+": finite limb pose")
		var foot_axis: Vector2 = rig.rig_root_node.to_local(foot.to_global(Vector2.RIGHT))-ankle
		var pitch := foot_axis.angle()
		var sole: Vector2 = ankle+skin._sole_contact(side,pitch)
		var contact: Dictionary = skin.ground_contacts[side]
		maximum_contact_error = maxf(maximum_contact_error,sole.distance_to(contact.center))
		maximum_contact_height_error = maxf(maximum_contact_height_error,absf(sole.y-float(contact.center.y)))
		maximum_pitch_error = maxf(maximum_pitch_error,absf(wrapf(pitch-float(contact.pitch),-PI,PI)))
		maximum_floor_penetration = maxf(maximum_floor_penetration,sole.y)
		if float(contact.lift)<0.01: maximum_floor_error = maxf(maximum_floor_error,absf(sole.y))
		if float(contact.lift)>0.5: airborne += 1
		m.ankle_min_x = minf(m.ankle_min_x,ankle.x-hip.x)
		m.ankle_max_x = maxf(m.ankle_max_x,ankle.x-hip.x)
		m.sole_min_y = minf(m.sole_min_y,sole.y)
		m.sole_max_y = maxf(m.sole_max_y,sole.y)
		var flex := absf((knee-hip).angle_to(ankle-knee))
		m.knee_flex_min = minf(m.knee_flex_min,flex)
		m.knee_flex_max = maxf(m.knee_flex_max,flex)
		if knee.x>hip.x:
			m.max_front_thigh_elevation = maxf(m.max_front_thigh_elevation,atan2(knee.x-hip.x,knee.y-hip.y))
			m.max_front_knee_above_hip = maxf(m.max_front_knee_above_hip,hip.y-knee.y)
		m.max_foot_lift = maxf(m.max_foot_lift,-sole.y)
	if airborne==2: m.both_feet_airborne += 1
	frames += 1

func _step(rig: Node2D, key: String, clip: String, velocity: Vector2, facing: float, dt: float = 1.0/60.0) -> void:
	clock += dt
	rig.wanderer_skin.set_motion_context(dt,velocity,clock,facing)
	rig.set_character_pose(clip,clock,facing)
	_measure(rig,key)

func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output"))
	var hashes := {}
	for name in ["cutout.png","body.png","leg.png","head.png","arm_relaxed_v19.png"]:
		var path: String = "res://art/yuxu_fullbody_v15/"+name
		if FileAccess.file_exists(path): hashes[name] = FileAccess.get_sha256(path)
	var rig := ApprovedRuntime.new()
	add_child(rig)
	_check(rig.build(),"approved original outfit runtime builds")
	if not rig.loaded:
		push_error(str(rig.load_errors))
		get_tree().quit(1)
		return
	var skin: RefCounted = rig.wanderer_skin
	for side in ["near","far"]:
		_check(absf(skin.bones[side+"_calf"].rest.origin.length()-skin.approved_leg_lengths.x)<0.00001,side+": same fixed thigh")
		_check(absf(skin.bones[side+"_foot"].rest.origin.length()-skin.approved_leg_lengths.y)<0.00001,side+": same fixed calf")
	for record in skin.mesh_records:
		if record.name not in ["near_leg","far_leg"]: continue
		var leg_mesh: Polygon2D = record.mesh
		var original_row_width := 195.0*float(skin.PIXEL_SCALE)
		for sample in [{"row":10,"minimum":1.22,"maximum":1.26,"part":"thigh"},{"row":34,"minimum":1.16,"maximum":1.20,"part":"calf"},{"row":56,"minimum":0.99999,"maximum":1.00001,"part":"full foot"}]:
			var start := int(sample.row)*(int(skin.COLUMNS)+1)
			var observed := leg_mesh.polygon[start].distance_to(leg_mesh.polygon[start+int(skin.COLUMNS)])/original_row_width
			_check(observed>=float(sample.minimum) and observed<=float(sample.maximum),str(record.name)+": actual rest "+str(sample.part)+" transverse fit")
	for frame in range(300): _step(rig,"run114","Run_New1",Vector2(114,0),1.0)
	for frame in range(240): _step(rig,"backstep114","BackstepWalk",Vector2(-114,0),1.0)
	for frame in range(240):
		var facing := 1.0 if frame<120 else -1.0
		_step(rig,"turn114","Run_New1",Vector2(114,0)*facing,facing)
	for frame in range(300):
		var moving := frame<150
		_step(rig,"start_stop","Run_New1" if moving else "Idle",Vector2(114,0) if moving else Vector2.ZERO,1.0)
	var frozen := []
	for bone in skin.bones.values(): frozen.append(bone.transform)
	var mesh_before := []
	for mesh in skin.meshes: mesh_before.append(mesh.polygon.duplicate())
	for frame in range(30):
		skin.set_motion_context(0.0,Vector2(114,0),clock,1.0)
		rig.set_character_pose("Run_New1",clock+5.0,1.0)
	var index := 0
	for bone in skin.bones.values():
		_check(bone.transform==frozen[index],"pause freezes bone "+str(index))
		index += 1
	for mesh_index in range(skin.meshes.size()): _check(skin.meshes[mesh_index].polygon==mesh_before[mesh_index],"pause freezes mesh "+str(mesh_index))
	_check(maximum_length_error<0.0002,"all actual thigh/calf lengths remain fixed")
	_check(maximum_scale_error<0.00001,"all leg/arm bone scales stay1")
	# On a flat sole, adjacent lowest alpha samples may tie in Y; minuscule
	# angle round-off can select a different valid X contact on that same sole.
	_check(maximum_contact_height_error<0.025,"actual sole height equals recorded contact target")
	_check(maximum_pitch_error<0.00001,"actual rigid foot pitch equals solved contact pitch")
	_check(maximum_floor_penetration<0.025,"actual sole never penetrates floor")
	_check(maximum_floor_error<0.025,"planted actual soles stay on floor")
	_check(float(metrics.run114.ankle_max_x)-float(metrics.run114.ankle_min_x)>45.0,"actual ankle trajectory has a broad run span")
	_check(float(metrics.run114.knee_flex_max)>1.2,"actual rear recovery bends the knee")
	_check(int(metrics.run114.both_feet_airborne)>0,"run contains actual both-feet flight")
	_check(float(metrics.run114.both_feet_airborne)/float(metrics.run114.frames)<0.30,"run flight remains a brief part of the cycle")
	for name in hashes:
		_check(FileAccess.get_sha256("res://art/yuxu_fullbody_v15/"+name)==hashes[name],"original PNG unchanged: "+name)
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"frames":frames,"maximum_length_error":maximum_length_error,"maximum_scale_error":maximum_scale_error,"maximum_sole_contact_height_error":maximum_contact_height_error,"maximum_pitch_error":maximum_pitch_error,"maximum_sole_contact_point_distance_with_flat_sole_ties":maximum_contact_error,"maximum_floor_penetration":maximum_floor_penetration,"maximum_planted_sole_floor_error":maximum_floor_error,"metrics":metrics,"art_hashes":hashes,"runtime":rig.get_runtime_summary(),"scope":"actual old-art project limbs and sole contacts over live114run/backstep/turn/start-stop/pause"}
	var file := FileAccess.open("res://output/approved_outfit_stride_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("APPROVED OUTFIT STRIDE ","PASS" if failures.is_empty() else "FAIL"," ",checks," checks ",JSON.stringify(failures)," LENGTH ",maximum_length_error," SOLE ",maximum_contact_error," FLOOR ",maximum_floor_penetration)
	get_tree().quit(0 if failures.is_empty() else 1)
