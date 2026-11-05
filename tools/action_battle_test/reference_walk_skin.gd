extends "cape_like_robe_skin.gd"

## Retarget the locally inspected source wrist silhouette onto our own bones.
## Only ordinary locomotion uses this profile; it grants no guard effect.
const WALK_SUPPORT := 0.54
var guigu_pose_profile: Dictionary = {}
var reference_source_phase := 0.0
var reference_cloth_base := 0.0
var reference_pose_active := false
var reference_pelvis_adjusted := false
var reference_clip := "Idle"
var reference_clock := 0.0
var reference_pelvis_drop := 0.0
var reference_chest_target := 0.0
var reference_lean_inertia := 0.0
var reference_hip_loading := 0.0
var reference_chest_counter := 0.0
var reference_arm_samples: Dictionary = {}
var reference_arm_stride_half := 18.5
var reference_anchor_last_position := Vector2.ZERO
var reference_anchor_last_velocity := Vector2.ZERO
var reference_anchor_acceleration := Vector2.ZERO
var reference_anchor_drive := 0.0
var reference_anchor_samples := 0
var reference_anchor_reset_count := 0
var reference_front_angles := Vector3.ZERO
var reference_front_angular_velocity := Vector3.ZERO
var reference_trailing_wind_load := 0.0
var reference_target_wind_load := 0.0
var reference_front_anchor := Vector2.ZERO
var reference_front_tip := Vector2.ZERO

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	var profile_path: String = get_script().resource_path.get_base_dir().path_join("guigu_run_pose_profile_20261104.json")
	var profile: Variant = JSON.parse_string(FileAccess.get_file_as_string(profile_path))
	if not profile is Dictionary or not profile.has("clips"):
		validation_errors.append("Missing inspected locomotion pose profile: "+profile_path)
		return false
	guigu_pose_profile = profile
	return super.build(owner_runtime,art_dir)

func _source_sample(clip_name: String, phase: float, field: String) -> Variant:
	var samples: Array = guigu_pose_profile.clips[clip_name].samples
	var sample_phase := fposmod(phase,1.0)*samples.size()
	var index := floori(sample_phase)
	var mix := sample_phase-index
	var first: Variant = samples[index%samples.size()][field]
	var second: Variant = samples[(index+1)%samples.size()][field]
	if first is Array:
		return Vector2(float(first[0]),float(first[1])).lerp(Vector2(float(second[0]),float(second[1])),mix)
	return lerpf(float(first),float(second),mix)

func _source_phase(phase: float, clip_name: String = "Run_New1") -> float:
	return fposmod(phase+float(guigu_pose_profile.clips[clip_name].nearFrontmostPhase),1.0)

func _build_approved_body() -> void:
	# Both lower panels hang from the hip. Binding still uses the unchanged
	# original polygon/UV drawing, so the zero-angle standing shape is exact.
	for entry in [["front_cloth",Vector2(0.0,60.0*PIXEL_SCALE)],
		["front_hem",Vector2(0.0,220.0*PIXEL_SCALE)],
		["front_edge",Vector2(0.0,180.0*PIXEL_SCALE)]]:
		var bone: Bone2D = bones[entry[0]]
		bone.position = entry[1]
		bone.rest = bone.transform
	super._build_approved_body()

func _init() -> void:
	gait_cadence_hz = 1.70
	gait_stride_half = 28.0
	gait_lift = 20.0
	gait_pitch_scale = 0.92

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	var previous_phase := gait_phase
	var previous_facing := motion_facing
	super.set_motion_context(delta,velocity,clock,facing)
	if delta>0.0 and (pending_gait_reset or facing!=previous_facing):
		_reset_anchor_history()
	if delta>0.0 and pending_gait_reset:
		reference_front_angles = Vector3.ZERO
		reference_front_angular_velocity = Vector3.ZERO
		reference_trailing_wind_load = 0.0
		reference_target_wind_load = 0.0
	if delta>0.0 and facing!=previous_facing:
		reference_front_angles *= -1.0
		reference_front_angular_velocity *= -1.0
		reference_trailing_wind_load *= -1.0
		reference_target_wind_load *= -1.0
		# Mirror within this walk's longer stance and shorter swing intervals.
		# The inherited 0.5-p mapping would turn a late stance into flight.
		var turn_phase := 0.0 if pending_gait_reset else previous_phase
		pending_gait_phase = fposmod(WALK_SUPPORT-turn_phase,1.0)
		gait_phase = pending_gait_phase
		# A mirrored actor retains its existing world-space torso lean and
		# angular momentum, then follows the new load through its spring.
		if cloth_positions.has("chest"):
			cloth_positions["chest"].x *= -1.0
			cloth_speeds["chest"].x *= -1.0

