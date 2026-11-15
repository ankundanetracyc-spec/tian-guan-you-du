extends RefCounted

## Our continuous sleeve carrier, authored against the actual painted image.
## Source shoulders/elbows/wrists are targets only. This does not reproduce the
## source 35/39-vertex tight sleeves, modify source frames, or warp texture UVs.
## Two fixed sleeve pieces rotate at the elbow; a local cloth joint and
## restrained fold shading cover the hinge. Cuffs and hands stay rigid.

class ElbowFolds extends Node2D:
	var creases: Array[PackedVector2Array] = []
	var outline := PackedVector2Array()
	var strength := 0.0
	var stroke := 0.13
	func _draw() -> void:
		if outline.size()>1: draw_polyline(outline,Color("6c706d"),stroke,true)
		for crease in creases: draw_colored_polygon(crease,Color(0.57,0.55,0.51,0.34*strength))

const CANVAS := Vector2(60.0,60.0)
const DEFAULT_AUTHORS := {
	"sleeve_near": {
		"shoulder":Vector2(615.0,272.0)*60.0/1254.0,
		"elbow":Vector2(595.0,577.0)*60.0/1254.0,
		"wrist":Vector2(610.0,870.0)*60.0/1254.0,
		"note":"canonical_v15 shared chart: chosen shoulder axis S(615,272), measured narrow elbow E(595,577), actual skin exit W(610,870) on native1254"
	},
	"sleeve_far": {
		"shoulder":Vector2(615.0,272.0)*60.0/1254.0,
		"elbow":Vector2(595.0,577.0)*60.0/1254.0,
		"wrist":Vector2(610.0,870.0)*60.0/1254.0,
		"note":"canonical_v15: exactly the same sleeve chart and physical width/hand scale as near; source far arm targets do not rescale the hand"
	}
}

var errors: Array[String] = []
var enabled := true
var visible := true
var owner_ref: WeakRef
var sleeves: Dictionary = {}
var update_count := 0
var fixed_upper_length := 0.0
var fixed_lower_length := 0.0

func _vec(value: Variant, fallback: Vector2) -> Vector2:
	if value is Vector2: return value
	if value is Array and value.size()==2: return Vector2(float(value[0]),float(value[1]))
	return fallback

func build(owner_skin: Variant, author_overrides: Dictionary = {}) -> bool:
	if not sleeves.is_empty():
		errors.append("Sleeve helper has already been built")
		return false
	var skin: Variant = owner_skin
	if skin==null or skin.runtime==null or skin.runtime.rig_root_node==null:
		errors.append("Sleeve helper requires a built fitting skin")
		return false
	owner_ref = weakref(skin)
	# Both sleeves use the near arm's initial Idle fit. Source pose endpoints
	# stretch and differ between sides, so they supply angles only after build.
	fixed_upper_length = skin.bones.near_upper.position.distance_to(skin.bones.near_forearm.position)
	fixed_lower_length = skin.bones.near_forearm.position.distance_to(skin.bones.near_hand.position)
	for key in ["sleeve_near","sleeve_far"]:
		var side := "near" if key=="sleeve_near" else "far"
		for joint in ["upper","forearm","hand"]:
			if not skin.bones.has(side+"_"+joint): errors.append("Missing source joint "+side+"_"+joint)
		var source_mesh: Polygon2D
		for record in skin.mesh_records:
			if str(record.get("component",""))==key: source_mesh = record.mesh
		if source_mesh==null or source_mesh.texture==null:
			errors.append("Missing painted sleeve mesh "+key)
			continue
		if not errors.is_empty(): continue
		var author: Dictionary = DEFAULT_AUTHORS[key].duplicate(true)
		author.merge(author_overrides.get(key,{}),true)
		var shoulder := _vec(author.get("shoulder"),DEFAULT_AUTHORS[key].shoulder)
		var elbow := _vec(author.get("elbow"),DEFAULT_AUTHORS[key].elbow)
		var wrist := _vec(author.get("wrist"),DEFAULT_AUTHORS[key].wrist)
		var upper_length := shoulder.distance_to(elbow)
		var lower_length := elbow.distance_to(wrist)
		if upper_length<1.0 or lower_length<1.0:
			errors.append("Invalid painted sleeve axes "+key)
			continue
		# Exported model coordinates are already Godot/source pixels (Unity x100).
		# The logical 60x60 canvas is in those same pixels: apply source_scale once.
		var pixels_to_world := float(skin.source_scale)
		var target_elbow: Vector2 = skin.bones[side+"_forearm"].position
		var target_wrist: Vector2 = skin.bones[side+"_hand"].position
		var hand_angle: float = skin.bones[side+"_hand"].rotation
		var angle_offset := (target_wrist-target_elbow).angle()-hand_angle-(wrist-elbow).angle()
		var mesh := Polygon2D.new()
		mesh.name = "AuthoredContinuous_"+key
		mesh.texture = source_mesh.texture
		mesh.material = source_mesh.material
		mesh.texture_filter = source_mesh.texture_filter
		mesh.z_index = source_mesh.z_index
		mesh.modulate = source_mesh.modulate
		var state := {"side":side,"source_mesh":source_mesh,"mesh":mesh,"author":author,
			"shoulder":shoulder,"elbow":elbow,"wrist":wrist,
			"upper_length":upper_length,"lower_length":lower_length,"total_length":upper_length+lower_length,
			"pixels_to_world":pixels_to_world,"hand_angle_offset":angle_offset,
			"sag_pixels":float(author.get("sag_pixels",0.25)),
			"half_span_pixels":float(author.get("half_span_pixels",8.5)),
			"columns":maxi(4,int(author.get("columns",12))),"rows":[],"uv_points":PackedVector2Array(),
			"rigid_hand_start":-1,"hand_pair_distance":0.0}
		_build_strip(state)
		var joint := Polygon2D.new()
		joint.name = "RoundedElbow_"+key
		joint.texture = mesh.texture
		joint.material = mesh.material
		joint.texture_filter = mesh.texture_filter
		joint.z_index = mesh.z_index
		joint.modulate = mesh.modulate
		state.joint_mesh = joint
		var folds := ElbowFolds.new()
		folds.name = "ElbowClothFolds_"+key
		folds.z_index = mesh.z_index
		state.folds = folds
		# The joint is behind the two rigid sleeve pieces, filling only the
		# outside hinge wedge. Their inside overlap cannot invert triangles.
		skin.runtime.rig_root_node.add_child(joint)
		skin.runtime.rig_root_node.add_child(mesh)
		skin.runtime.rig_root_node.add_child(folds)
		sleeves[key] = state
	if not errors.is_empty():
		for state in sleeves.values():
			state.mesh.queue_free()
		sleeves.clear()
		return false
	set_enabled(enabled)
	update_pose()
	return sleeves.size()==2

