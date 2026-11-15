extends SceneTree
## One real three-NPC trial, captured sequentially to avoid six live rigs/worlds.
## Effects are generated through their existing skill/contact paths. Only the
## manual blood-wave preview is a presentation-only release, as in its UI.

const ARENA_SCENE := "res://tscn/scene/BloodWaveNpcTrialScene.tscn"
const RUNTIME := "res://csharp/battle/ActionHumanSparringRuntime.cs"
const VIEW_SIZE := Vector2i(1280, 720)
const CROP := Rect2i(320, 160, 720, 440)
var cases := [
	{"id": "fist", "label": "探拳 · 灰白墨劲", "skill": "拳掌", "direction": Vector2.RIGHT, "peak": 0.30},
	{"id": "combo", "label": "四段拳掌 · 斜向", "skill": "拳掌", "direction": Vector2(1.0, -0.28).normalized(), "peak": 1.97},
	{"id": "heavy", "label": "痛击 · 浅金重墨", "skill": "痛击", "direction": Vector2.LEFT, "peak": 0.31},
	{"id": "guard", "label": "架招 · 普通窗口", "skill": "架招", "direction": Vector2.RIGHT, "peak": 0.10},
	{"id": "guard_success", "label": "架招 · 真实化解", "skill": "架招", "direction": Vector2(1.0, -0.25).normalized(), "peak": 0.14},
	{"id": "blood", "label": "血浪 · 人物圆心与手高", "skill": "血浪预览", "direction": Vector2.RIGHT, "peak": 0.62},
]

class CaptureArena extends "res://tools/action_battle_test/blood_wave_npc_trial.gd":
	var fixed_aim := Vector2.RIGHT
	func _enter_trial_fullscreen(_fullscreen := true) -> void:
		# Capture remains a physical 1280x720 window; keep the arena's normal
		# content scale setup without re-entering fullscreen during _ready().
		super._enter_trial_fullscreen(false)
	func attack_aim_direction() -> Vector2:
		return fixed_aim
	func movement_direction() -> Vector2:
		return Vector2.ZERO

var arena: Node2D
var capture_view: SubViewport
var output_dir := "res://output/unified_ink_vfx_20261104"
var frames := 84
var fps := 30.0
var stills := false
var manifest := {"source_scene": ARENA_SCENE, "fps": 30.0, "frames": 84,
	"crop": [320, 160, 720, 440], "cases": [], "presentation_only_blood_wave": true}


func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			output_dir = argument.trim_prefix("--capture-dir=").replace("\\", "/")
		elif argument.begins_with("--frames="):
			frames = maxi(1, argument.trim_prefix("--frames=").to_int())
		elif argument.begins_with("--fps="):
			fps = maxf(1.0, argument.trim_prefix("--fps=").to_float())
		elif argument == "--stills":
			stills = true
	call_deferred("capture")


func _window() -> void:
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_set_size(VIEW_SIZE)
	root.mode = Window.MODE_WINDOWED
	root.size = VIEW_SIZE
	root.content_scale_size = VIEW_SIZE
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)


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
	for index in range(arena.npcs.size()):
		var npc: Node2D = arena.npcs[index]
		npc.ai_enabled = false
		npc.player = arena.player + (Vector2(rule.direction) * 55.0 if index == 0 else Vector2(-150, -110 if index == 1 else 120))
		npc.enemy = arena.player
		npc.facing = -arena.facing
		npc.walk_amount = 0.0
	arena.refresh_health()
	arena.trial_camera.zoom = Vector2.ONE * 2.0
	arena.trial_camera.position = arena.player + Vector2(60, -45)
	arena.trial_camera.position_smoothing_enabled = false
	arena.trial_camera.force_update_scroll()
	_pose(0.0)
	arena.battle_hud.visible = false
	if arena.hitbox_overlay != null:
		arena.hitbox_overlay.visible = false


func _start(rule: Dictionary) -> void:
	if rule.id == "blood":
		assert(arena.preview_blood_wave(rule.direction))
		return
	if rule.id == "guard_success":
		# The enemy begins a real heavy attack 0.10 s before the defended sample.
		# Contact occurs inside the player's actual 0.20 s guard window.
		var npc: Node2D = arena.npcs[0]
		npc.use_skill("痛击")
		npc.attack_direction = (arena.player - npc.player).normalized()
		npc.update_skill_timers(0.10)
	arena.held_basic_attack = rule.id == "combo"
	arena.use_skill(rule.skill)
	if rule.skill == "痛击":
		arena.attack_direction = rule.direction
	if rule.skill == "架招" and not arena.guard_fx.is_empty():
		# Base guard reads the desktop pointer; fix its saved visual aim only.
		arena.guard_fx[0].dir = rule.direction


func _advance(rule: Dictionary, delta: float) -> void:
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
	for wave in arena.blood_waves:
		wave.advance(delta)
	_pose(delta)
	# Still-only captures advance offscreen between peak frames. Synchronize at
	# the actual release/contact tick so pooled ink has the correct visual age.
	if arena.skill_vfx != null:
		arena.skill_vfx.sync_visuals()
	for npc in arena.npcs:
		if npc.skill_vfx != null:
			npc.skill_vfx.sync_visuals()


