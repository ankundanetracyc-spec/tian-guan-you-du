extends Node2D

## Exercises the shared game loader and the real visual/gameplay consumers.
## This fixture never enters MainScene or loads/writes a personal save.
const RigRuntime = preload("guigu_rig_runtime.gd")
const ApprovedSkin = preload("approved_outfit_crossing_skin.gd")
const FullArmLayer = preload("approved_outfit_full_arm_layer.gd")
const ORIGINAL_SWORD := "dcc95466b3b9430131d73a4b2d99f89c1e5a8c64a25fd42f9c54bc3b37302721"
const EXPECTED_PALM := 3.81277745419
const STEP := 1.0 / 60.0

var checks := 0
var failures: Array[String] = []
var evidence: Dictionary = {}
var resource_dir := ""
var sample_count := 0
var maximum_bone_length_error := 0.0
var maximum_wrist_error := 0.0
var maximum_palm_error := 0.0
var maximum_grip_error := 0.0

func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		push_error("OUTFIT INTEGRATION: " + label)

func _ready() -> void:
	resource_dir = get_script().resource_path.get_base_dir()
	call_deferred("_run")

func _run() -> void:
	var rig := RigRuntime.new()
	add_child(rig)
	var loaded: bool = rig.load_runtime(resource_dir.path_join("guigu_rig/rig.json"), resource_dir.path_join("guigu_rig/skins.json"), resource_dir.path_join("guigu_rig/animations.json"), resource_dir.path_join("art"), "yuxu_fullbody_v15")
	_check(loaded, "shared load_runtime builds approved outfit from actual game data: " + str(rig.load_errors))
	if loaded:
		rig.set_skin_mode("experiment")
		_validate_skin(rig, "shared")
		_sample_shared(rig)
		evidence.shared = rig.get_runtime_summary()
	else:
		evidence.shared_errors = rig.load_errors.duplicate()
	rig.queue_free()
	await get_tree().process_frame
	if "--runtime-only" in OS.get_cmdline_user_args():
		evidence.consumer_scope = "Nested project: shared game loader only; formal root consumers are covered by root.json."
	else:
		await _check_world_traveler()
		await _check_actual_arena()
	_check(maximum_bone_length_error < 0.0001, "all sampled actual arm/leg segments retain fixed rest lengths")
	_check(maximum_wrist_error < 0.0001, "registered wrists stay on actual hand bones")
	_check(maximum_palm_error < 0.0001, "all five hand variants retain the measured common palm ruler")
	_check(maximum_grip_error < 0.0001, "independent sword follows the registered grip")
	_finish()

func _validate_skin(rig: Node2D, scope: String) -> void:
	var skin: Variant = rig.wanderer_skin
	_check(skin != null and skin.get_script() == ApprovedSkin, scope + ": actual shared loader uses the approved continuous skin")
	if skin == null or skin.get_script() != ApprovedSkin: return
	_check(skin.validation_errors.is_empty(), scope + ": skin reports no validation errors")
	_check(is_equal_approx(skin.PIXEL_SCALE, 16.0 / 186.0) and is_equal_approx(skin.display_scale, 0.72), scope + ": original drawing ruler and battle display scale remain")
	var hands: Variant = skin.approved_hands
	_check(hands != null and hands.get_script() == FullArmLayer and hands.assets.size() == 5, scope + ": actual complete-arm layer has all five sources")
	if hands != null:
		_check(absf(float(hands.target_palm_width) - EXPECTED_PALM) < 0.0000001, scope + ": palm width is the measured 80-percent result")
		for key in FullArmLayer.KEYS:
			_check(hands.assets.has(key) and str(hands.assets[key].path) == resource_dir.path_join("art/approved_outfit_full_arms_20261105/" + str(key) + ".png"), scope + ": actual source path " + str(key))
			if hands.assets.has(key):
				_check(hands.assets[key].sha256 == hands.registrations[key].source_sha256, scope + ": generated arm matches measured registration " + str(key))
		_check(hands.sword_source_sha256 == ORIGINAL_SWORD and FileAccess.get_sha256(resource_dir.path_join("art/yuxu_fullbody_v15/arm_sword_fingers_v21.png")) == ORIGINAL_SWORD, scope + ": protected original v21 sword fingers remain byte-identical")
	_check(skin.approved_closed_robe_mesh != null and skin.approved_closed_robe_mesh.visible, scope + ": actual continuous robe is visible")
	_check(skin.approved_closed_robe_mesh.polygon.size() == 513 and skin.approved_closed_robe_mesh.polygons.size() == 896, scope + ": continuous robe retains its accepted topology")
	for record in skin.mesh_records:
		if str(record.name) in ["robe_front", "robe_back"]:
			_check(not record.mesh.visible, scope + ": legacy robe panel stays hidden " + str(record.name))
	_check(str(skin.approved_upper_source).contains("male101"), scope + ": torso and arms use male101")
	_check(skin.approved_source_run_legs.profile.source.character == "male101", scope + ": actual leg source uses male101")
	_check(skin.approved_source_run_legs.profile.samples.size() == 144 and is_equal_approx(skin.guigu_pose_profile.clips.Run_New1.nearFrontmostPhase, 0.0), scope + ": 144 original run phases share zero phase")
	_check(is_equal_approx(skin.approved_sword.swing_duration, 0.72), scope + ": inspection swing remains a 0.72-second single motion")

