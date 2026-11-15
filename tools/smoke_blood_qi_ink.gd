extends SceneTree
## Match the blood layer to the actual basic force's atlas, frame and upright
## basis. Only forward extent, pigment and the existing .38s fade may differ.
const Renderer = preload("res://tools/action_battle_test/manual_blood_vfx.gd")

class FixedSource extends "res://tools/action_battle_test/action_battle_feel_test.gd":
	func attack_aim_direction() -> Vector2:
		return Vector2.RIGHT
	func movement_direction() -> Vector2:
		return Vector2.ZERO

var checks := 0
var failures := 0
var samples := 0
var maximum_transform_error := 0.0
var renderer: Node2D
var source: Node2D


func _initialize() -> void:
	call_deferred("verify")


func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		push_error(label)


func freeze(stage: Node2D) -> void:
	stage.set_process(false)
	stage.set_process_input(false)
	if stage.battle_hud != null:
		stage.battle_hud.set_process_input(false)
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1280, 720)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true


func visible_slots(node: Node2D) -> int:
	var count := 0
	for stamp: Sprite2D in node.slots:
		if stamp.is_visible_in_tree():
			count += 1
	return count


func particle_origins(node: Node2D) -> Array[Vector2]:
	var result: Array[Vector2] = []
	for particle: Dictionary in node.ink._pool:
		if bool(particle.alive):
			result.append(particle.origin)
	return result


func state(arena: Node2D) -> Dictionary:
	var units: Array = [arena]
	units.append_array(arena.npcs)
	var result := {"actors": [], "runtime": []}
	for fighter: Node2D in units:
		result.actors.append([fighter.hp, fighter.enemy_hp, fighter.stamina, fighter.mana,
			fighter.qi_blood, fighter.qi, fighter.skill_cooldowns.duplicate(true),
			fighter.pending_attack, fighter.pending_self_skill, fighter.self_cast_remaining])
	for index in range(4):
		result.runtime.append(arena.attribute_runtime.get_unit_state(index).duplicate(true))
	return result


func compare_to_basic(stamp: Sprite2D, reference: Sprite2D, plane: Vector2, ratio: float) -> void:
	# Compare to the real ordinary Sprite, not another copy of its layout formula.
	var plain := reference.global_transform
	var blood := stamp.global_transform
	var x_error := blood.x.distance_to(plain.x * ratio)
	var y_error := blood.y.distance_to(plain.y)
	var image_basis := Transform2D(plain.x.normalized(), plain.y.normalized(), Vector2.ZERO)
	var undo := image_basis.affine_inverse()
	var plain_position := undo.basis_xform(plain.origin - plane)
	var blood_position := undo.basis_xform(blood.origin - plane)
	var position_error := blood_position.distance_to(Vector2(plain_position.x * ratio, plain_position.y))
	var error := maxf(maxf(x_error, y_error), position_error)
	maximum_transform_error = maxf(maximum_transform_error, error)
	check(error < 0.001, "blood extends only the original attack's X and keeps its Y shape, origin and upright basis")
	check(signf(blood.determinant()) == signf(plain.determinant()),
		"eight-way blood force mirrors exactly as the ordinary fist/palm")


