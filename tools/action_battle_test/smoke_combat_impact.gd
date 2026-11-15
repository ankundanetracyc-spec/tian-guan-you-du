extends SceneTree

## Presentation contract checks. The real combat-rule suites remain separate.
const CONTACT_SPACE = preload("human_contact_space.gd")
const DIRECTOR_PATH := "res://tools/action_battle_test/combat_impact_feedback.gd"

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
	_check(loaded and rig.native_source_mode, label + " uses the current original male101 runtime")
	fighter.original_wanderer = rig
	fighter.skinned_player = rig
	fighter.skinned_player_ready = loaded
	fighter.motion_time = 0.25
	fighter.visual_motion_delta = 1.0 / 60.0
	fighter.update_skinned_player()
	return fighter


func _cpu_snapshot(fighter: Node2D) -> Dictionary:
	var rig: Node2D = fighter.original_wanderer
	var skin: Variant = rig.wanderer_skin
	var bones := {}
	for key in skin.bones:
		bones[key] = skin.bones[key].global_transform
	var meshes: Array = []
	for mesh in skin.meshes:
		meshes.append([mesh.global_transform, mesh.polygon.duplicate()])
	return {
		"player": fighter.player,
		"controller_transform": fighter.global_transform,
		"rig_transform": rig.global_transform,
		"skin_root_transform": rig.rig_root_node.global_transform,
		"hand_height": CONTACT_SPACE.hand_height(fighter),
		"body_height": CONTACT_SPACE.body_height(fighter),
		"foot": skin.contact_shadow.global_transform,
		"clock": fighter.motion_time,
		"native_clock": skin.last_motion_clock,
		"native_physics": skin.native_physics.components.duplicate(true),
		"native_world": skin.world_transforms.duplicate(true),
		"bones": bones,
		"meshes": meshes,
		"hp": fighter.hp,
		"stamina": fighter.stamina,
		"mana": fighter.mana,
		"cooldowns": fighter.skill_cooldowns.duplicate(true),
	}


func _voices(node: Node) -> Array[AudioStreamPlayer]:
	var result: Array[AudioStreamPlayer] = []
	for child in node.get_children():
		if child is AudioStreamPlayer:
			result.append(child)
		result.append_array(_voices(child))
	return result


func _snapshot() -> Dictionary:
	return director.debug_snapshot()


func _result(damage := 0, blocked := 0, evaded := false, critical := false) -> Dictionary:
	return {"inactive": false, "evaded": evaded, "damage": damage, "blocked": blocked, "critical": critical}


func _report(result: Dictionary, segment := "本体", skill := "拳掌") -> void:
	director.report(source, target, result, skill, segment, CONTACT_SPACE.hand_height(source))


func _assert_cpu_unchanged(before_source: Dictionary, before_target: Dictionary, label: String) -> void:
	_check(_cpu_snapshot(source) == before_source, label + ": attacker CPU pose, clock, ground point and combat state stay unchanged")
	_check(_cpu_snapshot(target) == before_target, label + ": defender CPU pose, clock, ground point and combat state stay unchanged")


func _run() -> void:
	var director_script: Script = load(DIRECTOR_PATH)
	if director_script == null:
		_check(false, "shared combat impact director loads")
		_finish()
		return
	director = director_script.new()
	root.add_child(director)
	director.set_process(false)
	source = _make_fighter(Vector2(420, 360), "ImpactSource")
	target = _make_fighter(Vector2(470, 360), "ImpactTarget")
	director.setup(source)
	source.set("impact_feedback", director)
	target.set("impact_feedback", director)
	director.register_actor(source)
	director.register_actor(target)
	_check(_voices(director).is_empty(), "visual feedback creates no audio players")
	var before_source := _cpu_snapshot(source)
	var before_target := _cpu_snapshot(target)
	_check(source.original_wanderer.wanderer_skin.meshes.size() == 7, "feedback is exercised on all seven native render meshes")
	_test_results(before_source, before_target)
	_test_pause_and_reset(before_source, before_target)
	_test_guard_and_world_results()
	_test_saturation()
	_test_native_pose_progression()
	_test_death_clear()
	director.reset()
	director.queue_free()
	source.queue_free()
	target.queue_free()
	await process_frame
	_finish()


func _test_results(before_source: Dictionary, before_target: Dictionary) -> void:
	director.reset()
	director.advance(1.0 / 60.0)
	_check(director.camera_offset().is_zero_approx(), "a released swing with no contact never creates a hit camera impulse")
	var miss := _snapshot()
	_check(miss.groups == 0 and miss.committed_hits == 0 and miss.marks == 0 and miss.active_reactions == 0, "spatial miss produces no contact or hurt reaction")
	_check(_voices(director).is_empty(), "spatial miss does not create a swing sound player")
	_assert_cpu_unchanged(before_source, before_target, "true spatial miss")
	director.reset()
	_report({"inactive": true, "damage": 50, "blocked": 6, "critical": true})
	_report(_result(0, 0, true))
	director.advance(1.0 / 60.0)
	_check(director.camera_offset().is_zero_approx(), "inactive and evaded results never create a damage camera impulse")
	var evade := _snapshot()
	_check(evade.committed_evades == 1 and evade.groups == 0 and evade.committed_hits == 0 and evade.committed_guards == 0 and evade.marks == 0 and evade.active_reactions == 0, "only active dodge is recorded; inactive and evaded results create no damage feedback")
	_assert_cpu_unchanged(before_source, before_target, "inactive and evaded results")
	director.reset()
	_report(_result(0, 0))
	director.advance(1.0 / 60.0)
	_check(director.camera_offset().is_zero_approx(), "zero damage and zero absorption never create a guard camera impulse")
	var zero := _snapshot()
	_check(zero.groups == 0 and zero.committed_guards == 0 and zero.marks == 0, "an active window with zero actual absorption has no guard feedback")
	director.reset()
	_report(_result(8), "本体")
	_report(_result(4), "血气")
	_check(_snapshot().pending == 2, "body and blood qi remain separate real result submissions until presentation flush")
	director.advance(1.0 / 60.0)
	_check(not director.camera_offset().is_zero_approx(), "real damage produces a bounded camera response")
	var combined := _snapshot()
	_check(combined.groups == 1 and combined.committed_hits == 1 and combined.marks == 1 and _voices(director).is_empty(), "simultaneous body and blood qi merge into one silent contact mark")
	var history: Array = director.event_history()
	_check(history.size() == 1 and history[0].damage == 12 and history[0].segments.has("本体") and history[0].segments.has("血气"), "presentation merge retains actual total damage and both segment identities")
	_assert_cpu_unchanged(before_source, before_target, "body and blood qi simultaneous contact")


