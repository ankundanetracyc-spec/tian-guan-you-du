extends SceneTree
## Observe real paint vertices and the fitted sword in the production preview.
## The shared motion helper is a contract, never a substitute rendered sword.
const RULES = preload("sword_combo_rules.gd")
const VFX = preload("sword_combo_vfx.gd")
const SWEEP = preload("sword_sweep_motion.gd")
var checks := 0
var failures := 0
var flow_cases := 0
var sword_samples := 0
var maximum_axis_error := 0.0
var maximum_grip_error := 0.0
var minimum_contact_speed := INF
var maximum_path_tip_error := 0.0
var maximum_live_tip_gap := 0.0
var maximum_live_in_bounds_gap := 0.0
var maximum_clip_gap := 0.0
var in_bounds_samples := 0
var clipped_samples := 0
var real_path_vertices := 0
var outside_path_vertices := 0
var clip_cases: Array[String] = []
var spatial_diagnostics: Array[Dictionary] = []
var outside_diagnostics: Array[Dictionary] = []
var maximum_projection_error := 0.0
var maximum_outline_error := 0.0
var maximum_actor_scale_error := 0.0
var maximum_global_roundtrip_error := 0.0
var canonical_ground: Dictionary = {}
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
	return release


func world_points(entry: Dictionary) -> Array[Vector2]:
	var result: Array[Vector2] = []
	for polygon: Polygon2D in entry.polygons:
		for point: Vector2 in polygon.polygon:
			result.append(polygon.to_global(point))
	return result


func paint_endpoints(entry: Dictionary) -> Array[Vector2]:
	var polygon: Polygon2D = entry.polygons[1]
	var count: int = polygon.polygon.size() / 2
	return [polygon.polygon[0], polygon.polygon[count - 1]]


func paint_span(entry: Dictionary) -> float:
	var endpoints := paint_endpoints(entry)
	if str(entry.shape) == "capsule":
		return endpoints[0].distance_to(endpoints[1])
	var height := Vector2(0, float(entry.attack_height))
	var first := SWEEP.unproject_ground(endpoints[0] + height, float(entry.view_depth)) - Vector2(entry.origin)
	var second := SWEEP.unproject_ground(endpoints[1] + height, float(entry.view_depth)) - Vector2(entry.origin)
	return absf(first.angle_to(second))


func maximum_displacement(before: Array[Vector2], after: Array[Vector2]) -> float:
	if before.size() != after.size():
		return INF
	var maximum := 0.0
	for index in range(before.size()):
		maximum = maxf(maximum, before[index].distance_to(after[index]))
	return maximum


func verify_continued_paint() -> void:
	for axis in range(8):
		var direction := Vector2.from_angle(axis * TAU / 8.0)
		for stage in range(4):
			var release := release_for(stage, direction)
			var original := release.duplicate(true)
			renderer.emit(release, float(release.reveal))
			var entry: Dictionary = renderer.marks[0]
			var first := world_points(entry)
			var ground_points: Array[Vector2] = []
			for polygon: Polygon2D in entry.polygons:
				for point: Vector2 in polygon.polygon:
					var logical := SWEEP.unproject_ground(point + Vector2(0, float(entry.attack_height)),
						float(entry.view_depth)) - Vector2(entry.origin)
					ground_points.append(logical.rotated(-direction.angle()))
			if axis == 0:
				canonical_ground[stage] = ground_points
			var projection_error := maximum_displacement(canonical_ground[stage], ground_points)
			maximum_projection_error = maxf(maximum_projection_error, projection_error)
			check(projection_error < 0.0002,
				"stage %d direction %d: actual vertices use fixed world-depth projection, not a rotated flattened picture" % [stage + 1, axis * 45])
			var first_tail: Vector2 = paint_endpoints(entry)[0]
			var first_span := paint_span(entry)
			var target_age: float = lerpf(float(release.reveal), float(release.life), 0.45)
			renderer.advance(target_age - float(entry.age))
			var middle := world_points(entry)
			var middle_tail: Vector2 = paint_endpoints(entry)[0]
			var middle_span := paint_span(entry)
			renderer.advance(float(release.life) * 0.92 - float(entry.age))
			var late := world_points(entry)
			var late_tail: Vector2 = paint_endpoints(entry)[0]
			var late_span := paint_span(entry)
			var label := "stage %d direction %d" % [stage + 1, axis * 45]
			check(maximum_displacement(first, middle) > 0.01
				and maximum_displacement(middle, late) > 0.01,
				label + ": actual geometry continues moving after the front is fully revealed")
			check(first_tail.distance_to(middle_tail) > 0.01
				and middle_tail.distance_to(late_tail) > 0.01
				and first_span > middle_span and middle_span > late_span,
				label + ": the old paint tail advances and the surviving stroke shortens")
			check(release == original and entry.origin == original.origin
				and entry.direction == original.direction and entry.attack_height == original.attack_height,
				label + ": flowing pigment does not move the release anchor or edit contact metadata")
			flow_cases += 1
			renderer.clear()
			await process_frame


