extends Node2D

const OutfitRuntime = preload("approved_outfit_fitting_runtime.gd")
var font := SystemFont.new()

func _label(value: String, rect: Rect2, size: int) -> void:
	var label := Label.new()
	label.text = value
	label.position = rect.position
	label.size = rect.size
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",Color("424b48"))
	add_child(label)

func _ready() -> void:
	var bent := OS.get_cmdline_user_args().has("--bent")
	var entries := [["near","open","近侧右臂 · 手背舒展"],["far","open","远侧左臂 · 掌心舒展"],["near","fist","近侧右臂 · 手背握拳"],["far","fist","远侧左臂 · 掌心握拳"],["near","grip_near","近侧右臂 · 握柄"],["near","sword_near","原版剑指 · 保留"]]
	var width := entries.size()*300
	get_window().size = Vector2i(width,650)
	get_window().content_scale_size = Vector2i(width,650)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var background := ColorRect.new()
	background.size = Vector2(width,650)
	background.color = Color("dddcd1")
	add_child(background)
	_label("旧版常服 · 完整肩至指尖手臂"+(" · 屈臂" if bent else ""),Rect2(0,24,width,44),28)
	var records := []
	for index in range(entries.size()):
		var side: String = entries[index][0]
		_label(entries[index][2],Rect2(index*300,90,300,36),19)
		var display := SubViewportContainer.new()
		display.position = Vector2(index*300+10,140)
		display.size = Vector2(280,450)
		display.stretch = true
		add_child(display)
		var viewport := SubViewport.new()
		viewport.size = Vector2i(280,450)
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		display.add_child(viewport)
		var backdrop := ColorRect.new()
		backdrop.size = Vector2(280,450)
		backdrop.color = Color("d3d4ca")
		viewport.add_child(backdrop)
		var rig := OutfitRuntime.new()
		viewport.add_child(rig)
		if not rig.build():
			push_error("Full arm gallery build: "+str(rig.load_errors))
			get_tree().quit(1)
			return
		var skin: Variant = rig.wanderer_skin
		skin.motion_context = false
		skin.pose_blending = false
		rig.display_zoom = 12.0
		rig.set_character_pose("Idle",0.0,1.0)
		skin._aim_arm(side,deg_to_rad(88.0 if bent else 72.0),deg_to_rad(-72.0 if bent else -32.0),0.0)
		var layer: Variant = skin.approved_hands
		layer.set_pose_override(entries[index][1])
		layer.update_pose()
		skin.set_visible(false)
		var state: Dictionary = layer.hands[side]
		state.mesh.visible = true
		if state.cap!=null: state.cap.visible = true
		var shoulder: Vector2 = skin.bones[side+"_upper"].global_position
		rig.position += Vector2(70,150)-shoulder
		records.append({"side":side,"style":entries[index][1],"full_arm_layer":layer.get_diagnostics()})
	_label("整条手臂与手共同生成 · 原骨长与掌宽保持 · 原版剑指另行保留",Rect2(0,608,width,28),18)
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var destination := "res://output/approved_outfit_full_arms_bent" if bent else "res://output/approved_outfit_full_arms_gallery"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): destination = argument.trim_prefix("--capture-dir=")
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	var saved := get_viewport().get_texture().get_image().save_png(output.path_join("complete_arm_gallery.png"))
	FileAccess.open(output.path_join("full_arm_capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"arms":records,"bent":bent,"scope":"native renderer full-arm gallery using the actual fitting runtime; no source-image slicing or composite"},"\t"))
	print("COMPLETE ARM NATIVE CAPTURE ","PASS" if saved==OK else "FAIL",": ",output)
	get_tree().quit(0 if saved==OK else 1)
