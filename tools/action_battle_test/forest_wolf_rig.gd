extends Node2D

## A scene-local, genuinely weighted Skeleton2D wolf. This node owns no combat
## decisions: the arena supplies movement, state and its remaining state clock.
## All animation advances through set_motion_context, so arena pause freezes it.

const PART_ORDER := ["far_hind", "far_front", "tail", "torso", "near_hind", "near_front", "head", "jaw", "ear_far", "ear_near"]
const DEFAULT_JOINTS := {
	"torso": [[-26.0,-45.0],[0.0,-50.0],[25.0,-51.0],[39.0,-61.0]],
	"head": [[44.0,-65.0]], "jaw": [[49.0,-61.0]],
	"near_front": [[25.0,-46.0],[27.0,-25.0],[32.0,-5.0],[39.0,-2.0]],
	"far_front": [[16.0,-46.0],[15.0,-25.0],[15.0,-7.0],[22.0,-4.0]],
	"near_hind": [[-28.0,-43.0],[-19.0,-26.0],[-33.0,-10.0],[-27.0,-2.0]],
	"far_hind": [[-36.0,-43.0],[-30.0,-27.0],[-42.0,-12.0],[-36.0,-4.0]],
	"tail": [[-37.0,-47.0],[-53.0,-42.0],[-69.0,-32.0],[-79.0,-25.0]],
	"ear_far": [[43.0,-76.0]], "ear_near": [[49.0,-76.0]]
}
const LIMBS := ["far_hind", "far_front", "near_hind", "near_front"]
const BODY_NAMES := ["pelvis", "spine", "chest", "neck"]

class BoneOverlay extends Node2D:
	var actor: Node2D
	func _draw() -> void:
		if is_instance_valid(actor):
			actor._draw_debug_bones(self)

var skeleton: Skeleton2D
var mirrored_root: Node2D
var bones: Dictionary = {}
var meshes: Array[Polygon2D] = []
var validation_errors: Array[String] = []
var parts: Dictionary = {}
var joints: Dictionary = {}
var rest_points: Dictionary = {}
var pose_transforms: Dictionary = {}
var bone_parent: Dictionary = {}
var source_texture: Texture2D
var source_path := ""
var debug_bones := false:
	set(value):
		debug_bones = value
		if is_instance_valid(_bone_overlay):
			_bone_overlay.queue_redraw()
var animation_time := 0.0
var gait_phase := 0.0
var movement_blend := 0.0
var hurt_remaining := 0.0
var death_progress := 0.0
var current_state := "chase"
var current_facing := 1.0
var _weighted_vertices := 0
var _built := false
var _bone_overlay: Node2D
var _windup_amount := 0.0
var _lunge_amount := 0.0
var _recovery_amount := 0.0
var _sample_delta := 0.0
var _foot_offsets: Dictionary = {}
var mouth_interior: Polygon2D
var foot_contacts: Dictionary = {}
var _last_local_ankles: Dictionary = {}
var _action_foot_start: Dictionary = {}
var _gait_trot_blend := 0.0
var _gait_duty := 0.72
var _gait_stride := 18.0
var _gait_frequency := 0.0
var _gait_local_velocity := Vector2.ZERO
var _gait_previous_velocity := Vector2.ZERO
var _gait_action := false
var _previous_gait_action := false
var _tail_inertia := 0.0
var _tail_inertia_speed := 0.0
var _support_pitch := 0.0
var _continuous_head := false
var _head_weight_start := 35.0
var _head_weight_end := 52.0
var _mouth_landmarks: Dictionary = {}
var _gait_has_context := false
var _last_actor_transform := Transform2D.IDENTITY
var limb_pose_debug: Dictionary = {}
var _limb_last_phi: Dictionary = {}
var _world_velocity := Vector2.ZERO
var _standing_compression := 0.0


func build(art_dir: String) -> bool:
	if _built or mirrored_root != null:
		push_error("Wolf rig build must be called once per instance.")
		return false
	var manifest: Variant = JSON.parse_string(FileAccess.get_file_as_string(art_dir.path_join("rig.json")))
	if not manifest is Dictionary or not manifest.get("parts") is Dictionary:
		validation_errors.append("Invalid wolf rig manifest")
		return false
	parts = manifest.parts
	_continuous_head = bool(manifest.get("continuous_head", false))
	_mouth_landmarks = manifest.get("mouth", {})
	source_path = art_dir.path_join(str(manifest.get("source", "source.png")))
	var bitmap := Image.new()
	if bitmap.load(ProjectSettings.globalize_path(source_path)) != OK:
		validation_errors.append("Wolf source texture could not be loaded")
		return false
	bitmap.generate_mipmaps()
	source_texture = ImageTexture.create_from_image(bitmap)
	for key in DEFAULT_JOINTS:
		var coordinates: Array = parts.get(key, {}).get("joints", DEFAULT_JOINTS[key])
		var points: Array[Vector2] = []
		for pair in coordinates:
			points.append(Vector2(float(pair[0]), float(pair[1])))
		joints[key] = points
	if manifest.has("head_joint"):
		var head_joint: Array = manifest.head_joint
		joints.head = [Vector2(float(head_joint[0]), float(head_joint[1]))]
	_head_weight_start = float(manifest.get("head_weight_start", Vector2(joints.torso[3]).x - 1.0))
	_head_weight_end = float(manifest.get("head_weight_end", Vector2(joints.head[0]).x + 8.0))
	for required in ["torso", "head", "jaw", "near_front", "far_front", "near_hind", "far_hind", "tail"]:
		if required == "head" and _continuous_head:
			continue
		if not parts.has(required):
			validation_errors.append("Missing wolf attachment: " + required)
	if not validation_errors.is_empty():
		return false
	mirrored_root = Node2D.new()
	mirrored_root.name = "Facing"
	add_child(mirrored_root)
	skeleton = Skeleton2D.new()
	skeleton.name = "WolfSkeleton"
	mirrored_root.add_child(skeleton)
	_create_bone("root", "", Vector2.ZERO)
	for index in range(4):
		_create_bone(BODY_NAMES[index], "root" if index == 0 else BODY_NAMES[index - 1], joints.torso[index])
	_create_bone("head", "neck", joints.head[0])
	_create_bone("jaw", "head", joints.jaw[0])
	for limb in LIMBS:
		var parent_name: String = "chest" if "front" in limb else "pelvis"
		var names := _limb_bone_names(limb)
		for index in range(names.size()):
			var bone_name: String = names[index]
			_create_bone(bone_name, parent_name, joints[limb][index])
			parent_name = bone_name
	for index in range(3):
		_create_bone("tail_%d" % index, "pelvis" if index == 0 else "tail_%d" % (index - 1), joints.tail[index])
	for ear in ["ear_far", "ear_near"]:
		if parts.has(ear):
			_create_bone(ear, "head", joints[ear][0])
	_update_bone_gizmos()
	mouth_interior = Polygon2D.new()
	mouth_interior.name = "MouthInterior"
	mouth_interior.color = Color(0.17, 0.135, 0.12, 1.0)
	mouth_interior.z_index = int(parts.torso.get("layer", PART_ORDER.find("torso"))) - 1 if _continuous_head else PART_ORDER.find("head")
	mirrored_root.add_child(mouth_interior)
	for part_name in PART_ORDER:
		if parts.has(part_name) and parts[part_name].has("rect"):
			_create_mesh(part_name)
	_bone_overlay = BoneOverlay.new()
	_bone_overlay.name = "BoneOverlay"
	_bone_overlay.actor = self
	_bone_overlay.z_index = 100
	add_child(_bone_overlay)
	_built = validation_errors.is_empty()
	reset_pose()
	return _built


