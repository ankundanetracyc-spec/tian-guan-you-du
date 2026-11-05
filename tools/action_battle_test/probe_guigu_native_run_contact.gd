extends Node2D

## Alpha-sole QA of the display-only contact layer against pure source3D/LBS.
const NativeRuntime = preload("guigu_native_first_runtime.gd")
const PHASES := 144
const EPS := 0.15
class PresentationArena extends "human_sparring_arena.gd":
	func _ready() -> void:
		pass

var checks := 0
var failures: Array[String] = []
var cases: Array = []
var frames: Array = []
var project_dir := ""
var maximum_source_leg_vertex_error := 0.0
var maximum_diagnostic_height_error := 0.0
var maximum_shadow_floor_error := 0.0
var maximum_contact_x_error := 0.0
var sampled_source_vertices := 0
var step_world_samples := 0
var maximum_elevated_shadow_world_y_error := 0.0
var elevated_shadow_samples := 0

func _ready() -> void:
	project_dir = ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().get_base_dir().get_base_dir()
	call_deferred("_run")

func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		push_error("RUN CONTACT: " + label)

func _legs(skin: RefCounted) -> Array:
	var result := []
	for record in skin.mesh_records:
		if int(record.sprite_id) == 87557: result.append(record)
	return result

func _sole(points: PackedVector2Array, skin: RefCounted, shift: float) -> Dictionary:
	var lowest := -INF
	var xmin := INF
	var xmax := -INF
	for sample in skin.grounding_profile.source_solid_sole_sample_hull:
		var point := Vector2.ZERO
		for k in range(3): point += points[int(sample.triangle_vertices[k])] * float(sample.barycentric_weights[k])
		lowest = maxf(lowest, point.y + shift)
		xmin = minf(xmin, point.x)
		xmax = maxf(xmax, point.x)
	return {"sole_y":lowest, "xmin":xmin, "xmax":xmax}

func _soles(skin: RefCounted) -> Array:
	var result := []
	for record in _legs(skin): result.append(_sole(record.mesh.polygon, skin, skin.runtime.rig_root_node.position.y))
	return result

func _height(skin: RefCounted) -> float:
	var bottom := -INF
	for sole in _soles(skin): bottom = maxf(bottom, float(sole.sole_y))
	return -bottom

func _snapshot(skin: RefCounted) -> Array:
	var result := [skin.runtime.rig_root_node.transform, skin.contact_shadow.transform]
	for record in skin.mesh_records: result.append(record.mesh.polygon.duplicate())
	return result

func _compare_source(skin: RefCounted) -> float:
	var time := fposmod(float(skin.locomotion_time), float(skin.clips.Run_New1.duration))
	var original: Dictionary = skin.evaluate_animation("Run_New1", time)
	var raw_bottom := -INF
	for record in _legs(skin):
		var expected: PackedVector2Array = skin.deform_mesh(record, original)
		var actual: PackedVector2Array = record.mesh.polygon
		for vertex in range(expected.size()):
			maximum_source_leg_vertex_error = maxf(maximum_source_leg_vertex_error, expected[vertex].distance_to(actual[vertex]))
			sampled_source_vertices += 1
		var raw := _sole(expected, skin, float(skin.grounding_profile.clips.Run_New1.render_offset_y))
		raw_bottom = maxf(raw_bottom, float(raw.sole_y))
	return -raw_bottom

func _measure_shadow(skin: RefCounted, diagnostic: Dictionary) -> void:
	maximum_shadow_floor_error = maxf(maximum_shadow_floor_error, absf(skin.contact_shadow.global_position.y - skin.runtime.global_position.y))
	var measured := _soles(skin)
	for index in range(mini(diagnostic.contacts.size(), measured.size())):
		var item: Dictionary = diagnostic.contacts[index]
		var sole: Dictionary = measured[index]
		var x := float(item.x)
		maximum_contact_x_error = maxf(maximum_contact_x_error, maxf(float(sole.xmin) - x, x - float(sole.xmax)))

func _longest_flight(heights: Array) -> int:
	var longest := 0
	var current := 0
	for index in range(heights.size() * 2):
		if float(heights[index % heights.size()]) > EPS:
			current += 1
			longest = maxi(longest, current)
		else: current = 0
	return mini(longest, heights.size())

func _new_actor() -> Node2D:
	var actor: Node2D = NativeRuntime.new()
	add_child(actor)
	_check(actor.build(), "actual native runtime builds")
	actor.position = Vector2(900, 700)
	return actor