func _advance_gait(clip: String) -> void:
	if not motion_context or motion_delta<=0.0 or motion_clock==gait_last_clock: return
	super._advance_gait(clip)
	var ratio := sqrt(clampf(gait_speed_smoothed/235.0,0.0,2.5))
	gait_cadence_hz = 1.20+0.50*ratio
	# Cadence and displacement remain unchanged. A deeper rear-foot recovery
	# distinguishes the run silhouette from the previous low walking return.
	reference_arm_stride_half = 12.0+6.5*ratio
	gait_stride_half = 18.0+10.0*ratio
	gait_lift = 14.5+5.5*ratio
	gait_pitch_scale = 0.72+0.20*minf(ratio,1.0)
	# The inherited advance is replaced from the saved phase, never added
	# again. Ordinary travel has one continuous rhythm and no input tiers.
	gait_phase = pending_gait_phase
	if motion_context and motion_delta>0.0 and (gait_speed_actual>0.01 or gait_amount>0.03):
		gait_phase = fposmod(gait_phase+motion_delta*gait_cadence_hz*clampf(gait_amount/0.35,0.0,1.0),1.0)

func _foot_sample(phase: float, backwards: float) -> Vector3:
	var p := fposmod(phase,1.0)
	var x := 0.0
	var lift := 0.0
	var pitch := 0.0
	if p<WALK_SUPPORT:
		var support := p/WALK_SUPPORT
		x = lerpf(gait_stride_half,-gait_stride_half,support)
		pitch = lerpf(-0.055,0.0,smoothstep(0.0,0.18,support))
		pitch += 0.25*smoothstep(0.76,1.0,support)
	else:
		var swing := (p-WALK_SUPPORT)/(1.0-WALK_SUPPORT)
		var recovery := lerpf(smoothstep(0.0,1.0,swing),swing,0.18)
		x = lerpf(-gait_stride_half,gait_stride_half,recovery)
		# Early lift folds the trailing knee before the ankle passes the hip.
		lift = gait_lift*pow(maxf(0.0,sin(pow(swing,0.80)*PI)),1.25)
		pitch = lerpf(0.25,-0.055,smoothstep(0.0,0.88,swing))
	return Vector3(lerpf(x,x*-0.82,backwards),lerpf(lift,lift*0.55,backwards),lerpf(pitch,-pitch*0.5,backwards)*gait_pitch_scale)

func _cloth_spring(key: String, target: Vector2, live: bool, stiffness: float = 190.0, damping: float = 18.0) -> Vector2:
	if key=="chest" and reference_pose_active:
		var amount := gait_amount if live else (1.0 if reference_clip in ["Run_New1","Run_New2","BackstepWalk"] else 0.0)
		var reverse := gait_backward if live else (1.0 if reference_clip=="BackstepWalk" else 0.0)
		var speed_ratio := clampf(gait_speed_actual/235.0,0.0,1.3) if live else 0.65
		var travelling := clampf(gait_speed_actual/30.0,0.0,1.0) if live else amount
		reference_lean_inertia = clampf(garment_drive*0.030,-0.090,0.090) if live else 0.0
		var phase := gait_phase if live else fposmod(reference_clock/(0.70 if reference_clip=="BackstepWalk" else 0.5833333),1.0)
		reference_source_phase = _source_phase(phase)
		# Compensate our drawing's rearward rest shoulder offset. The source
		# lean is measured from its waist/neck line, not a raw local rotation.
		var source_forward: float = _source_sample("Run_New1",reference_source_phase,"body_lean")
		var source_backward: float = _source_sample("Run_New2",_source_phase(phase,"Run_New2"),"body_lean")
		var forward_lean := (source_forward+0.085)*(0.94+0.06*minf(speed_ratio,1.0))
		var backward_lean := source_backward+0.045
		reference_chest_counter = 0.0
		target.x = lerpf(forward_lean,backward_lean,reverse)*travelling+reference_lean_inertia
		reference_chest_target = target.x
		# A moderately damped load-response allows startup lean, braking
		# counter-sway and settling without changing the arm's absolute aim.
		return super._cloth_spring(key,target,live,145.0,13.0)
	return super._cloth_spring(key,target,live,stiffness,damping)

