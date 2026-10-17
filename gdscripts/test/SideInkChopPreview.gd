extends Node2D

const ART_ROOT := "res://textures/characters/side_ink_portrait_outfits_20261015_v3/"
const CANVAS_SIZE := Vector2(2048.0, 2048.0)
const GROUND_ORIGIN := Vector2(1024.0, 1900.0)
const REFERENCE_HEIGHT := 1375.0
const MOTION_ENVELOPE_WIDTH := 1600.0
const FX_ROOT := "res://textures/vfx/ink_chop_sequence_20261016_v2/"
const FX_FPS := 24.0
const HIT_HOLD := 0.20
const CYCLE_DURATION := 3.8
const WINDUP_START := 1.10
const STRIKE_START := 1.36
const IMPACT_START := STRIKE_START + 5.0 / FX_FPS
const RECOVER_START := IMPACT_START + 2.0 / FX_FPS + HIT_HOLD
const RECOVER_END := RECOVER_START + 18.0 / FX_FPS
const RESPONSE_PLAYBACK := preload("res://gdscripts/test/SideInkResponsePlayback.gd")

class InkOverlay extends Node2D:
	var preview: Node2D
	var actor_index := 0
	func _draw() -> void:
		if is_instance_valid(preview):
			preview._paint_actor_effects(self, actor_index)

var actors: Array[Dictionary] = []
var outfit_manifest: Dictionary = {}
var response_playback: RefCounted
var action_kind := "attack"
var action_buttons_row: HBoxContainer
var action_buttons: Dictionary = {}
var hud_layer: CanvasLayer
var title_label: Label
var subtitle_label: Label
var status_label: Label
var name_labels: Array[Label] = []
var controls_label: Label
var buttons_row: HBoxContainer
var pause_button: Button
var timeline: HSlider
var portrait_reference: TextureRect
var portrait_caption: Label
var show_portrait_reference := false
var sequence_frames: SpriteFrames
var comparison := false
var selected_actor := 0
var effects_enabled := true
var cinematic_zoom := 1.0
var camera_shake := Vector2.ZERO
var cycle_time := 0.0
var phase_name := "idle"
var paused := false
var background_mode := 0
var simulate_missing_action := false
var mirrored := false
var show_guides := false
var display_size_mode := 0
var local_focus: Node

func _ready() -> void:
	z_index = -20
	var wardrobe: Variant = JSON.parse_string(FileAccess.get_file_as_string(ART_ROOT + "wardrobe.json"))
	if wardrobe is Dictionary:
		outfit_manifest = wardrobe.get("outfits", {})
	sequence_frames = load(FX_ROOT + "chop_effects.tres") as SpriteFrames
	response_playback=RESPONSE_PLAYBACK.new()
	response_playback.configure(self)
	_create_actor("male", 0)
	_create_actor("female", 1)
	_create_hud()
	local_focus=load("res://csharp/battle/BattleLocalFocusView.cs").new()
	add_child(local_focus)
	get_viewport().size_changed.connect(_layout)
	_layout()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--preview-action="):
			var requested:=argument.trim_prefix("--preview-action=")
			if requested in ["attack","hurt","guard","restore","dodge"]:
				_set_action(requested)

func _process(delta: float) -> void:
	if paused:
		return
	cycle_time = fposmod(cycle_time + delta, CYCLE_DURATION)
	_refresh_cycle()

func _unhandled_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	match event.keycode:
		KEY_SPACE:
			_toggle_pause()
		KEY_R:
			_restart()
		KEY_B:
			_cycle_background()
		KEY_F:
			_toggle_missing()
		KEY_M:
			_toggle_mirror()
		KEY_G:
			_toggle_guides()
		KEY_V:
			_cycle_display_size()
		KEY_TAB:
			_switch_actor()
		KEY_C:
			_toggle_comparison()
		KEY_LEFT:
			_step_frame(-1)
		KEY_RIGHT:
			_step_frame(1)
		KEY_E:
			effects_enabled = not effects_enabled
			_refresh_cycle()
		KEY_P:
			_toggle_portrait_reference()
		KEY_1:
			_set_action("attack")
		KEY_2:
			_set_action("hurt")
		KEY_3:
			_set_action("guard")
		KEY_4:
			_set_action("restore")
		KEY_5:
			_set_action("dodge")
		KEY_ESCAPE:
			_return_home()
		_:
			return
	get_viewport().set_input_as_handled()

