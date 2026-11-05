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
	get_window().size = Vector2i(1500,900)
	get_window().content_scale_size = Vector2i(1500,900)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var background := ColorRect.new()
	background.size = Vector2(1500,900)
	background.color = Color("dddcd1")
	add_child(background)
	_label("同尺寸袖身 · 伸臂与弯臂",Rect2(0,30,1500,48),30)
	var records := []
	var rig_entries := []
	var rig_count := 4
	for index in range(rig_count):
		var side := "near" if index%2==0 else "far"
		var bent := index<2
		_label(("近侧" if side=="near" else "远侧")+" · "+("弯臂" if bent else "伸臂"),Rect2(index*375,115,375,34),22)
		var rig := OutfitRuntime.new()
		add_child(rig)
		rig.position = Vector2(index*375+170,790)
		rig.display_zoom = 6.2
		assert(rig.build())
		var skin = rig.wanderer_skin
		skin.motion_context = false
		skin.pose_blending = false
		skin.set_hand_pose_override("open")
		rig.set_character_pose("Idle",0.0,1.0)
		var bones: Dictionary = skin.bones
		var shoulder: Vector2 = bones[side+"_upper"].position
		# These are isolated geometry review inputs, not new gameplay actions.
		# Both sides use the exact same physical lengths, in both poses.
		var upper_length: float = 21.5*skin.source_scale
		var lower_length: float = 15.0*skin.source_scale
		var upper_direction := Vector2(0.07,1.0).normalized()
		var lower_direction := Vector2(1.0,-0.42).normalized() if bent else Vector2(0.17,1.0).normalized()
		var elbow: Vector2 = shoulder+upper_direction*upper_length
		var wrist: Vector2 = elbow+lower_direction*lower_length
		bones[side+"_forearm"].position = elbow
		bones[side+"_hand"].position = wrist
		var state: Dictionary = skin.sleeve_reconstruction.sleeves["sleeve_"+side]
		var rest: Vector2 = (state.wrist-state.elbow).normalized()
		bones[side+"_hand"].rotation = lower_direction.angle()-rest.angle()-float(state.hand_angle_offset)
		skin.sleeve_reconstruction.update_pose(0.0,"geometry_review")
		# Expose one complete sleeve per figure so both sides can be compared.
		var other := "far" if side=="near" else "near"
		skin.sleeve_reconstruction.set_component_visible("sleeve_"+other,false)
		records.append({"side":side,"pose":"bent" if bent else "extended","upper_length":upper_length,"lower_length":lower_length,"independent_geometry_input":true,"runtime":rig.get_runtime_summary()})
		rig_entries.append({"rig":rig,"side":side})
	_label("同一主袖片、同一横向尺标；端点仅供形状审查，不新增玩法动作",Rect2(0,835,1500,30),18)
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var destination := "res://output/ink_outfit_v15_sleeve_pose_review"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): destination = argument.trim_prefix("--capture-dir=")
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	assert(get_viewport().get_texture().get_image().save_png(output.path_join("bent_extended_same_scale.png"))==OK)
	for index in range(rig_entries.size()):
		var rig: Node2D = rig_entries[index].rig
		var side: String = rig_entries[index].side
		rig.wanderer_skin.set_visible(false)
		rig.wanderer_skin.sleeve_reconstruction.set_component_visible("sleeve_"+side,true)
		rig.scale = Vector2.ONE*rig.wanderer_skin.display_scale*12.0
		await get_tree().process_frame
		rig.position += Vector2(index*375+100,230)-rig.wanderer_skin.bones[side+"_upper"].global_position
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	assert(get_viewport().get_texture().get_image().save_png(output.path_join("isolated_bent_extended_same_scale.png"))==OK)
	var file := FileAccess.open(output.path_join("sleeve_pose_capture_record.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"scope":"native whole-outfit bent/extended same-length sleeve geometry comparison","poses":records},"\t"))
	file.close()
	print("SLEEVE BENT/EXTENDED NATIVE REVIEW COMPLETE: ",output)
	get_tree().quit()