func _create_bone(bone_name: String, parent_name: String, point: Vector2) -> void:
	var parent_node: Node2D = skeleton if parent_name.is_empty() else bones[parent_name]
	var bone := Bone2D.new()
	bone.name = bone_name
	bone.set_autocalculate_length_and_angle(false)
	bone.length = 7.0
	bone.position = point - Vector2.ZERO if parent_name.is_empty() else point - Vector2(rest_points[parent_name])
	bone.rest = bone.transform
	parent_node.add_child(bone)
	bones[bone_name] = bone
	bone_parent[bone_name] = parent_name
	rest_points[bone_name] = point
	pose_transforms[bone_name] = Transform2D(0.0, point)


func _mesh_bone_names(part_name: String) -> Array:
	if part_name == "torso":
		return BODY_NAMES + ["head"] if _continuous_head else BODY_NAMES
	if part_name in LIMBS:
		return _limb_bone_names(part_name) + ["chest" if "front" in part_name else "pelvis"]
	if part_name == "tail":
		return ["tail_0", "tail_1", "tail_2", "pelvis"]
	return [part_name]


func _limb_bone_names(limb: String) -> Array:
	if joints[limb].size() >= 5:
		return [limb + "_upper", limb + "_lower", limb + ("_wrist" if "front" in limb else "_hock"), limb + "_paw"]
	return [limb + "_upper", limb + "_lower", limb + "_paw"]


func _foot_index(limb: String) -> int:
	return 3 if joints[limb].size() >= 5 else 2


func _update_bone_gizmos() -> void:
	for key in bones:
		var bone: Bone2D = bones[key]
		for child in bone.get_children():
			if child is Bone2D:
				var direction: Vector2 = Vector2(rest_points[str(child.name)]) - Vector2(rest_points[key])
				bone.set_length(direction.length())
				bone.set_bone_angle(direction.angle())
				break
	for limb in LIMBS:
		var names := _limb_bone_names(limb)
		for index in range(names.size()):
			var direction: Vector2 = joints[limb][index + 1] - joints[limb][index]
			bones[names[index]].set_length(direction.length())
			bones[names[index]].set_bone_angle(direction.angle())
	for index in range(3):
		var direction: Vector2 = joints.tail[index + 1] - joints.tail[index]
		bones["tail_%d" % index].set_length(direction.length())
		bones["tail_%d" % index].set_bone_angle(direction.angle())


func _create_mesh(part_name: String) -> void:
	var config: Dictionary = parts[part_name]
	if not config.has("bounds") or not config.has("rect"):
		validation_errors.append("Attachment lacks bounds/rect: " + part_name)
		return
	var bounds: Array = config.bounds
	var region: Array = config.rect
	var columns := 32 if part_name == "torso" and _continuous_head else (16 if part_name in ["torso", "tail"] else 8)
	var rows := 18 if part_name == "torso" and _continuous_head else (10 if part_name in ["torso", "tail", "head", "jaw"] else 40)
	var vertices := PackedVector2Array()
	var uv := PackedVector2Array()
	var triangles: Array[PackedInt32Array] = []
	var mesh_names := _mesh_bone_names(part_name)
	var weight_sets: Array[PackedFloat32Array] = []
	for unused in mesh_names:
		weight_sets.append(PackedFloat32Array())
	for row in range(rows + 1):
		for column in range(columns + 1):
			var u := float(column) / columns
			var v := float(row) / rows
			var point := Vector2(float(bounds[0]) + float(bounds[2]) * u, float(bounds[1]) + float(bounds[3]) * v)
			vertices.append(point)
			uv.append(Vector2(float(region[0]) + float(region[2]) * u, float(region[1]) + float(region[3]) * v))
			var weights := _vertex_weights(part_name, point)
			var total := 0.0
			for index in range(mesh_names.size()):
				weight_sets[index].append(weights[index])
				total += weights[index]
			if not is_equal_approx(total, 1.0):
				validation_errors.append("Unnormalized wolf skin weight: " + part_name)
			if row < rows and column < columns:
				var a := row * (columns + 1) + column
				var b := a + columns + 1
				triangles.append(PackedInt32Array([a, a + 1, b]))
				triangles.append(PackedInt32Array([a + 1, b + 1, b]))
	var mesh := Polygon2D.new()
	mesh.name = "WolfSkin_" + part_name
	mesh.polygon = vertices
	mesh.uv = uv
	mesh.polygons = triangles
	mesh.texture = source_texture
	mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	mesh.texture_repeat = CanvasItem.TEXTURE_REPEAT_DISABLED
	mesh.z_index = int(config.get("layer", PART_ORDER.find(part_name)))
	mirrored_root.add_child(mesh)
	mesh.skeleton = mesh.get_path_to(skeleton)
	for index in range(mesh_names.size()):
		mesh.add_bone(skeleton.get_path_to(bones[mesh_names[index]]), weight_sets[index])
	meshes.append(mesh)
	_weighted_vertices += vertices.size()


