extends Node2D

const ApprovedRuntime = preload("approved_outfit_fitting_runtime.gd")
var rig: Node2D
var skin: RefCounted
var clock := 0.0
var checks := 0
var failures: Array[String] = []
var frame_count := 0
var visible_robe_counts := []
var maximum_shared_edge_error := 0.0
var nonfinite_vertices := 0
var physical_fold_triangles := 0
var capture_enabled := false
var output := "res://output/approved_outfit_closed_surface_review"
var caption: Label
var native_captures := []
var rest_uv := PackedVector2Array()
var rest_topology: Array = []
var body_image: Image
var record: Dictionary
var hip_mask_review: Dictionary = {}
var motion_states: Dictionary = {}

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value: failures.append(message)

func _topology(mesh: Polygon2D) -> Dictionary:
	var adjacency := {}
	var counts := {}
	for triangle in mesh.polygons:
		for edge in [[triangle[0],triangle[1]],[triangle[1],triangle[2]],[triangle[2],triangle[0]]]:
			var a: int = edge[0]
			var b: int = edge[1]
			if not adjacency.has(a): adjacency[a] = []
			if not adjacency.has(b): adjacency[b] = []
			adjacency[a].append(b)
			adjacency[b].append(a)
			var key := str(mini(a,b))+":"+str(maxi(a,b))
			counts[key] = int(counts.get(key,0))+1
	var components := 0
	var visited := {}
	for seed in adjacency:
		if visited.has(seed): continue
		components += 1
		var queue := [seed]
		visited[seed] = true
		while not queue.is_empty():
			var point: int = queue.pop_back()
			for next in adjacency[point]:
				if visited.has(next): continue
				visited[next] = true
				queue.append(next)
	var boundary := {}
	var nonmanifold := 0
	for key in counts:
		if counts[key]>2: nonmanifold += 1
		if counts[key]!=1: continue
		var pieces: PackedStringArray = key.split(":")
		var a := int(pieces[0])
		var b := int(pieces[1])
		if not boundary.has(a): boundary[a] = []
		if not boundary.has(b): boundary[b] = []
		boundary[a].append(b)
		boundary[b].append(a)
	var boundary_loops := 0
	var bad_degree := 0
	visited.clear()
	for seed in boundary:
		if boundary[seed].size()!=2: bad_degree += 1
		if visited.has(seed): continue
		boundary_loops += 1
		var queue := [seed]
		visited[seed] = true
		while not queue.is_empty():
			var point: int = queue.pop_back()
			for next in boundary[point]:
				if not visited.has(next):
					visited[next] = true
					queue.append(next)
	return {"vertices":mesh.polygon.size(),"triangles":mesh.polygons.size(),"connected_components":components,"boundary_loops":boundary_loops,"bad_boundary_degrees":bad_degree,"nonmanifold_edges":nonmanifold}

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
	return result

func _tick(clip: String, velocity: Vector2, facing: float) -> void:
	clock += 1.0/60.0
	skin.set_motion_context(1.0/60.0,velocity,clock,facing)
	rig.set_character_pose(clip,clock,facing)
	frame_count += 1
	var robe_count := 0
	for item in skin.mesh_records:
		if str(item.name).begins_with("robe_") and item.mesh.visible: robe_count += 1
	visible_robe_counts.append(robe_count)
	var mesh: Polygon2D = record.mesh
	if mesh.uv!=rest_uv or mesh.polygons!=rest_topology:
		_check(false,"continuous cloth UV or topology changed during motion")
	var vertices := _posed_vertices()
	for point in vertices:
		if not point.is_finite(): nonfinite_vertices += 1
	# A cloth fold can overlap in projection. It must remain one indexed
	# surface; shared vertices are evaluated once rather than separate panels.
	for tri in mesh.polygons:
		var uv := (mesh.uv[tri[0]]+mesh.uv[tri[1]]+mesh.uv[tri[2]])/3.0
		if uv.y<750.0: continue
		var pixel := Vector2i(clampi(int(uv.x),0,body_image.get_width()-1),clampi(int(uv.y),0,body_image.get_height()-1))
		if body_image.get_pixelv(pixel).a<0.5: continue
		var original_area: float = (mesh.uv[tri[1]]-mesh.uv[tri[0]]).cross(mesh.uv[tri[2]]-mesh.uv[tri[0]])
		var posed_area := (vertices[tri[1]]-vertices[tri[0]]).cross(vertices[tri[2]]-vertices[tri[0]])
		if original_area*posed_area<0.0: physical_fold_triangles += 1

