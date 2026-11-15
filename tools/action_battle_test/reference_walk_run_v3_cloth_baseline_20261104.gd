extends "cape_like_robe_skin.gd"

## Project-authored single movement with readable running lean and arm swing.
## Values are visual fitting parameters, not measurements from the video.
const WALK_SUPPORT := 0.62
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

func _init() -> void:
	gait_cadence_hz = 1.70
	gait_stride_half = 26.0
	gait_lift = 10.6
	gait_pitch_scale = 0.92

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	var previous_phase := gait_phase
	var previous_facing := motion_facing
	super.set_motion_context(delta,velocity,clock,facing)
	if delta>0.0 and (pending_gait_reset or facing!=previous_facing):
		_reset_anchor_history()
	if delta>0.0 and facing!=previous_facing:
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
	# Keep the accepted v2 arm excursion while the legs take a larger step.
	reference_arm_stride_half = 12.0+6.5*ratio
	gait_stride_half = 16.0+10.0*ratio
	gait_lift = 7.2+3.4*ratio
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
		pitch += 0.14*smoothstep(0.80,1.0,support)
	else:
		var swing := (p-WALK_SUPPORT)/(1.0-WALK_SUPPORT)
		var recovery := lerpf(smoothstep(0.0,1.0,swing),swing,0.18)
		x = lerpf(-gait_stride_half,gait_stride_half,recovery)
		lift = gait_lift*pow(maxf(0.0,sin(swing*PI)),1.5)
		pitch = lerpf(0.14,-0.055,smoothstep(0.0,0.88,swing))
	return Vector3(lerpf(x,-x,backwards),lerpf(lift,lift*0.85,backwards),lerpf(pitch,-pitch*0.5,backwards)*gait_pitch_scale)