func _reset_anchor_history() -> void:
	if reference_anchor_samples>0: reference_anchor_reset_count += 1
	reference_anchor_samples = 0
	reference_anchor_last_position = Vector2.ZERO
	reference_anchor_last_velocity = Vector2.ZERO
	reference_anchor_acceleration = Vector2.ZERO
	reference_anchor_drive = 0.0

func _sample_anchor_drive() -> float:
	if not reference_pose_active or motion_delta<=0.0:
		_reset_anchor_history()
		return 0.0
	# Read the real hip attachment, before any cloth rotation is applied.
	# Actor translation already drives garment_drive, so only the local
	# attachment motion supplies this small additional gait-load impulse.
	var anchor: Vector2 = runtime.rig_root_node.to_local(bones.pelvis.to_global(bones.back_cloth.rest.origin))
	if reference_anchor_samples==0:
		reference_anchor_last_position = anchor
		reference_anchor_samples = 1
		return 0.0
	var velocity := (anchor-reference_anchor_last_position)/motion_delta
	reference_anchor_last_position = anchor
	if reference_anchor_samples==1:
		reference_anchor_last_velocity = velocity
		reference_anchor_samples = 2
		return 0.0
	var acceleration := ((velocity-reference_anchor_last_velocity)/motion_delta).limit_length(350.0)
	reference_anchor_last_velocity = velocity
	reference_anchor_acceleration = reference_anchor_acceleration.lerp(acceleration,1.0-exp(-16.0*motion_delta))
	reference_anchor_drive = clampf(reference_anchor_acceleration.x/300.0,-0.65,0.65)
	return reference_anchor_drive