func _snapshot(name: String) -> void:
	if not capture_enabled: return
	caption.text = name
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var path := output.path_join(name+".png")
	_check(get_viewport().get_texture().get_image().save_png(ProjectSettings.globalize_path(path))==OK,"native capture "+name)
	native_captures.append(path)
	motion_states[name] = {"rear_angles":skin.garment_angles,"front_angles":skin.reference_front_angles,"crossing":skin.get_garment_diagnostics().approved_crossing}

func _check_hip_material() -> void:
	if not capture_enabled: return
	var leg_record: Dictionary = {}
	for item in skin.mesh_records:
		if item.name=="near_leg": leg_record = item
	if leg_record.is_empty():
		_check(false,"near leg material available for native alpha review")
		return
	var original: Image = leg_record.mesh.texture.get_image()
	var viewport := SubViewport.new()
	viewport.size = Vector2i(original.get_width(),original.get_height())
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	var sprite := Sprite2D.new()
	sprite.centered = false
	sprite.texture = leg_record.mesh.texture
	sprite.material = leg_record.mesh.material
	viewport.add_child(sprite)
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var rendered := viewport.get_texture().get_image()
	var probes := []
	for probe in [["hidden_cap",700.0,false],["underlap_below_hip",780.0,true],["knee",1080.0,true],["ankle",1417.0,true]]:
		var y := int(probe[1])
		var x := -1
		for candidate in range(330,515):
			if original.get_pixel(candidate,y).a>0.95:
				x = candidate
				break
		_check(x>=0,"original opaque leg sample exists at "+str(probe[0]))
		if x<0: continue
		var before := original.get_pixel(x,y).a
		var after := rendered.get_pixel(x,y).a
		probes.append({"region":probe[0],"source_px":[x,y],"before_alpha":before,"rendered_alpha":after})
		_check(after>0.90 if bool(probe[2]) else after<0.01,"native leg shader "+str(probe[0])+" obeys cap-only underlap policy")
	hip_mask_review = {"method":"original leg image rendered through actual current leg ShaderMaterial to transparent native-size SubViewport","probes":probes}
	rendered.save_png(ProjectSettings.globalize_path(output.path_join("11_hip_mask_native.png")))
	viewport.queue_free()

