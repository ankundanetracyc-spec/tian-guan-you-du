extends "human_sparring_arena.gd"
## Visual test layered over the existing three-NPC battle; no blood-wave damage.

const BloodWave = preload("blood_wave_vfx.gd")
const GROUND_PROJECTION := 0.46
var blood_waves: Array[Node2D] = []
var wave_release_count := 0


func _ready() -> void:
	super._ready()
	var hud_parent := battle_hud.get_parent()
	battle_hud.hide()
	battle_hud.set_process(false)
	battle_hud.set_process_input(false)
	battle_hud.queue_free()
	battle_hud = load(_action_test_path("blood_wave_npc_hud.gd")).new()
	battle_hud.battle = self
	hud_parent.add_child(battle_hud)
	for index in range(4):
		var wave: Node2D = BloodWave.new()
		wave.name = "BloodWavePreview%d" % index
		wave.auto_advance = false
		wave.y_projection = GROUND_PROJECTION
		wave.crest_lift = 14.0
		# Actual release depth is assigned when this slot is played.
		wave.z_index = 1
		add_child(wave)
		blood_waves.append(wave)
		wave.stop()


func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_V:
		preview_blood_wave()
		get_viewport().set_input_as_handled()
		return
	super._input(event)


func blood_wave_aim() -> Vector2:
	var plane_origin: Vector2 = player - Vector2(0, BloodWave.hand_plane_height(self))
	var point: Vector2 = get_global_mouse_position() - plane_origin
	var ground := Vector2(point.x, point.y / GROUND_PROJECTION)
	return ground.normalized() if ground.length_squared() > 0.01 else Vector2(facing, 0)


func preview_blood_wave(direction := Vector2.ZERO) -> bool:
	if combat_paused() or hp <= 0 or enemy_hp <= 0:
		return false
	var aim: Vector2 = blood_wave_aim() if direction.is_zero_approx() else direction.normalized()
	for wave in blood_waves:
		if not wave.playing:
			wave.z_index = int(player.y) + 6
			wave.play(to_global(player), aim, BloodWave.hand_plane_height(self))
			wave_release_count += 1
			return true
	return false


func _process(delta: float) -> void:
	var was_paused := combat_paused()
	super._process(delta)
	if was_paused or combat_paused() or hp <= 0 or enemy_hp <= 0:
		return
	for wave in blood_waves:
		wave.advance(delta)


func clear_blood_waves() -> void:
	for wave in blood_waves:
		if is_instance_valid(wave):
			wave.clear_visuals()


func restart_battle() -> void:
	clear_blood_waves()
	wave_release_count = 0
	super.restart_battle()


func clear_finished_battle() -> void:
	super.clear_finished_battle()
	clear_blood_waves()


func _exit_tree() -> void:
	clear_blood_waves()
	super._exit_tree()