func _load_pose(actor_id: String, pose: String) -> Texture2D:
	var path := ART_ROOT + actor_id + "_" + pose + ".png"
	if not ResourceLoader.exists(path):
		return null
	return load(path) as Texture2D

func _create_actor(actor_id: String, index: int) -> void:
	var stage := Node2D.new()
	stage.name = actor_id.capitalize() + "CinematicStage"
	add_child(stage)
	var root := Node2D.new()
	root.name = actor_id.capitalize() + "GroundOrigin"
	root.z_as_relative = false
	root.z_index = 0
	stage.add_child(root)
	var idle_sprite := _make_sprite(actor_id.capitalize() + "Idle", _load_pose(actor_id, "idle"))
	var chop_sprite := _make_sprite(actor_id.capitalize() + "Chop", _load_pose(actor_id, "chop"))
	root.add_child(idle_sprite)
	root.add_child(chop_sprite)
	var outfit: Dictionary = outfit_manifest.get(actor_id, {})
	var point: Array = outfit.get("fx_hit", [650.0,-355.0])
	var hit := Vector2(float(point[0]),float(point[1]))
	var shift := hit - Vector2(650.0,-355.0)
	var slash := _make_effect("slash", Vector2(480.0,-780.0) + shift, Vector2(940.0,1080.0))
	root.add_child(slash)
	var impact := _make_effect("impact", hit + Vector2(28.0,28.0), Vector2(560.0,560.0))
	root.add_child(impact)
	var overlay := InkOverlay.new()
	overlay.name = "IndependentInkEffects"
	overlay.preview = self
	overlay.actor_index = index
	overlay.z_index = 2
	root.add_child(overlay)
	actors.append({
		"id": actor_id, "root": root, "stage":stage, "idle": idle_sprite, "chop": chop_sprite,
		"slash":slash, "impact":impact,
		"outfit":outfit, "fx_hit":hit,
		"overlay": overlay, "base_position": Vector2.ZERO, "base_scale": 1.0
	})
	response_playback.attach(actors.back())

func _make_effect(animation_name: String, at: Vector2, dimensions: Vector2) -> AnimatedSprite2D:
	var effect := AnimatedSprite2D.new()
	effect.name = animation_name.capitalize() + "Sequence"
	effect.sprite_frames = sequence_frames
	effect.animation = animation_name
	effect.position = at
	effect.scale = dimensions / 768.0
	effect.z_index = 4 if animation_name == "slash" else 6
	effect.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	var palette := ShaderMaterial.new()
	palette.shader = load(FX_ROOT + "palette.gdshader")
	effect.material = palette
	return effect

func _make_sprite(node_name: String, texture: Texture2D) -> Sprite2D:
	var sprite := Sprite2D.new()
	sprite.name = node_name
	sprite.texture = texture
	sprite.centered = false
	sprite.offset = -GROUND_ORIGIN
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	sprite.modulate.a = 0.0
	return sprite

