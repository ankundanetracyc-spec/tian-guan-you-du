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
	verify(rig.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art", "yuxu_unified_v14"), "统一七头身素材载入")
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	skin.pose_blending = false
	verify(skin.validation_errors.is_empty(), "所有网格权重归一化")
	verify(skin.bones.size() == 19 and skin.meshes.size() == 7, "自有19骨与7张连续蒙皮")
	rig.set_character_pose("BindPose", 0, 1.0)
	var definition: Dictionary = skin.config
	verify(is_equal_approx((float(definition.sole) - float(definition.crown[1])) / (float(definition.chin[1])-float(definition.crown[1])), 7.0), "颅顶至脚底七头长，不计发髻")
	for side in ["near", "far"]:
		verify(is_equal_approx(skin.bones[side+"_forearm"].position.length(), 17.0), "上臂按参考收为17")
		verify(is_equal_approx(skin.bones[side+"_hand"].position.length(), 14.0), "前臂按参考收为14")
		verify(is_equal_approx(skin.bones[side+"_calf"].position.length(), 27.0), "统一髋膝骨长")
		verify(is_equal_approx(skin.bones[side+"_foot"].position.length(), 25.0), "统一膝踝骨长")
	for mesh in skin.meshes:
		verify(mesh.polygon.size() == 513 and mesh.polygons.size() == 896, "所有分件保留完整网格，尤其髋到靴底不得裁掉")
		var used: Dictionary = {}
		for triangle in mesh.polygons:
			for index in triangle:
				used[index] = true
		verify(used.size() == mesh.polygon.size(), "每个顶点参与可见拓扑，没有裁腿掩盖接合")
	var first: Dictionary = {}
	var previous: Dictionary = {}
	var foot_samples: Array = []
	for clip in ["Idle", "Run_New1", "AttackIdle", "Step1", "BackstepWalk"]:
		var duration: float = 0.7 if clip == "BackstepWalk" else rig.animation_meta[clip].duration
		for frame in range(49):
			var time: float = duration * frame / 48.0
			rig.set_character_pose(clip, time, 1.0)
			for key in skin.bones:
				var point: Vector2 = rig.rig_root_node.to_local(skin.bones[key].global_position)
				verify(point.is_finite(), "全部动作骨骼坐标有效")
				if clip == "Run_New1":
					if frame == 0: first[key] = point
					if frame > 0: verify(point.distance_to(previous[key]) < 12.0, "跑步完整周期没有单帧关节跳变")
					if frame == 48: verify(point.distance_to(first[key]) < 0.001, "跑步循环首尾连续")
					previous[key] = point
			var near_foot: Vector2 = rig.rig_root_node.to_local(skin.bones.near_foot.global_position)
			if clip == "Run_New1": foot_samples.append(near_foot.x)
			rig.set_character_pose(clip, time, -1.0)
			verify(rig.rig_root_node.to_local(skin.bones.near_foot.global_position).distance_to(near_foot) < 0.001, "左右整体镜像，骨骼与材质不换比例")
	verify(foot_samples.max()-foot_samples.min() > 20.0, "完整腿有真实摆动，非静止人像平移")
	for mode in ["reference", "project", "experiment"]:
		rig.set_skin_mode(mode)
		for mesh in skin.meshes:
			verify(mesh.visible == (mode == "experiment"), "旧外观切换不留新图层")
	print("YUXU UNIFIED CHECKS: ", checks, "; failures: ", failures.size())
	rig.queue_free()
	quit(0 if failures.is_empty() else 1)
