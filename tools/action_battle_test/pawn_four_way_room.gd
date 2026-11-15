extends Node2D

## Standalone appearance draft room. Never calls a production save or setter.
const SIZE := Vector2i(1280,800)
const WALK_BOUNDS := Rect2(120,422,592,126)
const DIRECTIONS := [Vector2.DOWN,Vector2.UP,Vector2.LEFT,Vector2.RIGHT]
const DIRECTION_NAMES := ["正面","背面","左侧","右侧"]
const PROFILE_FIELDS := {"clothes":"appearance_clothes_style","hair_front":"appearance_hair_style","hair_back":"appearance_hair_back_style"}
const INK := Color("394940")
var adapter: RefCounted
var avatar_script: Script
var actor: Node2D
var comparisons: Array[Node2D] = []
var ui: Control
var portrait_root: Node2D
var portrait_nodes: Array[Node2D] = []
var portrait_textures := {}
var portrait_missing: Array[String] = []
var portrait_layers: Array = []
var draft: Dictionary = {}
var initial_profile: Dictionary = {}
var initial_source: Dictionary = {}
var options := {}
var option_buttons := {}
var direction_buttons: Array[Button] = []
var source_label: Label
var status_label: Label
var resource_label: Label
var renderer_message: Label
var read_button: Button
var reset_button: Button
var sample_button: Button
var pause_button: Button
var front_check: CheckButton
var back_check: CheckButton
var show_front := true
var show_back := true
var mouse_facing := false
var direction := Vector2.DOWN
var paused := false
var capture_active := false
var renderer_ready := false
var load_errors: Array[String] = []
var clock := 0.0
var last_velocity := Vector2.ZERO
var font := SystemFont.new()

func _folder() -> String:
	return get_script().resource_path.get_base_dir()

func _ready() -> void:
	font.font_names = PackedStringArray(["Microsoft YaHei","Noto Sans CJK SC"])
	get_window().title = "圆袖人物 · 四方向独立预览"
	get_window().mode = Window.MODE_WINDOWED
	get_window().size = SIZE
	get_window().content_scale_size = SIZE
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	_build_ui()
	_load_adapter()
	_reload_avatars()
	read_saved_appearance()
	_apply_preview_arguments()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			get_window().position = Vector2i(-7000,-7000)
			get_window().unfocusable = true
			capture_active = true
			call_deferred("_capture_room",argument.trim_prefix("--capture-dir="))
	queue_redraw()

func _apply_preview_arguments() -> void:
	var named_directions := {"front":Vector2.DOWN,"back":Vector2.UP,"left":Vector2.LEFT,"right":Vector2.RIGHT}
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--direction="):
			var value := argument.trim_prefix("--direction=")
			if named_directions.has(value): set_preview_direction(named_directions[value])
		elif argument.begins_with("--front-style=") or argument.begins_with("--back-style="):
			var front := argument.begins_with("--front-style=")
			var value := argument.trim_prefix("--front-style=" if front else "--back-style=")
			if value in ["0","1","2"] and adapter!=null and not draft.is_empty():
				set_preview_profile(adapter.with_field(draft,"appearance_hair_style" if front else "appearance_hair_back_style",int(value)))

func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO,Vector2(SIZE)),Color("e8e6dc"))
	for panel in [Rect2(32,105,770,480),Rect2(830,105,418,665),Rect2(32,610,770,160)]:
		draw_rect(panel,Color("f2f0e7"))
		draw_rect(panel,Color("aeb5a7"),false,1.0)
		draw_line(panel.position+Vector2(1,1),panel.position+Vector2(panel.size.x-1,1),Color("697b6b"),2.0,true)
	# Quiet horizontal brush lines establish the walking plane without a grid.
	for index in range(5):
		var y := 370.0+float(index)*43.0
		draw_line(Vector2(73,y),Vector2(762,y),Color(0.45,0.52,0.44,0.035),1.0,true)
	for index in range(4):
		var rect := Rect2(38+index*191,616,183,148)
		if direction==DIRECTIONS[index]: draw_rect(rect,Color(0.36,0.47,0.38,0.06))
		draw_rect(rect,Color("c1c6b7"),false,1.0)

func _label(text_value: String, at: Vector2, size_value: Vector2, font_size: int = 16) -> Label:
	var label := Label.new()
	label.text = text_value
	label.position = at
	label.size = size_value
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size",font_size)
	label.add_theme_color_override("font_color",INK)
	ui.add_child(label)
	return label

func _style(fill: Color, border: Color) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = fill
	box.border_color = border
	box.set_border_width_all(1)
	box.set_corner_radius_all(3)
	box.content_margin_left = 10
	box.content_margin_right = 10
	return box

