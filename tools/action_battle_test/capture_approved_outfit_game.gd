extends Node2D

## Capture the actual root-project arena with deterministic keyboard replay.
## The scene, simulation, camera, HUD, AI and character runtime remain native.
const ARENA := "res://tscn/scene/ActionBattleOriginalWandererScene.tscn"
const OUTPUT := "res://output/guigu_native_first_20261105/actual_game"

func _key(code: Key, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)

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
	arena.enemy = Vector2(2200,970)
	arena._update_trial_camera(0.0,true)
	await get_tree().process_frame
	var records := []
	var failures := []
	for frame in range(300):
		if frame==30: _key(KEY_RIGHT,true)
		if frame==105: _key(KEY_RIGHT,false)
		if frame==135:
			arena.set_outfit_sword_visible(true)
			_key(KEY_LEFT,true)
		if frame==195: _key(KEY_LEFT,false)
		if frame in [210,246]:
			if not arena.preview_outfit_swing(): failures.append("trial swing rejected at frame %d"%frame)
		if frame==282: arena.set_outfit_sword_visible(false)
		var left := frame>=135 and frame<210
		arena.inspection_facing_override = -1.0 if left else 1.0
		arena._process(1.0/30.0)
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var screenshot := get_viewport().get_texture().get_image()
		if screenshot.save_png(destination.path_join("frame_%03d.png"%frame))!=OK:
			failures.append("capture failed at %d"%frame)
		if frame%15==0:
			var skin: Variant = arena.original_wanderer.wanderer_skin
			records.append({"frame":frame,"player":[arena.player.x,arena.player.y],"facing":arena.facing,"clip":arena._current_player_animation(),"hp":arena.hp,"enemy_hp":arena.enemy_hp,"sword":skin.approved_sword.get_diagnostics(),"garment":skin.get_garment_diagnostics()})
		if frame%60==0: print("GAME OUTFIT NATIVE FRAME ",frame," / 300")
	_key(KEY_RIGHT,false)
	_key(KEY_LEFT,false)
	FileAccess.open(OUTPUT.path_join("capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"passed":failures.is_empty(),"frames":300,"fps":30,"scene":ARENA,"native_window":[1280,720],"sequence":"idle0-29; ordinary run30-104; stop105-134; sword left run135-194; left hold195-209; two right single trial swings210-281; sheathe282-299","scope":"Actual root-project wolf arena and shared runtime; deterministic keyboard and facing replay (offscreen pointer is not controllable), no replacement illustration or copied fitting actor","failures":failures,"records":records},"\t"))
	print("GAME OUTFIT NATIVE CAPTURE ","PASS" if failures.is_empty() else "FAIL",": ",destination)
	get_tree().quit(0 if failures.is_empty() else 1)
