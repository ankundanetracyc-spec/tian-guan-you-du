extends SceneTree

# Capture the actual queue battle, without controlling the user's editor/window.
const OUTPUT := "res://docs/美术规范/行动队列局内形象_20261103/"
var frames_path := "res://tmp/queue_avatar_idle_capture/"
var suffix := ""
var scene_path := "res://tools/QueueAvatarBattlePreview.tscn"

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--scene="):
			scene_path = argument.trim_prefix("--scene=")
		if argument == "--v3":
			suffix = "v3_"
			frames_path = "res://tmp/queue_avatar_idle_capture_v3/"
	call_deferred("_capture")

func _capture() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1440, 810)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	var input_router := root.get_node_or_null("GameplayInputRouter")
	if input_router:
		input_router.set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	DirAccess.make_dir_recursive_absolute(frames_path)
	var packed := load(scene_path) as PackedScene
	if packed == null:
		push_error("Missing queue avatar scene: " + scene_path)
		quit(1)
		return
	var scene := packed.instantiate()
	root.add_child(scene)
	current_scene = scene
	for index in range(45):
		await process_frame
	var toggle := scene.find_child("PlanningToggle", true, false)
	if toggle is Button and "隐藏" in toggle.text:
		toggle.emit_signal("pressed")
		await process_frame
	await RenderingServer.frame_post_draw
	var skeletons := root.find_children("*", "Skeleton2D", true, false)
	var bones := root.find_children("*", "Bone2D", true, false)
	print("QUEUE_CAPTURE_RIGS: skeletons=", skeletons.size(), ", bones=", bones.size())
	if skeletons.size() < 2:
		push_error("Expected both battle facing directions using Skeleton2D")
		quit(1)
		return
	var initial := _bone_positions(bones)
	var moved := false
	for index in range(180):
		await process_frame
		await RenderingServer.frame_post_draw
		var frame := root.get_texture().get_image()
		if frame.save_png(frames_path + "motion_%03d.png" % index) != OK:
			push_error("Cannot save runtime frame")
			quit(1)
			return
		if index in [0, 45, 90, 135]:
			frame.save_png(OUTPUT + "queue_idle_" + suffix + "%03d.png" % index)
		var positions := _bone_positions(bones)
		for bone_index in range(initial.size()):
			if not initial[bone_index].is_equal_approx(positions[bone_index]):
				moved = true
	paused = true
	await process_frame
	var frozen := _bone_positions(bones)
	for index in range(15):
		await process_frame
	var still_frozen := _bone_positions(bones)
	var pause_ok := true
	for index in range(frozen.size()):
		if not frozen[index].is_equal_approx(still_frozen[index]):
			pause_ok = false
	paused = false
	print("QUEUE_CAPTURE_RESULT: frames=180, fixed_fps=30, idle_moved=", moved, ", tree_pause_freezes=", pause_ok)
	quit(0 if moved and pause_ok else 1)

func _bone_positions(bones: Array[Node]) -> Array[Transform2D]:
	var result: Array[Transform2D] = []
	for bone in bones:
		result.append((bone as Node2D).global_transform)
	return result