func _shot(path: String, full_path := "") -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var shot := capture_view.get_texture().get_image()
	assert(shot != null and not shot.is_empty(), "A real rendering window is required")
	if shot.get_size() != VIEW_SIZE:
		push_error("Capture viewport is %s; expected %s" % [shot.get_size(), VIEW_SIZE])
		quit(2)
		return
	assert(shot.get_size() == VIEW_SIZE)
	assert(shot.get_region(CROP).save_png(path) == OK)
	if not full_path.is_empty():
		assert(shot.save_png(full_path) == OK)


func _capture_case(rule: Dictionary) -> bool:
	var folder := output_dir.path_join(rule.id)
	assert(DirAccess.make_dir_recursive_absolute(folder) == OK)
	var attempts := 0
	var successful_guard := false
	var event_log: Array = []
	var event_keys := {}
	while attempts < (8 if rule.id == "guard_success" else 1):
		attempts += 1
		_reset(rule)
		_start(rule)
		var peak_frame := mini(frames - 1, int(round(float(rule.peak) * fps)))
		var guard_seen := false
		for frame in range(frames):
			if frame > 0:
				_advance(rule, 1.0 / fps)
			for visual in arena.guard_fx:
				guard_seen = guard_seen or bool(visual.success)
			for mark in arena.combat_texts:
				if str(mark.kind) == "block":
					var key := "%s|%s|%s" % [mark.kind, mark.side, mark.value]
					if not event_keys.has(key):
						event_keys[key] = true
						event_log.append({"kind": mark.kind, "side": mark.side, "value": mark.value})
			if not stills or frame == peak_frame:
				var full_path := output_dir.path_join(rule.id + "_full.png") if frame == peak_frame else ""
				await _shot(folder.path_join("frame_%03d.png" % frame), full_path)
				if frame == peak_frame:
					var peak := capture_view.get_texture().get_image().get_region(CROP)
					assert(peak.save_png(output_dir.path_join(rule.id + "_peak.png")) == OK)
			else:
				# PNG I/O is optional; real frame boundaries are not. They flush
				# mesh/skinning changes instead of queuing an entire case at once.
				await process_frame
				await RenderingServer.frame_post_draw
		if rule.id != "guard_success" or guard_seen:
			successful_guard = guard_seen
			break
	var item := {"id": rule.id, "label": rule.label, "direction": [rule.direction.x, rule.direction.y],
		"peak_frame": mini(frames - 1, int(round(float(rule.peak) * fps))),
		"guard_success_from_real_contact": successful_guard, "attempts": attempts,
		"player_hp": arena.hp, "npc_hp": arena.npcs[0].hp, "player_stamina": arena.stamina,
		"contact_feedback": event_log, "frames": frames, "stills_only": stills,
		"static_memory_mb": OS.get_static_memory_usage() / 1048576.0,
		"static_memory_peak_mb": OS.get_static_memory_peak_usage() / 1048576.0}
	manifest.cases.append(item)
	print("UNIFIED_INK_CASE ", JSON.stringify(item))
	return rule.id != "guard_success" or successful_guard


func capture() -> void:
	_window()
	assert(DirAccess.make_dir_recursive_absolute(output_dir) == OK)
	capture_view = SubViewport.new()
	capture_view.name = "UnifiedInkCaptureViewport"
	capture_view.size = VIEW_SIZE
	capture_view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(capture_view)
	# This subclass uses the actual trial script plus its exact runtime child;
	# only deterministic mouse aim and zero movement replace desktop input.
	arena = CaptureArena.new()
	var runtime: Node = load(RUNTIME).new()
	runtime.name = "HumanSparringAttributes"
	arena.add_child(runtime)
	capture_view.add_child(arena)
	current_scene = capture_view
	arena.set_process(false)
	arena.set_process_input(false)
	arena.battle_hud.set_process_input(false)
	_window()
	for frame in range(3):
		await process_frame
	# Scene setup and saved video preferences can restore fullscreen after load.
	# Reapply the requested physical window after those deferred changes finish.
	_window()
	await process_frame
	await RenderingServer.frame_post_draw
	var actual_size := capture_view.get_texture().get_image().get_size()
	print("UNIFIED_INK_VIEWPORT requested=%s actual=%s window=%s mode=%s root_mode=%s root_min=%s root_max=%s pid=%s" % [VIEW_SIZE, actual_size, DisplayServer.window_get_size(), DisplayServer.window_get_mode(), root.mode, root.min_size, root.max_size, OS.get_process_id()])
	if actual_size != VIEW_SIZE:
		push_error("The fixed capture viewport did not settle at the requested size")
		quit(2)
		return
	manifest["view_size"] = [actual_size.x, actual_size.y]
	var passed := true
	for rule in cases:
		passed = await _capture_case(rule) and passed
	manifest.fps = fps
	manifest.frames = frames
	manifest["stills_only"] = stills
	manifest["passed"] = passed
	var file := FileAccess.open(output_dir.path_join("manifest.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest, "\t"))
	file.close()
	print("UNIFIED_INK_CAPTURE_%s cases=%d frames_per_case=%d fps=%s" % ["OK" if passed else "FAIL", cases.size(), frames, fps])
	arena.queue_free()
	await process_frame
	quit(0 if passed else 1)
