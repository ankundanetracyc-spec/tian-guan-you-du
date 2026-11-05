extends RefCounted
## Bounded 3D port of the DynamicBone.cs shipped in the local game Mod SDK.
## Input/output dictionaries are character-root-local WORLD transforms, in Unity
## units. Actor motion is applied inside physics and removed from the output.
## This is the supplied algorithm + serialized male101 setup, not a claim that
## the original game's Animator, runtime overrides or native binary are restored.

var components: Array = []
var nodes: Array = []
var node_by_path: Dictionary = {}
var children: Dictionary = {}
var rest_world: Dictionary = {}
var setup_errors: Array[String] = []
var initialized := false
var weight := 1.0 # DynamicBone's actual private-field initializer.
var last_step_substeps := 0


func setup(source_config: Dictionary, source_nodes: Array) -> bool:
	components.clear()
	nodes.clear()
	node_by_path.clear()
	children.clear()
	rest_world.clear()
	setup_errors.clear()
	initialized = false
	for source_node in source_nodes:
		var node: Dictionary = source_node.duplicate(true)
		var path := str(node.get("path", ""))
		node["rest_local"] = _rest_local(node)
		nodes.append(node)
		node_by_path[path] = node
		children[path] = []
	for node in nodes:
		var parent := str(node.get("parentPath", "")) if node.get("parentPath") != null else ""
		if children.has(parent):
			children[parent].append(str(node.path))
		var local: Transform3D = node.rest_local
		rest_world[node.path] = (rest_world[parent] * local) if rest_world.has(parent) else local
	for source_component in source_config.get("components", [source_config]):
		var config: Dictionary = source_component.duplicate(true)
		if not node_by_path.has(str(config.get("rootPath", ""))):
			setup_errors.append("Missing source rootPath: " + str(config.get("rootPath")))
			continue
		# The two verified male101 components contain none of these branches.
		# Refuse unknown active behavior instead of silently inventing physics.
		if not config.get("colliders", []).is_empty():
			setup_errors.append("Collider records are not part of the selected male101 setup; supply collider geometry before enabling.")
		if bool(config.get("distantDisable", false)):
			setup_errors.append("Distant-disable camera/reference state is not captured in this source setup.")
		if int(config.get("updateMode", 0)) != 0:
			setup_errors.append("Selected source is Normal update; other Unity scheduling modes need a caller scheduling adapter.")
		for field in ["updateRate", "damping", "elasticity", "stiffness", "inert", "radius", "endLength", "endOffset", "gravity", "force", "freezeAxis"]:
			if not config.has(field):
				setup_errors.append("Missing serialized field: " + field)
		for key in ["damping", "elasticity", "stiffness", "inert", "radius"]:
			for curve_key in config.get(key + "Distrib", {}).get("keys", []):
				if int(curve_key.get("weightedMode", 0)) != 0:
					setup_errors.append("Weighted curve keys need a Unity weighted-Bezier adapter; selected source uses unweighted keys.")
		var state := {"config": config, "particles": [], "total_length": 0.0,
			"time": 0.0, "object_prev": Vector3.ZERO, "object_move": Vector3.ZERO,
			"object_scale": 1.0, "local_gravity": Vector3.ZERO}
		_append_particles(state, str(config.rootPath), -1, 0.0)
		var root: Transform3D = rest_world[config.rootPath]
		state.local_gravity = _rotation(root.basis).inverse() * _vec(config.gravity)
		for p in state.particles:
			var fraction := float(p.bone_length) / float(state.total_length) if float(state.total_length) > 0.0 else 0.0
			for key in ["damping", "elasticity", "stiffness", "inert", "radius"]:
				var value := float(config[key])
				var curve: Dictionary = config.get(key + "Distrib", {})
				if not curve.get("keys", []).is_empty():
					value *= evaluate_curve(curve, fraction)
				p[key] = maxf(value, 0.0) if key == "radius" else clampf(value, 0.0, 1.0)
		components.append(state)
	return setup_errors.is_empty()


func _rest_local(node: Dictionary) -> Transform3D:
	if node.get("rest_local") is Transform3D:
		return node.rest_local
	var trs: Dictionary = node.get("localTRS", {})
	var q: Array = trs.get("rotationQuaternionXYZW", [0.0, 0.0, 0.0, 1.0])
	var b := Basis(Quaternion(float(q[0]), float(q[1]), float(q[2]), float(q[3])).normalized())
	b = b.scaled_local(_vec(trs.get("scale", [1.0, 1.0, 1.0])))
	return Transform3D(b, _vec(trs.get("position", [0.0, 0.0, 0.0])))


