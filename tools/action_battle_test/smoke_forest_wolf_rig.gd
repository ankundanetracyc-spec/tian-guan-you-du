extends SceneTree

## Checks the real native skeleton and skin bindings, then exercises the
## presentation through the same public inputs as the live battle.
var checks := 0
var failures: Array[String] = []
var maximum_weight_error := 0.0
var maximum_idle_paw_drift := 0.0
var mixed_vertices := 0
var maximum_animation_step := 0.0
var maximum_joint_rotation_step := 0.0
var maximum_joint_step_context := {}
var source_alpha := {}
var death_geometry := {}
var world_gait := {}
var relocation := {}
var rig: Node2D


func _initialize() -> void:
	call_deferred("_run")


func _local(relative: String) -> String:
	return get_script().resource_path.get_base_dir().path_join(relative)


func _art_dir() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--art-dir="):
			var path: String = argument.trim_prefix("--art-dir=")
			return path if path.begins_with("res://") or path.is_absolute_path() else _local(path)
	var pattern := RegEx.new()
	pattern.compile("art/forest_wolf_ingame_v[0-9]+")
	var found := pattern.search(FileAccess.get_file_as_string(_local("action_battle_feel_test.gd")))
	return _local(found.get_string()) if found != null else _local("art/forest_wolf_ingame_v3")


func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)
		push_error(message)


func _pose(actor: Node2D) -> Dictionary:
	var result := {}
	for node in actor.find_children("*", "Bone2D", true, false):
		result[str(actor.get_path_to(node))] = node.transform
	return result


func _pose_error(before: Dictionary, after: Dictionary) -> float:
	if before.size() != after.size():
		return INF
	var error := 0.0
	for key in before:
		if not after.has(key):
			return INF
		var a: Transform2D = before[key]
		var b: Transform2D = after[key]
		error = maxf(error, a.origin.distance_to(b.origin))
		error = maxf(error, a.x.distance_to(b.x))
		error = maxf(error, a.y.distance_to(b.y))
	return error


func _advance(seconds: float, velocity: Vector2, state: String, facing := 1.0, alive := true) -> void:
	var frames := maxi(1, ceili(seconds * 60.0))
	for frame in range(frames):
		var dt := seconds / frames
		var remaining := maxf(0.0, seconds - float(frame + 1) * dt)
		rig.global_position += velocity * dt
		rig.position += velocity * dt
		rig.set_motion_context(dt, velocity, state, remaining, facing, alive)