func _vertex_weights(part_name: String, point: Vector2) -> PackedFloat32Array:
	if part_name == "torso":
		# The shoulder, abdomen and pelvis remain one continuous painted surface.
		var rear_share := 1.0 - smoothstep(-23.0, -1.0, point.x)
		var chest_share := smoothstep(-3.0, 25.0, point.x)
		var neck_share := smoothstep(float(joints.torso[2].x) - 4.0, float(joints.torso[3].x) + 2.0, point.x) * (1.0 - smoothstep(-53.0, -37.0, point.y))
		var middle := (1.0 - rear_share) * (1.0 - chest_share)
		var chest := (1.0 - rear_share) * chest_share
		var body_weights := PackedFloat32Array([rear_share * (1.0 - neck_share), middle * (1.0 - neck_share), chest * (1.0 - neck_share), neck_share])
		if _continuous_head:
			var head_share := smoothstep(_head_weight_start, maxf(_head_weight_start + 1.0, _head_weight_end), point.x)
			for index in range(body_weights.size()):
				body_weights[index] *= 1.0 - head_share
			body_weights.append(head_share)
		return body_weights
	if part_name in LIMBS or part_name == "tail":
		var chain: Array = joints[part_name]
		var distance := _chain_distance(point, chain)
		if part_name in LIMBS and chain.size() >= 5:
			# Articulated shafts stay rigid. Only a narrow ring around each real
			# elbow/knee, wrist/hock and pad joint blends into its neighbours.
			var shares := PackedFloat32Array()
			var boundaries: Array[float] = [0.0]
			for index in range(4):
				boundaries.append(boundaries[-1] + chain[index].distance_to(chain[index + 1]))
			for index in range(4):
				var incoming := 1.0
				var outgoing := 0.0
				if index > 0:
					var width := minf(2.0, minf(boundaries[index] - boundaries[index - 1], boundaries[index + 1] - boundaries[index]) * 0.24)
					incoming = smoothstep(boundaries[index] - width, boundaries[index] + width, distance)
				if index < 3:
					var width := minf(2.0, minf(boundaries[index + 1] - boundaries[index], boundaries[index + 2] - boundaries[index + 1]) * 0.24)
					outgoing = smoothstep(boundaries[index + 1] - width, boundaries[index + 1] + width, distance)
				shares.append(incoming * (1.0 - outgoing))
			var cap_share := 1.0 - smoothstep(0.0, 4.0, distance)
			for index in range(shares.size()):
				shares[index] *= 1.0 - cap_share
			shares.append(cap_share)
			return shares
		var first_length: float = chain[0].distance_to(chain[1])
		var second_length: float = chain[1].distance_to(chain[2])
		var lower_share := smoothstep(first_length - 4.0, first_length + 4.0, distance)
		var end_share := smoothstep(first_length + second_length - 3.0, first_length + second_length + 3.0, distance)
		var attachment_share := 1.0 - smoothstep(0.0, 8.0, distance)
		return PackedFloat32Array([(1.0 - lower_share) * (1.0 - attachment_share), lower_share * (1.0 - end_share) * (1.0 - attachment_share), lower_share * end_share * (1.0 - attachment_share), attachment_share])
	return PackedFloat32Array([1.0])


func _chain_distance(point: Vector2, chain: Array) -> float:
	var closest_squared := INF
	var distance := 0.0
	var walked := 0.0
	for index in range(chain.size() - 1):
		var segment: Vector2 = chain[index + 1] - chain[index]
		var factor := clampf((point - Vector2(chain[index])).dot(segment) / maxf(segment.length_squared(), 0.001), 0.0, 1.0)
		var projected: Vector2 = chain[index] + segment * factor
		var squared := projected.distance_squared_to(point)
		if squared < closest_squared:
			closest_squared = squared
			distance = walked + segment.length() * factor
		walked += segment.length()
	return distance


func reset_pose() -> void:
	animation_time = 0.0
	gait_phase = 0.0
	movement_blend = 0.0
	hurt_remaining = 0.0
	death_progress = 0.0
	_windup_amount = 0.0
	_lunge_amount = 0.0
	_recovery_amount = 0.0
	_sample_delta = 0.0
	_foot_offsets.clear()
	foot_contacts.clear()
	_last_local_ankles.clear()
	_action_foot_start.clear()
	_gait_trot_blend = 0.0
	_gait_duty = 0.72
	_gait_stride = 18.0
	_gait_frequency = 0.0
	_gait_local_velocity = Vector2.ZERO
	_gait_previous_velocity = Vector2.ZERO
	_gait_action = false
	_previous_gait_action = false
	_tail_inertia = 0.0
	_tail_inertia_speed = 0.0
	_support_pitch = 0.0
	limb_pose_debug.clear()
	_limb_last_phi.clear()
	_world_velocity = Vector2.ZERO
	_standing_compression = 0.0
	_gait_has_context = false
	_last_actor_transform = global_transform
	current_state = "chase"
	if _built:
		_sample_pose(Vector2.ZERO, "chase", 0.0, true)
	if is_instance_valid(_bone_overlay):
		_bone_overlay.queue_redraw()


func trigger_hurt() -> void:
	if death_progress <= 0.0:
		hurt_remaining = 0.28


func set_motion_context(delta: float, velocity: Vector2, state: String, clock: float, facing: float, alive: bool) -> void:
	if not _built:
		return
	var step := maxf(delta, 0.0)
	# A paused caller may supply zero velocity. It must not iterate a joint
	# solver or change a planted pose while the arena clock is frozen.
	if step == 0.0 and _gait_has_context and global_transform.is_equal_approx(_last_actor_transform) and state == current_state and signf(facing) == current_facing and alive == (death_progress == 0.0):
		return
	_world_velocity = velocity
	_rebase_contacts_if_needed(step, velocity)
	_sample_delta = step
	animation_time += step
	hurt_remaining = maxf(0.0, hurt_remaining - step)
	current_state = state
	var next_facing := -1.0 if facing < 0.0 else 1.0
	if next_facing != current_facing:
		foot_contacts.clear()
		_last_local_ankles.clear()
	current_facing = next_facing
	mirrored_root.scale.x = current_facing
	if alive:
		death_progress = 0.0
	else:
		death_progress = minf(1.0, death_progress + step / 0.85)
	var moving := clampf(velocity.length() / 95.0, 0.0, 1.0) if alive and state == "chase" else 0.0
	# Acceleration and the last planted step settle over several frames.
	movement_blend = move_toward(movement_blend, moving, step * (4.8 if moving > movement_blend else 3.2))
	_update_gait_contacts(step, velocity, state, alive)
	var support_clearance := _support_clearance() if alive else 0.0
	_standing_compression = move_toward(_standing_compression, support_clearance, step * (18.0 if support_clearance > _standing_compression else 5.0))
	var windup_target := smoothstep(0.0, 1.0, clampf(1.0 - clock / 0.8, 0.0, 1.0)) if state == "windup" and alive else 0.0
	var lunge_target := sin(clampf(1.0 - clock / 0.2, 0.0, 1.0) * PI) if state == "lunge" and alive else 0.0
	var recovery_target := clampf(clock / 1.5, 0.0, 1.0) if state in ["recover", "recovery"] and alive else 0.0
	_windup_amount = move_toward(_windup_amount, windup_target, step * (2.4 if state == "windup" else 5.5))
	_lunge_amount = move_toward(_lunge_amount, lunge_target, step * 11.0)
	_recovery_amount = move_toward(_recovery_amount, recovery_target, step * 5.0)
	_sample_pose(velocity, state, clock, alive)
	_last_actor_transform = global_transform
	_gait_has_context = true
	_bone_overlay.queue_redraw()


