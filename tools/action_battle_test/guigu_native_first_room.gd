extends Node2D

const NativeRuntime = preload("guigu_native_first_runtime.gd")
var actors: Array[Node2D] = []
var cameras: Array[Camera2D] = []
var clock := 0.0
var clip_clock := 0.0
var selected_clip := "Run_New1"
var facing := 1.0
var paused := false
var capture_mode := false
var status: Label
var font := SystemFont.new()

func _label(value: String, point: Vector2, size: int) -> Label:
	var item := Label.new()
	item.text = value
	item.position = point
	item.add_theme_font_override("font",font)
	item.add_theme_font_size_override("font_size",size)
	item.add_theme_color_override("font_color",Color("36433e"))
	add_child(item)
	return item

func _actor_panel(original: bool, x: float) -> void:
	var display := SubViewportContainer.new()
	display.position = Vector2(x,112)
	display.size = Vector2(550,570)
	display.stretch = true
	add_child(display)
	var viewport := SubViewport.new()
	viewport.size = Vector2i(550,570)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	display.add_child(viewport)
	var background := CanvasLayer.new()
	background.layer = -10
	viewport.add_child(background)
	var paper := ColorRect.new()
	paper.size = Vector2(550,570)
	paper.color = Color("c9cec3")
	background.add_child(paper)
	var actor := NativeRuntime.new()
	viewport.add_child(actor)
	assert(actor.build(),str(actor.load_errors))
	actor.set_skin_mode("reference" if original else "experiment")
	actors.append(actor)
	var ground := Line2D.new()
	ground.points = PackedVector2Array([Vector2(-100000,0),Vector2(100000,0)])
	ground.width = 0.25
	ground.default_color = Color("8f9a8b")
	ground.z_index = -1
	viewport.add_child(ground)
	var camera := Camera2D.new()
	viewport.add_child(camera)
	camera.zoom = Vector2.ONE*4.0
	camera.position = Vector2(0,-actor.wanderer_skin.native_height*0.48)
	cameras.append(camera)

func _ready() -> void:
	get_window().size = Vector2i(1140,790)
	get_window().content_scale_size = Vector2i(1140,790)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().title = "原规格人物 · 鬼谷101与白青常服"
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var paper := ColorRect.new()
	paper.size = Vector2(1140,790)
	paper.color = Color("e2dfd2")
	add_child(paper)
	_label("原规格人物 · 第一套白青常服",Vector2(28,18),27)
	_label("鬼谷男性101",Vector2(195,76),22)
	_label("我们的白青常服",Vector2(750,76),22)
	_actor_panel(true,15)
	_actor_panel(false,575)
	_label("1 静站　2 跑动　3 出手　4 错步　F 换向　空格 暂停",Vector2(28,710),19)
	status = _label("",Vector2(28,749),16)
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			capture_mode = true
			set_process(false)
			get_window().position = Vector2i(-7000,-7000)
			get_window().unfocusable = true
			_capture(argument.trim_prefix("--capture-dir="))
			return

func _select(clip: String) -> void:
	selected_clip = clip
	clip_clock = 0.0

func _step(delta: float) -> void:
	if paused: return
	clock += delta
	clip_clock += delta
	var gait_speed: float = actors[0].wanderer_skin.ROBED_MOVE_SPEED
	var velocity := Vector2(gait_speed*facing,0) if selected_clip=="Run_New1" else Vector2.ZERO
	for index in range(actors.size()):
		var actor := actors[index]
		actor.position += velocity*delta
		actor.wanderer_skin.set_motion_context(delta,velocity,clock,facing)
		actor.set_character_pose(selected_clip,clip_clock,facing)
		cameras[index].position = actor.position+Vector2(0,-actor.wanderer_skin.native_height*0.48)
	status.text = "身体沿原尺寸、骨架、动作与衣摆物理 · 发型采用自有朴素束发髻"

func _process(delta: float) -> void:
	_step(minf(delta,0.05))

func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo: return
	match event.physical_keycode:
		KEY_1: _select("Idle")
		KEY_2: _select("Run_New1")
		KEY_3: _select("AttackIdle")
		KEY_4: _select("Step1")
		KEY_F: facing *= -1.0
		KEY_SPACE: paused = not paused

func _capture(destination: String) -> void:
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	FileAccess.open(output.path_join(".gdignore"),FileAccess.WRITE).store_string("")
	var records := []
	for frame in range(300):
		if frame==0: _select("Idle")
		if frame==30: _select("Run_New1")
		if frame==150: _select("Idle")
		if frame==210:
			facing = -1.0
			_select("Run_New1")
		if frame==270: _select("Idle")
		_step(1.0/30.0)
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		assert(get_viewport().get_texture().get_image().save_png(output.path_join("frame_%03d.png"%frame))==OK)
		if frame%30==0: records.append({"frame":frame,"left":actors[0].get_runtime_summary(),"right":actors[1].get_runtime_summary()})
	FileAccess.open(output.path_join("capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"native":true,"frames":300,"fps":30,"same_body_geometry_animation_and_cloth_physics":true,"own_hair_override":true,"view_zoom":4.0,"records":records},"\t"))
	print("NATIVE ORIGINAL-SIZE OUTFIT CAPTURE COMPLETE: ",output)
	get_tree().quit()
