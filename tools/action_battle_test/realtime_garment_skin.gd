extends "yuxu_approved_skin.gd"

## Structural policy for the realtime actor. The approved drawing, hands and
## complete leg chains remain shared; the legacy split robe stays opt-in.
const CLOSED_ROBE := &"closed_robe"
const SPLIT_OUTFIT := &"split_outfit"
# Select from the garment design before build; no inventory classification is
# inferred from a filename, colour, rank or gender.
var garment_mode: StringName = CLOSED_ROBE
var garment_angles := Vector3.ZERO
var garment_angular_velocity := Vector3.ZERO
var garment_drive := 0.0
var garment_air_drag := 0.0
var garment_ground_shift := 0.0
var garment_floor_max := -INF
var garment_last_clock := -1.0
var garment_simulation_steps := 0
var garment_body: Polygon2D
var garment_record: Dictionary = {}
var garment_outline_samples: Array = []
var garment_last_root_origin := Vector2.ZERO
var garment_has_root_origin := false

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	if garment_mode not in [CLOSED_ROBE,SPLIT_OUTFIT]:
		validation_errors.append("Unknown garment structure: "+str(garment_mode))
		return false
	# The parent verifies its legacy seven-mesh count. This policy replaces
	# both lower panels with one mesh, so a closed actor has six meshes.
	var legacy_ready := super.build(owner_runtime, art_dir)
	if garment_mode == SPLIT_OUTFIT: return legacy_ready
	return validation_errors.is_empty() and meshes.size() == 6 and garment_body != null

func _build_approved_body() -> void:
	if garment_mode == SPLIT_OUTFIT:
		super._build_approved_body()
		return
	var rect := [275, 310, 390, 1030]
	var vertices := PackedVector2Array()
	var sets: Array = []
	for index in range(8): sets.append(PackedFloat32Array())
	for row in range(ROWS+1):
		var source_y := float(rect[1])+float(rect[3])*row/ROWS
		# Keep the waist and artwork's UV unchanged. Only the lower drawing is
		# fitted to a floor-length closed silhouette, with a smooth knee join.
		var fitted_y := source_y+146.0*smoothstep(940.0,1340.0,source_y)
		var torso_share := 1.0-smoothstep(660.0,790.0,source_y)
		var cloth_share := smoothstep(735.0,1000.0,source_y)
		var hem_share := smoothstep(1010.0,1225.0,source_y)
		var edge_share := smoothstep(1210.0,1340.0,source_y)
		for column in range(COLUMNS+1):
			var source_x := float(rect[0])+float(rect[2])*column/COLUMNS
			var front_share := smoothstep(365.0,575.0,source_x)
			var loose := (1.0-torso_share)*cloth_share
			vertices.append(_world(Vector2(source_x,fitted_y)))
			sets[0].append(torso_share)
			sets[1].append((1.0-torso_share)*(1.0-cloth_share))
			sets[2].append(loose*(1.0-hem_share)*front_share)
			sets[3].append(loose*hem_share*(1.0-edge_share)*front_share)
			sets[4].append(loose*(1.0-hem_share)*(1.0-front_share))
			sets[5].append(loose*hem_share*(1.0-edge_share)*(1.0-front_share))
			sets[6].append(loose*hem_share*edge_share*front_share)
			sets[7].append(loose*hem_share*edge_share*(1.0-front_share))
	texture = textures.body
	_create_mesh("robe_closed",rect,vertices,["torso","pelvis","front_cloth","front_hem","back_cloth","back_hem","front_edge","back_edge"],sets,60)
	garment_body = meshes.back()
	garment_record = mesh_records.back()
	_cache_garment_outline()

