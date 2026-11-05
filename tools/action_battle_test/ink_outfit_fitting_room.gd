extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")
var fitting_rig: Node2D
var current_clip := "Idle"
var pose_clock := 0.0
var simulation_clock := 0.0
var facing := 1.0
var paused := false
var zoom := 6.0
var font := SystemFont.new()
var state_label: Label
var availability_label: Label
var pause_button: Button
var action_buttons: Dictionary = {}
var hand_buttons: Dictionary = {}
var hand_preview := "auto"

func _label(parent: Node, text: String, position: Vector2, size: int, color: Color = Color("3e4846")) -> Label:
	var result := Label.new()
	result.text = text
	result.position = position
	result.add_theme_font_override("font",font)
	result.add_theme_font_size_override("font_size",size)
	result.add_theme_color_override("font_color",color)
	parent.add_child(result)
	return result

func _button(parent: Node, text: String, y: float, callback: Callable) -> Button:
	var result := Button.new()
	result.text = text
	result.position = Vector2(30,y)
	result.size = Vector2(190,42)
	result.add_theme_font_override("font",font)
	result.add_theme_font_size_override("font_size",18)
	result.pressed.connect(callback)
	parent.add_child(result)
	return result

func _ready() -> void:
	get_window().size = Vector2i(1000,700)
	get_window().content_scale_size = Vector2i(1000,700)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var background := ColorRect.new()
	background.size = Vector2(1000,700)
	background.color = Color("cbcfc9")
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)
	var panel := ColorRect.new()
	panel.color = Color("e8e5da")
	panel.size = Vector2(250,700)
	add_child(panel)
	_label(self,"第一套衣服",Vector2(28,28),28)
	_label(self,"独立试穿",Vector2(30,69),17)
	_label(self,"衣身 · 双袖 · 裤靴",Vector2(30,108),15)
	var y := 158.0
	for pair in [["Idle","1  静站"],["Run_New1","2  跑动"],["BackstepWalk","3  后退"],["AttackIdle","4  出手"],["Step1","5  错步"]]:
		var clip := str(pair[0])
		action_buttons[clip] = _button(self,str(pair[1]),y,func(): _select_pose(clip))
		y += 51
	_button(self,"F  换朝向",y+8,func(): facing *= -1.0)
	pause_button = _button(self,"空格  暂停",y+59,_toggle_pause)
	_button(self,"R  重新载入",y+110,_reload_outfit)
	_label(self,"查看比例",Vector2(30,598),14)
	var slider := HSlider.new()
	slider.position = Vector2(30,631)
	slider.size = Vector2(190,24)
	slider.min_value = 2.0
	slider.max_value = 6.0
	slider.step = 0.1
	slider.value = zoom
	slider.value_changed.connect(func(value: float): zoom = value)
	add_child(slider)
	_label(self,"水墨服装试穿",Vector2(286,30),25)
	_label(self,"固定束发 · 侧身无五官",Vector2(287,70),15)
	var hand_x := 286.0
	for pair in [["auto","6  普通手"],["sword_near","7  右手剑指"],["grip_near","8  右手握柄"]]:
		var style := str(pair[0])
		var button := _button(self,str(pair[1]),110,func(): _select_hand(style))
		button.position.x = hand_x
		hand_buttons[style] = button
		hand_x += 210.0
	state_label = _label(self,"静站",Vector2(286,632),18)
	availability_label = _label(self,"",Vector2(286,662),14)
	var floor_line := Line2D.new()
	floor_line.points = PackedVector2Array([Vector2(350,600),Vector2(920,600)])
	floor_line.width = 1.0
	floor_line.default_color = Color(0.36,0.41,0.39,0.3)
	add_child(floor_line)
	_reload_outfit()
	if OS.get_cmdline_user_args().has("--preview-run"): _select_pose("Run_New1")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			set_process(false)
			get_window().unfocusable = true
			get_window().position = Vector2i(-7000,-7000)
			await _capture_fitting(argument.trim_prefix("--capture-dir="))
			return

func _reload_outfit() -> void:
	if fitting_rig!=null:
		remove_child(fitting_rig)
		fitting_rig.queue_free()
	fitting_rig = OutfitRuntime.new()
	add_child(fitting_rig)
	fitting_rig.position = Vector2(650,600)
	var built: bool = fitting_rig.build()
	availability_label.text = "素材已装配，可切换动作观察" if built else "服装素材尚未齐备，完成后按 R 载入"
	if not built: print("OUTFIT FITTING NOT READY: ",JSON.stringify(fitting_rig.get_runtime_summary()))
	else: print("OUTFIT FITTING READY: ",JSON.stringify(fitting_rig.get_runtime_summary()))
	_select_pose("Idle")
	_select_hand(hand_preview)

