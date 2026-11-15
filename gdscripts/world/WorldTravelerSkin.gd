extends Node2D
## The map traveler is a 2D skeleton. Each painted limb covers its joints
## with one weighted mesh, while the hair and robe have their own bone chains.

const PARTS_TEXTURE := "res://textures/world/bai_chuan/traveler_rig_20261115/traveler_parts.png"
const APPEARANCE := "bai_chuan_white_teal_traveler"
const HEIGHT := 128.0
const RUN_PERIOD := 0.64
const IDLE_PERIOD := 1.63

const HEAD_RECT := Rect2(126, 92, 228, 293)
const ROBE_RECT := Rect2(510, 60, 381, 795)
const ARM_RECT := Rect2(1072, 190, 133, 518)
const LEG_RECT := Rect2(1428, 157, 181, 644)
const HAIR_RECT := Rect2(155, 566, 137, 211)

const HIP_Y := -66.0
const CHEST_Y := -76.0
const SHOULDER_Y := -94.0
const NECK_Y := -105.5
const HAIR_ORIGIN := Vector2(-4.8, -113.2)
const ELBOW := 16.5
const WRIST := 31.5
const ARM_ORIGIN := Vector2(1137, 252)
const WRIST_SOURCE_X := 1151.0
const KNEE := 28.5
const ANKLE := 60.0
const SOLE := [Vector2(-4.1, 6.35), Vector2(9.85, 5.95)]

# One near-leg cycle in the travel reference. The far leg uses the same curve
# half a cycle later. Importing a new costume does not change these phases.
const STRIDE := [
	Vector3(39.9, -20, -6), Vector3(35.56, -28.87, 8.18),
	Vector3(28.12, -31.87, 15.38), Vector3(9.34, -36.98, 30.07),
	Vector3(2.60, -36.39, 31.12), Vector3(-17.78, -25.48, 18.39),
	Vector3(-37.10, -15.12, 6.31), Vector3(-38.77, -31.90, 9.63),
	Vector3(-37.80, -72.00, 20), Vector3(-23.26, -93.31, 17.34),
	Vector3(-10.36, -111.96, 14.99), Vector3(11.36, -105.57, 13.11),
	Vector3(42.38, -96.43, 10.42), Vector3(48.50, -86.48, 7.96),
	Vector3(53.84, -50.17, -0.76), Vector3(50.06, -37.28, -3.23),
]

const HEM_STIFFNESS := Vector3(118, 78, 55)
const HEM_DAMPING := Vector3(17, 13, 10)
const HAIR_STIFFNESS := Vector3(160, 108, 68)
const HAIR_DAMPING := Vector3(20, 14, 9)
const DARK_CLOTH := Color(0.79, 0.81, 0.82)
const FAR_SLEEVE_TINT := Color(0.87, 0.88, 0.88)

static var _texture: Texture2D
var skeleton: Skeleton2D
var gait_weight := 0.0
var airflow := 0.0
var robe_angles := Vector3.ZERO
var hair_angles := Vector3.ZERO
var sole_clearance := 0.0
var _stride_time := 0.0
var _robe_velocity := Vector3.ZERO
var _hair_velocity := Vector3.ZERO
var _facing := 1.0
var _hips: Bone2D
var _chest: Bone2D
var _head: Bone2D
var _arms: Array[Array] = []
var _legs: Array[Array] = []
var _hem: Array[Bone2D]
var _hair: Array[Bone2D]


