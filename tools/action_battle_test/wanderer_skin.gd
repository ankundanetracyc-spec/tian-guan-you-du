extends RefCounted

## The original art has its own UVs, rest-space meshes and normalized weights.
## Source clips drive the body/legs. Visual arm chains fit the painted shoulders
## and keep bent elbows and neutral wrists during locomotion.
## The two supplementary cloth chains sample that same source skirt curve with
## separate response/phase; they do not replace the source locomotion animation.

const BODY := "/10002/Root/Body/yifu/yifu/"
const CLOTH_PATHS := [BODY + "shenti2", BODY + "shenti2/shenti2_1", BODY + "shenti2/shenti2_1/shenti2_2"]
const COLUMNS := 6
const ROWS := 40

var runtime: Node2D
var meshes: Array[Polygon2D] = []
var parts: Dictionary = {}
var cloth_chains: Dictionary = {}
var leg_chains: Dictionary = {}
var arm_chains: Dictionary = {}
var source_texture: Texture2D
var arm_texture: Texture2D
var running_arm_texture: Texture2D
var display_scale := 0.78
var validation_errors: Array[String] = []


func build(owner_runtime: Node2D, art_dir: String) -> bool:
	runtime = owner_runtime
	var manifest: Variant = runtime._read_json(art_dir.path_join("skin_v2.json"))
	if not manifest is Dictionary:
		return false
	parts = manifest.parts
	display_scale = float(manifest.displayScale)
	var source_image := Image.new()
	if source_image.load(ProjectSettings.globalize_path(art_dir.path_join(str(manifest.source)))) != OK:
		return false
	# This sheet shrinks more than tenfold in the arena. Mipmaps prevent tiny
	# cloud strokes and alpha edges from flickering as the original rig moves.
	source_image.generate_mipmaps()
	source_texture = ImageTexture.create_from_image(source_image)
	if manifest.has("armSource"):
		var arm_image := Image.new()
		if arm_image.load(ProjectSettings.globalize_path(art_dir.path_join(str(manifest.armSource)))) != OK:
			return false
		arm_image.generate_mipmaps()
		arm_texture = ImageTexture.create_from_image(arm_image)
	if manifest.has("runningArmSource"):
		var running_image := Image.new()
		if running_image.load(ProjectSettings.globalize_path(art_dir.path_join(str(manifest.runningArmSource)))) != OK:
			return false
		running_image.generate_mipmaps()
		running_arm_texture = ImageTexture.create_from_image(running_image)
	_build_rigid("head", BODY + "shenti1/tou")
	_build_rigid("torso", BODY + "shenti1")
	_build_limb("far_arm", BODY + "shenti1/zuobi/zuobi/zuobi1", "zuobi2", "zuobi3", false)
	_build_limb("near_arm", BODY + "shenti1/youbi/youbi/youbi1", "youbi2", "youbi3", false)
	_build_limb("far_leg", "/10002/Root/Body/Foot/youtui/youtui/tui1", "tui2", "tui3", true)
	_build_limb("near_leg", "/10002/Root/Body/Foot/zuotui/zuotui/tui1", "tui2", "tui3", true)
	_build_cloth("back_panel")
	_build_cloth("front_panel")
	return meshes.size() == 8 and validation_errors.is_empty()


func _rest_point(path: String) -> Vector2:
	return runtime.rig_root_node.to_local(runtime.node_by_rig_path[path].global_position)


func _build_rigid(part_name: String, bone_path: String) -> void:
	var bounds: Array = parts[part_name].bounds
	var vertices := PackedVector2Array()
	for row in range(ROWS + 1):
		for column in range(COLUMNS + 1):
			vertices.append(Vector2(float(bounds[0]) + float(bounds[2]) * column / COLUMNS, float(bounds[1]) + float(bounds[3]) * row / ROWS))
	var weights := PackedFloat32Array()
	weights.resize(vertices.size())
	weights.fill(1.0)
	_create_mesh(part_name, vertices, [runtime.node_by_rig_path[bone_path]], [weights])


