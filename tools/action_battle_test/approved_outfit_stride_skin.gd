extends "reference_walk_skin.gd"

## Independent preview of the original v15 outfit with a broader running gait.
## Keeps the existing sole-aware two-bone IK and project pixel/bone scale.
## The source-inspired pose supplies movement; it never supplies bone lengths.
var approved_stride_ratio := 0.60
var approved_lift_ratio := 0.45
const APPROVED_RUN_SUPPORT := 0.44
var approved_leg_lengths := Vector2.ZERO
var approved_leg_ratio := 0.0
var approved_max_length_error := 0.0
var approved_leg_width_profiles: Dictionary = {}
var approved_source_run_legs: RefCounted

const APPROVED_PANTS_ROOT_SHADER := """shader_type canvas_item;
render_mode unshaded;
void fragment() {
	vec4 pixel = texture(TEXTURE,UV);
	float source_y = UV.y/TEXTURE_PIXEL_SIZE.y;
	// Keep ten opaque drawing pixels above the hip inside the robe; trim only
	// the old rounded cap that otherwise appears through the belt/torso.
	pixel.a *= smoothstep(744.0,750.0,source_y);
	COLOR = pixel;
}
"""

func _leg_points(side: String) -> Array:
	# The original common leg PNG is fitted to the near leg's approved chart.
	# Far is the same physical limb, translated ten drawing pixels for depth.
	var shift := Vector2(10.0,0.0) if side=="far" else Vector2.ZERO
	return [Vector2(475,760)+shift,Vector2(440,1080)+shift,Vector2(423,1417)+shift]

func _approved_leg_width_at(source_y: float) -> float:
	var width := lerpf(1.24,1.18,smoothstep(1045.0,1115.0,source_y))
	width = lerpf(width,1.10,smoothstep(1300.0,1398.0,source_y))
	return lerpf(width,1.0,smoothstep(1398.0,1435.0,source_y))

func _build_approved_leg(side: String) -> void:
	# Static transverse fit before binding: increase fabric volume without
	# changing a bone, its longitudinal ruler, the original image or its UV.
	var rect := [325,660,195,830]
	var points := _leg_points(side)
	var upper_axis: Vector2 = (points[1]-points[0]).normalized()
	var lower_axis: Vector2 = (points[2]-points[1]).normalized()
	var vertices := PackedVector2Array()
	var sets := [PackedFloat32Array(),PackedFloat32Array(),PackedFloat32Array()]
	var width_samples := []
	for row in range(ROWS+1):
		var y := float(rect[1])+float(rect[3])*row/ROWS
		var source_axis := _interpolate(y,[760.0,1080.0,1417.0],[410.0,386.0,384.0])
		var target_axis := _interpolate(y,[760.0,1080.0,1417.0],[points[0].x,points[1].x,points[2].x])
		var calf := smoothstep(1045.0,1115.0,y)
		var foot := smoothstep(1398.0,1435.0,y)
		var axis := upper_axis.lerp(lower_axis,calf).normalized()
		var normal := Vector2(axis.y,-axis.x)
		var width := _approved_leg_width_at(y)
		width_samples.append({"source_y":y,"width_factor":width})
		for column in range(COLUMNS+1):
			var x := float(rect[0])+float(rect[2])*column/COLUMNS
			var original := Vector2(x+target_axis-source_axis,y)
			var axis_point := Vector2(target_axis,y)
			# Only the posterior upper-thigh contour must enter the robe as a
			# narrow root. Keep the visible anterior thigh full, then restore the
			# posterior side completely before the knee blend at1045.
			var fit_width := lerpf(0.20,width,smoothstep(900.0,1040.0,y)) if x<source_axis else width
			var widened := original+normal*(original-axis_point).dot(normal)*(fit_width-1.0)
			vertices.append(_world(widened))
			sets[0].append(1.0-calf)
			sets[1].append(calf*(1.0-foot))
			sets[2].append(calf*foot)
	texture = textures.leg
	_create_mesh(side+"_leg",rect,vertices,[side+"_thigh",side+"_calf",side+"_foot"],sets,35 if side=="near" else 30)
	var shader := Shader.new()
	shader.code = APPROVED_PANTS_ROOT_SHADER
	var material := ShaderMaterial.new()
	material.shader = shader
	meshes.back().material = material
	approved_leg_width_profiles[side] = {"posterior_hip_insertion":0.20,"posterior_hip_taper_source_rows":[900.0,1040.0],"anterior_thigh":1.24,"thigh":1.24,"calf":1.18,"boot_shaft":1.10,"full_foot":1.0,"hip_source_y":760.0,"root_trim_source_y":744.0,"root_fully_opaque_source_y":750.0,"underrobe_overlap_source_pixels":10.0,"static_rest_transverse_only":true,"row_samples":width_samples}

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	var ready := super.build(owner_runtime,art_dir)
	if not ready: return false
	approved_leg_lengths = Vector2(bones.near_calf.rest.origin.length(),bones.near_foot.rest.origin.length())
	approved_leg_ratio = approved_leg_lengths.x/approved_leg_lengths.y
	_fit_approved_stride(1.0)
	approved_source_run_legs = load(get_script().resource_path.get_base_dir().path_join("approved_outfit_source_run_legs.gd")).new()
	if not approved_source_run_legs.build(self):
		validation_errors.append_array(approved_source_run_legs.errors)
		return false
	return true

func _fit_approved_stride(amount: float) -> void:
	if approved_leg_lengths.length_squared()<0.001: return
	var reach := approved_leg_lengths.x+approved_leg_lengths.y
	# Visual fitting choices within the inspected run's fixed-length envelope:
	# source horizontal half-span/total is0.599; full lift/total is0.841.
	# Retain a lower project lift for the old boot/robe and grounded contacts.
	var amplitude := lerpf(0.55,1.0,clampf(amount,0.0,1.0))
	gait_stride_half = reach*approved_stride_ratio*amplitude
	gait_lift = reach*approved_lift_ratio*amplitude

