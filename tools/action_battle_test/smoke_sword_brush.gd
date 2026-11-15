extends SceneTree
## Read real emitted polygons back through the production contact predicate.
## No attack rule, pose or damage value is replaced by this visual regression.
const RULES = preload("sword_combo_rules.gd")
const VFX = preload("sword_combo_vfx.gd")
const SWEEP = preload("sword_sweep_motion.gd")
var checks := 0
var failures := 0
var vertices_checked := 0
var geometry_cases := 0
var bad_vertices := 0
var bad_case_samples: Array[String] = []
var renderer: Node2D


func _initialize() -> void:
	call_deferred("verify")


func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		push_error(label)


func release_for(stage: int, direction: Vector2) -> Dictionary:
	var release := RULES.preview(stage, 8.0, 8.0, 6.0)
	release.origin = Vector2(37, -18)
	release.direction = direction
	release.attack_height = 58.0
	release.targets = {}
	release.combo_id = 17
	release.is_basic_attack = true
	release.is_combo_last = stage == 3
	return release


func world_points(entry: Dictionary) -> Array[Vector2]:
	var result: Array[Vector2] = []
	for polygon: Polygon2D in entry.polygons:
		for point: Vector2 in polygon.polygon:
			result.append(polygon.global_transform * point)
	return result


func coverage(entry: Dictionary) -> float:
	var minimum := INF
	var maximum := -INF
	var d: Vector2 = entry.direction
	var origin: Vector2 = entry.origin
	for polygon: Polygon2D in entry.polygons:
		for point: Vector2 in polygon.polygon:
			var relative := SWEEP.unproject_ground(point + Vector2(0, float(entry.attack_height)),
				float(entry.view_depth)) - origin
			var value := relative.dot(d) if str(entry.shape) == "capsule" else d.angle_to(relative)
			minimum = minf(minimum, value)
			maximum = maxf(maximum, value)
	return maxf(0.0, maximum - minimum)


func verify_geometry() -> void:
	for axis in range(8):
		var direction := Vector2.from_angle(axis * TAU / 8.0)
		for stage in range(4):
			var release := release_for(stage, direction)
			var original := release.duplicate(true)
			var all_inside := true
			var early_coverage := 0.0
			var final_coverage := 0.0
			var ages := [0.0, float(release.reveal) * 0.10,
				float(release.reveal) * 0.5, float(release.reveal), float(release.life) * 0.80]
			for age in ages:
				renderer.clear()
				renderer.emit(release, float(age))
				var entry: Dictionary = renderer.marks[0]
				var bad_this_case := 0
				for polygon: Polygon2D in entry.polygons:
					for point: Vector2 in polygon.polygon:
						# Vertical attack height is separate from the fixed camera's
						# ground-depth projection; recover unchanged logical XY.
						var ground := SWEEP.unproject_ground(point + Vector2(0, float(entry.attack_height)),
							float(entry.view_depth))
						if not RULES.contains(release, ground):
							bad_this_case += 1
						vertices_checked += 1
				geometry_cases += 1
				bad_vertices += bad_this_case
				all_inside = all_inside and bad_this_case == 0
				if bad_this_case > 0 and bad_case_samples.size() < 8:
					bad_case_samples.append("stage=%d angle=%d age=%.5f bad_vertices=%d" % [
						stage + 1, axis * 45, age, bad_this_case])
				if is_zero_approx(float(age)):
					early_coverage = coverage(entry)
				if is_equal_approx(float(age), float(release.reveal)):
					final_coverage = coverage(entry)
			check(all_inside, "stage %d angle %d: every brush polygon stays in the original sector/capsule throughout reveal" % [stage + 1, axis * 45])
			check(final_coverage > early_coverage and final_coverage > 0.0,
				"stage %d angle %d: the blade trace visibly unfolds before fading" % [stage + 1, axis * 45])
			check(release == original, "the painter leaves released attack metadata and target bookkeeping unchanged")
			renderer.clear()
			await process_frame
	if not bad_case_samples.is_empty():
		print("SWORD_BRUSH_GEOMETRY_SAMPLES ", JSON.stringify(bad_case_samples))


