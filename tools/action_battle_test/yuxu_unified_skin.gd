extends RefCounted

## A single seven-head anatomical ruler drives every rest landmark. These are
## original meshes/UVs; the imported rig is only a read-only motion reference.
const BODY := "/10002/Root/Body/yifu/yifu/"
const ROWS := 56
const COLUMNS := 8
var runtime: Node2D
var config: Dictionary
var meshes: Array[Polygon2D] = []
var bones: Dictionary = {}
var arm_chains: Dictionary = {}
var leg_chains: Dictionary = {}
var cloth_chains: Dictionary = {}
var validation_errors: Array[String] = []
var display_scale := 0.72
var texture: Texture2D
var mesh_records: Array[Dictionary] = []
var pose_landmarks: Dictionary = {}
var pose_blending := true
var last_clip := ""
var blend_started := 0
var blend_from: Dictionary = {}

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	runtime = owner_runtime
	config = runtime._read_json(art_dir.path_join("rig.json"))
	display_scale = float(config.displayScale)
	var image := Image.new()
	if image.load(ProjectSettings.globalize_path(art_dir.path_join(config.source))) != OK:
		return false
	image.generate_mipmaps()
	texture = ImageTexture.create_from_image(image)
	_new_bone("pelvis", "", Vector2(0, -56))
	_new_bone("torso", "pelvis", Vector2(0, -66))
	_new_bone("head", "torso", Vector2(0, -94))
	for side in ["far", "near"]:
		var shoulder := _landmark(side + "_shoulder")
		var arm_lengths: Array = config.armLengths
		_new_bone(side + "_upper", "torso", shoulder)
		_new_bone(side + "_forearm", side + "_upper", shoulder + Vector2(0, float(arm_lengths[0])))
		_new_bone(side + "_hand", side + "_forearm", shoulder + Vector2(0, float(arm_lengths[0]) + float(arm_lengths[1])))
		arm_chains[side] = [bones[side + "_upper"], bones[side + "_forearm"], bones[side + "_hand"]]
		var hip := Vector2(-1.0 if side == "near" else 1.0, -56)
		_new_bone(side + "_thigh", "pelvis", hip)
		_new_bone(side + "_calf", side + "_thigh", hip + Vector2(0, 27))
		_new_bone(side + "_foot", side + "_calf", hip + Vector2(0, 52))
		leg_chains[side] = [bones[side + "_thigh"], bones[side + "_calf"], bones[side + "_foot"]]
	_new_bone("cloth_middle", "pelvis", Vector2(0, -36))
	_new_bone("cloth_hem", "cloth_middle", Vector2(0, -12))
	_new_bone("back_middle", "pelvis", Vector2(0, -36))
	_new_bone("back_hem", "back_middle", Vector2(0, -12))
	cloth_chains = {"front": [bones.cloth_middle, bones.cloth_hem], "back": [bones.back_middle, bones.back_hem]}
	_build_head()
	_build_robe("robe")
	_build_robe("back_panel")
	for side in ["far", "near"]:
		_build_limb(side, false)
		_build_limb(side, true)
	return validation_errors.is_empty() and meshes.size() == 7

func _landmark(key: String) -> Vector2:
	return Vector2(float(config.landmarks[key][0]), float(config.landmarks[key][1]))

func _new_bone(key: String, parent_key: String, point: Vector2) -> void:
	var bone := Bone2D.new()
	bone.name = "Yuxu7H_" + key
	bone.set_autocalculate_length_and_angle(false)
	bone.length = 0.0
	var parent: Node2D = runtime.rig_root_node if parent_key.is_empty() else bones[parent_key]
	parent.add_child(bone)
	bone.global_position = runtime.rig_root_node.to_global(point)
	bone.rest = bone.transform
	bones[key] = bone

