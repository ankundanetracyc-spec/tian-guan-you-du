extends Node2D

## Deterministic replay of the real arena, not a replacement fitting actor.
const ARENA := "res://tscn/scene/ActionBattleOriginalWandererScene.tscn"
const OUTPUT := "res://output/guigu_native_first_20261105/moving_attack_game"

func _key(code: Key, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)

func _click(arena: Node2D, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	arena._input(event)

func _ready() -> void:
	var destination := ProjectSettings.globalize_path(OUTPUT)
	DirAccess.make_dir_recursive_absolute(destination)
	FileAccess.open(OUTPUT.path_join(".gdignore"),FileAccess.WRITE).store_string("")
	var arena: Node2D = load(ARENA).instantiate()
	add_child(arena)
	arena.set_process(false)
	arena.set_process_input(false)
	get_window().mode = Window.MODE_WINDOWED
	get_window().size = Vector2i(1280,720)
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	arena.player = Vector2(1400,950)
	arena.enemy = Vector2(2800,970)
	arena._update_trial_camera(0.0,true)
	await get_tree().process_frame
	var skin: RefCounted = arena.original_wanderer.wanderer_skin
	var records := []
	var failures := []
	var stages := {}
	var swing_forms := {}
	var moving_attack_frames := 0
	for frame in range(300):
		# Offscreen focus changes can clear Godot's held-input state. Replay
		# the held state every simulation frame without touching OS input.
		_key(KEY_RIGHT,frame>=15 and frame<120)
		_key(KEY_LEFT,frame>=144 and frame<207)
		_click(arena,frame>=30 and frame<104)
		if frame==144:
			arena.set_outfit_sword_visible(true)
		if frame in [153,180,222,252]:
			if not arena.preview_outfit_swing(): failures.append("trial swing rejected at frame %d"%frame)
		if frame==282: arena.set_outfit_sword_visible(false)
		arena.inspection_facing_override = -1.0 if frame>=144 and frame<222 else 1.0
		# Keep capture aim deterministic while the offscreen window has no cursor.
		# No timing, damage, reach, cost or displacement rules are overridden.
		if arena.fist_combo!=null and arena.fist_combo.active:
			arena.fist_combo.direction = Vector2.RIGHT
		arena._process(1.0/30.0)
		if bool(skin.compose_state.get("moving_attack",false)): moving_attack_frames += 1
		if arena.fist_combo!=null and arena.fist_combo.active: stages[arena.fist_combo.stage] = true
		if skin.approved_sword.mode=="swing":
			swing_forms[int(skin.approved_sword.last.form_index)] = str(skin.approved_sword.last.form_name)
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		if get_viewport().get_texture().get_image().save_png(destination.path_join("frame_%03d.png"%frame))!=OK:
			failures.append("capture failed at %d"%frame)
		if frame%3==0:
			records.append({"frame":frame,"player":[arena.player.x,arena.player.y],"facing":arena.facing,"clip":arena._current_player_animation(),"combo_stage":arena.fist_combo.stage if arena.fist_combo!=null and arena.fist_combo.active else -1,"composition":skin.compose_state.duplicate(),"contact":skin.get_contact_diagnostics(),"hp":arena.hp,"enemy_hp":arena.enemy_hp,"sword":skin.approved_sword.get_diagnostics()})
		if frame%60==0: print("MOVING ATTACK GAME FRAME ",frame," / 300")
	_key(KEY_RIGHT,false)
	_key(KEY_LEFT,false)
	_click(arena,false)
	if stages.size()!=4: failures.append("did not observe all four actual attack stages")
	if moving_attack_frames<40: failures.append("insufficient actual moving-attack frames")
	if swing_forms.size()!=4: failures.append("did not observe all four distinct inspection swing forms")
	FileAccess.open(OUTPUT.path_join("capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":failures.is_empty(),"frames":300,"fps":30,"scene":ARENA,"native_window":[1280,720],"ordinary_move_speed":arena.move_speed,"gait_reference_speed":skin.ROBED_MOVE_SPEED,"swing_forms":swing_forms,"actual_combo_stages":stages.keys(),"moving_attack_frames":moving_attack_frames,"sequence":"idle0-14;235run15-119; held actual left-button combo30-104; stop120-143; left moving sword144-206 with outward/return horizontal swings153/180; left hold207-221; right downcut/upcut swings222/252; sheathe282","scope":"Real root wolf arena, input, combo runtime, camera, HUD and shared native avatar. Facing and fist aim controlled for offscreen recording only; four3D sword forms remain no-damage inspection actions.","failures":failures,"records":records},"\t"))
	print("MOVING ATTACK GAME CAPTURE ","PASS" if failures.is_empty() else "FAIL",": ",failures)
	get_tree().quit(0 if failures.is_empty() else 1)