func _integrate_garment() -> void:
	# Travel maintains a visible trailing form; it is not merely a source of
	# tiny oscillations. Gravity and seam elasticity still return both panels
	# to the unchanged hanging pose once the actual travel load disappears.
	var anchor_drive := _sample_anchor_drive()
	# The source clip supplies the root's changing running silhouette. Only
	# the root receives this gait load; the free middle/tip still lag behind.
	var source_cloth: float = _source_sample("Run_New1",_source_phase(gait_phase),"cloth_root_angle")
	reference_cloth_base = clampf(0.62+(source_cloth-0.75)*0.70,0.52,0.76)
	reference_target_wind_load = signf(cape_wind)*sqrt(absf(cape_wind))
	var opposite_load := reference_target_wind_load*reference_trailing_wind_load<0.0
	var unloading := absf(reference_target_wind_load)<absf(reference_trailing_wind_load)
	var load_time := 0.14 if opposite_load else (0.20 if unloading else 0.08)
	reference_trailing_wind_load = lerpf(reference_trailing_wind_load,reference_target_wind_load,1.0-exp(-motion_delta/load_time))
	var dt := motion_delta/8.0
	for substep in range(8):
		var parent_angle := 0.0
		var parent_speed := 0.0
		for segment in range(3):
			var stiffness: float = [65.0,39.0,22.0][segment]
			var gravity: float = [29.0,24.0,17.0][segment]
			var damping: float = [10.2,8.4,4.6][segment]
			var inertia: float = [4.0,7.0,10.0][segment]
			var root_target := reference_cloth_base+29.0/65.0*sin(reference_cloth_base)
			var target := reference_trailing_wind_load*float([root_target,0.23,0.28][segment])
			var angle := garment_angles[segment]
			var torque := (target-angle)*stiffness-gravity*sin(parent_angle+angle)
			torque += garment_drive*inertia*cos(parent_angle+angle)-parent_speed*1.8
			torque += anchor_drive*float([0.0,1.2,3.0][segment])
			garment_angular_velocity[segment] += (torque-garment_angular_velocity[segment]*damping)*dt
			angle += garment_angular_velocity[segment]*dt
			var limit: float = [0.90,0.34,0.38][segment]
			if absf(angle)>limit:
				angle = clampf(angle,-limit,limit)
				garment_angular_velocity[segment] *= 0.25
			garment_angles[segment] = angle
			parent_angle += angle
			parent_speed += garment_angular_velocity[segment]
		# The foreground facing must also follow: otherwise it hides the rear
		# deformation and leaves a rigid vertical edge. It is more sheltered
		# in forward travel, and becomes the visible trailing side in reverse.
		parent_angle = 0.0
		parent_speed = 0.0
		var exposure := 0.70 if reference_trailing_wind_load>=0.0 else 1.0
		for segment in range(3):
			var stiffness: float = [90.0,58.0,40.0][segment]
			var gravity: float = [34.0,28.0,21.0][segment]
			var damping: float = [13.0,9.8,7.2][segment]
			var inertia: float = [2.0,4.0,6.0][segment]
			var front_root_target := reference_cloth_base+34.0/90.0*sin(reference_cloth_base)
			var target := reference_trailing_wind_load*exposure*float([front_root_target,0.21,0.25][segment])
			var angle := reference_front_angles[segment]
			var torque := (target-angle)*stiffness-gravity*sin(parent_angle+angle)
			torque += garment_drive*inertia*cos(parent_angle+angle)-parent_speed*1.8
			torque += anchor_drive*float([0.0,0.6,1.4][segment])
			reference_front_angular_velocity[segment] += (torque-reference_front_angular_velocity[segment]*damping)*dt
			angle += reference_front_angular_velocity[segment]*dt
			var limit: float = [0.72,0.32,0.38][segment]
			if absf(angle)>limit:
				angle = clampf(angle,-limit,limit)
				reference_front_angular_velocity[segment] *= 0.25
			reference_front_angles[segment] = angle
			parent_angle += angle
			parent_speed += reference_front_angular_velocity[segment]
	garment_simulation_steps += 1

func _reference_far_target(amount: float, reverse: float, phase: float, live: bool) -> Dictionary:
	var sample := _foot_sample(fposmod(phase+0.5,1.0),reverse)
	if live: sample.x *= gait_horizontal
	var pitch := sample.z*amount
	var sole := _sole_contact("far",pitch)
	var ankle := Vector2(lerpf(_world(_leg_points("far")[2]).x,sample.x,amount),-sole.y-sample.y*amount)
	return {"ankle":ankle,"pitch":pitch}

func _solve_leg(side: String, ankle: Vector2, foot_pitch: float) -> void:
	if reference_pose_active and not reference_pelvis_adjusted:
		reference_pelvis_adjusted = true
		var live := motion_context and pose_blending
		var amount := gait_amount if live else (1.0 if reference_clip in ["Run_New1","Run_New2","BackstepWalk"] else 0.0)
		var reverse := gait_backward if live else (1.0 if reference_clip=="BackstepWalk" else 0.0)
		var phase := gait_phase if live else fposmod(reference_clock/(0.70 if reference_clip=="BackstepWalk" else 0.5833333),1.0)
		var clock := motion_clock if live else reference_clock
		var breathing := sin(clock*TAU/3.4)*0.24*(1.0-amount)
		# Low weight transfer replaces the parent's constant crouch. Reach is
		# then checked against BOTH original sole targets before solving either
		# fixed-length leg, preventing floating shoes after raising the body.
		reference_hip_loading = sin((phase-0.08)*TAU)*amount
		bones.pelvis.position = bones.pelvis.rest.origin+Vector2(lerpf(1.8,-0.90,reverse)*amount+reference_hip_loading*1.00,1.35*amount+0.14*cos((phase-0.18)*TAU*2.0)*amount+breathing)
		# Lean about the hip instead of only rotating the top of the chest.
		# The pelvis and both original sole targets stay in the IK solution.
		bones.torso.position = bones.torso.rest.origin.rotated(bones.torso.rotation*amount)+Vector2(-reference_hip_loading*0.40,0.0)
		var far_target := _reference_far_target(amount,reverse,phase,live)
		for target in [{"side":side,"ankle":ankle},{"side":"far","ankle":far_target.ankle}]:
			var hip: Vector2 = runtime.rig_root_node.to_local(bones[target.side+"_thigh"].global_position)
			var reach: float = bones[target.side+"_calf"].rest.origin.length()+bones[target.side+"_foot"].rest.origin.length()-0.12
			var required_y: float = target.ankle.y-sqrt(maxf(1.0,reach*reach-pow(target.ankle.x-hip.x,2.0)))
			bones.pelvis.position.y += maxf(0.0,required_y-hip.y)
		reference_pelvis_drop = bones.pelvis.position.y-bones.pelvis.rest.origin.y
	super._solve_leg(side,ankle,foot_pitch)

