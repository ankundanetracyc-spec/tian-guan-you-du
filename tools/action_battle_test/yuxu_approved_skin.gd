extends "yuxu_unified_skin.gd"

## V15 uses the approved illustration's common 1024x1536 pixel space.
## Head, bent arm and continuous clothing share that one rest drawing.
const PIXEL_SCALE := 16.0 / 186.0
const SOURCE_ORIGIN := Vector2(475, 1474)
const ROBED_MOVE_SPEED := 114.0
const ROBED_CADENCE := 1.8
const ROBED_STRIDE := 44.0
const SUPPORT_FRACTION := 0.50
var textures: Dictionary = {}
var arm_rest_points := [Vector2(425, 435), Vector2(466, 562), Vector2(609, 473)]
var motion_context := false
var motion_delta := 0.0
var motion_clock := 0.0
var previous_velocity := Vector2.ZERO
var motion_facing := 1.0
var gait_phase := 0.0
var gait_amount := 0.0
var gait_backward := 0.0
var gait_horizontal := 1.0
var acceleration_lean := 0.0
var cloth_acceleration := Vector2.ZERO
var cloth_positions: Dictionary = {}
var cloth_speeds: Dictionary = {}
var transition_age := 1.0
var sole_profiles: Dictionary = {}
var ground_contacts: Dictionary = {}
var contact_shadow: Node2D
var shadow_center := 0.0
var idle_gesture_age := 0.0
var gesture_last_clock := -1.0
var gesture_waist := 0.0
var gesture_focus := 0.0
var gesture_ready := 0.0
var action_guard := false
var action_preparing := false
var arm_materials: Dictionary = {}
var combo_stage := 0
var combo_phase := 0.0

func set_combo_context(stage: int, progress: float) -> void:
	combo_stage = stage
	combo_phase = progress

func set_action_context(guarding: bool, preparing: bool) -> void:
	# Read-only visual input. No timers, skill effects or costs are written.
	action_guard = guarding
	action_preparing = preparing

class ContactShadow extends Node2D:
	var skin: RefCounted
	func _ellipse(center: Vector2, radius: Vector2, opacity: float) -> void:
		var points := PackedVector2Array()
		for index in range(40):
			points.append(center+Vector2(cos(index*TAU/40.0),sin(index*TAU/40.0))*radius)
		draw_colored_polygon(points,Color(0.10,0.12,0.12,opacity))
	func _draw() -> void:
		# The broad body shadow and small shoe contacts share the actual floor.
		# Layered low-opacity edges avoid an isolated hard oval under the feet.
		for ring in range(4):
			_ellipse(Vector2(skin.shadow_center,0.0),Vector2(20.0-ring*2.4,4.0-ring*0.55),0.022)
		for contact in skin.ground_contacts.values():
			var strength: float = 1.0-smoothstep(0.0,10.0,float(contact.lift))
			for ring in range(3):
				_ellipse(Vector2(contact.center.x,0.0),Vector2(7.0-ring*1.2,1.65-ring*0.35),strength*0.060)

func has_contact_shadow() -> bool:
	return contact_shadow != null

func set_visible(value: bool) -> void:
	super.set_visible(value)
	if contact_shadow != null: contact_shadow.visible = value

## The live arena supplies simulation time, including zero while paused. Capture
## tools can disable pose_blending and sample the same motion deterministically.
func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	motion_context = true
	motion_delta = clampf(delta, 0.0, 0.05)
	motion_clock = clock
	if delta <= 0.0:
		return
	var speed := velocity.length()
	if speed>0.01: gait_horizontal = absf(velocity.x)/speed
	var active := clampf(speed / ROBED_MOVE_SPEED, 0.0, 1.0)
	var response := 15.0 if active > gait_amount else 19.0
	gait_amount = lerpf(gait_amount, active, 1.0-exp(-response*delta))
	var backward := 1.0 if velocity.x*facing < -10.0 else 0.0
	gait_backward = lerpf(gait_backward, backward, 1.0-exp(-12.0*delta))
	if facing != motion_facing:
		# Reverse the contact trajectory at a turn: a grounded foot remains
		# grounded instead of starting again at the opposite airborne pose.
		gait_phase = fposmod(0.5-gait_phase, 1.0)
		# Preserve world-space cloth inertia when the side-view body mirrors.
		for key in cloth_positions:
			# The panel attachment stays on its anatomical side; its loose
			# folds retain world-space inertia through the mirrored turn.
			if key in ["front","back","chest"]: continue
			cloth_positions[key].x *= -1.0
			cloth_speeds[key].x *= -1.0
		motion_facing = facing
	# Forward and backward contacts cover the same ground speed. Short steps
	# are paired with the explicitly approved slower trial movement speed.
	var cadence := ROBED_CADENCE
	if active > 0.01 or gait_amount > 0.08:
		gait_phase = fposmod(gait_phase+delta*cadence*maxf(0.35,gait_amount),1.0)
	var acceleration := (velocity.x-previous_velocity.x)*facing/maxf(delta,0.001)
	acceleration_lean = lerpf(acceleration_lean, clampf(acceleration/4000.0,-1.0,1.0),1.0-exp(-10.0*delta))
	var world_acceleration := (velocity-previous_velocity)/maxf(delta,0.001)
	cloth_acceleration = cloth_acceleration.lerp(world_acceleration.limit_length(1800.0),1.0-exp(-14.0*delta))
	previous_velocity = velocity
	transition_age += delta

