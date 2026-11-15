extends SceneTree
## MovieMaker records the visible production arena; the preview exports video only.
## This controller schedules genuine inputs; it never advances combat offline,
## changes a snapshot, or substitutes presentation events for contact results.

const VIEW_SIZE := Vector2i(1280, 720)
const HUMAN_RUNTIME := "res://csharp/battle/ActionHumanSparringRuntime.cs"
const MANUAL_RUNTIME := "res://csharp/battle/ActionManualBattleRuntime.cs"
const MANUAL_RULES = preload("res://tools/action_battle_test/action_manual_rules.gd")

class HumanArena extends "res://tools/action_battle_test/human_sparring_arena.gd":
	var fixed_aim := Vector2.RIGHT
	func _enter_trial_fullscreen(_fullscreen := true) -> void:
		super._enter_trial_fullscreen(false)
	func attack_aim_direction() -> Vector2:
		return fixed_aim
	func movement_direction() -> Vector2:
		return Vector2.ZERO
	func _notification(what: int) -> void:
		if what != NOTIFICATION_WM_WINDOW_FOCUS_OUT:
			super._notification(what)

class ManualArena extends "res://tools/action_battle_test/manual_battle_arena.gd":
	var fixed_aim := Vector2.RIGHT
	func _enter_trial_fullscreen(_fullscreen := true) -> void:
		super._enter_trial_fullscreen(false)
	func attack_aim_direction() -> Vector2:
		return fixed_aim
	func movement_direction() -> Vector2:
		return Vector2.ZERO
	func _notification(what: int) -> void:
		if what != NOTIFICATION_WM_WINDOW_FOCUS_OUT:
			super._notification(what)

var cases: Array[Dictionary] = [
	{"id":"fist", "label":"拳掌", "skill":"拳掌", "distance":52.0, "frames":150, "expect":"damage"},
	{"id":"heavy", "label":"痛击", "skill":"痛击", "distance":55.0, "frames":150, "expect":"damage"},
	{"id":"heavy_miss", "label":"痛击 · 距离外", "skill":"痛击", "distance":145.0, "frames":150, "expect":"miss"},
	{"id":"guard", "label":"迎面架招", "skill":"架招", "distance":55.0, "frames":150, "expect":"block"},
	{"id":"dodge", "label":"错步", "skill":"错步", "distance":15.0, "frames":150, "expect":"evade"},
	{"id":"blood_qi", "label":"催血后拳掌", "skill":"拳掌", "distance":52.0, "frames":240, "expect":"blood", "manual":true},
]
var arena: Node2D
var arena_manual := false
var caption: Label
var outcome_label: Label
var output_dir := "res://output/combat_impact_capture_20261105"
var only_case := ""
var fps := 60.0
var visible_window := false
var include_blood := true
var include_wave := false
var reaction_demo := false
var max_attempts := 6
var global_frame := 0
var case_frame := -1
var case_index := -1
var attempt := 0
var warmup_frames := 45
var finishing_frames := -1
var active := false
var finished := false
var current_take: Dictionary = {}
var mark_keys: Dictionary = {}
var manifest := {
	"source_scenes":["res://tscn/scene/ActionHumanSparringScene.tscn", "res://tscn/scene/ActionManualBattleScene.tscn"],
	"frame_step":"One production arena process per MovieMaker frame; no manual advance calls.",
	"audio":"Not requested. Combat feedback has no audio players; preview export contains no audio track.",
	"fixture":{"opponents":1, "autonomous_ai":false, "movement_input":[0,0], "snapshot_overrides":false,
		"resource_overrides":false, "production_camera":true, "native_avatar_required":true},
	"takes":[], "case_results":[], "passed":false,
}