func _interpolate(value: float, xs: Array, ys: Array) -> float:
	var section := 0
	while section < xs.size() - 2 and value > float(xs[section + 1]):
		section += 1
	return lerpf(float(ys[section]), float(ys[section + 1]), (value - float(xs[section])) / (float(xs[section + 1]) - float(xs[section])))

func _build_head() -> void:
	var part: Dictionary = config.parts.head
	var region: Array = part.region
	var scale_factor := float(config.headUnit) / (float(part.sourceChin) - float(part.sourceCrown))
	var vertices := PackedVector2Array()
	for row in range(ROWS + 1):
		for column in range(COLUMNS + 1):
			var uv := Vector2(float(region[0]) + float(region[2]) * column / COLUMNS, float(region[1]) + float(region[3]) * row / ROWS)
			vertices.append(Vector2((uv.x - float(part.sourceNeckX)) * scale_factor, -112.0 + (uv.y - float(part.sourceCrown)) * scale_factor))
	var weights := PackedFloat32Array()
	weights.resize(vertices.size())
	weights.fill(1.0)
	_create_mesh("head", region, vertices, ["head"], [weights], int(part.layer))

func _build_robe(name: String) -> void:
	var part: Dictionary = config.parts[name]
	var region: Array = part.region
	var is_back := name == "back_panel"
	var vertices := PackedVector2Array()
	var uv_rows := PackedVector2Array()
	var sets := [PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array()]
	for row in range(ROWS + 1):
		var source_y := float(region[1]) + float(region[3]) * row / ROWS
		var y := _interpolate(source_y, part.sourceRows, part.targetRows)
		var cloth := smoothstep(-64.0, -31.0, y)
		var hem := smoothstep(-32.0, -9.0, y)
		var torso := 1.0 - smoothstep(-70.0, -57.0, y)
		# The isolated head sits beside the upper robe on the atlas. Follow
		# the garment's own row envelope so no neighbouring face enters its UVs.
		var left := float(region[0]) if is_back else lerpf(700.0, float(region[0]), smoothstep(410.0, 850.0, source_y))
		var right := float(region[0]) + float(region[2])
		for column in range(COLUMNS + 1):
			var source_x := lerpf(left, right, float(column) / COLUMNS)
			uv_rows.append(Vector2(source_x, source_y))
			var chest_width := 1.0 if is_back else lerpf(float(config.chestWidth), 1.0, smoothstep(-72.0, -48.0, y))
			vertices.append(Vector2((source_x - float(part.sourceAxis)) * float(part.widthScale) * chest_width, y))
			sets[0].append(torso)
			sets[1].append((1.0 - torso) * (1.0 - cloth))
			sets[2].append((1.0 - torso) * cloth * (1.0 - hem))
			sets[3].append((1.0 - torso) * cloth * hem)
	_create_mesh(name, region, vertices, ["torso", "pelvis", "back_middle" if is_back else "cloth_middle", "back_hem" if is_back else "cloth_hem"], sets, int(part.layer), uv_rows)