func _rebase_contacts_if_needed(step: float, velocity: Vector2) -> void:
	var actual_delta := global_position - _last_actor_transform.origin
	var expected_delta := velocity * step
	var body_width := absf(float(parts.torso.bounds[2]) * global_scale.x)
	var maximum_step := maxf(24.0, body_width * 1.2)
	var discrepancy := maxf(10.0, body_width * 0.5)
	var changed_scale := not global_transform.get_scale().is_equal_approx(_last_actor_transform.get_scale())
	var teleported := actual_delta.length() > maximum_step or (actual_delta - expected_delta).length() > discrepancy
	if not _gait_has_context or changed_scale or teleported:
		# build/reset may happen at origin, before the caller applies the arena
		# position and visual scale. Rebase only once they are final. External
		# teleports similarly create fresh contacts instead of stretching legs
		# back across the arena to an obsolete world-space contact.
		foot_contacts.clear()
		_last_local_ankles.clear()
		_action_foot_start.clear()
		_previous_gait_action = false


func _gait_phase_offset(limb: String) -> float:
	# Four-beat walk progressively becomes diagonal-pair trot. These are phase
	# offsets, not offsets applied to a planted foot's position.
	match limb:
		"near_front": return 0.0
		"far_hind": return lerpf(0.75, 1.0, _gait_trot_blend)
		"far_front": return 0.5
		"near_hind": return lerpf(0.25, 0.5, _gait_trot_blend)
	return 0.0


func _initialize_foot(limb: String) -> void:
	var rest: Vector2 = joints[limb][_foot_index(limb)]
	var local_point: Vector2 = _last_local_ankles.get(limb, rest)
	foot_contacts[limb] = {"stance": true, "planted": true, "mode": "stance", "phase": 0.0,
		"world_position": mirrored_root.to_global(local_point), "lift_from": Vector2.ZERO,
		"landing_world": Vector2.ZERO, "swing_progress": 0.0, "swing_duration": 0.15,
		"can_lift": true, "stopping": false}


func _landing_world(limb: String, velocity: Vector2, seconds: float) -> Vector2:
	var ahead := _gait_local_velocity.normalized() * (_gait_stride * 0.5)
	return mirrored_root.to_global(Vector2(joints[limb][_foot_index(limb)]) + ahead) + velocity * seconds


func _begin_swing(limb: String, velocity: Vector2, stopping: bool = false) -> void:
	var contact: Dictionary = foot_contacts[limb]
	var point: Vector2 = contact.world_position
	var duration := clampf((1.0 - _gait_duty) / maxf(_gait_frequency, 0.1), 0.09, 0.34)
	var landing := _landing_world(limb, velocity, duration)
	if stopping:
		duration = 0.13
		# Preserve horizontal placement; the last raised paw lowers vertically.
		landing = Vector2(point.x, mirrored_root.to_global(joints[limb][_foot_index(limb)]).y)
	contact.merge({"stance": false, "planted": false, "mode": "swing", "lift_from": point,
		"landing_world": landing, "swing_progress": 0.0, "swing_duration": duration,
		"can_lift": false, "stopping": stopping, "settling": false}, true)


