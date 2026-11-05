extends "realtime_garment_skin.gd"

## A continuous closed surface supported by the actual articulated legs.
## The old angular garment is only a low-amplitude inertial carrier. Capsule
## contacts provide local knee/shin bulges; released cloth settles with springs.
var visible_leg_meshes: Array[Polygon2D] = []
var support_rest_vertices := PackedVector2Array()
var support_base_vertices := PackedVector2Array()
var support_row_edges: Array[Vector2] = []
var support_offsets := PackedVector3Array()
var support_speeds := PackedVector3Array()
var support_targets := PackedVector3Array()
var support_neutral_targets := PackedVector3Array()
var support_cpu_ready := false
var support_rendered := false
var support_reset_pending := true
var support_last_clock := -1.0
var support_steps := 0
var local_support_peak := 0.0
var knee_support_peak := 0.0
var hem_carry_peak := 0.0
var support_point_records: Dictionary = {}
var support_row_records: Array = []

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	garment_mode = CLOSED_ROBE
	var ready := super.build(owner_runtime,art_dir)
	if not ready: return false
	for mesh in meshes:
		if mesh.name in ["YuxuUnified_near_leg","YuxuUnified_far_leg"]:
			visible_leg_meshes.append(mesh)
	# The body keeps its original texture, UVs, triangles and skin records.
	# Its final skinning is evaluated once on CPU so contact can affect a local
	# piece of cloth instead of rotating an entire side of the robe.
	garment_body.skeleton = NodePath("")
	garment_body.clear_bones()
	support_cpu_ready = true
	_cache_neutral_support()
	return visible_leg_meshes.size() == 2

func _build_approved_body() -> void:
	super._build_approved_body()
	var fitted := garment_body.polygon
	for index in range(fitted.size()):
		var source: Vector2 = garment_body.uv[index]
		# Keep the approved V21's original width and ankle-length hem. The
		# previous closed candidates added 146 source pixels and hid the shoes.
		fitted[index] = _world(source)
	garment_body.polygon = fitted
	support_rest_vertices = fitted.duplicate()
	var source_image: Image = textures.body.get_image()
	for row in range(ROWS+1):
		var source_y := clampi(roundi(310.0+1030.0*row/ROWS),0,source_image.get_height()-1)
		var left := 665.0
		var right := 275.0
		for source_x in range(275,666):
			if source_image.get_pixel(source_x,source_y).a > 0.80:
				left = minf(left,float(source_x))
				right = maxf(right,float(source_x))
		if right < left:
			left = 350.0
			right = 610.0
		support_row_edges.append(Vector2((left-275.0)/390.0,(right-275.0)/390.0))
	support_offsets.resize(ROWS+1)
	support_offsets.fill(Vector3.ZERO)
	support_speeds.resize(ROWS+1)
	support_speeds.fill(Vector3.ZERO)
	support_targets.resize(ROWS+1)
	support_targets.fill(Vector3.ZERO)
	support_neutral_targets.resize(ROWS+1)
	support_neutral_targets.fill(Vector3.ZERO)

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	if delta > 0.0:
		if clock < motion_clock or clock-motion_clock > 0.25:
			support_reset_pending = true
		if runtime != null and garment_has_root_origin:
			if runtime.global_position.distance_to(garment_last_root_origin) > maxf(90.0,velocity.length()*delta*3.0):
				support_reset_pending = true
		# Surface offsets are actor-local. A turn resets this tiny local solve;
		# the low-frequency carrier retains world momentum in the parent.
		if facing != motion_facing: support_reset_pending = true
	super.set_motion_context(delta,velocity,clock,facing)

func _foot_sample(phase: float, backwards: float) -> Vector3:
	# The full walk is inside the cloth. Never route through the previous
	# almost-straight knee / sub-pixel-lift restrictions of the closed skin.
	var p := fposmod(phase,1.0)
	var x := 0.0
	var lift := 0.0
	var pitch := 0.0
	if p < SUPPORT_FRACTION:
		var support := p/SUPPORT_FRACTION
		x = lerpf(15.0,-15.0,support)
		pitch = lerpf(-0.07,0.0,smoothstep(0.0,0.16,support))
		pitch += 0.19*smoothstep(0.76,1.0,support)
	else:
		var swing := (p-SUPPORT_FRACTION)/(1.0-SUPPORT_FRACTION)
		x = lerpf(-15.0,15.0,pow(smoothstep(0.0,1.0,swing),1.15))
		lift = 8.5*pow(maxf(0.0,sin(swing*PI)),1.3)
		pitch = lerpf(0.19,-0.07,smoothstep(0.05,0.86,swing))
	return Vector3(lerpf(x,-x,backwards),lerpf(lift,lift*0.76,backwards),lerpf(pitch,-pitch*0.5,backwards))

