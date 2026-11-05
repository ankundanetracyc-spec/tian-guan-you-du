extends SceneTree

## Native-rig display envelopes and repeated-contact contract. GPU image QA is separate.
const DIRECTOR_PATH := "res://tools/action_battle_test/combat_impact_feedback.gd"
const CONTACT_SPACE = preload("human_contact_space.gd")
const EPSILON := 0.00001
const SAMPLE_STEP := 1.0 / 240.0

class ProbeFighter extends "action_battle_feel_test.gd":
	var elevation := 0.0

	func _ready() -> void:
		pass

	func body_effect_origin() -> Vector2:
		return player - Vector2(0, elevation)

	func attack_aim_direction() -> Vector2:
		return Vector2.RIGHT

var checks := 0
var failures: Array[String] = []
var director: Node2D
var source: Node2D
var target: Node2D


func _initialize() -> void:
	call_deferred("_run")


func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		push_error(label)


func _make_fighter(point: Vector2, label: String) -> Node2D:
	var fighter := ProbeFighter.new()
	fighter.name = label
	root.add_child(fighter)
	fighter.set_process(false)
	fighter.set_process_input(false)
	fighter.player = point
	fighter.hp = 100
	fighter.enemy_hp = 100
	var rig: Node2D = load("res://tools/action_battle_test/guigu_rig_runtime.gd").new()
	fighter.add_child(rig)
	var loaded: bool = rig.load_runtime("", "", "", "", "yuxu_fullbody_v15")
	_check(loaded and rig.native_source_mode, label + " loads the current native rig")
	fighter.original_wanderer = rig
	fighter.skinned_player = rig
	fighter.skinned_player_ready = loaded
	fighter.motion_time = 0.25
	fighter.visual_motion_delta = 1.0 / 60.0
	fighter.update_skinned_player()
	return fighter


func _reaction(fighter: Node2D) -> Dictionary:
	return director.reaction_snapshot(fighter)


func _report(direction := Vector2.RIGHT, skill := "拳掌", damage := 8, blocked := 0,
		attacker: Node2D = null, defender: Node2D = null) -> void:
	if attacker == null:
		attacker = source
	if defender == null:
		defender = target
	var foot: Vector2 = defender.to_global(defender.player)
	director.report_world(attacker, defender,
		{"inactive": false, "evaded": false, "damage": damage, "blocked": blocked},
		skill, "本体", foot - Vector2(0, 58), direction, foot.y)


func _strength(state: Dictionary) -> float:
	# A dimensionally comparable display signal, not a simulated pixel measurement.
	return Vector2(state.recoil).length() + absf(float(state.lean)) * 100.0 + absf(float(state.compression)) * 100.0


func _same_display(a: Dictionary, b: Dictionary) -> bool:
	return Vector2(a.recoil).distance_to(Vector2(b.recoil)) <= EPSILON \
		and absf(float(a.lean) - float(b.lean)) <= EPSILON \
		and absf(float(a.compression) - float(b.compression)) <= EPSILON


func _finite_display(state: Dictionary) -> bool:
	var recoil: Vector2 = state.recoil
	return is_finite(recoil.x) and is_finite(recoil.y) \
		and is_finite(float(state.lean)) and is_finite(float(state.compression))


func _zero_display(state: Dictionary) -> bool:
	return Vector2(state.recoil).is_zero_approx() \
		and is_zero_approx(float(state.lean)) and is_zero_approx(float(state.compression))


func _materials_match(fighter: Node2D, state: Dictionary) -> bool:
	for mesh in fighter.original_wanderer.wanderer_skin.meshes:
		if not mesh.material is ShaderMaterial:
			return false
		var material: ShaderMaterial = mesh.material
		if Vector2(material.get_shader_parameter("recoil")).distance_to(Vector2(state.recoil)) > EPSILON:
			return false
		if absf(float(material.get_shader_parameter("lean")) - float(state.lean)) > EPSILON:
			return false
		if absf(float(material.get_shader_parameter("compression")) - float(state.compression)) > EPSILON:
			return false
	return true


func _cpu_snapshot(fighter: Node2D) -> Dictionary:
	var rig: Node2D = fighter.original_wanderer
	var skin: Variant = rig.wanderer_skin
	var bones := {}
	for key in skin.bones:
		bones[key] = skin.bones[key].global_transform
	var meshes: Array = []
	for mesh in skin.meshes:
		meshes.append([mesh.global_transform, mesh.polygon.duplicate()])
	return {"player": fighter.player, "controller": fighter.global_transform, "rig": rig.global_transform,
		"skin_root": rig.rig_root_node.global_transform, "foot": skin.contact_shadow.global_transform,
		"clock": fighter.motion_time, "native_clock": skin.last_motion_clock,
		"hand_height": CONTACT_SPACE.hand_height(fighter), "body_height": CONTACT_SPACE.body_height(fighter),
		"native_physics": skin.native_physics.components.duplicate(true),
		"native_world": skin.world_transforms.duplicate(true), "bones": bones, "meshes": meshes,
		"hp": fighter.hp, "stamina": fighter.stamina, "mana": fighter.mana,
		"cooldowns": fighter.skill_cooldowns.duplicate(true)}


