extends SceneTree

const OUTPUT := "res://docs/美术规范/惊蛰原地引雷与落雷_20261020/雷柱修订/"
const MOTION := "res://tmp/jingzhe_column_motion_20261020/"
const SKILL := "res://resources/activeskills/玄枢含章经/惊蛰.tres"
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
		var directory := MOTION+sex+"/"
		DirAccess.make_dir_recursive_absolute(directory)
		scene.call("SetPreviewCaster",female)
		scene.call("PreviewSkill",SKILL,1 if female else 0)
		for index in range(75):
			await process_frame
			await RenderingServer.frame_post_draw
			var frame := root.get_texture().get_image()
			frame.save_png(directory+"motion_%03d.png"%index)
			if index==8: frame.save_png(OUTPUT+sex+"_cast.png")
			if index==17: frame.save_png(OUTPUT+sex+"_strike.png")
			if index==29: frame.save_png(OUTPUT+sex+"_remainder.png")
			if index==70: frame.save_png(OUTPUT+sex+"_idle.png")
	print("JINGZHE_COLUMN_CAPTURE_COMPLETE: existing showcase, male/female and opposite directions. No save writes.")
	if "--keep-open" in OS.get_cmdline_user_args():
		scene.call("SetPreviewCaster",false)
		scene.call("PreviewSkill",SKILL,0)
		root.gui_disable_input = false
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position = Vector2i(100,60)
		return
	scene.queue_free()
	await process_frame
	quit()
