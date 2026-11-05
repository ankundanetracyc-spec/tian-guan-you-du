extends SceneTree
## The existing side-view trial is the visual stage. Combat and AI never advance.
## Only the real idle pose and standalone blood-wave presentation are sampled.

const Wave = preload("res://tools/action_battle_test/blood_wave_vfx.gd")
const ARENA := "res://tscn/scene/ActionBattleOriginalWandererScene.tscn"
const VIEW_SIZE := Vector2i(1280, 720)
const LOOP_SECONDS := 3.2
var arena: Node2D
var wave: Node2D
var capture_dir := ""
var capture_frames := 96
var capture_fps := 30.0
var direction := Vector2.RIGHT
var preview_clock := 0.0
var running := false
var _space_was_down := false
var _left_was_down := false
var _right_was_down := false
var _combat_before: Array = []


func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			capture_dir = argument.trim_prefix("--capture-dir=").replace("\\", "/")
		elif argument.begins_with("--frames="):
			capture_frames = maxi(1, argument.trim_prefix("--frames=").to_int())
		elif argument.begins_with("--fps="):
			capture_fps = maxf(1.0, argument.trim_prefix("--fps=").to_float())
		elif argument == "--left" or argument == "--direction=left":
			direction = Vector2.LEFT
		elif argument.begins_with("--direction-deg="):
			direction = Vector2.RIGHT.rotated(deg_to_rad(argument.trim_prefix("--direction-deg=").to_float()))
	call_deferred("setup")


func setup() -> void:
	root.size = VIEW_SIZE
	root.content_scale_size = VIEW_SIZE
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.title = "赤潮催劲 · 同场景血浪美术候选"
	arena = load(ARENA).instantiate()
	root.add_child(arena)
	current_scene = arena
	# Freeze before the first process frame. The standalone wave is advanced by
	# this SceneTree explicitly, even though its parent stage is disabled.
	arena.process_mode = Node.PROCESS_MODE_DISABLED
	arena.set_process_input(false)
	arena.set_process_unhandled_input(false)
	arena.player = Vector2(1470, 1000)
	arena.enemy = arena.player + _ground_project(direction * 310.0) + Vector2(0, 4)
	arena.facing = -1.0 if direction.x < -0.001 else 1.0
	arena.wolf_facing = -arena.facing
	arena.wolf_state = "recover"
	arena.wolf_clock = 10.0
	arena.visual_motion_velocity = Vector2.ZERO
	arena.visual_motion_delta = 0.0
	arena.update_skinned_player()
	arena.update_skinned_wolf(0.0)
	if arena.battle_hud != null:
		arena.battle_hud.visible = false
	if arena.skill_vfx != null:
		arena.skill_vfx.visible = false
	# Same camera / world and character scales as the trial; the close framing
	# fits the entire 220-unit sector plus the original character and opponent.
	arena.trial_camera.zoom = Vector2.ONE * 2.0
	arena.trial_camera.position = arena.player + _ground_project(direction * 85.0) + Vector2(0, -37)
	arena.trial_camera.position_smoothing_enabled = false
	arena.trial_camera.force_update_scroll()
	arena.queue_redraw()
	wave = Wave.new()
	wave.auto_advance = false
	wave.y_projection = 0.46
	wave.visual_scale = 1.0
	wave.crest_lift = 14.0
	wave.z_index = 5
	arena.add_child(wave)
	_build_caption()
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_set_size(VIEW_SIZE)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	if not capture_dir.is_empty():
		root.position = Vector2i(-4096, -4096)
		root.gui_disable_input = true
	_combat_before = _combat_snapshot()
	running = true
	replay()
	if not capture_dir.is_empty():
		capture()


func _build_caption() -> void:
	var layer := CanvasLayer.new()
	layer.layer = 50
	root.add_child(layer)
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei", "Noto Sans CJK SC"])
	var caption := Label.new()
	caption.position = Vector2(26, 22)
	caption.text = "赤潮催劲 · 血浪美术候选"
	caption.add_theme_font_override("font", font)
	caption.add_theme_font_size_override("font_size", 24)
	caption.add_theme_color_override("font_color", Color("f1e7dc"))
	caption.add_theme_color_override("font_outline_color", Color("36302e"))
	caption.add_theme_constant_override("outline_size", 5)
	layer.add_child(caption)
	if capture_dir.is_empty():
		var hint := Label.new()
		hint.position = Vector2(26, 675)
		hint.text = "鼠标左键指定任意方向   空格重播   ← / → 转向   Esc退出"
		hint.add_theme_font_override("font", font)
		hint.add_theme_font_size_override("font_size", 16)
		hint.add_theme_color_override("font_outline_color", Color("36302e"))
		hint.add_theme_constant_override("outline_size", 4)
		layer.add_child(hint)


func replay() -> void:
	preview_clock = 0.0
	wave.z_index = int(arena.player.y) + 6
	wave.play(arena.to_global(arena.player), direction, Wave.hand_plane_height(arena))


