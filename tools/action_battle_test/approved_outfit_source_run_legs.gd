extends RefCounted

## Male101 evaluated joint timing, retargeted onto fixed project leg lengths.
## Source feet may be airborne: only a measured low-foot band receives floor
## contact correction. This never pulls every source frame down to the floor.
const PROFILE := "res://approved_guigu_run_profile_20261105.json"
const SOURCE_TOTAL := 27.778198183301956+28.63288688291839
var owner_ref: WeakRef
var profile: Dictionary = {}
var errors: Array[String] = []
var fixed_lengths := Vector2.ZERO
var source_scale := 1.0
var mean_hip := Vector2.ZERO
var project_hip_center := Vector2.ZERO
var floor_bias := 0.0
var contact_band := 3.0
var contact_fade := 1.25
var active := false
var last: Dictionary = {}

func adapted_foot_pitch(raw_angle: float) -> float:
	var angle := wrapf(raw_angle,-PI,PI)
	var onset := deg_to_rad(75.0)
	var range := deg_to_rad(140.0-75.0)
	return onset+range*tanh((angle-onset)/range) if angle>onset else angle

func _v(value: Array) -> Vector2:
	return Vector2(float(value[0]),float(value[1]))

func build(skin: RefCounted) -> bool:
	owner_ref = weakref(skin)
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(get_script().resource_path.get_base_dir().path_join(PROFILE.get_file())))
	if not parsed is Dictionary or not parsed.has("samples"):
		errors.append("Missing measured male101 run profile")
		return false
	profile = parsed
	fixed_lengths = skin.approved_leg_lengths
	source_scale = (fixed_lengths.x+fixed_lengths.y)/SOURCE_TOTAL
	contact_band *= source_scale
	contact_fade *= source_scale
	project_hip_center = (skin.bones.near_thigh.rest.origin+skin.bones.far_thigh.rest.origin)*0.5
	for sample in profile.samples: mean_hip += _v(sample.pelvis)
	mean_hip /= float(profile.samples.size())
	var maximum_sole_y := -INF
	for sample in profile.samples:
		for side in ["near","far"]:
			var pose := _reference_pose(sample,side,skin)
			maximum_sole_y = maxf(maximum_sole_y,float(pose.sole_y))
	floor_bias = -maximum_sole_y
	return true

func sample_at(phase: float) -> Dictionary:
	var cursor := fposmod(phase,1.0)*float(profile.samples.size())
	var index := floori(cursor)
	var blend := cursor-float(index)
	var first: Dictionary = profile.samples[index%profile.samples.size()]
	var second: Dictionary = profile.samples[(index+1)%profile.samples.size()]
	var result := {"phase":fposmod(phase,1.0)}
	for key in ["pelvis","near_hip","far_hip"]: result[key] = _v(first[key]).lerp(_v(second[key]),blend)
	for key in ["near_leg_upper_angle","near_leg_lower_angle","far_leg_upper_angle","far_leg_lower_angle","near_foot_angle","far_foot_angle"]:
		result[key] = lerp_angle(float(first[key]),float(second[key]),blend)
	return result

func _sample_v(sample: Dictionary, key: String) -> Vector2:
	return sample[key] if sample[key] is Vector2 else _v(sample[key])

func _reference_pose(sample: Dictionary, side: String, skin: Variant) -> Dictionary:
	var hip: Vector2 = skin.bones.pelvis.rest.origin+skin.bones[side+"_thigh"].rest.origin+(_sample_v(sample,"pelvis")-mean_hip)*source_scale
	var knee: Vector2 = hip+Vector2.from_angle(float(sample[side+"_leg_upper_angle"]))*fixed_lengths.x
	var ankle: Vector2 = knee+Vector2.from_angle(float(sample[side+"_leg_lower_angle"]))*fixed_lengths.y
	var pitch := adapted_foot_pitch(float(sample[side+"_foot_angle"]))
	var sole: Vector2 = skin._sole_contact(side,pitch)
	return {"hip":hip,"knee":knee,"ankle":ankle,"pitch":pitch,"sole_y":ankle.y+sole.y}

func should_apply(skin: Variant) -> bool:
	if profile.is_empty() or str(skin.locomotion_style)!="ground": return false
	if str(skin.last_clip) not in ["Run_New1","Idle"]: return false
	var live: bool = skin.motion_context and skin.pose_blending
	return (float(skin.gait_amount)>0.01 and float(skin.gait_backward)<0.35) if live else str(skin.last_clip)=="Run_New1"

