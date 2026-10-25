extends SceneTree

const OUTPUT := "res://docs/美术规范/炼器剑图标重绘_20261023/"
var scene: Node

func _initialize() -> void:
	call_deferred("_capture")

func _capture() -> void:
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,810)
	root.position=Vector2i(-4096,-4096)
	root.gui_disable_input=true
	root.get_node("GameplayInputRouter").set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	scene=load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await create_timer(.5,false).timeout
	if "--preview-only" in OS.get_cmdline_user_args():
		scene.call("PreviewSwordIdle",0,0)
		root.gui_disable_input=false
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position=Vector2i(100,60)
		return
	var sheet := Image.create(2100,570,false,Image.FORMAT_RGBA8)
	for appearance in range(3):
		scene.call("PreviewSwordIdle",appearance,0)
		await create_timer(.25,false).timeout
		await RenderingServer.frame_post_draw
		var frame := root.get_texture().get_image()
		frame.save_png(OUTPUT+"sword_%d.png"%appearance)
		var closeup := frame.get_region(Rect2i(780,300,700,570))
		closeup.convert(Image.FORMAT_RGBA8)
		closeup.save_png(OUTPUT+"sword_%d_closeup.png"%appearance)
		sheet.blit_rect(closeup,Rect2i(0,0,700,570),Vector2i(appearance*700,0))
	sheet.save_png(OUTPUT+"sword_comparison.png")
	scene.call("PreviewSwordIdle",1,1)
	await create_timer(.25,false).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"mirrored_broad.png")
	print("SWORD_IDLE_CAPTURE_COMPLETE: same male pose with three independent sword icons, plus mirrored broad sword.")
	quit()
