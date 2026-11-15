extends "realtime_garment_skin.gd"

## The approved ankle-length robe keeps its quiet front. Only the rear cloth
## hangs from the hip and responds to travel, acceleration and gravity.
const FAST_WALK_REFERENCE := 235.0
var visible_leg_meshes: Array[Polygon2D] = []
var gait_speed_actual := 0.0
var gait_speed_smoothed := 0.0
var gait_cadence_hz := 0.80
var gait_stride_half := 22.0
var gait_lift := 11.5
var gait_pitch_scale := 0.45
var gait_last_clock := -1.0
var cape_wind := 0.0
var cape_last_clock := -1.0
var cape_rear_anchor := Vector2.ZERO
var cape_rear_tip := Vector2.ZERO
var cape_reset_count := 0
var pending_gait_phase := 0.0
var pending_gait_amount := 0.0
var pending_gait_reset := false
var cape_body_records: Array[Dictionary] = []
var cape_source_image: Image
var locomotion_style: StringName = &"ground"
var arm_swing_scale := 1.0
var arm_speed_correction := Vector3.ZERO

func set_locomotion_style(value: StringName) -> void:
	# Future locomotion styles can opt out of grounded arm coordination.
	# This is a pose input only; it does not introduce an airborne mechanic.
	locomotion_style = value

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	garment_mode = SPLIT_OUTFIT
	var ready := super.build(owner_runtime,art_dir)
	if not ready: return false
	for mesh in meshes:
		if mesh.name in ["YuxuUnified_near_leg","YuxuUnified_far_leg"]:
			visible_leg_meshes.append(mesh)
	for record in mesh_records:
		if record.name in ["robe_back","robe_front"]: cape_body_records.append(record)
	cape_source_image = textures.body.get_image()
	return visible_leg_meshes.size() == 2

func _build_approved_body() -> void:
	# Rest transforms are captured when the original mesh is bound below.
	# Moving this rear chain's pivots changes its hanging structure, while
	# the unchanged source-space polygon preserves the exact resting drawing.
	for entry in [["back_cloth",Vector2(0.0,60.0*PIXEL_SCALE)],
		["back_hem",Vector2(0.0,220.0*PIXEL_SCALE)],
		["back_edge",Vector2(0.0,180.0*PIXEL_SCALE)]]:
		var bone: Bone2D = bones[entry[0]]
		bone.position = entry[1]
		bone.rest = bone.transform
	super._build_approved_body()

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	if delta <= 0.0:
		super.set_motion_context(delta,velocity,clock,facing)
		return
	pending_gait_phase = gait_phase
	pending_gait_amount = gait_amount
	var facing_before := motion_facing
	pending_gait_reset = clock < motion_clock or clock-motion_clock > 0.25
	if runtime != null and garment_has_root_origin:
		if runtime.global_position.distance_to(garment_last_root_origin) > maxf(90.0,velocity.length()*delta*3.0): pending_gait_reset = true
	var old_velocity := previous_velocity
	if pending_gait_reset:
		old_velocity = velocity
		garment_angles = Vector3.ZERO
		garment_angular_velocity = Vector3.ZERO
		cloth_positions.clear()
		cloth_speeds.clear()
		gait_speed_smoothed = 0.0
		pending_gait_phase = 0.0
		cape_reset_count += 1
	if facing != facing_before:
		pending_gait_phase = fposmod(0.5-pending_gait_phase,1.0)
		garment_angles *= -1.0
		garment_angular_velocity *= -1.0
	super.set_motion_context(delta,velocity,clock,facing)
	# The parent supplies body acceleration and turn bookkeeping. Defer the
	# sole phase advance until the current clip identifies an actual dash.
	gait_phase = pending_gait_phase
	gait_amount = pending_gait_amount
	gait_speed_actual = velocity.length()
	garment_drive = clampf((velocity.x-old_velocity.x)*facing/maxf(motion_delta,0.001)/1200.0,-3.8,3.8)
	garment_air_drag = clampf(velocity.x*facing/114.0,-2.1,2.1)
	garment_last_root_origin = runtime.global_position if runtime != null else Vector2.ZERO
	garment_has_root_origin = runtime != null
	var local_travel := velocity.x*facing
	var travel_sign := signf(local_travel) if absf(local_travel) > 1.0 else 1.0
	cape_wind = clampf(gait_speed_actual/114.0,0.0,2.5)*travel_sign

