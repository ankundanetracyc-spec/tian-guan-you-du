extends "realtime_garment_skin.gd"

## Independent visual candidates: configure robe_profile before build().
## The live runtime continues using realtime_garment_skin.gd unchanged.
const PROFILE_A := &"A"
const PROFILE_B := &"B"
const PROFILE_C := &"C"
var robe_profile: StringName = PROFILE_A
var hidden_leg_meshes: Array[Polygon2D] = []
var profile_settings: Dictionary = {}

func _settings_for_profile() -> Dictionary:
	match robe_profile:
		PROFILE_A:
			return {"label":"直垂收口", "hem_width":0.67, "knee_width":0.88, "front_wrap":0.0,
				"stiffness":[112.0,66.0,39.0], "damping":[14.8,10.7,8.1],
				"gravity":[38.0,34.0,30.0], "inertia":[18.0,24.0,27.0],
				"limit":[0.10,0.15,0.18], "travel":[0.026,0.036,0.038]}
		PROFILE_B:
			return {"label":"小幅钟形", "hem_width":0.87, "knee_width":0.96, "front_wrap":0.0,
				"stiffness":[90.0,48.0,28.0], "damping":[12.4,8.5,6.5],
				"gravity":[35.0,31.0,27.0], "inertia":[20.0,29.0,35.0],
				"limit":[0.125,0.20,0.23], "travel":[0.034,0.048,0.047]}
		PROFILE_C:
			return {"label":"厚重叠摆", "hem_width":0.75, "knee_width":0.91, "front_wrap":0.11,
				"stiffness":[128.0,83.0,58.0], "damping":[18.0,14.2,11.8],
				"gravity":[42.0,39.0,36.0], "inertia":[20.0,25.0,28.0],
				"limit":[0.09,0.12,0.14], "travel":[0.021,0.028,0.027]}
	return {}

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	profile_settings = _settings_for_profile()
	if profile_settings.is_empty():
		validation_errors.append("Unknown closed robe candidate: "+str(robe_profile))
		return false
	garment_mode = CLOSED_ROBE
	var ready := super.build(owner_runtime,art_dir)
	# All articulated leg meshes, original weights and sole profiles remain.
	# Closed candidates suppress their rendering to meet the explicit zero-shoe
	# requirement even when the source alpha edge curves above a planted sole.
	# This is a garment-specific display policy, not certified cloth collision.
	for mesh in meshes:
		if mesh.name in ["YuxuUnified_near_leg","YuxuUnified_far_leg"]:
			hidden_leg_meshes.append(mesh)
	_hide_enclosed_legs()
	return ready and hidden_leg_meshes.size() == 2

func _build_approved_body() -> void:
	super._build_approved_body()
	var fitted := garment_body.polygon
	for index in range(fitted.size()):
		var source: Vector2 = garment_body.uv[index]
		# Upper torso, waist and their UV artwork are untouched. The lower
		# silhouette converges smoothly to a narrower, floor-length shell.
		var skirt := smoothstep(760.0,1030.0,source.y)
		var lower := smoothstep(1010.0,1340.0,source.y)
		var width := lerpf(1.0,float(profile_settings.knee_width),skirt)
		width = lerpf(width,float(profile_settings.hem_width),lower)
		# C broadens the existing blue front facing without adding a free panel,
		# seam, slit or duplicate layer. The surface stays one continuous mesh.
		var wrap := float(profile_settings.front_wrap)*lower*smoothstep(420.0,545.0,source.x)
		fitted[index].x = (source.x-475.0)*PIXEL_SCALE*(width+wrap)
	garment_body.polygon = fitted

func _hide_enclosed_legs() -> void:
	for mesh in hidden_leg_meshes: mesh.visible = false

func set_visible(value: bool) -> void:
	super.set_visible(value)
	_hide_enclosed_legs()

func _foot_sample(phase: float, backwards: float) -> Vector3:
	var sample := super._foot_sample(phase,backwards)
	# Keep the original cadence and arena speed while feet shuffle inside.
	sample.x *= 0.70
	sample.y *= 0.50
	sample.z *= 0.65
	return sample

func _solve_leg(side: String, ankle: Vector2, foot_pitch: float) -> void:
	ankle.x = clampf(ankle.x,-4.2,3.2)
	super._solve_leg(side,ankle,foot_pitch)

func _integrate_garment() -> void:
	# Three coupled pendulums retain start/turn/stop momentum. Each candidate
	# differs in cloth stiffness/damping/range, never movement or skill timing.
	var dt := motion_delta/6.0
	var knee_near: Vector2 = runtime.rig_root_node.to_local(bones.near_calf.global_position)
	var knee_far: Vector2 = runtime.rig_root_node.to_local(bones.far_calf.global_position)
	var contact_bias := clampf((knee_near.x+knee_far.x)*0.0008,-0.025,0.025)*gait_amount
	for substep in range(6):
		var parent_angle := 0.0
		var parent_speed := 0.0
		for segment in range(3):
			var stiffness: float = profile_settings.stiffness[segment]
			var damping: float = profile_settings.damping[segment]
			var gravity: float = profile_settings.gravity[segment]
			var inertia: float = profile_settings.inertia[segment]
			var limit: float = profile_settings.limit[segment]
			var target: float = garment_air_drag*float(profile_settings.travel[segment])+contact_bias
			var angle := garment_angles[segment]
			var absolute_angle := parent_angle+angle
			var torque := (target-angle)*stiffness-gravity*sin(absolute_angle)
			torque += garment_drive*inertia*cos(absolute_angle)-parent_speed*2.3
			garment_angular_velocity[segment] += (torque-garment_angular_velocity[segment]*damping)*dt
			angle += garment_angular_velocity[segment]*dt
			if absf(angle)>limit:
				angle = clampf(angle,-limit,limit)
				garment_angular_velocity[segment] *= 0.35
			garment_angles[segment] = angle
			parent_angle += angle
			parent_speed += garment_angular_velocity[segment]
	garment_simulation_steps += 1

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	super.update_cloth(clip,time,duration,tracks)
	_hide_enclosed_legs()

func get_garment_diagnostics() -> Dictionary:
	var result := super.get_garment_diagnostics()
	result.profile = str(robe_profile)
	result.profile_label = str(profile_settings.get("label",""))
	result.leg_rendering = "enclosed_hidden"
	result.hidden_leg_meshes = hidden_leg_meshes.size()
	result.complete_leg_bones = leg_chains.size() == 2 and leg_chains.near.size() == 3 and leg_chains.far.size() == 3
	result.hem_width = profile_settings.get("hem_width",0.0)
	result.angular_limits = profile_settings.get("limit",[])
	result.damping = profile_settings.get("damping",[])
	return result

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "approved fullbody v15 / closed robe independent candidates"
	return result