func apply() -> void:
	var skin: Variant = owner_ref.get_ref()
	if skin==null: return
	active = should_apply(skin)
	if not active: return
	var live: bool = skin.motion_context and skin.pose_blending
	var amount := clampf(float(skin.gait_amount),0.0,1.0) if live else 1.0
	var phase := float(skin.gait_phase) if live else fposmod(float(skin.reference_clock)/float(profile.duration),1.0)
	var sample := sample_at(phase)
	var maximum_sole_y := -INF
	for side in ["near","far"]: maximum_sole_y = maxf(maximum_sole_y,float(_reference_pose(sample,side,skin).sole_y)+floor_bias)
	var clearance := maxf(0.0,-maximum_sole_y)
	var contact_weight := 1.0-smoothstep(contact_band,contact_band+contact_fade,clearance)
	var floor_contact_drop := clearance*contact_weight
	var sample_hip := _sample_v(sample,"pelvis")
	var pelvis_offset := (sample_hip-mean_hip)*source_scale+Vector2(0,floor_bias+floor_contact_drop)
	skin.bones.pelvis.position = skin.bones.pelvis.rest.origin+pelvis_offset*amount
	# Match the existing hip-based torso lean without the old sole-IK crouch.
	skin.bones.torso.position = skin.bones.torso.rest.origin.rotated(skin.bones.torso.rotation*amount)
	for side in ["near","far"]:
		var thigh: Bone2D = skin.bones[side+"_thigh"]
		var calf: Bone2D = skin.bones[side+"_calf"]
		var foot: Bone2D = skin.bones[side+"_foot"]
		# The source's two projected hip roots differ in height by about seven
		# units. Keep this outfit's existing same-height waist mounts instead.
		thigh.position = thigh.rest.origin
		var upper := lerp_angle(calf.rest.origin.angle(),float(sample[side+"_leg_upper_angle"]),amount)
		var lower := lerp_angle(foot.rest.origin.angle(),float(sample[side+"_leg_lower_angle"]),amount)
		var pitch := lerp_angle(0.0,adapted_foot_pitch(float(sample[side+"_foot_angle"])),amount)
		thigh.rotation = upper-calf.rest.origin.angle()-skin.bones.pelvis.rotation
		calf.rotation = lower-foot.rest.origin.angle()-thigh.rotation-skin.bones.pelvis.rotation
		foot.rotation = pitch-thigh.rotation-calf.rotation-skin.bones.pelvis.rotation
	# Floating frames remain floating. Only guard against interpolated startup
	# positions penetrating the floor: translate the entire actor's hip upward.
	var penetration := _refresh_contacts(skin)
	if penetration>0.0:
		skin.bones.pelvis.position.y -= penetration
		_refresh_contacts(skin)
	skin.reference_pelvis_adjusted = true
	skin.reference_pelvis_drop = skin.bones.pelvis.position.y-skin.bones.pelvis.rest.origin.y
	last = {"phase":phase,"amount":amount,"source_clearance":clearance,"contact_weight":contact_weight,"floor_contact_drop":floor_contact_drop,"pelvis_offset":skin.bones.pelvis.position-skin.bones.pelvis.rest.origin,"source_scale":source_scale,"same_male_zero_phase_as_upper_body":true,"fixed_lengths":fixed_lengths,"project_hip_mounts_unchanged":true}

func _refresh_contacts(skin: Variant) -> float:
	var maximum_y := -INF
	for side in ["near","far"]:
		var foot: Bone2D = skin.bones[side+"_foot"]
		var ankle: Vector2 = skin.runtime.rig_root_node.to_local(foot.global_position)
		var axis: Vector2 = skin.runtime.rig_root_node.to_local(foot.to_global(Vector2.RIGHT))-ankle
		var pitch := axis.angle()
		var sole: Vector2 = ankle+skin._sole_contact(side,pitch)
		maximum_y = maxf(maximum_y,sole.y)
		skin.ground_contacts[side] = {"center":sole,"lift":maxf(0.0,-sole.y),"pitch":pitch,"source_run":true}
	if skin.contact_shadow!=null: skin.contact_shadow.queue_redraw()
	return maximum_y

func finalize_pose() -> void:
	var skin: Variant = owner_ref.get_ref()
	if skin==null or not active: return
	# The inherited transition blend happens after the two leg callbacks.
	# Do not re-pose the legs, only refresh their final real contact/shadow.
	var penetration := _refresh_contacts(skin)
	if penetration>0.0:
		skin.bones.pelvis.position.y -= penetration
		_refresh_contacts(skin)

func get_diagnostics() -> Dictionary:
	return {"active":active,"profile":PROFILE,"samples":profile.get("sampleCount",0),"duration":profile.get("duration",0),"source":profile.get("source",{}),"fixed_lengths":fixed_lengths,"source_scale":source_scale,"floor_bias":floor_bias,"contact_band":contact_band,"contact_fade":contact_fade,"last":last,"foot_pitch_adaptation":{"onset_degrees":75.0,"asymptote_degrees":140.0,"formula":"75+65*tanh((raw-75)/65) above75; negatives unchanged","scope":"old v15 long boot fit, same source recovery timing and rigid foot"},"method":"measured male101 hip/knee/ankle angles and adapted foot pitch on fixed project bones; source flight retained; narrow low-foot contact band only"}
