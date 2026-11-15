extends SceneTree
## Real manual combat cases; target snapshots, accuracy and damage are untouched.
## The specified starting qi_blood is an explicit presentation fixture resource.

const Rules = preload("res://tools/action_battle_test/action_manual_rules.gd")
const RUNTIME := "res://csharp/battle/ActionManualBattleRuntime.cs"
const VIEW_SIZE := Vector2i(1280, 720)
const CROP := Rect2i(320, 100, 720, 500)
var cases := [
	{"id": "no_blood", "label": "无气血 · 普通拳掌", "blood": 0, "distance": 52.0, "direction": Vector2.RIGHT, "combo": false, "peak": 0.30},
	{"id": "near", "label": "近处 · 本体与血气", "blood": 2, "distance": 52.0, "direction": Vector2.RIGHT, "combo": false, "peak": 0.30},
	{"id": "extended", "label": "本体范围外 · 血气外放", "blood": 2, "distance": 88.0, "direction": Vector2.RIGHT, "combo": false, "peak": 0.30},
	{"id": "combo", "label": "四段外放 · 左斜向", "blood": 4, "distance": 90.0, "direction": Vector2(-1.0, -0.22).normalized(), "combo": true, "peak": 1.93},
]

class CaptureArena extends "res://tools/action_battle_test/manual_battle_arena.gd":
	var fixed_aim := Vector2.RIGHT
	func _enter_trial_fullscreen(_fullscreen := true) -> void:
		super._enter_trial_fullscreen(false)
	func attack_aim_direction() -> Vector2:
		return fixed_aim
	func movement_direction() -> Vector2:
		return Vector2.ZERO

var arena: Node2D
var capture_view: SubViewport
var output_dir := "res://output/blood_qi_ink_20261105"
var frames := 84
var fps := 30.0
var stills := false
var suite := "default"
var explicit_output := false
var only_case := ""
var manifest := {"source_script": "res://tools/action_battle_test/manual_battle_arena.gd",
	"runtime": RUNTIME, "fps": 30.0, "frames": 84, "columns": 2,
	"crop": [320, 100, 720, 500], "cases": [],
	"fixture": {"chapter": 4, "role": 1, "bindings": ["", "", "", ""],
		"enemy_count": 1, "custom_attributes": false, "red_tide_buff_used": false}}


func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			output_dir = argument.trim_prefix("--capture-dir=").replace("\\", "/")
			explicit_output = true
		elif argument.begins_with("--frames="):
			frames = maxi(1, argument.trim_prefix("--frames=").to_int())
		elif argument.begins_with("--fps="):
			fps = maxf(1.0, argument.trim_prefix("--fps=").to_float())
		elif argument == "--stills":
			stills = true
		elif argument.begins_with("--suite="):
			suite = argument.trim_prefix("--suite=")
		elif argument.begins_with("--only="):
			only_case = argument.trim_prefix("--only=")
	if not _select_suite():
		quit(2)
		return
	call_deferred("capture")


func _select_suite() -> bool:
	if suite not in ["default", "stages", "directions"]:
		push_error("Unknown blood-qi capture suite: " + suite)
		return false
	if suite == "stages":
		cases.clear()
		var labels := ["第一段 · 探拳", "第二段 · 进步横掌", "第三段 · 回身穿拳", "第四段 · 推掌收势"]
		var peaks := [0.30, 0.78, 1.30, 1.91]
		for index in range(4):
			cases.append({"id": "stage_%d" % (index + 1), "label": labels[index],
				"blood": 4, "distance": 200.0, "direction": Vector2.RIGHT,
				"combo": true, "stage_focus": index + 1, "peak": peaks[index],
				"reference_direction": Vector2.RIGHT, "camera_x": 35.0})
	elif suite == "directions":
		cases.clear()
		var headings := [Vector2(-1, -1), Vector2.UP, Vector2(1, -1), Vector2.LEFT,
			Vector2.RIGHT, Vector2(-1, 1), Vector2.DOWN, Vector2(1, 1)]
		var labels := ["左上", "向上", "右上", "向左", "向右", "左下", "向下", "右下"]
		var positions := [Vector2i(0, 0), Vector2i(1, 0), Vector2i(2, 0), Vector2i(0, 1),
			Vector2i(2, 1), Vector2i(0, 2), Vector2i(1, 2), Vector2i(2, 2)]
		for index in range(headings.size()):
			cases.append({"id": "direction_%d" % index, "label": labels[index] + " · 探拳",
				"blood": 2, "distance": 200.0, "direction": Vector2(headings[index]).normalized(),
				"combo": false, "peak": 0.30, "grid_position": [positions[index].x, positions[index].y],
				"reference_direction": Vector2.RIGHT, "camera_x": 35.0})
		manifest.columns = 3
		manifest["rows"] = 3
		manifest["center_label"] = "同一探拳 · 八向同形\n共用普攻原图集"
	if suite != "default" and not explicit_output:
		output_dir = "res://output/blood_qi_ink_%s_20261105" % suite
	if not only_case.is_empty():
		cases = cases.filter(func(rule): return str(rule.id) == only_case)
		if cases.is_empty():
			push_error("No blood-qi capture case named " + only_case)
			return false
		if cases.size() == 1:
			manifest.columns = 1
			manifest["rows"] = 1
			manifest.erase("center_label")
			cases[0].erase("grid_position")
		manifest["only"] = only_case
	manifest["suite"] = suite
	return true