func verify_matching_atlas() -> void:
	var center := Vector2(510, 330)
	const HEIGHT := 57.0
	const REACH := 58.2
	const EXTENSION := 34.5
	for sample in range(8):
		var direction := Vector2.RIGHT.rotated(sample * TAU / 8.0)
		for key in ["拳掌", "拳掌_掌"]:
			var meta := {"age": 0.0, "kind": "拳掌", "origin": center,
				"dir": direction, "reach": REACH, "attack_height": HEIGHT,
				"hit": false, "impact": center + direction * REACH,
				"visual_key": key, "combo_stage": 2 if key == "拳掌_掌" else 1,
				"visual_height": 0.92 if key == "拳掌_掌" else 0.78,
				"probe_payload": {"untouched": [1, 2, 3]}}
			var before := meta.duplicate(true)
			source.strike_fx.clear()
			renderer.clear_visuals()
			renderer.emit_blood(center, direction, REACH, EXTENSION, HEIGHT, meta, source)
			check(meta == before, "creating blood ink leaves the actual release metadata unchanged")
			check(renderer.marks.size() == 1 and visible_slots(renderer) == 1,
				"one released attack produces one pooled Sprite2D")
			var mark: Dictionary = renderer.marks[0]
			var stamp: Sprite2D = renderer.slots[int(mark.slot)]
			check(stamp.texture == source.strike_fx_textures[key] and mark.texture == stamp.texture,
				"blood uses the live ordinary attack's exact texture object")
			check(str(mark.source_type) == "basic_attack_atlas" and FileAccess.file_exists(str(mark.texture_path)),
				"source metadata identifies an existing registered atlas")
			check(str(mark.form) == ("palm" if key == "拳掌_掌" else "fist")
				and int(mark.combo_stage) == int(meta.combo_stage), "actual fist/palm identity and stage are retained")
			check(is_equal_approx(float(mark.visual_extent), REACH + EXTENSION),
				"forward visual extent is the existing reach plus extension")
			check(stamp.material.shader == source.skill_vfx.force_shader
				and is_equal_approx(float(stamp.material.get_shader_parameter("blood_mix")), 1.0),
				"the ordinary ink shader supplies only the dark-red pigment variant")
			source.strike_fx.append(meta)
			var previous := 0.0
			for age in [0.02, 0.09, 0.24, 0.379]:
				renderer.advance(float(age) - previous)
				previous = float(age)
				meta.age = float(age)
				source.skill_vfx.sync_visuals()
				var reference: Sprite2D = source.skill_vfx.stamps[0]
				check(stamp.region_rect == reference.region_rect
					and int(mark.animation_frame) == source.strike_fx_frame_index("拳掌", float(age)),
					"both layers use the same frame and clipped atlas region at the same age")
				compare_to_basic(stamp, reference, center - Vector2(0, HEIGHT), (REACH + EXTENSION) / REACH)
				var ordinary_mix = reference.material.get_shader_parameter("blood_mix")
				check(ordinary_mix == null or is_zero_approx(float(ordinary_mix)),
					"sharing the shader does not recolour the ordinary force")
				samples += 1
			renderer.advance(0.002)
			check(renderer.marks.is_empty() and visible_slots(renderer) == 0 and renderer.ink.active_count() == 0,
				"the original .38s blood lifetime still clears silhouette and particles")
			# Height changes only the release plane, never its X or silhouette.
			renderer.clear_visuals()
			renderer.emit_blood(center, direction, REACH, EXTENSION, 0.0, meta, source)
			renderer.advance(0.12)
			var low: Transform2D = renderer.slots[int(renderer.marks[0].slot)].global_transform
			var low_ink := particle_origins(renderer)
			renderer.clear_visuals()
			renderer.emit_blood(center, direction, REACH, EXTENSION, HEIGHT, meta, source)
			renderer.advance(0.12)
			var high: Transform2D = renderer.slots[int(renderer.marks[0].slot)].global_transform
			var high_ink := particle_origins(renderer)
			check(low.x.is_equal_approx(high.x) and low.y.is_equal_approx(high.y)
				and (high.origin - low.origin).is_equal_approx(Vector2(0, -HEIGHT)),
				"raising translates the same atlas silhouette without shifting the character center X")
			var ink_error := 0.0
			for index in range(mini(high_ink.size(), low_ink.size())):
				ink_error = maxf(ink_error, (high_ink[index] - low_ink[index] - Vector2(0, -HEIGHT)).length())
			check(high_ink.size() == low_ink.size() and not high_ink.is_empty() and ink_error < 0.001,
				"residual ink translates to the same hand-height plane")
	renderer.clear_visuals()
	source.strike_fx.clear()
	source.skill_vfx.clear_visuals()


