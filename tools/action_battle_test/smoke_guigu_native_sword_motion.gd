extends Node2D

## Independent geometry oracle for the actual native four-direction inspection.
const NativeRuntime = preload("guigu_native_first_runtime.gd")
var checks := 0
var failures: Array[String] = []
var samples := 0
var metrics := {"grip_error":0.0,"source_length_error":0.0,"projection_error":0.0,"arm_length_error":0.0,"minimum_projected_length":INF,"maximum_projected_length":0.0,"minimum_elbow_flex":INF,"maximum_elbow_flex":0.0,"maximum_wrist_bend":0.0}
var strokes := []
var rig: Node2D
var skin: RefCounted
var sword: RefCounted
var hands: RefCounted

func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		push_error("NATIVE FOUR SWINGS: "+label)

func _ready() -> void:
	rig = NativeRuntime.new()
	add_child(rig)
	_check(rig.build(),"actual original male101 runtime builds")
	if not rig.loaded:
		_finish()
		return
	skin = rig.wanderer_skin
	sword = skin.approved_sword
	hands = skin.native_hand_socket
	skin.motion_context = false
	skin.physics_enabled = false
	skin.motion_delta = 0.0
	_check(skin.rest_trs.size()==51 and skin.meshes.size()==7,"native source transforms and seven body meshes retained")
	_check(sword.native_pose_helper!=null and sword.far_sword_sprite!=null,"actual native 3D chain and split-depth sword active")
	_check(is_equal_approx(sword.swing_duration,0.72) and is_equal_approx(sword.grip_to_tip,55.0),"each inspection retains 0.72 seconds and source sword length55")
	var source_hash := FileAccess.get_sha256(sword.asset_dir.path_join("sword.png"))
	for facing in [-1.0,1.0]:
		for zoom in [1.0,2.6]:
			skin.display_scale = zoom
			for clip in ["Idle","Run_New1"]:
				for form in range(4):
					sword.restart_swing(true,form)
					var first := PackedVector2Array()
					for frame in range(181):
						sword.swing_elapsed = float(frame)/180.0*sword.swing_duration
						rig.set_character_pose(clip,0.173,facing)
						_sample(clip)
						var inverse: Transform2D = rig.rig_root_node.global_transform.affine_inverse()
						var grip: Vector2 = inverse*sword.last.grip_global
						var tip: Vector2 = inverse*sword.last.tip_global
						if frame==0: first = PackedVector2Array([grip,tip])
						if frame==180: _check(grip.distance_to(first[0])<0.0002 and tip.distance_to(first[1])<0.0002,"common held seam closes: "+str([form,clip,facing,zoom]))
					_stroke(form,clip,facing,zoom)
	_check(float(metrics.grip_error)<0.0002,"grip remains on the original palm bindpose through every 3D swing")
	_check(float(metrics.source_length_error)<0.0002,"true 3D grip-to-tip remains55 source pixels")
	_check(float(metrics.projection_error)<0.0002,"PNG grip and tip match direct 3D orthographic projection without normalized stretching")
	_check(float(metrics.arm_length_error)<0.0002,"upper/lower arm source 3D lengths preserved")
	_check(float(metrics.minimum_projected_length)<8.0 and float(metrics.maximum_projected_length)>54.9,"horizontal depth turn foreshortens then fully opens the same55 source blade")
	_check(float(metrics.minimum_elbow_flex)>25.0 and float(metrics.maximum_elbow_flex)<110.0,"actual elbow retains a safe bend throughout all four swings")
	_check(float(metrics.maximum_wrist_bend)<70.0,"shoulder/elbow carry the sweep rather than folding the wrist")
	for facing in [-1.0,1.0]:
		for clip in ["Idle","Run_New1"]: _wrist_probe(clip,facing)
	_controls()
	_check(FileAccess.get_sha256(sword.asset_dir.path_join("sword.png"))==source_hash,"sword PNG unchanged")
	_finish()