func _advance_gait(clip: String) -> void:
	if not motion_context or motion_delta<=0.0 or motion_clock==gait_last_clock: return
	gait_last_clock = motion_clock
	# A fast ordinary walk remains a walk at every speed. Only the explicit
	# Step1 clip holds walking parameters while its dedicated dash pose plays.
	var walk_speed := gait_speed_smoothed if clip=="Step1" else gait_speed_actual
	var speed_response := 12.0 if walk_speed > gait_speed_smoothed else 18.0
	gait_speed_smoothed = lerpf(gait_speed_smoothed,walk_speed,1.0-exp(-speed_response*motion_delta))
	var active := clampf(walk_speed/30.0,0.0,1.0)
	var amount_response := 15.0 if active > pending_gait_amount else 19.0
	gait_amount = lerpf(pending_gait_amount,active,1.0-exp(-amount_response*motion_delta))
	var speed_ratio := maxf(0.0,gait_speed_smoothed/FAST_WALK_REFERENCE)
	# At 235 the original V21 stride/lift is restored. Faster travel grows
	# both continuously; a gentle asymptote keeps very high attributes sane.
	var amplitude_ratio := minf(speed_ratio,2.5)
	gait_cadence_hz = 0.80+1.0*pow(speed_ratio,0.80)
	gait_stride_half = 14.0+8.0*pow(amplitude_ratio,0.80)
	gait_lift = 7.5+4.0*pow(amplitude_ratio,0.85)
	gait_pitch_scale = 0.45+0.55*minf(speed_ratio,1.0)
	# Replace the parent's fixed-rate phase advance. This is the sole phase
	# accumulation, so speed changes retain the same foot contact sequence.
	gait_phase = pending_gait_phase
	if walk_speed > 0.01 or gait_amount > 0.03:
		gait_phase = fposmod(gait_phase+motion_delta*gait_cadence_hz*clampf(gait_amount/0.35,0.0,1.0),1.0)

func _foot_sample(phase: float, backwards: float) -> Vector3:
	var p := fposmod(phase,1.0)
	var x := 0.0
	var lift := 0.0
	var pitch := 0.0
	if p < SUPPORT_FRACTION:
		var support := p/SUPPORT_FRACTION
		x = lerpf(gait_stride_half,-gait_stride_half,support)
		pitch = lerpf(-0.07,0.0,smoothstep(0.0,0.16,support))
		pitch += 0.19*smoothstep(0.76,1.0,support)
	else:
		var swing := (p-SUPPORT_FRACTION)/(1.0-SUPPORT_FRACTION)
		x = lerpf(-gait_stride_half,gait_stride_half,pow(smoothstep(0.0,1.0,swing),1.15))
		lift = gait_lift*pow(maxf(0.0,sin(swing*PI)),1.3)
		pitch = lerpf(0.19,-0.07,smoothstep(0.05,0.86,swing))
	pitch *= gait_pitch_scale
	return Vector3(lerpf(x,-x,backwards),lerpf(lift,lift*0.76,backwards),lerpf(pitch,-pitch*0.5,backwards))