func advance_scene(scene: Node2D, seconds: float) -> void:
	var remaining := seconds
	while remaining > 0.0000001:
		var delta := minf(1.0 / 240.0, remaining)
		scene.advance(delta)
		remaining -= delta


func sword_axis(scene: Node2D) -> Vector2:
	var sword: RefCounted = scene.rig.wanderer_skin.approved_sword
	var grip: Vector2 = scene.world.to_local(sword.sword_sprite.to_global(sword.source_grip))
	var tip: Vector2 = scene.world.to_local(sword.sword_sprite.to_global(sword.source_tip))
	return (tip - grip).normalized()


func grip_error(scene: Node2D) -> float:
	var skin: RefCounted = scene.rig.wanderer_skin
	var sword: RefCounted = skin.approved_sword
	var actual: Vector2 = sword.sword_sprite.to_global(sword.source_grip)
	var expected: Vector2 = skin.approved_hands.get_attachment_transform("near").origin
	return actual.distance_to(expected)


func sword_tip(scene: Node2D) -> Vector2:
	var sword: RefCounted = scene.rig.wanderer_skin.approved_sword
	return scene.world.to_local(sword.sword_sprite.to_global(sword.source_tip))


func rendered_front(scene: Node2D, entry: Dictionary) -> Vector2:
	# Layer zero is an intentionally delayed pale trail, not the live edge.
	var polygon: Polygon2D = entry.polygons[1]
	var count: int = polygon.polygon.size() / 2
	return scene.world.to_local(polygon.to_global(polygon.polygon[count - 1]))


func in_footprint(entry: Dictionary, point: Vector2) -> bool:
	var ground := SWEEP.unproject_ground(point + Vector2(0, float(entry.attack_height)),
		float(entry.view_depth))
	return RULES.contains(entry, ground)


func verify_real_vertices(scene: Node2D, entry: Dictionary) -> bool:
	var inside := true
	for polygon: Polygon2D in entry.polygons:
		for point: Vector2 in polygon.polygon:
			# Both real nodes are children of the preview's world. Preserve
			# their local transform chain instead of adding then subtracting
			# screen translation in float32 before a 1/.24 inverse projection.
			var local: Vector2 = scene.vfx.transform * (polygon.transform * point)
			var through_global: Vector2 = scene.world.to_local(polygon.to_global(point))
			maximum_global_roundtrip_error = maxf(maximum_global_roundtrip_error, local.distance_to(through_global))
			var valid := in_footprint(entry, local)
			inside = inside and valid
			real_path_vertices += 1
			if not valid:
				outside_path_vertices += 1
				if outside_diagnostics.size() < 8:
					var ground := SWEEP.unproject_ground(local + Vector2(0, float(entry.attack_height)),
						float(entry.view_depth))
					var relative := ground - Vector2(entry.origin)
					var direction: Vector2 = entry.direction
					outside_diagnostics.append({"stage":entry.index + 1,"age":entry.age,
						"direction":direction.angle(),"ground":str(ground),
						"range_excess":relative.length()-float(entry.reach),
						"angle_excess":absf(direction.angle_to(relative))-deg_to_rad(float(entry.angle)*0.5),
						"direct_local_inside":in_footprint(entry, scene.vfx.transform*(polygon.transform*point))})
	return inside