func _update_gait_contacts(step: float, velocity: Vector2, state: String, alive: bool) -> void:
	_gait_local_velocity = mirrored_root.global_transform.basis_xform_inv(velocity)
	var local_speed := _gait_local_velocity.length()
	var moving := alive and state == "chase" and local_speed > 0.6
	var trot_target := smoothstep(35.0, 110.0, local_speed) if moving else 0.0
	_gait_trot_blend = move_toward(_gait_trot_blend, trot_target, step * 3.2)
	_gait_duty = lerpf(0.73, 0.58, _gait_trot_blend)
	_gait_stride = lerpf(18.0, 30.0, _gait_trot_blend)
	# v * stance_duration = stride, measured in this skeleton's local units.
	# A .55-scale wolf therefore takes quicker steps than a full-size rig.
	_gait_frequency = local_speed * _gait_duty / _gait_stride if moving else 0.0
	if moving:
		gait_phase = fposmod(gait_phase + TAU * _gait_frequency * step, TAU)
	_gait_action = not alive or state == "lunge"
	for limb in LIMBS:
		if not foot_contacts.has(limb):
			_initialize_foot(limb)
		var contact: Dictionary = foot_contacts[limb]
		var phase := fposmod(gait_phase / TAU + _gait_phase_offset(limb), 1.0)
		contact.phase = phase
		if _gait_action:
			if not _previous_gait_action:
				_action_foot_start[limb] = _last_local_ankles.get(limb, joints[limb][_foot_index(limb)])
			contact.merge({"stance": false, "planted": false, "mode": "action"}, true)
			continue
		if _previous_gait_action:
			contact.world_position = mirrored_root.to_global(_last_local_ankles.get(limb, joints[limb][_foot_index(limb)]))
			_begin_swing(limb, Vector2.ZERO, true)
		if contact.stance:
			if phase < _gait_duty:
				contact.can_lift = true
			if moving:
				var ankle_local := mirrored_root.to_local(contact.world_position)
				var rest_hip: Vector2 = joints[limb][0] + Vector2(0.0, 3.8)
				var total_length := 0.0
				for link in range(_foot_index(limb)):
					total_length += joints[limb][link].distance_to(joints[limb][link + 1])
				var approaching_limit := ankle_local.distance_to(rest_hip) > total_length - 1.2
				var rear_offset: Vector2 = ankle_local - Vector2(joints[limb][_foot_index(limb)])
				var behind_stride := rear_offset.dot(_gait_local_velocity.normalized()) < -_gait_stride * 0.52
				if (bool(contact.can_lift) and phase >= _gait_duty) or approaching_limit or behind_stride:
					_begin_swing(limb, velocity)
		else:
			if not moving and not bool(contact.stopping):
				_begin_swing(limb, Vector2.ZERO, true)
			var duration := float(contact.swing_duration)
			contact.swing_progress = minf(1.0, float(contact.swing_progress) + step / duration)
			var progress := float(contact.swing_progress)
			if moving and not bool(contact.stopping):
				# The destination predicts root travel over the remaining swing.
				# At constant velocity it is a fixed world-space landing point.
				contact.landing_world = _landing_world(limb, velocity, duration * (1.0 - progress))
			var from: Vector2 = contact.lift_from
			var to: Vector2 = contact.landing_world
			var point := from.lerp(to, smoothstep(0.0, 1.0, progress))
			if not bool(contact.stopping) or bool(contact.get("settling", false)):
				var lift_height := 2.5 if bool(contact.get("settling", false)) else lerpf(5.0, 8.0, _gait_trot_blend)
				var local_up_world := mirrored_root.global_transform.basis_xform(Vector2(0.0, -lift_height))
				point += local_up_world * sin(progress * PI)
			contact.world_position = point
			if progress >= 1.0:
				contact.merge({"stance": true, "planted": true, "mode": "stance", "world_position": to, "settling": false}, true)
	if not moving and alive and not _gait_action and step > 0.0:
		_settle_stopped_feet()
	_previous_gait_action = _gait_action
	if step > 0.0:
		var acceleration := (_gait_local_velocity.x - _gait_previous_velocity.x) / step
		_tail_inertia_speed += (-_tail_inertia * 45.0 - _tail_inertia_speed * 13.0 - clampf(acceleration, -900.0, 900.0) * 0.012) * step
		_tail_inertia += _tail_inertia_speed * step
		_tail_inertia = clampf(_tail_inertia, -0.18, 0.18)
		_gait_previous_velocity = _gait_local_velocity
		var fore_support := (float(foot_contacts.near_front.stance) + float(foot_contacts.far_front.stance)) * 0.5
		var rear_support := (float(foot_contacts.near_hind.stance) + float(foot_contacts.far_hind.stance)) * 0.5
		_support_pitch = move_toward(_support_pitch, (rear_support - fore_support) * 0.025, step * 0.25)


func _settle_stopped_feet() -> void:
	for contact in foot_contacts.values():
		if not bool(contact.stance):
			return
	for limb in LIMBS:
		if joints[limb].size() < 5:
			continue
		var chain: Array = joints[limb]
		var pad := mirrored_root.to_local(foot_contacts[limb].world_position)
		var reach := 0.0
		for link in range(3):
			reach += Vector2(chain[link]).distance_to(chain[link + 1])
		# A long final step can be reachable while crouched, yet not once the
		# body stands up. Reposition one real foot before raising the body.
		if Vector2(chain[0]).distance_to(pad) > reach - 0.65:
			_begin_swing(limb, Vector2.ZERO, true)
			foot_contacts[limb].merge({"settling": true, "landing_world": mirrored_root.to_global(chain[3]), "swing_duration": 0.14}, true)
			return


func _support_clearance() -> float:
	var compression := 0.0
	for limb in LIMBS:
		if not foot_contacts.has(limb) or not bool(foot_contacts[limb].stance) or joints[limb].size() < 5:
			continue
		var chain: Array = joints[limb]
		var pad := mirrored_root.to_local(foot_contacts[limb].world_position)
		var reach := -0.45
		for link in range(3):
			reach += Vector2(chain[link]).distance_to(chain[link + 1])
		var lateral := pad.x - float(chain[0].x)
		var vertical := sqrt(maxf(1.0, reach * reach - lateral * lateral))
		compression = maxf(compression, pad.y - float(chain[0].y) - vertical + 0.4)
	return clampf(compression, 0.0, 3.0)


func _pose_bone(bone_name: String, point: Vector2, angle: float = 0.0) -> void:
	var target := Transform2D(angle, point)
	pose_transforms[bone_name] = target
	var parent_name: String = bone_parent[bone_name]
	bones[bone_name].transform = target if parent_name.is_empty() else Transform2D(pose_transforms[parent_name]).affine_inverse() * target