func _run() -> void:
	var script: Script = load(DIRECTOR_PATH)
	if script == null:
		_check(false, "shared impact director loads")
		_finish()
		return
	director = script.new()
	root.add_child(director)
	director.set_process(false)
	if not director.has_method("reaction_snapshot"):
		_check(false, "impact director exposes reaction_snapshot for display verification")
		_finish()
		return
	source = _make_fighter(Vector2(420, 360), "ReactionSource")
	target = _make_fighter(Vector2(470, 360), "ReactionTarget")
	director.setup(source)
	director.register_actor(source)
	director.register_actor(target)
	var before_source := _cpu_snapshot(source)
	var before_target := _cpu_snapshot(target)
	var initial := _reaction(target)
	var schema := true
	for key in ["recoil", "lean", "compression", "age", "duration", "priority", "peak"]:
		schema = schema and initial.has(key)
	_check(schema, "reaction snapshot includes current display, timing, priority and peak")
	if not schema:
		_finish()
		return
	_check(_zero_display(initial), "registered native actor begins with no display deformation")
	var light := _sample_profile("拳掌", Vector2.RIGHT)
	var heavy := _sample_profile("痛击", Vector2.RIGHT)
	_test_profile_relationship(light, heavy)
	_test_eight_directions()
	_test_same_priority_reentry()
	_test_priority_protection()
	_test_priority_promotion()
	_test_guard_profiles(light, heavy)
	_test_alternating_reentry()
	_test_pause_and_reset()
	_check(_cpu_snapshot(source) == before_source, "all display envelopes preserve attacker CPU rig, feet, native physics, clock and combat state")
	_check(_cpu_snapshot(target) == before_target, "all display envelopes preserve defender CPU rig, feet, native physics, clock and combat state")
	director.reset()
	director.queue_free()
	source.queue_free()
	target.queue_free()
	await process_frame
	_finish()


func _sample_profile(skill: String, direction: Vector2, damage := 8, blocked := 0) -> Dictionary:
	director.reset()
	_report(direction, skill, damage, blocked)
	director.advance(0.0)
	var first := _reaction(target)
	_check(_zero_display(first), skill + " begins a new reaction continuously from rest")
	var duration := float(first.duration)
	var samples: Array[Dictionary] = []
	var maximum := 0.0
	var peak: Dictionary = first
	var elapsed := 0.0
	while elapsed < duration + SAMPLE_STEP:
		var state := _reaction(target)
		var strength := _strength(state)
		samples.append({"time": elapsed, "strength": strength, "state": state})
		if strength > maximum:
			maximum = strength
			peak = state
		director.advance(SAMPLE_STEP)
		elapsed += SAMPLE_STEP
	return {"duration": duration, "samples": samples, "maximum": maximum,
		"peak": peak, "final": _reaction(target)}


func _strength_at(profile: Dictionary, time: float) -> float:
	var nearest: Dictionary = profile.samples[0]
	for sample in profile.samples:
		if absf(float(sample.time) - time) < absf(float(nearest.time) - time):
			nearest = sample
	return float(nearest.strength)


func _test_profile_relationship(light: Dictionary, heavy: Dictionary) -> void:
	_check(float(light.duration) >= 0.30 and float(light.duration) <= 0.40, "light hurt reaction keeps a readable roughly one-third-second envelope")
	_check(float(heavy.duration) >= 0.44 and float(heavy.duration) <= 0.55, "heavy hurt reaction keeps a longer roughly half-second envelope")
	_check(float(heavy.maximum) > float(light.maximum) * 1.35, "heavy hurt peak is visibly stronger than the light profile")
	_check(float(light.maximum) > 8.0, "light profile carries a substantial combined upper-body display signal")
	_check(_strength_at(light, 0.15) > float(light.maximum) * 0.30, "light reaction remains readable at 150ms rather than crossing into a tiny reverse bounce")
	_check(_strength_at(heavy, 0.20) > float(heavy.maximum) * 0.40, "heavy reaction retains substantial recoil at 200ms")
	for entry in [["light", light], ["heavy", heavy]]:
		var label: String = entry[0]
		var profile: Dictionary = entry[1]
		var previous := float(profile.maximum)
		var monotonic := true
		var bounded := true
		var no_reverse := true
		var peak: Dictionary = profile.peak
		for sample in profile.samples:
			var state: Dictionary = sample.state
			bounded = bounded and _finite_display(state) and float(sample.strength) <= 80.0
			if float(sample.time) >= 0.12:
				monotonic = monotonic and float(sample.strength) <= previous + EPSILON
				previous = float(sample.strength)
			no_reverse = no_reverse and Vector2(state.recoil).dot(Vector2(peak.recoil)) >= -EPSILON
			no_reverse = no_reverse and float(state.lean) * float(peak.lean) >= -EPSILON
		_check(monotonic, label + " isolated hurt recovery returns monotonically after its peak")
		_check(no_reverse, label + " isolated hurt does not oscillate into an opposite lean")
		_check(bounded, label + " envelope remains finite and bounded")
		_check(_zero_display(profile.final), label + " completes at exactly zero deformation")