func verify_lifecycle() -> void:
	for stage in range(4):
		var release := release_for(stage, Vector2.RIGHT)
		renderer.emit(release)
		var polygons: Array = renderer.marks[0].polygons.duplicate()
		renderer.advance(float(release.life) - 0.001)
		check(renderer.marks.size() == 1, "each stage keeps its existing visual lifetime")
		renderer.advance(0.002)
		check(renderer.marks.is_empty(), "each stage naturally expires at its original lifetime")
		await process_frame
		check(polygons.all(func(node): return not is_instance_valid(node)), "expired paint layers are freed without leftover nodes")
	renderer.emit(release_for(0, Vector2.RIGHT))
	var per_mark: int = renderer.marks[0].polygons.size()
	for repeat in range(40):
		renderer.emit(release_for(repeat % 4, Vector2.RIGHT))
	check(renderer.marks.size() <= 16, "rapid visual emission retains the existing finite mark limit")
	await process_frame
	check(renderer.get_child_count() <= 16 * per_mark, "discarded old marks do not retain hidden paint nodes")
	renderer.clear()
	await process_frame
	check(renderer.marks.is_empty() and renderer.get_child_count() == 0, "explicit clear removes every paint layer")


func verify_random_isolation() -> void:
	const FIXED_SEED := 147197
	seed(FIXED_SEED)
	var expected := randf()
	seed(FIXED_SEED)
	var fresh: Node2D = VFX.new()
	root.add_child(fresh)
	fresh.emit(release_for(3, Vector2.RIGHT))
	var actual := randf()
	check(is_equal_approx(expected, actual), "building brush texture and emitting a trace do not consume the combat random sequence")
	check(fresh.brush.get_image().get_data() == renderer.brush.get_image().get_data(),
		"independent fixed-seed brushes reproduce the same pigment texture")
	fresh.clear()
	fresh.queue_free()
	await process_frame


func verify_real_scene() -> void:
	var scene: Node2D = load("res://sword_combo_preview.tscn").instantiate()
	root.add_child(scene)
	scene.set_process(false)
	scene.auto_loop = false
	scene.aim_from_mouse = false
	scene.aim_override = Vector2.RIGHT
	scene.start_single(2)
	var rule := RULES.preview(2, 8, 8, 6)
	var at_time: float = float(rule.duration) * float(rule.hit) + float(rule.reveal) * 0.6
	for step in range(ceili(at_time * 120.0)):
		scene.advance(minf(1.0 / 120.0, maxf(0.0, at_time - float(step) / 120.0)))
	check(scene.combo.releases.size() == 1 and scene.vfx.marks.size() == 1,
		"the original single-stage control still releases only one real stage")
	var entry: Dictionary = scene.vfx.marks[0]
	var release: Dictionary = scene.combo.releases[0]
	check(entry.origin == release.origin and entry.direction == release.direction
		and is_equal_approx(float(entry.attack_height), float(release.attack_height)),
		"the real scene fixes the released position, aim and measured hand height")
	var saved_points := world_points(entry)
	var saved_age := float(entry.age)
	var saved_release := release.duplicate(true)
	scene.paused = true
	var contacts_before: int = scene.contact_count
	var cooldown_before: float = scene.cooldown_remaining
	for step in range(6):
		scene.advance(0.05)
	check(float(entry.age) == saved_age and world_points(entry) == saved_points
		and scene.contact_count == contacts_before and scene.cooldown_remaining == cooldown_before,
		"normal preview pause freezes paint, contacts and action clocks")
	scene.player += Vector2(30, 12)
	scene.aim_override = Vector2.LEFT
	scene.update_actor(0.000001)
	check(world_points(entry) == saved_points and scene.combo.releases[0] == saved_release,
		"moving or turning the character does not drag a released blade trace")
	scene.paused = false
	scene.vfx.advance(float(entry.life) + 0.01)
	check(scene.contact_count == contacts_before and scene.combo.releases[0] == saved_release,
		"paint fading cannot add a hit or alter a release's targets/damage")
	scene.restart()
	await process_frame
	check(scene.vfx.marks.is_empty() and scene.vfx.get_child_count() == 0,
		"normal restart clears every new brush layer")
	scene.queue_free()
	await process_frame


func verify() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1280, 720)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	renderer = VFX.new()
	root.add_child(renderer)
	await verify_geometry()
	await verify_lifecycle()
	await verify_random_isolation()
	await verify_real_scene()
	renderer.queue_free()
	await process_frame
	print("SWORD_BRUSH_CHECKS_%s checks=%d failures=%d directions=8 cases=%d vertices=%d outside_vertices=%d" % [
		"OK" if failures == 0 else "FAIL", checks, failures, geometry_cases, vertices_checked, bad_vertices])
	quit(0 if failures == 0 else 1)