func _ready() -> void:
	capture_enabled = OS.get_cmdline_user_args().has("--capture")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): output = argument.trim_prefix("--capture-dir=")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
	if capture_enabled:
		get_window().size = Vector2i(1000,700)
		get_window().content_scale_size = Vector2i(1000,700)
		get_window().position = Vector2i(-7000,-7000)
		get_window().unfocusable = true
		var background := ColorRect.new()
		background.size = Vector2(1000,700)
		background.color = Color("cbcfc9")
		add_child(background)
		caption = Label.new()
		caption.position = Vector2(40,30)
		caption.add_theme_color_override("font_color",Color("3e4846"))
		caption.add_theme_font_size_override("font_size",22)
		add_child(caption)
	rig = ApprovedRuntime.new()
	add_child(rig)
	rig.position = Vector2(520,600)
	_check(rig.build(),"approved source outfit builds")
	if not rig.loaded:
		_finish({})
		return
	skin = rig.wanderer_skin
	# The approved skin explicitly disables its garment simulation along with
	# pose blending. Keep the actual preview setting for inertia/turn captures.
	skin.pose_blending = true
	_check(skin.approved_closed_robe_mesh!=null,"closed outer robe exists")
	record = skin.approved_closed_robe_record
	var mesh: Polygon2D = record.mesh
	var topology := _topology(mesh)
	_check(topology.connected_components==1,"outer cloth is one connected surface")
	_check(topology.boundary_loops==1 and topology.bad_boundary_degrees==0,"cloth has one outside boundary and no internal topological holes")
	_check(topology.nonmanifold_edges==0,"continuous cloth has no nonmanifold edges")
	body_image = mesh.texture.get_image()
	var source_image := Image.new()
	_check(source_image.load(ProjectSettings.globalize_path("res://art/yuxu_fullbody_v15/body.png"))==OK,"selected original PNG is present")
	_check(body_image.get_width()==source_image.get_width() and body_image.get_height()==source_image.get_height(),"closed cloth uses original texture dimensions")
	var source_bytes := source_image.get_data()
	_check(body_image.get_data().slice(0,source_bytes.size())==source_bytes,"closed cloth preserves original body PNG pixels")
	rest_uv = mesh.uv.duplicate()
	rest_topology = mesh.polygons.duplicate(true)
	_check(mesh.uv[0]==Vector2(275,310) and mesh.uv[-1]==Vector2(665,1340),"whole source body chart UV endpoints remain registered")
	for frame in range(60): _tick("Idle",Vector2.ZERO,1.0)
	await _snapshot("01_idle")
	for frame in range(120): _tick("Run_New1",Vector2(114,0),1.0)
	await _snapshot("02_run_right")
	_tick("Run_New1",Vector2(-114,0),-1.0)
	await _snapshot("03_turn_01")
	for frame in range(5): _tick("Run_New1",Vector2(-114,0),-1.0)
	await _snapshot("04_turn_06")
	for frame in range(9): _tick("Run_New1",Vector2(-114,0),-1.0)
	await _snapshot("05_turn_15")
	for frame in range(90): _tick("Run_New1",Vector2(-114,0),-1.0)
	await _snapshot("06_run_left")
	_tick("Idle",Vector2.ZERO,-1.0)
	await _snapshot("07_stop_01")
	for frame in range(5): _tick("Idle",Vector2.ZERO,-1.0)
	await _snapshot("08_stop_06")
	for frame in range(24): _tick("Idle",Vector2.ZERO,-1.0)
	await _snapshot("09_stop_30")
	for frame in range(180): _tick("Idle",Vector2.ZERO,-1.0)
	await _snapshot("10_settled")
	_check(visible_robe_counts.min()==1 and visible_robe_counts.max()==1,"exactly one outer robe remains visible in run, turn and stop")
	_check(nonfinite_vertices==0,"all evaluated continuous cloth points stay finite")
	await _check_hip_material()
	_finish(topology)

func _finish(topology: Dictionary) -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"frames":frame_count,"topology":topology,"visible_robe_count_range":[visible_robe_counts.min(),visible_robe_counts.max()] if not visible_robe_counts.is_empty() else [],"nonfinite_vertices":nonfinite_vertices,"projected_physical_fold_triangle_samples":physical_fold_triangles,"native_captures":native_captures,"motion_states":motion_states,"hip_material_native_review":hip_mask_review,"original_body_png_sha256":FileAccess.get_sha256("res://art/yuxu_fullbody_v15/body.png"),"scope":"One actual indexed closed outer cloth surface, exact original body texture/UV registration, native run-turn-stop capture with actual garment simulation enabled. Projected overlapping folds are reported rather than forbidden. No final aesthetic approval inferred."}
	FileAccess.open(output.path_join("closed_surface_review.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("APPROVED CLOSED SURFACE ","PASS" if failures.is_empty() else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if failures.is_empty() else 1)
