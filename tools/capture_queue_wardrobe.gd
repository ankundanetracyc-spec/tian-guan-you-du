extends SceneTree
const OUT := "res://docs/美术规范/玉虚四套局内形象_20261103/"
var actors: Array[Node] = []
func _initialize() -> void:
	call_deferred("capture")
func capture() -> void:
	root.size=Vector2i(1440,810)
	root.position=Vector2i(-4096,-4096)
	root.gui_disable_input=true
	var battle := "--battle" in OS.get_cmdline_user_args()
	var wide := "--wide" in OS.get_cmdline_user_args()
	var scene: Node
	if battle:
		scene=load("res://tools/QueueAvatarBattlePreview.tscn").instantiate()
		root.add_child(scene)
		for i in range(45): await process_frame
	else:
		root.content_scale_size=Vector2i(1440,810)
		root.content_scale_mode=Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
		scene=Node2D.new()
		root.add_child(scene)
		var bg:=ColorRect.new()
		bg.size=Vector2(1440,810)
		bg.color=Color("919e96")
		scene.add_child(bg)
		var names:=["杂役","外门","内门","真传"]
		var paths:=["queue_yuxu_servant_v1","queue_yuxu_outer_v1","yuxu_fullbody_v15","queue_yuxu_true_v1"]
		if wide:
			names=["旧窄袖 · 向右","宽袖 · 向右","旧窄袖 · 向左","宽袖 · 向左"]
			paths=["queue_yuxu_outer_v1","queue_yuxu_outer_wide_v2","queue_yuxu_outer_v1","queue_yuxu_outer_wide_v2"]
		for i in range(4):
			var actor=load("res://tools/queue_small_avatar/queue_small_avatar.gd").new()
			scene.add_child(actor)
			assert(actor.configure(420.0,"res://tools/action_battle_test/art/"+paths[i],"male_clothes_04" if wide else "male_clothes_%02d"%(i+3)))
			actor.position=Vector2(180+i*350,690)
			actor.z_index=20+i*20
			actor.set_facing(i>=2 if wide else i%2==1)
			actors.append(actor)
			var label:=Label.new()
			label.position=Vector2(65+i*350 if wide else 125+i*350,70)
			label.text=names[i]
			label.add_theme_font_override("font",load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"))
			label.add_theme_font_size_override("font_size",30)
			label.add_theme_color_override("font_color",Color("162923"))
			scene.add_child(label)
	current_scene=scene
	var kind:="battle" if battle else "close"
	if wide: kind="wide_"+kind
	var output_dir:="res://docs/美术规范/玉虚外门宽袖样板_20261103/" if wide else OUT
	var dir:="res://tmp/queue_wardrobe_"+kind+"/"
	DirAccess.make_dir_recursive_absolute(dir)
	var end_frame:=-1
	for frame in range(240 if battle else 160):
		var t:=frame/20.0
		if battle:
			if frame==20: scene.call("demonstrate_action","combat")
		else:
			var action:="idle"
			var elapsed:=0.0
			var duration:=1.0
			if t>=1 and t<1.58:
				action="attack"; elapsed=t-1; duration=.58
			elif t>=2.2 and t<3.32:
				action="hurt"; elapsed=t-2.2; duration=1.12
			elif t>=3.8 and t<5.15:
				action="guard"; elapsed=t-3.8; duration=1.35
			elif t>=5.6 and t<6.7:
				action="dodge"; elapsed=t-5.6; duration=1.1
			for actor in actors: actor.update_visual(.05,action,elapsed,duration)
		await process_frame
		await RenderingServer.frame_post_draw
		var shot:=root.get_texture().get_image()
		shot.save_png(dir+"frame_%03d.png"%frame)
		if frame in [0,26,50,82,118,150]: shot.save_png(output_dir+kind+"_%03d.png"%frame)
		if battle and frame>20:
			if end_frame<0 and not scene.call("is_demo_running"): end_frame=frame
			if end_frame>=0 and frame>=end_frame+20: break
	print("QUEUE_WARDROBE_CAPTURE_OK ",kind)
	quit()
