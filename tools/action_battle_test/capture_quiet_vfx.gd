extends SceneTree

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	root.size = Vector2i(1280, 720)
	var scene: Node2D = load("res://ActionBattleFeelTest.tscn").instantiate()
	scene.original_wanderer_enabled = true
	scene.original_wanderer_art_folder = "yuxu_fullbody_v15"
	root.add_child(scene)
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_set_size(Vector2i(1280, 720))
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	scene.set_process(false)
	var output := ProjectSettings.globalize_path("res://../../output/quiet_vfx_20261104")
	DirAccess.make_dir_recursive_absolute(output)
	var frame_number := 0
	var stills_only := "--stills" in OS.get_cmdline_user_args()
	var cases := ["拳掌", "拳掌_掌", "痛击", "催血", "聚炁", "聚炁_周天", "聚炁_中断", "拳掌_左"]
	for case_index in range(cases.size()):
		scene.restart_battle()
		scene.player = Vector2(1500, 950)
		var direction := Vector2.LEFT if case_index == 7 else Vector2.RIGHT
		scene.enemy = scene.player + direction * (56 if case_index < 3 or case_index == 7 else 145)
		scene.enemy_hp = 1000
		scene.enemy_max_hp = 1000
		scene.wolf_state = "recover"
		scene.wolf_clock = 100.0
		scene.mana = 1
		scene.qi = 4 if case_index == 5 else 0
		scene.facing = direction.x
		scene._update_trial_camera(0.0, true)
		var is_attack := case_index < 3 or case_index == 7
		var skill: String = "拳掌" if case_index in [0, 1, 7] else ("痛击" if case_index == 2 else ("催血" if case_index == 3 else "聚炁"))
		scene.use_skill(skill)
		scene.attack_direction = direction
		for step in range((11 if is_attack else 23) if stills_only else (30 if is_attack else 49)):
			var dt := 1.0 / 30.0
			scene.update_strike_fx(dt)
			scene.update_guard_fx(dt)
			scene.update_combat_texts(dt)
			scene.update_skill_timers(dt)
			if is_attack and not scene.strike_fx.is_empty():
				if case_index in [0, 7]: scene.strike_fx[0].visual_key = "拳掌"
				if case_index == 1: scene.strike_fx[0].visual_key = "拳掌_掌"
			if case_index == 6 and step == 15:
				scene.use_skill("错步")
				scene.dash_move = 0.0
				scene.invulnerable = 0.0
				scene.player += Vector2(-45, 0)
				scene._update_trial_camera(0.0, true)
			scene.visual_attack_time = step * dt if is_attack and step < 5 else -1.0
			scene.motion_time += dt
			scene.visual_motion_delta = dt
			scene.update_skinned_player()
			scene.queue_redraw()
			scene.battle_hud.queue_redraw()
			await process_frame
			RenderingServer.force_draw(false)
			var shot := root.get_texture().get_image()
			if not stills_only: assert(shot.save_png(output.path_join("motion_%04d.png" % frame_number)) == OK)
			if step == ((9 if case_index == 2 else 5) if is_attack else 21):
				assert(shot.save_png(output.path_join("case_%d.png" % case_index)) == OK)
				print("CAPTURE_GEOMETRY case=", case_index, " player=", scene.player, " enemy=", scene.enemy, " zoom=", scene.trial_camera.zoom, " screen=", scene.get_global_transform_with_canvas(), " effects=", scene.strike_fx)
			frame_number += 1
	print("QUIET_VFX_CAPTURE ", frame_number, " actual rendered frames")
	scene.queue_free()
	await process_frame
	quit()