func build() -> bool:
	if not _load_texture():
		return false
	skeleton = Skeleton2D.new()
	skeleton.name = "TravelerSkeleton"
	add_child(skeleton)
	_hips = _bone(skeleton, "Hips", Vector2(0, HIP_Y))
	_chest = _bone(_hips, "Chest", Vector2(0, CHEST_Y - HIP_Y))
	_head = _bone(_chest, "Head", Vector2(0, NECK_Y - CHEST_Y))
	_hair = _chain(_head, ["Nape", "NapeMiddle", "NapeTip"],
		HAIR_ORIGIN - Vector2(0, NECK_Y), 5.0, 10.0)
	_hem = _chain(_chest, ["RobeWaist", "RobeKnee", "RobeHem"], Vector2.ZERO, 23.0, 47.0)
	for side: String in ["Near", "Far"]:
		var near := side == "Near"
		var arm := _chain(_chest, [side + "Shoulder", side + "Elbow", side + "Wrist"],
			Vector2(-1.15 if near else -0.45, SHOULDER_Y - CHEST_Y), ELBOW, WRIST)
		arm[2].position.x = (WRIST_SOURCE_X - ARM_ORIGIN.x) * _sleeve_width(near)
		arm[2].rest = arm[2].transform
		_arms.append(arm)
		_legs.append(_chain(_hips, [side + "Thigh", side + "Shin", side + "Boot"],
			Vector2(0.6 if near else -0.65, 0), KNEE, ANKLE))

	_add_arm(1)
	_add_leg(1)
	_add_leg(0)
	_add_mesh("NapeLock", HAIR_RECT, Vector2(251, 600), HAIR_ORIGIN, Vector2(0.078, 0.083),
		20, _hair, [Vector2(HAIR_ORIGIN.y + 5.0, 2.8), Vector2(HAIR_ORIGIN.y + 10.0, 2.8)])
	_add_mesh("HeadAndNeck", HEAD_RECT, Vector2(241, 342), Vector2(0, NECK_Y),
		Vector2(0.092, 0.092), 1, [_head], [])
	var robe := _add_mesh("ContinuousRobe", ROBE_RECT, Vector2(756, 318), Vector2(0, CHEST_Y),
		Vector2(0.108, 0.125), 48, [_chest, _hem[0], _hem[1], _hem[2]],
		[Vector2(HIP_Y + 4.0, 9.0), Vector2(CHEST_Y + 23.0, 13.0), Vector2(CHEST_Y + 47.0, 14.0)],
		Color.WHITE, 10)
	# Add fullness across the seat and blend it into the waist and folds.
	var points := robe.polygon
	for index in points.size():
		var point := points[index]
		var seat := smoothstep(-84.0, -70.0, point.y) * (1.0 - smoothstep(-58.0, -42.0, point.y))
		var fullness := 0.30 if point.x < 0.0 else 0.15
		points[index].x = point.x * (1.0 + fullness * seat)
	robe.polygon = points
	_add_arm(0)
	return true


func _bone(parent: Node, bone_name: String, offset: Vector2) -> Bone2D:
	var bone := Bone2D.new()
	bone.name = bone_name
	bone.set_autocalculate_length_and_angle(false)
	bone.position = offset
	bone.rest = bone.transform
	parent.add_child(bone)
	return bone


func _chain(parent: Bone2D, names: Array[String], offset: Vector2,
		lower_joint: float, tip_joint: float) -> Array[Bone2D]:
	var upper := _bone(parent, names[0], offset)
	var lower := _bone(upper, names[1], Vector2(0, lower_joint))
	var tip := _bone(lower, names[2], Vector2(0, tip_joint - lower_joint))
	return [upper, lower, tip]


func _add_arm(side: int) -> void:
	var chain: Array[Bone2D] = _arms[side]
	var near := side == 0
	_add_mesh("NearSleeveAndHand" if near else "FarSleeveAndHand",
		ARM_RECT, ARM_ORIGIN, Vector2(chain[0].position.x, SHOULDER_Y),
		Vector2(_sleeve_width(near), 0.087), 28, chain,
		[Vector2(SHOULDER_Y + ELBOW, 7.0), Vector2(SHOULDER_Y + WRIST, 4.8)],
		Color.WHITE if near else FAR_SLEEVE_TINT)


func _sleeve_width(near: bool) -> float:
	return 0.114 if near else 0.098


func _add_leg(side: int) -> void:
	var chain: Array[Bone2D] = _legs[side]
	_add_mesh("NearTrouserAndBoot" if side == 0 else "FarTrouserAndBoot",
		LEG_RECT, Vector2(1500, 230), Vector2(chain[0].position.x, HIP_Y),
		Vector2(0.105, 0.119), 36, [_hips, chain[0], chain[1], chain[2]],
		[Vector2(HIP_Y + 2.0, 8.5), Vector2(HIP_Y + KNEE, 9.5), Vector2(HIP_Y + ANKLE, 5.0)],
		Color.WHITE if side == 0 else DARK_CLOTH)


