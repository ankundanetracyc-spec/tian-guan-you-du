extends SceneTree

const OUTPUT := "res://docs/美术规范/行动队列局内形象_20261103/"
const WRAPPER := "res://tools/queue_small_avatar/queue_small_avatar.gd"
var close_review := false
var revision2 := false
var revision3 := false
var revision4 := false
var revision5 := false
var still_only := false
var gait_check := false
var actors: Array[Node] = []
var label: Label

func _initialize() -> void:
	close_review = "--close" in OS.get_cmdline_user_args()
	revision5 = "--v5" in OS.get_cmdline_user_args()
	revision4 = "--v4" in OS.get_cmdline_user_args() or revision5
	revision3 = "--v3" in OS.get_cmdline_user_args() or revision4
	revision2 = "--v2" in OS.get_cmdline_user_args() or revision3
	still_only = "--still" in OS.get_cmdline_user_args()
	gait_check = "--gait-check" in OS.get_cmdline_user_args()
	call_deferred("_capture")

func _capture() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1440, 810)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	var router := root.get_node_or_null("GameplayInputRouter")
	if router:
		router.set_process_input(false)
	var scene: Node
	if close_review:
		root.content_scale_size = Vector2i(1440,810)
		root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
		scene = Node2D.new()
		root.add_child(scene)
		var background := ColorRect.new()
		background.size = Vector2(1440,810)
		background.color = Color("aeb5a8")
		scene.add_child(background)
		for side in range(2):
			var actor: Node2D = load(WRAPPER).new()
			scene.add_child(actor)
			actor.call("configure", 420.0)
			actor.call("set_facing", side == 1)
			actor.position = Vector2(320+side*800 if revision2 else 480+side*480,680)
			if revision2:
				actor.z_index = 20+side*20
			actors.append(actor)
		label = Label.new()
		label.position = Vector2(42,40)
		label.add_theme_font_override("font", load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"))
		label.add_theme_font_size_override("font_size",32)
		label.add_theme_color_override("font_color",Color("283532"))
		scene.add_child(label)
	else:
		scene = load("res://tools/QueueAvatarBattlePreview.tscn").instantiate()
		root.add_child(scene)
		for i in range(45):
			await process_frame
		actors = scene.find_children("BattlePaperDoll", "", true, false)
		if actors.size() != 6:
			push_error("Expected actual distant3v3 actors")
			quit(1)
			return
		for actor in actors:
			print("QUEUE_CAPTURE_PARENT_Z: ",actor.get_path()," actorZ=",(actor as Node2D).z_index," parentZ=",(actor.get_parent() as Node2D).z_index)
	current_scene = scene
	var variant := "close" if close_review else "battle"
	var prefix := "queue_small_v5_" if revision5 else ("queue_small_v4_" if revision4 else ("queue_small_v3_" if revision3 else ("queue_small_v2_" if revision2 else "queue_small_")))
	var frames := "res://tmp/"+prefix+variant+"_capture/"
	DirAccess.make_dir_recursive_absolute(frames)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	var demo_finished_frame := -1
	for frame in range(1 if still_only else (70 if gait_check else (1000 if revision2 and not close_review else (180 if revision5 else (160 if revision2 else 200))))):
		var time := frame/20.0
		var state := _state(time)
		if close_review:
			label.text = "动作展示 · 原小人骨骼（非局内镜头）\n"+state.caption
			for side in range(actors.size()):
				var actor := actors[side]
				if revision2:
					var travel := _travel(time)
					var sign_facing := 1.0 if side == 0 else -1.0
					actor.position.x = 320+side*800+travel.offset*sign_facing
					actor.call("set_motion",Vector2(travel.velocity*sign_facing,0))
					if revision5:
						actor.call("set_travel_mode",travel.mode)
						actor.call("set_travel_progress",travel.progress)
					elif revision3:
						actor.call("set_travel_mode","approach" if time>=1.0 and time<1.4 else ("retreat" if time>=2.155 and time<2.555 else "idle"))
				actor.call("update_visual",.05,state.action,state.elapsed,state.duration)
		else:
			if frame == 20:
				scene.call("demonstrate_action","combat" if revision2 else "all")
		await process_frame
		if revision2:
			var wrappers: Array[Node] = actors if close_review else scene.find_children("QueueSmallAvatar","",true,false)
			for wrapper in wrappers:
				var layers: Array = wrapper.call("get_layer_state")
				var max_leg := -4096
				var min_robe := 4096
				for layer in layers:
					if not layer.relative:
						push_error("Absolute layer remains: "+str(layer.name))
						quit(1)
						return
					if "_leg" in layer.name:
						max_leg = maxi(max_leg,layer.effective_z)
					if "Skirt" in layer.name or "robe_" in layer.name:
						min_robe = mini(min_robe,layer.effective_z)
				if max_leg >= min_robe:
					push_error("Leg renders above robe at frame "+str(frame))
					quit(1)
					return
		await RenderingServer.frame_post_draw
		var image := root.get_texture().get_image()
		image.save_png(frames+"motion_%03d.png"%frame)
		if frame in [0,23,25,26,32,34,48,50,54,59,60,64,67,78,82,86,90,94,101,104,108,111,116,118,125,132,150,180]:
			image.save_png(OUTPUT+prefix+variant+"_%03d.png"%frame)
		if revision2 and not close_review and frame > 20:
			if demo_finished_frame < 0 and not scene.call("is_demo_running"):
				demo_finished_frame = frame
			if demo_finished_frame >= 0 and frame >= demo_finished_frame+20:
				break
	print("QUEUE_SMALL_CAPTURE_OK variant=",variant," revision2=",revision2," fixedfps=20; rendered original wrapper.")
	quit()

func _state(time: float) -> Dictionary:
	if revision5:
		if time>=1.0 and time<1.8:
			return {"action":"idle","elapsed":0.0,"duration":1.0,"caption":"前飞 · 蹬地 / 腾空 / 落地"}
		if time>=1.8 and time<2.555:
			var elapsed := time-1.8
			if elapsed>.30:
				elapsed=maxf(.30,elapsed-.175)
			return {"action":"attack","elapsed":elapsed,"duration":.58,"caption":"攻击 · 起手 / 接触 / 收势"}
		if time>=2.555 and time<3.355:
			return {"action":"idle","elapsed":0.0,"duration":1.0,"caption":"倒飞 · 朝敌撤回 / 落地缓冲"}
		if time>=4.0 and time<5.12:
			return {"action":"hurt","elapsed":time-4.0,"duration":1.12,"caption":"受击 · 后倾 / 回稳"}
		if time>=6.0 and time<7.1:
			return {"action":"dodge","elapsed":time-6.0,"duration":1.1,"caption":"闪避 · 收身 / 回位"}
		return {"action":"idle","elapsed":0.0,"duration":1.0,"caption":"待机 · 呼吸与衣摆"}
	if revision2:
		if time >= 1.0 and time < 1.4:
			return {"action":"idle","elapsed":0.0,"duration":1.0,"caption":"上前 · 轻功短掠" if revision3 else "上前 · 袍内小步"}
		if time >= 1.4 and time < 2.155:
			var elapsed := time-1.4
			if elapsed > .30:
				elapsed = maxf(.30,elapsed-.175)
			return {"action":"attack","elapsed":elapsed,"duration":.58,"caption":"攻击 · 起手 / 接触停顿 / 收势"}
		if time >= 2.155 and time < 2.555:
			return {"action":"idle","elapsed":0.0,"duration":1.0,"caption":"后撤 · 面朝敌人掠回" if revision3 else "后退 · 面朝敌人倒步回位"}
		if time >= 3.0 and time < 4.12:
			return {"action":"hurt","elapsed":time-3.0,"duration":1.12,"caption":"受击 · 后倾 / 回稳"}
		if time >= 5.0 and time < 6.1:
			return {"action":"dodge","elapsed":time-5.0,"duration":1.1,"caption":"闪避 · 收身 / 回位"}
		return {"action":"idle","elapsed":0.0,"duration":0.0,"caption":"待机 · 呼吸与衣摆"}
	if time >= 1.0 and time < 1.58:
		return {"action":"attack","elapsed":time-1.0,"duration":.58,"caption":"攻击 · 起手 / 出掌 / 收势"}
	if time >= 2.5 and time < 3.62:
		return {"action":"hurt","elapsed":time-2.5,"duration":1.12,"caption":"受击 · 后倾 / 回稳"}
	if not revision2 and time >= 4.0 and time < 5.35:
		return {"action":"guard","elapsed":time-4.0,"duration":1.35,"caption":"架招 · 提手 / 护身 / 放松"}
	var dodge_start := 4.5 if revision2 else 6.0
	if time >= dodge_start and time < dodge_start+1.1:
		return {"action":"dodge","elapsed":time-dodge_start,"duration":1.1,"caption":"闪避 · 收身 / 回位"}
	return {"action":"idle","elapsed":0.0,"duration":0.0,"caption":"待机 · 呼吸与衣摆"}

func _travel(time: float) -> Dictionary:
	if revision5:
		var mode := "approach" if time>=1.0 and time<1.8 else ("retreat" if time>=2.555 and time<3.355 else "idle")
		var progress := clampf((time-(1.0 if mode=="approach" else 2.555))/.8,0.0,1.0)
		var horizontal := smoothstep(0.0,1.0,clampf((progress-.15)/.67,0.0,1.0))
		var offset := 140.0*horizontal if mode=="approach" else (140.0*(1.0-horizontal) if mode=="retreat" else (140.0 if time>=1.8 and time<2.555 else 0.0))
		return {"offset":offset,"velocity":175.0 if mode=="approach" else (-175.0 if mode=="retreat" else 0.0),"mode":mode,"progress":progress}
	if revision3:
		var phase := (time-1.0)/.4 if time<1.4 else (time-2.155)/.4
		if (time>=1.0 and time<1.4) or (time>=2.155 and time<2.555):
			var eased := 4.0*pow(phase,3) if phase<.5 else 1.0-pow(-2.0*phase+2.0,3)/2.0
			var velocity := 3000.0*pow(phase if phase<.5 else 1.0-phase,2)
			return {"offset":100.0*(eased if time<1.4 else 1.0-eased),"velocity":velocity if time<1.4 else -velocity}
		return {"offset":100.0 if time>=1.4 and time<2.155 else 0.0,"velocity":0.0}
	var speed := 38.0*420.0/((1474.0-75.0)*16.0/186.0)
	if time >= 1.0 and time < 1.4:
		return {"offset":speed*(time-1.0),"velocity":speed}
	if time >= 1.4 and time < 2.155:
		return {"offset":speed*.4,"velocity":0.0}
	if time >= 2.155 and time < 2.555:
		return {"offset":speed*(2.555-time),"velocity":-speed}
	return {"offset":0.0,"velocity":0.0}
