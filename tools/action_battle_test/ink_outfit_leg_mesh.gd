extends RefCounted

## Fixed-size trousers: source poses supply hip location and segment angles,
## never their changing bone lengths or skinned vertex deformation.
## Thigh, calf/boot shaft and foot are rigid; only their knee/ankle hinges move.
const CANVAS := Vector2(50.0,80.0)
const HIP := Vector2(23.2171268,11.9900977)
const KNEE := Vector2(21.9535855,39.7395430)
const ANKLE := Vector2(19.6304320,68.2780183)
## The source's toe/instep artwork begins above its ankle pivot.
const FOOT_CUT_Y := 65.5

var errors: Array[String] = []
var enabled := true
var visible := true
var owner_ref: WeakRef
var legs: Dictionary = {}
var fixed_upper_length := 0.0
var fixed_lower_length := 0.0
var update_count := 0

func build(owner_skin: Variant) -> bool:
	owner_ref = weakref(owner_skin)
	if owner_skin==null or not legs.is_empty(): return false
	fixed_upper_length = owner_skin.bones.near_thigh.position.distance_to(owner_skin.bones.near_calf.position)
	fixed_lower_length = owner_skin.bones.near_calf.position.distance_to(owner_skin.bones.near_foot.position)
	for record in owner_skin.mesh_records:
		if str(record.category)!="leg": continue
		var side := "near" if str(record.source.rendererPath).contains("youtui") else "far"
		var state := {"side":side,"source_mesh":record.mesh,"last":{},"pixels_to_world":float(owner_skin.source_scale)}
		var idle_lower: Vector2 = owner_skin.bones[side+"_foot"].position-owner_skin.bones[side+"_calf"].position
		state.foot_rotation_offset = idle_lower.angle()-(ANKLE-KNEE).angle()-float(owner_skin.bones[side+"_foot"].rotation)
		for piece in ["upper","lower","foot"]:
			var mesh := Polygon2D.new()
			mesh.name = "FixedLeg_"+side+"_"+piece
			mesh.texture = record.mesh.texture
			mesh.material = record.mesh.material
			mesh.texture_filter = record.mesh.texture_filter
			mesh.z_index = record.mesh.z_index
			mesh.modulate = record.mesh.modulate
			var start_y := 0.0 if piece=="upper" else (KNEE.y if piece=="lower" else FOOT_CUT_Y)
			var end_y := KNEE.y if piece=="upper" else (FOOT_CUT_Y if piece=="lower" else CANVAS.y)
			var points := PackedVector2Array([Vector2(0,start_y),Vector2(CANVAS.x,start_y),Vector2(CANVAS.x,end_y),Vector2(0,end_y)])
			state[piece+"_points"] = points
			var uv := PackedVector2Array()
			for point in points: uv.append(point/CANVAS*mesh.texture.get_size())
			mesh.uv = uv
			mesh.polygons = [PackedInt32Array([0,1,2]),PackedInt32Array([0,2,3])]
			state[piece+"_mesh"] = mesh
		var joint := Polygon2D.new()
		joint.name = "ClothKnee_"+side
		joint.texture = record.mesh.texture
		joint.material = record.mesh.material
		joint.texture_filter = record.mesh.texture_filter
		joint.z_index = record.mesh.z_index
		state.joint_mesh = joint
		var ankle_joint := Polygon2D.new()
		ankle_joint.name = "BootAnkle_"+side
		ankle_joint.texture = record.mesh.texture
		ankle_joint.material = record.mesh.material
		ankle_joint.texture_filter = record.mesh.texture_filter
		ankle_joint.z_index = record.mesh.z_index
		state.ankle_joint_mesh = ankle_joint
		# The joint goes below both rigid pieces, covering their outer wedge.
		owner_skin.runtime.rig_root_node.add_child(joint)
		owner_skin.runtime.rig_root_node.add_child(ankle_joint)
		owner_skin.runtime.rig_root_node.add_child(state.upper_mesh)
		owner_skin.runtime.rig_root_node.add_child(state.lower_mesh)
		owner_skin.runtime.rig_root_node.add_child(state.foot_mesh)
		legs[side] = state
	if legs.size()!=2:
		errors.append("Fixed legs require both source leg meshes")
		return false
	update_pose()
	return true

func _map(point: Vector2, pivot: Vector2, destination: Vector2, axis: Vector2, rotation: float, length_scale: float, width_scale: float) -> Vector2:
	var local := point-pivot
	var normal := Vector2(-axis.y,axis.x)
	return destination+(axis*local.dot(axis)*length_scale+normal*local.dot(normal)*width_scale).rotated(rotation)