func _cycle(label: String, facing: float, velocity: Vector2, attack: bool, cast: bool) -> void:
	var actor := _new_actor()
	var skin: RefCounted = actor.wanderer_skin
	_check(skin.ground_contact_enabled and skin.locomotion_layer_enabled, label + ": actual ground and movement layers enabled")
	_check(skin.has_contact_shadow() and skin.contact_shadow.get_parent() == actor, label + ": native shoe shadow outside displaced drawing root")
	var rate: float = velocity.length() / float(skin.ROBED_MOVE_SPEED)
	var dt := float(skin.clips.Run_New1.duration) / PHASES / rate
	var clock := 0.0
	var heights := []
	var contacts := 0
	var literal_contacts := 0
	var maximum_height := -INF
	var minimum_height := INF
	for frame in range(PHASES):
		clock += dt
		actor.position += velocity * dt
		skin.set_motion_context(dt, velocity, clock, facing)
		skin.set_action_context(cast, false)
		skin.set_combo_context(1 if attack else 0, float(frame) / PHASES)
		actor.set_character_pose("AttackIdle" if attack else "Run_New1", fposmod(clock, float(skin.clips.AttackIdle.duration)) if attack else clock, facing)
		var height := _height(skin)
		var raw := _compare_source(skin)
		var diagnostic: Dictionary = skin.get_contact_diagnostics()
		maximum_diagnostic_height_error = maxf(maximum_diagnostic_height_error, maxf(absf(height - float(diagnostic.flight_height)), absf(raw - float(diagnostic.raw_flight_height))))
		_measure_shadow(skin, diagnostic)
		heights.append(height)
		if absf(height) <= EPS: contacts += 1
		if absf(height) <= 0.0001: literal_contacts += 1
		maximum_height = maxf(maximum_height, height)
		minimum_height = minf(minimum_height, height)
		frames.append({"case":label, "phase":float(frame + 1) / PHASES, "leg_clock":skin.locomotion_time, "raw_height":raw, "actual_height":height, "render_y":actor.rig_root_node.position.y, "contact":diagnostic})
	var longest := _longest_flight(heights)
	_check(contacts >= 108 and literal_contacts >= 90 and contacts < PHASES, label + ": substantial opaque-shoe contact with retained visible flight")
	_check(maximum_height <= 2.15 and maximum_height > 0.5 and minimum_height >= -EPS, label + ": alpha soles stay below2.15px flight and avoid penetration")
	_check(float(longest) * dt * rate <= 0.1, label + ": visible flight remains below0.1 source seconds")
	cases.append({"case":label, "phases":PHASES, "gait_rate":rate, "contact_frames_within0_15px":contacts, "literal_ground_contact_frames":literal_contacts, "contact_fraction":float(contacts) / PHASES, "maximum_visible_shoe_height_pixels":maximum_height, "minimum_visible_shoe_height_pixels":minimum_height, "longest_visible_flight_frames":longest, "longest_visible_flight_wall_seconds":longest * dt, "longest_visible_flight_source_seconds":longest * dt * rate})
	_stop_pause_step(actor, skin, clock, facing, label)
	actor.queue_free()

func _stop_pause_step(actor: Node2D, skin: RefCounted, clock: float, facing: float, label: String) -> void:
	var stopped_clock := float(skin.locomotion_time)
	var penetration := 0.0
	for frame in range(24):
		clock += 1.0 / 60.0
		skin.set_motion_context(1.0 / 60.0, Vector2.ZERO, clock, facing)
		skin.set_action_context(false, false)
		skin.set_combo_context(0, 0.0)
		actor.set_character_pose("Idle", clock, facing)
		penetration = maxf(penetration, -_height(skin))
	_check(skin.locomotion_time == stopped_clock and absf(_height(skin)) < EPS and penetration < EPS, label + ": stop settles onto the alpha sole with frozen stride")
	var before := _snapshot(skin)
	for frame in range(4):
		skin.set_motion_context(0.0, Vector2.ZERO, clock, facing)
		actor.set_character_pose("Idle", clock, facing)
	_check(_snapshot(skin) == before, label + ": paused duplicate clocks freeze geometry and shadow")
	var maximum_step_error := 0.0
	for frame in range(12):
		clock += 1.0 / 60.0
		var time := fposmod(float(frame + 1) / 60.0, float(skin.clips.Step1.duration))
		skin.set_motion_context(1.0 / 60.0, Vector2(500 * facing, 0), clock, facing)
		actor.set_character_pose("Step1", time, facing)
		var expected: Dictionary = skin.evaluate_animation("Step1", time)
		for node in skin.rig_data.nodes:
			var a: Transform3D = skin.animated_world[node.path]
			var b: Transform3D = expected[node.path]
			maximum_step_error = maxf(maximum_step_error, a.origin.distance_to(b.origin))
			maximum_step_error = maxf(maximum_step_error, a.basis.x.distance_to(b.basis.x))
			maximum_step_error = maxf(maximum_step_error, a.basis.y.distance_to(b.basis.y))
			maximum_step_error = maxf(maximum_step_error, a.basis.z.distance_to(b.basis.z))
			step_world_samples += 1
		maximum_step_error = maxf(maximum_step_error, absf(actor.rig_root_node.position.y - float(skin.grounding_profile.clips.Step1.render_offset_y)))
	_check(maximum_step_error < 0.000001 and skin.locomotion_time == stopped_clock, label + ": Step1 preserves all source world TRS and fixed landing")