func _sample_pose(_velocity: Vector2, state: String, clock: float, alive: bool) -> void:
	var death := smoothstep(0.0, 1.0, death_progress)
	var breath := sin(animation_time * TAU / 2.6) * 0.30 * (1.0 - death)
	# Slight joint compression leaves room for a real, bounded leg stride.
	var gait_bob := maxf((4.5 - absf(sin(gait_phase)) * 0.65) * movement_blend, _standing_compression) * (1.0 - death)
	var windup := _windup_amount * (1.0 - death)
	var lunge := _lunge_amount * (1.0 - death)
	var recovery := _recovery_amount * (1.0 - death)
	var hurt := sin((1.0 - hurt_remaining / 0.28) * PI) if hurt_remaining > 0.0 else 0.0
	var body_shift := Vector2(-windup * 4.0 + lunge * 4.0 - hurt * 3.0, breath + gait_bob + windup * 5.0 + death * 27.0 + hurt)
	var body_angle := windup * -0.04 + lunge * 0.04 - hurt * 0.05 + death * -0.16 + _support_pitch * movement_blend * (1.0 - death)
	_pose_bone("root", Vector2.ZERO)
	for index in range(4):
		var name_key: String = BODY_NAMES[index]
		var offset := Vector2(rest_points[name_key]) - Vector2(rest_points.pelvis)
		var point := Vector2(rest_points.pelvis) + offset.rotated(body_angle) + body_shift
		if index > 0:
			point.y += breath * float(index) / 3.0
		if index == 3:
			point.y += death * 12.0
		# Shoulder and pelvis receive opposite diagonal-foot loading. The spine
		# carries that load with a slight phase delay rather than bobbing as a board.
		var load_phase := gait_phase + float(index) * 0.7
		var load_amount := movement_blend * (1.0 - death)
		point.y += sin(load_phase) * 0.65 * load_amount
		point.x += sin(load_phase - 0.4) * 0.40 * load_amount
		_pose_bone(name_key, point, body_angle + sin(load_phase) * 0.022 * load_amount)
	var neck_transform: Transform2D = pose_transforms.neck
	var head_point: Vector2 = neck_transform * (Vector2(rest_points.head) - Vector2(rest_points.neck))
	var head_angle := body_angle + sin(animation_time * 1.9) * 0.013 * (1.0 - death) + windup * 0.12 + lunge * 0.19 + recovery * 0.035 - hurt * 0.14 + death * 0.52
	head_point += Vector2(windup * 2.0 + lunge * 4.0, windup * 2.5 + death * 3.0)
	head_point.y -= gait_bob * 0.45
	head_angle -= _support_pitch * movement_blend * 0.7 * (1.0 - death)
	_pose_bone("head", head_point, head_angle)
	var head_transform: Transform2D = pose_transforms.head
	var jaw_point: Vector2 = head_transform * (Vector2(rest_points.jaw) - Vector2(rest_points.head))
	var jaw_open := windup * 0.14 + lunge * 0.42 + recovery * 0.11 + hurt * 0.07 + death * 0.12
	_pose_bone("jaw", jaw_point, head_angle + jaw_open)
	_update_mouth(jaw_open)
	for ear in ["ear_far", "ear_near"]:
		if bones.has(ear):
			var ear_point: Vector2 = head_transform * (Vector2(rest_points[ear]) - Vector2(rest_points.head))
			var twitch := sin(animation_time * 2.4 + (0.9 if ear == "ear_far" else 0.0)) * 0.026
			_pose_bone(ear, ear_point, head_angle + twitch * (1.0 - death) - windup * 0.13 + death * 0.2)
	for limb in LIMBS:
		_pose_limb(limb, state, windup, lunge, death)
	_pose_tail(body_angle, windup, lunge, death)
	if not alive and death_progress >= 1.0:
		# The dead pose is sampled without any live breathing or tail jitter.
		hurt_remaining = 0.0


func _update_mouth(open_amount: float) -> void:
	# The generated upper and lower lips are separate paintings. This small
	# untextured oral cavity joins them as the jaw rotates, behind both lip skins.
	# All landmarks live in the same bind space as their parent attachment.
	mouth_interior.visible = open_amount > 0.028
	if not mouth_interior.visible:
		return
	var head_transform: Transform2D = pose_transforms.head
	var jaw_transform: Transform2D = pose_transforms.jaw
	var upper_corner := _mouth_point("upper_corner", Vector2(50.0, -57.5))
	var upper_tip := _mouth_point("upper_tip", Vector2(64.5, -57.6))
	var lower_tip := _mouth_point("lower_tip", Vector2(64.0, -58.1))
	var lower_corner := _mouth_point("lower_corner", Vector2(47.0, -58.5))
	mouth_interior.polygon = PackedVector2Array([
		head_transform * (upper_corner - Vector2(rest_points.head)),
		head_transform * (upper_tip - Vector2(rest_points.head)),
		jaw_transform * (lower_tip - Vector2(rest_points.jaw)),
		jaw_transform * (lower_corner - Vector2(rest_points.jaw))])


func _mouth_point(key: String, fallback: Vector2) -> Vector2:
	if not _mouth_landmarks.has(key):
		return fallback
	var point: Array = _mouth_landmarks[key]
	return Vector2(float(point[0]), float(point[1]))


func _pose_tail(body_angle: float, windup: float, lunge: float, death: float) -> void:
	var chain: Array = joints.tail
	var live_points: Array[Vector2] = []
	var live_angles: Array[float] = []
	var parent_pose: Transform2D = pose_transforms.pelvis
	var previous_rest: Vector2 = rest_points.pelvis
	for index in range(3):
		var point: Vector2 = parent_pose * (Vector2(chain[index]) - previous_rest)
		var wave := sin(animation_time * 2.8 - index * 0.7) * (0.055 + index * 0.012)
		var angle := body_angle + (wave + _tail_inertia * (1.0 - index * 0.22)) * (1.0 - death) - windup * 0.14 + lunge * 0.11
		live_points.append(point)
		live_angles.append(angle)
		parent_pose = Transform2D(angle, point)
		previous_rest = chain[index]
	live_points.append(parent_pose * (Vector2(chain[3]) - Vector2(chain[2])))
	# With the pelvis lowered the tail lays back along the floor; inheriting
	# the old downward standing-tail direction would drive its tip underground.
	var base: Vector2 = live_points[0]
	var dead_points: Array[Vector2] = [Vector2(base.x, minf(base.y, -15.0)), Vector2(base.x - 17.0, -14.0), Vector2(base.x - 34.0, -11.0), Vector2(base.x - 49.0, -10.0)]
	var final_points: Array[Vector2] = []
	for index in range(4):
		var point: Vector2 = live_points[index].lerp(dead_points[index], death)
		if death > 0.0:
			point.y = minf(point.y, -4.0)
		final_points.append(point)
	for index in range(3):
		# Derive each bone angle from the constrained next joint as well. Blending
		# rotation separately can leave the terminal rendered tail below its point.
		var angle: float = (final_points[index + 1] - final_points[index]).angle() - (Vector2(chain[index + 1]) - Vector2(chain[index])).angle()
		_pose_bone("tail_%d" % index, final_points[index], angle)


func _signed_joint_turn(first: Vector2, second: Vector2) -> float:
	return atan2(first.cross(second), first.dot(second))


func _articulated_candidate(hip: Vector2, pad: Vector2, lengths: Array[float], phi: float, bend: float) -> Dictionary:
	var wrist := pad - Vector2(cos(phi), sin(phi)) * lengths[2]
	var delta := wrist - hip
	var distance := delta.length()
	if distance < absf(lengths[0] - lengths[1]) + 0.001 or distance > lengths[0] + lengths[1] - 0.001:
		return {}
	var forward := delta / distance
	var along := (lengths[0] * lengths[0] - lengths[1] * lengths[1] + distance * distance) / (2.0 * distance)
	var altitude := sqrt(maxf(0.0, lengths[0] * lengths[0] - along * along))
	var knee := hip + forward * along + Vector2(-forward.y, forward.x) * altitude * bend
	return {"knee": knee, "wrist": wrist, "phi": phi,
		"proximal_turn": _signed_joint_turn(knee - hip, wrist - knee),
		"distal_turn": _signed_joint_turn(wrist - knee, pad - wrist)}