func verify_real_tail(scene: Node2D, entry: Dictionary) -> bool:
	var before := world_points(entry)
	var main: Polygon2D = entry.polygons[1]
	var old_tail := main.uv[0].x
	var first_span: float = main.uv[main.uv.size() / 2 - 1].x - old_tail
	var changes := true
	var inside := true
	for age in [lerpf(float(entry.reveal), float(entry.life), 0.45), float(entry.life) * 0.92]:
		scene.vfx.advance(float(age) - float(entry.age))
		var after := world_points(entry)
		var next_tail := main.uv[0].x
		var next_span: float = main.uv[main.uv.size() / 2 - 1].x - next_tail
		changes = changes and maximum_displacement(before, after) > 0.01
		changes = changes and next_tail > old_tail and next_span < first_span
		inside = verify_real_vertices(scene, entry) and inside
		before = after
		old_tail = next_tail
		first_span = next_span
	check(inside, "real recorded blade ribbons remain inside the unchanged contact shape while drying")
	return changes


func verify_display_projection(scene: Node2D) -> void:
	scene.player = Vector2(23, -31)
	scene.update_actor(0.000001)
	var actual_foot: Vector2 = scene.to_local(scene.rig.to_global(Vector2.ZERO))
	var displayed_foot: Vector2 = scene.ground_to_screen(scene.player)
	check(actual_foot.distance_to(displayed_foot) < 0.002,
		"the real actor foot and the production marker transform share one ground projection")
	for marker: Vector2 in scene.markers:
		var screen: Vector2 = scene.ground_to_screen(marker)
		var recovered := SWEEP.unproject_ground((screen - Vector2(scene.screen_origin)) / float(scene.zoom))
		check(recovered.distance_to(marker) < 0.0002,
			"production marker drawing preserves its original logical ground position")
	var body_transform: Transform2D = scene.rig.global_transform
	var scale_error := absf(body_transform.x.length() - body_transform.y.length())
	maximum_actor_scale_error = maxf(maximum_actor_scale_error, scale_error)
	check(scale_error < 0.0002,
		"the actual upright character keeps equal horizontal and vertical scale")
	var floor_screen: Vector2 = scene.ground_to_screen(scene.player)
	var raised_screen: Vector2 = scene.ground_to_screen(scene.player, 42.0)
	check(absf(floor_screen.distance_to(raised_screen) - 42.0 * float(scene.zoom)) < 0.0002,
		"vertical attack height remains uncompressed above projected ground")
	for axis in range(8):
		var direction := Vector2.from_angle(axis * TAU / 8.0)
		for stage in range(4):
			var release := release_for(stage, direction)
			var outline := RULES.outline(release)
			var displayed: PackedVector2Array = scene.display_outline(release, float(release.attack_height))
			var outline_error := 0.0
			for index in range(outline.size()):
				var ground := SWEEP.unproject_ground(
					(displayed[index] - Vector2(scene.screen_origin)) / float(scene.zoom)
					+ Vector2(0, float(release.attack_height)))
				outline_error = maxf(outline_error, ground.distance_to(outline[index]))
			maximum_outline_error = maxf(maximum_outline_error, outline_error)
			check(outline_error < 0.0002,
				"the production G outline uses the same fixed camera while preserving the original shape")
	scene.restart()


