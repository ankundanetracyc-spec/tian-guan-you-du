extends SceneTree

func _initialize() -> void:
	call_deferred("_capture")

func _capture() -> void:
	var prefix := "res://tools/action_battle_test/" if FileAccess.file_exists("res://tools/action_battle_test/action_battle_feel_test.gd") else "res://"
	var battle = load(prefix + "action_battle_feel_test.gd").new()
	battle.original_wanderer_enabled = true
	battle.original_wanderer_art_folder = "yuxu_fullbody_v15"
	root.add_child(battle)
	battle.set_process(false)
	battle.set_process_input(false)
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1280, 720)
	root.content_scale_size = Vector2i(1280, 720)
	battle.player = Vector2(1500, 950)
	battle.enemy = Vector2(1660, 950)
	battle.wolf_facing = -1.0
	battle.update_skinned_player()
	battle.update_skinned_wolf(0.0)
	battle._update_trial_camera(0.0, true)
	assert(battle.skinned_wolf != null)
	for i in range(3):
		await process_frame
		await RenderingServer.frame_post_draw
	var output := ProjectSettings.globalize_path(prefix + "../../output/forest_wolf_small_bones_20261104").simplify_path()
	DirAccess.make_dir_recursive_absolute(output)
	assert(root.get_texture().get_image().save_png(output.path_join("wolf_actual_arena.png")) == OK)
	print("WOLF LIVE ARENA captured ", battle.skinned_wolf.get_runtime_summary())
	battle.queue_free()
	await process_frame
	quit()
