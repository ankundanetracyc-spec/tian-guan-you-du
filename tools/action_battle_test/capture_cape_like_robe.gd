extends "capture_leg_supported_robe.gd"

## Current visual target: a hanging/trailing rear robe, original shoes and
## standing outline. This independent scene never changes the game importer.
class CapeReplayBattle extends "action_battle_feel_test.gd":
	var replay_direction := Vector2.ZERO
	var driver_facing := 1.0
	func movement_direction() -> Vector2:
		return replay_direction
	func update_skinned_player():
		facing = driver_facing
		moving_backward = replay_direction.x*facing < -0.1
		super.update_skinned_player()
	func _enter_trial_fullscreen(_fullscreen := true) -> void:
		super._enter_trial_fullscreen(false)
	func _notification(_what: int) -> void:
		# Desktop focus belongs to the person using the editor. This isolated
		# replay keeps its scripted key stream; game focus-loss is tested apart.
		pass

class CapeRig extends "guigu_rig_runtime.gd":
	var comparison_mode := "new"
	func load_runtime(rig_path: String, skins_path: String, animations_path: String, art_root: String, art_folder: String = "yuxu_fullbody_v15") -> bool:
		custom_art_folder = art_folder
		load_errors.clear()
		var rig_data: Variant = _read_json(rig_path)
		var skins_data: Variant = _read_json(skins_path)
		var animations_data: Variant = _read_json(animations_path)
		if not rig_data is Dictionary or not skins_data is Dictionary or not animations_data is Dictionary: return false
		if not _build_character_hierarchy(rig_data): return false
		if not _load_texture_sets(art_root): return false
		if not _build_weighted_meshes(skins_data, art_root): return false
		wanderer_skin = load("res://yuxu_approved_skin.gd" if comparison_mode == "old" else "res://cape_like_robe_skin.gd").new()
		bone_bound_art_ready = wanderer_skin.build(self, art_root.path_join(custom_art_folder))
		_load_animation_tracks(animations_data)
		loaded = bone_bound_art_ready
		return loaded

const CAPE_PROBE_FRAMES := [0,12,30,36,42,48,53,54,72,78,84,90,95,96,114,120,126,132,137,138,156,162,168,174,179,180,181,183,186,192,203,204,205,207,210,215,216,221,227,228,234,246,270,299]
const ARENA_FRAMES := [0,42,84,126,168,186,210,246,299]