func _create_hud() -> void:
	hud_layer = CanvasLayer.new()
	hud_layer.layer = 5
	add_child(hud_layer)
	title_label = _make_label("墨落 · 通用出手", 30)
	subtitle_label = _make_label("立绘默认衣装 · 中分长披 · 蓄势 → 下劈 → 飞墨消散", 18)
	status_label = _make_label("", 16)
	name_labels.append(_make_label("男修 · " + str(actors[0]["outfit"].get("display_name","")), 22))
	name_labels.append(_make_label("女修 · " + str(actors[1]["outfit"].get("display_name","")), 22))
	controls_label = _make_label("Space 暂停　← → 逐帧　Tab 男女　C 对照　P 立绘　E 墨效　G 边界　V 尺寸　F 缺图回退", 15)
	buttons_row = HBoxContainer.new()
	buttons_row.add_theme_constant_override("separation", 8)
	hud_layer.add_child(buttons_row)
	_add_button("重播演出", _replay_strike)
	pause_button = _add_button("暂停", _toggle_pause)
	_add_button("男／女", _switch_actor)
	_add_button("单人／对照", _toggle_comparison)
	_add_button("立绘对照", _toggle_portrait_reference)
	_add_button("镜像朝向", _toggle_mirror)
	_add_button("浅／深背景", _cycle_background)
	_add_button("画布边界", _toggle_guides)
	_add_button("局内尺寸", _cycle_display_size)
	_add_button("返回首页", _return_home)
	timeline = HSlider.new()
	timeline.min_value = 0.0
	timeline.max_value = CYCLE_DURATION
	timeline.step = 1.0 / FX_FPS
	timeline.focus_mode = Control.FOCUS_NONE
	timeline.value_changed.connect(_scrub)
	hud_layer.add_child(timeline)
	portrait_reference = TextureRect.new()
	portrait_reference.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait_reference.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	portrait_reference.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud_layer.add_child(portrait_reference)
	portrait_caption = _make_label("立绘参考", 17)
	action_buttons_row=HBoxContainer.new()
	action_buttons_row.add_theme_constant_override("separation",10)
	hud_layer.add_child(action_buttons_row)
	for kind in ["attack","hurt","guard","restore","dodge"]:
		var text_value: String={"attack":"通用出手 [1]","hurt":"受击 [2]","guard":"应对防御 [3]","restore":"休整回气 [4]","dodge":"闪避 [5]"}[kind]
		var button:=_add_button(text_value,_set_action.bind(kind),action_buttons_row)
		button.toggle_mode=true
		action_buttons[kind]=button
	_update_action_buttons()

func _make_label(text_value: String, font_size: int) -> Label:
	var label := Label.new()
	label.text = text_value
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", Color8(232, 225, 207))
	var font := load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf") as Font
	if font:
		label.add_theme_font_override("font", font)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud_layer.add_child(label)
	return label

func _add_button(text_value: String, action: Callable, row: HBoxContainer = null) -> Button:
	var button := Button.new()
	button.text = text_value
	button.custom_minimum_size = Vector2(110.0, 38.0)
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.focus_mode = Control.FOCUS_NONE
	button.add_theme_font_size_override("font_size", 16)
	button.add_theme_color_override("font_color", Color8(34, 57, 50))
	var style := StyleBoxFlat.new()
	style.bg_color = Color8(221, 211, 187)
	style.border_color = Color8(87, 108, 93)
	style.set_border_width_all(1)
	style.set_corner_radius_all(4)
	button.add_theme_stylebox_override("normal", style)
	var hover := style.duplicate() as StyleBoxFlat
	hover.bg_color = Color8(241, 232, 210)
	button.add_theme_stylebox_override("hover", hover)
	var selected:=style.duplicate() as StyleBoxFlat
	selected.bg_color=Color8(165,190,173)
	button.add_theme_stylebox_override("pressed",selected)
	button.pressed.connect(action)
	(row if row else buttons_row).add_child(button)
	return button

func _layout() -> void:
	var size := get_viewport_rect().size
	if size.x <= 0.0 or size.y <= 0.0:
		return
	# The idle body height controls scale. Canvas padding and pose bounds do not.
	var panel_width := size.x * 0.5 if comparison else size.x
	var envelope := 2100.0 if comparison else MOTION_ENVELOPE_WIDTH
	var target_height := minf(clampf(size.y - 420.0, 190.0, 520.0), maxf(190.0, (panel_width - 100.0) * REFERENCE_HEIGHT / envelope))
	# Reserve space above the attack arc during the strongest camera push.
	target_height = minf(target_height, maxf(190.0, (size.y - 375.0) * REFERENCE_HEIGHT / (1320.0 * 1.28)))
	if display_size_mode == 1:
		target_height = 190.0
	elif display_size_mode == 2:
		target_height = 320.0
	var scale_factor := target_height / REFERENCE_HEIGHT
	for index in range(actors.size()):
		var horizontal := size.x * (0.22 if index == 0 else 0.72) if comparison else size.x * 0.44
		actors[index]["base_position"] = Vector2(horizontal, size.y - 200.0)
		actors[index]["stage"].visible = comparison or selected_actor == index
		actors[index]["base_scale"] = scale_factor
	title_label.position = Vector2(0.0, 15.0)
	title_label.size = Vector2(size.x, 44.0)
	subtitle_label.position = Vector2(0.0, 62.0)
	subtitle_label.size = Vector2(size.x, 30.0)
	status_label.position = Vector2(0.0, 101.0)
	status_label.size = Vector2(size.x, 30.0)
	action_buttons_row.position=Vector2(size.x*.5-320.0,143.0)
	action_buttons_row.size=Vector2(640.0,38.0)
	for index in range(name_labels.size()):
		name_labels[index].position = Vector2(size.x * float(index) * 0.5 if comparison else 0.0, size.y - 185.0)
		name_labels[index].size = Vector2(size.x * 0.5 if comparison else size.x, 36.0)
		name_labels[index].visible = comparison or selected_actor == index
	buttons_row.position = Vector2(30.0, size.y - 132.0)
	buttons_row.size = Vector2(size.x - 60.0, 38.0)
	timeline.position = Vector2(90.0, size.y - 83.0)
	timeline.size = Vector2(size.x - 180.0, 20.0)
	controls_label.position = Vector2(0.0, size.y - 58.0)
	controls_label.size = Vector2(size.x, 38.0)
	portrait_reference.position = Vector2(30.0,205.0)
	portrait_reference.size = Vector2(minf(225.0,size.x*.18),minf(340.0,size.y-380.0))
	portrait_caption.position = portrait_reference.position + Vector2(0.0,portrait_reference.size.y + 8.0)
	portrait_caption.size = Vector2(portrait_reference.size.x,46.0)
	_update_portrait_reference()
	_update_name_colors()
	_refresh_cycle()