func _build_limb(side: String, is_leg: bool) -> void:
	var part: Dictionary = config.parts.leg if is_leg else config.parts.arm
	var region: Array = part.region
	var start := Vector2(-1.0 if side == "near" else 1.0, -56.0) if is_leg else _landmark(side + "_shoulder")
	var arm_lengths: Array = config.armLengths
	var lengths := [0.0, 27.0, 52.0, 56.0] if is_leg else [0.0, float(arm_lengths[0]), float(arm_lengths[0]) + float(arm_lengths[1]), float(arm_lengths[0]) + float(arm_lengths[1]) + float(arm_lengths[2])]
	var keys := [side + "_thigh", side + "_calf", side + "_foot"] if is_leg else [side + "_upper", side + "_forearm", side + "_hand"]
	if not is_leg:
		keys.append("torso")
	var sets: Array = []
	for key in keys:
		sets.append(PackedFloat32Array())
	var vertices := PackedVector2Array()
	for row in range(ROWS + 1):
		var source_y := float(region[1]) + float(region[3]) * row / ROWS
		var y := _interpolate(source_y, part.jointY, lengths)
		var axis := _interpolate(source_y, part.jointY, part.jointX)
		var lower := smoothstep(float(lengths[1]) - 3.0, float(lengths[1]) + 3.0, y)
		var end := smoothstep(float(lengths[2]) - 1.5, float(lengths[2]) + 1.5, y)
		var torso := 0.0 if is_leg else 1.0 - smoothstep(-3.5, 2.5, y)
		for column in range(COLUMNS + 1):
			var source_x := float(region[0]) + float(region[2]) * column / COLUMNS
			vertices.append(start + Vector2((source_x - axis) * float(part.width) / float(region[2]), y))
			sets[0].append((1.0 - lower) * (1.0 - torso))
			sets[1].append(lower * (1.0 - end) * (1.0 - torso))
			sets[2].append(lower * end * (1.0 - torso))
			if not is_leg:
				sets[3].append(torso)
	var layer := (35 if side == "near" else 30) if is_leg else (70 if side == "near" else 20)
	_create_mesh(side + ("_leg" if is_leg else "_arm"), region, vertices, keys, sets, layer)

func _create_mesh(name: String, region: Array, vertices: PackedVector2Array, keys: Array, sets: Array, layer: int, custom_uv := PackedVector2Array()) -> void:
	var mesh := Polygon2D.new()
	mesh.name = "YuxuUnified_" + name
	mesh.polygon = vertices
	mesh.texture = texture
	mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	mesh.z_index = layer
	var uvs := PackedVector2Array()
	var triangles: Array = []
	for row in range(ROWS + 1):
		for column in range(COLUMNS + 1):
			uvs.append(Vector2(float(region[0]) + float(region[2]) * column / COLUMNS, float(region[1]) + float(region[3]) * row / ROWS))
			if row < ROWS and column < COLUMNS:
				var a := row * (COLUMNS + 1) + column
				var b := a + COLUMNS + 1
				triangles.append(PackedInt32Array([a, a+1, b]))
				triangles.append(PackedInt32Array([a+1, b+1, b]))
	mesh.uv = custom_uv if not custom_uv.is_empty() else uvs
	mesh.polygons = triangles
	runtime.rig_root_node.add_child(mesh)
	mesh.skeleton = mesh.get_path_to(runtime.skeleton)
	var rest_transforms: Array = []
	for i in range(keys.size()):
		mesh.add_bone(runtime.skeleton.get_path_to(bones[keys[i]]), sets[i])
		rest_transforms.append(runtime.rig_root_node.global_transform.affine_inverse() * bones[keys[i]].global_transform)
	for vertex in range(vertices.size()):
		var sum := 0.0
		for weights in sets:
			sum += weights[vertex]
		if not is_equal_approx(sum, 1.0):
			validation_errors.append(name + " weights not normalized")
	meshes.append(mesh)
	mesh_records.append({"name": name, "mesh": mesh, "keys": keys, "weights": sets, "rest": rest_transforms})

