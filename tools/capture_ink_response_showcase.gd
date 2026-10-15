extends SceneTree

const DIRECTORY := "res://tmp/ink_response_capture/"
var preview: Node2D

func _initialize() -> void:
	call_deferred("_capture_showcase")

func _capture_showcase() -> void:
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,900)
	root.content_scale_size=Vector2i(1440,900)
	var packed:=load("res://tscn/scene/BattlePoseLibraryShowcase.tscn") as PackedScene
	preview=packed.instantiate()
	root.add_child(preview)
	preview.set_process(false)
	await process_frame
	DirAccess.make_dir_recursive_absolute(DIRECTORY)
	for kind in ["hurt","guard","restore"]:
		preview._set_action(kind)
		preview.comparison=false
		preview.selected_actor=0
		preview.background_mode=2
		preview.mirrored=false
		preview._layout()
		var peak:=1.43 if kind=="hurt" else (1.48 if kind=="guard" else 1.82)
		await _capture(DIRECTORY+kind+"_male.png",peak)
		var motion_dir: String=DIRECTORY+str(kind)+"/"
		DirAccess.make_dir_recursive_absolute(motion_dir)
		for index in range(88):
			await _capture(motion_dir+"motion_%03d.png" % index,.55+index/30.0)
		preview.selected_actor=1
		preview._layout()
		await _capture(DIRECTORY+kind+"_female.png",peak)
		preview.comparison=true
		preview.background_mode=0
		preview._layout()
		await _capture(DIRECTORY+kind+"_comparison.png",peak)
		preview.comparison=false
		preview.mirrored=true
		preview.display_size_mode=1
		preview._layout()
		await _capture(DIRECTORY+kind+"_mirror_190.png",peak)
		preview.display_size_mode=0
	preview.queue_free()
	await process_frame
	print("INK_RESPONSE_CAPTURE_COMPLETE: actual game entry; hurt,guard,restore; male/female; light/dark; mirrored190; 264 motion frames")
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