func _refresh_cycle() -> void:
	if action_kind!="attack":
		response_playback.apply(action_kind,cycle_time)
		_finish_refresh()
		return
	response_playback.hide_responses()
	if cycle_time < WINDUP_START:
		phase_name = "idle"
	elif cycle_time < STRIKE_START:
		phase_name = "windup"
	elif cycle_time < IMPACT_START:
		phase_name = "strike"
	elif cycle_time < RECOVER_START:
		phase_name = "impact"
	elif cycle_time < RECOVER_END:
		phase_name = "recover"
	else:
		phase_name = "idle"
	_apply_actor_poses()
	_apply_sequences()
	_apply_camera()
	_finish_refresh()

func _finish_refresh() -> void:
	if timeline:
		timeline.set_value_no_signal(cycle_time)
	_refresh_status()
	queue_redraw()
	for actor in actors:
		actor["overlay"].queue_redraw()

func _apply_actor_poses() -> void:
	for actor in actors:
		var root: Node2D = actor["root"]
		var idle: Sprite2D = actor["idle"]
		var chop: Sprite2D = actor["chop"]
		var scale_factor: float = actor["base_scale"]
		var direction := -1.0 if mirrored else 1.0
		var local_offset := Vector2.ZERO
		var use_chop := phase_name in ["strike", "impact"]
		match phase_name:
			"windup":
				var t := clampf((cycle_time - WINDUP_START) / (STRIKE_START - WINDUP_START), 0.0, 1.0)
				local_offset.x = -30.0 * t
			"strike":
				var t := clampf((cycle_time - STRIKE_START) / (IMPACT_START - STRIKE_START), 0.0, 1.0)
				local_offset.x = lerpf(-30.0, 12.0, 1.0 - pow(1.0 - t, 3.0))
			"impact":
				local_offset.x = 12.0
			"recover":
				var t := clampf((cycle_time - RECOVER_START) / (RECOVER_END - RECOVER_START), 0.0, 1.0)
				local_offset.x = lerpf(12.0, 0.0, t)
				use_chop = t < 0.55
		var available := chop.texture != null and not simulate_missing_action
		idle.visible = not (use_chop and available)
		chop.visible = use_chop and available
		idle.modulate.a = 1.0
		chop.modulate.a = 1.0
		root.position = actor["base_position"] + Vector2(local_offset.x * direction, local_offset.y) * scale_factor
		root.scale = Vector2(direction * scale_factor, scale_factor)

func _refresh_status() -> void:
	var phase_text: String = {"idle":"待机", "windup":"短促蓄势", "strike":"手刀下劈", "impact":"命中停顿", "recover":"回到待机"}.get(phase_name, "待机")
	var size_text := "自由预览" if display_size_mode == 0 else ("190 像素" if display_size_mode == 1 else "320 像素")
	if action_kind!="attack":
		phase_text=response_playback.phase_text
	status_label.text = ("已暂停 · " if paused else "") + phase_text + "　｜　" + size_text
	if effects_enabled:
		if action_kind=="attack":
			status_label.text += "　｜　挥砍 %02d/16 · 命中 %02d/20" % [actors[0]["slash"].frame + 1, actors[0]["impact"].frame + 1]
		else:
			status_label.text += "　｜　%02d/%02d 帧" % [response_playback.frame_number+1,response_playback.frame_count]
	if simulate_missing_action:
		status_label.text += "　｜　缺图回退"
	if pause_button:
		pause_button.text = "继续" if paused else "暂停"

