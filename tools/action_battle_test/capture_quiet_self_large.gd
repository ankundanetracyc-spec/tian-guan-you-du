extends SceneTree

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	root.size = Vector2i(1280, 720)
	var capture_view := SubViewport.new()
	capture_view.size = Vector2i(1280, 720)
	capture_view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(capture_view)
	var scene: Node2D = load("res://capture_self_vfx_arena.gd").new()
	scene.original_wanderer_enabled = true
	scene.original_wanderer_art_folder = "yuxu_fullbody_v15"
	capture_view.add_child(scene)
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_set_size(Vector2i(1280, 720))
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_MINIMIZED)
	scene.set_process(false)
	var output := ProjectSettings.globalize_path("res://../../output/quiet_self_refine_v3_20261104")
	DirAccess.make_dir_recursive_absolute(output)
	for kind in ["催血", "聚炁"]:
		scene.restart_battle()
		scene.player = Vector2(1500, 950)
		scene.enemy = scene.player + Vector2(145, 0)
		scene.enemy_hp = 1000
		scene.enemy_max_hp = 1000
		scene.wolf_state = "recover"
		scene.wolf_clock = 100.0
		scene.facing = 1
		scene._update_trial_camera(0.0, true)
		scene.use_skill(kind)
		for step in range(60):
			var dt := 1.0 / 30.0
			scene.update_strike_fx(dt)
			scene.update_skill_timers(dt)
			scene.motion_time += dt
			scene.visual_motion_delta = dt
			scene.update_skinned_player()
			scene.queue_redraw()
			if scene.battle_hud != null: scene.battle_hud.queue_redraw()
			await process_frame
			RenderingServer.force_draw(false)
			var shot := capture_view.get_texture().get_image()
			assert(shot.save_png(output.path_join("%s_%02d.png" % ["blood" if kind == "催血" else "qi", step])) == OK)
	# Inspect the opposite-facing hand attachment and wrapping at the same scale.
	for kind in ["催血", "聚炁"]:
		scene.restart_battle()
		scene.player = Vector2(1500, 950)
		scene.enemy = scene.player - Vector2(145, 0)
		scene.facing = -1
		scene._update_trial_camera(0.0, true)
		scene.use_skill(kind)
		scene.update_skill_timers(0.6)
		scene.update_skinned_player()
		scene.queue_redraw()
		await process_frame
		RenderingServer.force_draw(false)
		var shot := capture_view.get_texture().get_image()
		assert(shot.save_png(output.path_join("left_%s.png" % ("blood" if kind == "催血" else "qi"))) == OK)
	print("QUIET_SELF_REFINED_CAPTURE 120 motion frames, 2 left-facing checks")
	scene.queue_free()
	await process_frame
	quit()
