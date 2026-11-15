extends SceneTree

var checks := 0
var failures: Array[String] = []

func verify(ok: bool, description: String) -> void:
	checks += 1
	if not ok:
		failures.append(description)
		push_error(description)

func key_event(key: int, pressed: bool, echo := false) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = key
	event.pressed = pressed
	event.echo = echo
	return event

func double_tap(step, key := KEY_D, start := 0.0) -> void:
	step.feed_key(key_event(key,true),start)
	step.feed_key(key_event(key,false),start+0.06)
	step.feed_key(key_event(key,true),start+0.16)

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var step = load("res://double_tap_fast_step.gd").new()
	step.feed_key(key_event(KEY_D,true),0.0)
	verify(not step.wants_fast_step(Vector2.RIGHT),"first press is ordinary movement")
	step.feed_key(key_event(KEY_D,true,true),0.1)
	step.feed_key(key_event(KEY_D,true),0.15)
	verify(not step.wants_fast_step(Vector2.RIGHT),"held or echo key is not a double tap")
	step.clear()
	double_tap(step)
	verify(step.wants_fast_step(Vector2.RIGHT),"same-direction double tap starts held fast step")
	verify(step.wants_fast_step(Vector2(1,-1).normalized()),"diagonal movement retains fast step while trigger is held")
	step.feed_key(key_event(KEY_D,false),0.2)
	verify(not step.wants_fast_step(Vector2.RIGHT),"release ends fast step")
	step.clear()
	step.feed_key(key_event(KEY_D,true),0.0)
	step.feed_key(key_event(KEY_D,false),0.05)
	step.feed_key(key_event(KEY_D,true),0.4)
	verify(not step.wants_fast_step(Vector2.RIGHT),"late press does not trigger fast step")
	step.clear()
	step.feed_key(key_event(KEY_D,true),0.0)
	step.feed_key(key_event(KEY_D,false),0.05)
	step.feed_key(key_event(KEY_A,true),0.15)
	verify(not step.wants_fast_step(Vector2.LEFT),"different direction does not trigger fast step")
	step.clear()
	double_tap(step,KEY_UP)
	verify(step.wants_fast_step(Vector2.UP),"arrow keys support the same double tap")
	step.feed_key(key_event(KEY_DOWN,true),0.2)
	verify(not step.wants_fast_step(Vector2.DOWN),"opposite direction cancels original fast step")
	step.clear()
	verify(not step.wants_fast_step(Vector2.UP),"cleared input cannot resume a held fast step")

	var battle = load("res://action_battle_feel_test.gd").new()
	battle.fast_step_enabled = true
	battle.fast_step = step
	battle.move_speed = 235.0
	battle.stamina = 6.0
	double_tap(step)
	battle.player = Vector2.ZERO
	battle.update_movement_stamina(Vector2.RIGHT,0.05)
	battle.move_character(Vector2.RIGHT,0.05)
	verify(is_equal_approx(battle.stamina,5.975),"fast step drains every frame at 0.5 per second")
	verify(is_equal_approx(battle.player.x,235.0*1.5*0.05),"fast step moves at 150 percent of character speed")
	verify(battle.stamina_clock==0.0,"fast step does not accumulate recovery time")
	var hud = load("res://action_battle_hud.gd").new()
	hud.battle = battle
	verify(is_equal_approx(hud.stamina_cell_fill(5),0.975),"resource bead displays fractional drain rather than removing one whole bead")
	battle.stamina = 5.5
	battle.stamina_clock = 1.0
	battle.fast_step_seconds_this_frame = 0.0
	verify(is_equal_approx(hud.stamina_cell_fill(5),0.5),"pending recovery does not inflate actual fractional stamina")
	battle.player = Vector2.ZERO
	battle.update_movement_stamina(Vector2(1,1).normalized(),0.05)
	battle.move_character(Vector2(1,1),0.05)
	verify(is_equal_approx(battle.player.length(),235.0*1.5*0.05),"diagonal fast step is normalized")
	battle.stamina = 6.0
	battle.stamina_clock = 0.0
	for frame in range(240): battle.update_movement_stamina(Vector2.RIGHT,0.05)
	verify(is_zero_approx(battle.stamina),"six stamina lasts twelve continuous fast-step seconds")
	verify(not step.wants_fast_step(Vector2.RIGHT),"exhaustion cancels held fast-step request")
	battle.update_movement_stamina(Vector2.RIGHT,0.05)
	verify(not battle.is_fast_stepping() and is_equal_approx(battle.stamina_clock,0.05),"exhausted held input stays ordinary and recovery resumes")

	step.clear()
	double_tap(step)
	battle.stamina = 0.01
	battle.stamina_clock = 0.0
	battle.player = Vector2.ZERO
	battle.update_movement_stamina(Vector2.RIGHT,0.05)
	battle.move_character(Vector2.RIGHT,0.05)
	verify(is_equal_approx(battle.player.x,235.0*(0.02*1.5+0.03)),"exhaustion inside a frame splits fast and ordinary movement")
	verify(battle.stamina==0.0 and is_equal_approx(battle.stamina_clock,0.03),"partial final frame neither overdraws nor loses ordinary recovery time")

	step.clear()
	double_tap(step)
	battle.stamina = 6.0
	battle.player = Vector2.ZERO
	battle.pending_self_skill = "聚炁"
	battle.self_cast_remaining = 1.0
	battle.update_movement_stamina(Vector2.RIGHT,0.05)
	battle.move_character(Vector2.RIGHT,0.05)
	verify(is_equal_approx(battle.player.x,235.0*1.5*0.5*0.05),"existing cast half speed still multiplies fast step")
	battle.pending_self_skill = ""
	battle.self_cast_remaining = 0.0
	battle.stamina = 5.0
	battle.player = Vector2.ZERO
	battle.update_movement_stamina(Vector2.RIGHT,0.05,true)
	verify(battle.stamina==5.0 and not battle.is_fast_stepping(),"dedicated dodge does not add fast-step drain or multiplier")
	battle.fast_step_seconds_this_frame = 0.0
	battle.stamina_clock = 0.0
	for frame in range(40): battle.update_movement_stamina(Vector2.ZERO,0.05)
	verify(is_equal_approx(battle.stamina,6.0),"ordinary stamina recovery remains one point per original interval")
	battle._notification(Node.NOTIFICATION_WM_WINDOW_FOCUS_OUT)
	verify(step.fast_key==0 and step.pressed_keys.is_empty(),"focus loss clears all fast-step input")
	double_tap(step)
	battle.configuring = true
	battle._process(0.05)
	verify(step.fast_key==0 and battle.visual_motion_delta==0.0,"configuration pause freezes resource simulation and clears fast step")
	battle.configuring = false
	double_tap(step)
	battle.restart_battle()
	verify(step.fast_key==0 and battle.stamina==6.0,"battle restart clears movement mode and restores the existing stamina baseline")
	hud.free()
	battle.free()
	print("DOUBLE TAP FAST STEP: ",checks," checks; failures=",failures.size())
	quit(0 if failures.is_empty() else 1)
