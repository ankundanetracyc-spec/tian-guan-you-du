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
	await get_tree().process_frame
	var output_name := "yuxu_natural_v16_arena"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output_name = argument.trim_prefix("--output=")
	var output := ProjectSettings.globalize_path("res://output/"+output_name)
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
	var journal := FileAccess.open(output.path_join("frames.jsonl"), FileAccess.WRITE)
	var intended_facing := 1.0
	var label := "idle"
	for frame in range(360):
		match frame:
			24:
				battle.replay_direction = Vector2.RIGHT
				label = "start_run_right"
			66:
				battle.replay_direction = Vector2(1,-1).normalized()
				label = "diagonal_up"
			90:
				battle.replay_direction = Vector2.LEFT
				intended_facing = -1.0
				label = "turn_run_left"
			126:
				battle.replay_direction = Vector2.ZERO
				label = "stop"
			144: label = "standing_strike"
			168:
				battle.replay_direction = Vector2.RIGHT
				label = "backward_right"
			204:
				intended_facing = 1.0
				label = "running_strike"
			240:
				battle.replay_direction = Vector2.ZERO
				label = "backward_dash"
			264:
				battle.replay_direction = Vector2(-1,1).normalized()
				intended_facing = -1.0
				label = "diagonal_down"
			306:
				battle.replay_direction = Vector2.ZERO
				label = "settle_idle"
		# Map the requested world aim into native window coordinates, including
		# camera and stretch. Hard-coded cursor pixels mislabelled older replays.
		var aim_world: Vector2 = battle.player+Vector2(intended_facing*260,0)
		var aim_view: Vector2 = get_viewport().get_canvas_transform()*aim_world
		var aim_window: Vector2 = get_viewport().get_screen_transform()*aim_view
		Input.warp_mouse(aim_window)
		await get_tree().process_frame
		if frame in [144,160,208,222]: battle.use_skill("拳掌")
		if frame == 240: battle.use_skill("错步")
		if frame == 318:
			battle.use_skill("架招")
			label = "guard_then_settle"
		battle._process(1.0/30.0)
		var selected: Array = battle._current_player_animation()
		rig.wanderer_skin.set_motion_context(battle.visual_motion_delta,battle.visual_motion_velocity,battle.motion_time,battle.facing)
		rig.wanderer_skin.set_action_context(battle.guard_window > 0.0, not battle.pending_attack.is_empty())
		rig.set_character_pose(str(selected[0]),float(selected[1]),battle.facing)
		rig.scale *= 4.0
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var screenshot := get_viewport().get_texture().get_image()
		screenshot.resize(1280,720,Image.INTERPOLATE_BILINEAR)
		assert(screenshot.save_jpg(output.path_join("frame_%04d.jpg" % frame),0.96)==OK)
		assert(detail.get_texture().get_image().save_png(output.path_join("detail_%04d.png" % frame))==OK)
		var record := {"frame":frame,"label":label,"time":float(frame)/30.0,"player":[battle.player.x,battle.player.y],"facing":battle.facing,"expected_facing":intended_facing,"clip":selected[0],"phase":selected[1],"backward":battle.moving_backward,"stamina":battle.stamina,"mouse":[battle.get_global_mouse_position().x,battle.get_global_mouse_position().y]}
		var skin = battle.original_wanderer.wanderer_skin
		if skin.has_method("has_contact_shadow"):
			record.gait_phase = skin.gait_phase
			record.move_speed = battle.move_speed
			record.cloth_angles = [skin.bones.front_cloth.rotation,skin.bones.front_hem.rotation,skin.bones.front_edge.rotation,skin.bones.back_cloth.rotation,skin.bones.back_hem.rotation,skin.bones.back_edge.rotation]
			record.torso_degrees = rad_to_deg(skin.bones.torso.rotation)
			record.feet = {}
			for side in ["near","far"]:
				var lowest := -INF
				for sole_point in skin.sole_profiles[side]:
					lowest = maxf(lowest,battle.original_wanderer.rig_root_node.to_local(skin.bones[side+"_foot"].to_global(sole_point)).y)
				record.feet[side] = {"sole_y":lowest,"lift":skin.ground_contacts[side].lift,"shadow_x":skin.ground_contacts[side].center.x,"world_ankle_x":skin.bones[side+"_foot"].global_position.x}
		journal.store_line(JSON.stringify(record))
		journal.flush()
		if frame%60 == 0: print("NATURAL ARENA ",frame," / 360; face=",battle.facing," expected=",intended_facing)
	journal.close()
	print("NATURAL ARENA COMPLETE: 360 actual rendered frames, 12 seconds of simulation")
	get_tree().quit()
