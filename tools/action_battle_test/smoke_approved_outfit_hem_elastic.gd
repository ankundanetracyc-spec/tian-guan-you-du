extends Node2D

const Runtime = preload("approved_outfit_fitting_runtime.gd")
var rig: Node2D
var skin: RefCounted
var record: Dictionary
var checks := 0
var failures: Array[String] = []
var clock := 0.0
var frames := 0
var rest_uv := PackedVector2Array()
var rest_topology: Array = []
var maximum_anchor_error := 0.0
var maximum_limb_scale_error := 0.0
var maximum_leg_length_error := 0.0
var maximum_arm_length_error := 0.0
var maximum_state_angle := 0.0
var maximum_segment_angles := Vector3.ZERO
var finite := true
var topology_unchanged := true
var one_robe := true
var metrics: Dictionary = {}

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value: failures.append(message)

func _point(bone: Node2D) -> Vector2:
	return rig.rig_root_node.to_local(bone.global_position)

func _posed_vertices() -> PackedVector2Array:
	var root_inverse: Transform2D = rig.rig_root_node.global_transform.affine_inverse()
	var transforms: Array[Transform2D] = []
	for index in range(record.keys.size()): transforms.append(root_inverse*skin.bones[record.keys[index]].global_transform*record.rest[index].affine_inverse())
	var mesh: Polygon2D = record.mesh
	var result := PackedVector2Array()
	for vertex in range(mesh.polygon.size()):
		var point := Vector2.ZERO
		for index in range(transforms.size()): point += (transforms[index]*mesh.polygon[vertex])*float(record.weights[index][vertex])
		result.append(point)
		finite = finite and point.is_finite()
		if mesh.uv[vertex].y>=790.0 and mesh.uv[vertex].y<835.0:
			# Waist follows the actual pelvis, rather than being frozen in world space.
			maximum_anchor_error = maxf(maximum_anchor_error,point.distance_to(transforms[1]*mesh.polygon[vertex]))
	return result

func _tick(clip: String, velocity: Vector2, facing: float = 1.0, delta: float = 1.0/60.0) -> void:
	clock += delta
	skin.set_motion_context(delta,velocity,clock,facing)
	rig.set_character_pose(clip,clock,facing)
	frames += 1
	var mesh: Polygon2D = record.mesh
	topology_unchanged = topology_unchanged and mesh.uv==rest_uv and mesh.polygons==rest_topology
	_posed_vertices()
	var visible := 0
	for item in skin.mesh_records:
		if str(item.name).begins_with("robe_") and item.mesh.visible: visible += 1
	one_robe = one_robe and visible==1
	for angles in [skin.garment_angles,skin.reference_front_angles]:
		finite = finite and angles.is_finite()
		maximum_segment_angles = Vector3(maxf(maximum_segment_angles.x,absf(angles.x)),maxf(maximum_segment_angles.y,absf(angles.y)),maxf(maximum_segment_angles.z,absf(angles.z)))
		for angle in [angles.x,angles.y,angles.z]: maximum_state_angle = maxf(maximum_state_angle,absf(angle))
	for side in ["near","far"]:
		var thigh: Bone2D = skin.bones[side+"_thigh"]
		var calf: Bone2D = skin.bones[side+"_calf"]
		var foot: Bone2D = skin.bones[side+"_foot"]
		maximum_leg_length_error = maxf(maximum_leg_length_error,maxf(absf(_point(thigh).distance_to(_point(calf))-skin.approved_leg_lengths.x),absf(_point(calf).distance_to(_point(foot))-skin.approved_leg_lengths.y)))
		var upper: Bone2D = skin.bones[side+"_upper"]
		var forearm: Bone2D = skin.bones[side+"_forearm"]
		var hand: Bone2D = skin.bones[side+"_hand"]
		maximum_arm_length_error = maxf(maximum_arm_length_error,maxf(absf(_point(upper).distance_to(_point(forearm))-forearm.rest.origin.length()),absf(_point(forearm).distance_to(_point(hand))-hand.rest.origin.length())))
		for bone in [thigh,calf,foot,upper,forearm,hand]:
			maximum_limb_scale_error = maxf(maximum_limb_scale_error,bone.scale.distance_to(Vector2.ONE))
			finite = finite and bone.position.is_finite() and is_finite(bone.rotation)