func _build_strip(state: Dictionary) -> void:
	var rows: Array[float] = []
	var upper: float = state.upper_length
	var lower: float = state.lower_length
	var total := upper+lower
	var start := -float(state.author.get("shoulder_overlap_pixels",8.5))
	var finish := total+float(state.author.get("hand_extent_pixels",15.0))
	var step := clampf(float(state.author.get("row_step_pixels",0.65)),0.3,1.2)
	var s := start
	while s<finish:
		rows.append(s)
		s += step
	# Exact elbow/wrist rows give the painter's anchors explicit seam-free rows.
	rows.append(0.0)
	rows.append(upper)
	rows.append(total)
	rows.append(finish)
	rows.sort()
	var clean: Array[float] = []
	for value in rows:
		if clean.is_empty() or value-clean.back()>0.02:
			clean.append(value)
		elif is_equal_approx(value,0.0) or is_equal_approx(value,upper) or is_equal_approx(value,total):
			clean[clean.size()-1] = value
	state.rows = clean
	for row in range(clean.size()):
		if is_equal_approx(clean[row],upper):
			clean.insert(row+1,upper)
			state.elbow_split_row = row+1
			break
	var uv_points := PackedVector2Array()
	var columns: int = state.columns
	var rest_lower := (state.wrist-state.elbow).normalized() as Vector2
	var rest_upper := (state.elbow-state.shoulder).normalized() as Vector2
	for row in range(clean.size()):
		var distance: float = clean[row]
		if distance>=total-0.0001 and int(state.rigid_hand_start)<0: state.rigid_hand_start = uv_points.size()
		var direction := rest_upper if row<int(state.elbow_split_row) else rest_lower
		var centre: Vector2 = state.shoulder+rest_upper*distance if row<int(state.elbow_split_row) else state.elbow+rest_lower*(distance-upper)
		var normal := Vector2(-direction.y,direction.x)
		for column in range(columns+1):
			var lateral := lerpf(-float(state.half_span_pixels),float(state.half_span_pixels),float(column)/columns)
			uv_points.append((centre+normal*lateral)/CANVAS)
	var triangles := []
	for row in range(clean.size()-1):
		if row==int(state.elbow_split_row)-1: continue
		for column in range(columns):
			var a := row*(columns+1)+column
			var b := a+1
			var c := a+columns+1
			triangles.append(PackedInt32Array([a,c,b]))
			triangles.append(PackedInt32Array([b,c,c+1]))
	state.uv_points = uv_points
	var native_uv := PackedVector2Array()
	for point in uv_points: native_uv.append(point*state.mesh.texture.get_size())
	state.mesh.uv = native_uv
	state.mesh.polygons = triangles
	# Hand points share one rigid transform, irrespective of sleeve bending.
	var hand_index: int = state.rigid_hand_start
	if hand_index>=0 and hand_index+columns<uv_points.size():
		state.hand_pair_distance = (uv_points[hand_index]*CANVAS).distance_to(uv_points[hand_index+columns]*CANVAS)*float(state.pixels_to_world)