func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			output_dir = argument.trim_prefix("--capture-dir=").replace("\\", "/")
		elif argument.begins_with("--only="):
			only_case = argument.trim_prefix("--only=")
		elif argument.begins_with("--fps="):
			fps = maxf(1.0, argument.trim_prefix("--fps=").to_float())
		elif argument.begins_with("--max-attempts="):
			max_attempts = maxi(1, argument.trim_prefix("--max-attempts=").to_int())
		elif argument == "--visible":
			visible_window = true
		elif argument == "--no-blood":
			include_blood = false
		elif argument == "--include-wave":
			include_wave = true
		elif argument == "--reaction-demo":
			reaction_demo = true
	if reaction_demo:
		cases = [
			{"id":"fist", "label":"拳掌 · 受击回弹", "skill":"拳掌", "distance":52.0, "frames":150, "expect":"damage"},
			{"id":"heavy", "label":"痛击 · 受击后仰", "skill":"痛击", "distance":55.0, "frames":150, "expect":"damage"},
			{"id":"heavy_up", "label":"向上命中 · 受击折身", "skill":"痛击", "distance":110.0, "direction":Vector2.UP, "offensive":true, "frames":150, "expect":"damage"},
			{"id":"heavy_down", "label":"向下命中 · 受击折身", "skill":"痛击", "distance":110.0, "direction":Vector2.DOWN, "offensive":true, "frames":150, "expect":"damage"},
		]
	if not include_blood:
		cases = cases.filter(func(rule): return not bool(rule.get("manual", false)))
	if include_wave:
		cases.append({"id":"blood_wave", "label":"赤潮奔涌 · 完整连段", "skill":"拳掌", "distance":MANUAL_RULES.WAVE_REACH*0.86,
			"frames":360, "expect":"wave", "manual":true})
	if not only_case.is_empty():
		cases = cases.filter(func(rule): return str(rule.id) == only_case)
	manifest["fps"] = fps
	manifest["size"] = [VIEW_SIZE.x, VIEW_SIZE.y]
	manifest["command_line"] = OS.get_cmdline_args()
	manifest["max_attempts"] = max_attempts
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output_dir))
	if cases.is_empty():
		push_error("No capture case: " + only_case)
		quit(2)
		return
	call_deferred("_setup")


func _setup() -> void:
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_set_size(VIEW_SIZE)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	root.mode = Window.MODE_WINDOWED
	root.size = VIEW_SIZE
	root.content_scale_size = VIEW_SIZE
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.position = Vector2i(80, 80) if visible_window else Vector2i(-4096, -4096)
	root.unfocusable = true
	root.gui_disable_input = true
	var layer := CanvasLayer.new()
	layer.layer = 50
	root.add_child(layer)
	var panel := Panel.new()
	panel.position = Vector2(28, 570)
	panel.size = Vector2(320, 80)
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.93, 0.91, 0.84, 0.88)
	style.border_color = Color(0.25, 0.29, 0.26, 0.45)
	style.set_border_width_all(1)
	panel.add_theme_stylebox_override("panel", style)
	layer.add_child(panel)
	caption = Label.new()
	caption.position = Vector2(14, 9)
	caption.add_theme_font_size_override("font_size", 21)
	caption.add_theme_color_override("font_color", Color("263b35"))
	caption.text = "现有招式 · 打击反馈"
	panel.add_child(caption)
	outcome_label = Label.new()
	outcome_label.position = Vector2(14, 43)
	outcome_label.add_theme_font_size_override("font_size", 17)
	outcome_label.add_theme_color_override("font_color", Color("65544c"))
	outcome_label.text = "实战预览 · 准备"
	panel.add_child(outcome_label)
	_create_arena(bool(cases[0].get("manual", false)))
	active = true


func _create_arena(manual: bool) -> void:
	if arena != null:
		root.remove_child(arena)
		arena.free()
	arena_manual = manual
	arena = ManualArena.new() if manual else HumanArena.new()
	arena.name = "CombatImpactCaptureArena"
	if manual:
		arena.manual_battle_setup = {
			"player":MANUAL_RULES.default_side(),
			"enemy":MANUAL_RULES.default_side(false), "enemy_count":1}
	var runtime: Node = load(MANUAL_RUNTIME if manual else HUMAN_RUNTIME).new()
	runtime.name = "ManualBattleAttributes" if manual else "HumanBattleAttributes"
	arena.add_child(runtime)
	root.add_child(arena)
	arena.set_process_input(false)
	arena.inspection_facing_override = 1.0
	for npc in arena.npcs:
		npc.ai_enabled = false
		npc.ai_move = Vector2.ZERO
	assert(arena.npcs.size() == 1)
	assert(arena.original_wanderer != null and arena.original_wanderer.native_source_mode)
	arena.set_simulation_paused(true)


