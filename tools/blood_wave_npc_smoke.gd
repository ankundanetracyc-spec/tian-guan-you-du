extends SceneTree
## Actual menu -> three-NPC battle -> menu regression with rendered evidence.

var checks := 0
var failures := 0
var output_dir := "res://output/blood_wave_npc_trial_20261104"


func _initialize() -> void:
	call_deferred("verify")


func check(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		push_error(label)


func _window() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1280, 720)
	root.content_scale_size = Vector2i(1280, 720)
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true


func press(arena: Node, keycode: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.pressed = true
	# F1 belongs to the HUD's existing input handler in the real scene tree.
	if keycode == KEY_F1:
		arena.battle_hud._input(event)
	else:
		arena._input(event)


func _save(name: String) -> void:
	for frame in range(3):
		await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	check(image != null and not image.is_empty(), "actual rendered frame is available: " + name)
	if image != null and not image.is_empty():
		check(image.save_png(output_dir.path_join(name + ".png")) == OK, "screenshot saved: " + name)


func _resource_state(arena: Node) -> Dictionary:
	var fighters: Array = [arena]
	fighters.append_array(arena.npcs)
	var state := {"fighters": [], "snapshots": []}
	for fighter in fighters:
		state.fighters.append({"hp": fighter.hp, "stamina": fighter.stamina,
			"mana": fighter.mana, "blood": fighter.qi_blood, "qi": fighter.qi,
			"cooldowns": fighter.skill_cooldowns.duplicate(true),
			"pending_attack": fighter.pending_attack, "pending_self_skill": fighter.pending_self_skill})
	for index in range(4):
		state.snapshots.append(arena.attribute_runtime.get_unit_state(index).duplicate(true))
	return state


func _identities(arena: Node) -> Array:
	var result := []
	for index in range(4):
		result.append(arena.attribute_runtime.get_unit_state(index).uid)
	return result


func _clear(arena: Node) -> bool:
	for wave in arena.blood_waves:
		if wave.playing or wave.visible:
			return false
		if wave._ink != null and wave._ink.active_count() != 0:
			return false
	return true


func _end() -> void:
	print("BLOOD_WAVE_NPC_TRIAL_%s checks=%d failures=%d" % ["OK" if failures == 0 else "FAIL", checks, failures])
	quit(0 if failures == 0 else 1)


func verify() -> void:
	_window()
	check(DirAccess.make_dir_recursive_absolute(output_dir) == OK, "capture directory is writable")
	var menu: Node = load("res://tscn/scene/StartMenu.tscn").instantiate()
	root.add_child(menu)
	current_scene = menu
	await process_frame
	_window()
	menu.get_node("StartMenuMenu/DemoToggle").emit_signal("pressed")
	await process_frame
	var entry: Button = menu.find_child("BloodWaveNpcTrial", true, false)
	check(entry != null and entry.text.contains("血浪"), "homepage has the actual blood-wave NPC entry")
	check(menu.find_child("CombatAndVfxPreviews", true, false) != null, "homepage groups combat and VFX previews")
	await _save("menu_grouped")
	if entry == null:
		_end()
		return
	entry.emit_signal("pressed")
	for frame in range(3):
		await process_frame
	var arena: Node = current_scene
	check(arena != null and arena.name == "BloodWaveNpcTrialScene", "menu opens the actual blood-wave NPC scene")
	if arena == null or arena.name != "BloodWaveNpcTrialScene":
		_end()
		return
	_window()
	arena.set_process(false)
	arena.set_process_input(false)
	arena.battle_hud.set_process_input(false)
	check(arena.npcs.size() == 3 and arena.attribute_runtime.get_unit_count() == 4,
		"scene has three NPC opponents and four isolated human snapshots")
	check(arena.npcs.all(func(npc): return npc.hp > 0 and npc.ai_enabled and npc.original_wanderer != null),
		"all three generated NPCs have live bodies and active AI")
	check(arena.skinned_wolf == null and arena.wolf_idle == null and arena.wolf_attack == null,
		"the blood-wave scene creates no wolf")
	check(arena.simulation_paused and not arena.has_started and arena.hp == 100,
		"entry begins in the existing prepared state")
	check(arena.blood_waves.size() == 4 and _clear(arena), "bounded visual slots start clear")
	check(arena.skill_slots == ["痛击", "架招", "催血", "聚炁", ""], "the existing six-action setup is retained")
	var initial_ids := _identities(arena)
	check(not arena.preview_blood_wave(Vector2.RIGHT), "wave preview is blocked before starting")
	await _save("ready")
	press(arena, KEY_P)
	check(not arena.simulation_paused and arena.has_started, "P starts the existing NPC simulation")
	var before := _resource_state(arena)
	press(arena, KEY_V)
	check(arena.wave_release_count == 1, "V starts a visual wave")
	check(_resource_state(arena) == before, "V leaves every fighter's HP, resources, cooldowns and pending actions unchanged")
	check(arena.preview_blood_wave(Vector2(1, -0.4)), "explicit arbitrary-direction preview starts")
	check(arena.wave_release_count == 2 and _resource_state(arena) == before,
		"explicit preview has no damage or payment side effects")
	for tick in range(5):
		arena._process(0.05)
	var wave: Node = arena.blood_waves[0]
	check(wave.playing and wave.age > 0.20 and wave._ink != null and wave._ink.active_count() > 0,
		"normal scene ticking advances both wave and ink particles")
	var age: float = wave.age
	var particle_clock: float = wave._ink.clock
	var active_count: int = wave._ink.active_count()
	press(arena, KEY_P)
	check(arena.simulation_paused, "P pauses the NPC simulation")
	var paused_state := _resource_state(arena)
	arena._process(0.35)
	check(is_equal_approx(wave.age, age) and is_equal_approx(wave._ink.clock, particle_clock),
		"pause freezes wave age and particle clock")
	check(wave._ink.active_count() == active_count and _resource_state(arena) == paused_state,
		"pause preserves particle population and all combat clocks/resources")
	var released_origin: Vector2 = wave.release_origin
	var old_particle_origin := Vector2.ZERO
	var has_particle := false
	for particle in wave._ink._pool:
		if particle.alive:
			old_particle_origin = particle.origin
			has_particle = true
			break
	arena.player += Vector2(70, 25)
	arena.update_skinned_player()
	arena._update_trial_camera(0.0, true)
	check(wave.release_origin == released_origin and wave.release_origin != arena.player,
		"moving the player does not move the released wave origin")
	check(has_particle and wave._ink._pool.any(func(particle): return particle.alive and particle.origin == old_particle_origin),
		"moving the player preserves previously emitted ink origins")
	press(arena, KEY_P)
	check(not arena.simulation_paused, "P resumes after pause")
	var npc_positions: Array = arena.npcs.map(func(npc): return npc.player)
	for tick in range(8):
		arena._process(1.0 / 30.0)
	check(arena.npcs.map(func(npc): return npc.player) != npc_positions, "existing NPC AI really advances toward the player")
	check(wave.age > age and wave._ink.clock > particle_clock, "resuming advances wave and particles again")
	arena._update_trial_camera(0.0, true)
	await _save("wave_with_npcs")
	press(arena, KEY_F1)
	check(arena.battle_hud.help_open and arena.combat_paused(), "F1 opens help and freezes simulation")
	await _save("help")
	press(arena, KEY_ESCAPE)
	check(not arena.battle_hud.help_open and current_scene == arena, "Esc closes help before leaving the scene")
	press(arena, KEY_T)
	check(arena.simulation_paused and not arena.has_started and arena.wave_release_count == 0 and _clear(arena),
		"T retains the existing restart behavior and clears every wave and particle")
	check(_identities(arena) == initial_ids, "restart keeps the same NPC identities")
	press(arena, KEY_P)
	check(arena.preview_blood_wave(Vector2(0.3, 1.0)), "preview can restart after T")
	arena._process(0.10)
	arena.clear_finished_battle()
	check(_clear(arena), "battle-finish cleanup removes all waves and ink")
	press(arena, KEY_ESCAPE)
	for frame in range(3):
		await process_frame
	_window()
	check(current_scene != null and current_scene.name == "StartMenu", "Esc safely returns to homepage")
	check(not is_instance_valid(arena), "leaving releases the entire NPC trial")
	_end()