func _world(point: Vector2) -> Vector2:
	return (point - SOURCE_ORIGIN) * PIXEL_SCALE

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	runtime = owner_runtime
	display_scale = 0.72
	for name in ["cutout", "body", "arm", "leg"]:
		var image := Image.new()
		if image.load(ProjectSettings.globalize_path(art_dir.path_join(name + ".png"))) != OK:
			return false
		image.generate_mipmaps()
		textures[name] = ImageTexture.create_from_image(image)
	for name in ["arm_relaxed_v19","arm_open_v19","arm_sword_fingers_v21"]:
		var image := Image.new()
		if image.load(ProjectSettings.globalize_path(art_dir.path_join(name+".png"))) != OK:
			return false
		image.generate_mipmaps()
		textures[name] = ImageTexture.create_from_image(image)
	_new_bone("pelvis", "", _world(Vector2(475, 760)))
	_new_bone("torso", "pelvis", _world(Vector2(490, 635)))
	_new_bone("head", "torso", _world(Vector2(470, 330)))
	for side in ["near", "far"]:
		var offset := Vector2.ZERO if side == "near" else Vector2(43, -9)
		_new_bone(side + "_upper", "torso", _world(arm_rest_points[0] + offset))
		_new_bone(side + "_forearm", side + "_upper", _world(arm_rest_points[1] + offset))
		_new_bone(side + "_hand", side + "_forearm", _world(arm_rest_points[2] + offset))
		arm_chains[side] = [bones[side+"_upper"], bones[side+"_forearm"], bones[side+"_hand"]]
		var points := _leg_points(side)
		_new_bone(side+"_thigh", "pelvis", _world(points[0]))
		_new_bone(side+"_calf", side+"_thigh", _world(points[1]))
		_new_bone(side+"_foot", side+"_calf", _world(points[2]))
		leg_chains[side] = [bones[side+"_thigh"], bones[side+"_calf"], bones[side+"_foot"]]
	for side in ["front", "back"]:
		_new_bone(side+"_cloth", "pelvis", _world(Vector2(475, 1040)))
		_new_bone(side+"_hem", side+"_cloth", _world(Vector2(475, 1160)))
		_new_bone(side+"_edge", side+"_hem", _world(Vector2(475, 1280)))
	cloth_chains = {"front": [bones.front_cloth, bones.front_hem, bones.front_edge], "back": [bones.back_cloth, bones.back_hem, bones.back_edge]}
	_build_approved_head()
	_build_approved_body()
	for side in ["far", "near"]:
		_build_approved_arm(side)
		_build_approved_leg(side)
		_cache_sole_profile(side)
	contact_shadow = ContactShadow.new()
	contact_shadow.skin = self
	contact_shadow.z_index = 1
	runtime.rig_root_node.add_child(contact_shadow)
	return validation_errors.is_empty() and meshes.size() == 7

func _cache_sole_profile(side: String) -> void:
	var source: Image = textures.leg.get_image()
	var points := _leg_points(side)
	var sole := PackedVector2Array()
	# Read the visible boot silhouette, rather than treating its ankle as the
	# floor or assuming a rectangular sole when the shoe pitches at toe-off.
	for x in range(330,515,3):
		for y in range(1488,1435,-1):
			if source.get_pixel(x,y).a < 0.92: continue
			var source_axis := _interpolate(float(y),[760.0,1080.0,1417.0],[410.0,386.0,384.0])
			var target_axis := _interpolate(float(y),[760.0,1080.0,1417.0],[points[0].x,points[1].x,points[2].x])
			sole.append(Vector2(float(x)+target_axis-source_axis-points[2].x,float(y)-1417.0)*PIXEL_SCALE)
			break
	sole_profiles[side] = sole

func _sole_contact(side: String, pitch: float) -> Vector2:
	var lowest := Vector2(0.0,-INF)
	for point in sole_profiles[side]:
		var rotated: Vector2 = point.rotated(pitch)
		if rotated.y > lowest.y: lowest = rotated
	return lowest

func _leg_points(side: String) -> Array:
	return [Vector2(475, 760), Vector2(440, 1080), Vector2(423, 1417)] if side == "near" else [Vector2(485, 760), Vector2(482, 1080), Vector2(510, 1417)]

func _build_approved_head() -> void:
	var rect := [335, 75, 245, 310]
	var vertices := PackedVector2Array()
	var sets := [PackedFloat32Array(), PackedFloat32Array()]
	for row in range(ROWS+1):
		var y := float(rect[1]) + float(rect[3]) * row / ROWS
		# Lower neck/collar underlap belongs to the torso. The head can turn
		# without peeling its skin base off the garment's front collar.
		var head_share := 1.0 - smoothstep(300.0, 354.0, y)
		for column in range(COLUMNS+1):
			vertices.append(_world(Vector2(float(rect[0]) + float(rect[2]) * column / COLUMNS, y)))
			sets[0].append(head_share)
			sets[1].append(1.0-head_share)
	texture = textures.cutout
	_create_mesh("head", rect, vertices, ["head", "torso"], sets, 50)