func _build_limb(part_name: String, path: String, middle_name: String, end_name: String, is_leg: bool) -> void:
	var config: Dictionary = parts[part_name]
	var middle_path := path + "/" + middle_name
	var end_path := middle_path + "/" + end_name
	var points: Array[Vector2] = [_rest_point(path), _rest_point(middle_path), _rest_point(end_path)]
	# Fit the visual chain to the shoulder landmark measured in the torso
	# image's rest-space mapping, leaving the imported source bones intact.
	var shoulder_offset := Vector2.ZERO
	if not is_leg:
		var upper_direction := (points[1] - points[0]).normalized()
		var forearm_direction := (points[2] - points[1]).normalized()
		points[1] = points[0] + upper_direction * float(config.get("upperLength", points[0].distance_to(points[1])))
		points[2] = points[1] + forearm_direction * float(config.get("forearmLength", points[1].distance_to(points[2])))
		var shoulder_point: Array = config.get("shoulderPoint", [-2.5, -80.0])
		shoulder_offset = Vector2(float(shoulder_point[0]), float(shoulder_point[1])) - points[0]
		for index in range(points.size()):
			points[index] += shoulder_offset
	# The last source bone carries the wrist/hand as well as the ankle/boot.
	var end_direction := Vector2.DOWN if is_leg else (points[2] - points[1]).normalized()
	points.append(points[2] + end_direction * (4.0 if is_leg else float(config.get("handLength", 4.5))))
	var joints: Array = config.joints
	var vertices := PackedVector2Array()
	var weight_sets := [PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array()]
	for row in range(ROWS + 1):
		var v := float(row) / ROWS
		var section := 0 if v <= float(joints[1]) else (1 if v <= float(joints[2]) else 2)
		var t := (v - float(joints[section])) / (float(joints[section + 1]) - float(joints[section]))
		var center: Vector2 = points[section].lerp(points[section + 1], t)
		var direction: Vector2 = (points[section + 1] - points[section]).normalized()
		# A continuous normal at each joint avoids the old independent sleeve gaps.
		for joint in [1, 2]:
			var blend := 0.065 if joint == 1 else 0.035
			if absf(v - float(joints[joint])) < blend:
				var before: Vector2 = (points[joint] - points[joint - 1]).normalized()
				var after: Vector2 = (points[joint + 1] - points[joint]).normalized()
				direction = before.lerp(after, smoothstep(float(joints[joint]) - blend, float(joints[joint]) + blend, v)).normalized()
		var normal := Vector2(direction.y, -direction.x)
		var lower_share := smoothstep(float(joints[1]) - 0.065, float(joints[1]) + 0.065, v)
		var end_share := smoothstep(float(joints[2]) - 0.035, float(joints[2]) + 0.035, v)
		# Follow the painted cuff centre. A single sheet-wide axis put the wrist
		# to one side of its bone, exaggerating the kink when the source hand turns.
		var axis := float(config.axis)
		if config.has("jointAxes"):
			axis = lerpf(float(config.jointAxes[section]), float(config.jointAxes[section + 1]), t)
		for column in range(COLUMNS + 1):
			var u := float(column) / COLUMNS
			var palm_width := lerpf(1.0, float(config.get("palmWidthScale", 1.0)), smoothstep(float(joints[2]), float(joints[2]) + 0.06, v)) if not is_leg else 1.0
			var vertex := center + normal * (u - axis) * float(config.width) * palm_width
			if part_name == "far_arm":
				# Tuck the painted cap under the shoulder; retain the forearm position.
				vertex.x += 2.0 * (1.0 - smoothstep(0.08, 0.36, v))
			vertices.append(vertex)
			# Anchor the sleeve cap to the torso and blend into the animated arm.
			# Otherwise the shoulder opens a visible slit when the arm swings back.
			var torso_share := (1.0 - smoothstep(0.015, 0.12, v)) if not is_leg else 0.0
			weight_sets[0].append((1.0 - lower_share) * (1.0 - torso_share))
			weight_sets[1].append(lower_share * (1.0 - end_share) * (1.0 - torso_share))
			weight_sets[2].append(lower_share * end_share * (1.0 - torso_share))
			weight_sets[3].append(torso_share)
	var mesh_bones: Array = [runtime.node_by_rig_path[path], runtime.node_by_rig_path[middle_path], runtime.node_by_rig_path[end_path], runtime.node_by_rig_path[BODY + "shenti1"]]
	var parent: Node = mesh_bones[0].get_parent()
	var chain: Array[Bone2D] = []
	for source in mesh_bones.slice(0, 3):
		var bone := Bone2D.new()
		bone.name = "Wanderer_%s_%d" % [part_name, chain.size()]
		bone.set_autocalculate_length_and_angle(false)
		bone.length = 0.0
		bone.transform = source.transform
		bone.rest = bone.transform
		parent.add_child(bone)
		if not is_leg and chain.is_empty():
			bone.position += parent.global_transform.basis_xform_inv(runtime.rig_root_node.global_transform.basis_xform(shoulder_offset))
			bone.rest = bone.transform
		elif not is_leg:
			bone.global_position = runtime.rig_root_node.to_global(points[chain.size()])
			bone.rest = bone.transform
		chain.append(bone)
		parent = bone
	if is_leg:
		leg_chains[part_name] = {"bones": chain, "paths": [path, middle_path, end_path]}
		mesh_bones = chain
		weight_sets.resize(3)
	else:
		var torso: Node2D = runtime.node_by_rig_path[BODY + "shenti1"]
		arm_chains[part_name] = {"bones": chain, "paths": [path, middle_path, end_path], "shoulder": torso.to_local(chain[0].global_position)}
		mesh_bones = [chain[0], chain[1], chain[2], torso]
	_create_mesh(part_name, vertices, mesh_bones, weight_sets)