func _append_particles(state: Dictionary, path: String, parent_index: int, bone_length: float) -> void:
	var end_offset := Vector3.ZERO
	var position: Vector3
	if not path.is_empty():
		position = (rest_world[path] as Transform3D).origin
	else:
		var parent: Dictionary = state.particles[parent_index]
		var parent_world: Transform3D = rest_world[parent.path]
		var parent_node: Dictionary = node_by_path[parent.path]
		var grand_path := str(parent_node.get("parentPath", ""))
		if float(state.config.endLength) > 0.0:
			if rest_world.has(grand_path):
				var reflected: Vector3 = parent_world.origin * 2.0 - (rest_world[grand_path] as Transform3D).origin
				end_offset = (parent_world.affine_inverse() * reflected) * float(state.config.endLength)
			else:
				end_offset = Vector3(float(state.config.endLength), 0.0, 0.0)
		else:
			var host: Transform3D = rest_world[state.config.componentPath]
			end_offset = parent_world.affine_inverse() * (_rotation(host.basis) * _vec(state.config.endOffset) + parent_world.origin)
		position = parent_world * end_offset
	if parent_index >= 0:
		bone_length += (state.particles[parent_index].position as Vector3).distance_to(position)
		state.total_length = maxf(float(state.total_length), bone_length)
	var index: int = state.particles.size()
	state.particles.append({"path": path, "parent_index": parent_index,
		"position": position, "prev_position": position, "end_offset": end_offset,
		"bone_length": bone_length})
	if path.is_empty():
		return
	var exclusions: Array = state.config.get("exclusionPaths", [])
	var has_end := float(state.config.endLength) > 0.0 or not _vec(state.config.endOffset).is_zero_approx()
	for child_path in children[path]:
		if not exclusions.has(child_path):
			_append_particles(state, child_path, index, bone_length)
		elif has_end:
			_append_particles(state, "", index, bone_length)
	if children[path].is_empty() and has_end:
		_append_particles(state, "", index, bone_length)


func reset(animated_world: Dictionary, actor_transform := Transform3D.IDENTITY) -> void:
	var world := _to_scene_world(animated_world, actor_transform)
	for state in components:
		for p in state.particles:
			var position: Vector3
			if not str(p.path).is_empty():
				position = (world[p.path] as Transform3D).origin
			else:
				var parent: Dictionary = state.particles[int(p.parent_index)]
				position = (world[parent.path] as Transform3D) * (p.end_offset as Vector3)
			p.position = position
			p.prev_position = position
		state.object_prev = (world[state.config.componentPath] as Transform3D).origin
		state.object_move = Vector3.ZERO
		# ResetParticlesPosition does not reset the source solver's time accumulator.
	initialized = true


func reframe_state(delta: Transform3D) -> void:
	# A visual facing change replaces the actor reference frame; it is not a
	# physical impulse. Move both Verlet samples together so internal velocity
	# and shape survive. The caller excludes real actor translation from delta.
	for state in components:
		for p in state.particles:
			p.position = delta * (p.position as Vector3)
			p.prev_position = delta * (p.prev_position as Vector3)
		state.object_prev = delta * (state.object_prev as Vector3)
		state.object_move = delta.basis * (state.object_move as Vector3)


func step(dt: float, animated_world: Dictionary, actor_transform := Transform3D.IDENTITY) -> Dictionary:
	if not setup_errors.is_empty():
		return animated_world.duplicate()
	if not initialized:
		reset(animated_world, actor_transform)
	var world := _to_scene_world(animated_world, actor_transform)
	last_step_substeps = 0
	for state in components:
		var c: Dictionary = state.config
		if not bool(c.get("enabled", true)) or not bool(c.get("isEnbale", true)) or weight <= 0.0:
			continue
		var host: Transform3D = world[c.componentPath]
		state.object_scale = host.basis.x.length()
		state.object_move = host.origin - (state.object_prev as Vector3)
		state.object_prev = host.origin
		var loops := 1
		if float(c.updateRate) > 0.0:
			var interval := 1.0 / float(c.updateRate)
			state.time = float(state.time) + maxf(dt, 0.0)
			loops = 0
			while float(state.time) >= interval:
				state.time = float(state.time) - interval
				loops += 1
				if loops >= 3: # Exact source backlog cap; no invented substep rate.
					state.time = 0.0
					break
		last_step_substeps += loops
		if loops > 0:
			for _iteration in range(loops):
				_update_particles_1(state, world)
				_update_particles_2(state, world, false)
				state.object_move = Vector3.ZERO
		else:
			_skip_update_particles(state, world)
		_apply_particles(state, world)
	var inverse_actor := actor_transform.affine_inverse()
	var output := {}
	for path in animated_world:
		output[path] = inverse_actor * (world[path] as Transform3D)
	return output