func _pose_guard_arm(side: String, phase: float, amount: float, reverse: float, breathe: float, impulse: float, attack_phase: float, dash: float) -> void:
	super._pose_guard_arm(side,phase,amount,reverse,breathe,impulse,attack_phase,dash)
	if not reference_pose_active or amount<=0.001: return
	if attack_phase>=0.0 or last_clip=="Step1" or action_guard or action_preparing or combo_stage>0 or gesture_ready>0.04: return
	# Both arms sample the same source frame. The caller supplies each leg's
	# own phase, so remove the far leg's half-cycle before source alignment.
	var global_phase := phase-(0.5 if side=="far" else 0.0)
	var source_phase := _source_phase(global_phase)
	var reverse_phase := _source_phase(global_phase,"Run_New2")
	var field := "near_wrist" if side=="near" else "far_wrist"
	var forward_target: Vector2 = _source_sample("Run_New1",source_phase,field)
	var backward_target: Vector2 = _source_sample("Run_New2",reverse_phase,field)
	var normalized_target := forward_target.lerp(backward_target,reverse)
	var upper: Bone2D = bones[side+"_upper"]
	var lower: Bone2D = bones[side+"_forearm"]
	var hand: Bone2D = bones[side+"_hand"]
	var upper_length := lower.rest.origin.length()
	var lower_length := hand.rest.origin.length()
	var offset := normalized_target*(upper_length+lower_length)
	var distance := clampf(offset.length(),absf(upper_length-lower_length)+0.05,upper_length+lower_length-0.05)
	offset = offset.normalized()*distance
	var hinge := acos(clampf((upper_length*upper_length+distance*distance-lower_length*lower_length)/(2.0*upper_length*distance),-1.0,1.0))
	var shoulder_target := offset.angle()+hinge
	var elbow_target := (offset-Vector2.from_angle(shoulder_target)*upper_length).angle()
	var wrist_target := -0.025 if side=="near" else 0.025
	var upper_angle: float = upper.rotation+lower.rest.origin.angle()+bones.torso.rotation
	var lower_angle: float = lower.rotation+hand.rest.origin.angle()+upper.rotation+bones.torso.rotation
	upper.position = upper.rest.origin+Vector2(0.12*sin(source_phase*TAU)*amount,-0.08*amount)
	_aim_arm(side,lerpf(upper_angle,shoulder_target,amount),lerpf(lower_angle,elbow_target,amount),lerpf(hand.rotation,wrist_target,amount))
	arm_swing_scale = 1.0
	arm_speed_correction = Vector3.ZERO
	reference_arm_samples[side] = {"leg_phase":phase,"source_phase":source_phase,"normalized_wrist_target":normalized_target,"shoulder_angle":shoulder_target,"forearm_angle":elbow_target,"elbow_flex":absf(wrapf(shoulder_target-elbow_target,-PI,PI))}

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	if motion_context and motion_delta<=0.0 and last_clip!="": return
	reference_clip = clip
	reference_clock = time
	reference_pelvis_adjusted = false
	reference_arm_samples.clear()
	var ordinary_pose := clip in ["Idle","Run_New1","Run_New2","BackstepWalk"] and not action_guard and not action_preparing and combo_stage==0 and locomotion_style==&"ground"
	if ordinary_pose!=reference_pose_active: _reset_anchor_history()
	reference_pose_active = ordinary_pose
	super.update_cloth(clip,time,duration,tracks)
	if clip=="BindPose": return
	if not motion_context or not pose_blending:
		reference_front_angles = Vector3.ZERO
		reference_front_angular_velocity = Vector3.ZERO
	for index in range(3):
		bones[["front_cloth","front_hem","front_edge"][index]].rotation = reference_front_angles[index]
	# One-sided floor correction never extends the source robe downward.
	garment_floor_max = _garment_lowest_point()
	if garment_floor_max> -0.5:
		bones.front_cloth.position.y -= garment_floor_max+0.5
		garment_floor_max = _garment_lowest_point()
	reference_front_anchor = runtime.rig_root_node.to_local(bones.front_cloth.global_position)
	reference_front_tip = runtime.rig_root_node.to_local(bones.front_edge.to_global(Vector2(0.0,120.0*PIXEL_SCALE)))
	if reference_pose_active:
		shadow_center = bones.pelvis.position.x*0.38
		contact_shadow.queue_redraw()