func _build_approved_body() -> void:
	# Split at the existing blue facing, preserving the approved UV drawing.
	# Coincident upper edges share weights; below the hip the two skirts can
	# open independently, with complete trouser legs visible through the slit.
	for side in ["back", "front"]:
		_build_robe_panel(side)

func _build_robe_panel(side: String) -> void:
	var rect := [275, 310, 390, 1030]
	var vertices := PackedVector2Array()
	var uvs := PackedVector2Array()
	var sets := [PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array()]
	for row in range(ROWS+1):
		var y := float(rect[1]) + float(rect[3]) * row / ROWS
		var torso_share := 1.0-smoothstep(660.0, 790.0, y)
		var cloth_share := smoothstep(735.0, 1000.0, y)
		var seam := _interpolate(y,[310.0,670.0,1000.0,1340.0],[490.0,512.0,487.0,428.0])
		var panel_release := smoothstep(820.0,1110.0,y)
		# A wrapped rear panel continues beneath the blue front facing. Its
		# overlap is widest at the thigh and tapers towards the free lower edge.
		var underlap := smoothstep(710.0,880.0,y)*lerpf(48.0,24.0,smoothstep(1030.0,1340.0,y))
		for column in range(COLUMNS+1):
			var x := lerpf(275.0,seam+underlap,float(column)/COLUMNS) if side=="back" else lerpf(seam,665.0,float(column)/COLUMNS)
			uvs.append(Vector2(x,y))
			var front_share := lerpf(smoothstep(365.0,575.0,x),1.0 if side=="front" else 0.0,panel_release)
			# One continuous mesh, with an oblique, gradual bend envelope:
			# the trailing edge releases earlier than the heavier front facing.
			var hem_share := smoothstep(1010.0+front_share*45.0, 1225.0+front_share*30.0, y)
			var edge_share := smoothstep(1210.0+front_share*20.0, 1340.0, y)
			var loose := (1.0-torso_share)*cloth_share
			# A subpixel overlap closes the resting facing without stitching
			# the two lower panels back together during a stride.
			var overlap := (float(column)/COLUMNS if side=="back" else -(1.0-float(column)/COLUMNS))*0.24*panel_release
			vertices.append(_world(Vector2(x,y))+Vector2(overlap,0.0))
			sets[0].append(torso_share)
			sets[1].append((1.0-torso_share)*(1.0-cloth_share))
			sets[2].append(loose*(1.0-hem_share)*front_share)
			sets[3].append(loose*hem_share*(1.0-edge_share)*front_share)
			sets[4].append(loose*(1.0-hem_share)*(1.0-front_share))
			sets[5].append(loose*hem_share*(1.0-edge_share)*(1.0-front_share))
			sets[6].append(loose*hem_share*edge_share*front_share)
			sets[7].append(loose*hem_share*edge_share*(1.0-front_share))
	texture = textures.body
	_create_mesh("robe_"+side,rect,vertices,["torso","pelvis","front_cloth","front_hem","back_cloth","back_hem","front_edge","back_edge"],sets,60,uvs)
	if side == "back":
		var mesh: Polygon2D = meshes.back()
		var skirt := mesh.duplicate() as Polygon2D
		skirt.name = "OverlappingRearSkirt"
		mesh.add_child(skirt)
		skirt.z_as_relative = false
		skirt.z_index = 55
		skirt.skeleton = skirt.get_path_to(runtime.skeleton)
		var lower: Array = []
		var upper: Array = []
		for triangle in mesh.polygons:
			var row_y: float = (mesh.uv[triangle[0]].y+mesh.uv[triangle[1]].y+mesh.uv[triangle[2]].y)/3.0
			if row_y > 880.0: lower.append(triangle)
			else: upper.append(triangle)
		skirt.polygons = lower
		mesh.polygons = upper
		# A narrow reverse facing uses the same cloth drawing and deformation;
		# only its shade differs. It is not a separate rigid dangling strip.
		var reverse_facing := skirt.duplicate() as Polygon2D
		reverse_facing.name = "RearSkirtInnerFacing"
		skirt.add_child(reverse_facing)
		reverse_facing.skeleton = reverse_facing.get_path_to(runtime.skeleton)
		reverse_facing.z_index = 56
		reverse_facing.modulate = Color(0.83,0.87,0.87,1.0)
		var lining: Array = []
		for triangle in lower:
			if mini(triangle[0]%9,mini(triangle[1]%9,triangle[2]%9)) >= 7: lining.append(triangle)
		reverse_facing.polygons = lining