func _button(text_value: String, at: Vector2, size_value: Vector2, callback: Callable) -> Button:
	var button := Button.new()
	button.text = text_value
	button.position = at
	button.size = size_value
	button.focus_mode = Control.FOCUS_NONE
	button.add_theme_stylebox_override("normal",_style(Color("e8ebdf"),Color("9eaa97")))
	button.add_theme_stylebox_override("hover",_style(Color("dae2d1"),Color("6d856b")))
	button.add_theme_stylebox_override("pressed",_style(Color("c6d3bd"),Color("4c6a52")))
	button.pressed.connect(callback)
	ui.add_child(button)
	return button

func _check(text_value: String, at: Vector2, checked: bool, callback: Callable) -> CheckButton:
	var button := CheckButton.new()
	button.text = text_value
	button.position = at
	button.size = Vector2(190,32)
	button.button_pressed = checked
	button.focus_mode = Control.FOCUS_NONE
	button.toggled.connect(callback)
	ui.add_child(button)
	return button

func _build_ui() -> void:
	ui = Control.new()
	ui.name = "DraftRoomControls"
	ui.z_index = 20
	ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var theme := Theme.new()
	theme.default_font = font
	theme.default_font_size = 16
	theme.set_color("font_color","Label",INK)
	theme.set_color("font_color","Button",INK)
	theme.set_color("font_color","OptionButton",INK)
	theme.set_color("font_color","CheckButton",INK)
	for control in ["Button","OptionButton","CheckButton"]:
		for state in ["font_hover_color","font_pressed_color","font_hover_pressed_color","font_focus_color"]: theme.set_color(state,control,INK)
	theme.set_color("font_disabled_color","OptionButton",Color("697667"))
	for state in ["normal","disabled"]: theme.set_stylebox(state,"OptionButton",_style(Color("e3e7d9"),Color("a8b29e")))
	for state in ["hover","pressed"]: theme.set_stylebox(state,"OptionButton",_style(Color("d5dfca"),Color("71876c")))
	theme.set_stylebox("panel","PopupMenu",_style(Color("eeeee2"),Color("7d917a")))
	theme.set_color("font_color","PopupMenu",INK)
	ui.theme = theme
	add_child(ui)
	_label("圆袖人物 · 四方向",Vector2(34,22),Vector2(750,42),30)
	_label("WASD行走　1—4转向　空格暂停　Home归位　F5重载素材",Vector2(36,69),Vector2(768,24),15)
	_label("独立样例 · 本房间试穿不会保存",Vector2(850,34),Vector2(376,30),16)
	_label("活动预览",Vector2(51,120),Vector2(260,28),19)
	_label("当前外观",Vector2(850,119),Vector2(360,30),19)
	var names := {"clothes":"服装","hair_front":"前发","hair_back":"后发"}
	for index in range(3):
		var part: String = ["clothes","hair_front","hair_back"][index]
		_label(names[part],Vector2(851,165+index*47),Vector2(56,30),16)
		var picker := OptionButton.new()
		picker.position = Vector2(912,158+index*47)
		picker.size = Vector2(313,35)
		picker.add_item("等待外观目录")
		picker.disabled = true
		picker.item_selected.connect(_on_option_selected.bind(part))
		ui.add_child(picker)
		option_buttons[part] = picker
	front_check = _check("显示前发",Vector2(847,299),true,func(value: bool): set_debug_layers(value,show_back))
	back_check = _check("显示后发",Vector2(1036,299),true,func(value: bool): set_debug_layers(show_front,value))
	source_label = _label("正在读取当前外观",Vector2(851,337),Vector2(374,31),13)
	source_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	read_button = _button("读取当前",Vector2(850,375),Vector2(118,32),read_saved_appearance)
	sample_button = _button("本套样例",Vector2(979,375),Vector2(117,32),use_demo_sample)
	reset_button = _button("恢复初始",Vector2(1107,375),Vector2(117,32),restore_initial_appearance)
	reset_button.disabled = true
	_label("真实立绘",Vector2(851,418),Vector2(370,24),15)
	_label("立绘与局内使用同一份预览外观",Vector2(851,744),Vector2(380,22),13)
	portrait_root = Node2D.new()
	portrait_root.name = "OriginalPortraitCanvas"
	portrait_root.position = Vector2(927,430)
	portrait_root.scale = Vector2.ONE*0.198
	portrait_root.z_index = 3
	add_child(portrait_root)
	status_label = _label("朝向：正面",Vector2(52,554),Vector2(460,23),15)
	resource_label = _label("",Vector2(52,584),Vector2(720,22),13)
	renderer_message = _label("等待人物样例素材",Vector2(200,312),Vector2(450,48),18)
	renderer_message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_check("朝向鼠标",Vector2(432,118),false,func(value: bool): mouse_facing=value)
	pause_button = _button("暂停",Vector2(664,120),Vector2(116,30),toggle_pause)
	for index in range(4):
		var button := _button("",Vector2(38+index*191,616),Vector2(183,148),set_preview_direction.bind(DIRECTIONS[index]))
		button.add_theme_stylebox_override("normal",_style(Color(0,0,0,0),Color(0,0,0,0)))
		button.add_theme_stylebox_override("hover",_style(Color(0.4,0.52,0.42,0.04),Color("8d9f88")))
		direction_buttons.append(button)
		var label := _label(str(index+1)+"  "+DIRECTION_NAMES[index],Vector2(43+index*191,622),Vector2(172,26),15)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

