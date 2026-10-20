extends SceneTree

const OUTPUT := "res://docs/美术规范/常规法术挥手抛出_20261020/"
const MOTION := "res://tmp/throw_motion_20261020/"
var scene: Node

func _initialize() -> void:
	call_deferred("_record")

func _record() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1440,810)
	root.position = Vector2i(-4096,-4096)
	root.gui_disable_input = true
	root.get_node("GameplayInputRouter").set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	scene = load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await create_timer(.5,false).timeout
	Engine.max_fps = 30
	for female in [false,true]:
		var sex := "female" if female else "male"
		scene.call("SetPreviewCaster",female)
		scene.call("PreviewThrowPose",1 if female else 0)
		await _clip(sex+"_pose",48,{8:sex+"_release.png",43:sex+"_idle.png"})
		scene.call("PreviewSkill","res://resources/activeskills/灵击.tres",1 if female else 0)
		await _clip(sex+"_spell",60,{17:sex+"_spell.png"})
	print("THROW_CAPTURE_COMPLETE: current showcase, male/female, both directions, pose-only and existing Lingji presentation. No save writes.")
	if "--keep-open" in OS.get_cmdline_user_args():
		scene.call("SetPreviewCaster",false)
		scene.call("PreviewThrowPose",0)
		root.gui_disable_input = false
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position = Vector2i(100,60)
		return
	scene.queue_free()
	await process_frame
	quit()

func _clip(label: String, count: int, shots: Dictionary) -> void:
	var directory := MOTION+label+"/"
	DirAccess.make_dir_recursive_absolute(directory)
	for index in range(count):
		await process_frame
		await RenderingServer.frame_post_draw
		var frame := root.get_texture().get_image()
		frame.save_png(directory+"motion_%03d.png"%index)
		if shots.has(index): frame.save_png(OUTPUT+str(shots[index]))