func _build_approved_arm(side: String) -> void:
	var rect := [360, 345, 355, 285]
	var offset := Vector2.ZERO if side == "near" else Vector2(43, -9)
	var vertices := PackedVector2Array()
	var sets := [PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array(), PackedFloat32Array()]
	var upper: Vector2 = (arm_rest_points[1]-arm_rest_points[0]).normalized()
	var lower: Vector2 = (arm_rest_points[2]-arm_rest_points[1]).normalized()
	var elbow_axis: Vector2 = (upper+lower).normalized()
	for row in range(ROWS+1):
		for column in range(COLUMNS+1):
			var point := Vector2(float(rect[0]) + float(rect[2])*column/COLUMNS, float(rect[1]) + float(rect[3])*row/ROWS)
			# An oblique joint envelope follows the bent sleeve; inner/outer
			# elbow vertices do not receive identical horizontal-row weights.
			var forearm := smoothstep(-42.0, 42.0, (point-arm_rest_points[1]).dot(elbow_axis))
			var hand := smoothstep(-18.0, 20.0, (point-arm_rest_points[2]).dot(lower))
			# Only the shoulder cap can share the torso. The raised fingers
			# occupy the same image rows: a row-only mask pinned their tips to
			# the chest and bent the hand into a claw as the forearm moved.
			var seam := (1.0-smoothstep(375.0,420.0,point.y))*(1.0-smoothstep(470.0,540.0,point.x))
			vertices.append(_world(point+offset))
			sets[0].append((1.0-forearm)*(1.0-seam))
			sets[1].append(forearm*(1.0-hand)*(1.0-seam))
			sets[2].append(forearm*hand*(1.0-seam))
			sets[3].append(seam)
	texture = textures.arm_relaxed_v19
	_create_mesh(side+"_arm",rect,vertices,[side+"_upper",side+"_forearm",side+"_hand","torso"],sets,70 if side=="near" else 40)
	if side == "near":
		var mesh: Polygon2D = meshes.back()
		var shoulder := mesh.duplicate() as Polygon2D
		shoulder.name = "ShoulderUnderClothing"
		mesh.add_child(shoulder)
		shoulder.z_as_relative = false
		shoulder.z_index = 59
		shoulder.skeleton = shoulder.get_path_to(runtime.skeleton)
		var cap: Array = []
		var sleeve: Array = []
		for triangle in mesh.polygons:
			var average: float = (mesh.uv[triangle[0]].y+mesh.uv[triangle[1]].y+mesh.uv[triangle[2]].y)/3.0
			var average_x: float = (mesh.uv[triangle[0]].x+mesh.uv[triangle[1]].x+mesh.uv[triangle[2]].x)/3.0
			if average < 405.0 and average_x < 520.0: cap.append(triangle)
			else: sleeve.append(triangle)
		shoulder.polygons = cap
		mesh.polygons = sleeve
	var arm_mesh: Polygon2D = meshes.back()
	var material := ShaderMaterial.new()
	material.shader = load(get_script().resource_path.get_base_dir().path_join("yuxu_hand_blend.gdshader"))
	material.set_shader_parameter("open_hand",textures.arm_open_v19)
	material.set_shader_parameter("sword_hand",textures.arm_sword_fingers_v21)
	arm_mesh.material = material
	arm_materials[side] = material

func _build_approved_leg(side: String) -> void:
	var rect := [325, 660, 195, 830]
	var points := _leg_points(side)
	var vertices := PackedVector2Array()
	var sets := [PackedFloat32Array(),PackedFloat32Array(),PackedFloat32Array()]
	for row in range(ROWS+1):
		var y := float(rect[1])+float(rect[3])*row/ROWS
		var source_axis := _interpolate(y,[760.0,1080.0,1417.0],[410.0,386.0,384.0])
		var target_axis := _interpolate(y,[760.0,1080.0,1417.0],[points[0].x,points[1].x,points[2].x])
		var calf := smoothstep(1045.0,1115.0,y)
		var foot := smoothstep(1398.0,1435.0,y)
		for column in range(COLUMNS+1):
			var x := float(rect[0])+float(rect[2])*column/COLUMNS
			vertices.append(_world(Vector2(x+target_axis-source_axis,y)))
			sets[0].append(1.0-calf)
			sets[1].append(calf*(1.0-foot))
			sets[2].append(calf*foot)
	texture = textures.leg
	_create_mesh(side+"_leg",rect,vertices,[side+"_thigh",side+"_calf",side+"_foot"],sets,35 if side=="near" else 30)

func _aim_arm(side: String, upper_angle: float, lower_angle: float, wrist_angle: float = 0.0) -> void:
	var upper: Bone2D = bones[side+"_upper"]
	var lower: Bone2D = bones[side+"_forearm"]
	var hand: Bone2D = bones[side+"_hand"]
	upper.rotation = upper_angle - lower.rest.origin.angle() - bones.torso.rotation
	lower.rotation = lower_angle - hand.rest.origin.angle() - upper.rotation - bones.torso.rotation
	hand.rotation = wrist_angle