func verify_real_sword() -> void:
	var scene: Node2D = load("res://sword_combo_preview.tscn").instantiate()
	root.add_child(scene)
	scene.set_process(false)
	scene.auto_loop = false
	scene.aim_from_mouse = false
	verify_display_projection(scene)
	for axis in range(8):
		var direction := Vector2.from_angle(axis * TAU / 8.0)
		for stage in range(4):
			scene.aim_override = direction
			scene.start_single(stage)
			var rule := RULES.preview(stage, 8.0, 8.0, 6.0)
			var contact: float = float(rule.duration) * float(rule.hit)
			const VELOCITY_STEP := 0.0001
			advance_scene(scene, contact - VELOCITY_STEP)
			var before_axis := sword_axis(scene)
			advance_scene(scene, VELOCITY_STEP)
			check(scene.combo.releases.size() == 1 and scene.vfx.marks.size() == 1,
				"the original real scene emits exactly one stage at contact")
			if scene.vfx.marks.is_empty():
				continue
			var entry: Dictionary = scene.vfx.marks[0]
			var at_axis := sword_axis(scene)
			var contact_speed := absf(before_axis.angle_to(at_axis)) / VELOCITY_STEP
			if stage != 2:
				advance_scene(scene, VELOCITY_STEP)
				contact_speed = minf(contact_speed, absf(at_axis.angle_to(sword_axis(scene))) / VELOCITY_STEP)
				minimum_contact_speed = minf(minimum_contact_speed, contact_speed)
				check(contact_speed > 0.001,
					"stage %d direction %d: the actual blade keeps angular speed through contact" % [stage + 1, axis * 45])
			var released: Dictionary = scene.combo.releases[0].duplicate(true)
			var label := "stage %d direction %d" % [stage + 1, axis * 45]
			var aligned := true
			var attached := true
			var spatially_aligned := true
			var path_valid: bool = entry.has("blade_path") and entry.blade_path.size() == 65
			var actual_geometry_inside := true
			check(path_valid, label + ": the real release records the actual 65-point blade path")
			if not path_valid:
				continue
			# Exact recorded knots avoid testing a duplicate interpolation formula.
			# Knot 33 is after the tiny contact velocity probe in every stage.
			for path_index in [33, 40, 48, 56, 64]:
				var sample: Dictionary = entry.blade_path[path_index]
				var target_age: float = maxf(VELOCITY_STEP, SWEEP.birth(float(sample.u), float(rule.reveal)))
				advance_scene(scene, maxf(0.0, target_age - float(entry.age)))
				var actual := sword_axis(scene)
				var front := rendered_front(scene, entry)
				var ground_axis := direction if stage == 2 else direction.rotated(SWEEP.angle(stage, float(rule.angle), float(entry.age), float(rule.reveal)))
				var expected := SWEEP.project_ground(ground_axis, float(entry.view_depth)).normalized()
				var error := absf(actual.angle_to(expected))
				var attachment_error := grip_error(scene)
				var actor_transform: Transform2D = scene.rig.global_transform
				var scale_error := absf(actor_transform.x.length() - actor_transform.y.length())
				maximum_actor_scale_error = maxf(maximum_actor_scale_error, scale_error)
				attached = attached and scale_error < 0.0002
				var recorded_tip: Vector2 = sample.tip
				var distance := front.distance_to(recorded_tip)
				if in_footprint(entry, recorded_tip):
					in_bounds_samples += 1
					maximum_path_tip_error = maxf(maximum_path_tip_error, distance)
					spatially_aligned = spatially_aligned and distance < 0.002
					if distance >= 0.002 and spatial_diagnostics.size() < 8:
						spatial_diagnostics.append({"case":label,"knot":path_index,"age":entry.age,
							"recorded":str(recorded_tip),"front":str(front),"live":str(sword_tip(scene))})
				else:
					clipped_samples += 1
					maximum_clip_gap = maxf(maximum_clip_gap, distance)
					if distance > 0.002 and not clip_cases.has(label):
						clip_cases.append(label)
				var live_tip := sword_tip(scene)
				maximum_live_tip_gap = maxf(maximum_live_tip_gap, front.distance_to(live_tip))
				if in_footprint(entry, live_tip):
					maximum_live_in_bounds_gap = maxf(maximum_live_in_bounds_gap, front.distance_to(live_tip))
				actual_geometry_inside = verify_real_vertices(scene, entry) and actual_geometry_inside
				maximum_axis_error = maxf(maximum_axis_error, error)
				maximum_grip_error = maxf(maximum_grip_error, attachment_error)
				aligned = aligned and error < 0.002
				attached = attached and attachment_error < 0.002
				sword_samples += 1
			check(aligned, label + ": the actual sword axis follows the shared world-angle clock")
			check(spatially_aligned, label + ": the main rendered head meets the recorded real release-base sword tip when inside the original footprint")
			check(actual_geometry_inside, label + ": actual path-based paint vertices keep the original contact footprint")
			check(attached, label + ": actual source grip stays on the hand attachment through mirrored and diagonal cuts")
			var points_before_pause := world_points(entry)
			var age_before_pause := float(entry.age)
			var sword_before_pause: Transform2D = scene.rig.wanderer_skin.approved_sword.sword_sprite.global_transform
			scene.paused = true
			advance_scene(scene, 0.10)
			check(world_points(entry) == points_before_pause and float(entry.age) == age_before_pause
				and scene.rig.wanderer_skin.approved_sword.sword_sprite.global_transform == sword_before_pause,
				label + ": normal pause freezes actual paint vertices and the sword together")
			scene.player += Vector2(23, -8)
			scene.aim_override = -direction
			scene.update_actor(0.000001)
			check(world_points(entry) == points_before_pause and scene.combo.releases[0] == released,
				label + ": moving the caster cannot drag the released flowing stroke or edit its contact")
			check(verify_real_tail(scene, entry), label + ": the real path-based main stroke keeps moving and retiring its tail after reveal")
			check(scene.combo.releases[0] == released,
				label + ": drying real recorded paint cannot add contact or mutate its blade path")
			scene.restart()
			await process_frame
	check(scene.vfx.marks.is_empty() and scene.vfx.get_child_count() == 0,
		"normal restart removes all flow layers")
	scene.queue_free()
	await process_frame