func _toggle_pause() -> void:
	paused = not paused
	_refresh_status()

func _restart() -> void:
	cycle_time = 0.0
	paused = false
	_refresh_cycle()

func _replay_strike() -> void:
	cycle_time = WINDUP_START
	paused = false
	_refresh_cycle()

func _set_action(kind: String) -> void:
	action_kind=kind
	cycle_time=1.0
	paused=false
	var names: Dictionary={"attack":"通用出手","hurt":"受击","guard":"应对防御","restore":"休整回气","dodge":"闪避"}
	title_label.text="墨落 · "+str(names.get(kind,"通用出手"))
	subtitle_label.text={"attack":"通用出手姿势 · 挥砍 → 命中定格 → 余墨渐散","hurt":"冲击入身 → 短促失衡 → 回到原位","guard":"架势迎击 → 接触定格 → 余劲拨散","restore":"轻收手臂 → 站立调息 → 回到待机","dodge":"撤身让锋 → 袖发拖影 → 回到原位"}.get(kind,"")
	_update_action_buttons()
	_refresh_cycle()

func _update_action_buttons() -> void:
	for kind in action_buttons:
		action_buttons[kind].set_pressed_no_signal(kind==action_kind)

func _cycle_background() -> void:
	background_mode = (background_mode + 1) % 3
	_update_name_colors()
	_refresh_cycle()

func _toggle_missing() -> void:
	simulate_missing_action = not simulate_missing_action
	_refresh_cycle()

func _toggle_mirror() -> void:
	mirrored = not mirrored
	_refresh_cycle()

func _toggle_guides() -> void:
	show_guides = not show_guides
	_refresh_cycle()

func _cycle_display_size() -> void:
	display_size_mode = (display_size_mode + 1) % 3
	_layout()

func _switch_actor() -> void:
	selected_actor = 1 - selected_actor
	_layout()

func _toggle_comparison() -> void:
	comparison = not comparison
	_layout()

func _toggle_portrait_reference() -> void:
	show_portrait_reference = not show_portrait_reference
	_update_portrait_reference()

func _update_portrait_reference() -> void:
	if not portrait_reference:
		return
	var outfit: Dictionary = actors[selected_actor]["outfit"]
	var path := str(outfit.get("portrait_reference",""))
	portrait_reference.texture = load(path) as Texture2D if ResourceLoader.exists(path) else null
	portrait_reference.visible = show_portrait_reference and not comparison
	portrait_caption.visible = portrait_reference.visible
	portrait_caption.text = "立绘参考\n" + str(outfit.get("display_name",""))

func _scrub(value: float) -> void:
	paused = true
	cycle_time = value
	_refresh_cycle()

func _step_frame(direction: int) -> void:
	_scrub(clampf(cycle_time + direction / FX_FPS, 0.0, CYCLE_DURATION))

func _apply_sequences() -> void:
	# Both animations are explicitly clocked; pause and contact hold freeze the same frame.
	var elapsed := cycle_time - STRIKE_START
	if cycle_time >= IMPACT_START:
		elapsed = 5.0 / FX_FPS + maxf(0.0, cycle_time - RECOVER_START)
	var slash_frame := clampi(floori(elapsed * FX_FPS + 0.0001), 0, 15)
	var impact_elapsed := cycle_time - IMPACT_START
	if impact_elapsed >= 2.0 / FX_FPS:
		impact_elapsed = 2.0 / FX_FPS + maxf(0.0, cycle_time - RECOVER_START)
	var impact_frame := clampi(floori(impact_elapsed * FX_FPS + 0.0001), 0, 19)
	for index in range(actors.size()):
		var actor := actors[index]
		var slash: AnimatedSprite2D = actor["slash"]
		var impact: AnimatedSprite2D = actor["impact"]
		slash.frame = slash_frame
		impact.frame = impact_frame
		slash.visible = effects_enabled and cycle_time >= STRIKE_START and slash_frame < 15
		impact.visible = effects_enabled and cycle_time >= IMPACT_START and impact_frame < 19
		slash.material.set_shader_parameter("dark_background", _panel_dark(index))
		impact.material.set_shader_parameter("dark_background", _panel_dark(index))