func _add_mesh(part: String, region: Rect2, source_pivot: Vector2, bind_pivot: Vector2,
		pixel_scale: Vector2, rows: int, bones: Array[Bone2D], bands: Array[Vector2],
		tint := Color.WHITE, columns := 1) -> Polygon2D:
	var points := PackedVector2Array()
	var uvs := PackedVector2Array()
	var strips: Array[PackedInt32Array] = []
	var row_width := columns + 1
	for row in range(rows + 1):
		var y := lerpf(region.position.y, region.end.y, float(row) / rows)
		for column in row_width:
			var x := lerpf(region.position.x, region.end.x, float(column) / columns)
			var uv := Vector2(x, y)
			uvs.append(uv)
			points.append(bind_pivot + (uv - source_pivot) * pixel_scale)
	for row in rows:
		for column in columns:
			var top := row * row_width + column
			var bottom := top + row_width
			strips.append(PackedInt32Array([top, top + 1, bottom + 1, bottom]))
	var mesh := Polygon2D.new()
	mesh.name = part
	mesh.color = tint
	mesh.texture = _texture
	mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	mesh.polygon = points
	mesh.uv = uvs
	mesh.polygons = strips
	add_child(mesh)
	mesh.skeleton = mesh.get_path_to(skeleton)
	for joint in bones.size():
		var weights := PackedFloat32Array()
		for point in points:
			var enters := _band_weight(point.y, bands[joint - 1]) if joint > 0 else 1.0
			var exits := _band_weight(point.y, bands[joint]) if joint < bands.size() else 0.0
			weights.append(enters - exits)
		mesh.add_bone(skeleton.get_path_to(bones[joint]), weights)
	return mesh


func _band_weight(y: float, band: Vector2) -> float:
	return smoothstep(band.x - band.y, band.x + band.y, y)


static func _load_texture() -> bool:
	if _texture != null:
		return true
	if ResourceLoader.exists(PARTS_TEXTURE, "Texture2D"):
		_texture = load(PARTS_TEXTURE) as Texture2D
		if _texture != null:
			return true
	var image := Image.load_from_file(PARTS_TEXTURE)
	if image == null or image.is_empty():
		return false
	image.generate_mipmaps()
	_texture = ImageTexture.create_from_image(image)
	return true


func pose(delta: float, moving: bool, clock: float, facing: float, pressure: float) -> void:
	if facing != _facing:
		mirror_wind()
		_facing = facing
		scale = Vector2(facing, 1.0)
	var transition := 0.17 if moving else 0.23
	gait_weight = move_toward(gait_weight, 1.0 if moving else 0.0, delta / transition)
	if moving:
		_stride_time = fposmod(_stride_time + delta, RUN_PERIOD)
	var phase := _stride_time / RUN_PERIOD
	var run := smoothstep(0.0, 1.0, gait_weight)
	var breath := clock * TAU / IDLE_PERIOD
	var beat := phase * TAU
	_step_wind(delta, pressure, breath, beat, run)

	_chest.rotation = deg_to_rad(lerpf(0.8 * sin(breath) + 3.0 * airflow,
		14.0 + 1.8 * sin(2.0 * beat), run))
	_head.rotation = -_chest.rotation * 0.68 + 0.012 * sin(breath)
	for side in 2:
		var side_phase := phase + 0.5 * side
		_pose_leg(_legs[side], side_phase, breath, run, side == 0)
		_pose_arm(_arms[side], side_phase, breath, run, side == 0)
	# The near/far feet use the contact half of the source stride. When both
	# are lifting, keep the small airborne beat separate from the ground anchor.
	var contact := maxf(_tread(phase), _tread(phase + 0.5))
	sole_clearance = run * (1.0 - contact) * 1.35
	_hips.position.y = HIP_Y
	_hips.position.y -= maxf(_sole_y(_legs[0]), _sole_y(_legs[1])) + sole_clearance

	for i in 3:
		_hem[i].rotation = robe_angles[i]
		_hair[i].rotation = hair_angles[i]


func _pose_arm(arm: Array[Bone2D], phase: float, breath: float, run: float, near: bool) -> void:
	var beat := phase * TAU
	var relax := (3.5 if near else 6.0) + 0.9 * sin(breath) + 5.0 * airflow
	var swing := 9.5 + (22.4 if near else 19.0) * cos(beat)
	# The far hand passes behind the body at mid-stride. The forearms fold
	# farther as they come forward and hang lower while passing the hip.
	var swing_flex := (32.0 if near else 26.0) - 13.0 * cos(beat - 0.35)
	var flex := lerpf(6.5 + 1.3 * sin(breath), swing_flex, run)
	arm[0].rotation = deg_to_rad(lerpf(relax, swing, run)) - _chest.rotation
	arm[1].rotation = -deg_to_rad(flex)
	arm[2].rotation = deg_to_rad(2.0 * sin(beat - 0.5) * run)