func _cache_garment_outline() -> void:
	# Collision samples follow opaque cloth, including the curved lower edge;
	# transparent texture corners must not hold the actual hem above ground.
	var source: Image = textures.body.get_image()
	for source_x in range(280,660,5):
		for source_y in range(1339,1199,-1):
			if source.get_pixel(source_x,source_y).a < 0.90: continue
			var uv := Vector2(source_x,source_y)
			var cell_x := clampi(floori((uv.x-275.0)/390.0*COLUMNS),0,COLUMNS-1)
			var cell_y := clampi(floori((uv.y-310.0)/1030.0*ROWS),0,ROWS-1)
			var fraction := Vector2((uv.x-275.0)/390.0*COLUMNS-cell_x,(uv.y-310.0)/1030.0*ROWS-cell_y)
			var a := cell_y*(COLUMNS+1)+cell_x
			var b := a+COLUMNS+1
			if fraction.x+fraction.y <= 1.0:
				garment_outline_samples.append({"indices":[a,a+1,b],"weights":Vector3(1.0-fraction.x-fraction.y,fraction.x,fraction.y)})
			else:
				garment_outline_samples.append({"indices":[a+1,b+1,b],"weights":Vector3(1.0-fraction.y,fraction.x+fraction.y-1.0,1.0-fraction.x)})
			break

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	if garment_mode == SPLIT_OUTFIT:
		super.set_motion_context(delta,velocity,clock,facing)
		return
	# A paused arena can still ask for the same pose. Capture no new momentum
	# or facing changes until simulation time resumes.
	if delta <= 0.0:
		motion_context = true
		motion_delta = 0.0
		return
	var old_facing := motion_facing
	var old_velocity := previous_velocity
	var reset := clock < motion_clock or clock-motion_clock > 0.25
	if runtime != null:
		var root_origin: Vector2 = runtime.global_position
		if garment_has_root_origin and root_origin.distance_to(garment_last_root_origin) > maxf(90.0,velocity.length()*delta*3.0): reset = true
		garment_last_root_origin = root_origin
		garment_has_root_origin = true
	if reset:
		garment_angles = Vector3.ZERO
		garment_angular_velocity = Vector3.ZERO
		garment_drive = 0.0
		cloth_acceleration = Vector2.ZERO
		previous_velocity = velocity
		old_velocity = velocity
		cloth_positions.clear()
		cloth_speeds.clear()
	if facing != old_facing:
		# Angles are actor-local. Mirroring them retains the free hem's world
		# momentum when aim turns the side-view rig around.
		garment_angles *= -1.0
		garment_angular_velocity *= -1.0
	super.set_motion_context(delta,velocity,clock,facing)
	garment_drive = ((velocity.x-old_velocity.x)*facing/maxf(delta,0.001))/1200.0
	garment_drive = clampf(garment_drive,-3.8,3.8)
	garment_air_drag = clampf(velocity.x*facing/ROBED_MOVE_SPEED,-1.6,1.6)

func _foot_sample(phase: float, backwards: float) -> Vector3:
	var sample := super._foot_sample(phase,backwards)
	if garment_mode == CLOSED_ROBE:
		# Short shuffling contacts stay inside this garment. Translation speed
		# belongs to the arena, and the approved sliding allowance is visual.
		sample.x *= 0.30
		sample.y *= 0.08
		sample.z *= 0.35
	return sample

func _solve_leg(side: String, ankle: Vector2, foot_pitch: float) -> void:
	if garment_mode == CLOSED_ROBE:
		# The parent's dash also has a visual wide stance. Enclose that pose,
		# retaining both complete articulated legs and their sole contacts.
		var sole_before := _sole_contact(side,foot_pitch)
		ankle.x = clampf(ankle.x,-6.5,5.0)
		foot_pitch = clampf(foot_pitch,-0.045,0.085)
		ankle.y += sole_before.y-_sole_contact(side,foot_pitch).y
	super._solve_leg(side,ankle,foot_pitch)

func _constrain_closed_legs() -> void:
	var targets := {}
	for side in ["near","far"]:
		var foot: Bone2D = bones[side+"_foot"]
		var pitch: float = bones[side+"_thigh"].rotation+bones[side+"_calf"].rotation+foot.rotation
		var ankle: Vector2 = runtime.rig_root_node.to_local(foot.global_position)
		var sole := _sole_contact(side,pitch)
		# This also encloses the separate dash swing. A closed floor-length
		# garment permits a sliding step, with only a very small toe clearance.
		var lift := clampf(-ankle.y-sole.y,0.0,0.45)
		ankle.y = -sole.y-lift
		targets[side] = {"ankle":ankle,"pitch":pitch}
	# A short ankle path alone does not constrain the knee: the parent's
	# five-unit running crouch bends it forward through the closed shell.
	# Restore an upright hip, retaining the complete fixed-length IK chains.
	bones.pelvis.position.x *= 0.20
	var clock := motion_clock if motion_context else 0.0
	bones.pelvis.position.y = bones.pelvis.rest.origin.y+0.25*gait_amount+sin(clock*TAU/3.4)*0.24*(1.0-gait_amount)
	for side in ["near","far"]:
		var hip: Vector2 = runtime.rig_root_node.to_local(bones[side+"_thigh"].global_position)
		var ankle: Vector2 = targets[side].ankle
		var reach: float = bones[side+"_calf"].rest.origin.length()+bones[side+"_foot"].rest.origin.length()-0.04
		var required_y := ankle.y-sqrt(maxf(1.0,reach*reach-pow(ankle.x-hip.x,2.0)))
		bones.pelvis.position.y += maxf(0.0,required_y-hip.y)
	for side in ["near","far"]: _solve_leg(side,targets[side].ankle,targets[side].pitch)

