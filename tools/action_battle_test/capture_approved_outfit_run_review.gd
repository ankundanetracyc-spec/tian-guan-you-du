extends Node2D

const Runtime = preload("approved_outfit_fitting_runtime.gd")
const BeforeRuntime = preload("res://output/approved_outfit_run_review/before/before_runtime.gd")
var font := SystemFont.new()
var output := "res://output/approved_outfit_run_review"
var clips: Array = []
var records: Array = []
var rendered_frames := 0

func _label(value: String, point: Vector2, size: Vector2, font_size: int = 20) -> void:
	var label := Label.new()
	label.text = value
	label.position = point
	label.size = size
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",font_size)
	label.add_theme_color_override("font_color",Color("454e49"))
	add_child(label)

func _viewport(point: Vector2, size: Vector2i) -> SubViewport:
	var display := SubViewportContainer.new()
	display.position = point
	display.size = size
	display.stretch = true
	add_child(display)
	var viewport := SubViewport.new()
	viewport.size = size
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	display.add_child(viewport)
	var background := ColorRect.new()
	background.size = size
	background.color = Color("c9cdc4")
	viewport.add_child(background)
	var floor := Line2D.new()
	floor.points = PackedVector2Array([Vector2(5,size.y-25),Vector2(size.x-5,size.y-25)])
	floor.width = 1.0
	floor.default_color = Color("a7aea5")
	viewport.add_child(floor)
	return viewport

func _actor(viewport: SubViewport, before: bool, zoom: float, point: Vector2) -> Node2D:
	var rig: Node2D = BeforeRuntime.new() if before else Runtime.new()
	viewport.add_child(rig)
	assert(rig.build(),"frozen/live actor could not build")
	rig.display_zoom = zoom
	rig.position = point
	if OS.get_cmdline_user_args().has("--sword"): rig.wanderer_skin.approved_sword.set_mode("hold")
	return rig

func _tick(rig: Node2D, time: float, moving: bool, facing: float, dt: float) -> void:
	rig.wanderer_skin.set_motion_context(dt,Vector2(114.0*facing,0.0) if moving else Vector2.ZERO,time,facing)
	rig.set_character_pose("Run_New1" if moving else "Idle",time,facing)

func _record(rig: Node2D) -> Dictionary:
	var skin = rig.wanderer_skin
	var bones := {}
	for name in skin.bones:
		var bone: Bone2D = skin.bones[name]
		var point: Vector2 = rig.rig_root_node.to_local(bone.global_position)
		bones[name] = {"point":[point.x,point.y],"rotation":bone.rotation,"scale":[bone.scale.x,bone.scale.y]}
	var result := {"phase":skin.gait_phase,"bones":bones,"contacts":skin.ground_contacts,"skin":skin.get_garment_diagnostics()}
	if OS.get_cmdline_user_args().has("--probe-uv"):
		var posed_meshes := []
		for record in skin.mesh_records:
			if record.name not in ["near_leg","far_leg"]: continue
			var mesh: Polygon2D = record.mesh
			var transforms: Array[Transform2D] = []
			for index in range(record.keys.size()): transforms.append(skin.bones[record.keys[index]].global_transform*record.rest[index].affine_inverse())
			var screen := []
			var uv := []
			var triangles := []
			for vertex in range(mesh.polygon.size()):
				var point := Vector2.ZERO
				for index in range(transforms.size()): point += (transforms[index]*mesh.polygon[vertex])*float(record.weights[index][vertex])
				screen.append([point.x,point.y])
				uv.append([mesh.uv[vertex].x,mesh.uv[vertex].y])
			for triangle in mesh.polygons: triangles.append(Array(triangle))
			posed_meshes.append({"name":record.name,"screen":screen,"uv":uv,"triangles":triangles})
		result.posed_meshes = posed_meshes
	return result

func _snapshot(name: String) -> void:
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	assert(get_viewport().get_texture().get_image().save_png(ProjectSettings.globalize_path(output.path_join(name)))==OK)
	rendered_frames += 1

func _grid() -> void:
	get_window().size = Vector2i(2400,1040)
	get_window().content_scale_size = Vector2i(2400,1040)
	_label("同一尺标 · 旧步态与当前步态",Vector2(0,8),Vector2(2400,40),28)
	for row in range(2):
		_label("调整前 · 固定旧脚本" if row==0 else "调整后 · 当前实际模型",Vector2(0,54+row*480),Vector2(2400,28),22)
		for column in range(8):
			var phase := float(column)/8.0
			_label("%d / 8"%column,Vector2(column*300,87+row*480),Vector2(300,25),17)
			var viewport := _viewport(Vector2(column*300,115+row*480),Vector2i(300,425))
			var rig := _actor(viewport,row==0,3.4,Vector2(155,400))
			for frame in range(120): _tick(rig,float(frame+1)/60.0,true,1.0,1.0/60.0)
			var skin = rig.wanderer_skin
			skin.set_motion_context(1.0/60.0,Vector2(114,0),2.1,1.0)
			skin.gait_phase = phase
			skin.pending_gait_phase = phase
			skin.gait_last_clock = 2.1
			rig.set_character_pose("Run_New1",2.1,1.0)
			records.append({"variant":"before" if row==0 else "after","requested_phase":phase,"actual":_record(rig)})
	await _snapshot("eight_phase_comparison.png")

func _video() -> void:
	get_window().size = Vector2i(1100,690)
	get_window().content_scale_size = Vector2i(1100,690)
	_label("同一尺标 · 连续起跑、转向与停步",Vector2(0,8),Vector2(1100,40),28)
	_label("调整前",Vector2(0,65),Vector2(550,30),22)
	_label("调整后",Vector2(550,65),Vector2(550,30),22)
	var before := _actor(_viewport(Vector2(0,108),Vector2i(550,540)),true,4.5,Vector2(285,515))
	var after := _actor(_viewport(Vector2(550,108),Vector2i(550,540)),false,4.5,Vector2(285,515))
	var only_frame := -1
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--only-frame="): only_frame = int(argument.trim_prefix("--only-frame="))
	for frame in range(240):
		var moving := frame>=30 and frame<180
		var facing := -1.0 if frame>=120 else 1.0
		var time := float(frame+1)/30.0
		_tick(before,time,moving,facing,1.0/30.0)
		_tick(after,time,moving,facing,1.0/30.0)
		if frame%15==0 or frame==only_frame: records.append({"frame":frame,"moving":moving,"facing":facing,"before":_record(before),"after":_record(after)})
		if only_frame<0 or frame==only_frame: await _snapshot("frame_%03d.png"%frame)
		if frame==only_frame: break

func _ready() -> void:
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var paper := ColorRect.new()
	paper.size = Vector2(2400,1040)
	paper.color = Color("e0ded2")
	add_child(paper)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	var video := OS.get_cmdline_user_args().has("--video")
	output = output.path_join("video" if video else "phases")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): output = argument.trim_prefix("--capture-dir=")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
	if video: await _video()
	else: await _grid()
	FileAccess.open(output.path_join("capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"native":true,"same_zoom_for_both_variants":true,"fps":30,"frames":rendered_frames,"frozen_baseline":"res://output/approved_outfit_run_review/before/","records":records},"\t"))
	print("APPROVED RUN REVIEW NATIVE COMPLETE ",ProjectSettings.globalize_path(output))
	get_tree().quit()