func _measure(rig: Node2D) -> void:
	var skin: Variant = rig.wanderer_skin
	var hands: Variant = skin.approved_hands
	var inverse: Transform2D = rig.rig_root_node.global_transform.affine_inverse()
	for side in ["near", "far"]:
		for chain in [["_upper", "_forearm", "_hand"], ["_thigh", "_calf", "_foot"]]:
			for index in range(1, 3):
				var parent: Bone2D = skin.bones[side + str(chain[index - 1])]
				var bone: Bone2D = skin.bones[side + str(chain[index])]
				var actual: float = (inverse * parent.global_position).distance_to(inverse * bone.global_position)
				maximum_bone_length_error = maxf(maximum_bone_length_error, absf(actual - bone.rest.origin.length()))
				if not bone.scale.is_equal_approx(Vector2.ONE):
					_check(false, "sampled limb bone has unexpected scale: " + side + str(chain[index]))
		var wrist: Bone2D = skin.bones[side + "_hand"]
		maximum_wrist_error = maxf(maximum_wrist_error, (inverse * hands.get_wrist_transform(side).origin).distance_to(inverse * wrist.global_position))
		var state: Dictionary = hands.hands[side]
		if hands.registrations.has(state.key):
			var registration: Dictionary = hands.registrations[state.key]
			var source_wrist := Vector2(registration.wrist_px[0], registration.wrist_px[1])
			var sample_point := source_wrist + Vector2(float(registration.palm_width_px), 0.0)
			var width: float = (inverse * hands.get_hand_point_global(side, source_wrist)).distance_to(inverse * hands.get_hand_point_global(side, sample_point))
			maximum_palm_error = maxf(maximum_palm_error, absf(width - EXPECTED_PALM))
	var sword: Variant = skin.approved_sword
	if sword.mode != "off":
		maximum_grip_error = maxf(maximum_grip_error, float(sword.last.get("attachment_error", 0.0)))
	sample_count += 1

func _sample_shared(rig: Node2D) -> void:
	var skin: Variant = rig.wanderer_skin
	var clock := 0.0
	for style in ["open", "fist", "grip_near", "sword_near"]:
		skin.approved_hands.set_pose_override(style)
		for direction in [1.0, -1.0]:
			for frame in range(60):
				clock += STEP
				skin.set_motion_context(STEP, Vector2(direction * 114.0, 0.0), clock, direction)
				rig.set_character_pose("Run_New1", clock, direction)
				_measure(rig)
			if style == "sword_near":
				_check(skin.approved_hands.hands.near.key == "original_v21_sword" and bool(skin.approved_hands.hands.near.material.get_shader_parameter("keep_original_sword")), "shared: protected v21 renders through its actual shader branch in both facings")
	skin.approved_hands.set_pose_override("auto")
	_check(skin.approved_source_run_legs.active, "shared: actual forward run executes measured male101 leg solver")

