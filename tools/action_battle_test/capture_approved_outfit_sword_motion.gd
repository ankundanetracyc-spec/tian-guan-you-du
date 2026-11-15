extends Node2D

const OutfitRuntime = preload("approved_outfit_fitting_runtime.gd")
var font := SystemFont.new()

func _label(value: String, position: Vector2, width: float, size: int) -> void:
	var label := Label.new()
	label.text = value
	label.position = position
	label.size = Vector2(width,36)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",Color("424b48"))
	add_child(label)

func _ready() -> void:
	var phases := [0.0,0.18,0.30,0.41,0.55,0.78,0.99999]
	var names := ["持架","提剑","蓄势","快斩","余势","缓收","归架"]
	var width := phases.size()*280
	get_window().size = Vector2i(width,930)
	get_window().content_scale_size = Vector2i(width,930)
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var background := ColorRect.new()
	background.size = Vector2(width,930)
	background.color = Color("dddcd1")
	add_child(background)
	_label("旧版常服 · 提剑、快斩与收势",Vector2(0,18),width,28)
	var records := []
	for row in range(2):
		for index in range(phases.size()):
			_label(names[index]+"  %.3f秒"%(float(phases[index])*0.72),Vector2(index*280,66+row*420),280,20)
			var display := SubViewportContainer.new()
			display.position = Vector2(index*280+4,110+row*420)
			display.size = Vector2(272,368)
			display.stretch = true
			add_child(display)
			var viewport := SubViewport.new()
			viewport.size = Vector2i(272,368)
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			display.add_child(viewport)
			var backdrop := ColorRect.new()
			backdrop.size = Vector2(272,368)
			backdrop.color = Color("caced0")
			viewport.add_child(backdrop)
			var rig := OutfitRuntime.new()
			viewport.add_child(rig)
			if not rig.build():
				push_error("Sword motion native capture build: "+str(rig.load_errors))
				get_tree().quit(1)
				return
			var skin: Variant = rig.wanderer_skin
			skin.motion_context = false
			skin.pose_blending = false
			skin.motion_delta = 0.0
			var sword: Variant = skin.approved_sword
			sword.restart_swing(true)
			sword.swing_elapsed = float(phases[index])*sword.swing_duration
			rig.display_zoom = 2.8
			rig.position = Vector2(105 if row==0 else 167,340)
			rig.set_character_pose("AttackIdle",0.0,1.0 if row==0 else -1.0)
			records.append({"phase":phases[index],"facing":1.0 if row==0 else -1.0,"sword":sword.get_diagnostics()})
	_label("0.72秒 · 手臂与剑保持当前比例",Vector2(0,890),width,19)
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var destination: String = get_script().resource_path.get_base_dir().path_join("output/approved_outfit_sword_motion_review")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): destination = argument.trim_prefix("--capture-dir=")
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	var saved := get_viewport().get_texture().get_image().save_png(output.path_join("sword_motion_native_gallery.png"))
	FileAccess.open(output.path_join("sword_motion_capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"samples":records,"scope":"Native renderer phase gallery of the actual approved runtime in both facings. No compositing or source-image modification."},"\t"))
	print("APPROVED SWORD MOTION NATIVE CAPTURE ","PASS" if saved==OK else "FAIL",": ",output)
	get_tree().quit(0 if saved==OK else 1)
