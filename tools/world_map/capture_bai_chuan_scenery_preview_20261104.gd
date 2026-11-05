extends SceneTree
## Fast, isolated GPU art review. Final acceptance also uses the real MainScene.

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var expected := ""
	var output := "res://output/bai_chuan_scenery_20261104/terrain_preview.png"
	var location := "bc_place_001"
	var zoom := 2
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--expected-appdata="): expected = argument.trim_prefix("--expected-appdata=")
		if argument.begins_with("--out="): output = argument.trim_prefix("--out=")
		if argument.begins_with("--location="): location = argument.trim_prefix("--location=")
		if argument.begins_with("--zoom="): zoom = int(argument.trim_prefix("--zoom="))
	if expected.is_empty() or OS.get_environment("APPDATA") != expected:
		push_error("Art review requires isolated APPDATA")
		quit(1)
		return
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1920, 1080)
	var map = load("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd").new()
	root.add_child(map)
	current_scene = map
	map.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for frame in 5: await process_frame
	map.set_process(false)
	map.restore_node_state({"current_node": location, "remaining_route": []})
	map.call("_set_zoom_level", zoom, root.size * 0.5)
	map.focus_player_node()
	for frame in 3: await process_frame
	await RenderingServer.frame_post_draw
	var screenshot := root.get_texture().get_image()
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output).get_base_dir())
	var status := screenshot.save_png(output)
	print("BAICHUAN_SCENERY_PREVIEW ", output, " ", status)
	screenshot = null
	current_scene = null
	map.queue_free()
	await process_frame
	quit(0 if status == OK else 1)