func _run() -> void:
	var actor := _new_actor()
	var skin: RefCounted = actor.wanderer_skin
	_check(skin.has_method("get_contact_diagnostics"), "actual shared skin exposes contact diagnostics")
	if not skin.has_method("get_contact_diagnostics"):
		actor.queue_free()
		_finish()
		return
	actor.queue_free()
	_cycle("run_right", 1.0, Vector2(235, 0), false, false)
	_cycle("run_left", -1.0, Vector2(-235, 0), false, false)
	_cycle("moving_attack_right", 1.0, Vector2(235, 0), true, false)
	_cycle("moving_attack_left", -1.0, Vector2(-235, 0), true, false)
	_cycle("half_speed_self_cast", 1.0, Vector2(117.5, 0), false, true)
	_cycle("backward_right_facing", 1.0, Vector2(-235, 0), false, false)
	_check(maximum_source_leg_vertex_error < 0.001, "contact changes only common display offset; actual local legs match pure Run3D LBS")
	_check(maximum_diagnostic_height_error < 0.001, "raw and corrected diagnostics match independently measured alpha soles")
	_check(maximum_shadow_floor_error < 0.001 and maximum_contact_x_error < EPS, "native shadow stays on actor ground and within shoe-alpha X bounds")
	_actual_player_npc_elevation()
	await get_tree().process_frame
	_finish()

func _actual_player_npc_elevation() -> void:
	var arena: Node2D = PresentationArena.new()
	add_child(arena)
	arena.set_process(false)
	arena.set_process_input(false)
	var npc: Node2D = load(get_script().resource_path.get_base_dir().path_join("human_sparring_fighter.gd")).new()
	add_child(npc)
	npc.set_process(false)
	npc.set_process_input(false)
	npc.arena = arena
	for unit in [arena, npc]:
		var actor: Node2D = NativeRuntime.new()
		unit.add_child(actor)
		_check(actor.build(), "actual human presentation override builds native contact actor")
		unit.original_wanderer = actor
		unit.skinned_player = actor
		unit.skinned_player_ready = true
		unit.original_wanderer_enabled = true
		unit.original_wanderer_art_folder = "yuxu_fullbody_v15"
		unit.player = Vector2(1400, 900)
		var role := "player" if unit == arena else "NPC"
		var role_error := 0.0
		for raised in [0.0, 12.0, 0.0]:
			unit.elevation = raised
			unit.motion_time += 1.0 / 60.0
			unit.visual_motion_delta = 1.0 / 60.0
			unit.visual_motion_velocity = Vector2.ZERO
			unit.update_skinned_player()
			var skin: RefCounted = actor.wanderer_skin
			var diagnostic: Dictionary = skin.get_contact_diagnostics()
			_check(absf(actor.global_position.y - (unit.player.y - raised)) < 0.001, role + ": actual override preserves intentional body elevation")
			for item in diagnostic.shadow.drawn:
				var world: Vector2 = item.world_position
				var error := absf(world.y - float(unit.player.y))
				role_error = maxf(role_error, error)
				maximum_elevated_shadow_world_y_error = maxf(maximum_elevated_shadow_world_y_error, error)
				elevated_shadow_samples += 1
		_check(role_error < 0.001, role + ": every actual shadow centre stays on logical world ground through raise and return")
	arena.queue_free()
	npc.queue_free()

func _finish() -> void:
	var suffix := "root" if ProjectSettings.globalize_path("res://").simplify_path().trim_suffix("/") == project_dir else "nested"
	var directory := project_dir.path_join("output/guigu_native_first_20261105/moving_attack")
	DirAccess.make_dir_recursive_absolute(directory)
	var report := {"passed":failures.is_empty(), "checks":checks, "failures":failures, "cases":cases, "sampled_actual_moving_phases":frames.size(), "sampled_source_leg_vertices":sampled_source_vertices, "step_source_world_samples":step_world_samples, "maximum_source_local_leg_vertex_error_pixels":maximum_source_leg_vertex_error, "maximum_reported_alpha_height_error_pixels":maximum_diagnostic_height_error, "maximum_shadow_ground_y_error_pixels":maximum_shadow_floor_error, "maximum_shadow_contact_x_outside_shoe_bounds_pixels":maximum_contact_x_error, "scope":"Actual shared runtime,235/117.5 speeds, both facings, moving Attack, backward travel, opaque-alpha sole hull, untouched source3D/LBS oracle, stop, duplicate-clock pause and dedicated Step1; source textures, bones and combat rules unchanged."}
	report.actual_human_elevation = {"shadow_centres_sampled":elevated_shadow_samples, "maximum_world_ground_y_error_pixels":maximum_elevated_shadow_world_y_error, "scope":"Actual human arena player and NPC update_skinned_player overrides with body elevations0/12/0; native shadow centres must remain at each logical player.y."}
	FileAccess.open(directory.path_join("run_contact_" + suffix + ".json"), FileAccess.WRITE).store_string(JSON.stringify(report, "\t"))
	FileAccess.open(directory.path_join("run_contact_frames_" + suffix + ".json"), FileAccess.WRITE).store_string(JSON.stringify(frames))
	print("NATIVE RUN CONTACT ", suffix, " ", "PASS" if failures.is_empty() else "FAIL", ": ", checks, " checks; ", JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