func _begin_take() -> void:
	var rule: Dictionary = cases[case_index]
	if arena_manual != bool(rule.get("manual", false)):
		_create_arena(bool(rule.get("manual", false)))
	arena.restart_battle()
	arena.player = arena.TRIAL_WORLD_SIZE * Vector2(0.5, 0.56)
	arena.fixed_aim = Vector2.RIGHT
	if rule.has("direction"): arena.fixed_aim = rule.direction
	arena.facing = 1.0
	arena.walk_amount = 0.0
	arena.configuring = false
	arena.battle_hud.help_open = false
	var npc: Node2D = arena.npcs[0]
	npc.ai_enabled = false
	npc.ai_move = Vector2.ZERO
	var distance: float = rule.distance
	if str(rule.expect)=="miss": distance = maxf(distance,float(arena.SKILL_RULES["痛击"].reach)+50.0)
	npc.player = arena.player + arena.fixed_aim*distance
	npc.enemy = arena.player
	npc.facing = -1.0
	npc.walk_amount = 0.0
	arena.refresh_health()
	arena.set_simulation_paused(false)
	arena.update_skinned_player()
	npc.update_skinned_player()
	# Use the normal shared framer at its normal zoom and native person scale.
	arena._update_trial_camera(0.0, true)
	case_frame = 0
	mark_keys.clear()
	caption.text = str(rule.label)
	outcome_label.text = ""
	current_take = {"case":rule.id, "attempt":attempt, "start_frame":global_frame,
		"start_seconds":float(global_frame)/fps, "before":_state(), "inputs":[], "marks":[]}
	print("CAPTURE_BEGIN ", rule.id, " attempt=", attempt, " frame=", global_frame)


func _state() -> Dictionary:
	var units: Array = []
	for index in range(int(arena.attribute_runtime.get_unit_count())):
		units.append(arena.attribute_runtime.get_unit_state(index))
	return {"units":units, "stamina":arena.stamina, "qi_blood":arena.qi_blood,
		"position":[arena.player.x, arena.player.y],
		"npc_position":[arena.npcs[0].player.x, arena.npcs[0].player.y],
		"cooldowns":arena.skill_cooldowns.duplicate(true)}


func _input_skill(fighter: Node2D, skill: String) -> void:
	current_take.inputs.append({"frame":global_frame, "local_frame":case_frame,
		"unit":0 if fighter == arena else fighter.unit_index, "skill":skill,
		"stamina_before":fighter.stamina, "blood_before":fighter.qi_blood})
	fighter.use_skill(skill)


func _schedule() -> void:
	var rule: Dictionary = cases[case_index]
	var npc: Node2D = arena.npcs[0]
	# At 60 fps the contact occurs about twelve frames after NPC heavy begins.
	# Inputs use seconds, so --fps must match MovieMaker's --fixed-fps.
	var tick := roundi(float(case_frame) * 60.0 / fps)
	if (rule.id in ["fist", "heavy", "heavy_miss"] or bool(rule.get("offensive",false))) and tick == 30:
		_input_skill(arena, str(rule.skill))
	elif rule.id in ["guard", "dodge"]:
		if tick == 30:
			_input_skill(npc, "痛击")
		if rule.id == "guard" and tick == 36:
			_input_skill(arena, "架招")
			# Saved guard visual aim is pointer based; the real guard check remains
			# the production check and its actual 0.20 s window.
			if not arena.guard_fx.is_empty():
				arena.guard_fx[0].dir = Vector2.RIGHT
		elif rule.id == "dodge" and tick == 40:
			_input_skill(arena, "错步")
	elif rule.id in ["blood_qi", "blood_wave"]:
		if tick == 30:
			_input_skill(arena, "催血")
		if rule.id == "blood_qi" and tick == 108:
			_input_skill(arena, "拳掌")
		elif rule.id == "blood_wave":
			if tick == 108:
				_input_skill(arena, MANUAL_RULES.SKILL)
			if tick == 126:
				arena.held_basic_attack = true
				_input_skill(arena, "拳掌")
			if tick == 270:
				arena.held_basic_attack = false


func _observe() -> void:
	if current_take.has("peak_due") and global_frame>=int(current_take.peak_due) and not current_take.has("peak_still"):
		var image := root.get_texture().get_image()
		var filename := "%s_attempt_%d_peak.png" % [cases[case_index].id, attempt]
		if image != null and image.save_png(output_dir.path_join(filename)) == OK:
			current_take["peak_still"] = filename
			current_take["peak_still_frame"] = global_frame-1
	for mark in arena.combat_texts:
		if float(mark.age) > 1.5 / fps:
			continue
		var born := global_frame - roundi(float(mark.age) * fps)
		var key := "%s/%s/%s/%s" % [mark.side, mark.kind, mark.value, born]
		if mark_keys.has(key):
			continue
		mark_keys[key] = true
		current_take.marks.append({"observed_frame":global_frame, "local_frame":case_frame,
			"kind":mark.kind, "side":mark.side, "value":mark.value,
			"critical":mark.get("critical", false)})
		if str(mark.kind) in ["damage", "block"] and int(mark.value) > 0 and not current_take.has("peak_due"):
			current_take["contact_frame"] = global_frame-1
			current_take["peak_due"] = global_frame+3
		match str(mark.kind):
			"damage": outcome_label.text = "实际伤害 %d" % int(mark.value)
			"block": outcome_label.text = "实际化解 %d" % int(mark.value)
			"evade": outcome_label.text = "闪避 · 未造成伤害"


