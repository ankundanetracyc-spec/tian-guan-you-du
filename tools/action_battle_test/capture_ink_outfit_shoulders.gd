extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")
var rig: Node2D

func _ready() -> void:
	get_window().size = Vector2i(640,480)
	get_window().content_scale_size = Vector2i(640,480)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	var background := ColorRect.new()
	background.size = Vector2(640,480)
	background.color = Color("dddcd1")
	add_child(background)
	rig = OutfitRuntime.new()
	add_child(rig)
	rig.display_zoom = 10.0
	if not rig.build():
		push_error("Shoulder audit requires the four current outfit materials: "+JSON.stringify(rig.load_errors))
		get_tree().quit(1)
		return
	rig.wanderer_skin.motion_context = false
	rig.wanderer_skin.pose_blending = false
	var destination := "res://output/ink_outfit_shoulder_cycle"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): destination = argument.trim_prefix("--capture-dir=")
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	var samples := []
	for clip in ["Idle","Run_New1","Run_New2"]:
		var data: Dictionary = rig.wanderer_skin.source_document.clips[clip]
		for index in range(24):
			var phase := float(index)/24.0
			rig.set_character_pose(clip,float(data.duration)*phase,1.0)
			rig.position += Vector2(320,205)-rig.wanderer_skin.bones.near_upper.global_position
			await get_tree().process_frame
			await RenderingServer.frame_post_draw
			var filename: String = clip+"_%03d.png"%index
			assert(get_viewport().get_texture().get_image().save_png(output.path_join(filename))==OK)
			samples.append({"file":filename,"clip":clip,"phase":phase,"sampled_phase":rig.wanderer_skin.sampled_source_phase})
	var materials: Dictionary = rig.wanderer_skin.material_diagnostics.duplicate(true)
	for key in materials: materials[key].sha256 = FileAccess.get_sha256(materials[key].path)
	var file := FileAccess.open(output.path_join("shoulder_capture_record.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"scope":"native close-up visual audit across complete Idle/Run1/Run2 cycles; no aesthetic pass inferred from data checks","frames_per_cycle":24,"clipped_viewport":true,"camera_focus":"near shoulder source attachment; pose and materials unchanged","pose_blending":false,"secondary_surrogate":false,"runtime":rig.get_runtime_summary(),"materials":materials,"samples":samples},"\t"))
	file.close()
	print("OUTFIT SHOULDER CYCLES COMPLETE: ",output)
	get_tree().quit()
