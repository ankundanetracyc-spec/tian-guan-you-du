extends SceneTree

var failures: Array[String] = []
var metrics := {"maximum_link_error": 0.0, "maximum_root_drift": 0.0,
	"maximum_planted_pad_drift": 0.0, "maximum_flat_pad_toe_height": 0.0,
	"maximum_target_error": 0.0, "maximum_joint_step": 0.0}
var articulation := {}

func _initialize() -> void:
	call_deferred("_run")

func _local(path: String) -> String:
	return get_script().resource_path.get_base_dir().path_join(path)

func _check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _turn(a: Vector2, b: Vector2) -> float:
	return atan2(a.cross(b), a.dot(b))

func _run() -> void:
	var actor = load(_local("forest_wolf_rig.gd")).new()
	root.add_child(actor)
	actor.scale = Vector2.ONE * 0.55
	actor.position = Vector2(800, 500)
	_check(actor.build(_local("art/forest_wolf_ingame_v3")), "articulated wolf builds")
	_check(actor.bones.size() == 26, "four complete four-bone limbs are present")
	var anchors := {}
	var previous_angles := {}
	for limb in actor.LIMBS:
		var chain: Array = actor.joints[limb]
		_check(chain.size() == 5, limb + " has root, elbow/stifle, wrist/hock, pad and toe landmarks")
		var names: Array = actor._limb_bone_names(limb)
		for index in range(4):
			var bone: Bone2D = actor.bones[names[index]]
			var direction: Vector2 = chain[index + 1] - chain[index]
			_check(absf(bone.get_length() - direction.length()) < 0.001, limb + " editor bone length matches the anatomy")
			_check(absf(wrapf(bone.get_bone_angle() - direction.angle(), -PI, PI)) < 0.001, limb + " editor bone points along its actual shaft")
		articulation[limb] = {"samples": 0, "min_distal_turn": INF, "max_distal_turn": -INF,
			"min_proximal_turn": INF, "max_proximal_turn": -INF}
	var prior_facing := 1.0
	for frame in range(720):
		var velocity := Vector2(20, 0)
		if frame >= 120 and frame < 240: velocity.x = 47.5
		elif frame >= 240 and frame < 420: velocity.x = 95
		elif frame >= 420 and frame < 480: velocity.x = lerpf(95, 0, (frame - 420) / 59.0)
		elif frame >= 480 and frame < 540: velocity = Vector2.ZERO
		elif frame >= 540: velocity.x = -95
		var facing := -1.0 if frame >= 540 else 1.0
		if facing != prior_facing:
			anchors.clear()
			previous_angles.clear()
		actor.global_position += velocity / 60.0
		actor.set_motion_context(1.0 / 60.0, velocity, "chase", 0.0, facing, true)
		var contacts: Dictionary = actor.get_gait_debug().foot_contacts
		for limb in actor.LIMBS:
			var chain: Array = actor.joints[limb]
			var names: Array = actor._limb_bone_names(limb)
			var points: Array[Vector2] = []
			for bone_name in names:
				var bone: Bone2D = actor.bones[bone_name]
				points.append(actor.mirrored_root.to_local(bone.global_position))
			var pad_bone: Bone2D = actor.bones[limb + "_paw"]
			points.append(actor.mirrored_root.to_local(pad_bone.to_global(Vector2(chain[4]) - Vector2(chain[3]))))
			for index in range(4):
				var actual := points[index].distance_to(points[index + 1])
				var rest := Vector2(chain[index]).distance_to(chain[index + 1])
				metrics.maximum_link_error = maxf(metrics.maximum_link_error, absf(actual - rest))
			var parent_name := "chest" if "front" in limb else "pelvis"
			var parent: Bone2D = actor.bones[parent_name]
			var root_offset: Vector2 = chain[0] - Vector2(actor.rest_points[parent_name])
			var expected_root: Vector2 = actor.mirrored_root.to_local(parent.to_global(root_offset))
			metrics.maximum_root_drift = maxf(metrics.maximum_root_drift, expected_root.distance_to(points[0]))
			var proximal := _turn(points[1] - points[0], points[2] - points[1])
			var distal := _turn(points[2] - points[1], points[3] - points[2])
			var state: Dictionary = articulation[limb]
			state.min_proximal_turn = minf(state.min_proximal_turn, proximal)
			state.max_proximal_turn = maxf(state.max_proximal_turn, proximal)
			if previous_angles.has(limb):
				metrics.maximum_joint_step = maxf(metrics.maximum_joint_step, absf(proximal - float(previous_angles[limb].x)))
				metrics.maximum_joint_step = maxf(metrics.maximum_joint_step, absf(distal - float(previous_angles[limb].y)))
			previous_angles[limb] = Vector2(proximal, distal)
			if bool(contacts[limb].stance):
				state.samples += 1
				state.min_distal_turn = minf(state.min_distal_turn, distal)
				state.max_distal_turn = maxf(state.max_distal_turn, distal)
				if not anchors.has(limb): anchors[limb] = pad_bone.global_position
				metrics.maximum_planted_pad_drift = maxf(metrics.maximum_planted_pad_drift, pad_bone.global_position.distance_to(anchors[limb]))
				metrics.maximum_flat_pad_toe_height = maxf(metrics.maximum_flat_pad_toe_height, absf(points[4].y - points[3].y))
			else:
				anchors.erase(limb)
			metrics.maximum_target_error = maxf(metrics.maximum_target_error, float(actor.limb_pose_debug[limb].pad_error))
		prior_facing = facing
	for limb in actor.LIMBS:
		var state: Dictionary = articulation[limb]
		_check(int(state.samples) > 80, limb + " supplies real planted-pad samples")
		_check(float(state.max_distal_turn) - float(state.min_distal_turn) > 0.05, limb + " wrist/hock articulates above the planted pad")
		if "front" in limb:
			_check(float(state.max_proximal_turn) < -0.025, limb + " elbow never flips or locks straight")
			_check(float(state.max_distal_turn) <= 0.20, limb + " wrist avoids backward collapse")
		else:
			_check(float(state.min_proximal_turn) > 0.10, limb + " stifle keeps its forward bend")
			_check(float(state.max_distal_turn) < -0.10, limb + " hock keeps the opposing S-shaped bend")
	_check(float(metrics.maximum_link_error) < 0.005, "all four leg segments retain their anatomical lengths")
	_check(float(metrics.maximum_root_drift) < 0.005, "shoulder/hip roots stay attached to the chest/pelvis")
	_check(float(metrics.maximum_planted_pad_drift) < 0.1, "actual pads, rather than wrists/hocks, stay planted")
	_check(float(metrics.maximum_flat_pad_toe_height) < 0.005, "weight-bearing toes remain on the pad floor plane")
	_check(float(metrics.maximum_target_error) < 0.1, "normal gait targets are reachable without sliding or root stretching")
	_check(float(metrics.maximum_joint_step) < 0.60, "joint articulation remains continuous")
	var output := ProjectSettings.globalize_path(_local("../../output/forest_wolf_joint_revision_20261104")).simplify_path()
	DirAccess.make_dir_recursive_absolute(output)
	var report := FileAccess.open(output.path_join("anatomy_report.json"), FileAccess.WRITE)
	report.store_string(JSON.stringify({"passed": failures.is_empty(), "failures": failures, "metrics": metrics, "limbs": articulation}, "\t"))
	print("WOLF JOINT ANATOMY ", "PASS" if failures.is_empty() else "FAIL", " ", JSON.stringify(metrics))
	actor.queue_free()
	await process_frame
	quit(0 if failures.is_empty() else 1)
