extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")
var font := SystemFont.new()

func _label(text: String, rect: Rect2, size: int) -> void:
	var label := Label.new()
	label.text = text
	label.position = rect.position
	label.size = rect.size
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",Color("424b48"))
	add_child(label)

func _ready() -> void:
	var optional := OS.get_cmdline_user_args().has("--optional")
	var sword_only := OS.get_cmdline_user_args().has("--sword-only")
	optional = optional or sword_only
	var count := 1 if sword_only else (2 if optional else 4)
	var width := count*300
	get_window().size = Vector2i(width,600)
	get_window().content_scale_size = Vector2i(width,600)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var background := ColorRect.new()
	background.size = Vector2(width,600)
	background.color = Color("dddcd1")
	add_child(background)
	_label("近侧右手 · 剑指" if sword_only else ("近手预备手型" if optional else "普通手型 · 原生近景"),Rect2(0,22,width,46),26 if sword_only else 29)
	var details := []
	for index in range(count):
		var side := "near" if optional or index%2==0 else "far"
		var style := ("sword_near" if index==0 else "grip_near") if optional else ("open" if index<2 else "fist")
		var caption := "手背朝屏幕" if sword_only else (("剑指" if index==0 else "握剑柄") if optional else (("近侧" if side=="near" else "远侧")+" · "+("舒展" if style=="open" else "松握拳")))
		_label(caption,Rect2(index*300,90,300,34),22)
		var container := SubViewportContainer.new()
		container.position = Vector2(index*300+10,138)
		container.size = Vector2(280,420)
		container.stretch = true
		add_child(container)
		var viewport := SubViewport.new()
		viewport.size = Vector2i(280,420)
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		container.add_child(viewport)
		var backdrop := ColorRect.new()
		backdrop.size = Vector2(280,420)
		backdrop.color = Color("d3d4ca")
		viewport.add_child(backdrop)
		var rig := OutfitRuntime.new()
		viewport.add_child(rig)
		rig.display_zoom = 40.0 if sword_only else 25.0
		assert(rig.build())
		rig.wanderer_skin.motion_context = false
		rig.wanderer_skin.pose_blending = false
		if optional: rig.wanderer_skin.set_hand_pose_override(style)
		var clip := "Idle" if optional or style=="open" else "Run_New1"
		rig.set_character_pose(clip,0.0,1.0)
		rig.wanderer_skin.set_visible(false)
		if rig.wanderer_skin.sleeve_reconstruction!=null and rig.wanderer_skin.sleeve_reconstruction_enabled:
			rig.wanderer_skin.sleeve_reconstruction.set_component_visible("sleeve_"+side,true)
		else:
			for record in rig.wanderer_skin.mesh_records:
				if record.component=="sleeve_"+side: record.mesh.visible = true
		await get_tree().process_frame
		var rendered_wrist: Vector2 = rig.wanderer_skin.sleeve_reconstruction.sleeves["sleeve_"+side].last.wrist
		rig.position += Vector2(140,205)-rig.rig_root_node.to_global(rendered_wrist)
		details.append({"side":side,"style":style,"source_clip":clip,"source_phase":0.0,"isolated_arm_for_review":true,"runtime":rig.get_runtime_summary()})
	_label("仅近手显式预备，不绑定技能" if optional else "待机舒展 · 跑动松拳 · 手型局部替换，袖身保持",Rect2(0,565,width,28),17)
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var destination := "res://output/ink_outfit_01_hands_final"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): destination = argument.trim_prefix("--capture-dir=")
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	assert(get_viewport().get_texture().get_image().save_png(output.path_join("near_optional_hands_native.png" if optional else "four_normal_hands_native.png"))==OK)
	var file := FileAccess.open(output.path_join("optional_hand_closeup_record.json" if optional else "hand_closeup_record.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"scope":"native hand closeups; same continuous sleeve meshes/shaders as fitting room; other body parts hidden only to expose each complete hand","hands":details,"optional_near":"explicit sword_near/grip_near only; no skill mapping or normal idle/run change"},"\t"))
	file.close()
	print("FOUR NORMAL HANDS NATIVE CAPTURE COMPLETE: ",output)
	get_tree().quit()