func _build_cloth(part_name: String) -> void:
	var chain: Array[Bone2D] = []
	var parent: Node = runtime.node_by_rig_path[CLOTH_PATHS[0]].get_parent()
	for index in range(CLOTH_PATHS.size()):
		var bone := Bone2D.new()
		bone.name = "Wanderer_%s_%d" % [part_name, index]
		bone.set_autocalculate_length_and_angle(false)
		bone.length = 0.0
		bone.transform = runtime.rest_by_rig_path[CLOTH_PATHS[index]]
		bone.rest = bone.transform
		parent.add_child(bone)
		chain.append(bone)
		parent = bone
	cloth_chains[part_name] = chain
	var bounds: Array = parts[part_name].bounds
	var vertices := PackedVector2Array()
	var weight_sets := [PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array()]
	for row in range(ROWS + 1):
		var v := float(row) / ROWS
		var middle_share := smoothstep(0.22, 0.66, v)
		var end_share := smoothstep(0.68, 1.0, v)
		# The top of each panel is sewn to the moving torso, not to the
		# independently lagging skirt chain. Blend the attachment into the
		# cloth below the sash to keep both the seam and flowing hem continuous.
		var torso_share := 1.0 - smoothstep(0.08, 0.30, v)
		# The painted panel narrows at its waist. Compensate in the UV-to-rest
		# mapping so both waist edges overlap beneath the same torso sash.
		var canvas_width := lerpf(float(parts[part_name].waistCanvasWidth), float(bounds[2]), v)
		var center_x := float(bounds[0]) + float(bounds[2]) * 0.5
		for column in range(COLUMNS + 1):
			vertices.append(Vector2(center_x + (float(column) / COLUMNS - 0.5) * canvas_width, float(bounds[1]) + float(bounds[3]) * v))
			weight_sets[0].append((1.0 - middle_share) * (1.0 - torso_share))
			weight_sets[1].append(middle_share * (1.0 - end_share) * (1.0 - torso_share))
			weight_sets[2].append(middle_share * end_share * (1.0 - torso_share))
			weight_sets[3].append(torso_share)
	var cloth_bones: Array = chain.duplicate()
	cloth_bones.append(runtime.node_by_rig_path[BODY + "shenti1"])
	_create_mesh(part_name, vertices, cloth_bones, weight_sets)