func _articulated_score(candidate: Dictionary, preferred: float, previous: float, rest_turn: float, rest_distal: float, is_front: bool, death: float) -> float:
	if candidate.is_empty():
		return INF
	# These are animation guards around the painted neutral limb, rather than
	# clinical range-of-motion values. They preserve the elbow/stifle's bend
	# branch and the opposing hock/pastern bend without a straight-leg flip.
	var proximal_low := -lerpf(2.15, 2.85, death) if is_front else 0.16
	var proximal_high := -0.045 if is_front else lerpf(2.25, 2.85, death)
	var distal_low := -lerpf(1.35, 2.85, death) if is_front else -lerpf(2.35, 2.85, death)
	var distal_high := 0.16 if is_front else -0.14
	var proximal := float(candidate.proximal_turn)
	var distal := float(candidate.distal_turn)
	var error := maxf(proximal_low - proximal, 0.0) + maxf(proximal - proximal_high, 0.0)
	error += maxf(distal_low - distal, 0.0) + maxf(distal - distal_high, 0.0)
	return error * error * 240.0 + pow(wrapf(float(candidate.phi) - preferred, -PI, PI), 2.0) * 0.65 + pow(wrapf(float(candidate.phi) - previous, -PI, PI), 2.0) * 0.45 + pow(proximal - rest_turn, 2.0) * 0.12 + pow(distal - rest_distal, 2.0) * 0.12


func _pose_limb(limb: String, state: String, windup: float, lunge: float, death: float) -> void:
	var chain: Array = joints[limb]
	if chain.size() < 5:
		_pose_legacy_limb(limb, state, windup, lunge, death)
		return
	var is_front := "front" in limb
	var attach_name := "chest" if is_front else "pelvis"
	var attach: Transform2D = pose_transforms[attach_name]
	var hip: Vector2 = attach * (Vector2(chain[0]) - Vector2(rest_points[attach_name]))
	var requested_pad: Vector2 = chain[3]
	var contact: Dictionary = foot_contacts.get(limb, {})
	if not contact.is_empty() and str(contact.mode) != "action":
		requested_pad = mirrored_root.to_local(contact.world_position)
	elif _gait_action:
		var start: Vector2 = _action_foot_start.get(limb, chain[3])
		var extension := Vector2(lunge * (10.0 if is_front else -6.0), -lunge * (8.0 if is_front else 3.0))
		requested_pad = start.lerp(Vector2(chain[3]) + extension, lunge)
	if death > 0.0:
		var dead_pad := Vector2(hip.x + 24.0, float(chain[3].y)) if is_front else Vector2(hip.x - 3.0, float(chain[3].y))
		requested_pad = requested_pad.lerp(dead_pad, death)
	var lengths: Array[float] = []
	for index in range(4):
		lengths.append(Vector2(chain[index]).distance_to(chain[index + 1]))
	var pad := requested_pad
	var reach := lengths[0] + lengths[1] + lengths[2] - 0.08
	if hip.distance_to(pad) > reach:
		# Keep the root attached and the shafts at their actual length. The
		# contact controller lifts before this circle during normal locomotion.
		pad = hip + hip.direction_to(pad) * reach
	var rest_upper: Vector2 = chain[1] - chain[0]
	var rest_lower: Vector2 = chain[2] - chain[1]
	var rest_pastern: Vector2 = chain[3] - chain[2]
	var rest_span: Vector2 = chain[2] - chain[0]
	var bend := 1.0 if rest_span.cross(rest_upper) >= 0.0 else -1.0
	var rest_phi := rest_pastern.angle()
	var previous := float(_limb_last_phi.get(limb, rest_phi))
	var preferred := rest_phi + clampf(-(pad.x - float(chain[3].x)) * 0.012, -0.25, 0.25)
	var progress := float(contact.get("swing_progress", 0.0))
	var swing := str(contact.get("mode", "")) == "swing"
	if swing:
		preferred += sin(progress * PI) * (0.24 if is_front else 0.32)
	var rest_turn := _signed_joint_turn(rest_upper, rest_lower)
	var rest_distal := _signed_joint_turn(rest_lower, rest_pastern)
	var best: Dictionary = {}
	var best_score := INF
	for index in range(101):
		var candidate := _articulated_candidate(hip, pad, lengths, lerpf(0.06, PI - 0.06, index / 100.0), bend)
		var score := _articulated_score(candidate, preferred, previous, rest_turn, rest_distal, is_front, death)
		if score < best_score:
			best = candidate
			best_score = score
	# Refine the free pastern angle continuously, keeping the same knee branch.
	for radius in [0.025, 0.006, 0.0015]:
		if best.is_empty():
			break
		var center := float(best.phi)
		for offset in [-1.0, -0.5, 0.5, 1.0]:
			var candidate := _articulated_candidate(hip, pad, lengths, clampf(center + float(radius) * float(offset), 0.04, PI - 0.04), bend)
			var score := _articulated_score(candidate, preferred, previous, rest_turn, rest_distal, is_front, death)
			if score < best_score:
				best = candidate
				best_score = score
	if best.is_empty():
		push_error("Wolf articulated limb has no reachable pose: " + limb)
		return
	var knee: Vector2 = best.knee
	var wrist: Vector2 = best.wrist
	_limb_last_phi[limb] = best.phi
	var toe_angle := -sin(progress * PI) * 0.25 if swing else 0.0
	var toe: Vector2 = pad + (Vector2(chain[4]) - Vector2(chain[3])).rotated(toe_angle)
	var points: Array[Vector2] = [hip, knee, wrist, pad, toe]
	var names := _limb_bone_names(limb)
	for index in range(4):
		var segment: Vector2 = points[index + 1] - points[index]
		var rest_segment: Vector2 = chain[index + 1] - chain[index]
		_pose_bone(names[index], points[index], segment.angle() - rest_segment.angle())
	_last_local_ankles[limb] = pad
	if not contact.is_empty() and str(contact.mode) == "action":
		contact.world_position = mirrored_root.to_global(pad)
	limb_pose_debug[limb] = {"points": points, "rest_lengths": lengths,
		"proximal_turn": best.proximal_turn, "distal_turn": best.distal_turn,
		"requested_pad": requested_pad, "pad_error": pad.distance_to(requested_pad),
		"root_reference": hip, "stance": bool(contact.get("stance", false))}


