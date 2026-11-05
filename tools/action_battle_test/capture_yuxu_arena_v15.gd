extends Node2D

class ReplayBattle extends "action_battle_feel_test.gd":
	var replay_direction := Vector2.ZERO
	func movement_direction() -> Vector2:
		return replay_direction

func _ready() -> void:
	var battle := ReplayBattle.new()
	battle.original_wanderer_enabled = true
	battle.original_wanderer_art_folder = "yuxu_fullbody_v15"
	add_child(battle)
	battle.set_process(false)
	battle.set_process_input(false)
	battle.player = Vector2(1500, 950)
	battle.enemy = Vector2(2350, 970)
	battle._update_trial_camera()
	var output := ProjectSettings.globalize_path("res://output/yuxu_v15_arena")
	DirAccess.make_dir_recursive_absolute(output)
	var records: Array = []
	var journal := FileAccess.open(output.path_join("frames.jsonl"), FileAccess.WRITE)
	var face_right := true
	for frame in range(240):
		match frame:
			24: battle.replay_direction = Vector2.RIGHT
			84:
				battle.replay_direction = Vector2.LEFT
				face_right = false
			126: face_right = true
			156: battle.replay_direction = Vector2.ZERO
			184:
				battle.replay_direction = Vector2.RIGHT
			218: battle.replay_direction = Vector2.ZERO
		Input.warp_mouse(Vector2(970 if face_right else 310,350))
		if frame in [160,174]: battle.use_skill("平击")
		if frame==184: battle.use_skill("错步")
		# Deterministic input replay through the unchanged real battle update:
		# movement, wolf AI, resources, camera, HUD and animation all run here.
		battle._process(1.0/30.0)
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var screenshot := get_viewport().get_texture().get_image()
		screenshot.resize(1280,720,Image.INTERPOLATE_BILINEAR)
		assert(screenshot.save_jpg(output.path_join("frame_%04d.jpg" % frame),0.96)==OK)
		var selected: Array=battle._current_player_animation()
		var record := {"frame":frame,"simulation_time":float(frame)/30.0,"player":[battle.player.x,battle.player.y],"facing":battle.facing,"clip":selected[0],"phase":selected[1],"backward":battle.moving_backward,"stamina":battle.stamina}
		records.append(record)
		journal.store_line(JSON.stringify(record))
		journal.flush()
		if frame % 60 == 0: print("ARENA V15 FRAME ",frame," / 240")
	journal.close()
	var file := FileAccess.open(output.path_join("frames.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(records,"  "))
	file.close()
	print("ARENA V15 COMPLETE: 240 actual native viewport frames / 8 simulated seconds")
	get_tree().quit()
