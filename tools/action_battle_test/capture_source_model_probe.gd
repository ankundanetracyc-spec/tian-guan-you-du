extends "smoke_source_model_build.gd"

func _ready() -> void:
	get_window().unfocusable = true
	get_window().position = Vector2i(-7000,-7000)
	get_window().size = Vector2i(440,500)
	get_window().content_scale_size = Vector2i(440,500)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	var backdrop := ColorRect.new()
	backdrop.color = Color("58646a")
	backdrop.size = Vector2(440,500)
	add_child(backdrop)
	var rig := SourceTestRig.new()
	add_child(rig)
	assert(rig.load_copy())
	rig.set_skin_mode("experiment")
	rig.position = Vector2(220,440)
	var skin = rig.wanderer_skin
	var destination := ProjectSettings.globalize_path("res://output/source_model_adapter_probe")
	DirAccess.make_dir_recursive_absolute(destination)
	for clip in ["Idle","Run_New1","BackstepWalk","AttackIdle"]:
		for frame in range(60):
			var clock := float(frame+1)/60.0
			skin.set_motion_context(1.0/60.0,Vector2(114,0) if clip=="Run_New1" else Vector2.ZERO,clock,1.0)
			rig.set_character_pose(clip,clock,1.0)
			rig.scale *= 4.0
			if frame in [30,42,59]:
				await get_tree().process_frame
				await RenderingServer.frame_post_draw
				var picture := get_viewport().get_texture().get_image()
				assert(picture.save_png(destination.path_join(clip+"_%03d.png"%frame))==OK)
	print("SOURCE MODEL ADAPTER CAPTURE COMPLETE: ",destination)
	get_tree().quit()