func _validate_skin() -> void:
	var skeletons := rig.find_children("*", "Skeleton2D", true, false)
	var bones := rig.find_children("*", "Bone2D", true, false)
	# The painted attachments are the actual weighted meshes. The separately
	# driven, untextured mouth cavity is validated through its own contract.
	var meshes: Array[Polygon2D] = rig.meshes
	_check(skeletons.size() == 1, "wolf owns one native Skeleton2D")
	_check(bones.size() >= 20, "wolf has a complete head, spine, tail and four-leg bone hierarchy")
	var expected_parts := 0
	for key in rig.parts:
		var part: Dictionary = rig.parts[key]
		if part.has("rect"):
			expected_parts += 1
	_check(expected_parts >= 7 and meshes.size() == expected_parts, "wolf draws every painted attachment declared by the current manifest")
	for value in meshes:
		var mesh: Polygon2D = value
		var vertex_count := mesh.polygon.size()
		_check(mesh.texture != null and vertex_count >= 4, "%s has an actual texture and polygon" % mesh.name)
		_check(mesh.uv.size() == vertex_count, "%s UVs cover every vertex" % mesh.name)
		var skeleton: Skeleton2D = mesh.get_node_or_null(mesh.skeleton) as Skeleton2D
		_check(skeleton != null, "%s resolves its live skeleton" % mesh.name)
		_check(mesh.get_bone_count() > 0, "%s is bound to bones" % mesh.name)
		var totals := PackedFloat32Array()
		var contributors := PackedInt32Array()
		totals.resize(vertex_count)
		contributors.resize(vertex_count)
		for binding in range(mesh.get_bone_count()):
			var bone: Bone2D = skeleton.get_node_or_null(mesh.get_bone_path(binding)) as Bone2D if skeleton != null else null
			_check(bone != null, "%s binding %d resolves a Bone2D" % [mesh.name, binding])
			var weights: PackedFloat32Array = mesh.get_bone_weights(binding)
			_check(weights.size() == vertex_count, "%s binding %d weight count matches its mesh" % [mesh.name, binding])
			if weights.size() != vertex_count:
				continue
			var valid := true
			for vertex in range(vertex_count):
				var weight: float = weights[vertex]
				valid = valid and is_finite(weight) and weight >= 0.0 and weight <= 1.00001
				totals[vertex] += weight
				if weight > 0.0001:
					contributors[vertex] += 1
			_check(valid, "%s binding %d has finite, nonnegative weights" % [mesh.name, binding])
		var finite_geometry := true
		for vertex in range(vertex_count):
			maximum_weight_error = maxf(maximum_weight_error, absf(totals[vertex] - 1.0))
			finite_geometry = finite_geometry and mesh.polygon[vertex].is_finite() and mesh.uv[vertex].is_finite()
			if contributors[vertex] >= 2:
				mixed_vertices += 1
		_check(finite_geometry, "%s has finite geometry and UVs" % mesh.name)
		var valid_indices := true
		for polygon in mesh.polygons:
			for index in polygon:
				valid_indices = valid_indices and index >= 0 and index < vertex_count
		_check(valid_indices, "%s triangulation stays inside its vertex array" % mesh.name)
	_check(maximum_weight_error < 0.0001, "every skinned vertex has normalized weights")
	_check(mixed_vertices >= 20, "joints use blended weights across continuous textured meshes")


func _validate_mouth() -> void:
	var mouth: Polygon2D = rig.find_child("MouthInterior", true, false) as Polygon2D
	_check(mouth != null, "the mouth cavity exists separately from the painted skin meshes")
	if mouth == null:
		return
	_check(mouth.texture == null, "the mouth cavity is a solid oral surface")
	rig.reset_pose()
	_check(not mouth.visible, "closed idle lips hide the mouth cavity")
	for frame in range(12):
		rig.set_motion_context(1.0 / 60.0, Vector2(300.0, 0.0), "lunge", 0.1, 1.0, true)
	_check(mouth.visible, "opening the jaw exposes the oral cavity")
	_check(mouth.polygon.size() == 4, "open mouth has four corner vertices")
	var finite_vertices := true
	var twice_area := 0.0
	for vertex in range(mouth.polygon.size()):
		var point: Vector2 = mouth.polygon[vertex]
		finite_vertices = finite_vertices and point.is_finite()
		twice_area += point.cross(mouth.polygon[(vertex + 1) % mouth.polygon.size()])
	_check(finite_vertices and absf(twice_area) > 0.1, "the open oral cavity is finite and covers an actual area")
	var opened := mouth.polygon.duplicate()
	for frame in range(8):
		rig.set_motion_context(1.0 / 60.0, Vector2.ZERO, "windup", 0.4, 1.0, true)
	var follows_jaw := false
	for vertex in range(mini(opened.size(), mouth.polygon.size())):
		follows_jaw = follows_jaw or opened[vertex].distance_to(mouth.polygon[vertex]) > 0.001
	_check(follows_jaw, "the oral surface follows the changing head and jaw pose")
	_advance(2.0, Vector2.ZERO, "idle")
	_check(not mouth.visible, "settled closed lips hide the oral cavity again")


