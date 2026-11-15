extends Node2D

var battle
var capture_prefix := "yuxu_profile_v7_full"
const REVIEW_PHASES := {
	"idle": [0.0, 0.408, 0.817, 1.225],
	"run": [0.03, 0.145, 0.267, 0.385, 0.50],
	"attack": [0.02, 0.067, 0.13],
	"step": [0.03, 0.083, 0.14],
}


func _ready() -> void:
	var scene_path := "res://ActionBattleFeelTest.tscn"
	if not ResourceLoader.exists(scene_path):
		scene_path = "res://tools/action_battle_test/ActionBattleFeelTest.tscn"
	var packed: PackedScene = load(scene_path)
	if packed == null:
		push_error("无法加载动作试玩场景：%s" % scene_path)
		get_tree().quit(1)
		return
	battle = packed.instantiate()
	add_child(battle)
	await get_tree().process_frame
	battle.set_process(false)
	battle.set_physics_process(false)
	battle.player = Vector2(400, 500)
	battle.enemy = Vector2(1000, 400)
	battle.hp = 100
	battle.enemy_hp = 100
	battle.facing = 1.0
	battle.original_wanderer_enabled = true
	var runtime_script = load(battle._action_test_path("guigu_rig_runtime.gd"))
	battle.original_wanderer = runtime_script.new()
	battle.add_child(battle.original_wanderer)
	var art_root = battle._action_test_path("art/own_guigu_fit/manifest.json").get_base_dir().get_base_dir()
	assert(battle.original_wanderer.load_runtime(battle._action_test_path("guigu_rig/rig.json"), battle._action_test_path("guigu_rig/skins.json"), battle._action_test_path("guigu_rig/animations.json"), art_root, "original_wanderer"))
	battle.original_wanderer.set_skin_mode("experiment")
	print("原创服装专用蒙皮：", battle.original_wanderer.get_runtime_summary())
	for action_name in REVIEW_PHASES:
		for phase in REVIEW_PHASES[action_name]:
			await _capture_pose(str(action_name), float(phase))
	battle.facing = -1.0
	for action_name in REVIEW_PHASES:
		for phase in REVIEW_PHASES[action_name]:
			await _capture_pose(str(action_name), float(phase))

	get_tree().quit()


func _capture_cycle(time: float, frame: int) -> void:
	battle.walk_amount = 1.0
	battle.motion_time = time
	battle.visual_attack_time = -1.0
	battle.dash_move = 0.0
	battle.update_skinned_player()
	battle.queue_redraw()
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	var output_dir := ProjectSettings.globalize_path("res://output")
	var crop := image.get_region(Rect2i(340, 382, 120, 140))
	crop.save_png(output_dir.path_join("%s_cycle_%s_%02d.png" % [capture_prefix, "left" if battle.facing < 0 else "right", frame]))


func _capture_pose(action_name: String, time: float) -> void:
	battle.walk_amount = 1.0 if action_name == "run" else 0.0
	battle.motion_time = time
	battle.visual_attack_time = time if action_name == "attack" else -1.0
	battle.dash_move = maxf(0.18 - time, 0.0) if action_name == "step" else 0.0
	battle.attack_effect = 0.0
	battle.update_skinned_player()
	battle.queue_redraw()
	if action_name == "run":
		for side in ["youtui", "zuotui"]:
			var leg_path := "/10002/Root/Body/Foot/%s/%s/tui1" % [side, side]
			var leg: Node2D = battle.original_wanderer.node_by_rig_path.get(leg_path)
			if leg != null:
				print("leg sample ", time, " ", side, " delta=", wrapf(leg.rotation - battle.original_wanderer.rest_by_rig_path[leg_path].get_rotation(), -PI, PI))
		for path in [
			"/10002/Root/Body/yifu/yifu/shenti2",
			"/10002/Root/Body/yifu/yifu/shenti2/shenti2_1",
			"/10002/Root/Body/yifu/yifu/shenti2/shenti2_1/shenti2_2",
		]:
			var node: Node2D = battle.original_wanderer.node_by_rig_path.get(path)
			if node != null:
				print("cloth bone sample ", time, " ", path.get_file(), " angle=", node.rotation, " rest=", battle.original_wanderer.rest_by_rig_path[path].get_rotation())
	await get_tree().process_frame
	await get_tree().create_timer(0.12).timeout
	var image := get_viewport().get_texture().get_image()
	var label := "standalone" if ResourceLoader.exists("res://ActionBattleFeelTest.tscn") else "main_menu"
	var output_dir := ProjectSettings.globalize_path("res://output")
	DirAccess.make_dir_recursive_absolute(output_dir)
	var tag := "%s_t%03d%s" % [action_name, roundi(time * 1000.0), "_left" if battle.facing < 0.0 else ""]
	var output_path := output_dir.path_join("%s_%s_review_%s.png" % [capture_prefix, label, tag])
	var error := image.save_png(output_path)
	if error != OK:
		push_error("截图保存失败：%s (%s)" % [output_path, error_string(error)])
		return
	var closeup := image.get_region(Rect2i(300, 310, 200, 245))
	closeup.resize(600, 735, Image.INTERPOLATE_CUBIC)
	var closeup_path := output_dir.path_join("%s_%s_review_%s_closeup.png" % [capture_prefix, label, tag])
	if closeup.save_png(closeup_path) != OK:
		push_error("近景截图保存失败：%s" % closeup_path)
	else:
		print("实机动作采样：", action_name, " t=", time, " -> ", output_path, " | ", closeup_path)