func _cloth_spring(key: String, target: Vector2, live: bool, stiffness: float = 190.0, damping: float = 18.0) -> Vector2:
	if key=="chest" and reference_pose_active:
		var amount := gait_amount if live else (1.0 if reference_clip in ["Run_New1","Run_New2","BackstepWalk"] else 0.0)
		var reverse := gait_backward if live else (1.0 if reference_clip=="BackstepWalk" else 0.0)
		var speed_ratio := clampf(gait_speed_actual/235.0,0.0,1.3) if live else 0.65
		var travelling := clampf(gait_speed_actual/30.0,0.0,1.0) if live else amount
		# The rest drawing's shoulder center sits behind its hip center.
		# The torso controller includes that rest offset so the actual visible
		# shoulder-to-hip line reads about 16--20 degrees at 114 / 235.
		var forward_lean := 0.320+0.130*speed_ratio
		var backward_lean := -0.045-0.045*speed_ratio
		reference_lean_inertia = clampf(garment_drive*0.030,-0.090,0.090) if live else 0.0
		var phase := gait_phase if live else fposmod(reference_clock/(0.70 if reference_clip=="BackstepWalk" else 0.5833333),1.0)
		reference_chest_counter = -0.022*sin((phase-0.08)*TAU)*amount
		target.x = lerpf(forward_lean,backward_lean,reverse)*travelling+reference_lean_inertia+reference_chest_counter
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
	# The waist and middle segment retain the established cape response.
	# A softer, less damped final segment stores and releases a small amount
	# of extra elastic energy after acceleration, turns and braking.
	var anchor_drive := _sample_anchor_drive()
	var dt := motion_delta/8.0
	for substep in range(8):
		var parent_angle := 0.0
		var parent_speed := 0.0
		for segment in range(3):
			var stiffness: float = [65.0,39.0,22.0][segment]
			var gravity: float = [29.0,24.0,17.0][segment]
			var damping: float = [10.2,8.4,4.6][segment]
			var inertia: float = [4.0,7.0,10.0][segment]
			var target := cape_wind*float([0.065,0.115,0.145][segment])
			var angle := garment_angles[segment]
			var torque := (target-angle)*stiffness-gravity*sin(parent_angle+angle)
			torque += garment_drive*inertia*cos(parent_angle+angle)-parent_speed*1.8
			torque += anchor_drive*float([0.0,1.2,3.0][segment])
			garment_angular_velocity[segment] += (torque-garment_angular_velocity[segment]*damping)*dt
			angle += garment_angular_velocity[segment]*dt
			var limit: float = [0.18,0.28,0.34][segment]
			if absf(angle)>limit:
				angle = clampf(angle,-limit,limit)
				garment_angular_velocity[segment] *= 0.25
			garment_angles[segment] = angle
			parent_angle += angle
			parent_speed += garment_angular_velocity[segment]
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
	var scale := clampf(reference_arm_stride_half/18.5,0.80,1.25)*lerpf(1.0,0.80,reverse)
	# The parent passes this side's LEG phase (far = near + one half cycle).
	# In +y-down coordinates an angle above PI/2 points backwards. A forward
	# same-side foot therefore increases this shoulder angle and sends the
	# hand back. Both arms share this law; neither has a fixed front/back role.
	var leg_projection := clampf(_foot_sample(phase,reverse).x/maxf(gait_stride_half,0.001),-1.0,1.0)
	# Keep the rear elbow flexed instead of dropping a nearly straight arm.
	# Extra rear shoulder travel lets the bent hand still return behind the
	# waist; the exact same continuous law is shared by both sides.
	var rear_follow := smoothstep(0.0,1.0,maxf(0.0,leg_projection))
	var shoulder_target := 1.62+leg_projection*0.64*scale+rear_follow*0.50*scale
	var elbow_flex := 1.32-leg_projection*0.12
	var elbow_target := shoulder_target-elbow_flex
	var wrist_target := 0.015-sin((phase-0.12)*TAU)*0.018
	var upper: Bone2D = bones[side+"_upper"]
	var lower: Bone2D = bones[side+"_forearm"]
	var hand: Bone2D = bones[side+"_hand"]
	var upper_angle: float = upper.rotation+lower.rest.origin.angle()+bones.torso.rotation
	var lower_angle: float = lower.rotation+hand.rest.origin.angle()+upper.rotation+bones.torso.rotation
	shoulder_target += bones.torso.rotation*0.20
	elbow_target += bones.torso.rotation*0.20
	upper.position = upper.rest.origin+Vector2(0.15*leg_projection*amount,-0.08*amount)
	_aim_arm(side,lerpf(upper_angle,shoulder_target,amount),lerpf(lower_angle,elbow_target,amount),lerpf(hand.rotation,wrist_target,amount))
	arm_swing_scale = scale
	arm_speed_correction = Vector3.ZERO
	reference_arm_samples[side] = {"leg_phase":phase,"same_side_foot_projection":leg_projection,"shoulder_angle":shoulder_target,"forearm_angle":elbow_target,"elbow_flex":elbow_flex}

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
	if reference_pose_active:
		shadow_center = bones.pelvis.position.x*0.38
		contact_shadow.queue_redraw()

func get_garment_diagnostics() -> Dictionary:
	var result := super.get_garment_diagnostics()
	result.walk_profile = "reference_run_like_single_movement_project_fit"
	result.walk_support_fraction = WALK_SUPPORT
	result.reference_pose_active = reference_pose_active
	result.reference_pelvis_drop = reference_pelvis_drop
	result.reference_chest_target = reference_chest_target
	result.reference_lean_inertia = reference_lean_inertia
	result.reference_chest_angle = cloth_positions.get("chest",Vector2.ZERO).x
	result.reference_chest_velocity = cloth_speeds.get("chest",Vector2.ZERO).x
	result.reference_elastic_tail = true
	result.reference_near_arm_pose = "alternating_contralateral_swing"
	result.reference_arm_samples = reference_arm_samples
	result.reference_arm_stride_half = reference_arm_stride_half
	result.reference_anchor_acceleration = reference_anchor_acceleration
	result.reference_anchor_drive = reference_anchor_drive
	result.reference_anchor_samples = reference_anchor_samples
	result.reference_anchor_reset_count = reference_anchor_reset_count
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
	result.reference_parameter_source = "project_visual_fit_not_video_measurement"
	return result

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "approved fullbody v21 / single run-like movement with rear inertia"
	return result