func _validate_death_geometry() -> void:
	rig.reset_pose()
	var maximum_knee_above_hip := -INF
	var maximum_paw_above_hip := -INF
	var lowest_tail_point := -INF
	for frame in range(120):
		rig.set_motion_context(1.0 / 60.0, Vector2.ZERO, "chase", 0.0, 1.0, false)
		for limb in ["near_front", "far_front", "near_hind", "far_hind"]:
			var hip: Bone2D = rig.bones[limb + "_upper"]
			var knee: Bone2D = rig.bones[limb + "_lower"]
			var paw: Bone2D = rig.bones[limb + "_paw"]
			var hip_point: Vector2 = rig.mirrored_root.to_local(hip.global_position)
			var knee_point: Vector2 = rig.mirrored_root.to_local(knee.global_position)
			var paw_point: Vector2 = rig.mirrored_root.to_local(paw.global_position)
			maximum_knee_above_hip = maxf(maximum_knee_above_hip, hip_point.y - knee_point.y)
			maximum_paw_above_hip = maxf(maximum_paw_above_hip, hip_point.y - paw_point.y)
		for index in range(3):
			var tail: Bone2D = rig.bones["tail_%d" % index]
			var point: Vector2 = rig.mirrored_root.to_local(tail.global_position)
			lowest_tail_point = maxf(lowest_tail_point, point.y)
		var tip_offset: Vector2 = Vector2(rig.joints.tail[3]) - Vector2(rig.joints.tail[2])
		var last: Bone2D = rig.bones.tail_2
		var tip: Vector2 = rig.mirrored_root.to_local(last.to_global(tip_offset))
		lowest_tail_point = maxf(lowest_tail_point, tip.y)
	_check(maximum_knee_above_hip <= 2.0, "front and hind knees never protrude above their shoulder or hip during death")
	_check(maximum_paw_above_hip <= 2.0, "folding front and hind paws stay below the torso during death")
	_check(lowest_tail_point <= 1.0, "tail bones and the terminal tail point remain above the floor throughout death")
	death_geometry = {"maximum_knee_above_hip": maximum_knee_above_hip,
		"maximum_paw_above_hip": maximum_paw_above_hip, "lowest_tail_point_y": lowest_tail_point}


func _validate_alpha() -> void:
	var bitmap := Image.new()
	_check(bitmap.load(ProjectSettings.globalize_path(str(rig.source_path))) == OK, "wolf source image loads for transparency validation")
	var transparent := 0
	var opaque := 0
	var samples := 0
	for y in range(0, bitmap.get_height(), 7):
		for x in range(0, bitmap.get_width(), 7):
			var alpha := bitmap.get_pixel(x, y).a
			transparent += int(alpha < 0.01)
			opaque += int(alpha > 0.99)
			samples += 1
	source_alpha = {"samples": samples, "transparent_fraction": float(transparent) / maxi(samples, 1), "opaque_fraction": float(opaque) / maxi(samples, 1)}
	_check(source_alpha.transparent_fraction > 0.20 and source_alpha.opaque_fraction > 0.01, "wolf atlas has real transparent background and solid painted parts")
	_check(bitmap.get_pixel(0, 0).a < 0.01 and bitmap.get_pixel(bitmap.get_width() - 1, bitmap.get_height() - 1).a < 0.01, "atlas corners contain no painted backdrop")


