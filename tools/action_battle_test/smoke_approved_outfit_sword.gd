extends Node2D

const ApprovedRuntime = preload("approved_outfit_fitting_runtime.gd")
var rig: Node2D
var skin: RefCounted
var hands: RefCounted
var sword: RefCounted
var clock := 0.0
var checks := 0
var failures: Array[String] = []
var frames := 0
var maximum_grip_error := 0.0
var maximum_sword_length_error := 0.0
var maximum_arm_length_error := 0.0
var maximum_uniform_scale_error := 0.0
var bad_depth_frames := 0
var bad_hand_frames := 0
var nonfinite := 0

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value: failures.append(message)

func _tick(clip: String, facing: float, delta: float = 1.0/60.0) -> void:
	clock += delta
	var velocity := Vector2(114.0*facing,0.0) if clip=="Run_New1" else Vector2.ZERO
	skin.set_motion_context(delta,velocity,clock,facing)
	rig.set_character_pose(clip,clock,facing)
	frames += 1
	if sword.mode=="off": return
	var attachment: Transform2D = hands.get_attachment_transform("near")
	var actual_grip: Vector2 = sword.sword_sprite.to_global(sword.source_grip)
	var actual_tip: Vector2 = sword.sword_sprite.to_global(sword.source_tip)
	var root_scale: float = rig.rig_root_node.global_transform.x.length()
	maximum_grip_error = maxf(maximum_grip_error,actual_grip.distance_to(attachment.origin)/root_scale)
	maximum_sword_length_error = maxf(maximum_sword_length_error,absf(actual_grip.distance_to(actual_tip)/root_scale-float(sword.grip_to_tip)))
	var transform: Transform2D = sword.sword_sprite.global_transform
	maximum_uniform_scale_error = maxf(maximum_uniform_scale_error,absf(transform.x.length()-transform.y.length())/root_scale)
	var upper: Bone2D = skin.bones.near_upper
	var elbow: Bone2D = skin.bones.near_forearm
	var wrist: Bone2D = skin.bones.near_hand
	maximum_arm_length_error = maxf(maximum_arm_length_error,absf(upper.global_position.distance_to(elbow.global_position)/root_scale-elbow.rest.origin.length()))
	maximum_arm_length_error = maxf(maximum_arm_length_error,absf(elbow.global_position.distance_to(wrist.global_position)/root_scale-wrist.rest.origin.length()))
	var near_hand: Sprite2D = hands.get_hand_node("near")
	if sword.sword_sprite.z_index!=near_hand.z_index-1: bad_depth_frames += 1
	if hands.hands.near.style!="grip" or hands.hands.far.style!="open": bad_hand_frames += 1
	if not actual_grip.is_finite() or not actual_tip.is_finite(): nonfinite += 1

func _ready() -> void:
	rig = ApprovedRuntime.new()
	add_child(rig)
	_check(rig.build(),"current approved outfit and sword build")
	if not rig.loaded:
		_finish()
		return
	skin = rig.wanderer_skin
	hands = skin.approved_hands
	sword = skin.approved_sword
	_check(sword!=null and sword.loaded and sword.errors.is_empty(),"sword asset and registration load")
	if sword==null or not sword.loaded:
		_finish()
		return
	_check(is_equal_approx(sword.grip_to_tip,55.0),"preview sword grip-to-tip ruler is55 character units")
	var sword_path: String = sword.asset_dir.path_join("sword.png")
	var sword_hash := FileAccess.get_sha256(sword_path)
	var original_sword_finger_hash := FileAccess.get_sha256("res://art/yuxu_fullbody_v15/arm_sword_fingers_v21.png")
	hands.set_pose_override("fist")
	sword.set_mode("hold")
	for facing in [-1.0,1.0]:
		for zoom in [2.0,5.0,6.0]:
			rig.display_zoom = zoom
			for clip in ["Idle","Run_New1"]:
				for frame in range(60): _tick(clip,facing)
	_check(sword.sword_sprite.visible,"held sword is visible")
	_check(hands.pose_override=="grip_near","holding selects only the near right grip hand")
	sword.set_mode("off")
	_tick("Idle",1.0)
	_check(not sword.sword_sprite.visible,"off mode hides the sword")
	_check(hands.pose_override=="fist" and hands.hands.near.style=="fist" and hands.hands.far.style=="fist","off mode restores previous normal hand override")
	hands.set_pose_override("auto")
	rig.display_zoom = 5.0
	sword.restart_swing(true)
	for facing in [-1.0,1.0]:
		for frame in range(120): _tick("Idle",facing)
	_check(sword.mode=="swing" and sword.swing_elapsed>sword.swing_duration*2.0,"looped swing survives multiple complete cycles")
	var elapsed: float = sword.swing_elapsed
	var transform: Transform2D = sword.sword_sprite.global_transform
	var upper_rotation: float = skin.bones.near_upper.rotation
	var lower_rotation: float = skin.bones.near_forearm.rotation
	for frame in range(12): _tick("Idle",1.0,0.0)
	_check(is_equal_approx(sword.swing_elapsed,elapsed) and sword.sword_sprite.global_transform==transform and is_equal_approx(skin.bones.near_upper.rotation,upper_rotation) and is_equal_approx(skin.bones.near_forearm.rotation,lower_rotation),"pause freezes sword motion and its arm without losing grip")
	sword.restart_swing(false)
	for frame in range(ceili(sword.swing_duration*60.0)+3): _tick("Idle",1.0)
	_check(sword.mode=="hold","single swing recovers into holding")
	sword.set_mode("off")
	_tick("Idle",1.0)
	_check(hands.pose_override=="auto","single swing/off restores the original automatic hands")
	_check(maximum_grip_error<0.002,"actual sword grip remains on the hand after zoom and mirror")
	_check(maximum_sword_length_error<0.002,"actual blade length stays55 without stretching")
	_check(maximum_arm_length_error<0.002,"holding and swinging preserve both arm lengths")
	_check(maximum_uniform_scale_error<0.00001,"sword image uses one uniform scale")
	_check(bad_depth_frames==0,"sword stays behind near fingers by one Z level")
	_check(bad_hand_frames==0,"holding and swinging use near grip/far relaxed only")
	_check(nonfinite==0,"all sword attachment points are finite")
	_check(FileAccess.get_sha256(sword_path)==sword_hash,"sword PNG remains unchanged during pose simulation")
	_check(FileAccess.get_sha256("res://art/yuxu_fullbody_v15/arm_sword_fingers_v21.png")==original_sword_finger_hash,"original v21 sword-finger artwork remains untouched")
	_finish()

func _finish() -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"frames":frames,"maximum_grip_attachment_error_character_units":maximum_grip_error,"maximum_grip_to_tip_length_error":maximum_sword_length_error,"maximum_arm_length_error":maximum_arm_length_error,"maximum_uniform_scale_error":maximum_uniform_scale_error,"bad_depth_frames":bad_depth_frames,"bad_hand_frames":bad_hand_frames,"nonfinite_attachments":nonfinite,"scope":"Actual final sword global transform after runtime zoom, both facings, Idle/Run_New1, looped and single swing, pause, hand restore. No skill, damage, hitbox or game inventory behavior is tested or introduced."}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output"))
	FileAccess.open("res://output/approved_outfit_sword_smoke.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("APPROVED SWORD ","PASS" if failures.is_empty() else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if failures.is_empty() else 1)