func _check_world_traveler() -> void:
	var path := "res://gdscripts/world/WorldActionTraveler.gd"
	_check(ResourceLoader.exists(path), "formal WorldActionTraveler is available under the actual root project")
	if not ResourceLoader.exists(path): return
	var traveler: Node2D = load(path).new()
	add_child(traveler)
	var built: bool = traveler.build(32.0)
	_check(built, "formal WorldActionTraveler builds without loading MainScene or a save")
	if built:
		_validate_skin(traveler.rig, "world")
		var silhouette := Image.new()
		var image_error: int = silhouette.load(ProjectSettings.globalize_path(resource_dir.path_join("art/yuxu_fullbody_v15/cutout.png")))
		_check(image_error == OK, "world: original source silhouette loads")
		var original_height: float = silhouette.get_used_rect().size.y * (16.0 / 186.0) * 0.72
		_check(absf(float(traveler.native_height) - original_height) < 0.0001, "world: native envelope still comes from original cutout and ruler")
		_check(absf(float(traveler.native_height) * traveler.scale.y - 32.0) < 0.0001, "world: existing 32-pixel marker envelope remains exact")
		var fixed_position := Vector2(139.0, 217.0)
		traveler.position = fixed_position
		var turns: Array = []
		for velocity in [Vector2(17, 0), Vector2(-23, 0), Vector2(0, 11), Vector2.ZERO]:
			for frame in range(90):
				traveler.update_motion(STEP, velocity)
				_measure(traveler.rig)
			turns.append({"velocity":str(velocity), "clip":traveler.current_clip, "facing":traveler.facing})
			_check(traveler.current_clip == ("Idle" if velocity == Vector2.ZERO else "Run_New1"), "world: actual travel input selects moving/idle clip " + str(velocity))
			if velocity.x != 0.0:
				_check(traveler.facing == signf(velocity.x) and signf(traveler.rig.scale.x) == signf(velocity.x), "world: travel direction mirrors actual rig " + str(velocity))
		_check(traveler.facing == -1.0, "world: vertical travel and idle preserve last horizontal facing")
		_check(traveler.position.is_equal_approx(fixed_position), "world: visual update leaves map-owned foot position unchanged")
		var frozen := _pose_snapshot(traveler.rig)
		for frame in range(20): traveler.update_motion(0.0, Vector2.ZERO)
		_check(_pose_snapshot(traveler.rig) == frozen, "world: zero time input freezes all actual visual bones and weapon")
		evidence.world = {"summary":traveler.get_visual_summary(), "native_height":traveler.native_height, "envelope_pixels":traveler.native_height * traveler.scale.y, "foot_position":str(traveler.position), "motion_cases":turns}
	traveler.queue_free()
	await get_tree().process_frame

func _combat_snapshot(arena: Node2D) -> Dictionary:
	return {"pending_attack":arena.pending_attack, "pending_self_skill":arena.pending_self_skill, "attack_lock":arena.attack_lock, "release_timer":arena.release_timer, "self_cast_remaining":arena.self_cast_remaining, "stamina":arena.stamina, "mana":arena.mana, "qi_blood":arena.qi_blood, "qi":arena.qi, "hp":arena.hp, "enemy_hp":arena.enemy_hp, "cooldowns":arena.skill_cooldowns.duplicate(true), "cooldown_totals":arena.skill_cooldown_totals.duplicate(true), "statuses":arena.visible_statuses.duplicate(), "shots":arena.shots.size(), "enemy_shots":arena.enemy_shots.size(), "strike_fx":arena.strike_fx.size(), "guard_fx":arena.guard_fx.size(), "combat_texts":arena.combat_texts.size(), "fist_active":arena.fist_combo.active if arena.fist_combo != null else false, "fist_chain_id":arena.fist_combo.chain_id if arena.fist_combo != null else 0, "dash_move":arena.dash_move, "guard_window":arena.guard_window, "attack_reach":arena.attack_reach, "player":str(arena.player)}

func _pose_snapshot(rig: Node2D) -> Array:
	var snapshot: Array = [rig.transform]
	for bone in rig.wanderer_skin.bones.values(): snapshot.append(bone.transform)
	snapshot.append(rig.wanderer_skin.approved_sword.sword_sprite.global_transform)
	snapshot.append(rig.wanderer_skin.approved_sword.swing_elapsed)
	return snapshot

