extends Node2D

## Deterministic visual-only replay through the real side-view battle update.
## Both detail rigs receive the same input; LegacyRig preserves the old split look.
class ReplayBattle extends "action_battle_feel_test.gd":
	var replay_direction := Vector2.ZERO
	var driver_facing := 1.0
	func movement_direction() -> Vector2:
		return replay_direction
	func update_skinned_player():
		# Native window mouse state is intentionally outside this replay. Set
		# visual aim before the unchanged parent advances the skin exactly once.
		facing = driver_facing
		moving_backward = replay_direction.x * facing < -0.1
		super.update_skinned_player()
	func _enter_trial_fullscreen(_fullscreen := true) -> void:
		# Capture owns its separate render window and keeps the user's editor alone.
		super._enter_trial_fullscreen(false)

class LegacyRig extends "guigu_rig_runtime.gd":
	func load_runtime(rig_path: String, skins_path: String, animations_path: String, art_root: String, art_folder: String = "yuxu_fullbody_v15") -> bool:
		custom_art_folder = art_folder
		load_errors.clear()
		var rig_data: Variant = _read_json(rig_path)
		var skins_data: Variant = _read_json(skins_path)
		var animations_data: Variant = _read_json(animations_path)
		if not rig_data is Dictionary or not skins_data is Dictionary or not animations_data is Dictionary:
			return false
		if not _build_character_hierarchy(rig_data): return false
		if not _load_texture_sets(art_root): return false
		if not _build_weighted_meshes(skins_data, art_root): return false
		wanderer_skin = load("res://yuxu_approved_skin.gd").new()
		bone_bound_art_ready = wanderer_skin.build(self, art_root.path_join(custom_art_folder))
		_load_animation_tracks(animations_data)
		loaded = not mesh_nodes.is_empty() or bone_bound_art_ready
		return loaded