func verify_registered_sources() -> void:
	renderer.clear_visuals()
	renderer.emit_blood(Vector2(400, 300), Vector2.RIGHT, 58.2, 30.0, 50.0)
	check(renderer.marks.size() == 1 and str(renderer.marks[0].form) == "fist",
		"legacy five-argument preview keeps the registered fist fallback")
	renderer.clear_visuals()
	var unknown := {"kind": "拳掌", "visual_key": "unsupported_left"}
	var before := unknown.duplicate(true)
	renderer.emit_blood(Vector2.ZERO, Vector2.RIGHT, 58.2, 30.0, 50.0, unknown)
	check(renderer.marks.is_empty() and renderer.ink.active_count() == 0 and unknown == before,
		"an unknown standalone key does not invent a replacement shape")
	source.strike_fx_textures["supported_custom_left"] = source.strike_fx_textures["拳掌"]
	renderer.emit_blood(Vector2.ZERO, Vector2.RIGHT, 58.2, 30.0, 50.0,
		{"kind": "custom_basic", "visual_key": "supported_custom_left"}, source)
	check(renderer.marks.size() == 1 and str(renderer.marks[0].form) == "source"
		and renderer.slots[int(renderer.marks[0].slot)].texture == source.strike_fx_textures["supported_custom_left"],
		"future supported textures are taken from the live source rather than guessed from skill names")
	source.strike_fx_textures.erase("supported_custom_left")
	renderer.clear_visuals()


func verify_pool() -> void:
	renderer.emit_blood(Vector2(400, 300), Vector2.RIGHT, 58.2, 30.0, 50.0)
	renderer.advance(0.40)
	var warm_count: int = renderer.slots.size()
	for repeat in range(40):
		renderer.emit_blood(Vector2(400, 300), Vector2.RIGHT.rotated(repeat * 0.1), 58.2, 30.0, 50.0)
		renderer.advance(0.40)
	check(warm_count > 0 and renderer.slots.size() == warm_count,
		"sequential attacks reuse Sprite2D slots and loaded atlases")
	check(renderer.marks.is_empty() and visible_slots(renderer) == 0, "sequential expiry leaves no visible silhouette")
	var capacity: int = Renderer.MAX_MARKS
	for repeat in range(capacity * 3):
		renderer.emit_blood(Vector2(400, 300), Vector2.RIGHT, 58.2, 30.0, 50.0)
	check(renderer.marks.size() <= capacity and renderer.slots.size() <= capacity,
		"concurrent emissions obey the finite visual pool")
	var bounded_nodes: int = renderer.slots.size()
	for repeat in range(capacity * 2):
		renderer.emit_blood(Vector2(400, 300), Vector2.RIGHT, 58.2, 30.0, 50.0)
	check(renderer.slots.size() == bounded_nodes, "saturated emission reuses bounded sprite slots")
	renderer.clear_visuals()
	check(renderer.ink.active_count() == 0 and visible_slots(renderer) == 0, "clear leaves no active ink or sprite")


func verify_live_metadata(arena: Node2D) -> void:
	arena.restart_battle()
	for npc: Node2D in arena.npcs:
		npc.ai_enabled = false
		npc.player = arena.player + Vector2(600 + npc.unit_index * 50, 0)
	arena.set_simulation_paused(false)
	arena.qi_blood = 2
	arena.held_basic_attack = true
	arena.use_skill("拳掌")
	for tick in range(45):
		arena.update_skill_timers(0.05)
	arena.held_basic_attack = false
	var forms: Array = []
	var stages: Array = []
	var heights: Array = []
	var live_texture_match := true
	for mark: Dictionary in arena.manual_vfx.marks:
		forms.append(str(mark.form))
		stages.append(int(mark.combo_stage))
		heights.append(float(mark.visual_height))
		live_texture_match = live_texture_match and mark.texture == arena.strike_fx_textures[str(mark.visual_key)]
	check(forms == ["fist", "palm", "fist", "palm"] and stages == [1, 2, 3, 4] and live_texture_match,
		"the production caller forwards each real stage and live source atlas")
	check(heights.size() == 4 and is_equal_approx(heights[0], 0.78)
		and is_equal_approx(heights[1], 0.92) and is_equal_approx(heights[2], 0.88)
		and is_equal_approx(heights[3], 1.10), "the caller keeps the original vertical shape variation")
	check(arena.wave_release_count == 0 and arena.qi_blood == 2,
		"matching silhouettes does not consume blood or invent a red-tide wave")
	var matched_clock := true
	for tick in range(6):
		arena._process(0.05)
		for mark: Dictionary in arena.manual_vfx.marks:
			var effect: Dictionary = mark.source_effect
			matched_clock = matched_clock and int(mark.animation_frame) == arena.strike_fx_frame_index(
				str(mark.kind), float(effect.age))
	check(matched_clock and not arena.manual_vfx.marks.is_empty(),
		"real frame-order ticking keeps blood animation exactly aligned with its original force effect")
	arena.manual_vfx.clear_visuals()