func _update_gesture_context(clip: String, time: float, amount: float, live: bool) -> void:
	var quiet := clip == "Idle" and amount < 0.10 and not action_guard and not action_preparing
	if live:
		if motion_clock != gesture_last_clock and motion_delta > 0.0:
			idle_gesture_age = idle_gesture_age+motion_delta if quiet else 0.0
			gesture_last_clock = motion_clock
	else:
		idle_gesture_age = time if quiet else 0.0
	# Long pauses separate the small idle variations. The sword-finger sign
	# is a brief, effect-free composure gesture, never a replacement attack.
	var age := fposmod(idle_gesture_age,56.0)
	var waist_target := smoothstep(4.0,5.6,age)*(1.0-smoothstep(10.0,11.6,age)) if quiet else 0.0
	var focus_target := smoothstep(18.0,19.3,age)*(1.0-smoothstep(21.5,23.0,age)) if quiet else 0.0
	var ready_target := 1.0 if action_guard or action_preparing or clip == "AttackIdle" else 0.0
	var response := 1.0-exp(-motion_delta*5.0) if live else 1.0
	gesture_waist = lerpf(gesture_waist,waist_target,response)
	gesture_focus = lerpf(gesture_focus,focus_target,response)
	gesture_ready = lerpf(gesture_ready,ready_target,(1.0-exp(-motion_delta*(24.0 if ready_target>gesture_ready else 4.0))) if live else 1.0)

## A low, softly flexed walking arm follows the opposite foot. Elbow flexion
## and a slight wrist lag avoid a rigid shoulder-only pendulum.
func _arm_cycle(phase: float) -> Vector3:
	var phases := [0.0,0.16,0.38,0.58,0.78,1.0]
	var poses := [Vector3(1.92,0.95,-0.03),Vector3(1.83,1.12,0.02),Vector3(1.49,0.84,0.06),Vector3(1.38,0.45,0.02),Vector3(1.65,0.58,-0.06),Vector3(1.92,0.95,-0.03)]
	var p := fposmod(phase,1.0)
	for index in range(phases.size()-1):
		if p <= phases[index+1]:
			var mix := smoothstep(phases[index],phases[index+1],p)
			return poses[index].lerp(poses[index+1],mix)
	return poses[0]

func _pose_guard_arm(side: String, phase: float, amount: float, reverse: float, breathe: float, impulse: float, attack_phase: float, dash: float) -> void:
	var near := side=="near"
	var rest := Vector3(1.72,1.05,0.03) if near else Vector3(1.94,1.45,0.05)
	# Quiet hands rest below the waist; a small forearm fold occasionally
	# brings the near hand to the belt without raising it into a guard.
	rest = rest.lerp(Vector3(1.64,0.24,-0.06) if near else Vector3(1.87,1.27,0.03),gesture_waist)
	rest = rest.lerp(Vector3(1.58,-0.65,-0.09) if near else Vector3(1.93,1.38,0.04),gesture_focus)
	var cycle := _arm_cycle(phase-(0.025 if near else 0.085))
	if not near:
		var shoulder_swing := cos((phase-0.06)*TAU)
		var elbow_follow := cos((phase-0.16)*TAU)
		cycle = Vector3(1.83+shoulder_swing*0.30,0.96+elbow_follow*0.35,0.04-sin((phase-0.21)*TAU)*0.065)
	var pose := rest.lerp(cycle,amount*lerpf(1.0,0.70,reverse))
	pose = pose.lerp(Vector3(1.50,-0.40,-0.04) if near else Vector3(2.02,1.36,0.04),gesture_ready)
	pose.x += breathe*0.014*(1.0-amount)+impulse*0.035
	pose.y -= breathe*0.018*(1.0-amount)+impulse*0.020
	# Keep part of the chest's counter-sway in the shoulder chain, while
	# the distal wrist trails it. Previously both angles cancelled the torso.
	var chest_follow: float = bones.torso.rotation*0.38
	pose.x += chest_follow
	pose.y += chest_follow*0.52
	var reach := 0.0
	var draw_in := 0.0
	if attack_phase >= 0.0:
		draw_in = smoothstep(0.0,0.16,attack_phase)*(1.0-smoothstep(0.16,0.34,attack_phase))
		reach = smoothstep(0.12,0.40,attack_phase)*(1.0-smoothstep(0.50,1.0,attack_phase))
		if near:
			pose = pose.lerp(Vector3(1.70,-0.92,-0.11),draw_in*0.58)
			pose.x = lerpf(pose.x,0.24,reach)
			pose.y = lerpf(pose.y,-0.12,reach)
			# The palm sets slightly after the elbow, and releases on recovery.
			var palm_set := smoothstep(0.23,0.44,attack_phase)*(1.0-smoothstep(0.53,0.92,attack_phase))
			pose.z = lerpf(pose.z,-0.20,palm_set)
		else:
			pose = pose.lerp(Vector3(2.15,1.48,0.03),reach*0.45+draw_in*0.12)
	pose.x += dash*(0.16 if near else -0.08)
	pose.y -= dash*(0.18 if near else 0.07)
	pose.z -= dash*0.04
	if combo_stage > 0 and combo_stage <= 4:
		# Pose-only variants reuse the approved bones, mesh and pigment masks.
		var near_targets := [Vector3(0.31,-0.08,-0.08),Vector3(0.48,-0.31,-0.30),Vector3(1.35,0.24,0.03),Vector3(0.13,-0.02,-0.22)]
		var far_targets := [Vector3(2.10,1.44,0.03),Vector3(1.24,0.32,-0.12),Vector3(0.27,-0.20,-0.08),Vector3(0.48,0.08,-0.19)]
		pose = pose.lerp(near_targets[combo_stage-1] if near else far_targets[combo_stage-1],reach)
	# A small shoulder glide is shared by the sleeve cap's torso weights.
	# Bone lengths and the approved sleeve/hand drawing remain unchanged.
	var shoulder: Bone2D = bones[side+"_upper"]
	shoulder.position += Vector2((0.36 if near else -0.20)*sin((phase-0.06)*TAU)*amount+(0.70 if near else -0.12)*reach,-0.16*amount+0.10*draw_in)
	_aim_arm(side,pose.x,pose.y,pose.z)
	# Continuous alpha/pigment blending keeps the wrist fixed while fingers
	# open; the approved sleeve is masked out of the hand-shape transition.
	var palm := smoothstep(0.05,0.65,reach) if near else 0.0
	if combo_stage > 0:
		palm = reach if combo_stage in [2,4] else 0.0
	if near and action_guard: palm = maxf(palm,gesture_ready*0.65)
	arm_materials[side].set_shader_parameter("open_amount",palm)
	arm_materials[side].set_shader_parameter("sword_amount",gesture_focus*(1.0-gesture_ready)*(1.0-amount) if near else 0.0)


