extends RefCounted

## Our continuous sleeve carrier, authored against the actual painted image.
## Source shoulders/elbows/wrists are targets only. This does not reproduce the
## source 35/39-vertex tight sleeves, modify source frames, or warp texture UVs.
## A rounded elbow spine and its tangent carry continuous sleeve sections.
## The hand is rigid
## from its own painted wrist, so elbow bending cannot stretch the fingers.

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
		skin.runtime.rig_root_node.add_child(mesh)
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
	var uv_points := PackedVector2Array()
	var columns: int = state.columns
	var rest_lower := (state.wrist-state.elbow).normalized() as Vector2
	var rest_state: Dictionary = state.duplicate(false)
	rest_state.pixels_to_world = 1.0
	for row in range(clean.size()):
		var distance: float = clean[row]
		if distance>=total-0.0001 and int(state.rigid_hand_start)<0: state.rigid_hand_start = uv_points.size()
		var sample := _spine(distance,state.shoulder,state.elbow,state.wrist,rest_lower,rest_state,0.0)
		var normal := Vector2(-sample.direction.y,sample.direction.x)
		for column in range(columns+1):
			var lateral := lerpf(-float(state.half_span_pixels),float(state.half_span_pixels),float(column)/columns)
			uv_points.append((sample.point+normal*lateral)/CANVAS)
	var triangles := []
	for row in range(clean.size()-1):
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

func _spine(distance: float, shoulder: Vector2, elbow: Vector2, wrist: Vector2, hand_direction: Vector2, state: Dictionary, sag: float) -> Dictionary:
	var upper_dir := (elbow-shoulder).normalized()
	var lower_dir := (wrist-elbow).normalized()
	var upper: float = state.upper_length
	var lower: float = state.lower_length
	var total := upper+lower
	# Round the actual centreline, not just section normals around a sharp corner.
	# A quadratic arc joins both straight segments tangentially. This prevents
	# the lower elbow outline from inheriting the old V-shaped centreline cusp.
	var bend_span := minf(upper,lower)*0.50
	var bend_start := upper-bend_span
	var bend_end := upper+bend_span
	if distance<0.0:
		return {"point":shoulder+upper_dir*distance*float(state.pixels_to_world),"direction":upper_dir}
	if distance<bend_start:
		var t := distance/upper
		return {"point":shoulder.lerp(elbow,t),"direction":upper_dir}
	if distance<=bend_end:
		var t := (distance-bend_start)/(bend_end-bend_start)
		var a := shoulder.lerp(elbow,bend_start/upper)
		var b := elbow.lerp(wrist,bend_span/lower)
		var first := a.lerp(elbow,t)
		var second := elbow.lerp(b,t)
		return {"point":first.lerp(second,t),"direction":(second-first).normalized()}
	if distance<total:
		var t := (distance-upper)/lower
		var wrist_turn := smoothstep(bend_span/lower,1.0,t)
		var sample := {"point":elbow.lerp(wrist,t),"direction":lower_dir.lerp(hand_direction,wrist_turn).normalized()}
		# A quarter-pixel-scale sag is cosmetic; no spring/state/physics is used.
		sample.point += Vector2.DOWN*(sin(PI*wrist_turn)*sag)
		return sample
	return {"point":wrist+hand_direction*(distance-total)*float(state.pixels_to_world),"direction":hand_direction}

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
		var elbow: Vector2 = skin.bones[side+"_forearm"].position
		var wrist: Vector2 = skin.bones[side+"_hand"].position
		var joint_turn := absf((elbow-shoulder).angle_to(wrist-elbow))
		var rotation: float = skin.bones[side+"_hand"].rotation+float(state.hand_angle_offset)
		var rest_lower := (state.wrist-state.elbow).normalized() as Vector2
		var hand_direction := rest_lower.rotated(rotation)
		var vertices := PackedVector2Array()
		var columns: int = state.columns
		var total: float = state.total_length
		for row in range(state.rows.size()):
			var distance: float = state.rows[row]
			var sample := _spine(distance,shoulder,elbow,wrist,hand_direction,state,float(state.sag_pixels)*float(state.pixels_to_world))
			var normal := Vector2(-sample.direction.y,sample.direction.x)
			for column in range(columns+1):
				var index := row*(columns+1)+column
				var point: Vector2
				if distance>=total-0.0001:
					point = wrist+(state.uv_points[index]*CANVAS-state.wrist).rotated(rotation)*float(state.pixels_to_world)
				else:
					var lateral := lerpf(-float(state.half_span_pixels),float(state.half_span_pixels),float(column)/columns)
					var upper_fraction := clampf(distance/float(state.upper_length),0.0,1.0)
					# The painted chart narrows 193px -> 139px already. Compensate
					# that taper instead of multiplying it into a pinched elbow.
					var width_factor := lerpf(0.98,1.26,smoothstep(0.0,1.0,upper_fraction))
					if distance>float(state.upper_length):
						var lower_fraction := (distance-float(state.upper_length))/float(state.lower_length)
						width_factor = lerpf(1.26,0.84,smoothstep(0.0,0.80,lower_fraction))
						# Rejoin the rigid wrist without a discontinuity.
						width_factor = lerpf(width_factor,1.0,smoothstep(0.80,1.0,lower_fraction))
					# Preserve perceived thickness where the cross-section turns at the elbow.
					var elbow_zone := 1.0-smoothstep(0.0,float(state.upper_length)*0.45,absf(distance-float(state.upper_length)))
					width_factor *= 1.0+0.22*elbow_zone*clampf(joint_turn/(PI*0.5),0.0,1.0)
					point = sample.point+normal*lateral*float(state.pixels_to_world)*width_factor
				vertices.append(point)
		state.mesh.polygon = vertices
		state.source_mesh.visible = false
		state.mesh.visible = visible
		var hand_index: int = state.rigid_hand_start
		var hand_error := 0.0
		if hand_index>=0 and hand_index+columns<vertices.size():
			hand_error = absf(vertices[hand_index].distance_to(vertices[hand_index+columns])-float(state.hand_pair_distance))
		state.last = {"shoulder":shoulder,"elbow":elbow,"wrist":wrist,"hand_scale_error":hand_error,"vertices":vertices.size(),"hand_rotation":rotation}
	update_count += 1