func update_pose(_delta: float = 0.0, _clip: String = "") -> void:
	if not enabled: return
	var skin: Variant = owner_ref.get_ref() if owner_ref!=null else null
	if skin==null: return
	for state in sleeves.values():
		var side: String = state.side
		var shoulder: Vector2 = skin.bones[side+"_upper"].position
		# Bury the rounded cap in the torso instead of widening it to bridge a seam.
		var torso_axis: Vector2 = skin.bones.head.position.lerp(skin.bones.pelvis.position,0.24)
		shoulder = shoulder.lerp(torso_axis,0.18)
		var source_elbow: Vector2 = skin.bones[side+"_forearm"].position
		var source_wrist: Vector2 = skin.bones[side+"_hand"].position
		var upper_dir := (source_elbow-shoulder).normalized()
		var lower_dir := (source_wrist-source_elbow).normalized()
		var elbow := shoulder+upper_dir*fixed_upper_length
		var wrist := elbow+lower_dir*fixed_lower_length
		# The complete cuff and hand share the forearm rotation. No progressive
		# wrist twist, frame-dependent width, or changing endpoint scale is applied.
		var rest_lower := (state.wrist-state.elbow).normalized() as Vector2
		var rest_upper := (state.elbow-state.shoulder).normalized() as Vector2
		var upper_rotation := upper_dir.angle()-rest_upper.angle()
		var rotation := lower_dir.angle()-rest_lower.angle()
		var vertices := PackedVector2Array()
		var columns: int = state.columns
		var total: float = state.total_length
		for row in range(state.rows.size()):
			var distance: float = state.rows[row]
			for column in range(columns+1):
				var index := row*(columns+1)+column
				var point: Vector2
				if distance>=total-0.0001:
					point = wrist+(state.uv_points[index]*CANVAS-state.wrist).rotated(rotation)*float(state.pixels_to_world)
				else:
					var source_point: Vector2 = state.uv_points[index]*CANVAS
					var upper_fraction := clampf(distance/float(state.upper_length),0.0,1.0)
					# The painted chart narrows 193px -> 139px already. Compensate
					# that taper instead of multiplying it into a pinched elbow.
					var width_factor := lerpf(0.98,1.26,smoothstep(0.0,1.0,upper_fraction))
					if distance>float(state.upper_length):
						var lower_fraction := (distance-float(state.upper_length))/float(state.lower_length)
						width_factor = lerpf(1.26,0.84,smoothstep(0.0,0.80,lower_fraction))
						# Rejoin the rigid wrist without a discontinuity.
						width_factor = lerpf(width_factor,1.0,smoothstep(0.80,1.0,lower_fraction))
					var up_local: Vector2 = source_point-state.shoulder
					var low_local: Vector2 = source_point-state.elbow
					var rest_up_normal := Vector2(-rest_upper.y,rest_upper.x)
					var rest_low_normal := Vector2(-rest_lower.y,rest_lower.x)
					var upper_point := shoulder+(rest_upper*up_local.dot(rest_upper)*fixed_upper_length/float(state.upper_length)+rest_up_normal*up_local.dot(rest_up_normal)*float(state.pixels_to_world)*width_factor).rotated(upper_rotation)
					var lower_point := elbow+(rest_lower*low_local.dot(rest_lower)*fixed_lower_length/float(state.lower_length)+rest_low_normal*low_local.dot(rest_low_normal)*float(state.pixels_to_world)*width_factor).rotated(rotation)
					# Duplicated elbow rows are disconnected: each belongs to one
					# rigid piece. Cloth overlap replaces texture-warp blending.
					point = upper_point if row<int(state.elbow_split_row) else lower_point
				vertices.append(point)
		state.mesh.polygon = vertices
		_update_elbow_joint(state,elbow,upper_dir,lower_dir,rest_upper)
		state.source_mesh.visible = false
		state.mesh.visible = visible
		var hand_index: int = state.rigid_hand_start
		var hand_error := 0.0
		if hand_index>=0 and hand_index+columns<vertices.size():
			hand_error = absf(vertices[hand_index].distance_to(vertices[hand_index+columns])-float(state.hand_pair_distance))
		state.last = {"shoulder":shoulder,"elbow":elbow,"wrist":wrist,"source_elbow":source_elbow,"source_wrist":source_wrist,"fixed_upper_length":fixed_upper_length,"fixed_lower_length":fixed_lower_length,"hand_scale_error":hand_error,"vertices":vertices.size(),"hand_rotation":rotation,"joint_turn":upper_dir.angle_to(lower_dir)}
	update_count += 1