func _update_particles_1(state: Dictionary, world: Dictionary) -> void:
	var gravity := _vec(state.config.gravity)
	var force := gravity
	var force_direction := gravity.normalized()
	var root: Transform3D = world[state.config.rootPath]
	var rest_force := _rotation(root.basis) * (state.local_gravity as Vector3)
	force -= force_direction * maxf(rest_force.dot(force_direction), 0.0)
	force = (force + _vec(state.config.force)) * float(state.object_scale)
	for p in state.particles:
		var position: Vector3 = p.position
		if int(p.parent_index) >= 0:
			var velocity := position - (p.prev_position as Vector3)
			var inert_move: Vector3 = (state.object_move as Vector3) * float(p.inert)
			p.prev_position = position + inert_move
			p.position = position + velocity * (1.0 - float(p.damping)) + force + inert_move
		else:
			p.prev_position = position
			p.position = (world[p.path] as Transform3D).origin


func _rest_length(p: Dictionary, p0: Dictionary, world: Dictionary) -> float:
	var parent_world: Transform3D = world[p0.path]
	if not str(p.path).is_empty():
		return parent_world.origin.distance_to((world[p.path] as Transform3D).origin)
	return (parent_world.basis * (p.end_offset as Vector3)).length()


func _rest_position(p: Dictionary, p0: Dictionary, world: Dictionary) -> Vector3:
	var parent_world: Transform3D = world[p0.path]
	var local: Vector3 = p.end_offset
	if not str(p.path).is_empty():
		local = parent_world.affine_inverse() * (world[p.path] as Transform3D).origin
	parent_world.origin = p0.position
	return parent_world * local


func _keep_length(p: Dictionary, p0: Dictionary, rest_length: float) -> void:
	var difference: Vector3 = (p0.position as Vector3) - (p.position as Vector3)
	var length := difference.length()
	if length > 0.0:
		p.position = (p.position as Vector3) + difference * ((length - rest_length) / length)


func _update_particles_2(state: Dictionary, world: Dictionary, skip: bool) -> void:
	for i in range(1, state.particles.size()):
		var p: Dictionary = state.particles[i]
		var p0: Dictionary = state.particles[int(p.parent_index)]
		var rest_length := _rest_length(p, p0, world)
		var stiffness := lerpf(1.0, float(p.stiffness), weight)
		if stiffness > 0.0 or (not skip and float(p.elasticity) > 0.0):
			var rest_position := _rest_position(p, p0, world)
			var delta := rest_position - (p.position as Vector3)
			if not skip:
				p.position = (p.position as Vector3) + delta * float(p.elasticity)
			if stiffness > 0.0:
				delta = rest_position - (p.position as Vector3)
				var length := delta.length()
				var max_length := rest_length * (1.0 - stiffness) * 2.0
				if length > max_length:
					p.position = (p.position as Vector3) + delta * ((length - max_length) / length)
		# Selected source has no collider entries; nonempty entries fail setup.
		var freeze := int(state.config.freezeAxis)
		if not skip and freeze > 0:
			var parent_rotation := _rotation((world[p0.path] as Transform3D).basis)
			var normal: Vector3 = [parent_rotation.x, parent_rotation.y, parent_rotation.z][freeze - 1]
			p.position = (p.position as Vector3) - normal * ((p.position as Vector3) - (p0.position as Vector3)).dot(normal)
		_keep_length(p, p0, rest_length)


func _skip_update_particles(state: Dictionary, world: Dictionary) -> void:
	# Source interleaves root transport and child constraints, in DFS order.
	for i in range(state.particles.size()):
		var p: Dictionary = state.particles[i]
		if int(p.parent_index) < 0:
			p.prev_position = p.position
			p.position = (world[p.path] as Transform3D).origin
			continue
		p.prev_position = (p.prev_position as Vector3) + (state.object_move as Vector3)
		p.position = (p.position as Vector3) + (state.object_move as Vector3)
		var p0: Dictionary = state.particles[int(p.parent_index)]
		var rest_length := _rest_length(p, p0, world)
		var stiffness := lerpf(1.0, float(p.stiffness), weight)
		if stiffness > 0.0:
			var delta := _rest_position(p, p0, world) - (p.position as Vector3)
			var length := delta.length()
			var max_length := rest_length * (1.0 - stiffness) * 2.0
			if length > max_length:
				p.position = (p.position as Vector3) + delta * ((length - max_length) / length)
		_keep_length(p, p0, rest_length)