func get_garment_diagnostics() -> Dictionary:
	var result := super.get_garment_diagnostics()
	result.walk_profile = "guigu_guard_run_retargeted_to_project_bone_lengths"
	result.walk_support_fraction = WALK_SUPPORT
	result.reference_pose_active = reference_pose_active
	result.reference_pelvis_drop = reference_pelvis_drop
	result.reference_chest_target = reference_chest_target
	result.reference_lean_inertia = reference_lean_inertia
	result.reference_chest_angle = cloth_positions.get("chest",Vector2.ZERO).x
	result.reference_chest_velocity = cloth_speeds.get("chest",Vector2.ZERO).x
	result.reference_elastic_tail = true
	result.reference_near_arm_pose = "source_youbi_held_guard_pose_only"
	result.reference_far_arm_pose = "source_zuobi_rear_swing"
	result.reference_source_phase = reference_source_phase
	result.reference_cloth_base = reference_cloth_base
	result.reference_arm_samples = reference_arm_samples
	result.reference_arm_stride_half = reference_arm_stride_half
	result.reference_anchor_acceleration = reference_anchor_acceleration
	result.reference_anchor_drive = reference_anchor_drive
	result.reference_anchor_samples = reference_anchor_samples
	result.reference_anchor_reset_count = reference_anchor_reset_count
	result.reference_trailing_wind_load = reference_trailing_wind_load
	result.reference_target_wind_load = reference_target_wind_load
	result.reference_front_angles = reference_front_angles
	result.reference_front_angular_velocity = reference_front_angular_velocity
	result.reference_rear_tip_from_anchor = cape_rear_tip-cape_rear_anchor
	result.reference_front_tip_from_anchor = reference_front_tip-reference_front_anchor
	result.reference_hip_loading = reference_hip_loading
	result.reference_chest_counter = reference_chest_counter
	var near_hand: Vector2 = runtime.rig_root_node.to_local(bones.near_hand.global_position)
	var near_shoulder: Vector2 = runtime.rig_root_node.to_local(bones.near_upper.global_position)
	var far_hand: Vector2 = runtime.rig_root_node.to_local(bones.far_hand.global_position)
	var far_shoulder: Vector2 = runtime.rig_root_node.to_local(bones.far_upper.global_position)
	result.reference_near_hand_from_shoulder = near_hand-near_shoulder
	result.reference_far_hand_from_shoulder = far_hand-far_shoulder
	var hip_center: Vector2 = (runtime.rig_root_node.to_local(bones.near_thigh.global_position)+runtime.rig_root_node.to_local(bones.far_thigh.global_position))*0.5
	var shoulder_center := (near_shoulder+far_shoulder)*0.5
	var load_axis := shoulder_center-hip_center
	result.reference_measured_body_lean = atan2(load_axis.x,-load_axis.y)
	result.reference_parameter_source = "local_verified_source_wrist_and_lean_with_project_ik_and_cloth_fit"
	return result

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "approved fullbody v21 / source-inspired held-arm run and shared robe trailing"
	return result