func _validate_continuity() -> void:
	rig.reset_pose()
	var previous := _pose(rig)
	# One uninterrupted real attack sequence, sampled at 60 Hz. This catches
	# whole-pose switches and discontinuous IK branches at the state boundaries.
	for phase in [{"state": "chase", "duration": 1.0, "velocity": Vector2(95, 0)},
		{"state": "windup", "duration": 0.8, "velocity": Vector2.ZERO},
		{"state": "lunge", "duration": 0.2, "velocity": Vector2(300, 0)},
		{"state": "recover", "duration": 1.5, "velocity": Vector2.ZERO},
		{"state": "idle", "duration": 1.0, "velocity": Vector2.ZERO}]:
		var frames := roundi(float(phase.duration) * 60.0)
		for frame in range(frames):
			var remaining := maxf(0.0, float(phase.duration) - float(frame + 1) / 60.0)
			rig.position += Vector2(phase.velocity) / 60.0
			rig.set_motion_context(1.0 / 60.0, phase.velocity, str(phase.state), remaining, 1.0, true)
			var current := _pose(rig)
			for path in previous:
				var before: Transform2D = previous[path]
				var after: Transform2D = current[path]
				maximum_animation_step = maxf(maximum_animation_step, before.origin.distance_to(after.origin))
				var angle_step := absf(wrapf(after.get_rotation() - before.get_rotation(), -PI, PI))
				if angle_step > maximum_joint_rotation_step:
					maximum_joint_rotation_step = angle_step
					maximum_joint_step_context = {"state": phase.state, "frame_in_state": frame, "bone": path}
			previous = current
	_check(maximum_animation_step < 8.0, "continuous attack sequence has no large one-frame bone position snap")
	_check(maximum_joint_rotation_step < 0.6, "continuous attack sequence keeps all joint rotations continuous")


func _relocation_geometry(actor: Node2D) -> Dictionary:
	var max_local_offset := 0.0
	var max_link_ratio := 0.0
	var finite_points := true
	for limb in ["near_front", "far_front", "near_hind", "far_hind"]:
		var chain: Array = actor.joints[limb]
		var actual: Array[Vector2] = []
		for bone_name in actor._limb_bone_names(limb):
			var bone: Bone2D = actor.bones[bone_name]
			var point: Vector2 = actor.mirrored_root.to_local(bone.global_position)
			finite_points = finite_points and point.is_finite()
			actual.append(point)
		var pad_index: int = actor._foot_index(limb)
		max_local_offset = maxf(max_local_offset, actual[pad_index].distance_to(chain[pad_index]))
		for segment in range(actual.size() - 1):
			var rest_length: float = Vector2(chain[segment]).distance_to(chain[segment + 1])
			max_link_ratio = maxf(max_link_ratio, actual[segment].distance_to(actual[segment + 1]) / rest_length)
	return {"finite": finite_points, "maximum_paw_local_offset": max_local_offset, "maximum_link_length_ratio": max_link_ratio}


func _validate_relocation() -> void:
	var probe: Node2D = load(_local("forest_wolf_rig.gd")).new()
	root.add_child(probe)
	_check(probe.build(_art_dir()), "relocation fixture builds at the origin")
	probe.scale = Vector2.ONE * 0.55
	probe.global_position = Vector2(1500, 950)
	probe.set_motion_context(0.0, Vector2.ZERO, "chase", 0.0, 1.0, true)
	var initial := _relocation_geometry(probe)
	_check(initial.finite and initial.maximum_paw_local_offset <= 40.0, "first zero-delta context registers feet at the final actor position and scale")
	_check(initial.maximum_link_length_ratio <= 1.05, "initial positioning does not stretch the leg links")
	for frame in range(60):
		probe.global_position += Vector2(95.0 / 60.0, 0)
		probe.set_motion_context(1.0 / 60.0, Vector2(95, 0), "chase", 0.0, 1.0, true)
	probe.global_position += Vector2(680, -230)
	probe.set_motion_context(0.0, Vector2.ZERO, "chase", 0.0, 1.0, true)
	var teleported := _relocation_geometry(probe)
	_check(teleported.finite and teleported.maximum_paw_local_offset <= 40.0, "zero-delta teleport rebases feet close to the relocated body")
	_check(teleported.maximum_link_length_ratio <= 1.05, "teleport does not pull any leg across the arena")
	var frozen := _pose(probe)
	var frozen_facing: Transform2D = probe.mirrored_root.transform
	for frame in range(12):
		probe.set_motion_context(0.0, Vector2.ZERO, "chase", 0.0, 1.0, true)
	_check(_pose_error(frozen, _pose(probe)) < 0.000001 and probe.mirrored_root.transform == frozen_facing, "same-position zero-delta pause still freezes the entire pose exactly")
	relocation = {"initial_context": initial, "teleport_context": teleported}
	probe.free()


