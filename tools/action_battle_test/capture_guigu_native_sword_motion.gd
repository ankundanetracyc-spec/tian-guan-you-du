extends Node2D

const NativeRuntime = preload("guigu_native_first_runtime.gd")
const FORMS := ["outer","return","down","up"]
const NAMES := ["外横斩","回横斩","下劈","上挑"]
var font := SystemFont.new()
var content: Node2D
var records := []
var capture_failures: Array[String] = []

func _label(value: String, position_value: Vector2, width_value: float, size_value: int) -> void:
	var label := Label.new()
	label.text = value
	label.position = position_value
	label.size = Vector2(width_value,36)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size_value)
	label.add_theme_color_override("font_color",Color("424b48"))
	content.add_child(label)

func _ready() -> void:
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	get_window().size = Vector2i(3136,1800)
	get_window().content_scale_size = Vector2i(3136,1800)
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	var destination: String = get_script().resource_path.get_base_dir().path_join("output/guigu_native_sword_motion_review")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): destination = argument.trim_prefix("--capture-dir=")
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	for form in range(4):
		if content!=null:
			remove_child(content)
			content.queue_free()
		content = Node2D.new()
		add_child(content)
		_build_gallery(form)
		if not capture_failures.is_empty():
			get_tree().quit(1)
			return
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var filename: String = "native_sword_four_"+FORMS[form]+".png"
		if get_viewport().get_texture().get_image().save_png(output.path_join(filename))!=OK:
			push_error("Native sword capture failed: "+filename)
			get_tree().quit(1)
			return
	FileAccess.open(output.path_join("native_sword_four_capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"samples":records,"scope":"Actual native renderer, four real 3D inspection forms. Every gallery: standing right/left then actual 1x running right/left. Original palm/arm lengths, fixed55 source sword with real projected shortening and split depth. No combat damage or skill migration."},"\t"))
	print("NATIVE FOUR SWINGS CAPTURE PASS: ",output)
	get_tree().quit()

func _build_gallery(form: int) -> void:
	var phases := [0.0,0.17,0.29,0.43,0.60,0.80,0.99999]
	var names := ["持架","蓄势","展剑","挥过","尽势","收回","归架"]
	var background := ColorRect.new()
	background.size = Vector2(3136,1800)
	background.color = Color("dddcd1")
	content.add_child(background)
	_label("原规格人物 · "+NAMES[form]+" · 真实肩肘腕与纵深",Vector2(0,12),3136,25)
	for row in range(4):
		var clip := "Idle" if row<2 else "Run_New1"
		var facing := 1.0 if row%2==0 else -1.0
		for index in range(phases.size()):
			_label(("站立" if row<2 else "跑动")+" · "+names[index]+"  %.3f秒"%(float(phases[index])*0.72),Vector2(index*448,53+row*426),448,19)
			var display := SubViewportContainer.new()
			display.position = Vector2(index*448+4,84+row*426)
			display.size = Vector2(440,390)
			display.stretch = true
			content.add_child(display)
			var viewport := SubViewport.new()
			viewport.size = Vector2i(440,390)
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			display.add_child(viewport)
			var backdrop := ColorRect.new()
			backdrop.size = Vector2(440,390)
			backdrop.color = Color("caced0")
			viewport.add_child(backdrop)
			var rig := NativeRuntime.new()
			viewport.add_child(rig)
			if not rig.build():
				capture_failures.append("native runtime build")
				push_error("Native sword capture build: "+str(rig.load_errors))
				return
			var skin: Variant = rig.wanderer_skin
			skin.motion_context = false
			skin.motion_delta = 0.0
			skin.physics_enabled = false
			skin.display_scale = 2.1
			rig.position = Vector2(220,360)
			var sword: Variant = skin.approved_sword
			sword.restart_swing(true,form)
			var pose_time: float = float(phases[index])*sword.swing_duration
			sword.swing_elapsed = pose_time
			if row>=2:
				sword.swing_elapsed = 0.0
				skin.set_motion_context(pose_time,Vector2(skin.ROBED_MOVE_SPEED*facing,0),pose_time,facing)
			rig.set_character_pose(clip,pose_time,facing)
			var bounds := Rect2(sword.sword_sprite.to_global(sword.source_tip),Vector2.ZERO)
			bounds = bounds.expand(sword.sword_sprite.to_global(sword.source_pommel))
			for record in skin.mesh_records:
				for vertex in record.mesh.polygon: bounds = bounds.expand(record.mesh.to_global(vertex))
			var within_panel := bounds.position.x>=0.0 and bounds.position.y>=0.0 and bounds.end.x<=440.0 and bounds.end.y<=390.0
			if not within_panel:
				capture_failures.append("geometry outside panel")
				push_error("Native sword gallery outside panel: "+str(bounds))
			records.append({"form":form,"phase":phases[index],"clip":clip,"facing":facing,"running_speed":skin.ROBED_MOVE_SPEED if row>=2 else 0.0,"sword":sword.get_diagnostics(),"hand_socket":skin.native_hand_socket.get_diagnostics(),"panel_geometry_bounds":bounds,"geometry_inside_panel":within_panel})
	_label("每式0.72秒无判伤试挥 · 原掌心握点与原骨长 · 剑源长55，纵深投影自然缩短",Vector2(0,1770),3136,17)