func _sample(clip: String) -> void:
	var root: Node2D = rig.rig_root_node
	var scale_value := root.global_transform.x.length()
	var hand: Transform3D = skin.world_transforms[skin.attachment_paths.near_hand]
	var grip_3d: Vector3 = hand*hands.grip_local
	var tip_3d := grip_3d+hand.basis.x.normalized()*0.55
	var expected_grip := root.to_global(Vector2(grip_3d.x,-grip_3d.y)*100.0-skin.ground_offset)
	var expected_tip := root.to_global(Vector2(tip_3d.x,-tip_3d.y)*100.0-skin.ground_offset)
	metrics.grip_error = maxf(metrics.grip_error,(sword.last.grip_global as Vector2).distance_to(expected_grip)/scale_value)
	metrics.source_length_error = maxf(metrics.source_length_error,absf(grip_3d.distance_to(sword.last.tip_3d)*100.0-55.0))
	metrics.projection_error = maxf(metrics.projection_error,(sword.last.tip_global as Vector2).distance_to(expected_tip)/scale_value)
	var projected_length: float = (sword.last.tip_global as Vector2).distance_to(sword.last.grip_global)/scale_value
	metrics.minimum_projected_length = minf(metrics.minimum_projected_length,projected_length)
	metrics.maximum_projected_length = maxf(metrics.maximum_projected_length,projected_length)
	var source: Dictionary = skin.evaluate_animation(clip,skin.sampled_time)
	for pair in [["near_upper","near_forearm"],["near_forearm","near_hand"]]:
		var a: String = skin.attachment_paths[pair[0]]
		var b: String = skin.attachment_paths[pair[1]]
		var expected_length: float = (source[a] as Transform3D).origin.distance_to((source[b] as Transform3D).origin)*100.0
		var actual_length: float = (skin.world_transforms[a] as Transform3D).origin.distance_to((skin.world_transforms[b] as Transform3D).origin)*100.0
		metrics.arm_length_error = maxf(metrics.arm_length_error,absf(actual_length-expected_length))
	var a: Vector3 = (skin.world_transforms[skin.attachment_paths.near_forearm] as Transform3D).origin-(skin.world_transforms[skin.attachment_paths.near_upper] as Transform3D).origin
	var b: Vector3 = hand.origin-(skin.world_transforms[skin.attachment_paths.near_forearm] as Transform3D).origin
	var flex := rad_to_deg(a.angle_to(b))
	metrics.minimum_elbow_flex = minf(metrics.minimum_elbow_flex,flex)
	metrics.maximum_elbow_flex = maxf(metrics.maximum_elbow_flex,flex)
	metrics.maximum_wrist_bend = maxf(metrics.maximum_wrist_bend,rad_to_deg(b.angle_to(hand.basis.x)))
	samples += 1

func _stroke(form: int, clip: String, facing: float, zoom: float) -> void:
	var points: Array[Vector3] = []
	var front_seen := false
	var back_seen := false
	for frame in range(61):
		sword.swing_elapsed = lerpf(0.29,0.60,float(frame)/60.0)*sword.swing_duration
		rig.set_character_pose(clip,0.173,facing)
		var tip: Vector3 = sword.last.tip_3d
		points.append(Vector3(tip.x,-tip.y,tip.z)*100.0)
		front_seen = front_seen or float(sword.last.arm_depth)<=float(sword.last.body_depth)
		back_seen = back_seen or float(sword.last.arm_depth)>float(sword.last.body_depth)
	var delta := points[-1]-points[0]
	var min_point := points[0]
	var max_point := points[0]
	for point in points:
		min_point = min_point.min(point)
		max_point = max_point.max(point)
	var span := max_point-min_point
	if form<2:
		_check((delta.x>75.0 if form==0 else delta.x< -75.0) and span.x>span.y*3.0,"horizontal sword truly sweeps across and return reverses it: "+str([form,clip,facing,zoom]))
		_check(span.z>60.0 and front_seen and back_seen,"horizontal hand/sword travel through real near/far depth: "+str([form,clip,facing,zoom]))
	else:
		_check((delta.y>100.0 if form==2 else delta.y< -100.0) and span.y>span.x*1.5,"vertical chop/rise have opposing large vertical sword paths: "+str([form,clip,facing,zoom]))
	_check(sword.sword_sprite.z_as_relative and sword.far_sword_sprite.z_as_relative and sword.last.depth_split,"native front/back blade rendering shares actor-relative depth: "+str([form,clip,facing,zoom]))
	var near_material: ShaderMaterial = sword.sword_sprite.material
	var far_material: ShaderMaterial = sword.far_sword_sprite.material
	_check(bool(near_material.get_shader_parameter("near_pass")) and not bool(far_material.get_shader_parameter("near_pass")) and sword.sword_sprite.global_transform.is_equal_approx(sword.far_sword_sprite.global_transform),"same PNG and transform split complementary near/far depth instead of flipping the hilt: "+str([form,clip,facing,zoom]))
	strokes.append({"form":form,"name":sword.last.form_name,"clip":clip,"facing":facing,"scale":zoom,"tip_main_stroke_delta_native_pixels":delta,"tip_main_stroke_span_native_pixels":span,"near_seen":front_seen,"far_seen":back_seen})