func _validate_world_gait() -> void:
	_check(rig.has_method("get_gait_debug"), "gait exposes contact phases for independent world-space measurement")
	if not rig.has_method("get_gait_debug"):
		return
	var saved_transform := rig.transform
	rig.position = Vector2(600, 400)
	rig.scale = Vector2.ONE * 0.55
	rig.reset_pose()
	var contacts := {}
	var samples := {}
	var segments := {}
	var max_drift := 0.0
	var max_turn_pose_step := 0.0
	var max_turn_joint_step := 0.0
	var late_stop_positions := {}
	var max_stop_drift := 0.0
	var max_stop_height_error := 0.0
	var previous_pose := _pose(rig)
	var previous_facing := 1.0
	var all_settled_contacts := true
	for frame in range(420):
		var speed := 47.5
		if frame >= 60 and frame < 180:
			speed = 95.0
		elif frame >= 180 and frame < 210:
			speed = lerpf(95.0, 0.0, float(frame - 180) / 29.0)
		elif frame >= 210 and frame < 270:
			speed = 0.0
		elif frame >= 270:
			speed = -95.0 * minf(1.0, float(frame - 269) / 30.0)
		var facing := -1.0 if frame >= 270 else 1.0
		var velocity := Vector2(speed, 0)
		rig.position += velocity / 60.0
		rig.set_motion_context(1.0 / 60.0, velocity, "chase", 0.0, facing, true)
		var debug: Dictionary = rig.get_gait_debug()
		var states: Dictionary = debug.get("foot_contacts", {})
		if frame == 0:
			_check(states.size() == 4, "all four feet report support or swing phases")
		if facing != previous_facing:
			contacts.clear()
		for limb in ["near_front", "far_front", "near_hind", "far_hind"]:
			var paw: Bone2D = rig.bones[limb + "_paw"]
			var foot: Dictionary = states.get(limb, {})
			var planted: bool = foot.get("stance", foot.get("planted", false))
			if planted and absf(speed) > 1.0:
				if not contacts.has(limb):
					contacts[limb] = {"anchor": paw.global_position, "frames": 0}
				contacts[limb].frames += 1
				if int(contacts[limb].frames) >= 3:
					max_drift = maxf(max_drift, absf(paw.global_position.x - Vector2(contacts[limb].anchor).x))
					samples[limb] = int(samples.get(limb, 0)) + 1
			else:
				if contacts.has(limb) and int(contacts[limb].frames) >= 3:
					segments[limb] = int(segments.get(limb, 0)) + 1
				contacts.erase(limb)
			if frame >= 250 and frame < 270:
				all_settled_contacts = all_settled_contacts and planted
				if not late_stop_positions.has(limb):
					late_stop_positions[limb] = paw.global_position
				max_stop_drift = maxf(max_stop_drift, paw.global_position.distance_to(late_stop_positions[limb]))
				var local_paw: Vector2 = rig.mirrored_root.to_local(paw.global_position)
				var rest_ankle: Vector2 = rig.joints[limb][rig._foot_index(limb)]
				max_stop_height_error = maxf(max_stop_height_error, absf(local_paw.y - rest_ankle.y) * 0.55)
		var current := _pose(rig)
		if frame >= 180 and facing == previous_facing:
			for path in previous_pose:
				var before: Transform2D = previous_pose[path]
				var after: Transform2D = current[path]
				max_turn_pose_step = maxf(max_turn_pose_step, before.origin.distance_to(after.origin))
				max_turn_joint_step = maxf(max_turn_joint_step, absf(wrapf(after.get_rotation() - before.get_rotation(), -PI, PI)))
		previous_pose = current
		previous_facing = facing
	_check(max_drift <= 1.0, "planted paws move no more than one world pixel during measured support segments")
	for limb in ["near_front", "far_front", "near_hind", "far_hind"]:
		_check(int(samples.get(limb, 0)) >= 20 and int(segments.get(limb, 0)) >= 2, "%s has enough independent planted-foot samples" % limb)
	_check(all_settled_contacts and max_stop_height_error <= 1.0, "all feet land by the settled stopped interval")
	_check(max_stop_drift <= 0.1, "settled stopped feet stay fixed in world space")
	_check(max_turn_pose_step <= 8.0 and max_turn_joint_step <= 0.6, "deceleration, stop and reversed gait avoid instantaneous local pose jumps")
	world_gait = {"scale": 0.55, "maximum_support_world_x_drift": max_drift, "support_samples": samples,
		"support_segments": segments, "maximum_stopped_world_drift": max_stop_drift,
		"maximum_stopped_height_error": max_stop_height_error, "maximum_turn_local_step": max_turn_pose_step,
		"maximum_turn_joint_step": max_turn_joint_step, "settled_contacts": all_settled_contacts}
	rig.transform = saved_transform
	rig.reset_pose()