func _topology() -> Dictionary:
	var adjacency := {}
	var edge_count := {}
	for triangle in record.mesh.polygons:
		for edge in [[triangle[0],triangle[1]],[triangle[1],triangle[2]],[triangle[2],triangle[0]]]:
			var a: int = edge[0]
			var b: int = edge[1]
			if not adjacency.has(a): adjacency[a] = []
			if not adjacency.has(b): adjacency[b] = []
			adjacency[a].append(b)
			adjacency[b].append(a)
			var key := str(mini(a,b))+":"+str(maxi(a,b))
			edge_count[key] = int(edge_count.get(key,0))+1
	var visited := {}
	var components := 0
	for seed in adjacency:
		if visited.has(seed): continue
		components += 1
		var queue := [seed]
		visited[seed] = true
		while not queue.is_empty():
			var current: int = queue.pop_back()
			for next in adjacency[current]:
				if not visited.has(next):
					visited[next] = true
					queue.append(next)
	var boundary := {}
	var nonmanifold := 0
	for key in edge_count:
		if edge_count[key]>2: nonmanifold += 1
		if edge_count[key]!=1: continue
		var pieces: PackedStringArray = key.split(":")
		for piece in pieces: boundary[piece] = int(boundary.get(piece,0))+1
	var bad_degree := 0
	for key in boundary:
		if boundary[key]!=2: bad_degree += 1
	return {"vertices":record.mesh.polygon.size(),"triangles":record.mesh.polygons.size(),"components":components,"boundary_vertices":boundary.size(),"bad_boundary_degree":bad_degree,"nonmanifold_edges":nonmanifold,"euler_characteristic":adjacency.size()-edge_count.size()+record.mesh.polygons.size()}

func _state() -> Dictionary:
	var rear: Vector3 = skin.garment_angles
	var front: Vector3 = skin.reference_front_angles
	return {"rear":[rear.x,rear.y,rear.z],"front":[front.x,front.y,front.z],"rear_load":skin.approved_cross_rear_load,"front_load":skin.approved_cross_front_load}