func _tick(clip: String, facing: float, clock_value: float, delta: float) -> void:
	skin.set_motion_context(delta,Vector2(skin.ROBED_MOVE_SPEED*facing,0) if clip=="Run_New1" else Vector2.ZERO,clock_value,facing)
	rig.set_character_pose(clip,clock_value,facing)

func _wrist_probe(clip: String, facing: float) -> void:
	skin.motion_context = false
	sword.set_mode("hold")
	rig.set_character_pose(clip,0.173,facing)
	var before_axis: Vector2 = sword.last.tip_global-sword.last.grip_global
	var before_elbow: Vector2 = skin.bones.near_forearm.position
	var before_wrist: Vector2 = skin.bones.near_hand.position
	var path: String = skin.attachment_paths.near_hand
	var pivot: Vector3 = (skin.world_transforms[path] as Transform3D).origin
	skin._rotate_source_branch(path,pivot,Quaternion(Vector3.BACK,-deg_to_rad(30.0)))
	skin._refresh_attachments()
	hands.update_pose()
	sword.update_attachment()
	var after_axis: Vector2 = sword.last.tip_global-sword.last.grip_global
	_check(absf(rad_to_deg(before_axis.angle_to(after_axis))-facing*30.0)<0.001,"actual isolated wrist still rotates actual sword by30 degrees: "+str([clip,facing]))
	_check(before_elbow.distance_to(skin.bones.near_forearm.position)<0.0001 and before_wrist.distance_to(skin.bones.near_hand.position)<0.0001,"isolated wrist changes no elbow/wrist joint positions: "+str([clip,facing]))
	_sample(clip)

func _controls() -> void:
	skin.motion_context = false
	skin.physics_enabled = true
	sword.set_mode("off")
	hands.set_pose_override("auto")
	sword.next_swing_form = 0
	var selected := []
	for index in range(4):
		sword.restart_swing(false)
		selected.append(sword.swing_form)
		rig.set_character_pose("Idle",0.0,1.0)
	_check(selected==[0,1,2,3],"successive actual 0-key single calls select outer/return/down/up")
	sword.restart_swing(true,0)
	var observed := {}
	for frame in range(240):
		_tick("Run_New1",-1.0,float(frame)/60.0,1.0/60.0)
		observed[int(sword.last.form_index)] = true
	_check(observed.size()==4 and sword.mode=="swing","loop visits all four actual poses while running")
	var elapsed: float = sword.swing_elapsed
	var before: Transform2D = sword.sword_sprite.global_transform
	for frame in range(12): _tick("Run_New1",-1.0,239.0/60.0,0.0)
	_check(is_equal_approx(elapsed,sword.swing_elapsed) and before.is_equal_approx(sword.sword_sprite.global_transform),"pause freezes true 3D swing and both projected blade pieces")
	for form in range(4):
		sword.restart_swing(false,form)
		_tick("Run_New1",-1.0,239.0/60.0,0.0)
		_check(sword.last.form_index==form and is_zero_approx(sword.swing_elapsed),"paused form selection redraws without advancing time: "+str(form))
	sword.set_mode("off")
	_tick("Run_New1",-1.0,239.0/60.0,0.0)
	_check(not sword.sword_sprite.visible and not sword.far_sword_sprite.visible and hands.pose_override=="auto","off hides both depth pieces and restores prior hand override")
	_check(sword.native_pose_helper.near_arm.z_index==sword.native_pose_helper.near_arm_base_z,"off restores original near arm render layer")
	sword.restart_swing(false,2)
	for frame in range(50): _tick("Idle",1.0,4.0+float(frame)/60.0,1.0/60.0)
	_check(sword.mode=="hold" and sword.last.swing_section=="hold","single inspection returns to common guard after original duration")
	_check(absf(float(sword.last.grip_to_tip_native)-55.0)<0.0002,"guard retains real55 source sword length after 3D swings")

func _finish() -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"samples":samples,"metrics":metrics,"strokes":strokes,"source_sword_length":55,"each_swing_duration":0.72,"form_order":["外横斩","回横斩","下劈","上挑"],"project_root":ProjectSettings.globalize_path("res://"),"scope":"Actual 51-transform native source chain. Four 0-key inspection forms only: real horizontal XZ sweeps and vertical up/down strokes, actual palm bindpose, constant 3D55 source sword, natural projected shortening and per-pixel split depth; no combat rule, damage or four-stage skill migration."}
	var directory: String = get_script().resource_path.get_base_dir().path_join("output")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(directory))
	var report_path := directory.path_join("guigu_native_sword_motion_smoke.json")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--report-path="): report_path = argument.trim_prefix("--report-path=")
	FileAccess.open(report_path,FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("NATIVE FOUR SWINGS ","PASS" if failures.is_empty() else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if failures.is_empty() else 1)