func _advance_gait(clip: String) -> void:
	var should_advance := motion_context and motion_delta>0.0 and motion_clock!=gait_last_clock
	super._advance_gait(clip)
	var amount := sqrt(clampf(gait_speed_smoothed/ROBED_MOVE_SPEED,0.0,1.0)) if motion_context else 1.0
	_fit_approved_stride(amount)
	if should_advance and clip=="Run_New1" and gait_backward<0.35 and approved_source_run_legs!=null:
		gait_cadence_hz = 1.0/float(approved_source_run_legs.profile.duration)
		# Replace the inherited advance from the saved phase; never add a
		# second step. Actor velocity and backward locomotion stay unchanged.
		gait_phase = pending_gait_phase
		if gait_speed_actual>0.01 or gait_amount>0.03:
			gait_phase = fposmod(gait_phase+motion_delta*gait_cadence_hz*clampf(gait_amount/0.35,0.0,1.0),1.0)

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	var old_phase := gait_phase
	var old_facing := motion_facing
	super.set_motion_context(delta,velocity,clock,facing)
	if delta>0.0 and facing!=old_facing:
		var turn_phase := 0.0 if pending_gait_reset else old_phase
		pending_gait_phase = fposmod(APPROVED_RUN_SUPPORT-turn_phase,1.0)
		gait_phase = pending_gait_phase

func _foot_sample(phase: float, backwards: float) -> Vector3:
	var p := fposmod(phase,1.0)
	var x := 0.0
	var lift := 0.0
	var pitch := 0.0
	if p<APPROVED_RUN_SUPPORT:
		var support := p/APPROVED_RUN_SUPPORT
		x = lerpf(gait_stride_half,-gait_stride_half,support)
		pitch = lerpf(-0.08,0.0,smoothstep(0.0,0.18,support))
		pitch += 0.55*smoothstep(0.72,1.0,support)
	else:
		var swing := (p-APPROVED_RUN_SUPPORT)/(1.0-APPROVED_RUN_SUPPORT)
		var recovery := lerpf(smoothstep(0.0,1.0,swing),swing,0.18)
		x = lerpf(-gait_stride_half,gait_stride_half,recovery)
		lift = gait_lift*pow(maxf(0.0,sin(pow(swing,0.80)*PI)),1.25)
		# Roll off the toe, recover the heel early, then aim the sole to land.
		pitch = lerpf(0.55,0.70,smoothstep(0.0,0.18,swing))*(1.0-smoothstep(0.20,0.85,swing))
		pitch -= 0.08*smoothstep(0.50,1.0,swing)
	return Vector3(lerpf(x,x*-0.82,backwards),lerpf(lift,lift*0.55,backwards),lerpf(pitch,-pitch*0.5,backwards)*gait_pitch_scale)

func _solve_leg(side: String, ankle: Vector2, foot_pitch: float) -> void:
	# Parent solves reachable targets with fixed rest lengths and adjusts the
	# pelvis before either leg, keeping the original soles at the same floor.
	if approved_source_run_legs!=null and approved_source_run_legs.should_apply(self):
		approved_source_run_legs.apply()
	else:
		if approved_source_run_legs!=null: approved_source_run_legs.active = false
		super._solve_leg(side,ankle,foot_pitch)
	var hip: Vector2 = runtime.rig_root_node.to_local(bones[side+"_thigh"].global_position)
	var knee: Vector2 = runtime.rig_root_node.to_local(bones[side+"_calf"].global_position)
	var actual_ankle: Vector2 = runtime.rig_root_node.to_local(bones[side+"_foot"].global_position)
	if approved_leg_lengths.length_squared()>0.001:
		approved_max_length_error = maxf(approved_max_length_error,maxf(absf(hip.distance_to(knee)-approved_leg_lengths.x),absf(knee.distance_to(actual_ankle)-approved_leg_lengths.y)))

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	if motion_context and motion_delta<=0.0 and not last_clip.is_empty(): return
	super.update_cloth(clip,time,duration,tracks)
	if approved_source_run_legs!=null: approved_source_run_legs.finalize_pose()

func get_garment_diagnostics() -> Dictionary:
	var result := super.get_garment_diagnostics()
	result.approved_fixed_legs = {"thigh":approved_leg_lengths.x,"calf":approved_leg_lengths.y,"ratio":approved_leg_ratio,"both_sides_same_chart":true,"maximum_length_error":approved_max_length_error,"stride_half":gait_stride_half,"lift":gait_lift,"stride_ratio":approved_stride_ratio,"lift_ratio":approved_lift_ratio,"foot":"independent sole-aware ankle pitch; original project IK","source_pose_lengths_copied":false,"base_art":"yuxu_fullbody_v15"}
	result.walk_support_fraction = APPROVED_RUN_SUPPORT
	result.approved_fixed_legs.support_fraction = APPROVED_RUN_SUPPORT
	result.approved_fixed_legs.legacy_fallback_settings = true
	result.approved_fixed_legs.method = "Forward Run_New1 uses authoritative source_run measured-angle FK/contact band; stride_ratio, lift_ratio, support_fraction and original-IK foot label are inherited backward/transition fallback only"
	result.approved_fixed_legs.width_fit = approved_leg_width_profiles
	if approved_source_run_legs!=null: result.approved_fixed_legs.source_run = approved_source_run_legs.get_diagnostics()
	return result

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "independent original v15 outfit / broad held-arm run on fixed project bones"
	return result
