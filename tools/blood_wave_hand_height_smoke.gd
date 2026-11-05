extends SceneTree
## Hand-height plane regression: geometry, retained ink, and the live NPC trial.

const Wave = preload("res://tools/action_battle_test/blood_wave_vfx.gd")
const OUT := "res://output/blood_wave_hand_height_20261104"
const TEST_HEIGHT := 63.5
var checks := 0
var failures := 0
var vertices := 0
var compared_particles := 0
var maximum_mesh_error := 0.0
var maximum_particle_error := 0.0
var arena: Node2D


func _initialize() -> void:
	call_deferred("verify")


func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		push_error(label)


func _window() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1280, 720)
	root.content_scale_size = Vector2i(1280, 720)
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true


func compare_geometry(low: Node2D, high: Node2D) -> Dictionary:
	var result := {"matching": true, "count": 0, "error": 0.0, "x_error": 0.0}
	for index in range(low._bands.size()):
		var first: MeshInstance2D = low._bands[index]
		var second: MeshInstance2D = high._bands[index]
		if first.visible != second.visible:
			result.matching = false
			continue
		if not first.visible:
			continue
		var mesh_a := first.mesh as ArrayMesh
		var mesh_b := second.mesh as ArrayMesh
		if mesh_a.get_surface_count() != mesh_b.get_surface_count():
			result.matching = false
			continue
		for surface in range(mesh_a.get_surface_count()):
			var points_a: PackedVector3Array = mesh_a.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
			var points_b: PackedVector3Array = mesh_b.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
			if points_a.size() != points_b.size():
				result.matching = false
				continue
			for point_index in range(points_a.size()):
				var point_a := points_a[point_index]
				var point_b := points_b[point_index]
				var world_a: Vector2 = first.global_transform * Vector2(point_a.x, point_a.y)
				var world_b: Vector2 = second.global_transform * Vector2(point_b.x, point_b.y)
				var delta := world_b - world_a
				result.error = maxf(float(result.error), (delta - Vector2(0, -TEST_HEIGHT)).length())
				result.x_error = maxf(float(result.x_error), absf(delta.x))
				result.count += 1
	return result


func compare_particles(low: Node2D, high: Node2D) -> Dictionary:
	var result := {"matching": true, "count": 0, "error": 0.0}
	if low._ink == null or high._ink == null:
		result.matching = false
		return result
	for index in range(low._ink._pool.size()):
		var first: Dictionary = low._ink._pool[index]
		var second: Dictionary = high._ink._pool[index]
		if bool(first.alive) != bool(second.alive):
			result.matching = false
			continue
		if not bool(first.alive):
			continue
		if first.kind != second.kind or first.age != second.age or first.velocity != second.velocity:
			result.matching = false
		var delta: Vector2 = Vector2(second.origin) - Vector2(first.origin)
		result.error = maxf(float(result.error), (delta - Vector2(0, -TEST_HEIGHT)).length())
		result.count += 1
	return result


func verify_translation() -> void:
	var stage := Node2D.new()
	root.add_child(stage)
	var low: Node2D = Wave.new()
	var high: Node2D = Wave.new()
	for sample in [low, high]:
		sample.auto_advance = false
		sample.y_projection = 0.46
		sample.crest_lift = 14.0
		stage.add_child(sample)
	var center := Vector2(400, 350)
	for facing in range(8):
		var direction := Vector2.RIGHT.rotated(float(facing) * PI / 4.0)
		low.play(center, direction, 0.0)
		high.play(center, direction, TEST_HEIGHT)
		check(high.release_origin == center and high.release_height == TEST_HEIGHT,
			"height does not displace the actor-centered ground pivot")
		for elapsed in [0.18, 0.46, 0.56]:
			low.advance(elapsed)
			high.advance(elapsed)
			var geometry := compare_geometry(low, high)
			var ink := compare_particles(low, high)
			vertices += int(geometry.count)
			compared_particles += int(ink.count)
			maximum_mesh_error = maxf(maximum_mesh_error, float(geometry.error))
			maximum_particle_error = maxf(maximum_particle_error, float(ink.error))
			check(bool(geometry.matching) and int(geometry.count) > 0 and float(geometry.error) < 0.001 and float(geometry.x_error) < 0.001,
				"every visible mesh is the same eight-way sector shifted only up by hand height")
			check(bool(ink.matching) and int(ink.count) > 0 and float(ink.error) < 0.001,
				"all retained ink profiles share the exact hand-height shift")
	low.clear_visuals()
	high.clear_visuals()
	stage.free()


func _particle_origins(wave: Node2D) -> Array:
	var points: Array = []
	if wave._ink != null:
		for particle in wave._ink._pool:
			if particle.alive:
				points.append(particle.origin)
	return points


