extends SceneTree

var directory := "res://tmp/ink_response_capture/"
var kinds: Array[String]=["hurt","guard","restore"]
var offscreen := false
var preview: Node2D

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			directory=argument.trim_prefix("--capture-dir=").trim_suffix("/")+"/"
		if argument.begins_with("--actions="):
			kinds.clear()
			for requested in argument.trim_prefix("--actions=").split(","):
				if requested in ["attack","hurt","guard","restore","dodge"]:
					kinds.append(requested)
		if argument=="--offscreen":
			offscreen=true
	call_deferred("_capture_showcase")

func _capture_showcase() -> void:
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,900)
	root.content_scale_size=Vector2i(1440,900)
	if offscreen:
		root.position=Vector2i(-4096,-4096)
	var packed:=load("res://tscn/scene/BattlePoseLibraryShowcase.tscn") as PackedScene
	preview=packed.instantiate()
	root.add_child(preview)
	preview.set_process(false)
	preview.set_process_unhandled_input(false)
	root.gui_disable_input=true
	await process_frame
	DirAccess.make_dir_recursive_absolute(directory)
	for kind in kinds:
		preview._set_action(kind)
		preview.comparison=false
		preview.selected_actor=0
		preview.background_mode=2
		preview.mirrored=false
		preview._layout()
		var peak:=1.43 if kind=="hurt" else (1.48 if kind=="guard" else (1.63 if kind=="attack" else 1.82))
		await _capture(directory+kind+"_male.png",peak)
		var motion_dir: String=directory+str(kind)+"/"
		DirAccess.make_dir_recursive_absolute(motion_dir)
		for index in range(88):
			await _capture(motion_dir+"motion_%03d.png" % index,.55+index/30.0)
		preview.selected_actor=1
		preview._layout()
		await _capture(directory+kind+"_female.png",peak)
		preview.comparison=true
		preview.background_mode=0
		preview._layout()
		await _capture(directory+kind+"_comparison.png",peak)
		preview.comparison=false
		preview.mirrored=true
		preview.display_size_mode=1
		preview._layout()
		await _capture(directory+kind+"_mirror_190.png",peak)
		preview.display_size_mode=0
	preview.queue_free()
	await process_frame
	print("INK_RESPONSE_CAPTURE_COMPLETE: actual game entry; ",kinds,"; male/female; light/dark; mirrored190; ",kinds.size()*88," motion frames")
	quit()

func _capture(path: String, time: float) -> void:
	preview.cycle_time=time
	preview._refresh_cycle()
	await process_frame
	await RenderingServer.frame_post_draw
	var result:=root.get_texture().get_image().save_png(path)
	if result!=OK:
		push_error("Capture failed: "+path)
		quit(1)