func verify_runtime_isolation() -> void:
	var arena: Node2D = load("res://tscn/scene/ActionManualBattleScene.tscn").instantiate()
	root.add_child(arena)
	freeze(arena)
	for npc: Node2D in arena.npcs:
		npc.ai_enabled = false
	var vfx: Node2D = arena.manual_vfx
	var before := state(arena)
	var center: Vector2 = arena.player
	var height: float = arena.attack_plane_height()
	vfx.emit_blood(center, Vector2.RIGHT, 58.2, 30.0, height,
		{"kind": "拳掌", "visual_key": "拳掌", "combo_stage": 1, "visual_height": 0.78}, arena)
	vfx.advance(0.12)
	check(state(arena) == before, "visual emission and advance cannot change HP, resources, cooldowns or actions")
	var mark: Dictionary = vfx.marks[0]
	var stamp: Sprite2D = vfx.slots[int(mark.slot)]
	var locked_transform := stamp.global_transform
	var locked_origin: Vector2 = mark.origin
	var locked_ink := particle_origins(vfx)
	arena.player += Vector2(70, -35)
	arena.facing = -1.0
	arena.update_skinned_player()
	check(stamp.global_transform == locked_transform and mark.origin == locked_origin,
		"moving and turning cannot drag the released basic-shaped blood force")
	check(particle_origins(vfx) == locked_ink, "moving and turning cannot drag emitted scatter")
	var marks_before: Array = vfx.marks.duplicate(true)
	var ink_before: Array = vfx.ink._pool.duplicate(true)
	var clock: float = vfx.ink.clock
	arena.set_simulation_paused(true)
	arena._process(0.05)
	check(vfx.marks == marks_before and vfx.ink._pool == ink_before and vfx.ink.clock == clock,
		"normal pause freezes sprite animation and particle clocks")
	for repeat in range(4):
		vfx.queue_redraw()
		await process_frame
	check(vfx.marks == marks_before and vfx.ink._pool == ink_before and vfx.ink.clock == clock,
		"actual repeated redraw does not re-emit ink or advance animation")
	vfx.clear_visuals()
	check(vfx.marks.is_empty() and vfx.ink.active_count() == 0 and visible_slots(vfx) == 0,
		"explicit clear removes the reused silhouette and residual particles")
	verify_live_metadata(arena)
	arena.queue_free()
	await process_frame


func verify() -> void:
	source = FixedSource.new()
	source.original_wanderer_enabled = true
	source.original_wanderer_art_folder = "yuxu_fullbody_v15"
	root.add_child(source)
	freeze(source)
	source.player = Vector2(510, 330)
	source.enemy_hp = 1000
	renderer = Renderer.new()
	root.add_child(renderer)
	verify_matching_atlas()
	verify_registered_sources()
	verify_pool()
	await verify_runtime_isolation()
	renderer.queue_free()
	source.queue_free()
	await process_frame
	print("BLOOD_QI_MATCHING_SMOKE_%s checks=%d failures=%d directions=8 frame_samples=%d max_transform_error=%s" % [
		"OK" if failures == 0 else "FAIL", checks, failures, samples, maximum_transform_error])
	quit(0 if failures == 0 else 1)