func _test_pause_and_reset(before_source: Dictionary, before_target: Dictionary) -> void:
	var camera: Vector2 = director.camera_offset()
	var visuals := _visual_state()
	director.advance(0.5, true)
	_check(director.camera_offset() == camera, "pause freezes the existing camera offset")
	_check(_visual_state() == visuals, "pause freezes material recoil")
	_check(_voices(director).is_empty(), "paused visual feedback remains silent")
	_assert_cpu_unchanged(before_source, before_target, "paused presentation")
	director.advance(0.0, false)
	_check(_voices(director).is_empty(), "resume does not create audio players")
	director.reset()
	_check(director.camera_offset().is_zero_approx(), "reset removes the camera impulse")
	_check(_voices(director).is_empty(), "reset retains no audio players")
	var cleared := _snapshot()
	_check(cleared.pending == 0 and cleared.marks == 0 and cleared.active_reactions == 0, "reset clears queued contact, marks and active reactions")
	_check(_visual_state().all(func(row): return row[0] == Vector2.ZERO and is_zero_approx(float(row[1]))), "reset removes all GPU recoil and compression")
	_assert_cpu_unchanged(before_source, before_target, "reset presentation")


func _visual_state() -> Array:
	var result: Array = []
	for fighter in [source, target]:
		for mesh in fighter.original_wanderer.wanderer_skin.meshes:
			_check(mesh.material is ShaderMaterial, "native render mesh has the shared presentation material")
			if mesh.material is ShaderMaterial:
				result.append([mesh.material.get_shader_parameter("recoil"), mesh.material.get_shader_parameter("compression")])
	return result


func _test_guard_and_world_results() -> void:
	director.reset()
	_report(_result(3, 6), "本体", "痛击")
	director.advance(1.0 / 60.0)
	var guard := _snapshot()
	_check(guard.groups == 1 and guard.committed_guards == 1 and guard.committed_hits == 1 and guard.marks == 1, "partial actual absorption retains both the guard and residual damage result")
	_check(director.event_history()[0].family == "guard", "actual absorption chooses the guard presentation profile")
	director.reset()
	director.report_world(source, null, _result(7), "拳掌", "本体", Vector2(470, 302), Vector2.RIGHT, 360.0)
	director.advance(1.0 / 60.0)
	_check(_snapshot().groups == 1 and _snapshot().committed_hits == 1 and _snapshot().marks == 1, "world contact supports an enemy without a human fighter node")
	_check(director.event_history()[0].target == -1, "world contact keeps the independent non-human target identity")


func _test_saturation() -> void:
	director.reset()
	for index in range(32):
		_report(_result(40, 0, false, true), "血浪", "赤潮奔涌")
		director.advance(0.0)
		_check(director.camera_offset().length() <= 1.65 + 0.00001, "repeated contacts never accumulate an unbounded camera impulse")
		_check(_voices(director).is_empty() and _snapshot().marks <= 16, "repeated contacts remain silent with finite mark limits")
	_check(_voices(director).is_empty(), "contact bursts never allocate audio players")


func _test_death_clear() -> void:
	director.reset()
	_report(_result(100, 0, false, true), "本体", "痛击")
	target.hp = 0
	target.clear_finished_battle()
	director.advance(1.0 / 60.0)
	_check(not director.camera_offset().is_zero_approx(), "combat cleanup preserves the queued final contact")
	var camera: Vector2 = director.camera_offset()
	for count in range(3):
		target.clear_finished_battle()
	_check(director.camera_offset() == camera, "repeated death cleanup does not erase the final hit's presentation tail")


func _test_native_pose_progression() -> void:
	for facing in [1.0, -1.0]:
		target.facing = facing
		for frame in range(4):
			target.motion_time += 1.0 / 60.0
			target.visual_motion_delta = 1.0 / 60.0
			target.visual_motion_velocity = Vector2.RIGHT * 114.0 * facing
			target.walk_amount = 1.0
			target.update_skinned_player()
			var moving_source := _cpu_snapshot(source)
			var moving_target := _cpu_snapshot(target)
			director.reset()
			_report(_result(8))
			director.advance(1.0 / 60.0)
			_assert_cpu_unchanged(moving_source, moving_target, "running native frame %d facing %d" % [frame, int(facing)])
	_check(is_equal_approx(target.original_wanderer.wanderer_skin.last_motion_clock, target.motion_time), "real native animation and cloth clock continue during feedback")


func _finish() -> void:
	print("COMBAT_IMPACT_SMOKE ", JSON.stringify({"passed": failures.is_empty(), "checks": checks, "failures": failures,
		"scope": "Feedback result contract with actual native character geometry; independent human/manual/fist gameplay suites verify contact rules."}))
	quit(0 if failures.is_empty() else 1)