func _source_delta(path: String) -> float:
	return wrapf(runtime.node_by_rig_path[path].rotation - runtime.rest_by_rig_path[path].get_rotation(), -PI, PI)

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	if pose_blending and clip != last_clip:
		blend_from.clear()
		if not last_clip.is_empty():
			for key in bones:
				blend_from[key] = bones[key].transform
		blend_started = Time.get_ticks_msec()
	last_clip = clip
	for bone in bones.values():
		bone.transform = bone.rest
	if clip == "BindPose":
		return
	var running := clip in ["Run_New1", "Run_New2"]
	var backward := clip == "BackstepWalk"
	var torso_delta := _source_delta(BODY + "shenti1")
	bones.torso.rotation = torso_delta * (0.52 if running else 0.75)
	# Exported clips set rhythm and body bounce; the new anatomical lengths are
	# never overwritten with the unrelated source-character rest transforms.
	var source_torso: Node2D = runtime.node_by_rig_path[BODY + "shenti1"]
	var bounce: Vector2 = source_torso.position - runtime.rest_by_rig_path[BODY + "shenti1"].origin
	bones.pelvis.position += Vector2(bounce.x * 0.25, bounce.y * 0.65)
	bones.head.rotation = _source_delta(BODY + "shenti1/tou") * 0.65
	for side in ["far", "near"]:
		var source_side := "zuotui" if side == "near" else "youtui"
		var path := "/10002/Root/Body/Foot/%s/%s/tui1" % [source_side, source_side]
		var thigh := _source_delta(path)
		var calf := _source_delta(path + "/tui2")
		var ankle := _source_delta(path + "/tui2/tui3")
		bones[side + "_thigh"].rotation = thigh * (0.72 if running else 0.90)
		bones[side + "_calf"].rotation = calf * (0.80 if running else 0.90)
		bones[side + "_foot"].rotation = ankle
		var upper_angle := -0.06
		var forearm_angle := 0.10
		if running or backward:
			var swing := sin(thigh)
			upper_angle = -0.12 + swing * (0.80 if running else 0.42)
			forearm_angle = 1.30 + swing * (0.40 if running else 0.20)
		elif clip == "AttackIdle" and side == "near":
			var attack := sin(clampf(time / maxf(duration, 0.001), 0.0, 1.0) * PI)
			upper_angle = lerpf(0.10, 1.30, attack)
			forearm_angle = lerpf(0.35, 1.65, attack)
		elif clip == "Step1":
			upper_angle = -0.35 if side == "near" else 0.3
			forearm_angle = 1.15
		# Directions use the final character plane, so the elbow actually flexes
		# instead of rigidly translating a sleeve around the chest.
		bones[side + "_upper"].rotation = -upper_angle - bones.torso.rotation
		bones[side + "_forearm"].rotation = upper_angle - forearm_angle
		bones[side + "_hand"].rotation = 0.0
	var near_knee: Vector2 = runtime.rig_root_node.to_local(bones.near_calf.global_position)
	var far_knee: Vector2 = runtime.rig_root_node.to_local(bones.far_calf.global_position)
	var leading := maxf(near_knee.x, far_knee.x)
	var trailing := minf(near_knee.x, far_knee.x)
	# The continuous robe follows both knees without deleting any leg surface.
	# Middle fabric is carried by the leading knee; the hem lags softly behind.
	bones.cloth_middle.position.x += maxf(0.0, leading - 2.0) * 0.70
	bones.cloth_hem.position.x -= maxf(0.0, leading - 2.0) * 0.20
	bones.back_middle.position.x += minf(0.0, trailing + 2.0) * 0.45
	bones.back_hem.position.x += minf(0.0, trailing + 2.0) * 0.20
	var cloth_path := BODY + "shenti2"
	var cloth_delta := _source_delta(cloth_path)
	bones.cloth_middle.rotation = cloth_delta * 0.10
	bones.cloth_hem.rotation = cloth_delta * 0.16
	bones.back_middle.rotation = cloth_delta * 0.14
	bones.back_hem.rotation = cloth_delta * 0.22
	if pose_blending and not blend_from.is_empty():
		var transition_ms := 45.0 if clip == "AttackIdle" else 90.0
		var mix := smoothstep(0.0, 1.0, float(Time.get_ticks_msec() - blend_started) / transition_ms)
		for key in bones:
			var target: Transform2D = bones[key].transform
			bones[key].transform = blend_from[key].interpolate_with(target, mix)
		if mix >= 1.0:
			blend_from.clear()

func set_visible(value: bool) -> void:
	for mesh in meshes:
		mesh.visible = value

func summary() -> Dictionary:
	return {"meshes": meshes.size(), "head_units": 7.0, "head_unit": 16.0, "own_bones": bones.size(), "display_scale": display_scale, "errors": validation_errors.duplicate()}
