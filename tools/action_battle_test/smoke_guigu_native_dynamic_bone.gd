extends SceneTree

const Solver = preload("guigu_native_dynamic_bone.gd")
const CONFIG_PATH := "resources/appearance/guigu_native_first_20261105/dynamic_bone_config.json"
const RIG_PATH := "resources/appearance/guigu_native_first_20261105/rig.json"
const FIXTURE_PATH := "tools/action_battle_test/output/guigu_native_dynamic_bone/animated_world_fixtures.json"
var checks := 0
var failures: Array[String] = []
var samples := 0
var max_length_error := 0.0
var max_root_error := 0.0
var max_world_z_excursion := 0.0
var max_unaffected_error := 0.0
var max_dynamic_displacement := 0.0


func _initialize() -> void:
	call_deferred("_run")


func _check(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures.append(label)


func _run() -> void:
	var config: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(_project_path(CONFIG_PATH)))
	var rig: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(_project_path(RIG_PATH)))
	var solver = Solver.new()
	_check(solver.setup(config, rig.nodes), "source setup accepted")
	var diagnostics: Dictionary = solver.get_diagnostics()
	_check(diagnostics.components.size() == 2, "cloth and hair independently restored")
	_check(diagnostics.components[0].particles == 7 and diagnostics.components[0].virtualEndParticles == 2, "cloth branching plus two end particles")
	_check(diagnostics.components[1].particles == 4 and diagnostics.components[1].virtualEndParticles == 1, "hair actual root plus virtual end")
	_check(str(config.components[1].rootPath).ends_with("/bone_3"), "hair root differs from component host bone_2")
	_check(absf(solver.evaluate_curve(config.components[0].stiffnessDistrib, 0.25) - 0.4375) < 1e-7, "Unity Hermite stiffness distribution retained")
	_check(absf(float(config.components[0].damping) - 0.147) < 1e-7 and absf(float(config.components[1].inert) - 0.272) < 1e-7, "freshly decoded unequal source parameters")
	for row in config.components:
		_check(row.bytesConsumed == row.rawPayloadBytes and row.rawPayloadBytes == 452, "complete original payload consumed")
	var original: Dictionary = solver.rest_world.duplicate()
	var idle_cloth_end: Vector3 = solver.components[0].particles[2].position
	for frame in range(180):
		var posed: Dictionary = solver.step(1.0 / 60.0, original)
		_assert_sample(solver, original, posed, Transform3D.IDENTITY, "static " + str(frame))
	_check((solver.components[0].particles[2].position as Vector3).distance_to(idle_cloth_end) < 2e-6, "zero-force cloth rest has no invented flutter")
	_check(max_dynamic_displacement > 1e-5, "real hair force produces secondary movement")
	# Actual original Clip TRS matrices, before all project IK/2D adjustments.
	var fixture: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(_project_path(FIXTURE_PATH)))
	for clip in fixture.clips:
		for facing in [1.0, -1.0]:
			solver = Solver.new()
			_check(solver.setup(config, rig.nodes), "fresh clip setup " + str(clip.name))
			for cycle in range(3):
				for index in range(clip.frames.size()):
					var animated := _decode_world(fixture.paths, clip.frames[index])
					var n: int = cycle * clip.frames.size() + index
					# Rotate the actor out of XY and translate all three dimensions.
					var actor_basis := Basis(Vector3.UP, 0.35 * sin(float(n) * 0.04))
					actor_basis = actor_basis.scaled_local(Vector3(facing, 1.0, 1.0))
					var actor := Transform3D(actor_basis, Vector3(float(n) * 0.001, sin(float(n) * 0.03) * 0.01, float(n) * 0.0007))
					var posed: Dictionary = solver.step(float(clip.duration) / float(clip.frames.size()), animated, actor)
					_assert_sample(solver, animated, posed, actor, str(clip.name) + " " + str(n))
	# Source scheduling has an exact cap of three simulation iterations.
	solver = Solver.new()
	solver.setup(config, rig.nodes)
	solver.reset(original)
	solver.step(10.0, original)
	_check(solver.last_step_substeps == 6, "three backlog substeps per component")
	solver.step(0.0, original)
	_check(solver.last_step_substeps == 0, "subframe uses SkipUpdateParticles")
	var rejected: Dictionary = config.duplicate(true)
	rejected.components[0].colliders = [{"fileID": 0, "pathID": 1}]
	var guard = Solver.new()
	_check(not guard.setup(rejected, rig.nodes), "unavailable collider geometry explicitly refused")
	_analytic_source_step(config)
	_check(max_length_error < 3e-6, "all original animated constraint lengths retained in 3D")
	_check(max_root_error < 3e-6, "both physics roots follow exact animated mounts")
	_check(max_unaffected_error < 3e-6, "body limbs remain unchanged by secondary solver")
	_check(max_world_z_excursion > 0.001, "solver actually carries actor full3D motion")
	var report := {"passed": failures.is_empty(), "checks": checks, "samples": samples,
		"failures": failures, "max3DLengthError": max_length_error,
		"maxRootMountError": max_root_error, "maxUnaffectedBoneError": max_unaffected_error,
		"maxSceneZExcursion": max_world_z_excursion, "maxDynamicDisplacement": max_dynamic_displacement,
		"source": "all six male101 clips at48 samples, three cycles, both facings, 3D actor movement, plus static source test",
		"diagnostics": diagnostics,
		"limits": "Parameter/geometry/scheduling regression of supplied SDK port. Does not establish native instruction identity or original gameplay runtime state."}
	var out := _project_path("tools/action_battle_test/output/guigu_native_dynamic_bone_smoke.json")
	FileAccess.open(out, FileAccess.WRITE).store_string(JSON.stringify(report, "\t") + "\n")
	print(JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)


