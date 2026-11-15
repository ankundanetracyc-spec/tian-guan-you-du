extends "capture_running_swing_short.gd"

## Same-input comparison against the actual pre-Guigu revision.
## Main take: three seconds running, two seconds released; short checks follow.
class ClothComparisonRig extends "guigu_rig_runtime.gd":
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
		if not _build_weighted_meshes(skins_data,art_root): return false
		wanderer_skin = load("res://reference_walk_skin.gd" if comparison_mode=="old" else "res://guigu_reconstructed_skin.gd").new()
		bone_bound_art_ready = wanderer_skin.build(self,art_root.path_join(custom_art_folder))
		_load_animation_tracks(animations_data)
		loaded = bone_bound_art_ready
		return loaded

const CLOTH_KEYS := [0,4,12,44,74,88,90,92,98,108,126,148,149,150,160,162,164,170,174,182,190,191]

func _make_detail(mode: String, overlay: Node, index: int) -> Dictionary:
	var detail := SubViewport.new()
	detail.size = Vector2i(440,500)
	detail.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(detail)
	var backdrop := ColorRect.new()
	backdrop.color = Color("58646a")
	backdrop.size = Vector2(440,500)
	detail.add_child(backdrop)
	var rig := ClothComparisonRig.new()
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
	_label(overlay,"上轮持架拟合" if mode=="old" else "素材与蒙皮重构",Vector2(index*440+22,9),24)
	return {"rig":rig,"viewport":detail,"mode":mode}

func _ready() -> void:
	get_tree().auto_accept_quit = false
	get_window().unfocusable = true
	get_window().position = Vector2i(-7000,-7000)
	ui_font.font_names = PackedStringArray(["Microsoft YaHei"])
	var output_name := "guigu_material_rebuild_final"
	var probe_only := OS.get_cmdline_user_args().has("--probe-only")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output_name = argument.trim_prefix("--output=")
	var output := ProjectSettings.globalize_path("res://output/"+output_name)
	DirAccess.make_dir_recursive_absolute(output)
	var provenance := FileAccess.open(output.path_join("capture_source.json"),FileAccess.WRITE)
	provenance.store_string(JSON.stringify({"new_source_sha256":FileAccess.get_sha256("res://guigu_reconstructed_skin.gd"),
		"old_source_sha256":FileAccess.get_sha256("res://reference_walk_skin.gd"),
		"source_profile_sha256":FileAccess.get_sha256("res://guigu_run_pose_profile_20261104.json"),
		"capture_scene":"res://capture_material_rebuild.tscn","main_run_frames":90,"main_stop_frames":60,
		"sample_rate":30,"base_speed":114,"enemy_fixture_offset":[-650,0]},"\t"))
	provenance.close()
	var battle := ReferenceReplayBattle.new()
	battle.original_wanderer_enabled = true
	battle.original_wanderer_art_folder = "yuxu_fullbody_v15"
	add_child(battle)
	battle.set_process(false)
	battle.set_process_input(false)
	assert(battle.move_speed==114.0)
	battle.player = Vector2(1500,950)
	# The normal enemy AI follows from behind so the release interval stays safe.
	# Its movement, attacks and the shared camera all retain their normal rules.
	battle.enemy = battle.player-Vector2(650,0)
	battle._update_trial_camera(0.0,true)
	battle.original_wanderer.queue_free()
	var arena_rig := ClothComparisonRig.new()
	battle.add_child(arena_rig)
	assert(arena_rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"))
	arena_rig.set_skin_mode("experiment")
	battle.original_wanderer = arena_rig
	get_window().size = Vector2i(880,600)
	get_window().content_scale_size = Vector2i(880,600)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	get_window().position = Vector2i(-7000,-7000)
	var overlay := CanvasLayer.new()
	overlay.layer = 100
	add_child(overlay)
	var panel := ColorRect.new()
	panel.color = Color("424d53")
	panel.size = Vector2(880,600)
	overlay.add_child(panel)
	var details := [_make_detail("old",overlay,0),_make_detail("new",overlay,1)]
	var phase_label := _label(overlay,"静止检查",Vector2(18,551),21)
	_label(overlay,"同输入 · 单一原移速 · 衣身、袖料与骨链",Vector2(18,578),15)
	await get_tree().process_frame
	battle.driver_facing = 1.0
	battle._process(1.0/30.0)
	var selected: Array = battle._current_player_animation()
	_pose_short(details,battle,selected)
	var idle := _short_record(-1,"静止",details,battle,selected)
	await _save_short(output,"idle",details,overlay)
	idle.native = _native_points(battle,arena_rig)
	var idle_file := FileAccess.open(output.path_join("idle_record.json"),FileAccess.WRITE)
	idle_file.store_string(JSON.stringify(idle,"\t"))
	idle_file.close()
	var journal := FileAccess.open(output.path_join("frames.jsonl"),FileAccess.WRITE)
	for frame in range(192):
		var label := "普通跑动"
		battle.driver_facing = 1.0
		battle.replay_direction = Vector2.RIGHT
		if frame>=90 and frame<150:
			battle.replay_direction = Vector2.ZERO
			label = "松键 · 衣摆回落"
		elif frame>=150 and frame<162:
			battle.replay_direction = Vector2.LEFT
			label = "短后退检查"
		elif frame>=162 and frame<174:
			battle.driver_facing = -1.0
			battle.replay_direction = Vector2.LEFT
			label = "转向检查"
		elif frame>=174:
			battle.driver_facing = -1.0
			battle.replay_direction = Vector2.ZERO
			label = "转向后停步"
		battle._process(1.0/30.0)
		assert(battle.stamina==6.0 and battle.hp==100.0)
		selected = battle._current_player_animation()
		_pose_short(details,battle,selected)
		var record := _short_record(frame,label,details,battle,selected)
		phase_label.text = "%04.2fs    %s" % [(frame+1)/30.0,label]
		if (not probe_only and frame<150 and frame%2==0) or frame in CLOTH_KEYS:
			await _save_short(output,"%04d"%frame,details,overlay)
			record.native = _native_points(battle,arena_rig)
		journal.store_line(JSON.stringify(record))
		journal.flush()
		if frame%30==0: print("GUIGU MATERIAL REBUILD CAPTURE ",frame,"/192")
	journal.close()
	print("GUIGU MATERIAL REBUILD CAPTURE COMPLETE: ",output)
	get_tree().quit()