func _apply_particles(state: Dictionary, world: Dictionary) -> void:
	for i in range(1, state.particles.size()):
		var p: Dictionary = state.particles[i]
		var p0: Dictionary = state.particles[int(p.parent_index)]
		var parent_path := str(p0.path)
		var parent_world: Transform3D = world[parent_path]
		if children[parent_path].size() <= 1:
			var local: Vector3 = p.end_offset
			if not str(p.path).is_empty():
				local = parent_world.affine_inverse() * (world[p.path] as Transform3D).origin
			var from_direction: Vector3 = _rotation(parent_world.basis) * local
			var to_direction: Vector3 = (p.position as Vector3) - (p0.position as Vector3)
			if from_direction.length_squared() > 1e-16 and to_direction.length_squared() > 1e-16:
				var turn := Basis(Quaternion(from_direction.normalized(), to_direction.normalized()))
				_set_world(parent_path, Transform3D(turn * parent_world.basis, parent_world.origin), world)
		if not str(p.path).is_empty():
			var current: Transform3D = world[p.path]
			current.origin = p.position
			_set_world(str(p.path), current, world)


func _set_world(path: String, transform: Transform3D, world: Dictionary) -> void:
	# Emulate Unity Transform: changing a parent updates descendant world poses
	# while preserving their local transforms, including non-particle children.
	var delta := transform * (world[path] as Transform3D).affine_inverse()
	world[path] = transform
	_propagate_delta(path, delta, world)


func _propagate_delta(path: String, delta: Transform3D, world: Dictionary) -> void:
	for child_path in children[path]:
		world[child_path] = delta * (world[child_path] as Transform3D)
		_propagate_delta(child_path, delta, world)


func _to_scene_world(animated_world: Dictionary, actor: Transform3D) -> Dictionary:
	var world := {}
	for path in animated_world:
		world[path] = actor * (animated_world[path] as Transform3D)
	return world


func _rotation(basis: Basis) -> Basis:
	var rotation := basis.orthonormalized()
	# Unity TransformDirection ignores negative scale. Match a mirrored actor X.
	if rotation.determinant() < 0.0:
		rotation.x = -rotation.x
	return rotation


func _vec(value: Variant) -> Vector3:
	if value is Vector3:
		return value
	return Vector3(float(value[0]), float(value[1]), float(value[2]))


func evaluate_curve(curve: Dictionary, time: float) -> float:
	var keys: Array = curve.get("keys", [])
	if keys.is_empty():
		return 1.0
	if time <= float(keys[0].time):
		return float(keys[0].value)
	if time >= float(keys[-1].time):
		return float(keys[-1].value)
	for i in range(keys.size() - 1):
		var a: Dictionary = keys[i]
		var b: Dictionary = keys[i + 1]
		if time > float(b.time):
			continue
		var span := float(b.time) - float(a.time)
		var t := (time - float(a.time)) / span
		# All extracted male101 keys have weightedMode 0. Preserve Unity's cubic
		# Hermite slopes (not linear interpolation: stiffness starts with slope 2).
		var t2 := t * t
		var t3 := t2 * t
		return (2.0 * t3 - 3.0 * t2 + 1.0) * float(a.value) + (t3 - 2.0 * t2 + t) * span * float(a.outTangent) + (-2.0 * t3 + 3.0 * t2) * float(b.value) + (t3 - t2) * span * float(b.inTangent)
	return float(keys[-1].value)


func get_diagnostics() -> Dictionary:
	var summary: Array = []
	for state in components:
		var virtual_count := 0
		for p in state.particles:
			if str(p.path).is_empty():
				virtual_count += 1
		summary.append({"rootPath": state.config.rootPath, "particles": state.particles.size(),
			"virtualEndParticles": virtual_count, "sourceTotalLength": state.total_length})
	return {"setupErrors": setup_errors.duplicate(), "components": summary,
		"coordinateSystem": "full 3D Unity units before XY projection",
		"unsupportedActiveBranches": ["nonempty collider geometry", "camera distant-disable", "non-Normal Unity scheduling", "weighted AnimationCurve keys"],
		"algorithmIdentityLimit": "Installed Mod SDK source port; native instruction identity and original gameplay state not established."}
