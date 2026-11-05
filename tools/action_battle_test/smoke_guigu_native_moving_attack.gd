extends Node2D

## Exercises the actual arena input/process path and the NPC presentation override.
## Geometry comparisons remove actor travel and use the untouched source evaluator.
const DT := 1.0 / 60.0
var checks := 0
var failures: Array[String] = []
var resource_dir := ""
var project_dir := ""
var frames: Array = []
var groups: Dictionary = {}
var maximum_foot_local_position_error := 0.0
var maximum_foot_local_rotation_error := 0.0
var maximum_foot_local_basis_error := 0.0
var maximum_foot_local_scale_error := 0.0
var maximum_attack_upper_local_basis_error := 0.0
var moving_attack_frames := 0
var maximum_all_local_position_error := 0.0
var maximum_all_local_scale_error := 0.0
var maximum_sole_penetration := 0.0
var sampled_frames := 0
var foot_samples := 0
var pressed_key := 0
var actual_step_frames := 0
var step_world_transform_samples := 0
var maximum_step_world_position_error := 0.0
var maximum_step_world_basis_error := 0.0
var maximum_step_ground_offset_error := 0.0
var step_context_preserved := true
var previous_stride_clocks: Dictionary = {}
var speed_driven_stride_samples := 0
var maximum_speed_driven_stride_error := 0.0

func _ready() -> void:
	resource_dir = get_script().resource_path.get_base_dir()
	project_dir = ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().get_base_dir().get_base_dir()
	call_deferred("_run")

func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		push_error("MOVING ATTACK: " + label)

func _has_property(object: Object, name: String) -> bool:
	for entry in object.get_property_list():
		if str(entry.name) == name: return true
	return false

func _key(value: int) -> void:
	if pressed_key == value: return
	if pressed_key != 0:
		var released := InputEventKey.new()
		released.keycode = pressed_key
		released.physical_keycode = pressed_key
		released.pressed = false
		Input.parse_input_event(released)
	pressed_key = value
	if pressed_key != 0:
		var event := InputEventKey.new()
		event.keycode = pressed_key
		event.physical_keycode = pressed_key
		event.pressed = true
		Input.parse_input_event(event)
	Input.flush_buffered_events()

