extends "capture_reference_walk.gd"

## Two seconds of real forward movement, plus static/backward review samples.
## The opponent starts 600 units away and keeps its normal AI/camera rules.
const SHORT_KEY_FRAMES := [8,18,28,38,48,58,64,74,82]
var short_header_old := "旧方案"
var short_header_new := "新展开动作"
var short_default_output := "reference_upper_open_short_probe"
var short_frame_count := 84
var short_fixture_distance := 600.0
var short_stop_frame := -1
var short_even_capture_until := -1
var short_extra_key_frames: Array[int] = []

func _pose_short(details: Array, battle, selected: Array) -> void:
	for item in details:
		var rig = item.rig
		var skin = rig.wanderer_skin
		skin.set_motion_context(battle.visual_motion_delta,battle.visual_motion_velocity,battle.motion_time,battle.facing)
		skin.set_action_context(battle.guard_window>0.0,not battle.pending_attack.is_empty())
		rig.set_character_pose(str(selected[0]),float(selected[1]),battle.facing)
		rig.scale *= 4.0

func _short_record(frame: int, label: String, details: Array, battle, selected: Array) -> Dictionary:
	var record := {"frame":frame,"seconds":(frame+1)/30.0,"label":label,"facing":battle.facing,"clip":selected[0],"phase":selected[1],
		"backward":battle.moving_backward,"delta":battle.visual_motion_delta,"clock":battle.motion_time,"base_speed":battle.move_speed,
		"actual_speed":battle.visual_motion_velocity.length(),"stamina":battle.stamina,"hp":battle.hp,
		"player":[battle.player.x,battle.player.y],"enemy":[battle.enemy.x,battle.enemy.y],"profiles":{}}
	for item in details: record.profiles[item.mode] = _record_rig(item.rig)
	return record

func _native_points(battle, arena_rig) -> Dictionary:
	var canvas: Transform2D = battle.get_canvas_transform()
	var result := {"foot_origin":_serial(canvas*battle.player),"points":{}}
	for key in ["pelvis","torso","head","near_upper","near_forearm","near_hand","far_upper","far_forearm","far_hand"]:
		result.points[key] = _serial(canvas*arena_rig.wanderer_skin.bones[key].global_position)
	if battle.trial_camera != null: result.camera_zoom = _serial(battle.trial_camera.zoom)
	return result

func _save_short(output: String, tag: String, details: Array, overlay: CanvasLayer) -> void:
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	assert(get_viewport().get_texture().get_image().save_png(output.path_join("compare_"+tag+".png"))==OK)
	for item in details:
		assert(item.viewport.get_texture().get_image().save_png(output.path_join(item.mode+"_"+tag+".png"))==OK)
	overlay.hide()
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	assert(get_viewport().get_texture().get_image().save_png(output.path_join("arena_"+tag+".png"))==OK)
	overlay.show()

func _ready() -> void:
	get_tree().auto_accept_quit = false
	get_window().unfocusable = true
	get_window().position = Vector2i(-7000,-7000)
	ui_font.font_names = PackedStringArray(["Microsoft YaHei"])
	var output_name := short_default_output
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--output="): output_name = argument.trim_prefix("--output=")
	var output := ProjectSettings.globalize_path("res://output/"+output_name)
	DirAccess.make_dir_recursive_absolute(output)
	var battle := ReferenceReplayBattle.new()
	battle.original_wanderer_enabled = true
	battle.original_wanderer_art_folder = "yuxu_fullbody_v15"
	add_child(battle)
	battle.set_process(false)
	battle.set_process_input(false)
	assert(battle.move_speed==114.0)
	battle.player = Vector2(1500,950)
	battle.enemy = battle.player+Vector2(short_fixture_distance,0)
	battle._update_trial_camera(0.0,true)
	var retired_rig = battle.original_wanderer
	retired_rig.queue_free()
	var arena_rig := ReferenceRig.new()
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
	for child in overlay.get_children():
		if child is Label and child.position.y==9:
			child.text = short_header_old if child.position.x<440 else short_header_new
	var phase_label := _label(overlay,"静止检查",Vector2(18,551),21)
	_label(overlay,"单一普通移动 · 同输入",Vector2(18,578),15)
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
	for frame in range(short_frame_count):
		var stopping := short_stop_frame>=0 and frame>=short_stop_frame
		battle.replay_direction = Vector2.ZERO if stopping else (Vector2.RIGHT if frame<60 else Vector2.LEFT)
		var label := "停步与衣摆回落" if stopping else ("普通前进" if frame<60 else "后退检查")
		battle._process(1.0/30.0)
		assert(battle.facing==1.0 and battle.stamina==6.0)
		selected = battle._current_player_animation()
		_pose_short(details,battle,selected)
		var record := _short_record(frame,label,details,battle,selected)
		phase_label.text = "%04.2fs    %s" % [(frame+1)/30.0,label]
		var capture_even := frame%2==0 and (short_even_capture_until<0 or frame<short_even_capture_until)
		if capture_even or frame in SHORT_KEY_FRAMES or frame in short_extra_key_frames:
			await _save_short(output,"%04d"%frame,details,overlay)
			record.native = _native_points(battle,arena_rig)
		journal.store_line(JSON.stringify(record))
		journal.flush()
		if frame%30==0: print("UPPER BODY SHORT CAPTURE ",frame,"/",short_frame_count)
	journal.close()
	print("UPPER BODY SHORT COMPLETE: ",output)
	get_tree().quit()