func _integrate_garment() -> void:
	# Three coupled pendulums. Acceleration excites them, air drag provides a
	# small travel bias, and gravity plus damping settle all motion at rest.
	# There is no periodic cloth timer; gait can push through actual knee pose.
	var dt := motion_delta/6.0
	var knee_near: Vector2 = runtime.rig_root_node.to_local(bones.near_calf.global_position)
	var knee_far: Vector2 = runtime.rig_root_node.to_local(bones.far_calf.global_position)
	var contact_bias := clampf((knee_near.x+knee_far.x)*0.0008,-0.025,0.025)*gait_amount
	for substep in range(6):
		var parent_angle := 0.0
		var parent_speed := 0.0
		for segment in range(3):
			var stiffness: float = [92.0,49.0,27.0][segment]
			var damping: float = [12.0,8.1,5.8][segment]
			var gravity: float = [35.0,31.0,27.0][segment]
			var inertia: float = [22.0,31.0,38.0][segment]
			var limit: float = [0.16,0.24,0.28][segment]
			var target: float = garment_air_drag*[0.045,0.060,0.055][segment]+contact_bias
			var angle := garment_angles[segment]
			var absolute_angle := parent_angle+angle
			var torque := (target-angle)*stiffness-gravity*sin(absolute_angle)
			torque += garment_drive*inertia*cos(absolute_angle)-parent_speed*2.3
			garment_angular_velocity[segment] += (torque-garment_angular_velocity[segment]*damping)*dt
			angle += garment_angular_velocity[segment]*dt
			if absf(angle) > limit:
				angle = clampf(angle,-limit,limit)
				garment_angular_velocity[segment] *= 0.35
			garment_angles[segment] = angle
			parent_angle += angle
			parent_speed += garment_angular_velocity[segment]
	garment_simulation_steps += 1

func _garment_vertex(index: int) -> Vector2:
	var point: Vector2 = garment_body.polygon[index]
	var result := Vector2.ZERO
	var root_inverse: Transform2D = runtime.rig_root_node.global_transform.affine_inverse()
	for bone_index in range(garment_record.keys.size()):
		var weight: float = garment_record.weights[bone_index][index]
		if weight <= 0.0: continue
		var current: Transform2D = root_inverse*bones[garment_record.keys[bone_index]].global_transform
		result += (current*garment_record.rest[bone_index].affine_inverse()*point)*weight
	return result

func _garment_lowest_point() -> float:
	var lowest := -INF
	for sample in garment_outline_samples:
		var point := Vector2.ZERO
		for corner in range(3): point += _garment_vertex(sample.indices[corner])*sample.weights[corner]
		lowest = maxf(lowest,point.y)
	return lowest

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	# Parent provides only the established body, arm, gesture and leg poses.
	# Replacing its cloth transforms below never changes action resolution.
	if garment_mode == CLOSED_ROBE and motion_context and motion_delta <= 0.0 and last_clip != "": return
	super.update_cloth(clip,time,duration,tracks)
	if garment_mode == SPLIT_OUTFIT or clip == "BindPose": return
	_constrain_closed_legs()
	var live := motion_context and pose_blending
	if live and motion_delta > 0.0 and motion_clock != garment_last_clock:
		_integrate_garment()
		garment_last_clock = motion_clock
	elif not live:
		garment_angles = Vector3.ZERO
		garment_angular_velocity = Vector3.ZERO
	for side in ["front","back"]:
		var upper: Bone2D = bones[side+"_cloth"]
		var middle: Bone2D = bones[side+"_hem"]
		var edge: Bone2D = bones[side+"_edge"]
		upper.transform = upper.rest
		middle.transform = middle.rest
		edge.transform = edge.rest
		upper.rotation = garment_angles.x
		middle.rotation = garment_angles.y
		edge.rotation = garment_angles.z
	# A shared closed shell has one collision plane. Correct its two chains
	# together using opaque outline samples, so no front/back seam can open.
	garment_ground_shift = -0.55-_garment_lowest_point()
	for side in ["front","back"]: bones[side+"_cloth"].position.y += garment_ground_shift
	garment_floor_max = _garment_lowest_point()
	# Contact reports must describe the constrained full legs, including dash.
	for side in ["near","far"]:
		var foot: Bone2D = bones[side+"_foot"]
		# Read actor-local chain angles; global_rotation changes handedness
		# under the mirrored root and would report a false shoe penetration.
		var pitch: float = bones[side+"_thigh"].rotation+bones[side+"_calf"].rotation+foot.rotation
		var sole := _sole_contact(side,pitch)
		var ankle: Vector2 = runtime.rig_root_node.to_local(foot.global_position)
		ground_contacts[side] = {"center":ankle+sole,"lift":maxf(0.0,-ankle.y-sole.y),"pitch":pitch}

func get_garment_diagnostics() -> Dictionary:
	return {"mode":str(garment_mode),"continuous_mesh":garment_body != null,"angles":garment_angles,"angular_velocity":garment_angular_velocity,"acceleration_drive":garment_drive,"travel_drag":garment_air_drag,"ground_shift":garment_ground_shift,"lowest_cloth_y":garment_floor_max,"simulation_steps":garment_simulation_steps,"outline_samples":garment_outline_samples.size()}

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "approved fullbody v15 / realtime garment policy"
	result.garment = get_garment_diagnostics()
	return result