func _clear() -> bool:
	for wave in arena.blood_waves:
		if wave.playing or wave.visible:
			return false
		if wave._ink != null and wave._ink.active_count() != 0:
			return false
	return true


func _save(name: String) -> void:
	arena.battle_hud._update_layout()
	arena.battle_hud.queue_redraw()
	arena.queue_redraw()
	for frame in range(3):
		# Offscreen capture does not acquire desktop focus; freeze visual clocks
		# independently while keeping the normal running overlay visible.
		arena.simulation_paused = false
		await process_frame
	await RenderingServer.frame_post_draw
	var shot := root.get_texture().get_image()
	check(shot != null and not shot.is_empty(), "real hand-height frame is available")
	if shot != null and not shot.is_empty():
		check(shot.save_png(OUT.path_join(name + ".png")) == OK, "hand-height screenshot saved")


func _pose(facing: float) -> void:
	arena.facing = facing
	arena.visual_motion_delta = 0.0
	arena.visual_motion_velocity = Vector2.ZERO
	arena.update_skinned_player()
	if arena.original_wanderer != null:
		arena.original_wanderer.z_index = int(arena.player.y)
	for npc in arena.npcs:
		npc.update_skinned_player()
	arena._update_trial_camera(0.0, true)


func verify() -> void:
	_window()
	check(DirAccess.make_dir_recursive_absolute(OUT) == OK, "capture directory writable")
	verify_translation()
	arena = load("res://tscn/scene/BloodWaveNpcTrialScene.tscn").instantiate()
	root.add_child(arena)
	current_scene = arena
	arena.set_process(false)
	arena.set_process_input(false)
	arena.battle_hud.set_process_input(false)
	_window()
	arena.player = Vector2(1650, 1020)
	var positions := [Vector2(180, 5), Vector2(-145, -95), Vector2(-145, 105)]
	for index in range(arena.npcs.size()):
		var npc: Node2D = arena.npcs[index]
		npc.ai_enabled = false
		npc.player = arena.player + positions[index]
	arena.set_simulation_paused(false)
	for facing in [1.0, -1.0]:
		arena.clear_blood_waves()
		_pose(facing)
		var height: float = arena.attack_plane_height()
		check(height > 0.0 and is_equal_approx(Wave.hand_plane_height(arena), height),
			"actual pose supplies live hand height")
		check(arena.preview_blood_wave(Vector2(facing, 0)), "live hand-height wave can start")
		var wave: Node2D = arena.blood_waves[0]
		check(wave.release_origin.is_equal_approx(arena.to_global(arena.player)),
			"live wave remains centered on the actor rather than the hand X")
		check(is_equal_approx(wave.release_height, height) and wave.global_position.is_equal_approx(wave.release_origin - Vector2(0, height)),
			"live wave plane is raised exactly to the hand height")
		check(wave.z_index == int(arena.player.y) + 6, "depth follows the ground center rather than raised screen Y")
		wave.advance(0.62)
		await _save("wave_right" if facing > 0 else "wave_left")
		if facing > 0:
			var shot := root.get_texture().get_image()
			check(shot.save_png(OUT.path_join("wave_with_npcs.png")) == OK, "primary NPC view saved")
	var old: Node2D = arena.blood_waves[0]
	var old_origin: Vector2 = old.release_origin
	var old_height: float = old.release_height
	var old_ink := _particle_origins(old)
	arena.player += Vector2(70, 25)
	arena.motion_time += 0.41
	_pose(1.0)
	check(old.release_origin == old_origin and old.release_height == old_height,
		"moving and turning preserve the old released center and height")
	check(not old_ink.is_empty() and _particle_origins(old) == old_ink,
		"moving and turning preserve every previously emitted world ink origin")
	var current_height: float = arena.attack_plane_height()
	check(arena.preview_blood_wave(Vector2(0.3, -1)), "new pose can release another hand-height wave")
	var fresh: Node2D = arena.blood_waves[1]
	check(fresh.release_origin.is_equal_approx(arena.to_global(arena.player)) and is_equal_approx(fresh.release_height, current_height),
		"new wave snapshots the new center and current hand height")
	var age: float = old.age
	var ink_clock: float = old._ink.clock
	arena.set_simulation_paused(true)
	arena._process(0.35)
	check(is_equal_approx(old.age, age) and is_equal_approx(old._ink.clock, ink_clock),
		"pause still freezes elevated wave and particle clocks")
	arena.clear_blood_waves()
	check(_clear(), "clear removes elevated bands and retained ink")
	arena.queue_free()
	await process_frame
	print("BLOOD_WAVE_HAND_HEIGHT_%s checks=%d failures=%d directions=8 samples=3 vertices=%d particles=%d max_mesh_error=%s max_particle_error=%s" % [
		"OK" if failures == 0 else "FAIL", checks, failures, vertices, compared_particles, maximum_mesh_error, maximum_particle_error])
	quit(0 if failures == 0 else 1)