func set_enabled(value: bool) -> void:
	enabled = value
	for state in sleeves.values():
		state.source_mesh.visible = visible and not enabled
		state.mesh.visible = visible and enabled

func set_visible(value: bool) -> void:
	visible = value
	set_enabled(enabled)

func get_component_mesh(component: String) -> Polygon2D:
	if not sleeves.has(component): return null
	return sleeves[component].mesh

func dispose() -> void:
	for state in sleeves.values():
		if is_instance_valid(state.source_mesh): state.source_mesh.visible = visible
		if is_instance_valid(state.mesh): state.mesh.queue_free()
	sleeves.clear()

func get_diagnostics() -> Dictionary:
	var parts := {}
	for key in sleeves:
		var state: Dictionary = sleeves[key]
		parts[key] = {"author":state.author,"canvas":[60,60],"vertices":state.mesh.polygon.size(),"triangles":state.mesh.polygons.size(),"hand_rigid":true,"width_scale":state.pixels_to_world,"last":state.get("last",{})}
	return {"enabled":enabled,"visible":visible,"errors":errors,"updates":update_count,"parts":parts,
		"source_frames_modified":false,"source_current_vertices_modified":false,"source_meshes_hidden_only":true,
		"method":"authored shared-chart UV strip; tangent-continuous rounded elbow centreline; independent rigid wrist/hand",
		"scope":"project-authored sleeve reconstruction, not the original tight-sleeve mesh or complete cloth simulation"}
