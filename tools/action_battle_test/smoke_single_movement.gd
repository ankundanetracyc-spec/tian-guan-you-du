extends SceneTree

## Gameplay regression for withdrawing the temporary double-tap fast mode.
## Visual/AI callbacks are stubbed; movement, stamina, input and timers are real.
class MovementArena extends "action_battle_feel_test.gd":
	var replay_direction := Vector2.RIGHT
	func _ready(): pass
	func movement_direction() -> Vector2: return replay_direction
	func update_skinned_player(): pass
	func update_skinned_wolf(_delta: float) -> void: pass
	func update_wolf(_delta: float, _previous_player: Vector2 = Vector2.INF): pass
	func _update_trial_camera(_delta := 0.0, _snap := false) -> void: pass

var checks := 0
var failures: Array[String] = []

func verify(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		push_error(label)

func _initialize() -> void:
	call_deferred("run_checks")

func reset_case(battle) -> void:
	battle.player = Vector2(400,500)
	battle.enemy = Vector2(1100,500)
	battle.hp = 100
	battle.enemy_hp = 100
	battle.stamina = 6.0
	battle.stamina_clock = 0.0
	battle.move_speed = 235.0
	battle.configuring = false
	battle.pending_self_skill = ""
	battle.self_cast_remaining = 0.0
	battle.pending_attack = ""
	battle.dash_move = 0.0
	battle.replay_direction = Vector2.RIGHT

func key_event(pressed: bool) -> InputEventKey:
	var event := InputEventKey.new()
	event.physical_keycode = KEY_D
	event.keycode = KEY_D
	event.pressed = pressed
	return event

func run_checks() -> void:
	var battle := MovementArena.new()
	root.add_child(battle)
	battle.set_process(false)
	reset_case(battle)
	var properties := []
	for property in battle.get_property_list(): properties.append(str(property.name))
	verify("fast_step_enabled" not in properties and "fast_step" not in properties and "fast_step_seconds_this_frame" not in properties,"temporary fast-step state removed")
	verify(not battle.has_method("ensure_fast_step") and not battle.has_method("clear_fast_step_input") and not battle.has_method("is_fast_stepping"),"temporary fast-step input API removed")
	verify(not battle.has_method("update_movement_stamina"),"temporary movement drain branch removed")
	for speed in [114.0,235.0]:
		reset_case(battle)
		battle.move_speed = speed
		var start: Vector2 = battle.player
		for frame in range(20): battle._process(0.05)
		verify(absf(battle.player.x-start.x-speed)<0.01,"ordinary one-second movement uses original base "+str(speed))
		verify(battle.stamina==6.0,"ordinary movement does not spend stamina at "+str(speed))
		reset_case(battle)
		battle.move_speed = speed
		battle._input(key_event(true))
		battle._input(key_event(false))
		battle._input(key_event(true))
		start = battle.player
		for frame in range(20): battle._process(0.05)
		verify(absf(battle.player.x-start.x-speed)<0.01,"repeated direction presses retain one speed at "+str(speed))
		verify(battle.stamina==6.0,"repeated direction presses do not add movement cost")
	reset_case(battle)
	battle.replay_direction = Vector2(1,-1)
	var start: Vector2 = battle.player
	for frame in range(20): battle._process(0.05)
	verify(absf(battle.player.distance_to(start)-235.0)<0.01,"ordinary diagonal movement stays normalized")
	reset_case(battle)
	battle.stamina = 3.25
	for frame in range(39): battle._process(0.05)
	verify(is_equal_approx(battle.stamina,3.25),"moving below full stamina has no drain or early recovery")
	battle._process(0.05)
	verify(is_equal_approx(battle.stamina,4.25),"original one-point-per-two-second recovery continues while moving")
	reset_case(battle)
	battle.stamina = 0.0
	start = battle.player
	battle._process(0.05)
	verify(absf(battle.player.x-start.x-235.0*0.05)<0.01 and battle.stamina==0.0,"zero stamina still permits ordinary movement")
	reset_case(battle)
	battle.stamina = 5.5
	battle.replay_direction = Vector2.ZERO
	for frame in range(40): battle._process(0.05)
	verify(battle.stamina==6.0,"idle recovery retains cap six")
	reset_case(battle)
	battle.pending_self_skill = "聚炁"
	battle.self_cast_remaining = 1.0
	start = battle.player
	battle.move_character(Vector2.RIGHT,0.1)
	verify(absf(battle.player.x-start.x-235.0*0.1*0.5)<0.01,"existing casting half-speed restriction retained")
	verify(battle.stamina==6.0,"casting movement adds no continuous movement charge")
	battle.player = start
	battle.self_cast_remaining = 0.03
	battle.move_character(Vector2.RIGHT,0.05)
	verify(absf(battle.player.x-start.x-235.0*(0.03*0.5+0.02))<0.01,"cast completion frame retains split movement timing")
	reset_case(battle)
	battle.dash_direction = Vector2.RIGHT
	battle.dash_move = 0.20
	start = battle.player
	battle._process(0.05)
	verify(absf(battle.player.x-start.x-500.0*0.05)<0.01 and battle.stamina==6.0,"dedicated dodge translation has no extra movement-mode multiplier or charge")
	reset_case(battle)
	battle.stamina = 4.5
	battle.stamina_clock = 1.0
	battle.configuring = true
	start = battle.player
	for frame in range(10): battle._process(0.05)
	verify(battle.player==start and battle.stamina==4.5 and battle.stamina_clock==1.0,"configuration pause freezes movement and resource recovery")
	var hud = load("res://action_battle_hud.gd").new()
	hud.battle = battle
	verify(is_equal_approx(hud.stamina_cell_fill(4),0.5) and hud.stamina_cell_fill(5)==0.0,"HUD retains true fractional stamina without recovery preview fill")
	battle.configuring = false
	battle.clear_finished_battle()
	battle.restart_battle()
	verify(battle.stamina==6.0 and battle.stamina_clock==0.0,"battle cleanup and restart work without removed movement-mode API")
	hud.free()
	battle.free()
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"sample_speed":114.0,"character_speed":235.0,"ordinary_movement_stamina_cost":0.0,"stamina_recovery_interval":2.0}
	var file := FileAccess.open("res://output/single_movement_withdrawal_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("SINGLE MOVEMENT WITHDRAWAL ",checks," checks; failures=",failures.size())
	quit(0 if failures.is_empty() else 1)