func _test_eight_directions() -> void:
	for index in range(8):
		var direction := Vector2.RIGHT.rotated(index * PI / 4.0)
		var light := _sample_profile("拳掌", direction)
		var heavy := _sample_profile("痛击", direction)
		var label := "direction %d degrees" % (index * 45)
		_check(float(light.maximum) > 5.0, label + " has a readable light upper-body signal, including vertical approaches")
		_check(float(heavy.maximum) > float(light.maximum) * 1.30, label + " preserves light/heavy separation")
		_check(_finite_display(light.peak) and _finite_display(heavy.peak), label + " has finite peak deformation")
		if absf(direction.x) > 0.5:
			_check(Vector2(light.peak.recoil).x * direction.x > 0.0, label + " light displacement follows the incoming force")
			_check(Vector2(heavy.peak.recoil).x * direction.x > 0.0, label + " heavy displacement follows the incoming force")
		director.reset()
		_report(direction, "痛击")
		director.advance(0.0)
		director.advance(0.06)
		_check(_materials_match(target, _reaction(target)), label + " writes the reaction into all seven native render meshes")


func _test_same_priority_reentry() -> void:
	for skill in ["拳掌", "痛击"]:
		for at in [0.008, 0.05, 0.16, 0.26]:
			director.reset()
			_report(Vector2.RIGHT, skill)
			director.advance(0.0)
			director.advance(at)
			var before := _reaction(target)
			_report(Vector2.LEFT, skill)
			director.advance(0.0)
			var after := _reaction(target)
			var label := "%s reentry at %dms" % [skill, int(round(at * 1000))]
			_check(_same_display(before, after), label + " preserves its current display at the new contact")
			_check(_materials_match(target, after), label + " preserves material continuity")
			director.advance(0.00001)
			var next := _reaction(target)
			_check(_finite_display(next) and Vector2(next.recoil).distance_to(Vector2(after.recoil)) < 0.02 \
				and absf(float(next.lean) - float(after.lean)) < 0.001, label + " has no discontinuity immediately after entry")
			if at >= 0.12:
				director.advance(0.04)
				var renewed := _reaction(target)
				_check(Vector2(renewed.recoil).x < 0.0 and float(renewed.lean) < 0.0,
					label + " responds to the new force after the smooth transition")
			director.advance(0.75)
			_check(_zero_display(_reaction(target)), label + " still settles completely")


func _test_priority_protection() -> void:
	for at in [0.01, 0.06, 0.20, 0.38]:
		director.reset()
		_report(Vector2.RIGHT, "痛击")
		director.advance(0.0)
		director.advance(at)
		var before := _reaction(target)
		_check(int(before.priority) == 4, "heavy hurt profile holds priority four during recovery")
		_report(Vector2.LEFT, "拳掌", 8, 0, target, source)
		director.advance(0.0)
		var source_response := _reaction(target)
		_check(_same_display(before, source_response) and is_equal_approx(float(before.age), float(source_response.age)) \
			and int(source_response.priority) == 4, "attacker recoil cannot replace a heavy hurt reaction at %dms" % int(round(at * 1000)))
		_report(Vector2.LEFT, "痛击", 0, 8)
		director.advance(0.0)
		var guard := _reaction(target)
		_check(_same_display(before, guard) and is_equal_approx(float(before.age), float(guard.age)) \
			and is_equal_approx(float(before.duration), float(guard.duration)) and int(guard.priority) == 4,
			"pure guard cannot shorten or weaken a heavy hurt recovery at %dms" % int(round(at * 1000)))
		_report(Vector2.LEFT, "拳掌")
		director.advance(0.0)
		var light := _reaction(target)
		_check(_same_display(before, light) and is_equal_approx(float(before.age), float(light.age)) \
			and int(light.priority) == 4, "light hurt cannot reset a stronger heavy hurt envelope at %dms" % int(round(at * 1000)))
		director.advance(0.75)
		_report(Vector2.LEFT, "拳掌")
		director.advance(0.0)
		_check(int(_reaction(target).priority) == 3, "light hurt can start after the stronger reaction has completed")