func verify() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1280, 720)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	renderer = VFX.new()
	root.add_child(renderer)
	await verify_continued_paint()
	await verify_real_sword()
	renderer.queue_free()
	await process_frame
	print("SWORD_FLOW_CHECKS_%s checks=%d failures=%d directions=8 flow_cases=%d sword_samples=%d max_axis_error=%.7f max_grip_error=%.7f min_contact_speed=%.7f" % [
		"OK" if failures == 0 else "FAIL", checks, failures, flow_cases, sword_samples,
		maximum_axis_error, maximum_grip_error, minimum_contact_speed])
	print("SWORD_FLOW_SPATIAL world_units=true recorded_tip_max_error=%.7f in_bounds_samples=%d clipped_samples=%d max_clip_gap=%.5f live_tip_max_gap=%.5f live_in_bounds_max_gap=%.5f real_vertices=%d outside_vertices=%d clipped_cases=%s" % [
		maximum_path_tip_error, in_bounds_samples, clipped_samples,
		maximum_clip_gap, maximum_live_tip_gap, maximum_live_in_bounds_gap, real_path_vertices,
		outside_path_vertices, JSON.stringify(clip_cases)])
	print("SWORD_FLOW_PROJECTION max_inverse_rotated_geometry_error=%.7f max_outline_inverse_error=%.7f max_actor_scale_difference=%.7f global_translation_roundtrip_error=%.7f view_depth=%.5f" % [
		maximum_projection_error, maximum_outline_error, maximum_actor_scale_error,
		maximum_global_roundtrip_error, SWEEP.VIEW_DEPTH])
	if not spatial_diagnostics.is_empty():
		print("SWORD_FLOW_SPATIAL_DIAGNOSTICS ", JSON.stringify(spatial_diagnostics))
	if not outside_diagnostics.is_empty():
		print("SWORD_FLOW_OUTSIDE_DIAGNOSTICS ", JSON.stringify(outside_diagnostics))
	quit(0 if failures == 0 else 1)
