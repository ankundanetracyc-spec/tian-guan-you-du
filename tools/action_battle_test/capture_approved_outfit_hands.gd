extends Node2D

const OutfitRuntime = preload("approved_outfit_fitting_runtime.gd")
const HandLayer = preload("approved_outfit_hand_layer.gd")
var font := SystemFont.new()
var helpers: Array = []

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
	var optional := OS.get_cmdline_user_args().has("--optional")
	var capture_clip := "Idle"
	var capture_time := 0.0
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--clip="): capture_clip = argument.trim_prefix("--clip=")
		if argument.begins_with("--time="): capture_time = float(argument.trim_prefix("--time="))
	var entries := [["near","sword_near","原版剑指 · 保留"],["near","grip_near","近侧右手 · 握柄"]] if optional else [["near","open","近侧右手 · 手背舒展"],["far","open","远侧左手 · 掌心舒展"],["near","fist","近侧右手 · 手背握拳"],["far","fist","远侧左手 · 掌心握拳"]]
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
	_label("旧版常服 · 备用近手" if optional else "旧版常服 · 两侧独立手型",Rect2(0,24,width,44),28)
	var records := []
	for index in range(entries.size()):
		var side: String = entries[index][0]
		var style: String = entries[index][1]
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
		assert(rig.build())
		var skin = rig.wanderer_skin
		skin.motion_context = false
		skin.pose_blending = false
		rig.display_zoom = 25.0
		rig.set_character_pose(capture_clip,capture_time,1.0)
		var layer = skin.get("approved_hands")
		if layer==null:
			layer = HandLayer.new()
			assert(layer.build(skin),str(layer.errors))
		helpers.append(layer)
		layer.set_pose_override(style)
		layer.update_pose()
		skin.set_visible(false)
		for record in skin.mesh_records:
			if record.name==side+"_arm": record.mesh.visible = true
		layer.set_visible(true)
		for other in layer.hands:
			if other!=side: layer.hands[other].node.visible = false
		var wrist_global: Vector2 = skin.bones[side+"_hand"].global_position
		rig.position += Vector2(85,250)-wrist_global
		records.append({"side":side,"style":style,"runtime":rig.get_runtime_summary(),"hand_layer":layer.get_diagnostics()})
	_label("袖身与袖口沿用原图 · 裸手分别制作 · 剑指保持原版" if optional else "近侧手背、远侧掌心 · 固定腕点与掌宽 · 不是整张袖子镜像",Rect2(0,608,width,28),17)
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var destination := "res://output/approved_outfit_hands_optional" if optional else "res://output/approved_outfit_hands_normal"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): destination = argument.trim_prefix("--capture-dir=")
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	assert(get_viewport().get_texture().get_image().save_png(output.path_join("near_optional_hands.png" if optional else "four_normal_hands.png"))==OK)
	FileAccess.open(output.path_join("hand_capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"hands":records,"scope":"native rigid hand sprites on the same approved-arm bones and cuff mask as the live fitting scene"},"\t"))
	print("APPROVED HAND NATIVE CAPTURE COMPLETE: ",output)
	get_tree().quit()