func _pose_guard_arm(side: String, phase: float, amount: float, reverse: float, breathe: float, impulse: float, attack_phase: float, dash: float) -> void:
	super._pose_guard_arm(side,phase,amount,reverse,breathe,impulse,attack_phase,dash)
	arm_swing_scale = clampf(gait_stride_half/22.0,0.72,1.45)
	arm_speed_correction = Vector3.ZERO
	if locomotion_style != &"ground" or amount<=0.001: return
	# Dedicated gestures retain the original shoulder, elbow and palm pose.
	if attack_phase>=0.0 or last_clip=="Step1" or action_guard or action_preparing or combo_stage>0 or gesture_ready>0.04: return
	var near := side=="near"
	var cycle := super._arm_cycle(phase-(0.025 if near else 0.085))
	var center := Vector3(1.65,0.785,0.0)
	if not near:
		cycle = Vector3(1.83+cos((phase-0.06)*TAU)*0.30,0.96+cos((phase-0.16)*TAU)*0.35,0.04-sin((phase-0.21)*TAU)*0.065)
		center = Vector3(1.83,0.96,0.04)
	var moving := amount*lerpf(1.0,0.70,reverse)*(1.0-gesture_ready)
	var correction := (cycle-center)*(arm_swing_scale-1.0)*moving
	# The wrist keeps its small lag. Faster legs mainly increase shoulder
	# and elbow excursion, rather than whipping or turning the palm.
	correction.z = (cycle.z-center.z)*(clampf(arm_swing_scale,0.90,1.12)-1.0)*moving
	var upper: Bone2D = bones[side+"_upper"]
	var lower: Bone2D = bones[side+"_forearm"]
	var hand: Bone2D = bones[side+"_hand"]
	var upper_angle: float = upper.rotation+lower.rest.origin.angle()+bones.torso.rotation
	var lower_angle: float = lower.rotation+hand.rest.origin.angle()+upper.rotation+bones.torso.rotation
	upper.position.x += (0.36 if near else -0.20)*sin((phase-0.06)*TAU)*amount*(arm_swing_scale-1.0)
	_aim_arm(side,upper_angle+correction.x,lower_angle+correction.y,hand.rotation+correction.z)
	arm_speed_correction = correction

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

func _constrain_closed_legs() -> void:
	# The final garment policy permits natural legs outside the split hem.
	# This override also keeps the earlier closed-robe shortening inapplicable.
	pass

func _integrate_garment() -> void:
	# Three hanging joints: seam elasticity, gravity, viscous damping and
	# motion-induced inertia. No animation phase drives these cloth angles.
	var dt := motion_delta/8.0
	for substep in range(8):
		var parent_angle := 0.0
		var parent_speed := 0.0
		for segment in range(3):
			var stiffness: float = [65.0,39.0,26.0][segment]
			var gravity: float = [29.0,24.0,19.0][segment]
			var damping: float = [10.2,8.4,7.0][segment]
			var inertia: float = [4.0,7.0,9.0][segment]
			var target := cape_wind*float([0.065,0.115,0.145][segment])
			var angle := garment_angles[segment]
			var torque := (target-angle)*stiffness-gravity*sin(parent_angle+angle)
			torque += garment_drive*inertia*cos(parent_angle+angle)-parent_speed*1.8
			garment_angular_velocity[segment] += (torque-garment_angular_velocity[segment]*damping)*dt
			angle += garment_angular_velocity[segment]*dt
			var limit: float = [0.18,0.28,0.34][segment]
			if absf(angle) > limit:
				angle = clampf(angle,-limit,limit)
				garment_angular_velocity[segment] *= 0.25
			garment_angles[segment] = angle
			parent_angle += angle
			parent_speed += garment_angular_velocity[segment]
	garment_simulation_steps += 1

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	if motion_context and motion_delta <= 0.0 and last_clip != "": return
	_advance_gait(clip)
	super.update_cloth(clip,time,duration,tracks)
	if clip == "BindPose": return
	if motion_context and pose_blending:
		if motion_clock!=cape_last_clock: _integrate_garment()
	else:
		garment_angles = Vector3.ZERO
		garment_angular_velocity = Vector3.ZERO
	# Cancel the shared adapter's old floor-length placement completely.
	# Both front and rear start at their true source drawing; only the rear
	# then receives the physical hanging angles.
	for side in ["front","back"]:
		for suffix in ["cloth","hem","edge"]:
			var bone: Bone2D = bones[side+"_"+suffix]
			bone.transform = bone.rest
	bones.back_cloth.rotation = garment_angles.x
	bones.back_hem.rotation = garment_angles.y
	bones.back_edge.rotation = garment_angles.z
	garment_ground_shift = 0.0
	# A one-sided collision only prevents penetration. It never lowers the
	# original hem to the floor or changes the approved resting robe length.
	garment_floor_max = _garment_lowest_point()
	if garment_floor_max > -0.5:
		var correction := garment_floor_max+0.5
		bones.back_cloth.position.y -= correction
		garment_ground_shift = -correction
		garment_floor_max = _garment_lowest_point()
	cape_rear_anchor = runtime.rig_root_node.to_local(bones.back_cloth.global_position)
	var edge: Bone2D = bones.back_edge
	cape_rear_tip = runtime.rig_root_node.to_local(edge.to_global(Vector2(0.0,120.0*PIXEL_SCALE)))
	cape_last_clock = motion_clock