func update_pose(_delta: float = 0.0, _clip: String = "") -> void:
	if not enabled: return
	var skin: Variant = owner_ref.get_ref()
	if skin==null: return
	var rest_upper := (KNEE-HIP).normalized()
	var rest_lower := (ANKLE-KNEE).normalized()
	for state in legs.values():
		var side: String = state.side
		var hip: Vector2 = skin.bones[side+"_thigh"].position
		var source_knee: Vector2 = skin.bones[side+"_calf"].position
		var source_ankle: Vector2 = skin.bones[side+"_foot"].position
		var upper_dir := (source_knee-hip).normalized()
		var lower_dir := (source_ankle-source_knee).normalized()
		var knee := hip+upper_dir*fixed_upper_length
		var ankle := knee+lower_dir*fixed_lower_length
		var upper_rotation := upper_dir.angle()-rest_upper.angle()
		var lower_rotation := lower_dir.angle()-rest_lower.angle()
		var foot_rotation := float(skin.bones[side+"_foot"].rotation)+float(state.foot_rotation_offset)
		for piece in ["upper","lower","foot"]:
			var vertices := PackedVector2Array()
			for point in state[piece+"_points"]:
				if piece=="upper": vertices.append(_map(point,HIP,hip,rest_upper,upper_rotation,fixed_upper_length/HIP.distance_to(KNEE),state.pixels_to_world))
				elif piece=="lower": vertices.append(_map(point,KNEE,knee,rest_lower,lower_rotation,fixed_lower_length/KNEE.distance_to(ANKLE),state.pixels_to_world))
				else: vertices.append(ankle+(point-ANKLE).rotated(foot_rotation)*float(state.pixels_to_world))
			state[piece+"_mesh"].polygon = vertices
			state[piece+"_mesh"].visible = visible
		# Fixed-size circular cloth support: only joint shape follows the hinge.
		# Sample this same pants cloth around its painted knee, no new artwork.
		var joint_points := PackedVector2Array()
		var joint_uv := PackedVector2Array()
		var joint_angle := upper_rotation+wrapf(lower_rotation-upper_rotation,-PI,PI)*0.5
		var radius := 4.5
		for index in range(32):
			var angle := TAU*float(index)/32.0
			var offset := Vector2(cos(angle),sin(angle))*radius
			joint_points.append(knee+offset.rotated(joint_angle)*float(state.pixels_to_world))
			joint_uv.append((KNEE+offset)/CANVAS*state.joint_mesh.texture.get_size())
		state.joint_mesh.polygon = joint_points
		state.joint_mesh.uv = joint_uv
		state.joint_mesh.visible = visible
		var ankle_points := PackedVector2Array()
		var ankle_uv := PackedVector2Array()
		var ankle_angle := lower_rotation+wrapf(foot_rotation-lower_rotation,-PI,PI)*0.5
		# Join the actual painted cut edges (alpha>180 measured on row1298).
		# A full circular cap protrudes behind the heel while missing the
		# forward instep. This support occupies only the gap between pieces.
		for x in [17.65,24.75]:
			var source_edge := Vector2(x,FOOT_CUT_Y)
			ankle_points.append(_map(source_edge,KNEE,knee,rest_lower,lower_rotation,fixed_lower_length/KNEE.distance_to(ANKLE),state.pixels_to_world))
			ankle_points.append(ankle+(source_edge-ANKLE).rotated(foot_rotation)*float(state.pixels_to_world))
		ankle_points = Geometry2D.convex_hull(ankle_points)
		if ankle_points.size()>1 and ankle_points[0].is_equal_approx(ankle_points[-1]): ankle_points.remove_at(ankle_points.size()-1)
		for point in ankle_points:
			var central_sample: Vector2 = ANKLE+(point-ankle).rotated(-ankle_angle)/float(state.pixels_to_world)*0.30
			ankle_uv.append(central_sample/CANVAS*state.ankle_joint_mesh.texture.get_size())
		state.ankle_joint_mesh.polygon = ankle_points
		state.ankle_joint_mesh.uv = ankle_uv
		state.ankle_joint_mesh.visible = visible
		state.source_mesh.visible = false
		state.last = {"hip":hip,"knee":knee,"ankle":ankle,"source_knee":source_knee,"source_ankle":source_ankle,"fixed_upper_length":fixed_upper_length,"fixed_lower_length":fixed_lower_length,"upper_rotation":upper_rotation,"lower_rotation":lower_rotation,"foot_rotation":foot_rotation,"source_foot_rotation":float(skin.bones[side+"_foot"].rotation),"independent_rigid_foot":true}
	update_count += 1

func set_enabled(value: bool) -> void:
	enabled = value
	for state in legs.values():
		state.source_mesh.visible = visible and not enabled
		for mesh in [state.upper_mesh,state.lower_mesh,state.foot_mesh,state.joint_mesh,state.ankle_joint_mesh]: mesh.visible = visible and enabled

func set_visible(value: bool) -> void:
	visible = value
	set_enabled(enabled)

func dispose() -> void:
	for state in legs.values():
		state.source_mesh.visible = visible
		for mesh in [state.upper_mesh,state.lower_mesh,state.foot_mesh,state.joint_mesh,state.ankle_joint_mesh]: mesh.queue_free()
	legs.clear()

func get_diagnostics() -> Dictionary:
	var parts := {}
	for side in legs:
		parts[side] = {"last":legs[side].last,"independent_rigid_foot":true,"rigid_pieces":3,"joint_only_changes_pose":true,"foot_rotation_offset":legs[side].foot_rotation_offset}
	return {"enabled":enabled,"visible":visible,"errors":errors,"fixed_upper_length":fixed_upper_length,"fixed_lower_length":fixed_lower_length,"fixed_ratio":fixed_upper_length/fixed_lower_length,"parts":parts,"updates":update_count,"author_canvas":[50,80],"anchors":{"hip":HIP,"knee":KNEE,"ankle":ANKLE},"source_frames_modified":false,"original_mesh_vertices_modified":false,"method":"source angles and hip location only; fixed thigh/calf lengths, independent rigid foot from source tui3 rotation, local knee/ankle support"}
