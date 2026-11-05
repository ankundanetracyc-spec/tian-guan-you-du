extends SceneTree
## Behavioral regression for the shared ink layer; no skill data is substituted.
## Uses the original fixed fixture for exact contact timing, then both human
## arenas for compatibility without re-encoding the manual's damage formulas.

class FixedArena extends "res://tools/action_battle_test/action_battle_feel_test.gd":
	var fixed_aim := Vector2.RIGHT
	func attack_aim_direction() -> Vector2:
		return fixed_aim
	func movement_direction() -> Vector2:
		return Vector2.ZERO

var arena: Node2D
var checks := 0
var failures := 0
var attack_counts := {}


func _initialize() -> void:
	call_deferred("verify")


func check(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		push_error(label)


func _freeze(stage: Node2D) -> void:
	stage.set_process(false)
	stage.set_process_input(false)
	if stage.battle_hud != null:
		stage.battle_hud.set_process_input(false)
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1280, 720)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true


func _state(fighter: Node2D) -> Array:
	return [fighter.hp, fighter.enemy_hp, fighter.stamina, fighter.mana,
		fighter.qi, fighter.qi_blood, fighter.player, fighter.enemy,
		fighter.skill_cooldowns.duplicate(true), fighter.pending_attack,
		fighter.release_timer, fighter.pending_self_skill,
		fighter.self_cast_remaining, fighter.guard_window, fighter.invulnerable]


func _sync(vfx: Node2D) -> void:
	vfx.sync_visuals()
	vfx.sync_guard_visuals()


func _ink(vfx: Node2D) -> Node2D:
	return vfx.get("ink_layer") as Node2D


func _reset() -> void:
	arena.restart_battle()
	arena.player = Vector2(1500, 950)
	arena.enemy = arena.player + Vector2(55, 0)
	arena.enemy_hp = 1000
	arena.enemy_max_hp = 1000
	arena.wolf_state = "recover"
	arena.wolf_clock = 100.0
	arena.fixed_aim = Vector2.RIGHT
	arena.facing = 1.0
	arena.update_skinned_player()
	_sync(arena.skill_vfx)


func _check_repeated_sync(vfx: Node2D, label: String) -> void:
	var layer := _ink(vfx)
	if layer == null:
		check(false, label + ": common ink layer missing")
		return
	var before := _state(arena)
	var ink_before: Array = layer._pool.duplicate(true)
	var clock_before: float = layer.clock
	for repeat in range(12):
		_sync(vfx)
	check(_state(arena) == before, label + ": redraw cannot change fighter state")
	check(layer._pool == ink_before and layer.clock == clock_before,
		label + ": repeated redraw neither re-emits nor advances particles")


func verify_attacks() -> void:
	var vfx: Node2D = arena.skill_vfx
	for kind in ["拳掌", "痛击"]:
		for hit in [false, true]:
			_reset()
			arena.enemy = arena.player + Vector2(55 if hit else 450, 0)
			var initial_stamina: float = arena.stamina
			arena.use_skill(kind)
			check(arena.stamina == initial_stamina - (1.0 if kind == "痛击" else 0.0),
				kind + ": original resource cost retained")
			arena.update_skill_timers(0.179 if kind == "拳掌" else 0.199)
			check(arena.enemy_hp == 1000 and arena.strike_fx.is_empty(),
				kind + ": no early contact or impact visual")
			arena.update_skill_timers(0.002)
			var expected_damage := (4 if kind == "拳掌" else 17) if hit else 0
			check(arena.enemy_hp == 1000 - expected_damage and arena.strike_fx.size() == 1,
				kind + ": original single contact damage / timing retained")
			check(bool(arena.strike_fx[0].hit) == hit,
				kind + ": visual hit flag follows the actual contact result")
			var before := _state(arena)
			_sync(vfx)
			check(_state(arena) == before, kind + ": initial visual sync is read-only")
			check(not vfx.stamps.is_empty() and vfx.stamps[0].visible,
				kind + ": main ink silhouette is visible")
			if not vfx.stamps.is_empty():
				check(vfx.stamps[0].material.shader == vfx.force_shader,
					kind + ": silhouette uses the common ink material")
			var layer := _ink(vfx)
			if layer == null:
				check(false, kind + ": particle layer missing")
				continue
			attack_counts[kind + ("_hit" if hit else "_miss")] = layer.active_count()
			check(layer.active_count() > 0, kind + ": release leaves a restrained ink trail")
			_check_repeated_sync(vfx, kind + (" hit" if hit else " miss"))
			arena.configuring = true
			var clock: float = layer.clock
			var ages: Array = layer._pool.duplicate(true)
			arena._process(0.05)
			check(layer.clock == clock and layer._pool == ages and _state(arena) == before,
				kind + ": configuration pause freezes the common visual clock and combat")
			arena.configuring = false
			var settled_hp: int = arena.enemy_hp
			arena.update_strike_fx(1.0)
			_sync(vfx)
			check(arena.enemy_hp == settled_hp, kind + ": fading particles cannot add damage")
			check(arena.strike_fx.is_empty() and not vfx.stamps[0].visible and layer.active_count() == 0,
				kind + ": the existing visual lifetime clears silhouette and particles")
		if attack_counts.has(kind + "_hit") and attack_counts.has(kind + "_miss"):
			check(int(attack_counts[kind + "_hit"]) > int(attack_counts[kind + "_miss"]),
				kind + ": actual hit adds contact ink while a miss only has the release trail")