func _make_detail(rig) -> SubViewport:
	var detail := SubViewport.new()
	detail.size = Vector2i(440, 500)
	detail.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(detail)
	var backdrop := ColorRect.new()
	backdrop.color = Color("58646a")
	backdrop.size = Vector2(440, 500)
	detail.add_child(backdrop)
	detail.add_child(rig)
	assert(rig.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art", "yuxu_fullbody_v15"))
	rig.set_skin_mode("experiment")
	rig.position = Vector2(220, 440)
	return detail

func _pose_detail(rig, battle, selected: Array) -> void:
	rig.wanderer_skin.set_motion_context(battle.visual_motion_delta, battle.visual_motion_velocity, battle.motion_time, battle.facing)
	rig.wanderer_skin.set_action_context(battle.guard_window > 0.0, not battle.pending_attack.is_empty())
	rig.set_character_pose(str(selected[0]), float(selected[1]), battle.facing)
	rig.scale *= 4.0

func _ready() -> void:
	var output_name := "realtime_closed_robe_20261104"
	var probe_only := OS.get_cmdline_user_args().has("--probe-only")
	var orientation := -1.0 if OS.get_cmdline_user_args().has("--mirror") else 1.0
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output_name = argument.trim_prefix("--output=")
	var output := ProjectSettings.globalize_path("res://output/" + output_name)
	DirAccess.make_dir_recursive_absolute(output)
	var battle := ReplayBattle.new()
	battle.original_wanderer_enabled = true
	battle.original_wanderer_art_folder = "yuxu_fullbody_v15"
	add_child(battle)
	battle.set_process(false)
	battle.set_process_input(false)
	battle.player = Vector2(1500, 950)
	battle.enemy = Vector2(2700, 950)
	battle._update_trial_camera(0.0, true)
	var current_rig = load("res://guigu_rig_runtime.gd").new()
	var legacy_rig := LegacyRig.new()
	var current_detail := _make_detail(current_rig)
	var legacy_detail := _make_detail(legacy_rig)
	await get_tree().process_frame
	var journal := FileAccess.open(output.path_join("frames.jsonl"), FileAccess.WRITE)
	var intended_facing := orientation
	var label := "idle"
	var total_frames := 224 if probe_only else 360
	for frame in range(total_frames):
		# The opponent stays out of range, preserving the live movement/skill path.
		battle.enemy = Vector2(2700, 950)
		match frame:
			18:
				battle.replay_direction = Vector2.RIGHT * orientation
				label = "start_forward"
			48:
				battle.replay_direction = Vector2(orientation, -1).normalized()
				label = "diagonal_up"
			66:
				battle.replay_direction = Vector2.LEFT * orientation
				intended_facing = -orientation
				label = "turn_left"
			96:
				battle.replay_direction = Vector2.ZERO
				label = "stop_left"
			126: label = "standing_strike"
			150:
				battle.replay_direction = Vector2.RIGHT * orientation
				label = "backward_right"
			198:
				intended_facing = orientation
				label = "turn_forward_right"
			228:
				battle.replay_direction = Vector2.ZERO
				label = "stop_settle"
			288:
				battle.configuring = true
				label = "pause_freeze"
			300:
				battle.configuring = false
				label = "resume_settle"
		battle.driver_facing = intended_facing
		await get_tree().process_frame
		if frame in [126, 140, 208]:
			battle.use_skill("拳掌")
			battle.attack_direction = Vector2(intended_facing, 0)
		if frame == 216:
			battle.use_skill("错步")
			label = "forward_dash"
		battle._process(1.0 / 30.0)
		if battle.facing != intended_facing:
			push_error("CAPTURE AIM MISMATCH frame=%d expected=%s actual=%s" % [frame, intended_facing, battle.facing])
			get_tree().quit(2)
			return
		var selected: Array = battle._current_player_animation()
		_pose_detail(current_rig, battle, selected)
		_pose_detail(legacy_rig, battle, selected)
		if probe_only and frame not in [0, 30, 74, 156, 163, 170, 216, 217, 219, 221, 223]: continue
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var screenshot := get_viewport().get_texture().get_image()
		screenshot.resize(1280, 720, Image.INTERPOLATE_BILINEAR)
		assert(screenshot.save_jpg(output.path_join("arena_%04d.jpg" % frame), 0.94) == OK)
		assert(current_detail.get_texture().get_image().save_png(output.path_join("current_%04d.png" % frame)) == OK)
		assert(legacy_detail.get_texture().get_image().save_png(output.path_join("legacy_%04d.png" % frame)) == OK)
		var skin = battle.original_wanderer.wanderer_skin
		var record := {"frame": frame, "seconds": (frame + 1) / 30.0, "label": label,
			"player": [battle.player.x, battle.player.y], "facing": battle.facing, "expected_facing": intended_facing,
			"clip": selected[0], "phase": selected[1], "backward": battle.moving_backward,
			"delta": battle.visual_motion_delta, "clock": battle.motion_time,
			"velocity": [battle.visual_motion_velocity.x, battle.visual_motion_velocity.y]}
		if skin.has_method("get_garment_diagnostics"):
			record.garment = skin.get_garment_diagnostics()
			for key in record.garment:
				var value: Variant = record.garment[key]
				if value is Vector3: record.garment[key] = [value.x, value.y, value.z]
				elif value is Vector2: record.garment[key] = [value.x, value.y]
		record.contacts = {}
		for side in ["near", "far"]:
			var contact: Dictionary = skin.ground_contacts[side]
			record.contacts[side] = {"center": [contact.center.x, contact.center.y], "lift": contact.lift, "pitch": contact.pitch}
		record.bones = {}
		for key in skin.bones:
			var bone: Bone2D = skin.bones[key]
			record.bones[key] = [bone.position.x, bone.position.y, bone.rotation]
		journal.store_line(JSON.stringify(record))
		journal.flush()
		if frame % 60 == 0: print("CLOSED ROBE CAPTURE ", frame, " / 360; ", label)
	journal.close()
	print("CLOSED ROBE COMPLETE: ", total_frames, " simulated frames; probe_only=", probe_only, "; ", output)
	get_tree().quit()
