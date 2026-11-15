extends Node2D
## Regression for negative-determinant facing folds. This deliberately inspects
## orientations and alpha-visible triangles, not just finite values/bone lengths.
## Pure-turn cases compare a turning actor against an otherwise identical
## unturned control. Dynamic folds already present in both are reported rather
## than confused with a new facing-induced fold.

const NativeRuntime = preload("guigu_native_first_runtime.gd")
var checks := 0
var failures: Array[String] = []
var cases: Array = []
var samples := 0
var duplicate_pose_vertex_error := 0.0
var report_path := ""
var alive: Array[Node2D] = []


func _ready() -> void:
	call_deferred("_run")


func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)


func _new_actor() -> Node2D:
	var actor: Node2D = NativeRuntime.new()
	add_child(actor)
	alive.append(actor)
	_check(actor.build(), "actual native runtime builds")
	return actor


func _step(actor: Node2D, clip: String, pose_time: float, clock: float, facing: float) -> void:
	actor.wanderer_skin.set_motion_context(1.0 / 60.0, Vector2.ZERO, clock, facing)
	actor.set_character_pose(clip, pose_time, facing)


func _cloth(skin: RefCounted) -> Dictionary:
	for record in skin.mesh_records:
		if record.sprite_id == 85649:
			return record
	return {}


func _visible_triangles(record: Dictionary) -> Array:
	var source_image: Image = Image.load_from_file(record.source_path)
	var visible: Array = []
	for triangle in record.original_triangles:
		var vertices: PackedVector2Array = record.original_uv
		var hit := false
		for a in range(1, 5):
			for b in range(1, 6 - a):
				var wa := float(a) / 6.0
				var wb := float(b) / 6.0
				var pixel := vertices[triangle[0]] * wa + vertices[triangle[1]] * wb + vertices[triangle[2]] * (1.0 - wa - wb)
				var alpha := source_image.get_pixel(clampi(int(pixel.x), 0, source_image.get_width() - 1), clampi(int(pixel.y), 0, source_image.get_height() - 1)).a
				hit = hit or alpha >= 0.25
		if hit:
			visible.append(triangle)
	return visible


func _area(points: PackedVector2Array, tri: PackedInt32Array) -> float:
	return (points[tri[1]] - points[tri[0]]).cross(points[tri[2]] - points[tri[0]]) * 0.5


func _flips(points: PackedVector2Array, reference: PackedVector2Array, triangles: Array) -> int:
	var count := 0
	for triangle in triangles:
		var a := _area(points, triangle)
		var b := _area(reference, triangle)
		if absf(a) > 0.0001 and absf(b) > 0.0001 and a * b < 0.0:
			count += 1
	return count


func _vertex_error(a: PackedVector2Array, b: PackedVector2Array) -> float:
	var error := 0.0
	for i in range(a.size()):
		error = maxf(error, a[i].distance_to(b[i]))
	return error


func _bone_error(world: Dictionary, reference: Dictionary, root_path: String) -> float:
	var error := 0.0
	for suffix in ["/shenti2_1", "/shenti2_1/shenti2_2", "/bone_3", "/bone_3/bone_4"]:
		var path: String = root_path + suffix
		var a: Vector3 = (world[path] as Transform3D).basis.x.normalized()
		var b: Vector3 = (reference[path] as Transform3D).basis.x.normalized()
		error = maxf(error, rad_to_deg(acos(clampf(a.dot(b), -1.0, 1.0))))
	return error


func _static_case(facing: float) -> void:
	var actor := _new_actor()
	var skin: RefCounted = actor.wanderer_skin
	var record := _cloth(skin)
	var triangles := _visible_triangles(record)
	var root_path := str(skin.native_physics.components[0].config.rootPath)
	var max_error := 0.0
	var max_angle := 0.0
	var max_flips := 0
	var finite := true
	var determinant_min := INF
	for frame in range(120):
		_step(actor, "Idle", 0.0, float(frame + 1) / 60.0, facing)
		var baseline: PackedVector2Array = skin.deform_mesh(record, skin.animated_world, false)
		var actual: PackedVector2Array = skin.deform_mesh(record, skin.world_transforms, false)
		max_error = maxf(max_error, _vertex_error(actual, baseline))
		max_angle = maxf(max_angle, _bone_error(skin.world_transforms, skin.animated_world, root_path))
		max_flips = maxi(max_flips, _flips(actual, baseline, triangles))
		determinant_min = minf(determinant_min, (skin._actor_transform() as Transform3D).basis.determinant())
		for point in actual:
			finite = finite and point.is_finite()
		samples += 1
	var label := "static right" if facing > 0.0 else "static left"
	_check(determinant_min > 0.0, label + ": 3D actor frame has positive determinant")
	_check(max_error < 0.01, label + ": no force-free cloth deformation")
	_check(max_angle < 0.1, label + ": no spontaneous bone half-turn")
	_check(max_flips == 0, label + ": zero alpha-visible triangle flips against physics-off pose")
	_check(finite, label + ": positions finite (auxiliary, not fold proof)")
	cases.append({"case": label, "frames": 120, "max_cloth_vertex_error_px": max_error,
		"max_bone_angle_error_degrees": max_angle, "max_visible_triangle_flips": max_flips,
		"visible_triangles_sampled": triangles.size(), "minimum_actor_determinant": determinant_min})


