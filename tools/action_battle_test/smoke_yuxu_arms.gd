extends SceneTree

var checks := 0
var failures: Array[String] = []

func verify(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var rig = load("res://guigu_rig_runtime.gd").new()
	root.add_child(rig)
	verify(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"),"认可玉虚造型载入")
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	skin.pose_blending = false
	for mesh in skin.meshes:
		if mesh.name in ["YuxuUnified_near_arm","YuxuUnified_far_arm"]:
			var torso_weights: PackedFloat32Array = mesh.get_bone_weights(3)
			var hand_weights: PackedFloat32Array = mesh.get_bone_weights(2)
			for index in range(mesh.uv.size()):
				var uv: Vector2 = mesh.uv[index]
				if uv.x>640.0 and uv.y<450.0:
					verify(torso_weights[index]<0.001 and hand_weights[index]>0.99,"手指顶点完整随腕，不再误绑躯干肩缝")
	rig.set_character_pose("Idle",0.0,1.0)
	var relaxed_near: Vector2 = rig.rig_root_node.to_local(skin.bones.near_hand.global_position)
	var relaxed_far: Vector2 = rig.rig_root_node.to_local(skin.bones.far_hand.global_position)
	verify(relaxed_near.y>-68.0 and relaxed_far.y>-68.0,"安静待机双手自然放低，不保持举手防御")
	skin.update_cloth("Idle",7.0,32.0,{})
	var waist_hand: Vector2 = rig.rig_root_node.to_local(skin.bones.near_hand.global_position)
	verify(waist_hand.y<relaxed_near.y-7.0 and waist_hand.y>-77.0,"较长静止只轻收手至腰前")
	skin.update_cloth("Idle",20.5,32.0,{})
	verify(skin.gesture_focus>0.99,"久站后才短暂凝神剑指")
	verify(float(skin.arm_materials.near.get_shader_parameter("sword_amount"))>0.99,"凝神使用独立剑指手型")
	skin.update_cloth("Idle",27.0,32.0,{})
	verify(skin.gesture_focus<0.001 and skin.gesture_waist<0.001,"凝神结束后自然垂手，不频繁轮换")
	var elbows: Array[float] = []
	var wrists: Array[float] = []
	var gaps: Array[float] = []
	var previous := {}
	for frame in range(97):
		var time := 0.5833333*frame/96.0
		rig.set_character_pose("Run_New1",time,1.0)
		elbows.append(skin.bones.near_forearm.rotation)
		wrists.append(skin.bones.near_hand.rotation)
		gaps.append(skin.bones.near_hand.global_position.distance_to(skin.bones.far_hand.global_position)/rig.rig_root_node.global_scale.x)
		for side in ["near","far"]:
			for part in ["upper","forearm","hand"]:
				var bone: Bone2D=skin.bones[side+"_"+part]
				verify(bone.scale.is_equal_approx(Vector2.ONE),"上肢不通过拉长缩放伪造动作")
				if frame>0: verify(absf(angle_difference(previous[bone.name],bone.rotation))<0.20,"肩肘腕完整周期无单帧跳角")
				previous[bone.name]=bone.rotation
	verify(elbows.max()-elbows.min()>0.25,"肘角独立屈伸，不再固定屈肘整臂平摆")
	verify(wrists.max()-wrists.min()>0.10,"腕部在跑动周期独立收放")
	verify(gaps.max()>4.0,"跑动主辅手有可读的分离轮廓")
	rig.set_character_pose("AttackIdle",float(rig.animation_meta.AttackIdle.duration)*0.42,1.0)
	var near: Vector2=rig.rig_root_node.to_local(skin.bones.near_hand.global_position)
	var far: Vector2=rig.rig_root_node.to_local(skin.bones.far_hand.global_position)
	verify(near.x-far.x>8.0 and far.y-near.y>12.0,"主手出掌而辅手保持低位，不是双手并推")
	verify(float(skin.arm_materials.near.get_shader_parameter("sword_amount"))<0.001,"拳掌出招不变成剑技手型")
	skin.set_action_context(true,false)
	rig.set_character_pose("Idle",0.0,1.0)
	verify(skin.gesture_ready>0.99 and skin.gesture_focus<0.001,"真实架招窗口映射抬手准备，没有附加凝神效果")
	skin.set_action_context(false,false)
	# Live stop/turn/attack uses actual simulation time; pause must hold the
	# independently animated wrist as well as the shoulder and elbow.
	skin.pose_blending=true
	var clock := 0.0
	for frame in range(90):
		clock+=1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(skin.ROBED_MOVE_SPEED,0),clock,1.0)
		rig.set_character_pose("Run_New1",clock,1.0)
	var held := {}
	for side in ["near","far"]:
		for part in ["upper","forearm","hand"]:
			var key: String=side+"_"+part
			held[key]=skin.bones[key].transform
	skin.set_motion_context(0.0,Vector2(skin.ROBED_MOVE_SPEED,0),clock,1.0)
	rig.set_character_pose("Run_New1",clock,1.0)
	for key in held: verify(held[key].is_equal_approx(skin.bones[key].transform),"暂停时肩肘腕保持当前姿态")
	# Exercise the real long-idle clock rather than the looping source clip.
	# A move interrupts both slow idle variations smoothly and resets age.
	var last_angles := {}
	var maximum_focus := 0.0
	var maximum_waist := 0.0
	for frame in range(1620):
		clock += 1.0/60.0
		var walking := frame>=1500
		var velocity := Vector2(skin.ROBED_MOVE_SPEED,0) if walking else Vector2.ZERO
		skin.set_motion_context(1.0/60.0,velocity,clock,1.0)
		rig.set_character_pose("Run_New1" if walking else "Idle",clock,1.0)
		maximum_focus = maxf(maximum_focus,skin.gesture_focus)
		maximum_waist = maxf(maximum_waist,skin.gesture_waist)
		for key in ["near_upper","near_forearm","near_hand","far_upper","far_forearm","far_hand"]:
			var angle: float = skin.bones[key].rotation
			if frame>0: verify(absf(angle_difference(last_angles[key],angle))<0.16,"长待机各手势及转行走肩肘腕无瞬间跳变")
			last_angles[key] = angle
	verify(maximum_focus>0.98 and maximum_waist>0.98,"真实持续静止时钟依次到达腰前与短凝神")
	verify(skin.idle_gesture_age<0.001 and skin.gesture_focus<0.001,"移动取消凝神并重新计静止时长")
	print("YUXU ARMS CHECKS: ",checks,"; failures: ",failures.size())
	rig.queue_free()
	quit(0 if failures.is_empty() else 1)
