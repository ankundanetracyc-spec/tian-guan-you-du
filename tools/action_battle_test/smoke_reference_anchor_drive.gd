extends "capture_reference_walk.gd"

var clock_value := 0.0

func _step(rig, velocity: Vector2, facing: float, clip: String, seconds := 1.0/60.0) -> void:
	clock_value += seconds
	rig.wanderer_skin.set_motion_context(seconds,velocity,clock_value,facing)
	rig.set_character_pose(clip,clock_value,facing)

func _ready() -> void:
	var rig := ReferenceRig.new()
	add_child(rig)
	assert(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"))
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	for frame in range(120): _step(rig,Vector2(114,0),1.0,"Run_New1")
	var relative_tip := Vector2(INF,-INF)
	var drive_peak := 0.0
	var acceleration_peak := 0.0
	for frame in range(90):
		_step(rig,Vector2(114,0),1.0,"Run_New1")
		var hip: Vector2 = rig.rig_root_node.to_local(skin.bones.pelvis.global_position)
		var tip_x: float = skin.cape_rear_tip.x-hip.x
		relative_tip.x = minf(relative_tip.x,tip_x)
		relative_tip.y = maxf(relative_tip.y,tip_x)
		drive_peak = maxf(drive_peak,absf(skin.reference_anchor_drive))
		acceleration_peak = maxf(acceleration_peak,skin.reference_anchor_acceleration.length())
	var before_acceleration: Vector2 = skin.reference_anchor_acceleration
	var before_drive: float = skin.reference_anchor_drive
	var before_steps: int = skin.garment_simulation_steps
	for frame in range(8): _step(rig,Vector2.ZERO,-1.0,"AttackIdle",0.0)
	var pause_pass: bool = before_acceleration==skin.reference_anchor_acceleration and before_drive==skin.reference_anchor_drive and before_steps==skin.garment_simulation_steps
	_step(rig,Vector2(114,0),-1.0,"BackstepWalk")
	var turn_pass: bool = skin.reference_anchor_samples==1 and skin.reference_anchor_drive==0.0 and skin.garment_angles.length()<0.50
	for frame in range(20): _step(rig,Vector2(114,0),-1.0,"BackstepWalk")
	_step(rig,Vector2(114,0),-1.0,"AttackIdle")
	var special_pass: bool = skin.reference_anchor_samples==0 and skin.reference_anchor_drive==0.0 and skin.garment_angles.length()<0.50
	_step(rig,Vector2(114,0),-1.0,"BackstepWalk")
	var return_pass: bool = skin.reference_anchor_samples==1 and skin.reference_anchor_drive==0.0
	clock_value = 0.0
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock_value,-1.0)
	rig.set_character_pose("Idle",clock_value,-1.0)
	var restart_pass: bool = skin.reference_anchor_samples==1 and skin.reference_anchor_drive==0.0 and skin.garment_angles.length()<0.001
	rig.position += Vector2(1200,0)
	_step(rig,Vector2.ZERO,-1.0,"Idle")
	var teleport_pass: bool = skin.reference_anchor_samples==1 and skin.reference_anchor_drive==0.0 and skin.garment_angles.length()<0.001
	for frame in range(60): _step(rig,Vector2(114,0),1.0,"Run_New1")
	for frame in range(240): _step(rig,Vector2.ZERO,1.0,"Idle")
	var settled_pass: bool = absf(skin.reference_anchor_drive)<0.00001 and skin.garment_angles.length()<0.002 and skin.garment_angular_velocity.length()<0.01
	var sustained_pass := drive_peak>0.01 and relative_tip.y-relative_tip.x>0.10 and drive_peak<=0.65 and acceleration_peak<=350.001
	var passed: bool = pause_pass and turn_pass and special_pass and return_pass and restart_pass and teleport_pass and settled_pass and sustained_pass
	var report := {"passed":passed,"steady_relative_tip_x_range":relative_tip.y-relative_tip.x,"anchor_drive_peak":drive_peak,"filtered_anchor_acceleration_peak":acceleration_peak,"pause_frozen":pause_pass,"turn_history_reset":turn_pass,"special_pose_history_reset":special_pass,"ordinary_return_warmup":return_pass,"clock_reset":restart_pass,"teleport_reset":teleport_pass,"stopped_settled":settled_pass,"final_anchor_drive":skin.reference_anchor_drive,"final_angles":_serial(skin.garment_angles),"final_angular_velocity":_serial(skin.garment_angular_velocity)}
	var file := FileAccess.open("res://output/reference_anchor_drive_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("REFERENCE ANCHOR DRIVE ","PASS" if passed else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if passed else 1)