func _turn_case(mode: String) -> void:
	var control := _new_actor()
	var turning := _new_actor()
	var a: RefCounted = control.wanderer_skin
	var b: RefCounted = turning.wanderer_skin
	var ra := _cloth(a)
	var rb := _cloth(b)
	var triangles := _visible_triangles(rb)
	var root_path := str(a.native_physics.components[0].config.rootPath)
	var max_error := 0.0
	var max_angle := 0.0
	var max_extra_flips := 0
	var max_base_control_flips := 0
	var max_base_turning_flips := 0
	var changes := 0
	var previous_facing := 1.0
	for frame in range(600):
		var clip := "Idle"
		var time := 0.0
		if mode == "run":
			clip = "Run_New1"
			time = float(frame) / 60.0
		elif mode == "clip_transitions":
			var phase := frame % 180
			clip = "Idle" if phase < 30 or phase >= 150 else ("Run_New1" if phase < 90 else ("AttackIdle" if phase < 120 else "Step1"))
			time = float(phase % 30) / 60.0
		var facing := -1.0 if (frame / 17) % 2 == 1 else 1.0
		if facing != previous_facing:
			changes += 1
		previous_facing = facing
		_step(control, clip, time, float(frame + 1) / 60.0, 1.0)
		_step(turning, clip, time, float(frame + 1) / 60.0, facing)
		var original: PackedVector2Array = a.deform_mesh(ra, a.animated_world, false)
		var pa: PackedVector2Array = a.deform_mesh(ra, a.world_transforms, false)
		var pb: PackedVector2Array = b.deform_mesh(rb, b.world_transforms, false)
		max_error = maxf(max_error, _vertex_error(pa, pb))
		max_angle = maxf(max_angle, _bone_error(b.world_transforms, a.world_transforms, root_path))
		max_extra_flips = maxi(max_extra_flips, _flips(pb, pa, triangles))
		max_base_control_flips = maxi(max_base_control_flips, _flips(pa, original, triangles))
		max_base_turning_flips = maxi(max_base_turning_flips, _flips(pb, original, triangles))
		samples += 1
	_check(changes >= 30, mode + ": repeated actual facing changes exercised")
	_check(max_error < 0.02, mode + ": pure turn preserves cloth shape against unturned physics control")
	_check(max_angle < 0.1, mode + ": pure turn introduces no half-turn relative to control")
	_check(max_extra_flips == 0, mode + ": no additional alpha-visible flips caused by facing")
	if mode == "static":
		_check(max_base_turning_flips == 0, "static repeated turns: zero visible flips against physics-off pose")
	cases.append({"case": mode, "frames": 600, "facing_changes": changes,
		"max_cloth_difference_from_unturned_control_px": max_error,
		"max_bone_angle_difference_from_control_degrees": max_angle,
		"max_turn_induced_triangle_flips": max_extra_flips,
		"max_control_flips_against_physics_off": max_base_control_flips,
		"max_turning_flips_against_physics_off": max_base_turning_flips})
	# Repeated rendering in one clock tick must not drop the solved pose.
	if mode == "run":
		var before: PackedVector2Array = b.deform_mesh(rb, b.world_transforms, false)
		turning.set_character_pose("Run_New1", 599.0 / 60.0, previous_facing)
		var after: PackedVector2Array = b.deform_mesh(rb, b.world_transforms, false)
		duplicate_pose_vertex_error = _vertex_error(before, after)
		_check(duplicate_pose_vertex_error < 0.001, "duplicate pose in same motion clock preserves last physics solution")


