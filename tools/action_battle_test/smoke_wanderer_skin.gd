extends SceneTree

var checks := 0
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run_checks")

func verify(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
		push_error(message)

func run_checks() -> void:
	var runtime = load("res://guigu_rig_runtime.gd").new()
	root.add_child(runtime)
	verify(runtime.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art", "original_wanderer"), "玉虚专用蒙皮载入")
	runtime.set_skin_mode("experiment")
	var skin: RefCounted = runtime.wanderer_skin
	verify(skin != null and skin.meshes.size() == 8, "八张专用蒙皮")
	verify(runtime.node_by_rig_path.size() == 71 and runtime.skeleton.get_bone_count() == 89, "保留原71节点，新增衣摆、腿部及手臂视觉骨链")
	verify(skin.validation_errors.is_empty(), "每顶点权重归一化")
	var source_manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://art/original_wanderer/skin_v2.json"))
	verify(source_manifest.clothingName == "玉虚·内门常服", "服装身份与现有衣着一致")
	for mesh in skin.meshes:
		var shoulder := mesh.get_node_or_null("ShoulderBehindTorso") as Polygon2D
		var triangle_count: int = mesh.polygons.size() + (shoulder.polygons.size() if shoulder else 0)
		var is_leg: bool = str(mesh.name) in ["WandererSkin_far_leg", "WandererSkin_near_leg"]
		verify(mesh.polygon.size() == 287 and mesh.uv.size() == 287 and triangle_count == (168 if is_leg else 480), "分件网格与可见拓扑完整")
		if is_leg:
			for triangle in mesh.polygons:
				verify(triangle[0] >= 26 * 7 and triangle[1] >= 26 * 7 and triangle[2] >= 26 * 7, "长袍内大腿和裤腿不露出，只保留靴口以下")
		if shoulder:
			verify(shoulder.z_index < int(source_manifest.parts.torso.layer) and mesh.z_index > int(source_manifest.parts.torso.layer), "肩部位于衣身后，前臂位于衣身前")
			verify(shoulder.get_bone_count() == mesh.get_bone_count(), "肩部沿用手臂骨骼权重")
		var expected_texture: Texture2D = skin.arm_texture if str(mesh.name) in ["WandererSkin_far_arm", "WandererSkin_near_arm"] else skin.source_texture
		verify(mesh.texture == expected_texture and mesh.get_bone_count() > 0, "分件纹理与独立袖手纹理绑定正确且保留蒙皮")
		verify(mesh.texture_filter == CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS, "小比例贴图使用缩小过滤")
	for part_name in ["far_arm", "near_arm"]:
		var arm: Dictionary = source_manifest.parts[part_name]
		verify(float(arm.joints[2]) <= 0.88 and float(arm.joints[2]) >= 0.84, "腕点位于宽袖内部的实际腕口，不得落到掌心")
		verify(float(arm.handLength) >= 6.0 and float(arm.handLength) <= 7.0, "小手与全身比例协调，不将手掌单独放大")
		verify(arm.jointAxes.size() == 4, "肩、肘、腕、指端贴图各有轴心，袖口与腕点连续")
		var arm_chain: Array = skin.arm_chains[part_name].bones
		verify(is_equal_approx(arm_chain[1].rest.origin.length(), 16.0) and is_equal_approx(arm_chain[2].rest.origin.length(), 13.5), "远近上臂/前臂保持一致解剖长度，避免一只袖被压短")
	var animated_path := "/10002/Root/Body/Foot/zuotui/zuotui/tui1"
	var angle_samples: Array[float] = []
	for clip in ["Idle", "Run_New1", "AttackIdle", "Step1"]:
		var duration: float = runtime.animation_meta[clip].duration
		for frame in range(24):
			var time := duration * float(frame) / 24.0
			runtime.set_character_pose(clip, time, 1.0)
			verify(runtime.scale.is_equal_approx(Vector2(0.72, 0.72)), "成人小比例显示")
			for path in runtime.node_by_rig_path:
				if path == "/10002/Root/Body":
					continue # Legacy 2D projection carrier correction, shared with reference.
				var expected: Transform2D = runtime._sample_property_curves(runtime.animation_tracks[clip].get(path, {}), time, runtime.rest_by_rig_path[path])
				verify(runtime.node_by_rig_path[path].transform.is_equal_approx(expected), "原骨节点必须直接保持导出曲线，不混用旧碧岚缩腿或移肩补丁：" + path)
			if clip == "Run_New1":
				angle_samples.append(runtime.node_by_rig_path[animated_path].rotation)
				var leg_bone: Bone2D = skin.leg_chains.near_leg.bones[0]
				var rest_angle: float = runtime.rest_by_rig_path[animated_path].get_rotation()
				var source_delta := wrapf(runtime.node_by_rig_path[animated_path].rotation - rest_angle, -PI, PI)
				verify(is_equal_approx(wrapf(leg_bone.rotation - rest_angle, -PI, PI), source_delta * 0.32), "袍下腿部视觉骨链跟随原跑步节奏并收敛摆幅")
			var before: Transform2D = runtime.node_by_rig_path[animated_path].transform
			runtime.set_character_pose(clip, time, -1.0)
			verify(runtime.scale.is_equal_approx(Vector2(-0.72, 0.72)) and runtime.node_by_rig_path[animated_path].transform.is_equal_approx(before), "左右朝向仅镜像显示，骨骼相位一致")
	verify(angle_samples.max() - angle_samples.min() > 1.0, "原步幅保留且非静止伪动画")
	var previous_wrists: Dictionary = {}
	var wrist_ranges: Dictionary = {"near_arm": [], "far_arm": []}
	for frame in range(49):
		var time: float = runtime.animation_meta.Run_New1.duration * float(frame) / 48.0
		runtime.set_character_pose("Run_New1", time, 1.0)
		for part_name in skin.arm_chains:
			var data: Dictionary = skin.arm_chains[part_name]
			var chain: Array = data.bones
			var torso: Node2D = runtime.node_by_rig_path["/10002/Root/Body/yifu/yifu/shenti1"]
			verify(chain[0].global_position.distance_to(torso.to_global(data.shoulder)) < 0.001, "整个循环肩点持续缝在衣身肩线")
			var upper: Vector2 = (chain[1].global_position - chain[0].global_position).normalized()
			var lower: Vector2 = (chain[2].global_position - chain[1].global_position).normalized()
			verify(absf(upper.angle_to(lower)) > 0.7 and absf(upper.angle_to(lower)) < 2.1, "跑步双臂持续自然屈肘，不伸直或反折")
			verify(is_equal_approx(chain[2].rotation, runtime.rest_by_rig_path[data.paths[2]].get_rotation()), "跑步腕骨随前臂，去除源立掌手势")
			var wrist: Vector2 = runtime.rig_root_node.to_local(chain[2].global_position)
			if previous_wrists.has(part_name):
				verify(wrist.distance_to(previous_wrists[part_name]) < 6.0, "连续相位腕点无跳变，包含循环接缝")
			previous_wrists[part_name] = wrist
			wrist_ranges[part_name].append(wrist.x)
			runtime.set_character_pose("Run_New1", time, -1.0)
			verify(runtime.rig_root_node.to_local(chain[2].global_position).distance_to(wrist) < 0.001, "左右仅整体镜像，同一袖手同一腕点")
			runtime.set_character_pose("Run_New1", time, 1.0)
	for part_name in wrist_ranges:
		var xs: Array = wrist_ranges[part_name]
		verify(xs.max() - xs.min() > 10.0, "双手有连续前后摆幅而非固定屈肘摆拍")
	for clip in ["Run_New1", "Idle", "AttackIdle", "Step1", "BackstepWalk", "AttackIdle"]:
		runtime.set_character_pose(clip, 0.067, 1.0)
		for mesh in skin.meshes:
			if str(mesh.name) in ["WandererSkin_near_arm", "WandererSkin_far_arm"]:
				verify(mesh.texture == skin.arm_texture, "同一宽袖与可见掌背面贯穿移动、待机、攻击和闪避，保持材质一致")
	runtime.set_character_pose("Run_New1", 0.145, 1.0)
	verify(absf(skin.cloth_chains.front_panel[1].rotation - skin.cloth_chains.back_panel[1].rotation) > 0.02, "前后摆独立响应原衣摆曲线")
	for mode in ["reference", "project", "experiment", "reference", "experiment"]:
		runtime.set_skin_mode(mode)
		for mesh in skin.meshes:
			verify(mesh.visible == (mode == "experiment"), "外观切换不会残留叠层")
	verify(runtime.skeleton.get_bone_count() == 89, "重复切换不累积派生骨或网格")
	if failures.is_empty():
		print("玉虚内门常服专用蒙皮检查通过：", checks, "项；源曲线96相位、屈肘循环49采样含接缝、双向镜像、肩腕连续、权重与外观切换。")
	else:
		printerr("玉虚内门常服蒙皮检查失败：", failures.size(), "/", checks, "项")
	runtime.queue_free()
	quit(0 if failures.is_empty() else 1)