func _window() -> void:
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_set_size(VIEW_SIZE)
	root.mode = Window.MODE_WINDOWED
	root.size = VIEW_SIZE
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)


func _resources() -> Dictionary:
	return {"hp": arena.hp, "stamina": arena.stamina, "mana": arena.mana,
		"qi": arena.qi, "qi_blood": arena.qi_blood,
		"target_hp": arena.npcs[0].hp, "target_stamina": arena.npcs[0].stamina,
		"skill_cooldowns": arena.skill_cooldowns.duplicate(true)}


func _pose(delta: float) -> void:
	arena.motion_time += delta
	arena.visual_motion_delta = delta
	arena.visual_motion_velocity = Vector2.ZERO
	arena.update_skinned_player()
	if arena.original_wanderer != null:
		arena.original_wanderer.z_index = int(arena.player.y)
	if arena.skill_vfx != null:
		arena.skill_vfx.z_index = int(arena.player.y) + 6
	for npc in arena.npcs:
		npc.motion_time += delta
		npc.visual_motion_delta = delta
		npc.visual_motion_velocity = Vector2.ZERO
		npc.update_skinned_player()
		if npc.original_wanderer != null:
			npc.original_wanderer.z_index = int(npc.player.y)
		if npc.skill_vfx != null:
			npc.skill_vfx.z_index = int(npc.player.y) + 6
	arena.queue_redraw()


func _reset(rule: Dictionary) -> void:
	arena.restart_battle()
	arena.set_simulation_paused(false)
	arena.player = Vector2(1650, 1020)
	arena.fixed_aim = rule.direction
	arena.facing = -1.0 if rule.direction.x < 0 else 1.0
	arena.walk_amount = 0.0
	arena.qi_blood = int(rule.blood)
	arena.sync_resource_statuses()
	var npc: Node2D = arena.npcs[0]
	npc.ai_enabled = false
	var reference_direction: Vector2 = rule.get("reference_direction", rule.direction)
	npc.player = arena.player + reference_direction * float(rule.distance)
	npc.enemy = arena.player
	npc.facing = -1.0 if npc.player.x >= arena.player.x else 1.0
	npc.walk_amount = 0.0
	arena.refresh_health()
	arena.trial_camera.zoom = Vector2.ONE * 2.0
	arena.trial_camera.position = arena.player + Vector2(float(rule.get("camera_x", 0.0)), -45)
	arena.trial_camera.position_smoothing_enabled = false
	arena.trial_camera.force_update_scroll()
	_pose(0.0)
	arena.battle_hud.visible = false
	if arena.hitbox_overlay != null:
		arena.hitbox_overlay.visible = false


