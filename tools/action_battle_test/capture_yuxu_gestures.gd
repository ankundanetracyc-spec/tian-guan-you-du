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
	battle.player = Vector2(1500,950)
	battle.enemy = Vector2(2700,950)
	battle._update_trial_camera()
	await get_tree().process_frame
	var output := ProjectSettings.globalize_path("res://output/yuxu_gestures_v21")
	DirAccess.make_dir_recursive_absolute(output)
	var detail := SubViewport.new()
	detail.size = Vector2i(440,500)
	detail.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(detail)
	var backdrop := ColorRect.new()
	backdrop.color = Color("58646a")
	backdrop.size = Vector2(440,500)
	detail.add_child(backdrop)
	var rig = load("res://guigu_rig_runtime.gd").new()
	detail.add_child(rig)
	assert(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"))
	rig.set_skin_mode("experiment")
	rig.position = Vector2(220,440)
	var journal := FileAccess.open(output.path_join("frames.jsonl"),FileAccess.WRITE)
	# 26 seconds: the entire long-idle gesture, then interruption by walking.
	# 20 fps capture uses one 50ms simulation step; no timer is forced forward.
	for frame in range(520):
		# Keep the test opponent out of range without ending combat, so the
		# actual player movement/skill path remains enabled throughout.
		battle.enemy = Vector2(2700,950)
		if frame==480: battle.replay_direction=Vector2.RIGHT
		var aim_world: Vector2=battle.player+Vector2(260,0)
		var aim_window: Vector2=get_viewport().get_screen_transform()*(get_viewport().get_canvas_transform()*aim_world)
		Input.warp_mouse(aim_window)
		await get_tree().process_frame
		battle._process(0.05)
		var selected: Array=battle._current_player_animation()
		rig.wanderer_skin.set_motion_context(battle.visual_motion_delta,battle.visual_motion_velocity,battle.motion_time,battle.facing)
		rig.wanderer_skin.set_action_context(battle.guard_window>0.0,not battle.pending_attack.is_empty())
		rig.set_character_pose(str(selected[0]),float(selected[1]),battle.facing)
		rig.scale*=4.0
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var shot:=get_viewport().get_texture().get_image()
		shot.resize(1280,720,Image.INTERPOLATE_BILINEAR)
		shot.save_jpg(output.path_join("arena_%04d.jpg"%frame),0.92)
		detail.get_texture().get_image().save_png(output.path_join("detail_%04d.png"%frame))
		var skin=battle.original_wanderer.wanderer_skin
		journal.store_line(JSON.stringify({"frame":frame,"seconds":(frame+1)*0.05,"clip":selected[0],"idle_age":skin.idle_gesture_age,"waist":skin.gesture_waist,"focus":skin.gesture_focus,"ready":skin.gesture_ready,"facing":battle.facing}))
	journal.close()
	print("YUXU GESTURE CAPTURE: 520 frames; 26 seconds; ",output)
	get_tree().quit()