## A contact/swing trajectory has explicit heel contact, support, toe-off,
## passing and landing events. It deliberately has no long straight back-leg
## hold: the swing foot folds under the body before reaching forwards.
func _foot_sample(phase: float, backwards: float) -> Vector3:
	var p := fposmod(phase, 1.0)
	var x := 0.0
	var lift := 0.0
	var pitch := 0.0
	if p < SUPPORT_FRACTION:
		var support := p/SUPPORT_FRACTION
		x = lerpf(ROBED_STRIDE*0.5,-ROBED_STRIDE*0.5,support)
		pitch = lerpf(-0.08,0.0,smoothstep(0.0,0.16,support))
		pitch += 0.24*smoothstep(0.76,1.0,support)
	else:
		var swing := (p-SUPPORT_FRACTION)/(1.0-SUPPORT_FRACTION)
		# The long robe limits both reach and knee lift. The complete leg still
		# folds under the body; no upper-leg geometry is removed or concealed.
		x = lerpf(-ROBED_STRIDE*0.5,ROBED_STRIDE*0.5,pow(smoothstep(0.0,1.0,swing),1.15))
		lift = 11.5*pow(maxf(0.0,sin(swing*PI)),1.3)
		pitch = lerpf(0.24,-0.08,smoothstep(0.05,0.86,swing))
	var backward_x := -x
	var backward_lift := lift*0.65
	x = lerpf(x,backward_x,backwards)
	lift = lerpf(lift,backward_lift,backwards)
	pitch = lerpf(pitch,-pitch*0.50,backwards)
	return Vector3(x,lift,pitch)

func _solve_leg(side: String, ankle: Vector2, foot_pitch: float) -> void:
	var thigh: Bone2D = bones[side+"_thigh"]
	var calf: Bone2D = bones[side+"_calf"]
	var foot: Bone2D = bones[side+"_foot"]
	var hip: Vector2 = runtime.rig_root_node.to_local(thigh.global_position)
	var upper_length := calf.rest.origin.length()
	var lower_length := foot.rest.origin.length()
	var offset := ankle-hip
	var distance := clampf(offset.length(),1.0,upper_length+lower_length-0.03)
	var hinge := acos(clampf((upper_length*upper_length+distance*distance-lower_length*lower_length)/(2.0*upper_length*distance),-1.0,1.0))
	var upper_angle := offset.angle()-hinge
	var knee := hip+Vector2.from_angle(upper_angle)*upper_length
	var lower_angle := (ankle-knee).angle()
	thigh.rotation = upper_angle-calf.rest.origin.angle()
	calf.rotation = lower_angle-foot.rest.origin.angle()-thigh.rotation
	foot.rotation = foot_pitch-thigh.rotation-calf.rotation

func _cloth_spring(key: String, target: Vector2, live: bool, stiffness: float = 190.0, damping: float = 18.0) -> Vector2:
	if not live or not cloth_positions.has(key):
		cloth_positions[key] = target
		cloth_speeds[key] = Vector2.ZERO
		return target
	var point: Vector2 = cloth_positions[key]
	var speed: Vector2 = cloth_speeds[key]
	# Two substeps keep short cloth chains stable at the arena's 50ms clamp.
	var dt := motion_delta*0.5
	for substep in range(2):
		speed += ((target-point)*stiffness-speed*damping)*dt
		point += speed*dt
	cloth_positions[key] = point
	cloth_speeds[key] = speed
	return point