func _has_mark(kind: String, side := "") -> bool:
	for mark in current_take.marks:
		if str(mark.kind) == kind and (side.is_empty() or str(mark.side) == side) and (kind not in ["damage", "block"] or int(mark.value) > 0):
			return true
	return false


func _feedback_director() -> Node:
	for property in arena.get_property_list():
		if str(property.name) == "impact_feedback":
			var director: Variant = arena.get("impact_feedback")
			return director if director is Node else null
	return null


func _end_take() -> void:
	var rule: Dictionary = cases[case_index]
	current_take["end_frame"] = global_frame
	current_take["end_seconds"] = float(global_frame)/fps
	current_take["after"] = _state()
	var director := _feedback_director()
	if director != null:
		if director.has_method("debug_snapshot"):
			current_take["impact_snapshot"] = director.debug_snapshot()
		if director.has_method("event_history"):
			current_take["impact_history"] = director.event_history()
	var passed := false
	match str(rule.expect):
		"damage": passed = _has_mark("damage", "npc_1")
		"block": passed = _has_mark("block", "player")
		"evade": passed = _has_mark("evade", "player") and not _has_mark("damage", "player")
		"miss": passed = current_take.marks.is_empty() and int(current_take.before.units[1].hp) == int(current_take.after.units[1].hp)
		"blood", "wave":
			current_take["manual_damage_events"] = arena.damage_events.duplicate(true)
			current_take["wave_release_count"] = arena.wave_release_count
			for event in arena.damage_events:
				if str(event.kind) == ("血气" if rule.expect == "blood" else "血浪") and int(event.get("damage", 0)) > 0:
					passed = true
	var feedback_ok := current_take.has("impact_snapshot")
	if feedback_ok:
		var snapshot: Dictionary = current_take.impact_snapshot
		if rule.expect in ["damage", "block", "blood", "wave"]:
			feedback_ok = int(snapshot.groups) > 0
		else:
			feedback_ok = int(snapshot.groups) == 0
	current_take["feedback_verified"] = feedback_ok
	passed = passed and feedback_ok
	current_take["passed"] = passed
	manifest.takes.append(current_take.duplicate(true))
	print("CAPTURE_END ", rule.id, " attempt=", attempt, " passed=", passed, " marks=", current_take.marks)
	if passed or attempt >= max_attempts:
		manifest.case_results.append({"id":rule.id, "passed":passed, "attempts":attempt})
		case_index += 1
		attempt = 1
	else:
		attempt += 1
	_write_manifest()
	if case_index >= cases.size():
		arena.set_simulation_paused(true)
		caption.text = "现有招式 · 打击反馈"
		outcome_label.text = "预览结束"
		finishing_frames = roundi(fps * 0.5)
		case_frame = -1
	else:
		_begin_take()


func _jsonable(value: Variant) -> Variant:
	if value is Dictionary:
		var result: Dictionary = {}
		for key in value:
			result[str(key)] = _jsonable(value[key])
		return result
	if value is Array:
		var result: Array = []
		for entry in value:
			result.append(_jsonable(entry))
		return result
	if value is Vector2 or value is Vector2i:
		return [value.x, value.y]
	return value


func _write_manifest() -> void:
	manifest["recorded_frames"] = global_frame
	manifest["recorded_seconds"] = float(global_frame)/fps
	var file := FileAccess.open(output_dir.path_join("manifest.json"), FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(_jsonable(manifest), "\t"))


func _process(_delta: float) -> bool:
	if not active or finished:
		return false
	global_frame += 1
	if finishing_frames >= 0:
		finishing_frames -= 1
		if finishing_frames <= 0:
			finished = true
			var passed := true
			for result in manifest.case_results:
				passed = passed and bool(result.passed)
			manifest["passed"] = passed
			_write_manifest()
			print("COMBAT_IMPACT_CAPTURE ", "PASS" if passed else "FAIL", " ", output_dir)
			quit(0 if passed else 1)
		return false
	if warmup_frames > 0:
		warmup_frames -= 1
		if warmup_frames == 0:
			case_index = 0
			attempt = 1
			_begin_take()
		return false
	if case_frame >= 0:
		_observe()
		case_frame += 1
		if case_frame >= roundi(float(cases[case_index].frames) * fps / 60.0):
			_end_take()
		else:
			_schedule()
	return false
