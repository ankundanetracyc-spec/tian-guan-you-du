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
	verify(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"),"认可玉虚素材载入")
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	skin.pose_blending = false
	verify(skin.validation_errors.is_empty(),"连续网格权重归一化")
	verify(skin.meshes.size()==7 and skin.bones.size()==21,"完整头身双臂双腿、前后开衩袍片与21骨")
	verify(skin.has_contact_shadow() and skin.contact_shadow.z_index>0,"接触影位于战场地面之上、人物之下")
	for mesh in skin.meshes:
		if "leg" in mesh.name:
			var used := {}
			for triangle in mesh.polygons:
				for vertex in triangle: used[vertex]=true
			verify(mesh.polygon.size()==513 and used.size()==513,"双腿从髋到鞋底完整，不隐藏顶点")
	var first := {}
	var previous := {}
	var swing_heights: Array[float] = []
	for clip in ["Idle","Run_New1","BackstepWalk","AttackIdle","Step1"]:
		var duration: float = 0.70 if clip=="BackstepWalk" else rig.animation_meta[clip].duration
		for frame in range(49):
			var time: float = duration*frame/48.0
			rig.set_character_pose(clip,time,1.0)
			for key in skin.bones:
				var point: Vector2 = rig.rig_root_node.to_local(skin.bones[key].global_position)
				verify(point.is_finite(),"所有姿态完整关节坐标有效")
				if clip=="Run_New1":
					if frame==0: first[key]=point
					if frame>0: verify(point.distance_to(previous[key])<6.5,"完整周期无单帧肢体跳变")
					if frame==48: verify(point.distance_to(first[key])<0.12,"跑步周期首尾闭合")
					previous[key]=point
			var near: Vector2=rig.rig_root_node.to_local(skin.bones.near_foot.global_position)
			var far: Vector2=rig.rig_root_node.to_local(skin.bones.far_foot.global_position)
			var lowest := -INF
			for side in ["near","far"]:
				var sole_y := -INF
				for point in skin.sole_profiles[side]:
					sole_y = maxf(sole_y,rig.rig_root_node.to_local(skin.bones[side+"_foot"].to_global(point)).y)
				verify(absf(sole_y+float(skin.ground_contacts[side].lift))<0.02,"可见鞋底实际蒙皮接触高度与抬脚一致")
				verify(sole_y<0.02,"鞋底不沉入地面")
				lowest=maxf(lowest,sole_y)
			verify(lowest>-0.02,"每一相位至少一只可见鞋底落在接触面")
			if clip=="Run_New1":
				verify(maxf(near.y,far.y)>-9.5,"始终有支撑脚贴近地面，未复现双脚长期悬空")
				swing_heights.append(absf(near.y-far.y))
			rig.set_character_pose(clip,time,-1.0)
			var mirrored: Vector2=rig.rig_root_node.to_local(skin.bones.near_foot.global_position)
			verify(mirrored.distance_to(near)<0.001,"左右只镜像，骨长和落地位置相同")
	verify(swing_heights.max()>8.0,"摆动脚确实屈膝回收而非双脚平移")
	var stride: float = skin._foot_sample(0.0,0.0).x-skin._foot_sample(0.50,0.0).x
	verify(stride>=43.9 and stride<=44.1,"长袍约束步幅44，完整腿保持可动")
	verify(swing_heights.max()>9.0 and swing_heights.max()<14.0,"收腿抬脚克制，不再大幅高抬")
	var robe_panels := []
	for record in skin.mesh_records:
		if record.name.begins_with("robe_"): robe_panels.append(record)
	verify(robe_panels.size()==2,"前后袍片独立成网格，完整腿可穿过开衩显示")
	if robe_panels.size()==2:
		var rear: Polygon2D=robe_panels[0].mesh
		var front: Polygon2D=robe_panels[1].mesh
		for row in range(57):
			var overlap: float = rear.uv[row*9+8].x-front.uv[row*9].x
			verify(overlap>=-0.001 and overlap<=48.01 and is_equal_approx(rear.uv[row*9+8].y,front.uv[row*9].y),"袍从腰部连续重叠，原衣襟UV未留洞或裁丢")
		verify(rear.uv[40*9+8].x-front.uv[40*9].x>35.0,"上腿处保留真正的包裹重叠，不再从腰切成两条直板")
	# Exercise transitions using the real simulation clock, including attacking
	# while moving and pause. Wall time and output screenshot speed are irrelevant.
	skin.pose_blending=true
	var before_attack := Vector2.ZERO
	var after_attack := Vector2.ZERO
	var t := 0.0
	for frame in range(100):
		t += 1.0/60.0
		var velocity := Vector2(114,0) if frame<60 else Vector2.ZERO
		var clip := "AttackIdle" if frame>=30 and frame<40 else ("Run_New1" if frame<60 else "Idle")
		skin.set_motion_context(1.0/60.0,velocity,t,1.0)
		rig.set_character_pose(clip,float(frame-30)/60.0 if clip=="AttackIdle" else t,1.0)
		if frame==30: before_attack=rig.rig_root_node.to_local(skin.bones.near_foot.global_position)
		if frame==39: after_attack=rig.rig_root_node.to_local(skin.bones.near_foot.global_position)
	verify(before_attack.distance_to(after_attack)>5.0,"移动施法时腿部继续支撑与迈步")
	verify(skin.gait_amount<0.001,"停止后步态幅度回落")
	var pause_pose := {}
	for key in skin.bones: pause_pose[key]=skin.bones[key].transform
	skin.set_motion_context(0.0,Vector2.ZERO,t,1.0)
	rig.set_character_pose("Idle",t,1.0)
	for key in skin.bones: verify(skin.bones[key].transform.is_equal_approx(pause_pose[key]),"暂停时动作与衣摆不按墙钟推进")
	# A live turn preserves bounded skirt motion. Upper legs may be visible;
	# the former forced knee-coverage assertions are intentionally retired.
	for frame in range(90):
		t += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(114,0),t,1.0)
		rig.set_character_pose("Run_New1",t,1.0)
	for frame in range(36):
		t += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(-114,0),t,-1.0)
		rig.set_character_pose("Run_New1",t,-1.0)
		verify(absf(skin.bones.front_cloth.position.x-skin.bones.front_cloth.rest.origin.x)<8.0,"转向前摆保持适度让位，不追膝鼓成大袋")
		verify(absf(skin.bones.back_cloth.position.x-skin.bones.back_cloth.rest.origin.x)<8.0,"转向后摆保持适度让位，不靠遮挡藏腿")
	var stop_fold: float = absf(skin.bones.front_hem.rotation)+absf(skin.bones.back_hem.rotation)+absf(skin.bones.back_edge.rotation)
	var delayed_fold := 0.0
	for frame in range(120):
		t += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,t,-1.0)
		rig.set_character_pose("Idle",t,-1.0)
		if frame==2:
			delayed_fold=absf(skin.bones.front_hem.rotation)+absf(skin.bones.back_hem.rotation)+absf(skin.bones.back_edge.rotation)
		for side in ["front","back"]:
			for part in ["cloth","hem","edge"]:
				var bone: Bone2D=skin.bones[side+"_"+part]
				verify(bone.scale.is_equal_approx(Vector2.ONE),"衣摆段不通过缩放拉长")
				verify(absf(bone.rotation)<=0.301,"起停回落没有大幅甩摆")
	verify(stop_fold>0.02 and delayed_fold>0.01,"停止后衣摆保留短暂延迟，未即时定住")
	for side in ["front","back"]:
		verify(absf(skin.bones[side+"_hem"].rotation)<0.008 and absf(skin.bones[side+"_edge"].rotation)<0.008,"停止两秒后下摆回落，无持续大抖")
		verify(absf(skin.cloth_positions[side+"_lift"].y)<0.02,"停止后提摆恢复原袍长")
	print("YUXU NATURAL CHECKS: ",checks,"; failures: ",failures.size())
	rig.queue_free()
	quit(0 if failures.is_empty() else 1)
