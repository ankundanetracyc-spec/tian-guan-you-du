extends SceneTree

func _initialize() -> void:
	call_deferred("inspect")

func inspect() -> void:
	var runtime = load("res://guigu_rig_runtime.gd").new()
	root.add_child(runtime)
	runtime.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art")
	for path in runtime.node_by_rig_path:
		if path.ends_with("/shenti1") or path.ends_with("/tou") or path.ends_with("/tui1") or path.ends_with("/tui2") or path.ends_with("/tui3") or path.ends_with("/youbi1") or path.ends_with("/youbi2") or path.ends_with("/youbi3") or path.ends_with("/zuobi1") or path.ends_with("/zuobi2") or path.ends_with("/zuobi3") or path.ends_with("/shenti2") or path.ends_with("/shenti2_1") or path.ends_with("/shenti2_2"):
			var bone: Node2D = runtime.node_by_rig_path[path]
			print(path, " ROOT POS=", runtime.rig_root_node.to_local(bone.global_position), " ANGLE=", rad_to_deg(bone.global_rotation))
	for clip in ["Idle", "Run_New1", "AttackIdle", "Step1"]:
		print("CLIP ", clip, " ", runtime.animation_meta[clip])
		for t in [0.0, 0.145, 0.267]:
			runtime.set_character_pose(clip, t, 1.0)
			for path in ["/10002/Root/Body", "/10002/Root/Body/yifu/yifu/shenti1", "/10002/Root/Body/yifu/yifu/shenti1/tou", "/10002/Root/Body/Foot/zuotui/zuotui/tui1", "/10002/Root/Body/Foot/zuotui/zuotui/tui1/tui2/tui3"]:
				var bone: Node2D = runtime.node_by_rig_path[path]
				print(t, " ", path.get_file(), " pos=", runtime.rig_root_node.to_local(bone.global_position), " rotation=", rad_to_deg(bone.rotation))
	quit()