func _far_placement_and_stop() -> void:
	var control := _new_actor()
	var placed := _new_actor()
	var a: RefCounted = control.wanderer_skin
	var b: RefCounted = placed.wanderer_skin
	var ra := _cloth(a)
	var rb := _cloth(b)
	var triangles := _visible_triangles(rb)
	var far_origin := Vector2(5344.0, 3789.0)
	var max_error := 0.0
	var max_extra_flips := 0
	var max_control_flips := 0
	var max_placed_flips := 0
	var max_late_idle_flips := 0
	var finite := true
	for frame in range(420):
		var clip := "Idle"
		var time := float(frame) / 60.0
		var offset := Vector2.ZERO
		if frame >= 60 and frame < 180:
			clip = "Run_New1"
			time = float(frame - 60) / 60.0
			offset.x = float(frame - 60) * 1.9
		elif frame >= 180:
			offset.x = 120.0 * 1.9
			time = float(frame - 180) / 60.0
			if frame >= 300 and frame < 330:
				clip = "Step1"
				time = float(frame - 300) / 60.0
		var facing := -1.0 if (frame / 37) % 2 == 0 else 1.0
		control.position = offset
		placed.position = far_origin + offset
		_step(control, clip, time, float(frame + 1) / 60.0, facing)
		_step(placed, clip, time, float(frame + 1) / 60.0, facing)
		var original: PackedVector2Array = a.deform_mesh(ra, a.animated_world, false)
		var pa: PackedVector2Array = a.deform_mesh(ra, a.world_transforms, false)
		var pb: PackedVector2Array = b.deform_mesh(rb, b.world_transforms, false)
		max_error = maxf(max_error, _vertex_error(pa, pb))
		max_extra_flips = maxi(max_extra_flips, _flips(pb, pa, triangles))
		max_control_flips = maxi(max_control_flips, _flips(pa, original, triangles))
		max_placed_flips = maxi(max_placed_flips, _flips(pb, original, triangles))
		if (frame >= 240 and frame < 300) or frame >= 390:
			max_late_idle_flips = maxi(max_late_idle_flips, _flips(pb, original, triangles))
		for point in pb:
			finite = finite and point.is_finite()
		samples += 1
	_check(max_error < 0.05, "far first placement: no origin-to-spawn secondary impulse")
	_check(max_extra_flips == 0, "far spawn/move/stop/clip/turn: no extra visible flips against colocated-path control")
	_check(max_late_idle_flips == 0, "stop recovery: no persistent alpha-visible folds after60 idle frames")
	_check(finite, "far spawn/move/stop/clip/turn: finite (auxiliary)")
	cases.append({"case": "far_placement_move_stop_clip_turn", "frames": 420,
		"first_placement_pixels": [far_origin.x, far_origin.y],
		"max_difference_from_same_path_control_px": max_error,
		"max_extra_triangle_flips": max_extra_flips,
		"max_control_flips_against_physics_off": max_control_flips,
		"max_placed_flips_against_physics_off": max_placed_flips,
		"max_late_idle_flips_against_physics_off": max_late_idle_flips})


func _run() -> void:
	_static_case(1.0)
	_static_case(-1.0)
	_turn_case("static")
	_turn_case("run")
	_turn_case("clip_transitions")
	if OS.get_cmdline_user_args().has("--extended"):
		_far_placement_and_stop()
	var root := ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().get_base_dir().get_base_dir()
	report_path = root.path_join("output/guigu_native_first_20261105/turn_integrity_probe.json")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--report="):
			report_path = argument.trim_prefix("--report=")
	var report := {"passed": failures.is_empty(), "checks": checks, "samples": samples,
		"failures": failures, "cases": cases, "duplicate_same_clock_cloth_error_px": duplicate_pose_vertex_error,
		"scope": "Actual native runtime/skin, both static facings and 3x600frame repeated pure-turn cases. Inspect 3D bone orientation and alpha-visible cloth triangle signs; length/finite checks alone are insufficient.",
		"limits": "Animated control may itself contain source/secondary cloth folds; these are reported separately and never labelled no-intersection merely because turning matches it."}
	DirAccess.make_dir_recursive_absolute(report_path.get_base_dir())
	FileAccess.open(report_path, FileAccess.WRITE).store_string(JSON.stringify(report, "\t") + "\n")
	print(JSON.stringify(report))
	for actor in alive:
		actor.queue_free()
	get_tree().quit(0 if failures.is_empty() else 1)