func _create_mesh(part_name: String, vertices: PackedVector2Array, bones: Array, weight_sets: Array) -> void:
	var mesh := Polygon2D.new()
	mesh.name = "WandererSkin_" + part_name
	mesh.polygon = vertices
	mesh.texture = source_texture
	if part_name in ["far_arm", "near_arm"] and arm_texture != null:
		mesh.texture = arm_texture
	mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	var detail_shader := Shader.new()
	detail_shader.code = "shader_type canvas_item; varying vec4 tint; void vertex() { tint = COLOR; } void fragment() { COLOR = tint * texture(TEXTURE, UV, -0.65); }"
	var detail_material := ShaderMaterial.new()
	detail_material.shader = detail_shader
	mesh.material = detail_material
	mesh.texture_repeat = CanvasItem.TEXTURE_REPEAT_DISABLED
	mesh.z_index = int(parts[part_name].layer)
	var region: Array = parts[part_name].region
	var uv := PackedVector2Array()
	var triangles: Array = []
	for row in range(ROWS + 1):
		for column in range(COLUMNS + 1):
			uv.append(Vector2(float(region[0]) + float(region[2]) * column / COLUMNS, float(region[1]) + float(region[3]) * row / ROWS))
			# Keep the complete hip-to-toe surface. Clothing occludes the leg;
			# removing thigh/calf triangles leaves detached boots during motion.
			if row < ROWS and column < COLUMNS:
				var a := row * (COLUMNS + 1) + column
				var b := a + COLUMNS + 1
				triangles.append(PackedInt32Array([a, a + 1, b]))
				triangles.append(PackedInt32Array([a + 1, b + 1, b]))
	mesh.uv = uv
	if part_name in ["near_arm", "far_arm"]:
		mesh.set_meta("open_hand_uv", uv)
		var running_uv := uv.duplicate()
		var wrist_y := float(region[1]) + float(region[3]) * float(parts[part_name].joints[2])
		for vertex_index in range(running_uv.size()):
			if running_uv[vertex_index].y > wrist_y:
				running_uv[vertex_index].y = lerpf(wrist_y, 940.0, (running_uv[vertex_index].y - wrist_y) / (float(region[1]) + float(region[3]) - wrist_y))
		mesh.set_meta("running_hand_uv", running_uv)
	mesh.polygons = triangles
	runtime.rig_root_node.add_child(mesh)
	mesh.skeleton = mesh.get_path_to(runtime.skeleton)
	for bone_index in range(bones.size()):
		var weights: PackedFloat32Array = weight_sets[bone_index]
		if weights.count(0.0) < weights.size():
			mesh.add_bone(runtime.skeleton.get_path_to(bones[bone_index]), weights)
	if part_name == "near_arm" and bool(parts[part_name].get("hideShoulderCap", false)):
		# Hide the rounded sleeve cap behind the body while keeping the moving
		# forearm in front. Both pieces retain the same vertices and weights.
		var shoulder := mesh.duplicate() as Polygon2D
		shoulder.name = "ShoulderBehindTorso"
		mesh.add_child(shoulder)
		shoulder.z_as_relative = false
		shoulder.z_index = int(parts.torso.layer) - 1
		shoulder.skeleton = shoulder.get_path_to(runtime.skeleton)
		var split := COLUMNS * 2 * 7
		shoulder.polygons = triangles.slice(0, split)
		mesh.polygons = triangles.slice(split)
	for vertex in range(vertices.size()):
		var total := 0.0
		for weights in weight_sets:
			total += weights[vertex]
		if not is_equal_approx(total, 1.0):
			validation_errors.append("%s vertex %d has total weight %f" % [part_name, vertex, total])
	meshes.append(mesh)


func set_visible(value: bool) -> void:
	for mesh in meshes:
		mesh.visible = value


func update_cloth(clip_name: String, sample_time: float, duration: float, clip_tracks: Dictionary) -> void:
	_update_arms(clip_name)
	# Keep the source gait timing while fitting its large trouser stride beneath
	# the long robe. This affects only this skin and leaves the source clips intact.
	for part_name in leg_chains:
		var leg_chain: Dictionary = leg_chains[part_name]
		for index in range(3):
			var path: String = leg_chain.paths[index]
			var leg: Bone2D = leg_chain.bones[index]
			var rest: Transform2D = runtime.rest_by_rig_path[path]
			leg.transform = runtime.node_by_rig_path[path].transform
			if clip_name == "Run_New1" and index < 2:
				leg.rotation = rest.get_rotation() + wrapf(leg.rotation - rest.get_rotation(), -PI, PI) * (0.72 if index == 0 else 0.90)
	for part_name in cloth_chains:
		var config: Dictionary = parts[part_name]
		var time := sample_time
		if clip_name == "Run_New1" and duration > 0.0:
			time = fposmod(time - float(config.phaseLag), duration)
		var chain: Array = cloth_chains[part_name]
		for index in range(chain.size()):
			var source_path: String = CLOTH_PATHS[index]
			var base: Transform2D = runtime.rest_by_rig_path[source_path]
			var pose: Transform2D = runtime._sample_property_curves(clip_tracks.get(source_path, {}), time, base)
			var bone: Bone2D = chain[index]
			bone.transform = pose
			# Distribute the source waist swing down the hem instead of rotating
			# the entire long panel as a board. No synthesized gait/sine is used.
			var drive_time := time
			if clip_name == "Run_New1" and duration > 0.0:
				drive_time = fposmod(time - index * 0.018, duration)
			var waist_rest: Transform2D = runtime.rest_by_rig_path[CLOTH_PATHS[0]]
			var waist_pose: Transform2D = runtime._sample_property_curves(clip_tracks.get(CLOTH_PATHS[0], {}), drive_time, waist_rest)
			var waist_delta := wrapf(waist_pose.get_rotation() - waist_rest.get_rotation(), -PI, PI)
			var distribution := [0.18, 0.60, 0.38]
			var local_delta := wrapf(pose.get_rotation() - base.get_rotation(), -PI, PI) if index > 0 else 0.0
			# The source skirt has only a waist rotation. Let the moving thigh
			# carry the loose lower fabric with a small delayed, distributed bend.
			var leg_side := "zuotui" if part_name == "front_panel" else "youtui"
			var leg_path := "/10002/Root/Body/Foot/%s/%s/tui1" % [leg_side, leg_side]
			var leg_rest: Transform2D = runtime.rest_by_rig_path[leg_path]
			var leg_pose: Transform2D = runtime._sample_property_curves(clip_tracks.get(leg_path, {}), drive_time, leg_rest)
			var leg_delta := wrapf(leg_pose.get_rotation() - leg_rest.get_rotation(), -PI, PI)
			if clip_name == "BackstepWalk":
				leg_delta = wrapf(runtime.node_by_rig_path[leg_path].rotation - leg_rest.get_rotation(), -PI, PI)
			var leg_influence: float = [0.02, 0.16, 0.13][index] if clip_name in ["Run_New1", "Step1", "BackstepWalk"] else 0.0
			bone.rotation = base.get_rotation() + (local_delta + waist_delta * float(distribution[index]) + leg_delta * float(leg_influence)) * float(config.rotationResponse)
		if part_name == "front_panel":
			# The front of a long robe is carried forward by the leading knee.
			# Both full legs remain underneath. Translate the middle cloth bone
			# in character space (its child inherits the displacement), while the
			# torso-weighted waist stays sewn to the sash. This creates a draped
			# bend instead of letting the knee walk through a rearward-only panel.
			var leading_knee := 0.0
			for leg_name in leg_chains:
				var knee: Bone2D = leg_chains[leg_name].bones[1]
				leading_knee = maxf(leading_knee, runtime.rig_root_node.to_local(knee.global_position).x - 4.0)
			var middle: Bone2D = chain[1]
			middle.global_position += runtime.rig_root_node.global_transform.basis_xform(Vector2(leading_knee * 1.05, 0.0))


