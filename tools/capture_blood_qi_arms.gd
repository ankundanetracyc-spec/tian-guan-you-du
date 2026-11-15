extends SceneTree

const OUTPUT := "res://docs/美术规范/气血法相主体与环绕血气_20261020/"
const MOTION := "res://tmp/blood_arms_orbit_motion_20261020/"
var scene: Node

func _initialize() -> void:
	call_deferred("_record")

func _record() -> void:
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
	Engine.max_fps=30
	if "--keys" in OS.get_cmdline_user_args():
		var names := ["key_single_punch","key_double_punch","key_double_guard"]
		var sheet := Image.create(1950,500,false,Image.FORMAT_RGBA8)
		sheet.fill(Color("e0d6bd"))
		for mode in range(3):
			scene.call("PreviewBloodActionKey",mode)
			await create_timer(1.0,false).timeout
			await RenderingServer.frame_post_draw
			var frame := root.get_texture().get_image()
			frame.save_png(OUTPUT+names[mode]+".png")
			var closeup := frame.get_region(Rect2i(800,330,650,500))
			closeup.convert(Image.FORMAT_RGBA8)
			closeup.save_png(OUTPUT+names[mode]+"_closeup.png")
			sheet.blit_rect(closeup,Rect2i(0,0,650,500),Vector2i(mode*650,0))
		sheet.save_png(OUTPUT+"action_keys.png")
		print("BLOOD_ARMS_ACTION_KEYS_CAPTURE_COMPLETE")
		quit()
		return
	if "--preview-only" in OS.get_cmdline_user_args():
		scene.call("PreviewBloodArms",2 if "--preview-follow" in OS.get_cmdline_user_args() else 1,0,1 if "--preview-single" in OS.get_cmdline_user_args() else 2)
		scene.call("SetPreviewBloodQi",12,4)
		root.gui_disable_input=false
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position=Vector2i(100,60)
		return
	if "--orbit" in OS.get_cmdline_user_args():
		scene.call("SetPreviewBloodQi",12,4)
		scene.call("PreviewBloodArms",1,0,2)
		await create_timer(.8,false).timeout
		var directory := MOTION+"orbit/"
		DirAccess.make_dir_recursive_absolute(directory)
		for index in range(150):
			await process_frame
			await RenderingServer.frame_post_draw
			var frame := root.get_texture().get_image()
			frame.save_png(directory+"motion_%03d.png"%index)
			if index==45: frame.save_png(OUTPUT+"orbit.png")
		print("BLOOD_ARMS_ORBIT_CAPTURE_COMPLETE")
		quit()
		return
	if "--quick" in OS.get_cmdline_user_args():
		scene.call("PreviewBloodArms",1,0,2)
		for level in [4,12,20,28]:
			scene.call("SetPreviewBloodQi",level,4)
			await create_timer(.8,false).timeout
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(OUTPUT+"qi_%d.png"%level)
		scene.call("SetPreviewCaster",true)
		scene.call("SetPreviewBloodQi",12,4)
		scene.call("PreviewBloodArms",1,1,2)
		await create_timer(.8,false).timeout
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(OUTPUT+"female_side.png")
		print("BLOOD_ARMS_MATERIAL_PREVIEW_COMPLETE")
		quit()
		return
	var clips := [{"name":"single","mode":0,"count":1,"shot":32,"frames":60},
		{"name":"double","mode":1,"count":2,"shot":75,"frames":150},
		{"name":"follow_single","mode":2,"count":1,"shot":52,"frames":105},
		{"name":"follow_double","mode":2,"count":2,"shot":55,"frames":105},
		{"name":"guard","mode":3,"count":2,"shot":45,"frames":90},
		{"name":"fade","mode":4,"count":2,"shot":43,"frames":75}]
	if "--actions" in OS.get_cmdline_user_args():
		clips=clips.filter(func(clip: Dictionary) -> bool: return clip.name in ["follow_single","follow_double","guard"])
	if "--single-follow" in OS.get_cmdline_user_args():
		clips=clips.filter(func(clip: Dictionary) -> bool: return clip.name=="follow_single")
	for clip in clips:
		var directory := MOTION+str(clip["name"])+"/"
		DirAccess.make_dir_recursive_absolute(directory)
		scene.call("SetPreviewBloodQi",12,4)
		scene.call("SetPreviewCaster",clip["name"]=="guard")
		scene.call("PreviewBloodArms",clip["mode"],1 if clip["name"]=="guard" else 0,clip["count"])
		for index in range(clip["frames"]):
			await process_frame
			await RenderingServer.frame_post_draw
			var frame := root.get_texture().get_image()
			frame.save_png(directory+"motion_%03d.png"%index)
			if index==clip["shot"]: frame.save_png(OUTPUT+str(clip["name"])+".png")
	if "--actions" in OS.get_cmdline_user_args() or "--single-follow" in OS.get_cmdline_user_args():
		print("BLOOD_ARMS_ACTIONS_CAPTURE_COMPLETE")
		quit()
		return
	scene.call("SetPreviewCaster",false)
	scene.call("SetPreviewBloodQi",4,4)
	scene.call("PreviewBloodArms",1,0,2)
	var growth_dir := MOTION+"growth/"
	DirAccess.make_dir_recursive_absolute(growth_dir)
	for index in range(150):
		scene.call("SetPreviewBloodQi",4+mini(24,maxi(0,index-24)/4),4)
		await process_frame
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(growth_dir+"motion_%03d.png"%index)
	print("BLOOD_ARMS_NATIVE_CAPTURE_COMPLETE: authored body with live depth-split ribbons; no skill/save writes.")
	if "--keep-open" in OS.get_cmdline_user_args():
		scene.call("SetPreviewCaster",false)
		scene.call("SetPreviewBloodQi",12,4)
		scene.call("PreviewBloodArms",1,0,2)
		root.gui_disable_input=false
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position=Vector2i(100,60)
		return
	scene.queue_free()
	await process_frame
	quit()