func _key(arena: Node2D, code: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = true
	arena._input(event)

func _held_key(code: Key, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)
	Input.flush_buffered_events()

func _point_mouse_at(arena: Node2D, world_point: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = arena.get_viewport().get_canvas_transform() * world_point
	event.global_position = event.position
	if DisplayServer.get_name() != "headless": arena.get_viewport().warp_mouse(event.position)
	Input.parse_input_event(event)
	Input.flush_buffered_events()

func _check_actual_arena() -> void:
	var scene_path := "res://tscn/scene/ActionBattleOriginalWandererScene.tscn"
	_check(ResourceLoader.exists(scene_path), "actual formal battle scene is present")
	if not ResourceLoader.exists(scene_path): return
	var arena: Node2D = load(scene_path).instantiate()
	add_child(arena)
	arena.set_process(false)
	arena.set_process_input(false)
	if arena.battle_hud != null: arena.battle_hud.set_process_input(false)
	_check(arena.original_wanderer != null and arena.original_wanderer.loaded, "actual formal battle scene constructs the shared outfit runtime")
	if arena.original_wanderer == null or not arena.original_wanderer.loaded:
		arena.queue_free()
		await get_tree().process_frame
		return
	var rig: Node2D = arena.original_wanderer
	_validate_skin(rig, "battle")
	_check(arena.attribute_runtime == null, "light battle fixture has no formal save/attribute writer")
	_check(not arena.skinned_player.visible and rig.visible, "battle: original comparison puppet is hidden and new outfit is visible")
	var sword: Variant = rig.wanderer_skin.approved_sword
	# Put the real enemy in blade range, but retain its existing recovery state
	# for a deterministic visual-only test with no unrelated bite damage.
	arena.enemy = arena.player + Vector2(10.0, 0.0)
	arena.wolf_state = "recover"
	arena.wolf_clock = 1000.0
	arena.ensure_fist_combo()
	var before := _combat_snapshot(arena)
	_key(arena, KEY_9)
	arena._process(STEP)
	_check(arena.outfit_sword_visible and sword.mode == "hold" and sword.sword_sprite.visible, "battle: actual key 9 equips the visible independent prop")
	_key(arena, KEY_0)
	_check(sword.mode == "swing" and not sword.swing_loop, "battle: actual key 0 starts one inspection swing")
	var saw_motion := false
	var held_transform: Transform2D = sword.sword_sprite.global_transform
	for frame in range(12):
		arena._process(STEP)
		_measure(rig)
		if not sword.sword_sprite.global_transform.is_equal_approx(held_transform): saw_motion = true
	_check(saw_motion and sword.swing_elapsed > 0.0, "battle: inspection swing changes actual rendered sword transform")
	_key(arena, KEY_TAB)
	_check(arena.combat_paused(), "battle: actual configuration key pauses combat")
	var paused_pose := _pose_snapshot(rig)
	var paused_state := _combat_snapshot(arena)
	var paused_clock: float = arena.motion_time
	for frame in range(24): arena._process(STEP)
	_check(_pose_snapshot(rig) == paused_pose and arena.motion_time == paused_clock, "battle: pause freezes swing time, all visual bones and motion clock")
	_check(_combat_snapshot(arena) == paused_state, "battle: paused preview changes no combat state")
	_check(not arena.preview_outfit_swing(), "battle: paused scene rejects a new swing")
	_key(arena, KEY_TAB)
	for frame in range(50):
		arena._process(STEP)
		_measure(rig)
	_check(sword.mode == "hold" and sword.sword_sprite.visible, "battle: one swing completes into held sword")
	var after := _combat_snapshot(arena)
	_check(after == before, "battle: near enemy inspection swing creates no attack, damage, cost, cooldown, resource, range, movement or hit effect")
	evidence.battle_preview_before = before
	evidence.battle_preview_after = after
	evidence.battle_enemy_fixture = "Real wolf held in its existing recovery state at ten units for contact-range no-damage isolation."
	# Exercise the existing unarmed action itself; no synthetic pending flag.
	arena.use_skill("拳掌")
	arena.update_skinned_player()
	_check(arena.fist_combo.active and arena.pending_attack == "拳掌" and sword.mode == "off", "battle: actual existing fist combo takes over and sheathes visual sword")
	_check(not arena.preview_outfit_swing(), "battle: real unarmed action rejects inspection swing")
	for frame in range(64): arena._process(STEP)
	_check(not arena.fist_combo.active and sword.mode == "hold", "battle: completed existing fist action restores held prop")
	# Actual cast also temporarily owns both hands and retains its own cost/gain.
	var cast_stamina: float = arena.stamina
	var cast_blood: int = arena.qi_blood
	arena.use_skill("催血")
	arena.update_skinned_player()
	_check(arena.pending_self_skill == "催血" and sword.mode == "off", "battle: actual existing self cast takes over and sheathes prop")
	_check(arena.stamina == cast_stamina - arena.skill_cost("催血"), "battle: existing cast alone pays its original stamina cost")
	for frame in range(66): arena._process(STEP)
	_check(arena.pending_self_skill.is_empty() and arena.qi_blood == cast_blood + 2 and sword.mode == "hold", "battle: existing cast completes original gain and restores prop")
	# Route real held direction input through the actual arena process.
	arena.enemy = Vector2(2900, 1600)
	var movement_cases: Array = []
	for code in [KEY_RIGHT, KEY_LEFT]:
		var direction: float = 1.0 if code == KEY_RIGHT else -1.0
		_point_mouse_at(arena, arena.player + Vector2(direction * 220.0, 0.0))
		_held_key(code, true)
		var start: Vector2 = arena.player
		for frame in range(30):
			_point_mouse_at(arena, arena.player + Vector2(direction * 220.0, 0.0))
			arena._process(STEP)
			_measure(rig)
		_held_key(code, false)
		var moved: Vector2 = arena.player - start
		_check(moved.x * direction > 50.0, "battle: actual held direction input moves character " + str(code))
		if DisplayServer.get_name() == "headless":
			# Headless has no native window pointer. Exercise the real visual
			# consumer's facing input explicitly; native aim is a separate review.
			arena.facing = direction
			arena.update_skinned_player()
			_check(signf(rig.scale.x) == direction, "battle: explicit facing input mirrors actual visible outfit " + str(code))
		else:
			_check(signf(arena.facing) == direction and signf(rig.scale.x) == direction, "battle: actual native aim turn mirrors visible outfit " + str(code))
		_check(sword.mode == "hold" and sword.sword_sprite.visible, "battle: held sword survives real movement and turning " + str(code))
		movement_cases.append({"key":code, "delta":str(moved), "facing":arena.facing, "clip":rig.wanderer_skin.last_clip})
	_key(arena, KEY_9)
	arena._process(STEP)
	_check(not arena.outfit_sword_visible and sword.mode == "off" and not sword.sword_sprite.visible, "battle: actual key 9 returns to ordinary hands")
	evidence.battle = {"scene":scene_path, "movement_cases":movement_cases, "skin":rig.wanderer_skin.get_garment_diagnostics(), "preview_no_gameplay_side_effects":before == after, "native_mouse_aim_validated":DisplayServer.get_name() != "headless", "turn_validation":"Actual native aim input" if DisplayServer.get_name() != "headless" else "Explicit facing through actual visual consumer; headless has no native pointer. Native mouse aim requires separate window review."}
	arena.queue_free()
	await get_tree().process_frame
	# Allow the existing arena's delayed window-state restoration to finish.
	await get_tree().create_timer(0.15).timeout

func _finish() -> void:
	var root_directory: String = ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().get_base_dir().get_base_dir()
	var directory := root_directory.path_join("output/approved_outfit_game_integration_20261105")
	DirAccess.make_dir_recursive_absolute(directory)
	var report := {"passed":failures.is_empty(), "checks":checks, "failures":failures, "samples":sample_count, "project_root":ProjectSettings.globalize_path("res://"), "maximum_fixed_bone_length_error":maximum_bone_length_error, "maximum_registered_wrist_error":maximum_wrist_error, "maximum_common_palm_width_error":maximum_palm_error, "maximum_sword_grip_error":maximum_grip_error, "evidence":evidence, "scope":"Actual shared load_runtime, measured source registration, continuous robe and fixed bones; root project also exercises unchanged formal WorldActionTraveler and actual battle scene key/control interfaces. Visual acceptance remains a native rendered review."}
	var file_name := "nested.json" if "--runtime-only" in OS.get_cmdline_user_args() else "root.json"
	var file := FileAccess.open(directory.path_join(file_name), FileAccess.WRITE)
	if file == null:
		push_error("Cannot save integration evidence: " + directory)
		get_tree().quit(2)
		return
	file.store_string(JSON.stringify(report, "\t"))
	file.close()
	print("APPROVED OUTFIT GAME INTEGRATION ", "PASS" if failures.is_empty() else "FAIL", ": ", checks, " checks, ", sample_count, " samples; ", JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