func _project_path(relative: String) -> String:
	var base := ProjectSettings.globalize_path("res://")
	if not FileAccess.file_exists(base.path_join("resources/appearance/guigu_native_first_20261105/rig.json")):
		base = base.path_join("../..").simplify_path()
	return base.path_join(relative)


func _analytic_source_step(source: Dictionary) -> void:
	var c: Dictionary = source.components[0].duplicate(true)
	c.rootPath = "/root"
	c.componentPath = "/root"
	c.endLength = 0.0
	c.damping = 0.25
	c.elasticity = 0.3
	c.stiffness = 0.0
	c.inert = 0.0
	var solver = Solver.new()
	var test_nodes := [{"path": "/root", "parentPath": null, "rest_local": Transform3D.IDENTITY},
		{"path": "/root/child", "parentPath": "/root", "rest_local": Transform3D(Basis.IDENTITY, Vector3.RIGHT)}]
	_check(solver.setup({"components": [c]}, test_nodes), "analytic source-step setup")
	var world: Dictionary = solver.rest_world.duplicate()
	solver.reset(world)
	solver.components[0].particles[1].prev_position = Vector3(1.0, -0.2, 0.0)
	var posed: Dictionary = solver.step(1.0 / 120.0, world)
	_check((posed["/root/child"] as Transform3D).origin.distance_to(Vector3.RIGHT) < 1e-7, "source subframe skip does not integrate velocity")
	posed = solver.step(1.0 / 120.0, world)
	# Direct source equations: velocity .2*(1-.25)=.15; elasticity leaves
	# .15*(1-.3)=.105; final length constraint normalizes (1,.105,0).
	var expected := Vector3(1.0, 0.105, 0.0).normalized()
	_check((posed["/root/child"] as Transform3D).origin.distance_to(expected) < 2e-7, "exact Verlet then elasticity then length order")


func _decode_world(paths: Array, frame: Array) -> Dictionary:
	var world := {}
	for i in range(paths.size()):
		var v: Array = frame[i]
		world[paths[i]] = Transform3D(Basis(Vector3(v[0], v[3], v[6]), Vector3(v[1], v[4], v[7]), Vector3(v[2], v[5], v[8])), Vector3(v[9], v[10], v[11]))
	return world


func _assert_sample(solver, animated: Dictionary, posed: Dictionary, actor: Transform3D, label: String) -> void:
	samples += 1
	var finite := true
	var influenced := {}
	for state in solver.components:
		var particles: Array = state.particles
		var root_path := str(state.config.rootPath)
		max_root_error = maxf(max_root_error, (posed[root_path] as Transform3D).origin.distance_to((animated[root_path] as Transform3D).origin))
		for p in particles:
			if not str(p.path).is_empty():
				influenced[p.path] = true
				max_dynamic_displacement = maxf(max_dynamic_displacement, (posed[p.path] as Transform3D).origin.distance_to((animated[p.path] as Transform3D).origin))
			var position: Vector3 = p.position
			finite = finite and position.is_finite()
			max_world_z_excursion = maxf(max_world_z_excursion, absf(position.z))
			if int(p.parent_index) < 0:
				continue
			var parent: Dictionary = particles[int(p.parent_index)]
			var rest_length: float
			if not str(p.path).is_empty():
				rest_length = (actor * (animated[p.path] as Transform3D)).origin.distance_to((actor * (animated[parent.path] as Transform3D)).origin)
			else:
				rest_length = ((actor * (animated[parent.path] as Transform3D)).basis * (p.end_offset as Vector3)).length()
			max_length_error = maxf(max_length_error, position.distance_to(parent.position) - rest_length if position.distance_to(parent.position) > rest_length else rest_length - position.distance_to(parent.position))
	for path in animated:
		finite = finite and (posed[path] as Transform3D).is_finite()
		if not influenced.has(path):
			max_unaffected_error = maxf(max_unaffected_error, (posed[path] as Transform3D).origin.distance_to((animated[path] as Transform3D).origin))
	_check(finite, "finite 3D particles/world: " + label)