func _solve_leg(side: String, ankle: Vector2, foot_pitch: float) -> void:
	# Fixed anatomical lengths and original soles; cloth, rather than a forced
	# straight stance, must enclose the resulting knee and shin.
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
	# Retain the parent's natural pelvis drop and bent-knee leg solution.
	# The hidden meshes still exist for diagnostics and future split garments.
	pass

func _integrate_garment() -> void:
	var dt := motion_delta/6.0
	for substep in range(6):
		var parent_angle := 0.0
		var parent_speed := 0.0
		for segment in range(3):
			var stiffness: float = [110.0,65.0,39.0][segment]
			var damping: float = [14.0,10.0,7.5][segment]
			var gravity: float = [35.0,31.0,27.0][segment]
			var angle := garment_angles[segment]
			var target := garment_air_drag*float([0.008,0.013,0.016][segment])
			var torque := (target-angle)*stiffness-gravity*sin(parent_angle+angle)
			torque += garment_drive*float([5.0,8.0,10.0][segment])-parent_speed*1.7
			garment_angular_velocity[segment] += (torque-garment_angular_velocity[segment]*damping)*dt
			angle += garment_angular_velocity[segment]*dt
			var limit: float = [0.045,0.070,0.095][segment]
			if absf(angle)>limit:
				angle = clampf(angle,-limit,limit)
				garment_angular_velocity[segment] *= 0.3
			garment_angles[segment] = angle
			parent_angle += angle
			parent_speed += garment_angular_velocity[segment]
	garment_simulation_steps += 1

func _garment_vertex(index: int) -> Vector2:
	if support_rendered: return garment_body.polygon[index]
	var point: Vector2 = support_rest_vertices[index] if not support_rest_vertices.is_empty() else garment_body.polygon[index]
	var result := Vector2.ZERO
	var root_inverse: Transform2D = runtime.rig_root_node.global_transform.affine_inverse()
	for bone_index in range(garment_record.keys.size()):
		var weight: float = garment_record.weights[bone_index][index]
		if weight <= 0.0: continue
		var current: Transform2D = root_inverse*bones[garment_record.keys[bone_index]].global_transform
		result += (current*garment_record.rest[bone_index].affine_inverse()*point)*weight
	return result

func _row_point(row: int, fraction: float) -> Vector2:
	var cell := clampf(fraction,0.0,1.0)*COLUMNS
	var column := mini(floori(cell),COLUMNS-1)
	var first: Vector2 = support_base_vertices[row*(COLUMNS+1)+column]
	return first.lerp(support_base_vertices[row*(COLUMNS+1)+column+1],cell-column)

func _capsule_row(a: Vector2, b: Vector2, radius: float, y: float) -> Vector2:
	# Analytic horizontal slices of spheres sampled along the actual segment.
	# Short overlapping capsules prevent discontinuities as a knee crosses rows.
	var result := Vector2(INF,-INF)
	for sample_index in range(9):
		var point := a.lerp(b,float(sample_index)/8.0)
		var vertical := absf(y-point.y)
		if vertical >= radius: continue
		var horizontal := sqrt(maxf(0.0,radius*radius-vertical*vertical))
		result.x = minf(result.x,point.x-horizontal)
		result.y = maxf(result.y,point.x+horizontal)
	return result

func _read_support_legs() -> Array:
	var result: Array = []
	support_point_records.clear()
	for side in ["far","near"]:
		var hip: Vector2 = runtime.rig_root_node.to_local(bones[side+"_thigh"].global_position)
		var knee: Vector2 = runtime.rig_root_node.to_local(bones[side+"_calf"].global_position)
		var ankle: Vector2 = runtime.rig_root_node.to_local(bones[side+"_foot"].global_position)
		var pitch: float = bones[side+"_thigh"].rotation+bones[side+"_calf"].rotation+bones[side+"_foot"].rotation
		var sole := _sole_contact(side,pitch)
		var toe := ankle+Vector2(5.2,1.0).rotated(pitch)
		var leg := {"side":side,"hip":hip,"knee":knee,"ankle":ankle,"toe":toe,"lift":maxf(0.0,-ankle.y-sole.y)}
		result.append(leg)
		support_point_records[side] = leg
	return result