func _pose_legacy_limb(limb: String, state: String, windup: float, lunge: float, death: float) -> void:
	var chain: Array = joints[limb]
	var attach_name: String = "chest" if "front" in limb else "pelvis"
	var attach_transform: Transform2D = pose_transforms[attach_name]
	var hip: Vector2 = attach_transform * (Vector2(chain[0]) - Vector2(rest_points[attach_name]))
	var ankle: Vector2 = chain[2]
	if foot_contacts.has(limb) and str(foot_contacts[limb].mode) != "action":
		# No interpolation here: during stance this is the same world ankle on
		# every frame, even as the actor moves, turns or changes visual scale.
		ankle = mirrored_root.to_local(foot_contacts[limb].world_position)
	elif _gait_action:
		var start: Vector2 = _action_foot_start.get(limb, chain[2])
		var extension := Vector2(lunge * (10.0 if "front" in limb else -6.0), -lunge * (8.0 if "front" in limb else 3.0))
		ankle = start.lerp(Vector2(chain[2]) + extension, lunge)
	var toe: Vector2 = ankle + (Vector2(chain[3]) - Vector2(chain[2]))
	var upper_length: float = chain[0].distance_to(chain[1])
	var lower_length: float = chain[1].distance_to(chain[2])
	var direction := ankle - hip
	var action_reach_margin := 0.01
	if death <= 0.0 and foot_contacts.has(limb):
		var contact: Dictionary = foot_contacts[limb]
		if state == "lunge" or (str(contact.mode) == "swing" and bool(contact.stopping)):
			# Keep an airborne attack knee slightly flexed; crossing a perfectly
			# straight two-link configuration otherwise causes a sharp angle snap.
			action_reach_margin = 0.35 + lunge * 0.6
	if death <= 0.0 and foot_contacts.has(limb) and bool(foot_contacts[limb].stance):
		# A planted contact wins over breathing or a tiny shoulder recoil. Let
		# the shoulder/hip joint compress under load instead of moving the paw
		# or stretching the painted upper/lower limb beyond its rest lengths.
		var compression := maxf(0.0, direction.length() - (upper_length + lower_length - 0.01))
		hip = hip.move_toward(ankle, compression)
		direction = ankle - hip
	var distance := clampf(direction.length(), absf(upper_length - lower_length) + 0.01, upper_length + lower_length - action_reach_margin)
	var forward := direction.normalized() if direction.length_squared() > 0.001 else Vector2.DOWN
	var along := (upper_length * upper_length - lower_length * lower_length + distance * distance) / (2.0 * distance)
	var altitude := sqrt(maxf(0.0, upper_length * upper_length - along * along))
	var rest_upper: Vector2 = chain[1] - chain[0]
	var rest_span: Vector2 = chain[2] - chain[0]
	var bend := 1.0 if rest_span.cross(rest_upper) >= 0.0 else -1.0
	var knee := hip + forward * along + Vector2(-forward.y, forward.x) * altitude * bend
	# All locomotion links retain their painted rest lengths. A lifted target
	# outside the reachable circle is limited while the paw is in swing.
	var ankle_solved := hip + forward * distance
	if death > 0.0:
		# A corpse no longer solves a standing leg against the original fixed
		# ground target. Explicit low folded targets keep knees below the back.
		var is_front := "front" in limb
		var side_offset := 0.0 if "near" in limb else -9.0
		var dead_knee := Vector2(40.0 + side_offset, -6.0 if "near" in limb else -8.0) if is_front else Vector2(-12.0 + side_offset, -8.0)
		var dead_ankle := Vector2(57.0 + side_offset, -6.0 if "near" in limb else -8.0) if is_front else Vector2(-30.0 + side_offset, -6.0)
		var dead_toe := dead_ankle + Vector2(8.0 if is_front else 10.0, 4.0)
		knee = knee.lerp(dead_knee, death)
		ankle_solved = ankle_solved.lerp(dead_ankle, death)
		toe = toe.lerp(dead_toe, death)
	# A planted paw stays horizontal while the two-link leg follows the chest.
	# The toe offset is carried by the paw bone, with no detached foot sprite.
	var paw_angle: float = (toe - ankle_solved).angle() - (Vector2(chain[3]) - Vector2(chain[2])).angle()
	_pose_bone(limb + "_upper", hip, (knee - hip).angle() - rest_upper.angle())
	_pose_bone(limb + "_lower", knee, (ankle_solved - knee).angle() - (Vector2(chain[2]) - Vector2(chain[1])).angle())
	_pose_bone(limb + "_paw", ankle_solved, paw_angle)
	_last_local_ankles[limb] = ankle_solved
	if foot_contacts.has(limb) and str(foot_contacts[limb].mode) == "action":
		foot_contacts[limb].world_position = mirrored_root.to_global(ankle_solved)


func get_runtime_summary() -> Dictionary:
	return {"bone_count": bones.size(), "mesh_count": meshes.size(), "weighted_vertices": _weighted_vertices, "validation_errors": validation_errors.duplicate(), "state": current_state, "phase": gait_phase, "death_progress": death_progress, "source": source_path}


func get_gait_debug() -> Dictionary:
	return {"foot_contacts": foot_contacts.duplicate(true), "phase": gait_phase, "frequency": _gait_frequency,
		"stride_local": _gait_stride, "stance_ratio": _gait_duty, "trot_blend": _gait_trot_blend,
		"local_velocity": _gait_local_velocity, "global_scale": global_scale,
		"limbs": limb_pose_debug.duplicate(true)}


func _draw_debug_bones(canvas: Node2D) -> void:
	if not debug_bones or not _built:
		return
	for key in bones:
		var bone: Bone2D = bones[key]
		var point := canvas.to_local(bone.global_position)
		var parent_key: String = bone_parent[key]
		if not parent_key.is_empty():
			var parent_bone: Bone2D = bones[parent_key]
			canvas.draw_line(canvas.to_local(parent_bone.global_position), point, Color(0.25, 0.9, 0.95, 0.85), 0.7, true)
		canvas.draw_circle(point, 1.25, Color(1.0, 0.7, 0.25, 0.95), true, -1.0, true)
