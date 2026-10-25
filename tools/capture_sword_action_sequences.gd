extends SceneTree
const OUTPUT := "res://docs/美术规范/剑招序列帧特效_20261024/"
const CAPTURE := "res://tmp/sword_action_motion/"
func _initialize() -> void:
	call_deferred("_capture")
func _capture() -> void:
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,810)
	root.position=Vector2i(-4096,-4096)
	root.get_node("GameplayInputRouter").set_process_input(false)
	var preview: Node=load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
	root.add_child(preview)
	current_scene=preview
	await create_timer(.35,false).timeout
	var caption := Label.new()
	caption.position=Vector2(460,315)
	caption.add_theme_font_size_override("font_size",28)
	caption.add_theme_color_override("font_color",Color("30443e"))
	caption.z_index=4000
	var caption_layer := CanvasLayer.new()
	caption_layer.layer=20
	root.add_child(caption_layer)
	caption_layer.add_child(caption)
	var ordinal := 0
	DirAccess.make_dir_recursive_absolute(CAPTURE)
	caption.text="右手持剑 · 待机"
	preview.call("PreviewSwordIdle",0,0)
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"held_right_idle_game.png")
	if "--still-only" not in OS.get_cmdline_user_args():
		for i in range(15):
			await process_frame
			await RenderingServer.frame_post_draw
			var frame := root.get_texture().get_image().get_region(Rect2i(420,300,1400,600))
			frame.resize(980,420,Image.INTERPOLATE_LANCZOS)
			frame.save_png(CAPTURE+"%03d.png"%ordinal)
			ordinal+=1
	for pose in ["cut","parry","nurture","finish"]:
		caption.text={"cut":"斩剑","parry":"格剑","nurture":"养锋","finish":"尽锋"}[pose]
		preview.call("PreviewSwordAction",pose,0,0)
		await process_frame
		await process_frame
		if "--still-only" not in OS.get_cmdline_user_args():
			for i in range(30):
				preview.call("SeekSwordAction",float(i)/15)
				await process_frame
				await RenderingServer.frame_post_draw
				var frame := root.get_texture().get_image().get_region(Rect2i(420,300,1400,600))
				frame.resize(980,420,Image.INTERPOLATE_LANCZOS)
				frame.save_png(CAPTURE+"%03d.png"%ordinal)
				ordinal+=1
		preview.call("SeekSwordAction",.82 if pose=="nurture" else .53)
		await process_frame
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(OUTPUT+pose+"_game.png")
	preview.call("PreviewSwordAction","finish",1,1)
	await process_frame
	preview.call("SeekSwordAction",.54)
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"finish_mirrored_broad.png")
	preview.call("PreviewSwordAction","finish",2,0)
	await process_frame
	preview.call("SeekSwordAction",.54)
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"finish_spined.png")
	preview.call("SetSwordPreviewDark",true)
	caption.text="斩剑"
	caption.add_theme_color_override("font_color",Color("dfd7bf"))
	preview.call("PreviewSwordAction","cut",0,0)
	await process_frame
	preview.call("SeekSwordAction",.53)
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"cut_dark.png")
	print("SWORD_ACTION_SEQUENCE_CAPTURE_COMPLETE frames=",ordinal)
	# Release the review rig while its paired dolls still exist. This also
	# removes the screen-reading closeup before the capture window shuts down.
	preview.call("PreviewSwordIdle",0,0)
	await process_frame
	preview.queue_free()
	await process_frame
	await process_frame
	quit()