func _load_adapter() -> void:
	var path := _folder().path_join("pawn_appearance_profile.gd")
	if not ResourceLoader.exists(path):
		load_errors.append("Appearance adapter not ready: "+path)
		read_button.disabled = true
		return
	adapter = load(path).new()
	if not adapter.build(self): load_errors.append("Appearance adapter could not bind current profile")
	if adapter.has_signal("profile_changed"): adapter.connect("profile_changed",_on_source_profile_changed)
	options = adapter.get_preview_options()
	for part in PROFILE_FIELDS:
		var picker: OptionButton = option_buttons[part]
		picker.clear()
		for entry in options.get(part,[]):
			picker.add_item(str(entry.get("label",entry.get("id",""))))
			picker.set_item_metadata(picker.item_count-1,int(entry.value))
			picker.set_item_tooltip(picker.item_count-1,str(entry.get("resource_path","")))
		picker.disabled = picker.item_count<2

func _new_avatar(at: Vector2, zoom: float, facing: Vector2) -> Node2D:
	var item: Node2D = avatar_script.new()
	item.position = at
	item.scale = Vector2.ONE*zoom
	add_child(item)
	var built: bool = item.build()
	item.visible = built
	if not built: renderer_ready = false
	if not draft.is_empty(): item.set_appearance(draft)
	item.set_direction(facing)
	item.advance(0.0,Vector2.ZERO)
	return item

func _reload_avatars() -> void:
	if actor!=null:
		remove_child(actor)
		actor.queue_free()
		actor = null
	for item in comparisons:
		remove_child(item)
		item.queue_free()
	comparisons.clear()
	var path := _folder().path_join("pawn_four_way_avatar.gd")
	renderer_ready = ResourceLoader.exists(path)
	if not renderer_ready:
		renderer_message.text = "人物样例素材尚未就绪"
		return
	avatar_script = load(path)
	actor = _new_avatar(Vector2(414,496),2.8,direction)
	for index in range(4): comparisons.append(_new_avatar(Vector2(129+index*191,750),1.02,DIRECTIONS[index]))
	renderer_message.visible = not renderer_ready
	renderer_message.text = "人物样例素材尚未制作完成"
	_apply_debug_layers()

func read_saved_appearance() -> void:
	if adapter==null:
		source_label.text = "外观数据尚未就绪"
		return
	var result: Dictionary = adapter.resolve_current()
	if initial_profile.is_empty() and not result.is_empty():
		initial_profile = result.duplicate(true)
		initial_source = result.get("capture_source",{}).duplicate(true)
	set_preview_profile(result)
	reset_button.disabled = initial_profile.is_empty()

func restore_initial_appearance() -> void:
	if not initial_profile.is_empty(): set_preview_profile(initial_profile.duplicate(true))

func use_demo_sample() -> void:
	if adapter!=null: set_preview_profile(adapter.get_demo_sample_profile())

func _on_source_profile_changed(profile: Dictionary) -> void:
	set_preview_profile(profile)

func _on_option_selected(index: int, part: String) -> void:
	if adapter==null or draft.is_empty(): return
	var value := int((option_buttons[part] as OptionButton).get_item_metadata(index))
	set_preview_profile(adapter.with_field(draft,PROFILE_FIELDS[part],value))