func verify_guard() -> void:
	_reset()
	var vfx: Node2D = arena.skill_vfx
	var layer := _ink(vfx)
	arena.use_skill("架招")
	check(is_equal_approx(arena.guard_window, 0.20) and arena.stamina == 5.0,
		"guard retains its 0.20s logical window and original cost")
	check(arena.guard_fx.size() == 1 and not bool(arena.guard_fx[0].success),
		"ordinary guard is not a successful collision")
	check(arena.guard_fx[0].has("attack_height") and float(arena.guard_fx[0].attack_height) > 0.0,
		"guard captures a real positive presentation height")
	_sync(vfx)
	check(layer != null and layer.active_count() == 0,
		"ordinary guard does not emit successful-contact particles")
	check(not vfx.guard_stamps.is_empty() and vfx.guard_stamps[0].visible,
		"ordinary guard still displays its own ink anticipation")
	if not vfx.guard_stamps.is_empty():
		check(vfx.guard_stamps[0].material.shader == vfx.force_shader,
			"guard shares the recoloured ink material rather than the raw atlas")
	_check_repeated_sync(vfx, "ordinary guard")
	arena.update_skill_timers(0.201)
	arena.update_guard_fx(0.201)
	_sync(vfx)
	check(is_zero_approx(arena.guard_window) and not arena.guard_fx.is_empty(),
		"guard logic expires while its faint visual residue remains")
	var residue_origin: Vector2 = arena.guard_fx[0].origin
	var residue_height: float = arena.guard_fx[0].attack_height
	arena.player += Vector2(90, 30)
	arena.update_skinned_player()
	arena.update_guard_fx(0.05)
	check(arena.guard_fx[0].origin == residue_origin and arena.guard_fx[0].attack_height == residue_height,
		"expired guard residue keeps its last position and height")
	arena.update_guard_fx(0.20)
	_sync(vfx)
	check(arena.guard_fx.is_empty() and not vfx.guard_stamps[0].visible,
		"ordinary guard visual is gone after 0.45s")
	_reset()
	var initial_hp: int = arena.hp
	arena.receive_wolf_bite()
	var unguarded_damage: int = initial_hp - arena.hp
	_reset()
	arena.use_skill("架招")
	arena.receive_wolf_bite()
	check(arena.hp < initial_hp and initial_hp - arena.hp < unguarded_damage,
		"real guard still reduces the original incoming damage")
	check(is_zero_approx(arena.guard_window) and arena.guard_fx.size() == 1 and bool(arena.guard_fx[0].success),
		"a settled successful guard replaces anticipation and consumes its logical window")
	var before := _state(arena)
	_sync(vfx)
	check(layer.active_count() > 0 and _state(arena) == before,
		"actual guard success emits visual ink without an additional payment or hit")
	_check_repeated_sync(vfx, "successful guard")
	arena.update_guard_fx(0.401)
	arena.update_strike_fx(0.401)
	_sync(vfx)
	check(arena.guard_fx.is_empty() and not vfx.guard_stamps[0].visible and layer.active_count() == 0,
		"success visual and its short particles finish within the existing 0.40s lifetime")
	_reset()
	arena.use_skill("痛击")
	arena.update_skill_timers(0.201)
	_sync(vfx)
	check(layer.active_count() > 0, "cleanup case begins with live particles")
	arena.restart_battle()
	check(layer.active_count() == 0 and vfx.stamps.all(func(stamp): return not stamp.visible)
		and vfx.guard_stamps.all(func(stamp): return not stamp.visible),
		"restart clears attack, guard and shared particle visuals")


func verify_human_compatibility(path: String) -> void:
	var stage: Node2D = load(path).instantiate()
	root.add_child(stage)
	_freeze(stage)
	check(stage.npcs.size() == 3 and stage.attribute_runtime.get_unit_count() == 4,
		path.get_file() + ": original player and three human opponents still initialize")
	var fighters: Array = [stage]
	fighters.append_array(stage.npcs)
	for fighter: Node2D in fighters:
		var before := _state(fighter)
		fighter.start_guard_visual(false, Vector2.RIGHT)
		_sync(fighter.skill_vfx)
		check(_state(fighter) == before and _ink(fighter.skill_vfx) != null,
			path.get_file() + ": shared visual sync preserves every human fighter's state")
		check(not fighter.skill_vfx.guard_stamps.is_empty() and fighter.skill_vfx.guard_stamps[0].visible,
			path.get_file() + ": each human fighter renders the unified guard")
		fighter.skill_vfx.clear_visuals()
		check(_ink(fighter.skill_vfx).active_count() == 0,
			path.get_file() + ": per-fighter cleanup empties the common ink pool")
	stage.queue_free()
	await process_frame


func verify() -> void:
	arena = FixedArena.new()
	arena.original_wanderer_enabled = true
	arena.original_wanderer_art_folder = "yuxu_fullbody_v15"
	root.add_child(arena)
	_freeze(arena)
	check(arena.skill_vfx != null and arena.skill_vfx.has_method("sync_guard_visuals")
		and _ink(arena.skill_vfx) != null, "unified ink interfaces are present")
	if not arena.skill_vfx.has_method("sync_guard_visuals") or _ink(arena.skill_vfx) == null:
		print("UNIFIED_INK_VFX_SMOKE_FAIL checks=%d failures=%d interfaces_missing=true" % [checks, failures])
		quit(1)
		return
	verify_attacks()
	verify_guard()
	arena.queue_free()
	await process_frame
	await verify_human_compatibility("res://tscn/scene/ActionHumanSparringScene.tscn")
	await verify_human_compatibility("res://tscn/scene/ActionManualBattleScene.tscn")
	print("UNIFIED_INK_VFX_SMOKE_%s checks=%d failures=%d attacks=%s" % [
		"OK" if failures == 0 else "FAIL", checks, failures, JSON.stringify(attack_counts)])
	quit(0 if failures == 0 else 1)