func _click(arena: Node2D, held: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = held
	arena._input(event)

func _legs(skin: RefCounted) -> Array:
	var result := []
	for record in skin.mesh_records:
		if int(record.sprite_id) == 87557: result.append(record)
	return result

func _leg_vertices(skin: RefCounted) -> PackedVector2Array:
	var result := PackedVector2Array()
	for record in _legs(skin): result.append_array(record.mesh.polygon)
	return result

func _vertex_distance(a: PackedVector2Array, b: PackedVector2Array) -> float:
	if a.size() != b.size(): return INF
	var result := 0.0
	for index in range(a.size()): result = maxf(result, a[index].distance_to(b[index]))
	return result

func _sole_y(skin: RefCounted) -> float:
	var lowest := -INF
	for record in _legs(skin):
		var points: PackedVector2Array = record.mesh.polygon
		for sample in skin.grounding_profile.source_solid_sole_sample_hull:
			var ids: Array = sample.triangle_vertices
			var weights: Array = sample.barycentric_weights
			var point: Vector2 = points[int(ids[0])] * float(weights[0]) + points[int(ids[1])] * float(weights[1]) + points[int(ids[2])] * float(weights[2])
			lowest = maxf(lowest, (skin.runtime.rig_root_node.transform * point).y)
	return lowest

func _local(world: Dictionary, node: Dictionary) -> Transform3D:
	var result: Transform3D = world[node.path]
	if node.parentPath != null and world.has(node.parentPath): result = (world[node.parentPath] as Transform3D).affine_inverse() * result
	return result

func _basis_error(a: Basis, b: Basis) -> float:
	return maxf(a.x.distance_to(b.x), maxf(a.y.distance_to(b.y), a.z.distance_to(b.z)))

func _compare_run(skin: RefCounted, arena: Node2D) -> void:
	var source_time := fposmod(float(skin.locomotion_time), float(skin.clips.Run_New1.duration))
	var source: Dictionary = skin.evaluate_animation("Run_New1", source_time)
	var attack_source := {}
	if bool(skin.compose_state.moving_attack):
		moving_attack_frames += 1
		var upper_clip := str(skin.compose_state.upper_clip)
		var selected: Array = arena._current_player_animation()
		var upper_time := float(selected[1]) if str(selected[0]) == upper_clip else 0.0
		upper_time = clampf(upper_time, 0.0, float(skin.clips[upper_clip].duration))
		attack_source = skin.evaluate_animation(upper_clip, upper_time)
	for node in skin.rig_data.nodes:
		var actual := _local(skin.animated_world, node)
		var expected := _local(source, node)
		var position_error := actual.origin.distance_to(expected.origin)
		var scale_error := actual.basis.get_scale().distance_to(expected.basis.get_scale())
		maximum_all_local_position_error = maxf(maximum_all_local_position_error, position_error)
		maximum_all_local_scale_error = maxf(maximum_all_local_scale_error, scale_error)
		if str(node.path).begins_with(str(skin.rig_data.rigRootPath) + "/Body/Foot"):
			foot_samples += 1
			maximum_foot_local_position_error = maxf(maximum_foot_local_position_error, position_error)
			maximum_foot_local_scale_error = maxf(maximum_foot_local_scale_error, scale_error)
			maximum_foot_local_basis_error = maxf(maximum_foot_local_basis_error, _basis_error(actual.basis, expected.basis))
			var rotation_error := actual.basis.get_rotation_quaternion().angle_to(expected.basis.get_rotation_quaternion())
			maximum_foot_local_rotation_error = maxf(maximum_foot_local_rotation_error, rotation_error)
		if not attack_source.is_empty() and (str(node.path) == str(skin.attachment_paths.torso) or str(node.path).begins_with(str(skin.attachment_paths.torso) + "/")):
			var attack_local := _local(attack_source, node)
			maximum_attack_upper_local_basis_error = maxf(maximum_attack_upper_local_basis_error, _basis_error(actual.basis, attack_local.basis))

func _sample(arena: Node2D, group: String, compare_run := true) -> void:
	var skin: RefCounted = arena.original_wanderer.wanderer_skin
	var sole := _sole_y(skin)
	maximum_sole_penetration = maxf(maximum_sole_penetration, sole)
	sampled_frames += 1
	if compare_run: _compare_run(skin, arena)
	var stage: int = arena.fist_combo.stage if arena.fist_combo != null and arena.fist_combo.active else -1
	var speed_ratio: float = arena.visual_motion_velocity.length() / float(skin.ROBED_MOVE_SPEED) if bool(skin.compose_state.locomotion_active) else 0.0
	var stride_error := 0.0
	var actor_id := arena.get_instance_id()
	if previous_stride_clocks.has(actor_id) and float(skin.locomotion_time) >= float(previous_stride_clocks[actor_id]):
		var actual_advance := float(skin.locomotion_time) - float(previous_stride_clocks[actor_id])
		var expected_advance: float = float(arena.visual_motion_delta) * speed_ratio
		stride_error = absf(actual_advance - expected_advance)
		maximum_speed_driven_stride_error = maxf(maximum_speed_driven_stride_error, stride_error)
		speed_driven_stride_samples += 1
	previous_stride_clocks[actor_id] = float(skin.locomotion_time)
	var data := {"group":group, "clock":arena.motion_time, "leg_clock":skin.locomotion_time, "stage":stage, "attack_phase":arena.fist_combo.progress() if stage >= 0 else -1.0, "velocity":[arena.visual_motion_velocity.x, arena.visual_motion_velocity.y], "speed_to_gait_ruler_ratio":speed_ratio, "leg_clock_advance_error":stride_error, "facing":arena.facing, "sole_y":sole, "composition":skin.compose_state.duplicate()}
	frames.append(data)
	if not groups.has(group): groups[group] = {"frames":0, "initial_legs":_leg_vertices(skin), "maximum_local_leg_vertex_displacement_pixels":0.0, "minimum_speed_to_gait_ruler_ratio":INF, "maximum_speed_to_gait_ruler_ratio":0.0, "maximum_leg_clock_advance_error":0.0}
	var entry: Dictionary = groups[group]
	entry.frames += 1
	entry.maximum_local_leg_vertex_displacement_pixels = maxf(float(entry.maximum_local_leg_vertex_displacement_pixels), _vertex_distance(entry.initial_legs, _leg_vertices(skin)))
	entry.minimum_speed_to_gait_ruler_ratio = minf(float(entry.minimum_speed_to_gait_ruler_ratio), speed_ratio)
	entry.maximum_speed_to_gait_ruler_ratio = maxf(float(entry.maximum_speed_to_gait_ruler_ratio), speed_ratio)
	entry.maximum_leg_clock_advance_error = maxf(float(entry.maximum_leg_clock_advance_error), stride_error)

func _tick(arena: Node2D, count: int, group: String, compare_run := true) -> void:
	for frame in range(count):
		arena._process(DT)
		_sample(arena, group, compare_run)

func _pose_snapshot(skin: RefCounted) -> Array:
	var result := [skin.runtime.transform, skin.runtime.rig_root_node.transform]
	for record in skin.mesh_records: result.append(record.mesh.polygon.duplicate())
	return result

func _actual_run_to_step(arena: Node2D) -> void:
	var skin: RefCounted = arena.original_wanderer.wanderer_skin
	arena.inspection_facing_override = 1.0
	_key(KEY_RIGHT)
	_tick(arena, 18, "run_before_dodge", false)
	_check(str(arena._current_player_animation()[0]) == "Run_New1" and str(skin.compose_state.base_clip) == "Run_New1", "actual held movement selects Run before the dedicated dodge transition")
	arena.use_skill("错步")
	for frame in range(16):
		arena._process(DT)
		_sample(arena, "run_to_dodge", false)
		var selected: Array = arena._current_player_animation()
		if str(selected[0]) != "Step1": continue
		actual_step_frames += 1
		var source_time := fposmod(float(selected[1]), float(skin.clips.Step1.duration))
		var original: Dictionary = skin.evaluate_animation("Step1", source_time)
		for node in skin.rig_data.nodes:
			var actual: Transform3D = skin.animated_world[node.path]
			var expected: Transform3D = original[node.path]
			maximum_step_world_position_error = maxf(maximum_step_world_position_error, actual.origin.distance_to(expected.origin))
			maximum_step_world_basis_error = maxf(maximum_step_world_basis_error, _basis_error(actual.basis, expected.basis))
			step_world_transform_samples += 1
		maximum_step_ground_offset_error = maxf(maximum_step_ground_offset_error, absf(float(skin.runtime.rig_root_node.position.y) - float(skin.grounding_profile.clips.Step1.render_offset_y)))
		step_context_preserved = step_context_preserved and str(skin.compose_state.base_clip) == "Step1" and float(skin.compose_state.stop_blend) == 1.0
	_check(actual_step_frames >= 8 and step_world_transform_samples == actual_step_frames * 51 and maximum_step_world_position_error < 0.000001 and maximum_step_world_basis_error < 0.000001, "every actual Run-to-dodge frame retains all51 pure source Step1 animated world TRS before native secondary physics")
	_check(step_context_preserved and maximum_step_ground_offset_error < 0.000001, "Run-to-dodge retains its dedicated Step1 whole-actor landing offset with no ordinary stop blend")
	_key(0)
	_tick(arena, 20, "after_run_dodge_stop", false)

func _run() -> void:
	var scene_path := "res://tscn/scene/ActionBattleOriginalWandererScene.tscn"
	var arena: Node2D
	if ResourceLoader.exists(scene_path): arena = load(scene_path).instantiate()
	else:
		arena = load(resource_dir.path_join("action_battle_feel_test.gd")).new()
		arena.original_wanderer_enabled = true
		arena.original_wanderer_art_folder = "yuxu_fullbody_v15"
	add_child(arena)
	arena.set_process(false)
	arena.set_process_input(false)
	var skin: RefCounted = arena.original_wanderer.wanderer_skin
	_check(arena.original_wanderer.native_source_mode, "actual arena uses source-native shared appearance loader")
	_check(skin.locomotion_layer_enabled, "actual arena enables the new movement and attack composition layer")
	_check(is_equal_approx(float(arena.move_speed), 235.0) and is_equal_approx(float(skin.ROBED_MOVE_SPEED), 235.0), "actual trial ordinary travel and native gait ruler both use the accepted235px/s speed")
	_check(_has_property(skin, "locomotion_time"), "source-native appearance exposes independent locomotion clock")
	if not _has_property(skin, "locomotion_time"):
		arena.queue_free()
		_finish()
		return
	arena.player = Vector2(1200, 900)
	arena.enemy = Vector2(3000, 1700)
	arena.enemy_timer = 100000.0
	arena.inspection_facing_override = 1.0
	_key(KEY_RIGHT)
	_check(arena.movement_direction().x > 0.99, "synthetic key enters actual held movement-input path")
	_tick(arena, 18, "run_warmup", false)
	_click(arena, true)
	_check(arena.held_basic_attack and arena.fist_combo.active, "actual left-button input starts held four-stage combo")
	var stages := {}
	var transitions := 0
	var previous_stage := -1
	var previous_leg_clock := float(skin.locomotion_time)
	var phase_continuous := true
	for frame in range(142):
		arena._process(DT)
		_sample(arena, "held_combo")
		if arena.fist_combo.active:
			stages[arena.fist_combo.stage] = true
			if arena.fist_combo.stage != previous_stage and previous_stage >= 0:
				transitions += 1
				phase_continuous = phase_continuous and float(skin.locomotion_time) >= previous_leg_clock and absf(float(skin.locomotion_time) - previous_leg_clock) < DT * 4.0
			previous_stage = arena.fist_combo.stage
		previous_leg_clock = float(skin.locomotion_time)
	_click(arena, false)
	_check(stages.size() == 4 and transitions >= 3, "continuous held input traverses all four real combo stages")
	_check(phase_continuous, "real attack-stage phase resets leave independent moving leg phase continuous")
	_check(float(groups.held_combo.maximum_local_leg_vertex_displacement_pixels) > 5.0, "two actual local leg meshes continue changing during moving attacks")
	_check(not arena.fist_combo.active, "full held four-stage combo ends through the existing runtime")
	arena.use_skill("痛击")
	_check(arena.pending_attack == "痛击", "actual heavy-attack action starts without fixture-only pose selection")
	_tick(arena, 18, "moving_heavy")
	arena.use_skill("架招")
	_check(arena.guard_window > 0.0, "actual guard action starts while moving")
	_tick(arena, 9, "moving_guard")
	_tick(arena, 8, "guard_finish")
	arena.use_skill("催血")
	_check(arena.pending_self_skill == "催血", "actual self-cast action starts while moving")
	_tick(arena, 70, "moving_self_cast")
	_check(float(groups.moving_self_cast.maximum_local_leg_vertex_displacement_pixels) > 5.0, "self-cast reduced movement still advances real source legs")
	_check(absf(float(groups.moving_self_cast.minimum_speed_to_gait_ruler_ratio) - 0.5) < 0.001, "actual half-speed self-cast drives the source legs at half cadence")
	_key(KEY_LEFT)
	_tick(arena, 36, "backward")
	_check(arena.visual_motion_velocity.x < 0.0 and arena.facing > 0.0, "actual movement input covers backward travel relative to facing")
	arena.inspection_facing_override = -1.0
	_tick(arena, 36, "left_facing")
	_check(arena.original_wanderer.scale.x < 0.0, "actual shared actor still mirrors left-facing source geometry")
	_key(0)
	arena._process(DT)
	var stopped_clock := float(skin.locomotion_time)
	_tick(arena, 14, "stop_settle", false)
	var settled := _leg_vertices(skin)
	_tick(arena, 36, "stopped", false)
	_check(is_equal_approx(float(skin.locomotion_time), stopped_clock), "stopped actual velocity freezes the leg phase throughout settling and idle")
	_check(_vertex_distance(settled, _leg_vertices(skin)) < 0.0001, "settled stopped legs stay at stable Idle pose instead of cycling in place")
	_check(absf(_sole_y(skin)) < 0.15, "settled real source-alpha sole samples touch actor ground within0.15 native pixels")
	_key(KEY_RIGHT)
	_tick(arena, 18, "resume_run", false)
	var before_pause := _pose_snapshot(skin)
	var pause_clock := float(skin.locomotion_time)
	arena.configuring = true
	_tick(arena, 18, "paused", false)
	_check(_pose_snapshot(skin) == before_pause and skin.locomotion_time == pause_clock, "actual arena configuration pause freezes complete geometry, physics and leg phase")
	arena.configuring = false
	_tick(arena, 18, "resume_after_pause")
	var duplicate := _pose_snapshot(skin)
	var duplicate_clock := float(skin.locomotion_time)
	arena.update_skinned_player()
	arena.update_skinned_player()
	_check(_pose_snapshot(skin) == duplicate and skin.locomotion_time == duplicate_clock, "duplicate actual pose requests at one motion clock never advance legs or cloth")
	_click(arena, true)
	_tick(arena, 8, "before_cancel")
	_key(0)
	arena._process(DT)
	_sample(arena, "stop_during_attack", false)
	var attack_stop_clock := float(skin.locomotion_time)
	_tick(arena, 12, "stop_during_attack", false)
	_check(arena.fist_combo.active and skin.locomotion_time == attack_stop_clock and _sole_y(skin) < 0.15, "stopping during a real combo freezes running legs and settles the alpha sole while the attack continues")
	var paused_attack := _pose_snapshot(skin)
	arena.configuring = true
	_tick(arena, 8, "attack_paused", false)
	_check(_pose_snapshot(skin) == paused_attack and skin.locomotion_time == attack_stop_clock, "pausing a real stopped combo freezes the source attack, stopped legs and cloth")
	arena.configuring = false
	_key(KEY_RIGHT)
	_tick(arena, 8, "resume_during_attack")
	arena.use_skill("错步")
	_check(not arena.fist_combo.active and arena.dash_move > 0.0, "actual dodge cancellation preserves existing attack interruption behavior")
	_click(arena, false)
	_tick(arena, 16, "cancel_dodge", false)
	_tick(arena, 18, "after_cancel")
	_key(0)
	arena.restart_battle()
	_check(not skin.physics_initialized, "actual restart discards previous cloth transport")
	arena.enemy = Vector2(3000, 1700)
	arena.enemy_timer = 100000.0
	_tick(arena, 20, "restart_idle", false)
	_check(absf(_sole_y(skin)) < 0.15, "restart settles on the actual alpha sole baseline")
	_actual_run_to_step(arena)
	_check(maximum_foot_local_position_error < 0.000001 and maximum_foot_local_scale_error < 0.000001 and maximum_foot_local_basis_error < 0.000001, "moving attacks retain source Run foot local TRS at the exact independent leg clock")
	_check(maximum_all_local_position_error < 0.000001 and maximum_all_local_scale_error < 0.000001, "upper-body action composition retains every source Run local position and scale without stretching")
	_check(moving_attack_frames >= 200 and maximum_attack_upper_local_basis_error < 0.000001, "real moving attacks and held action contexts overlay source Attack upper-body rotations while legs run")
	_check(maximum_sole_penetration < 0.15, "all sampled moving, attacking, turning, stopping and paused alpha soles avoid ground penetration")
	_run_npc_override(arena)
	_check(speed_driven_stride_samples > 500 and maximum_speed_driven_stride_error < 0.000001, "every measured actual movement frame advances the independent leg clock by actual speed divided by the native gait ruler; stops pauses and Step1 do not advance it")
	for group in groups.values(): group.erase("initial_legs")
	arena.queue_free()
	await get_tree().process_frame
	_finish()

func _run_npc_override(owner_arena: Node2D) -> void:
	# The production override is exercised directly. No NPC AI/save/attribute
	# fixture is introduced into this visual regression.
	var npc: Node2D = load(resource_dir.path_join("human_sparring_fighter.gd")).new()
	add_child(npc)
	npc.arena = owner_arena
	npc.set_process(false)
	npc.set_process_input(false)
	var actor: Node2D = load(resource_dir.path_join("guigu_rig_runtime.gd")).new()
	npc.add_child(actor)
	_check(actor.load_runtime("", "", "", "", "yuxu_fullbody_v15"), "NPC fixture builds actual shared source-native appearance loader")
	npc.original_wanderer = actor
	npc.original_wanderer_enabled = true
	npc.original_wanderer_art_folder = "yuxu_fullbody_v15"
	npc.player = Vector2(1400, 900)
	npc.facing = 1.0
	var skin: RefCounted = actor.wanderer_skin
	for frame in range(42):
		npc.motion_time += DT
		npc.player.x += 114.0 * DT
		npc.visual_motion_delta = DT
		npc.visual_motion_velocity = Vector2(114, 0)
		npc.walk_amount = 1.0
		npc.visual_attack_time = fposmod(frame * DT, 0.167)
		npc.pending_self_skill = "催血" if frame >= 20 else ""
		npc.update_skinned_player()
		_sample(npc, "npc_override", frame >= 12)
	_check(float(groups.npc_override.maximum_local_leg_vertex_displacement_pixels) > 5.0, "actual NPC presentation override advances source legs during attack and self-cast context")
	_check(maximum_foot_local_position_error < 0.000001 and maximum_foot_local_basis_error < 0.000001, "NPC override and player share the same original Run foot branch")
	npc.queue_free()

func _finish() -> void:
	_key(0)
	var root_path := ProjectSettings.globalize_path("res://").simplify_path().trim_suffix("/")
	var suffix := "root" if root_path == project_dir else "nested"
	var directory := project_dir.path_join("output/guigu_native_first_20261105/moving_attack")
	DirAccess.make_dir_recursive_absolute(directory)
	var report := {"passed":failures.is_empty(), "checks":checks, "failures":failures, "locomotion_composition_enabled":true, "sampled_actual_frames":sampled_frames, "source_foot_local_samples":foot_samples, "moving_attack_frames":moving_attack_frames, "maximum_source_foot_local_position_error_units":maximum_foot_local_position_error, "maximum_source_foot_local_rotation_error_radians":maximum_foot_local_rotation_error, "maximum_source_foot_local_basis_error":maximum_foot_local_basis_error, "maximum_source_foot_local_scale_error":maximum_foot_local_scale_error, "maximum_source_attack_upper_local_basis_error":maximum_attack_upper_local_basis_error, "maximum_source_all_local_position_error_units":maximum_all_local_position_error, "maximum_source_all_local_scale_error":maximum_all_local_scale_error, "maximum_alpha_sole_penetration_native_pixels":maximum_sole_penetration, "groups":groups, "scope":"Actual arena held movement keys, actual left-button held four-stage attack input, original heavy/guard/self-cast/cancel runtimes; source Run local TRS oracle;65 opaque-alpha source sole hull samples; actual NPC presentation override exercised separately without AI or gameplay claims."}
	report.run_to_dodge = {"actual_step_frames":actual_step_frames, "animated_world_transform_samples":step_world_transform_samples, "maximum_source_world_position_error_units":maximum_step_world_position_error, "maximum_source_world_basis_error":maximum_step_world_basis_error, "maximum_source_dedicated_landing_offset_error_pixels":maximum_step_ground_offset_error, "dedicated_step_context_preserved":step_context_preserved, "scope":"All51 source animated world TRS are compared before native secondary physics; source DynamicBone remains active in the actual rendered actor."}
	report.speed_driven_gait = {"native_gait_ruler_pixels_per_second":235.0, "measured_actual_frames":speed_driven_stride_samples, "maximum_leg_clock_advance_error_seconds":maximum_speed_driven_stride_error, "scope":"Consecutive actual arena/NPC presentation samples compare independent stride-clock increments with actual velocity magnitude divided by the public native ruler; a reset starts a fresh sequence."}
	FileAccess.open(directory.path_join("moving_attack_" + suffix + ".json"), FileAccess.WRITE).store_string(JSON.stringify(report, "\t"))
	FileAccess.open(directory.path_join("actual_frames_" + suffix + ".json"), FileAccess.WRITE).store_string(JSON.stringify(frames))
	print("NATIVE MOVING ATTACK ", suffix, " ", "PASS" if failures.is_empty() else "FAIL", ": ", checks, " checks, ", sampled_frames, " actual frames; ", JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
