extends Node2D

var battle: Node2D
var records: Array = []

func key(code: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)

func aim(right: bool) -> void:
	Input.warp_mouse(Vector2(970 if right else 310, 350))

func strike() -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = Vector2(970, 350)
	event.pressed = true
	Input.parse_input_event(event)
	var release := event.duplicate()
	release.pressed = false
	Input.parse_input_event(release)

func _ready() -> void:
	Engine.max_fps = 30
	battle = load("res://ActionBattleFeelTest.tscn").instantiate()
	battle.original_wanderer_enabled = true
	battle.original_wanderer_art_folder = "yuxu_unified_v14"
	add_child(battle)
	battle.player = Vector2(1500, 950)
	battle.enemy = Vector2(2350, 970)
	battle._update_trial_camera()
	aim(true)
	var output := ProjectSettings.globalize_path("res://output/yuxu_unified_v14_arena")
	DirAccess.make_dir_recursive_absolute(output)
	for warmup in range(5):
		await get_tree().process_frame
	var start := Time.get_ticks_msec()
	for frame in range(240):
		match frame:
			24: key(KEY_D, true)
			84:
				key(KEY_D, false)
				key(KEY_A, true)
				aim(false)
			126: aim(true)
			156: key(KEY_A, false)
			160, 174: strike()
			184:
				key(KEY_D, true)
				key(KEY_SPACE, true)
			185: key(KEY_SPACE, false)
			218: key(KEY_D, false)
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var screenshot := get_viewport().get_texture().get_image()
		# Export the actual full viewport at presentation resolution; no scene
		# replacement, character cutout, fixed-pose translation or camera change.
		screenshot.resize(1280, 720, Image.INTERPOLATE_LANCZOS)
		assert(screenshot.save_jpg(output.path_join("frame_%04d.jpg" % frame), 0.95) == OK)
		var selected: Array = battle._current_player_animation()
		records.append({"frame": frame, "ms": Time.get_ticks_msec()-start, "player": [battle.player.x, battle.player.y], "facing": battle.facing, "clip": selected[0], "phase": selected[1], "backward": battle.moving_backward})
	key(KEY_A, false)
	key(KEY_D, false)
	key(KEY_SPACE, false)
	var file := FileAccess.open(output.path_join("frames.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(records, "  "))
	file.close()
	print("ARENA INPUT CAPTURE COMPLETE: ", records.size(), " actual frames, ", Time.get_ticks_msec()-start, " ms")
	get_tree().quit()