func _advance(delta: float) -> void:
	arena.simulation_paused = false
	arena.update_strike_fx(delta)
	arena.update_guard_fx(delta)
	arena.update_combat_texts(delta)
	arena.update_skill_timers(delta)
	arena.update_stamina_recovery(delta)
	for npc in arena.npcs:
		npc.update_strike_fx(delta)
		npc.update_guard_fx(delta)
		npc.update_skill_timers(delta)
	if arena.manual_vfx != null:
		arena.manual_vfx.advance(delta)
	_pose(delta)
	if arena.skill_vfx != null:
		arena.skill_vfx.sync_visuals()
	for npc in arena.npcs:
		if npc.skill_vfx != null:
			npc.skill_vfx.sync_visuals()


func _shot(path: String, full_path := "") -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var shot := capture_view.get_texture().get_image()
	assert(shot != null and not shot.is_empty() and shot.get_size() == VIEW_SIZE)
	assert(shot.get_region(CROP).save_png(path) == OK)
	if not full_path.is_empty():
		assert(shot.save_png(full_path) == OK)


func _mark_sample(mark: Dictionary) -> Dictionary:
	return {"origin": [mark.origin.x, mark.origin.y],
		"direction": [mark.direction.x, mark.direction.y],
		"reach": mark.reach, "extension": mark.extension,
		"visual_key": str(mark.get("visual_key", "")),
		"combo_stage": int(mark.get("combo_stage", 0)),
		"visual_height": float(mark.get("visual_height", 1.0)),
		"form": str(mark.get("form", "")),
		"texture_path": str(mark.get("texture_path", "")),
		"source_type": str(mark.get("source_type", "")),
		"animation_frame": int(mark.get("animation_frame", -1)),
		"visual_extent": float(mark.get("visual_extent", 0.0))}


func _capture_case(rule: Dictionary) -> bool:
	_reset(rule)
	var before := _resources()
	var folder := output_dir.path_join(rule.id)
	assert(DirAccess.make_dir_recursive_absolute(folder) == OK)
	arena.held_basic_attack = bool(rule.combo)
	arena.use_skill("拳掌")
	var peak_frame := mini(frames - 1, int(round(float(rule.peak) * fps)))
	var seen_marks: Array[Dictionary] = []
	var mark_samples: Array[Dictionary] = []
	var peak_marks := 0
	var peak_samples: Array[Dictionary] = []
	for frame in range(frames):
		if frame > 0:
			_advance(1.0 / fps)
		for mark in arena.manual_vfx.marks:
			if not seen_marks.has(mark):
				seen_marks.append(mark)
				mark_samples.append(_mark_sample(mark))
		if frame == peak_frame:
			peak_marks = arena.manual_vfx.marks.size()
			for mark in arena.manual_vfx.marks:
				peak_samples.append(_mark_sample(mark))
		if not stills or frame == peak_frame:
			var full := output_dir.path_join(rule.id + "_full.png") if frame == peak_frame else ""
			await _shot(folder.path_join("frame_%03d.png" % frame), full)
			if frame == peak_frame:
				assert(capture_view.get_texture().get_image().get_region(CROP).save_png(output_dir.path_join(rule.id + "_peak.png")) == OK)
		else:
			# Always drain mesh/skinning rendering work, including still-only mode.
			await process_frame
			await RenderingServer.frame_post_draw
	var events: Array = arena.damage_events.duplicate(true)
	var body_contacts := events.filter(func(event): return event.kind == "本体").size()
	var blood_contacts := events.filter(func(event): return event.kind == "血气").size()
	var expected_marks := (4 if rule.combo else 1) if int(rule.blood) > 0 else 0
	var no_wave: bool = arena.wave_release_count == 0 and arena.active_waves.is_empty() and is_zero_approx(arena.action_manual.buff_remaining)
	var passed: bool = seen_marks.size() == expected_marks and no_wave and arena.qi_blood == int(rule.blood)
	if rule.id == "no_blood":
		passed = passed and blood_contacts == 0
	if rule.id == "near":
		passed = passed and body_contacts > 0 and blood_contacts > 0
	if rule.id == "extended":
		passed = passed and body_contacts == 0 and blood_contacts > 0
	if suite in ["stages", "directions"]:
		passed = passed and body_contacts == 0 and blood_contacts == 0
	if suite == "stages":
		var focus := int(rule.stage_focus)
		var expected_key := "拳掌" if focus in [1, 3] else "拳掌_掌"
		passed = passed and peak_samples.any(func(mark): return (int(mark.combo_stage) == focus
			and str(mark.source_type) == "basic_attack_atlas" and str(mark.visual_key) == expected_key
			and is_equal_approx(float(mark.visual_extent), float(mark.reach) + float(mark.extension))))
	if suite == "directions":
		passed = passed and peak_samples.any(func(mark): return (str(mark.source_type) == "basic_attack_atlas"
			and str(mark.visual_key) == "拳掌"
			and is_equal_approx(float(mark.visual_extent), float(mark.reach) + float(mark.extension))))
	var item := {"id": rule.id, "label": rule.label, "blood": rule.blood,
		"target_distance": rule.distance, "direction": [rule.direction.x, rule.direction.y],
		"frames": frames, "peak_frame": peak_frame, "stills_only": stills,
		"resource_before": before, "resource_after": _resources(), "damage_events": events,
		"body_contacts": body_contacts, "blood_contacts": blood_contacts,
		"blood_marks_emitted": seen_marks.size(), "peak_live_marks": peak_marks,
		"peak_mark_samples": peak_samples, "stage_focus": int(rule.get("stage_focus", 0)),
		"blood_mark_samples": mark_samples, "no_red_tide_buff_or_wave": no_wave, "passed": passed,
		"static_memory_mb": OS.get_static_memory_usage() / 1048576.0,
		"static_memory_peak_mb": OS.get_static_memory_peak_usage() / 1048576.0}
	if rule.has("grid_position"):
		item["grid_position"] = rule.grid_position
	manifest.cases.append(item)
	# Keep the source-atlas metadata in the manifest and the log concise.
	var summary := {"id": rule.id, "passed": passed, "suite": suite,
		"marks": seen_marks.size(), "peak_live_marks": peak_marks,
		"body_contacts": body_contacts, "blood_contacts": blood_contacts,
		"peak_stage": peak_samples.map(func(mark): return mark.combo_stage),
		"peak_form": peak_samples.map(func(mark): return mark.form),
		"source_type": peak_samples.map(func(mark): return mark.source_type),
		"animation_frame": peak_samples.map(func(mark): return mark.animation_frame),
		"visual_extent": peak_samples.map(func(mark): return mark.visual_extent),
		"no_wave": no_wave, "blood_before": before.qi_blood, "blood_after": arena.qi_blood,
		"static_memory_mb": item.static_memory_mb, "static_memory_peak_mb": item.static_memory_peak_mb}
	print("BLOOD_QI_INK_CASE ", JSON.stringify(summary))
	return passed