func _run() -> void:
	var script: Script = load(_local("forest_wolf_rig.gd"))
	_check(script != null, "wolf runtime loads")
	if script == null:
		_finish()
		return
	rig = script.new()
	root.add_child(rig)
	_check(rig.build(_art_dir()), "current natural wolf parts build a native rig")
	if rig.find_children("*", "Bone2D", true, false).is_empty():
		_finish()
		return
	_validate_skin()
	_validate_alpha()
	_validate_mouth()
	_validate_death_geometry()
	_validate_continuity()
	rig.reset_pose()
	rig.set_motion_context(1.0 / 60.0, Vector2.ZERO, "idle", 0.0, 1.0, true)
	var idle_pose := _pose(rig)
	var paws := {}
	for node in rig.find_children("*", "Bone2D", true, false):
		if "paw" in str(node.name).to_lower() or "foot" in str(node.name).to_lower():
			paws[str(rig.get_path_to(node))] = node.global_position
	_check(paws.size() >= 4, "each leg has an independently bound paw")
	for frame in range(180):
		rig.set_motion_context(1.0 / 60.0, Vector2.ZERO, "idle", 0.0, 1.0, true)
		for path in paws:
			var node: Node2D = rig.get_node(path)
			maximum_idle_paw_drift = maxf(maximum_idle_paw_drift, node.global_position.distance_to(paws[path]))
	_check(maximum_idle_paw_drift < 0.01, "breathing keeps all four idle paw contacts fixed")
	_check(_pose_error(idle_pose, _pose(rig)) > 0.0001, "idle has living head, breathing or tail motion")
	var paused_pose := _pose(rig)
	var paused_scale := rig.scale
	for frame in range(20):
		rig.set_motion_context(0.0, Vector2.ZERO, "idle", 0.0, 1.0, true)
	_check(_pose_error(paused_pose, _pose(rig)) < 0.000001 and rig.scale == paused_scale, "zero external delta freezes pose and facing")
	rig.reset_pose()
	_advance(0.35, Vector2(95.0, 0.0), "chase")
	var chase_pose := _pose(rig)
	_advance(0.25, Vector2(95.0, 0.0), "chase")
	_check(_pose_error(chase_pose, _pose(rig)) > 0.001, "actual pursuit movement drives the four-leg gait")
	var facing_root: Node2D = rig.find_child("Facing", true, false)
	var right_scale := facing_root.scale
	rig.set_motion_context(1.0 / 60.0, Vector2(-95.0, 0.0), "chase", 0.0, -1.0, true)
	_check(signf(facing_root.scale.x) != signf(right_scale.x) and is_equal_approx(absf(facing_root.scale.x), absf(right_scale.x)), "left facing mirrors the whole skinned rig without changing width")
	var previous := _pose(rig)
	for state in ["windup", "lunge", "recover"]:
		_advance(0.15, Vector2(300.0, 0.0) if state == "lunge" else Vector2.ZERO, state)
		var current := _pose(rig)
		_check(_pose_error(previous, current) > 0.001, "%s changes bone pose through the public state input" % state)
		previous = current
	rig.trigger_hurt()
	_advance(0.06, Vector2.ZERO, "recover")
	_check(_pose_error(previous, _pose(rig)) > 0.001, "real hurt feedback reaches the bone pose")
	_advance(1.2, Vector2.ZERO, "chase", 1.0, false)
	var death_pose := _pose(rig)
	_advance(1.0, Vector2.ZERO, "chase", 1.0, false)
	_check(_pose_error(death_pose, _pose(rig)) < 0.000001, "death holds its final bone pose")
	_check(float(rig.get_runtime_summary().get("death_progress", 0.0)) >= 0.99, "death animation reaches its terminal state")
	rig.reset_pose()
	_check(float(rig.get_runtime_summary().get("death_progress", 1.0)) == 0.0, "restart clears death state")
	_advance(0.2, Vector2(95.0, 0.0), "chase")
	_check(_pose_error(death_pose, _pose(rig)) > 0.1, "restarted wolf leaves the terminal death pose")
	_validate_world_gait()
	_validate_relocation()
	var scene_path := "res://tscn/scene/ActionBattleOriginalWandererScene.tscn"
	if not ResourceLoader.exists(scene_path):
		scene_path = _local("ActionBattleFeelTest.tscn")
	var packed: PackedScene = load(scene_path)
	_check(packed != null, "current battle scene loads")
	if packed != null:
		var battle: Node2D = packed.instantiate()
		root.add_child(battle)
		battle.set_process(false)
		battle.set_process_input(false)
		var live_wolf: Node2D = null
		for candidate in battle.find_children("*", "Node2D", true, false):
			if candidate.get_script() == script:
				live_wolf = candidate
		_check(live_wolf != null, "the live battle instantiates the new wolf runtime")
		if live_wolf != null:
			_check(live_wolf.find_children("*", "Skeleton2D", true, false).size() == 1, "the integrated wolf uses its real native skeleton")
			_check(is_equal_approx(live_wolf.scale.x, 0.55) and is_equal_approx(live_wolf.scale.y, 0.55), "the live battle uses the approved smaller wolf scale")
		battle.free()
	_finish()