func _garment_lowest_point() -> float:
	var lowest := -INF
	var root_inverse: Transform2D = runtime.rig_root_node.global_transform.affine_inverse()
	for record in cape_body_records:
		var mesh: Polygon2D = record.mesh
		var transforms: Array[Transform2D] = []
		for bone_index in range(record.keys.size()):
			transforms.append(root_inverse*bones[record.keys[bone_index]].global_transform*record.rest[bone_index].affine_inverse())
		for index in range(mesh.polygon.size()):
			var uv: Vector2 = mesh.uv[index]
			if uv.y<900.0 or cape_source_image.get_pixel(clampi(roundi(uv.x),0,cape_source_image.get_width()-1),clampi(roundi(uv.y),0,cape_source_image.get_height()-1)).a<0.75: continue
			var point := Vector2.ZERO
			for bone_index in range(record.keys.size()):
				point += (transforms[bone_index]*mesh.polygon[index])*float(record.weights[bone_index][index])
			lowest = maxf(lowest,point.y)
	return lowest

func get_garment_diagnostics() -> Dictionary:
	var result := super.get_garment_diagnostics()
	result.mode = "cape_like_open_robe"
	result.support_model = "hip_hung_rear_chain_gravity_inertia"
	result.leg_rendering = "original_full_legs_and_boots"
	result.visible_leg_meshes = visible_leg_meshes.size()
	result.hidden_leg_meshes = 0
	result.pants_uv_clipped = false
	result.gait_phase_advance_location = "update_cloth_once_per_clock"
	result.original_split_panels = cape_body_records.size()==2
	result.front_bones_resting = bones.front_cloth.transform == bones.front_cloth.rest and bones.front_hem.transform == bones.front_hem.rest and bones.front_edge.transform == bones.front_edge.rest
	result.rear_anchor = cape_rear_anchor
	result.rear_tip = cape_rear_tip
	result.rear_wind = cape_wind
	result.gait_speed_actual = gait_speed_actual
	result.gait_speed_smoothed = gait_speed_smoothed
	result.gait_cadence_hz = gait_cadence_hz
	result.gait_stride_half = gait_stride_half
	result.gait_lift = gait_lift
	result.gait_phase = gait_phase
	result.gait_amount = gait_amount
	result.gait_style = "slow" if gait_speed_smoothed < 90.0 else ("fast" if gait_speed_smoothed > 180.0 else "regular")
	result.locomotion_style = str(locomotion_style)
	result.arm_swing_scale = arm_swing_scale
	result.arm_speed_correction = arm_speed_correction
	result.reset_count = cape_reset_count
	result.rest_hem_source_y = 1340.0
	result.source_geometry_preserved = true
	return result

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "approved fullbody v21 / hip-hung rear robe inertia"
	return result