func capture() -> void:
	_window()
	assert(DirAccess.make_dir_recursive_absolute(output_dir) == OK)
	capture_view = SubViewport.new()
	capture_view.size = VIEW_SIZE
	capture_view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(capture_view)
	current_scene = capture_view
	arena = CaptureArena.new()
	var player_options := Rules.default_side(true)
	player_options.chapter = 4
	player_options.role = 1
	player_options.bindings = ["", "", "", ""]
	var enemy_options := Rules.default_side(false)
	enemy_options.book = false
	arena.manual_battle_setup = {"player": player_options, "enemy": enemy_options, "enemy_count": 1}
	var runtime: Node = load(RUNTIME).new()
	runtime.name = "ManualBattleAttributes"
	arena.add_child(runtime)
	capture_view.add_child(arena)
	arena.set_process(false)
	arena.set_process_input(false)
	arena.battle_hud.set_process_input(false)
	for frame in range(3):
		await process_frame
	_window()
	await process_frame
	await RenderingServer.frame_post_draw
	assert(capture_view.get_texture().get_image().get_size() == VIEW_SIZE)
	var passed := true
	for rule in cases:
		passed = await _capture_case(rule) and passed
	manifest.fps = fps
	manifest.frames = frames
	manifest["stills_only"] = stills
	manifest["passed"] = passed
	manifest["view_size"] = [VIEW_SIZE.x, VIEW_SIZE.y]
	var file := FileAccess.open(output_dir.path_join("manifest.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest, "\t"))
	file.close()
	print("BLOOD_QI_INK_CAPTURE_%s cases=%d frames=%d fps=%s" % ["OK" if passed else "FAIL", cases.size(), frames, fps])
	arena.queue_free()
	await process_frame
	quit(0 if passed else 1)