func _ready() -> void:
	rig = Runtime.new()
	add_child(rig)
	_check(rig.build(),"actual independent preview builds")
	if not rig.loaded:
		_finish()
		return
	skin = rig.wanderer_skin
	record = skin.approved_closed_robe_record
	rest_uv = record.mesh.uv.duplicate()
	rest_topology = record.mesh.polygons.duplicate(true)
	metrics.topology = _topology()
	_check(metrics.topology.components==1 and metrics.topology.euler_characteristic==1 and metrics.topology.bad_boundary_degree==0 and metrics.topology.nonmanifold_edges==0,"outer cloth is one connected disk with a closed boundary and no internal holes")
	var hashes: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://output/approved_outfit_hem_elastic_review/before/original_png_sha256.json"))
	for name in hashes: _check(FileAccess.get_sha256("res://art/yuxu_fullbody_v15/"+name)==hashes[name],"original PNG unchanged: "+name)
	var original := Image.new()
	_check(original.load(ProjectSettings.globalize_path("res://art/yuxu_fullbody_v15/body.png"))==OK,"original body PNG decodes")
	var actual: Image = record.mesh.texture.get_image()
	# GPU texture includes generated mip levels; compare its full-size source level.
	_check(actual.get_width()==original.get_width() and actual.get_height()==original.get_height() and actual.get_data().slice(0,original.get_data().size())==original.get_data(),"continuous robe samples unchanged original body pixels")
	for frame in range(60): _tick("Idle",Vector2.ZERO)
	metrics.idle = _state()
	var rear_edge_min := INF
	var rear_edge_max := -INF
	var front_edge_min := INF
	var front_edge_max := -INF
	for frame in range(180):
		_tick("Run_New1",Vector2(114,0))
		if frame>=60:
			rear_edge_min = minf(rear_edge_min,skin.garment_angles.z)
			rear_edge_max = maxf(rear_edge_max,skin.garment_angles.z)
			front_edge_min = minf(front_edge_min,skin.reference_front_angles.z)
			front_edge_max = maxf(front_edge_max,skin.reference_front_angles.z)
	metrics.run = _state()
	metrics.steady_rear_edge_angle_range_rad = [rear_edge_min,rear_edge_max]
	metrics.steady_front_edge_angle_range_rad = [front_edge_min,front_edge_max]
	_check(skin.garment_angles.length()>0.05 and rear_edge_max-rear_edge_min>0.02 and front_edge_max-front_edge_min>0.02,"lower cloth trails and both free edges keep moving during fixed-speed steady run")
	_check((skin.garment_angles-skin.reference_front_angles).length()>0.005,"connected cloth retains front/rear layer depth")
	for frame in range(3): _tick("Idle",Vector2.ZERO)
	metrics.stop03 = _state()
	_check(skin.garment_angles.length()>0.03,"hem keeps its inertia in the first stopping frames")
	for frame in range(15): _tick("Idle",Vector2.ZERO)
	metrics.stop18 = _state()
	for frame in range(42): _tick("Idle",Vector2.ZERO)
	metrics.stop60 = _state()
	for frame in range(180): _tick("Idle",Vector2.ZERO)
	metrics.settled = _state()
	_check(skin.garment_angles.length()<0.01 and skin.reference_front_angles.length()<0.01,"both lower cloth chains settle back to hanging after stop")
	for frame in range(120): _tick("Run_New1",Vector2(-114,0),-1.0)
	for frame in range(60): _tick("Cast",Vector2.ZERO,-1.0)
	for frame in range(60): _tick("Idle",Vector2.ZERO,-1.0)
	var frozen: Dictionary = skin.get_garment_diagnostics().duplicate(true)
	var frozen_points := _posed_vertices()
	for frame in range(12): _tick("Idle",Vector2.ZERO,-1.0,0.0)
	_check(frozen==skin.get_garment_diagnostics() and frozen_points==_posed_vertices(),"pause freezes actual posed cloth and all diagnostic state")
	_check(finite and maximum_segment_angles.x<=0.95001 and maximum_segment_angles.y<=0.58001 and maximum_segment_angles.z<=0.58001,"all posed vertices and bone states remain finite within the released cloth limits")
	_check(one_robe and topology_unchanged,"exactly one visible robe keeps original UV and topology throughout")
	_check(maximum_anchor_error<0.0002,"waist cloth stays attached to moving pelvis without extra bulge")
	_check(maximum_leg_length_error<0.0002 and maximum_arm_length_error<0.0002 and maximum_limb_scale_error<0.00001,"all arm and leg lengths and scales remain fixed")
	_finish()

func _finish() -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"frames":frames,"failures":failures,"maximum_waist_anchor_error":maximum_anchor_error,"maximum_leg_length_error":maximum_leg_length_error,"maximum_arm_length_error":maximum_arm_length_error,"maximum_limb_scale_error":maximum_limb_scale_error,"maximum_state_angle_rad":maximum_state_angle,"maximum_segment_angles_rad":[maximum_segment_angles.x,maximum_segment_angles.y,maximum_segment_angles.z],"allowed_segment_angles_rad":[0.95,0.58,0.58],"metrics":metrics,"scope":"Actual single connected cloth; moving waist attachment, released lower trailing/deceleration/settling, layer difference, turns/non-run clip/pause, unchanged PNG/UV/topology and fixed limb proportions. Visual review remains separate."}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output/approved_outfit_hem_elastic_review"))
	FileAccess.open("res://output/approved_outfit_hem_elastic_review/hem_elastic_smoke.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("APPROVED OUTFIT HEM ELASTIC ","PASS" if failures.is_empty() else "FAIL"," ",checks," checks; ",frames," frames; ",JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