func _advance_visuals(delta: float) -> void:
	wave.advance(delta)
	# The original rig retains its real idle pose / breathing. No arena._process,
	# attack, timer, resource, collision, target or AI update is called.
	arena.motion_time += delta
	arena.visual_motion_delta = delta
	arena.visual_motion_velocity = Vector2.ZERO
	arena.update_skinned_player()
	arena.queue_redraw()


func _process(delta: float) -> bool:
	if not running or not capture_dir.is_empty():
		return false
	_advance_visuals(minf(delta, 0.05))
	preview_clock += delta
	if Input.is_physical_key_pressed(KEY_ESCAPE):
		wave.clear_visuals()
		quit()
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		var plane_origin: Vector2 = arena.player - Vector2(0, Wave.hand_plane_height(arena))
		var screen_delta: Vector2 = arena.get_global_mouse_position() - plane_origin
		if screen_delta.length_squared() > 100.0:
			var aim := Vector2(screen_delta.x, screen_delta.y / 0.46).normalized()
			if aim.dot(direction) < 0.999:
				set_direction(aim)
	var space_down := Input.is_physical_key_pressed(KEY_SPACE)
	var left_down := Input.is_physical_key_pressed(KEY_LEFT)
	var right_down := Input.is_physical_key_pressed(KEY_RIGHT)
	if left_down and not _left_was_down:
		set_direction(Vector2.LEFT)
	elif right_down and not _right_was_down:
		set_direction(Vector2.RIGHT)
	elif (space_down and not _space_was_down) or preview_clock >= LOOP_SECONDS:
		replay()
	_space_was_down = space_down
	_left_was_down = left_down
	_right_was_down = right_down
	return false


func set_direction(value: Vector2) -> void:
	direction = value
	arena.facing = -1.0 if direction.x < -0.001 else 1.0
	arena.enemy = arena.player + _ground_project(direction * 310.0) + Vector2(0, 4)
	arena.wolf_facing = -arena.facing
	arena.update_skinned_player()
	arena.update_skinned_wolf(0.0)
	arena.trial_camera.position = arena.player + _ground_project(direction * 85.0) + Vector2(0, -37)
	arena.trial_camera.force_update_scroll()
	_combat_before = _combat_snapshot()
	replay()


func _ground_project(point: Vector2) -> Vector2:
	return Vector2(point.x, point.y * 0.46)


func _combat_snapshot() -> Array:
	return [arena.hp, arena.enemy_hp, arena.stamina, arena.mana, arena.qi,
		arena.qi_blood, arena.player, arena.enemy, arena.skill_cooldowns.duplicate(true),
		arena.pending_attack, arena.pending_self_skill, arena.self_cast_remaining,
		arena.release_timer, arena.elapsed]


func capture() -> void:
	if DirAccess.make_dir_recursive_absolute(capture_dir) != OK:
		push_error("Cannot create blood-wave capture directory: " + capture_dir)
		quit(2)
		return
	for i in range(3):
		await process_frame
	for frame in range(capture_frames):
		if frame > 0:
			_advance_visuals(1.0 / capture_fps)
		await process_frame
		await RenderingServer.frame_post_draw
		var shot := root.get_texture().get_image()
		if shot == null or shot.is_empty():
			push_error("A rendered window is required for blood-wave capture")
			quit(3)
			return
		if shot.save_png(capture_dir.path_join("frame_%03d.png" % frame)) != OK:
			push_error("Cannot save blood-wave capture frame")
			quit(4)
			return
	var checks := 0
	var after := _combat_snapshot()
	for i in range(_combat_before.size()):
		checks += 1
		if after[i] != _combat_before[i]:
			push_error("The blood-wave art sample changed frozen combat state at index %d" % i)
			quit(5)
			return
	# A complete capture must end naturally; short deliberate captures still
	# verify explicit cancellation below without pretending to test completion.
	if float(capture_frames - 1) / capture_fps >= wave.duration():
		checks += 1
		if wave.playing:
			push_error("The completed blood-wave sample is still playing")
			quit(6)
			return
		for band in wave.get_children():
			if band is MeshInstance2D:
				checks += 1
				if band.visible:
					push_error("A completed blood-wave band remains visible")
					quit(7)
					return
	wave.clear_visuals()
	checks += 1
	if wave.playing:
		push_error("Blood-wave explicit cleanup failed")
		quit(8)
		return
	for band in wave.get_children():
		if band is MeshInstance2D:
			checks += 1
			if band.visible:
				push_error("A cleared blood-wave band remains visible")
				quit(9)
				return
	print("BLOOD_WAVE_INGAME_CAPTURE_OK frames=%d fps=%s direction_degrees=%s combat_state_unchanged=true checks=%d" % [capture_frames, capture_fps, rad_to_deg(direction.angle()), checks])
	quit(0)