func _point_arm_bone(bone: Bone2D, child: Bone2D, direction: Vector2) -> void:
	var local_direction: Vector2 = bone.get_parent().global_transform.basis_xform_inv(runtime.rig_root_node.global_transform.basis_xform(direction))
	bone.rotation = local_direction.angle() - child.position.angle()


func _update_arms(clip_name: String) -> void:
	var locomotion := clip_name in ["Run_New1", "Run_New2", "BackstepWalk"]
	var torso: Node2D = runtime.node_by_rig_path[BODY + "shenti1"]
	for part_name in arm_chains:
		var data: Dictionary = arm_chains[part_name]
		var chain: Array = data.bones
		for index in range(3):
			chain[index].transform = runtime.node_by_rig_path[data.paths[index]].transform
			if index > 0:
				chain[index].position = chain[index].rest.origin
		chain[0].global_position = torso.to_global(data.shoulder)
		if not locomotion:
			continue
		var side := "zuotui" if part_name == "near_arm" else "youtui"
		var leg_path := "/10002/Root/Body/Foot/%s/%s/tui1" % [side, side]
		var leg_delta := wrapf(runtime.node_by_rig_path[leg_path].rotation - runtime.rest_by_rig_path[leg_path].get_rotation(), -PI, PI)
		# Keep the source curve continuous; clipping the hip angle created
		# plateaus where both hands looked as if they only translated.
		var swing := sin(leg_delta)
		var upper_angle := -0.20 + 1.05 * swing
		var forearm_angle := 1.20 + 0.75 * swing
		if clip_name == "BackstepWalk":
			upper_angle = -0.12 + 0.55 * swing
			forearm_angle = 0.85 + 0.20 * swing
		_point_arm_bone(chain[0], chain[1], Vector2(sin(upper_angle), cos(upper_angle)))
		_point_arm_bone(chain[1], chain[2], Vector2(sin(forearm_angle), cos(forearm_angle)))
		# A running wrist continues the forearm, rather than inheriting the
		# source's upright open-palm gesture. The mesh remains sewn to its cuff.
		chain[2].rotation = runtime.rest_by_rig_path[data.paths[2]].get_rotation()
	for mesh in meshes:
		if str(mesh.name) in ["WandererSkin_far_arm", "WandererSkin_near_arm"]:
			mesh.texture = running_arm_texture if locomotion and running_arm_texture != null else arm_texture
			mesh.uv = mesh.get_meta("running_hand_uv" if locomotion and running_arm_texture != null else "open_hand_uv")


func summary() -> Dictionary:
	return {"meshes": meshes.size(), "cloth_chains": cloth_chains.size(), "arm_chains": arm_chains.size(), "vertices_per_mesh": (COLUMNS + 1) * (ROWS + 1), "display_scale": display_scale, "errors": validation_errors.duplicate()}