func _finish() -> void:
	var output := ProjectSettings.globalize_path(_local("../../output/forest_wolf_joint_revision_20261104")).simplify_path()
	DirAccess.make_dir_recursive_absolute(output)
	var report := {"passed": failures.is_empty(), "checks": checks, "failures": failures,
		"maximum_weight_error": maximum_weight_error, "maximum_idle_paw_drift": maximum_idle_paw_drift,
		"mixed_vertices": mixed_vertices, "maximum_animation_step": maximum_animation_step,
		"maximum_joint_rotation_step": maximum_joint_rotation_step, "source_alpha": source_alpha,
		"maximum_joint_step_context": maximum_joint_step_context,
		"death_geometry": death_geometry, "engine": Engine.get_version_info(),
		"world_gait": world_gait,
		"relocation": relocation,
		"project_root": ProjectSettings.globalize_path("res://"),
		"runtime": rig.get_runtime_summary() if rig != null else {}}
	var environment := "main" if _local("").begins_with("res://tools/") else "standalone"
	for filename in ["smoke_report.json", "smoke_report_%s.json" % environment]:
		var file := FileAccess.open(output.path_join(filename), FileAccess.WRITE)
		file.store_string(JSON.stringify(report, "\t"))
		file.close()
	print("FOREST WOLF RIG ", "PASS" if failures.is_empty() else "FAIL", ": ", checks, " checks; ", output)
	quit(0 if failures.is_empty() else 1)