func _pose_leg(leg: Array[Bone2D], phase: float, breath: float, run: float, near: bool) -> void:
	var frame := fposmod(phase, 1.0) * STRIDE.size()
	var index := floori(frame)
	var stride: Vector3 = STRIDE[index].lerp(STRIDE[(index + 1) % STRIDE.size()], frame - index)
	var relaxed_thigh := (-2.4 if near else 0.4) - 0.65 * sin(breath)
	var relaxed_shin := (4.0 if near else 1.8) + 1.3 * sin(breath)
	var relaxed := Vector3(relaxed_thigh, relaxed_shin, -relaxed_thigh - relaxed_shin)
	var angles := relaxed.lerp(stride * Vector3(-0.80, -0.77, -0.82), run) * (PI / 180.0)
	for i in 3:
		leg[i].rotation = angles[i]


func _tread(phase: float) -> float:
	var step := fposmod(phase, 1.0)
	return smoothstep(0.02, 0.075, step) * (1.0 - smoothstep(0.31, 0.375, step))


func _sole_y(leg: Array[Bone2D]) -> float:
	var foot := _hips.transform * leg[0].transform * leg[1].transform * leg[2].transform
	return maxf((foot * SOLE[0]).y, (foot * SOLE[1]).y)


func _step_wind(delta: float, pressure: float, breath: float, beat: float, run: float) -> void:
	airflow = lerpf(airflow, pressure, 1.0 - exp(-6.0 * delta))
	var steps := 2.0 * beat
	var rhythm := Vector3(cos(steps), cos(steps - 0.50), cos(steps - 1.05))
	var gust := Vector3(sin(breath), sin(breath - 0.78), sin(breath - 1.56))
	var robe := (Vector3(0.055, 0.065, 0.075) + rhythm * Vector3(0.02, 0.04, 0.063)) * run
	robe += (Vector3(0.16, 0.19, 0.17) + gust * Vector3(0.04, 0.071, 0.097)) * airflow
	robe += gust * Vector3(0.007, 0.017, 0.027)
	var hair := (Vector3(0.24, 0.20, 0.12) + rhythm * Vector3(0.072, 0.10, 0.17)) * run
	hair += (Vector3(0.52, 0.47, 0.30) + gust * Vector3(0.10, 0.15, 0.19)) * airflow
	hair += gust * Vector3(0.021, 0.043, 0.06)
	var slices := maxi(1, ceili(delta * 120.0))
	var step := delta / slices
	for i in slices:
		_robe_velocity += ((robe - robe_angles) * HEM_STIFFNESS - _robe_velocity * HEM_DAMPING) * step
		_hair_velocity += ((hair - hair_angles) * HAIR_STIFFNESS - _hair_velocity * HAIR_DAMPING) * step
		robe_angles += _robe_velocity * step
		hair_angles += _hair_velocity * step


func mirror_wind() -> void:
	robe_angles = -robe_angles
	hair_angles = -hair_angles
	_robe_velocity = -_robe_velocity
	_hair_velocity = -_hair_velocity


func carry_wind_from(other: Node2D, facing: float) -> void:
	robe_angles = other.robe_angles
	hair_angles = other.hair_angles
	_robe_velocity = other._robe_velocity
	_hair_velocity = other._hair_velocity
	airflow = other.airflow
	_facing = facing
	scale = Vector2(facing, 1.0)


func reset_secondary_motion() -> void:
	gait_weight = 0.0
	_stride_time = 0.0
	airflow = 0.0
	robe_angles = Vector3.ZERO
	hair_angles = Vector3.ZERO
	_robe_velocity = Vector3.ZERO
	_hair_velocity = Vector3.ZERO


func summary() -> Dictionary:
	return {"errors": [], "visible_hair_motion": "world_nape_lock",
		"world_motion": {"gait_weight": gait_weight, "airflow": airflow, "hair_angles": hair_angles,
			"robe_angles": robe_angles, "sole_clearance": sole_clearance}}