func _raw_support_target(row: int, legs: Array) -> Vector3:
	var source_y := 310.0+1030.0*row/ROWS
	var loose := smoothstep(790.0,940.0,source_y)
	var edge: Vector2 = support_row_edges[row]
	var left := _row_point(row,edge.x)
	var right := _row_point(row,edge.y)
	var row_y := (left.y+right.y)*0.5
	var envelope := Vector2(left.x,right.x)
	var carry := 0.0
	for leg in legs:
		# Shoes retain their approved visibility below the original hem. The
		# covered thigh/shin support cloth; actual ankle lift carries the hem.
		for capsule in [[leg.hip,leg.knee,4.4],[leg.knee,leg.ankle,4.0]]:
			var slice := _capsule_row(capsule[0],capsule[1],float(capsule[2])+0.8,row_y)
			envelope.x = minf(envelope.x,slice.x)
			envelope.y = maxf(envelope.y,slice.y)
		var reach := exp(-pow((row_y-leg.ankle.y)/15.0,2.0))
		var outer := smoothstep(0.0,12.0,absf(leg.ankle.x))
		carry = maxf(carry,float(leg.lift)*0.23*reach*(0.35+0.65*outer))
	return Vector3(clampf(envelope.x-left.x,-16.0,0.0),clampf(envelope.y-right.x,0.0,20.0),-carry)*loose

func _cache_neutral_support() -> void:
	# The approved standing illustration already contains cloth ease around
	# its resting legs. Store that ease in source space, rather than visibly
	# inflating the static drawing or gating collision by an animation phase.
	var saved := {}
	for key in bones: saved[key] = bones[key].transform
	for bone in bones.values(): bone.transform = bone.rest
	for side in ["near","far"]:
		var ankle: Vector2 = _world(_leg_points(side)[2])
		ankle.y = -_sole_contact(side,0.0).y
		_solve_leg(side,ankle,0.0)
	support_base_vertices = support_rest_vertices.duplicate()
	var neutral_legs := _read_support_legs()
	for row in range(ROWS+1): support_neutral_targets[row] = _raw_support_target(row,neutral_legs)
	for key in saved: bones[key].transform = saved[key]
	support_point_records.clear()