func _apply_camera() -> void:
	var weight := 0.0
	if phase_name == "windup":
		weight = .3 * (cycle_time - WINDUP_START) / (STRIKE_START - WINDUP_START)
	elif phase_name == "strike":
		weight = lerpf(.3, 1.0, (cycle_time - STRIKE_START) / (IMPACT_START - STRIKE_START))
	elif phase_name == "impact":
		weight = 1.0
	elif phase_name == "recover":
		weight = pow(1.0 - clampf((cycle_time - RECOVER_START) / (RECOVER_END - RECOVER_START), 0.0, 1.0), 2.0)
	var enabled := effects_enabled and display_size_mode == 0
	cinematic_zoom = 1.0 + weight * (.12 if comparison else .28) if enabled else 1.0
	camera_shake = Vector2.ZERO
	var contact := cycle_time - IMPACT_START
	if enabled and contact >= 0.0 and contact < .065:
		camera_shake = Vector2(sin(contact * 190.0) * 9.0, cos(contact * 220.0) * 5.0) * (1.0 - contact / .065)
	_apply_local_focus()

func _apply_local_focus() -> void:
	for actor in actors:
		actor["stage"].scale=Vector2.ONE
		actor["stage"].position=Vector2.ZERO
	if not is_instance_valid(local_focus):
		return
	var size:=get_viewport_rect().size
	var region:=Rect2(0,190,size.x,maxf(1,size.y-340))
	var actor: Dictionary=actors[selected_actor]
	var point: Vector2=actor["base_position"]+Vector2(130,-650)*float(actor["base_scale"])
	if comparison:
		point.x=size.x*.5
	local_focus.call("present_region",region,point,cinematic_zoom,.08*clampf((cinematic_zoom-1)/.28,0,1),camera_shake)

func _return_home() -> void:
	get_tree().change_scene_to_file("res://tscn/scene/StartMenu.tscn")

func _panel_dark(index: int) -> bool:
	return background_mode == 2 or (background_mode == 0 and (not comparison or index == 1))

func _update_name_colors() -> void:
	for index in range(name_labels.size()):
		name_labels[index].add_theme_color_override("font_color", Color8(231, 226, 208) if _panel_dark(index) else Color8(35, 52, 46))

func _draw() -> void:
	var size := get_viewport_rect().size
	draw_rect(Rect2(Vector2.ZERO, size), Color8(20, 26, 26))
	var count := 2 if comparison else 1
	var panel_width := size.x / count
	for panel in range(count):
		var index := panel if comparison else selected_actor
		var color := Color8(24, 30, 30) if _panel_dark(index) else Color8(229, 222, 206)
		var darken := 0.16 if phase_name == "impact" and effects_enabled else 0.0
		draw_rect(Rect2(panel_width * panel, 195.0, panel_width, maxf(0.0, size.y - 340.0)), color.darkened(darken))
		var line_color := Color(0.8, 0.79, 0.72, 0.20) if _panel_dark(index) else Color(0.15, 0.23, 0.2, 0.2)
		draw_line(Vector2(panel_width * panel + 50.0, size.y - 200.0), Vector2(panel_width * (panel + 1) - 50.0, size.y - 200.0), line_color, 1.0, true)
	if comparison:
		draw_line(Vector2(size.x * .5,145), Vector2(size.x * .5,size.y - 145), Color(.5,.54,.48,.25),1.0)

func _paint_actor_effects(target: CanvasItem, _index: int) -> void:
	if show_guides:
		target.draw_rect(Rect2(-GROUND_ORIGIN, CANVAS_SIZE), Color(0.6, 0.48, 0.2, 0.7), false, 3.0)
		target.draw_line(Vector2(-36.0, 0.0), Vector2(36.0, 0.0), Color(0.8, 0.35, 0.16), 5.0)
		target.draw_line(Vector2(0.0, -36.0), Vector2(0.0, 36.0), Color(0.8, 0.35, 0.16), 5.0)