func _select_hand(style: String) -> void:
	hand_preview = style
	if fitting_rig != null and fitting_rig.loaded:
		fitting_rig.wanderer_skin.set_hand_pose_override("" if style == "auto" else style)
	for key in hand_buttons: hand_buttons[key].disabled = key == style

func _select_pose(clip: String) -> void:
	current_clip = clip
	pose_clock = 0.0
	for key in action_buttons: action_buttons[key].disabled = key==clip
	state_label.text = {"Idle":"静站","Run_New1":"跑动","BackstepWalk":"后退","AttackIdle":"出手","Step1":"错步"}.get(clip,clip)

func _toggle_pause() -> void:
	paused = not paused
	pause_button.text = "空格  继续" if paused else "空格  暂停"

func _process(delta: float) -> void:
	_pose_step(0.0 if paused else minf(delta,0.05))

func _pose_step(delta: float) -> void:
	if fitting_rig==null or not fitting_rig.loaded: return
	simulation_clock += delta
	pose_clock += delta
	fitting_rig.display_zoom = zoom
	var velocity := Vector2.ZERO
	if current_clip=="Run_New1": velocity = Vector2(114*facing,0)
	elif current_clip=="BackstepWalk": velocity = Vector2(-114*facing,0)
	fitting_rig.wanderer_skin.set_motion_context(delta,velocity,simulation_clock,facing)
	fitting_rig.set_character_pose(current_clip,pose_clock,facing)

func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo: return
	match event.physical_keycode:
		KEY_1: _select_pose("Idle")
		KEY_2: _select_pose("Run_New1")
		KEY_3: _select_pose("BackstepWalk")
		KEY_4: _select_pose("AttackIdle")
		KEY_5: _select_pose("Step1")
		KEY_6: _select_hand("auto")
		KEY_7: _select_hand("sword_near")
		KEY_8: _select_hand("grip_near")
		KEY_F: facing *= -1.0
		KEY_SPACE: _toggle_pause()
		KEY_R: _reload_outfit()

func _snapshot(path: String) -> void:
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	assert(get_viewport().get_texture().get_image().save_png(path)==OK)

func _capture_fitting(destination: String) -> void:
	if fitting_rig==null or not fitting_rig.loaded:
		push_error("Outfit capture requires all four new full-canvas materials")
		get_tree().quit(1)
		return
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	var journal := []
	var snapshots := []
	for style in ["auto","sword_near","grip_near"]:
		_select_hand(style)
		for clip in ["Idle","Run_New1","AttackIdle"]:
			_select_pose(clip)
			for frame in range(42): _pose_step(1.0/60.0)
			var filename: String = "hand_"+style+"_"+clip+".png"
			await _snapshot(output.path_join(filename))
			snapshots.append({"file":filename,"clip":clip,"hand_preview":style,"facing":facing})
	_select_hand("auto")
	for clip in ["Idle","Run_New1","BackstepWalk","AttackIdle","Step1"]:
		simulation_clock = 0.0
		_select_pose(clip)
		for frame in range(60):
			_pose_step(1.0/60.0)
			if frame in [29,41,59]:
				var filename: String = clip+"_%03d.png"%frame
				await _snapshot(output.path_join(filename))
				snapshots.append({"file":filename,"clip":clip,"clock":simulation_clock,"source_clip":fitting_rig.wanderer_skin.sampled_source_clip,"source_phase":fitting_rig.wanderer_skin.sampled_source_phase,"facing":facing})
	facing = -1.0
	for clip in ["Idle","Run_New1","BackstepWalk"]:
		simulation_clock = 0.0
		_select_pose(clip)
		for frame in range(42): _pose_step(1.0/60.0)
		var filename: String = clip+"_left_041.png"
		await _snapshot(output.path_join(filename))
		snapshots.append({"file":filename,"clip":clip,"clock":simulation_clock,"source_clip":fitting_rig.wanderer_skin.sampled_source_clip,"source_phase":fitting_rig.wanderer_skin.sampled_source_phase,"facing":facing})
	facing = 1.0
	simulation_clock = 0.0
	for frame in range(150):
		if frame==0: _select_pose("Run_New1")
		if frame==90: _select_pose("Idle")
		_pose_step(1.0/30.0)
		if frame%2==0: await _snapshot(output.path_join("run_stop_%03d.png"%frame))
		journal.append({"frame":frame,"seconds":float(frame+1)/30.0,"clip":current_clip,"phase":fitting_rig.wanderer_skin.sampled_source_phase,"facing":facing})
	var file := FileAccess.open(output.path_join("capture_record.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"source":fitting_rig.get_runtime_summary(),"snapshots":snapshots,"run_stop_frames":journal,"artwork_scope":"four original full-canvas costume textures; neutral head is a fitting aid"},"\t"))
	file.close()
	print("OUTFIT FITTING CAPTURE COMPLETE: ",output)
	get_tree().quit()