func _update_elbow_joint(state: Dictionary, elbow: Vector2, upper_dir: Vector2, lower_dir: Vector2, rest_upper: Vector2) -> void:
	var turn := upper_dir.angle_to(lower_dir)
	var joint: Polygon2D = state.joint_mesh
	joint.visible = visible and absf(turn)>0.005
	var folds: ElbowFolds = state.folds
	folds.visible = joint.visible
	if not joint.visible: return
	var outside := -signf(turn)
	# Actual master alpha at row577 spans x519..665 around axis595.
	var radius := (76.0 if outside>0.0 else 70.0)*60.0/1254.0
	var normal := Vector2(-upper_dir.y,upper_dir.x)*outside
	var rest_normal := Vector2(-rest_upper.y,rest_upper.x)*outside
	var points := PackedVector2Array([elbow])
	var uv := PackedVector2Array([state.elbow/CANVAS*state.mesh.texture.get_size()])
	var triangles := []
	for index in range(25):
		var angle := turn*float(index)/24.0
		points.append(elbow+normal.rotated(angle)*radius*1.26*float(state.pixels_to_world))
		# Sample inside the opaque elbow cloth; the joint geometry supplies its
		# silhouette, rather than inheriting transparent cut edges from the PNG.
		uv.append((state.elbow+rest_normal.rotated(angle)*radius*0.80)/CANVAS*state.mesh.texture.get_size())
		if index>0: triangles.append(PackedInt32Array([0,index,index+1]))
	joint.polygon = points
	joint.uv = uv
	joint.polygons = triangles
	folds.outline = PackedVector2Array()
	for index in range(1,points.size()): folds.outline.append(points[index])
	folds.creases.clear()
	folds.strength = smoothstep(0.35,1.5,absf(turn))
	folds.stroke = 0.13*float(state.pixels_to_world)
	var inner := -(Vector2(-upper_dir.y,upper_dir.x)+Vector2(-lower_dir.y,lower_dir.x)).normalized()*outside
	var size: float = state.pixels_to_world
	# Tapered, local cloth creases imply compression; source cloth outside
	# the joint remains a rigid image and receives no new distortion.
	folds.creases.append(PackedVector2Array([elbow+inner*1.45*size-upper_dir*1.1*size,elbow+inner*1.8*size-upper_dir*0.8*size,elbow-inner*0.9*size]))
	folds.creases.append(PackedVector2Array([elbow+inner*1.4*size+lower_dir*1.15*size,elbow+inner*1.75*size+lower_dir*0.85*size,elbow-inner*0.4*size+lower_dir*0.2*size]))
	folds.queue_redraw()

func set_enabled(value: bool) -> void:
	enabled = value
	for state in sleeves.values():
		state.source_mesh.visible = visible and not enabled
		state.mesh.visible = visible and enabled
		state.joint_mesh.visible = visible and enabled and absf(float(state.get("last",{}).get("joint_turn",1.0)))>0.005
		state.folds.visible = state.joint_mesh.visible

func set_visible(value: bool) -> void:
	visible = value
	set_enabled(enabled)

func get_component_mesh(component: String) -> Polygon2D:
	if not sleeves.has(component): return null
	return sleeves[component].mesh

func set_component_visible(component: String, value: bool) -> void:
	if not sleeves.has(component): return
	sleeves[component].mesh.visible = value
	sleeves[component].joint_mesh.visible = value
	sleeves[component].folds.visible = value

func dispose() -> void:
	for state in sleeves.values():
		if is_instance_valid(state.source_mesh): state.source_mesh.visible = visible
		if is_instance_valid(state.mesh): state.mesh.queue_free()
		if is_instance_valid(state.joint_mesh): state.joint_mesh.queue_free()
		if is_instance_valid(state.folds): state.folds.queue_free()
	sleeves.clear()

func get_diagnostics() -> Dictionary:
	var parts := {}
	for key in sleeves:
		var state: Dictionary = sleeves[key]
		parts[key] = {"author":state.author,"canvas":[60,60],"vertices":state.mesh.polygon.size(),"triangles":state.mesh.polygons.size(),"hand_rigid":true,"width_scale":state.pixels_to_world,"last":state.get("last",{})}
	return {"enabled":enabled,"visible":visible,"errors":errors,"updates":update_count,"parts":parts,
		"source_frames_modified":false,"source_current_vertices_modified":false,"source_meshes_hidden_only":true,
		"method":"fixed shared Idle lengths; rigid upper/forearm and cuff/hand; split hinge with local rounded outer elbow fill",
		"scope":"project-authored sleeve reconstruction, not the original tight-sleeve mesh or complete cloth simulation"}