func solve_leg_supported_surface(delta: float, force_reset := false) -> void:
	if not support_cpu_ready: return
	support_rendered = false
	support_base_vertices.resize(support_rest_vertices.size())
	for index in range(support_rest_vertices.size()): support_base_vertices[index] = _garment_vertex(index)
	var legs := _read_support_legs()
	support_row_records.clear()
	for row in range(ROWS+1):
		var raw := _raw_support_target(row,legs)
		var neutral: Vector3 = support_neutral_targets[row]
		support_targets[row] = Vector3(minf(0.0,raw.x-neutral.x+0.55),maxf(0.0,raw.y-neutral.y-0.55),raw.z)
	# Longitudinal cloth tension spreads a knee contact into the hanging
	# material below. This limits a hard joint-shaped kink without adding a
	# timer or a second free panel. The waist remains pinned to its source.
	for row in range(ROWS-1,26,-1):
		var distance := absf(_row_point(row,0.5).y-_row_point(row+1,0.5).y)
		var falloff := distance*1.25
		var target: Vector3 = support_targets[row]
		var neighbor: Vector3 = support_targets[row+1]
		target.x = minf(target.x,neighbor.x+falloff)
		target.y = maxf(target.y,neighbor.y-falloff)
		support_targets[row] = target
	for row in range(28,ROWS+1):
		var distance := absf(_row_point(row,0.5).y-_row_point(row-1,0.5).y)
		var falloff := distance*0.75
		var target: Vector3 = support_targets[row]
		var neighbor: Vector3 = support_targets[row-1]
		target.x = minf(target.x,neighbor.x+falloff)
		target.y = maxf(target.y,neighbor.y-falloff)
		support_targets[row] = target
	for row in range(ROWS+1):
		var source_y := 310.0+1030.0*row/ROWS
		var loose := smoothstep(790.0,940.0,source_y)
		var row_y := _row_point(row,0.5).y
		var target: Vector3 = support_targets[row]
		if force_reset or support_reset_pending:
			support_offsets[row] = target
			support_speeds[row] = Vector3.ZERO
		else:
			var position := support_offsets[row]
			var speed := support_speeds[row]
			var subdelta := clampf(delta,0.0,0.05)/5.0
			for substep in range(5):
				# Soft contacts move outward briskly. Release is slower, allowing
				# gravity/seam tension and inertia to return the emptied fold.
				var release := Vector3(115.0,115.0,92.0)
				var force := (target-position)*release-speed*Vector3(12.0,12.0,9.6)
				speed += force*subdelta
				position += speed*subdelta
				if position.x > target.x:
					position.x = target.x
					speed.x = minf(speed.x,0.0)
				if position.y < target.y:
					position.y = target.y
					speed.y = maxf(speed.y,0.0)
			position.x = clampf(position.x,-18.0,1.0)
			position.y = clampf(position.y,-1.0,22.0)
			position.z = clampf(position.z,-4.0,0.8)
			support_offsets[row] = position
			support_speeds[row] = speed
		if loose > 0.0:
			support_row_records.append({"row":row,"y":row_y,"target":target,"offset":support_offsets[row]})
	var final_vertices := support_base_vertices.duplicate()
	var vertex_colors := PackedColorArray()
	vertex_colors.resize(final_vertices.size())
	vertex_colors.fill(Color.WHITE)
	local_support_peak = 0.0
	knee_support_peak = 0.0
	hem_carry_peak = 0.0
	for row in range(ROWS+1):
		var edge: Vector2 = support_row_edges[row]
		var offset: Vector3 = support_offsets[row]
		for column in range(COLUMNS+1):
			var index := row*(COLUMNS+1)+column
			var column_fraction := float(column)/COLUMNS
			var across := clampf((column_fraction-edge.x)/maxf(0.1,edge.y-edge.x),0.0,1.0)
			var left_weight := pow(1.0-across,1.65)
			var right_weight := pow(across,1.65)
			var shift := Vector2(offset.x*left_weight+offset.y*right_weight,0.0)
			# The rising side alone lifts; the other edge remains floor length.
			var foot_weight := 0.0
			var point: Vector2 = support_base_vertices[index]
			for leg in legs:
				var x_weight := exp(-pow((point.x-float(leg.ankle.x))/12.0,2.0))
				foot_weight = maxf(foot_weight,x_weight)
			shift.y = offset.z*foot_weight
			final_vertices[index] += shift
			local_support_peak = maxf(local_support_peak,shift.length())
			if point.y < -19.0: knee_support_peak = maxf(knee_support_peak,absf(shift.x))
			hem_carry_peak = maxf(hem_carry_peak,-shift.y)
			# Gentle self-shading makes pressure folds visible in the original
			# painted cloth; no marks or new texture are drawn over the costume.
			var pressure := clampf((absf(offset.x)*left_weight+absf(offset.y)*right_weight)/16.0,0.0,1.0)
			var fold := 1.0-pressure*0.075*sin(across*PI)
			vertex_colors[index] = Color(fold,fold,fold,1.0)
	garment_body.polygon = final_vertices
	garment_body.vertex_colors = vertex_colors
	support_rendered = true
	# Preserve the real ground plane without translating the waist. Contact
	# can lift one local hem region; only downward penetration is corrected.
	var penetration := maxf(0.0,_garment_lowest_point()+0.55)
	if penetration > 0.0:
		for index in range(final_vertices.size()):
			var source: Vector2 = garment_body.uv[index]
			final_vertices[index].y -= penetration*smoothstep(850.0,1100.0,source.y)
		garment_body.polygon = final_vertices
	garment_floor_max = _garment_lowest_point()
	support_steps += 1
	support_reset_pending = false

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	if motion_context and motion_delta <= 0.0 and last_clip != "": return
	support_rendered = false
	super.update_cloth(clip,time,duration,tracks)
	if clip == "BindPose":
		garment_body.polygon = support_rest_vertices
		return
	# The shared realtime adapter previously translated its floor-length
	# shell down to the floor. Cancel that translation: the approved hem
	# ends above the shoes and must retain exactly that original length.
	for side in ["front","back"]:
		bones[side+"_cloth"].position.y -= garment_ground_shift
	garment_ground_shift = 0.0
	if motion_clock != support_last_clock or not motion_context:
		solve_leg_supported_surface(motion_delta,not motion_context)
		support_last_clock = motion_clock
	else:
		support_rendered = true

func get_garment_diagnostics() -> Dictionary:
	var result := super.get_garment_diagnostics()
	result.support_model = "actual_leg_capsule_rows_with_release_springs"
	result.leg_rendering = "approved_visible_shoes"
	result.visible_leg_meshes = visible_leg_meshes.size()
	result.hidden_leg_meshes = 0
	result.complete_leg_bones = leg_chains.size() == 2 and leg_chains.near.size() == 3 and leg_chains.far.size() == 3
	result.local_support_peak = local_support_peak
	result.knee_support_peak = knee_support_peak
	result.hem_carry_peak = hem_carry_peak
	result.leg_support_points = support_point_records
	result.support_rows = support_row_records
	result.support_steps = support_steps
	result.rest_hem_source_y = 1340.0
	result.rest_hem_local_y = _world(Vector2(475,1340)).y
	result.source_geometry_preserved = true
	result.continuous_triangles = garment_body.polygons.size() if garment_body != null else 0
	return result

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "approved fullbody v15 / leg-supported continuous closed robe"
	return result