## A lightweight angular pendulum for each cloth segment. Gravity acts on
## its absolute hanging angle, while seam stiffness acts on the local fold.
## Acceleration and upstream angular speed supply inertia; no cloth timer or
## repeating sine wave drives the hem. Fixed bone lengths preserve the seam.
func _cloth_pendulum(key: String, target: float, parent_angle: float, parent_speed: float, live: bool, stiffness: float, damping: float, gravity: float, inertia: float, limit: float) -> float:
	if not live or not cloth_positions.has(key):
		var rest_angle := (target*stiffness-parent_angle*gravity)/(stiffness+gravity)
		cloth_positions[key] = Vector2(rest_angle,0.0)
		cloth_speeds[key] = Vector2.ZERO
		return rest_angle
	var angle: float = cloth_positions[key].x
	var angular_speed: float = cloth_speeds[key].x
	var acceleration := cloth_acceleration.x*motion_facing/1200.0
	var dt := motion_delta/4.0
	for substep in range(4):
		var absolute_angle := angle+parent_angle
		var torque := (target-angle)*stiffness-gravity*sin(absolute_angle)
		torque += acceleration*inertia*cos(absolute_angle)-parent_speed*1.6
		angular_speed += (torque-angular_speed*damping)*dt
		angle += angular_speed*dt
		if absf(angle)>limit:
			angle = clampf(angle,-limit,limit)
			angular_speed *= 0.25
	cloth_positions[key] = Vector2(angle,0.0)
	cloth_speeds[key] = Vector2(angular_speed,0.0)
	return angle

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	var live := motion_context and pose_blending
	if clip != last_clip:
		blend_from.clear()
		if pose_blending and not last_clip.is_empty():
			for key in bones: blend_from[key] = bones[key].transform
		transition_age = 0.0
	last_clip = clip
	for bone in bones.values(): bone.transform = bone.rest
	if clip == "BindPose": return
	var running := clip in ["Run_New1","Run_New2"]
	var backward := clip == "BackstepWalk"
	var amount := gait_amount if live else (1.0 if running or backward else 0.0)
	var reverse := gait_backward if live else (1.0 if backward else 0.0)
	var phase := gait_phase if live else fposmod(time/(0.70 if backward else 0.5833333),1.0)
	var clock := motion_clock if live else time
	var breathe := sin(clock*TAU/3.4)
	var double_step := cos((phase-0.18)*TAU*2.0)
	var impulse := acceleration_lean if live else 0.0
	var attack := 0.0
	var attack_phase := -1.0
	if clip == "AttackIdle":
		# Quick preparation and extension; recovery is longer than the strike.
		var p := clampf(time/maxf(duration,0.001),0.0,1.0)
		attack_phase = p
		attack = smoothstep(0.06,0.39,p)*(1.0-smoothstep(0.48,1.0,p))
	var dash := sin(clampf(time/0.20,0.0,1.0)*PI) if clip == "Step1" else 0.0
	if clip == "Step1": amount = minf(amount,0.60)
	var lean_target := lerpf(0.175,-0.10,reverse)*amount+impulse*0.10+attack*0.052+dash*lerpf(0.075,-0.10,reverse)
	if combo_stage > 0 and combo_stage <= 4:
		lean_target += [-0.015,0.035,-0.03,0.055][combo_stage-1]*attack
	var lean := _cloth_spring("chest",Vector2(lean_target,0.0),live,260.0,27.0).x
	var weight_shift := lerpf(4.0,-2.0,reverse)*amount+impulse*1.0+attack*0.7
	bones.pelvis.position += Vector2(weight_shift,(5.0+0.55*double_step)*amount+dash*1.2+breathe*0.24*(1.0-amount))
	bones.torso.rotation = lean+sin(phase*TAU)*0.014*amount+breathe*0.006*(1.0-amount)
	# Counter-rotation keeps the eyes/head plane level while chest and pelvis
	# transfer weight. Neck/collar vertices still share the same continuous seam.
	bones.head.rotation = -bones.torso.rotation*0.62+breathe*0.004
	var leg_targets: Array = []
	for index in range(2):
		var side := "near" if index == 0 else "far"
		var leg_phase := fposmod(phase+float(index)*0.5,1.0)
		var sample := _foot_sample(leg_phase,reverse)
		# Project the stride onto screen X during diagonal travel; its lateral
		# planted-foot speed follows the actual horizontal movement component.
		if live: sample.x *= gait_horizontal
		var points := _leg_points(side)
		var rest_ankle := _world(points[2])
		var foot_pitch := sample.z*amount
		var lift := sample.y*amount
		var ankle := Vector2(lerpf(rest_ankle.x,sample.x,amount),0.0)
		if dash > 0.0:
			ankle.x = lerpf(rest_ankle.x,-21.0 if index==0 else 15.0,dash)
			lift = (0.0 if index==0 else 2.0)*dash
			foot_pitch = (0.02 if index==0 else -0.10)*dash
		var sole := _sole_contact(side,foot_pitch)
		ankle.y = -sole.y-lift
		leg_targets.append({"side":side,"phase":leg_phase,"ankle":ankle,"pitch":foot_pitch,"lift":lift,"sole":sole})
		# Lower the pelvis only as far as needed to keep a planted heel inside
		# the two fixed leg lengths. An unreachable ankle formerly floated.
		var hip: Vector2 = runtime.rig_root_node.to_local(bones[side+"_thigh"].global_position)
		var reach: float = bones[side+"_calf"].rest.origin.length()+bones[side+"_foot"].rest.origin.length()-0.12
		var required_y := ankle.y-sqrt(maxf(1.0,reach*reach-pow(ankle.x-hip.x,2.0)))
		bones.pelvis.position.y += maxf(0.0,required_y-hip.y)
	_update_gesture_context(clip,time,amount,live)
	ground_contacts.clear()
	for target in leg_targets:
		var side: String = target.side
		var ankle: Vector2 = target.ankle
		var foot_pitch: float = target.pitch
		_solve_leg(side,ankle,foot_pitch)
		ground_contacts[side] = {"center":ankle+target.sole,"lift":target.lift,"pitch":foot_pitch}
		_pose_guard_arm(side,target.phase,amount,reverse,breathe,impulse,attack_phase,dash)
	shadow_center = weight_shift*0.38
	contact_shadow.queue_redraw()
	var a: Vector2 = runtime.rig_root_node.to_local(bones.near_calf.global_position)
	var b: Vector2 = runtime.rig_root_node.to_local(bones.far_calf.global_position)
	var lead := maxf(a.x,b.x)-weight_shift
	var trail := minf(a.x,b.x)-weight_shift
	# The split skirts give the knees room instead of enclosing their full
	# envelope. Small contact deflection opens the slit without ballooning.
	var drift := lerpf(-1.6,1.2,reverse)*amount-impulse*1.0
	var front_target := Vector2(minf(maxf(0.0,lead-6.0)*0.12,2.4)+drift*0.15,0.0)
	var back_target := Vector2(maxf(minf(0.0,trail+6.0)*0.10,-1.8)+drift*0.65,0.0)
	var front_envelope := _cloth_spring("front",front_target,live,230.0,25.0)
	var back_envelope := _cloth_spring("back",back_target,live,190.0,22.0)
	bones.front_cloth.position += front_envelope
	bones.back_cloth.position += back_envelope
	var travel := clampf(previous_velocity.x*motion_facing/ROBED_MOVE_SPEED,-1.0,1.0) if live else lerpf(1.0,-1.0,reverse)*amount
	for side in ["front","back"]:
		var front: bool = side=="front"
		# A passing knee nudges the wrapped cloth; release and drag propagate
		# down the three fixed-length segments through their different damping.
		var knee_push := clampf((lead-10.0)/18.0,0.0,1.0)*amount
		var recovery: float = (leg_targets[0].lift-leg_targets[1].lift)/11.5
		var upper_angle := travel*0.045-knee_push*(0.045 if front else 0.015)
		var upper := _cloth_pendulum(side+"_bend",upper_angle,0.0,0.0,live,105.0,17.0,32.0,8.0,0.12)
		var middle_target := travel*(0.05 if front else 0.07)+recovery*(0.055 if front else -0.075)
		var upper_speed: float = cloth_speeds[side+"_bend"].x
		var middle := _cloth_pendulum(side+"_fold",middle_target,upper,upper_speed,live,70.0 if front else 58.0,12.5 if front else 11.0,30.0,11.0,0.22)
		var edge_target := recovery*(0.065 if front else -0.10)
		var parent_speed: float = upper_speed+cloth_speeds[side+"_fold"].x
		var edge := _cloth_pendulum(side+"_curl",edge_target,upper+middle,parent_speed,live,42.0 if front else 34.0,8.5 if front else 7.8,28.0,13.0,0.26)
		bones[side+"_cloth"].rotation = upper
		bones[side+"_hem"].rotation = middle
		bones[side+"_edge"].rotation = edge
		# Only the running lift is translated. Free-edge motion comes from
		# bending fixed-length segments, avoiding a stretched rubber silhouette.
		var panel_open := 0.35*amount if front else -0.30*amount
		bones[side+"_hem"].position += _cloth_spring(side+"_lift",Vector2(panel_open+drift*(0.35 if front else 0.45),-0.8*amount),live,155.0 if front else 120.0,19.0 if front else 16.0)
	if pose_blending and not blend_from.is_empty():
		var blend_time := 0.035 if clip == "AttackIdle" else 0.11
		var mix := smoothstep(0.0,1.0,transition_age/blend_time)
		for key in bones:
			# Live locomotion continues below the waist while casting. Its
			# contact solution must not be frozen by an upper-body transition.
			if live and (key.contains("thigh") or key.contains("calf") or key.contains("foot") or key=="pelvis" or key.contains("cloth") or key.ends_with("hem") or key.ends_with("edge")):
				continue
			bones[key].transform = blend_from[key].interpolate_with(bones[key].transform,mix)
		if mix >= 1.0: blend_from.clear()

func summary() -> Dictionary:
	return {"meshes":meshes.size(),"own_bones":bones.size(),"source":"approved fullbody v15","common_pixel_scale":PIXEL_SCALE,"head_to_crown_ratio":(1474.0-130.0)/186.0,"display_scale":display_scale,"errors":validation_errors.duplicate()}