func _make_detail(mode: String, overlay: Node, index: int) -> Dictionary:
	var detail := SubViewport.new()
	detail.size = Vector2i(440,500)
	detail.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(detail)
	var backdrop := ColorRect.new()
	backdrop.color = Color("58646a")
	backdrop.size = Vector2(440,500)
	detail.add_child(backdrop)
	var rig := CapeRig.new()
	rig.comparison_mode = mode
	detail.add_child(rig)
	assert(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"))
	rig.set_skin_mode("experiment")
	rig.position = Vector2(220,440)
	var display := TextureRect.new()
	display.texture = detail.get_texture()
	display.position = Vector2(index*440,48)
	display.size = Vector2(440,500)
	overlay.add_child(display)
	_label(overlay,"原版 · 原袍长与露鞋" if mode=="old" else "新 · 后摆垂坠与惯性",Vector2(index*440+22,9),25)
	return {"rig":rig,"viewport":detail,"mode":mode}

func _record_rig(rig) -> Dictionary:
	var result := super._record_rig(rig)
	var skin = rig.wanderer_skin
	result.arm_bones = {}
	for key in ["near_upper","near_forearm","near_hand","far_upper","far_forearm","far_hand"]:
		var bone: Bone2D = skin.bones[key]
		result.arm_bones[key] = {"position":_serial(rig.rig_root_node.to_local(bone.global_position)),"rotation":bone.rotation}
	result.gait = {"phase":skin.gait_phase,"amount":skin.gait_amount,"backward":skin.gait_backward,"horizontal":skin.gait_horizontal,
		"actual_speed":skin.previous_velocity.length(),"sample_forward":_serial(skin._foot_sample(0.0,0.0)),"sample_back":_serial(skin._foot_sample(0.5,0.0)),"sample_lift":_serial(skin._foot_sample(0.75,0.0))}
	return result

func _feed_key(battle, key: int, pressed: bool, seconds: float) -> Dictionary:
	var event := InputEventKey.new()
	event.physical_keycode = key
	event.keycode = key
	event.pressed = pressed
	event.echo = false
	battle.fast_step.feed_key(event,seconds)
	return {"key":key,"pressed":pressed,"seconds":seconds}

func _double_tap(battle, key: int, seconds: float) -> Array:
	# The real helper receives release/press/release/press. Base speed remains
	# unchanged; actual movement and continuous stamina use the live battle path.
	return [_feed_key(battle,key,false,seconds-0.21),_feed_key(battle,key,true,seconds-0.20),
		_feed_key(battle,key,false,seconds-0.10),_feed_key(battle,key,true,seconds)]

func _ready() -> void:
	ui_font.font_names = PackedStringArray(["Microsoft YaHei"])
	var args := OS.get_cmdline_user_args()
	var probe_only := args.has("--probe-only")
	var orientation := -1.0 if args.has("--mirror") else 1.0
	var output_name := "cape_like_robe_open_probe" if probe_only else "cape_like_robe_open_full"
	for argument in args:
		if argument.begins_with("--output="): output_name = argument.trim_prefix("--output=")
	var output := ProjectSettings.globalize_path("res://output/"+output_name)
	DirAccess.make_dir_recursive_absolute(output)
	var battle := CapeReplayBattle.new()
	battle.original_wanderer_enabled = true
	battle.original_wanderer_art_folder = "yuxu_fullbody_v15"
	add_child(battle)
	battle.set_process(false)
	battle.set_process_input(false)
	battle.fast_step_enabled = true
	battle.ensure_fast_step()
	battle.player = Vector2(1500,950)
	battle.enemy = Vector2(3100,950)
	battle._update_trial_camera(0.0,true)
	var retired_rig = battle.original_wanderer
	retired_rig.queue_free()
	var arena_rig := CapeRig.new()
	battle.add_child(arena_rig)
	assert(arena_rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"))
	arena_rig.set_skin_mode("experiment")
	battle.original_wanderer = arena_rig
	get_window().size = Vector2i(880,600)
	get_window().content_scale_size = Vector2i(880,600)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	var overlay := CanvasLayer.new()
	overlay.layer = 100
	add_child(overlay)
	var panel := ColorRect.new()
	panel.color = Color("424d53")
	panel.size = Vector2(880,600)
	overlay.add_child(panel)
	var details := [_make_detail("old",overlay,0),_make_detail("new",overlay,1)]
	var phase_label := _label(overlay,"",Vector2(18,551),21)
	var status_label := _label(overlay,"同输入 · 实际位移与双击快步",Vector2(18,578),15)
	await get_tree().process_frame
	var journal := FileAccess.open(output.path_join("frames.jsonl"),FileAccess.WRITE)
	var intended_facing := orientation
	var forward_key := KEY_D if orientation>0.0 else KEY_A
	var backward_key := KEY_A if orientation>0.0 else KEY_D
	var phase_name := "原样静站"
	for frame in range(300):
		battle.enemy = Vector2(3100,950)
		var input_events := []
		match frame:
			12:
				battle.move_speed = 114.0
				battle.replay_direction = Vector2.RIGHT*orientation
				input_events.append(_feed_key(battle,forward_key,true,frame/30.0))
				phase_name = "样例慢步 · 114"
			54:
				battle.move_speed = 114.0
				input_events.append_array(_double_tap(battle,forward_key,frame/30.0))
				phase_name = "样例快步 · 171"
			96:
				battle.move_speed = 235.0
				input_events.append(_feed_key(battle,forward_key,false,frame/30.0))
				input_events.append(_feed_key(battle,forward_key,true,frame/30.0))
				phase_name = "角色慢步 · 235"
			138:
				battle.move_speed = 235.0
				input_events.append_array(_double_tap(battle,forward_key,frame/30.0))
				phase_name = "角色快步 · 352.5"
			180:
				battle.replay_direction = Vector2.LEFT*orientation
				intended_facing = -orientation
				input_events.append(_feed_key(battle,forward_key,false,frame/30.0-0.22))
				input_events.append_array(_double_tap(battle,backward_key,frame/30.0))
				phase_name = "转向甩摆"
			204:
				battle.replay_direction = Vector2.ZERO
				input_events.append(_feed_key(battle,backward_key,false,frame/30.0))
				phase_name = "急停 · 后摆继续回落"
			216:
				battle.configuring = true
				phase_name = "暂停冻结"
			228:
				battle.configuring = false
				phase_name = "恢复 · 后摆继续回落"
		battle.driver_facing = intended_facing
		await get_tree().process_frame
		battle._process(1.0/30.0)
		assert(battle.facing==intended_facing)
		var selected: Array = battle._current_player_animation()
		var hud_cells := []
		if battle.battle_hud != null and battle.battle_hud.has_method("stamina_cell_fill"):
			for cell in range(6): hud_cells.append(float(battle.battle_hud.stamina_cell_fill(cell)))
		var record := {"frame":frame,"seconds":frame/30.0,"label":phase_name,"facing":battle.facing,"expected_facing":intended_facing,
			"clip":selected[0],"phase":selected[1],"backward":battle.moving_backward,"delta":battle.visual_motion_delta,"clock":battle.motion_time,
			"configured_test_speed":battle.move_speed,"actual_speed":battle.visual_motion_velocity.length(),"velocity":[battle.visual_motion_velocity.x,battle.visual_motion_velocity.y],
			"fast_step":battle.is_fast_stepping(),"fast_step_seconds":battle.fast_step_seconds_this_frame,"stamina":battle.stamina,"stamina_clock":battle.stamina_clock,
			"hp":battle.hp,"hud_stamina_cells":hud_cells,"input_events":input_events,"profiles":{}}
		for item in details:
			var rig = item.rig
			var skin = rig.wanderer_skin
			skin.set_motion_context(battle.visual_motion_delta,battle.visual_motion_velocity,battle.motion_time,battle.facing)
			skin.set_action_context(battle.guard_window>0.0,not battle.pending_attack.is_empty())
			rig.set_character_pose(str(selected[0]),float(selected[1]),battle.facing)
			rig.scale *= 4.0
			record.profiles[item.mode] = _record_rig(rig)
		phase_label.text = "%04.2fs / 10.00s    %s" % [frame/30.0,phase_name]
		status_label.text = "体力 %.2f / 6   ·   %s   ·   同输入原生渲染" % [float(battle.stamina),"双击快步" if battle.is_fast_stepping() else "慢步 / 停步"]
		journal.store_line(JSON.stringify(record))
		journal.flush()
		if probe_only and frame not in CAPE_PROBE_FRAMES: continue
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		assert(get_viewport().get_texture().get_image().save_png(output.path_join("compare_%04d.png"%frame))==OK)
		for item in details:
			assert(item.viewport.get_texture().get_image().save_png(output.path_join("%s_%04d.png"%[item.mode,frame]))==OK)
		if frame in ARENA_FRAMES:
			overlay.hide()
			await get_tree().process_frame
			await RenderingServer.frame_post_draw
			assert(get_viewport().get_texture().get_image().save_png(output.path_join("arena_%04d.png"%frame))==OK)
			overlay.show()
		if frame%60==0: print("CAPE LIKE ROBE CAPTURE ",frame,"/300")
	journal.close()
	print("CAPE LIKE ROBE COMPLETE: ",output)
	get_tree().quit()