func _test_priority_promotion() -> void:
	director.reset()
	_report(Vector2.RIGHT, "拳掌")
	director.advance(0.0)
	director.advance(0.05)
	var before := _reaction(target)
	_report(Vector2.LEFT, "痛击")
	director.advance(0.0)
	var promoted := _reaction(target)
	_check(_same_display(before, promoted), "stronger hurt promotion starts at the current light display without a jump")
	_check(int(promoted.priority) == 4 and float(promoted.duration) >= 0.44,
		"a new heavy hurt can replace the weaker light profile during its peak")
	director.advance(0.04)
	var peak := _reaction(target)
	_check(Vector2(peak.recoil).x < 0.0 and float(peak.lean) < 0.0 and _strength(peak) > _strength(before) * 1.35,
		"stronger hurt promotion reaches its new direction and heavy peak")
	director.advance(0.75)
	_check(_zero_display(_reaction(target)), "promoted heavy hurt still settles at zero")


func _test_guard_profiles(light: Dictionary, heavy: Dictionary) -> void:
	var guard := _sample_profile("痛击", Vector2.RIGHT, 0, 8)
	_check(int(guard.peak.priority) == 2, "pure absorption uses the guard priority even when the incoming attack is heavy")
	_check(float(guard.maximum) > 0.0 and float(guard.maximum) < float(light.maximum) * 0.60,
		"pure guard shows bounded bracing smaller than a light hurt reaction")
	_check(director.event_history()[0].family == "guard", "pure absorption retains guard contact presentation")
	var partial := _sample_profile("痛击", Vector2.RIGHT, 3, 6)
	_check(director.event_history()[0].family == "guard", "partial absorption retains the established guard contact family")
	_check(int(partial.peak.priority) == 4 and float(partial.maximum) > float(guard.maximum) * 1.5,
		"residual damage under a guard still produces a hurt body reaction")
	_check(float(partial.maximum) < float(heavy.maximum) and float(partial.maximum) >= float(heavy.maximum) * 0.55,
		"partial guard reduces its hurt reaction without hiding the residual damage")


func _test_alternating_reentry() -> void:
	director.reset()
	var continuous := true
	var bounded := true
	for index in range(32):
		var before := _reaction(target)
		_report(Vector2.RIGHT if index % 2 == 0 else Vector2.LEFT, "痛击")
		director.advance(0.0)
		var now := _reaction(target)
		continuous = continuous and _same_display(before, now)
		director.advance(1.0 / 60.0)
		now = _reaction(target)
		bounded = bounded and _finite_display(now) and _strength(now) <= 80.0
	_check(continuous, "rapid alternating heavy contacts preserve display continuity at every entry")
	_check(bounded, "rapid alternating contacts remain finite without accumulating unbounded deformation")
	_check(int(director.debug_snapshot().marks) <= 16, "reaction stress retains the finite contact-mark limit")
	director.advance(0.75)
	_check(_zero_display(_reaction(target)), "rapid alternating contacts still recover fully after the last hit")


func _test_pause_and_reset() -> void:
	director.reset()
	_report(Vector2.UP, "痛击")
	director.advance(0.0)
	director.advance(0.08)
	var before := _reaction(target)
	director.advance(0.50, true)
	var paused := _reaction(target)
	_check(_same_display(before, paused) and is_equal_approx(float(before.age), float(paused.age)), "pause freezes lean, recoil, compression and reaction age")
	_check(_materials_match(target, paused), "paused native meshes retain the exact display deformation")
	director.advance(0.0, false)
	_check(_same_display(paused, _reaction(target)), "resume with no elapsed time preserves reaction continuity")
	director.reset()
	for fighter in [source, target]:
		var cleared := _reaction(fighter)
		_check(_zero_display(cleared), "reset clears recoil, lean and compression for " + fighter.name)
		_check(_materials_match(fighter, cleared), "reset clears all native-mesh deformation uniforms for " + fighter.name)
	director.advance(0.0)
	_check(_zero_display(_reaction(target)), "a zero-time update after reset cannot resurrect the old reaction")


func _finish() -> void:
	print("COMBAT_REACTION_SMOKE ", JSON.stringify({"passed": failures.is_empty(), "checks": checks,
		"failures": failures,
		"scope": "Native-rig display envelopes, eight-direction profiles, material continuity and repeated-contact priorities. Actual GPU pixel appearance requires visual QA."}))
	quit(0 if failures.is_empty() else 1)
