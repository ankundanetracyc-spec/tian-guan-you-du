extends SceneTree
const OUTPUT := "res://docs/美术规范/持剑与御剑待机_20261023/"
func _initialize() -> void:
	call_deferred("_capture")
func _capture() -> void:
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,810)
	root.position=Vector2i(-4096,-4096)
	root.get_node("GameplayInputRouter").set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT+"frames")
	var preview: Node=load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
	root.add_child(preview)
	current_scene=preview
	await create_timer(.5,false).timeout
	if "--preview-only" in OS.get_cmdline_user_args():
		preview.call("PreviewSwordOrbitIdle",0,0)
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position=Vector2i(100,60)
		return
	preview.call("PreviewSwordIdle",0,0)
	await create_timer(.15,false).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"held_game.png")
	preview.call("PreviewSwordOrbitIdle",0,0)
	await create_timer(.6,false).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"orbit_game.png")
	preview.call("SetSwordIdlePaused",true)
	await create_timer(.1,false).timeout
	preview.call("SetSwordIdlePaused",false)
	preview.call("PreviewSwordOrbitIdle",1,1)
	await create_timer(.2,false).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"orbit_mirrored_broad.png")
	preview.queue_free()
	await process_frame
	var gallery := Control.new()
	root.add_child(gallery)
	current_scene=gallery
	var background := ColorRect.new()
	background.color=Color("ddd5bd")
	background.size=Vector2(1920,1080)
	gallery.add_child(background)
	_label(gallery,"持剑 / 御剑 · 待机",Vector2(80,40),36)
	_label(gallery,"人物底层 → 独立剑图 → 前景握剑手指",Vector2(80,92),20)
	_label(gallery,"手持剑",Vector2(420,180),28)
	_label(gallery,"御剑 · 双手空置",Vector2(1250,180),28)
	var icon := load("res://textures/ui/forged_swords_20261023_v1/slender_sword.png")
	var body_script := load("res://csharp/battle/BattlePaperDoll.cs")
	var data_script := load("res://csharp/battle/BattleEntityData.cs")
	var held: Node2D=body_script.new()
	var orbit: Node2D=body_script.new()
	for body: Node2D in [held,orbit]:
		var data: Resource=data_script.new()
		data.set("gender",0)
		data.set("faction",1)
		gallery.add_child(body)
		body.call("Configure",data)
		body.call("SetDisplayHeight",680.0)
	held.call("show_sword_idle",icon,Vector2(.874,.512),Vector2(.024,.505),780.0,34.6)
	orbit.call("show_sword_orbit_idle",icon,Vector2(.874,.512),Vector2(.024,.505),780.0)
	held.position=Vector2(520,970)
	orbit.position=Vector2(1270,970)
	await create_timer(.12,false).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"two_idles.png")
	for i in range(0 if "--grip-only" in OS.get_cmdline_user_args() else 108):
		await create_timer(1.0/15.0,false).timeout
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(OUTPUT+"frames/%03d.png"%i)
	background.color=Color("243531")
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"two_idles_dark.png")
	background.color=Color("ddd5bd")
	orbit.visible=false
	for child in gallery.get_children():
		if child is Label:
			child.visible=false
	held.call("SetDisplayHeight",1800.0)
	held.position=Vector2(500,1250)
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().get_region(Rect2i(80,150,650,500)).save_png(OUTPUT+"grip_detail.png")
	print("TWO_SWORD_IDLES_CAPTURE_COMPLETE")
	quit()
func _label(parent: Node,text: String,at: Vector2,size: int) -> void:
	var label := Label.new()
	label.text=text
	label.position=at
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",Color("30433e"))
	parent.add_child(label)