func set_preview_profile(profile: Dictionary) -> void:
	draft = profile.duplicate(true)
	for part in PROFILE_FIELDS:
		var picker: OptionButton = option_buttons[part]
		for index in range(picker.item_count):
			if picker.get_item_metadata(index)==draft.get(PROFILE_FIELDS[part]): picker.select(index)
	if actor!=null: actor.set_appearance(draft)
	for item in comparisons: item.set_appearance(draft)
	var source := str(draft.get("source","unavailable"))
	var source_text := "当前人物外观" if source=="runtime_player" else ("开局外观副本" if source=="game_start_preview" else ("本次试穿" if source=="private_preview_draft" else "外观暂不可用"))
	var description := str(draft.get("source_label","来源："+source_text))
	source_label.text = description if description.contains("存档") else description+" · 不写入存档"
	var support: Dictionary = draft.get("asset_support",{})
	if renderer_ready:
		renderer_message.visible = not bool(support.get("supported",false))
		renderer_message.text = str(support.get("message","当前外观尚未制作对应样例，可选右侧“本套样例”"))+"\n可选右侧“本套样例”查看独立草稿"
	var labels := []
	for part in PROFILE_FIELDS:
		var picker: OptionButton = option_buttons[part]
		if picker.selected>=0: labels.append(picker.get_item_text(picker.selected))
	resource_label.text = " / ".join(labels)
	_render_portrait()
	_apply_debug_layers()

func _vec(value: Variant, fallback: Vector2) -> Vector2:
	if value is Vector2: return value
	if value is Array and value.size()==2: return Vector2(float(value[0]),float(value[1]))
	return fallback

func _texture(path: String) -> Texture2D:
	if portrait_textures.has(path): return portrait_textures[path]
	var image := Image.load_from_file(ProjectSettings.globalize_path(path))
	if image==null: return null
	var texture := ImageTexture.create_from_image(image)
	portrait_textures[path] = texture
	return texture

func _render_portrait() -> void:
	for item in portrait_nodes:
		portrait_root.remove_child(item)
		item.queue_free()
	portrait_nodes.clear()
	portrait_missing.clear()
	portrait_layers = adapter.get_portrait_layers(draft) if adapter!=null and not draft.is_empty() else []
	for entry in portrait_layers:
		if not bool(entry.get("visible",true)): continue
		var path := str(entry.get("resource_path",""))
		if path.is_empty(): continue
		var texture := _texture(path)
		if texture==null:
			portrait_missing.append(path)
			continue
		var sprite := Sprite2D.new()
		sprite.name = str(entry.get("part","layer"))
		sprite.set_meta("portrait_part",str(entry.get("part","")))
		sprite.texture = texture
		sprite.centered = false
		sprite.position = _vec(entry.get("position"),Vector2.ZERO)
		sprite.offset = _vec(entry.get("offset"),Vector2.ZERO)
		sprite.scale = _vec(entry.get("scale"),Vector2.ONE)
		var tint: Color = entry.get("modulate",Color.WHITE)
		tint.a *= float(entry.get("alpha",1.0))
		sprite.modulate = tint
		sprite.z_index = int(entry.get("z_index",0))
		portrait_root.add_child(sprite)
		portrait_nodes.append(sprite)

func set_debug_layers(front: bool, back: bool) -> void:
	show_front = front
	show_back = back
	if front_check!=null: front_check.set_pressed_no_signal(front)
	if back_check!=null: back_check.set_pressed_no_signal(back)
	_apply_debug_layers()

func _apply_debug_layers() -> void:
	var items: Array[Node2D] = comparisons.duplicate()
	if actor!=null: items.append(actor)
	for item in items:
		if item.has_method("set_layer_visible"):
			item.set_layer_visible("hair_front",show_front)
			item.set_layer_visible("hair_back",show_back)
	for item in portrait_nodes:
		var part := str(item.get_meta("portrait_part",""))
		if part in ["hair_front","front_hair"]: item.visible = show_front
		if part in ["hair_back","back_hair"]: item.visible = show_back

func set_preview_direction(value: Vector2) -> void:
	if value==Vector2.ZERO: return
	direction = Vector2(signf(value.x),0) if absf(value.x)>absf(value.y) else Vector2(0,signf(value.y))
	if actor!=null: actor.set_direction(direction)
	status_label.text = "朝向："+DIRECTION_NAMES[DIRECTIONS.find(direction)]+(" · 已暂停" if paused else "")
	queue_redraw()

func toggle_pause() -> void:
	paused = not paused
	pause_button.text = "继续" if paused else "暂停"
	set_preview_direction(direction)

func advance_preview(delta: float, velocity: Vector2) -> void:
	if actor==null: return
	var previous_position := actor.position
	actor.position += velocity*delta
	actor.position = actor.position.clamp(WALK_BOUNDS.position,WALK_BOUNDS.end)
	last_velocity = (actor.position-previous_position)/delta if delta>0.0 else Vector2.ZERO
	if velocity.length_squared()>0.01 and not mouse_facing: set_preview_direction(velocity)
	actor.advance(delta,last_velocity/actor.scale.x)
	clock += delta

func _process(delta: float) -> void:
	if paused or capture_active or actor==null: return
	var input := Vector2(float(Input.is_physical_key_pressed(KEY_D))-float(Input.is_physical_key_pressed(KEY_A)),float(Input.is_physical_key_pressed(KEY_S))-float(Input.is_physical_key_pressed(KEY_W)))
	if mouse_facing and Rect2(32,105,770,480).has_point(get_global_mouse_position()): set_preview_direction(get_global_mouse_position()-actor.position)
	advance_preview(delta,input.normalized()*170.0)

func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo: return
	if event.physical_keycode in [KEY_1,KEY_2,KEY_3,KEY_4]: set_preview_direction(DIRECTIONS[int(event.physical_keycode)-int(KEY_1)])
	elif event.physical_keycode==KEY_SPACE: toggle_pause()
	elif event.physical_keycode==KEY_HOME and actor!=null: actor.position = Vector2(414,496)
	elif event.physical_keycode==KEY_F5: _reload_avatars()

func get_diagnostics() -> Dictionary:
	var cards := []
	for item in comparisons: cards.append(item.get_diagnostics())
	return {"renderer_ready":renderer_ready,"errors":load_errors,"profile":draft.duplicate(true),"initial_capture_source":initial_source.duplicate(true),"portrait_layer_count":portrait_nodes.size(),"portrait_missing":portrait_missing.duplicate(),"portrait_layers":portrait_layers.duplicate(true),"show_front":show_front,"show_back":show_back,"actor":actor.get_diagnostics() if actor!=null else {},"comparisons":cards,"direction":direction,"position":actor.position if actor!=null else Vector2.ZERO,"clock":clock,"preview_velocity":last_velocity,"writes_formal_save":false}

func _exit_tree() -> void:
	if adapter!=null: adapter.unbind()

func _snapshot(path: String) -> void:
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var result := get_viewport().get_texture().get_image().save_png(path)
	if result!=OK: push_error("Four-way room capture failed: "+path)

func _capture_room(destination: String) -> void:
	if not renderer_ready or adapter==null or draft.is_empty():
		push_error("Four-way room capture requires the renderer and real appearance adapter")
		get_tree().quit(1)
		return
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	var snapshots := []
	var original := draft.duplicate(true)
	await _snapshot(output.path_join("room_overview.png"))
	if OS.get_cmdline_user_args().has("--overview-only"):
		FileAccess.open(output.path_join("overview_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"diagnostics":get_diagnostics(),"overview_only":true,"scope":"Single native screenshot for current colour/layout review; no visual approval or save write."},"\t"))
		print("PAWN FOUR WAY OVERVIEW CAPTURE PASS: ",output)
		get_tree().quit()
		return
	for front in options.get("hair_front",[]):
		for back in options.get("hair_back",[]):
			var profile: Dictionary = adapter.with_field(original,"appearance_hair_style",int(front.value))
			profile = adapter.with_field(profile,"appearance_hair_back_style",int(back.value))
			set_preview_profile(profile)
			var file := "front_%d_back_%d.png"%[int(front.value),int(back.value)]
			await _snapshot(output.path_join(file))
			snapshots.append({"file":file,"diagnostics":get_diagnostics()})
	var debug_snapshots := []
	for pair in [[false,true],[true,false],[false,false]]:
		set_debug_layers(bool(pair[0]),bool(pair[1]))
		var file := "debug_front_%s_back_%s.png"%[str(pair[0]),str(pair[1])]
		await _snapshot(output.path_join(file))
		debug_snapshots.append({"file":file,"diagnostics":get_diagnostics()})
	set_debug_layers(true,true)
	var movement := []
	actor.position = Vector2(414,458)
	for frame in range(120):
		var velocity: Vector2 = DIRECTIONS[int(frame/30)]*90.0
		advance_preview(1.0/30.0,velocity)
		var file := "move_%03d.png"%frame
		await _snapshot(output.path_join(file))
		movement.append({"file":file,"frame":frame,"direction":direction,"position":actor.position,"avatar":actor.get_diagnostics()})
	set_preview_profile(original)
	FileAccess.open(output.path_join("capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"initial_capture_source":initial_source,"snapshots":snapshots,"debug_snapshots":debug_snapshots,"movement":movement,"fps":30,"scope":"Standalone four-direction draft; original portrait layers and pawn read the same resolved appearance. No production save/default changes."},"\t"))
	print("PAWN FOUR WAY ROOM CAPTURE PASS: ",output)
	get_tree().quit()
