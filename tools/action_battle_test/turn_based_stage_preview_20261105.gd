extends Node2D

## Isolated composition review. No battle runtime, world state, or save access.
## Both layouts use the same provisional pair: male bun and existing female half-up hair.
const NativeRuntime = preload("guigu_native_first_runtime.gd")
const FemaleRuntime = preload("turn_based_female_runtime_20261106.gd")
const CANVAS := Vector2(1440, 810)
const DUEL_HEIGHT := 248.0
const TEAM_HEIGHT := 204.0
const DEFAULT_OUTPUT := "docs/美术规范/男女首套回合制人物_20261106"
const INK := Color("30413b")

class HUDLines extends Node2D:
	var preview: Node2D
	func _draw() -> void:
		preview._draw_hud_lines(self)

var project_root: String
var font: FontFile
var units := Node2D.new()
var overlays := Node2D.new()
var hud := Node2D.new()
var actors: Array[Dictionary] = []
var nameplates: Array[Rect2] = []
var textures := {}
var mode := 1
var clock := 0.0
var size_factor := 1.0
var paused := false
var mode_buttons: Array[Button] = []
var pause_button: Button
var size_label: Label
var hud_lines: HUDLines
var failures: Array[String] = []

func _path(relative: String) -> String:
	return project_root.path_join(relative).simplify_path()

func _texture(relative: String) -> Texture2D:
	if textures.has(relative): return textures[relative]
	var source := Image.load_from_file(_path(relative))
	assert(source != null, "Missing preview asset: " + relative)
	source.generate_mipmaps()
	var result := ImageTexture.create_from_image(source)
	textures[relative] = result
	return result

func _atlas(relative: String, region: Rect2) -> AtlasTexture:
	var result := AtlasTexture.new()
	result.atlas = _texture(relative)
	result.region = region
	result.filter_clip = true
	return result

func _label(parent: Node, text: String, rect: Rect2, font_size: int, color := INK, centered := false) -> Label:
	var item := Label.new()
	item.text = text
	item.position = rect.position
	item.size = rect.size
	item.add_theme_font_override("font", font)
	item.add_theme_font_size_override("font_size", font_size)
	item.add_theme_color_override("font_color", color)
	item.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	if centered: item.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	item.mouse_filter = Control.MOUSE_FILTER_IGNORE
	item.z_index = 2
	parent.add_child(item)
	return item

func _paper(parent: Node, rect: Rect2) -> NinePatchRect:
	var item := NinePatchRect.new()
	item.texture = _atlas("textures/ui/world_ink_20261104/ink_ui_atlas_v1.png", Rect2(34, 74, 442, 427))
	item.position = rect.position
	item.size = rect.size
	item.patch_margin_left = 28
	item.patch_margin_right = 28
	item.patch_margin_top = 28
	item.patch_margin_bottom = 28
	item.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(item)
	return item

func _button(text: String, rect: Rect2, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.position = rect.position
	button.size = rect.size
	button.focus_mode = Control.FOCUS_NONE
	button.z_index = 2
	button.add_theme_font_override("font", font)
	button.add_theme_font_size_override("font_size", 19)
	for state in ["normal", "hover", "pressed", "focus"]:
		var style := StyleBoxFlat.new()
		style.bg_color = Color("e5e7dc") if state == "normal" else Color("d2dace")
		style.border_color = Color("839084")
		style.set_border_width_all(1)
		style.set_corner_radius_all(2)
		button.add_theme_stylebox_override(state, style)
	button.add_theme_color_override("font_color", INK)
	button.add_theme_color_override("font_hover_color", INK)
	button.add_theme_color_override("font_pressed_color", INK)
	button.pressed.connect(action)
	hud.add_child(button)
	return button

func _ready() -> void:
	project_root = ProjectSettings.globalize_path("res://").path_join("../..").simplify_path()
	font = FontFile.new()
	assert(font.load_dynamic_font(_path("resources/font/LXGWZhenKaiSlabGB-Regular.ttf")) == OK)
	get_window().title = "回合制战场 · 人物比例预览"
	get_window().size = Vector2i(1440, 810)
	get_window().content_scale_size = Vector2i(1440, 810)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	_background()
	units.name = "PreviewActors"
	add_child(units)
	overlays.name = "UnitLabels"
	overlays.z_index = 1000
	add_child(overlays)
	hud.name = "PreviewHUD"
	hud.z_index = 2000
	add_child(hud)
	_build_hud()
	_set_mode(1)
	if "--stage-capture" in OS.get_cmdline_user_args():
		set_process(false)
		get_window().position = Vector2i(-7000, -7000)
		get_window().unfocusable = true
		_capture.call_deferred()
	else:
		var screen := DisplayServer.screen_get_usable_rect()
		var available := Vector2(screen.size-Vector2i(48,80))
		var fit := minf(1.0, minf(available.x/CANVAS.x, available.y/CANVAS.y))
		if fit > 0.0:
			get_window().size = Vector2i(CANVAS*fit)
			get_window().position = screen.position + (screen.size-get_window().size)/2

func _background() -> void:
	var background := Sprite2D.new()
	background.texture = _texture("textures/battle/ink_terrace_multiteam_20261009_v2.png")
	background.position = CANVAS * 0.5
	background.scale = CANVAS / Vector2(background.texture.get_size())
	background.z_index = -100
	var shader := Shader.new()
	shader.code = """shader_type canvas_item;
void fragment() {
	vec4 source = texture(TEXTURE, UV);
	float gray = dot(source.rgb, vec3(0.299, 0.587, 0.114));
	vec3 muted = mix(vec3(gray), source.rgb, 0.42);
	vec3 paper = vec3(0.851, 0.875, 0.831);
	float distance_wash = mix(0.46, 0.20, smoothstep(0.16, 0.68, UV.y));
	float center = smoothstep(0.10, 0.30, UV.x) * (1.0-smoothstep(0.70, 0.90, UV.x));
	float floor_area = smoothstep(0.35, 0.50, UV.y) * (1.0-smoothstep(0.82, 1.0, UV.y));
	COLOR = vec4(mix(muted, paper, distance_wash + center * floor_area * 0.13), source.a);
}
"""
	var material := ShaderMaterial.new()
	material.shader = shader
	background.material = material
	add_child(background)

func _build_hud() -> void:
	_paper(hud, Rect2(30, 22, 180, 80))
	_label(hud, "演武场", Rect2(46, 28, 148, 34), 27, INK, true)
	_label(hud, "切磋", Rect2(46, 66, 148, 22), 15, Color("687368"), true)
	_paper(hud, Rect2(530, 22, 380, 80))
	_label(hud, "第一回合", Rect2(550, 28, 340, 28), 24, INK, true)
	_label(hud, "玉虚修士 · 选择招式", Rect2(550, 66, 340, 24), 16, Color("637067"), true)
	mode_buttons.append(_button("单挑", Rect2(1070, 28, 88, 40), func(): _set_mode(1)))
	mode_buttons.append(_button("三对三", Rect2(1170, 28, 102, 40), func(): _set_mode(3)))
	pause_button = _button("暂停", Rect2(1284, 28, 94, 40), _toggle_pause)
	_label(hud, "人物大小", Rect2(1074, 83, 86, 28), 16, Color("637067"))
	_button("−", Rect2(1170, 82, 34, 30), func(): _change_size(-0.05))
	size_label = _label(hud, "100%", Rect2(1206, 82, 60, 30), 16, INK, true)
	_button("＋", Rect2(1270, 82, 34, 30), func(): _change_size(0.05))
	_button("复原", Rect2(1314, 82, 64, 30), func(): size_factor = 1.0; _relayout())
	var panel := NinePatchRect.new()
	panel.texture = _atlas("textures/ui/combat/combat_skill_bar_paper_20261012.png", Rect2(38,176,2096,390))
	panel.position = Vector2(70, 650)
	panel.size = Vector2(1300, 142)
	panel.patch_margin_left = 64
	panel.patch_margin_right = 64
	panel.patch_margin_top = 40
	panel.patch_margin_bottom = 40
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(panel)
	hud_lines = HUDLines.new()
	hud_lines.preview = self
	hud_lines.z_index = 1
	hud.add_child(hud_lines)
	_label(hud, "玉虚修士", Rect2(124, 670, 160, 36), 25)
	_label(hud, "体力", Rect2(124, 708, 50, 24), 17)
	_label(hud, "法力", Rect2(124, 740, 50, 24), 17)
	for index in range(10):
		var x := 326.0 + 99.0*index
		if index < 3:
			var names := ["应敌", "痛击", "架招"]
			var icon := TextureRect.new()
			icon.texture = _texture("textures/ui/combat/skill_icons/凡俗_" + names[index] + "_v5.png")
			icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			icon.position = Vector2(x+4, 673)
			icon.size = Vector2(68, 76)
			icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
			icon.z_index = 2
			hud.add_child(icon)
			_label(hud, names[index], Rect2(x-4, 752, 84, 28), 18, INK, true)
	queue_redraw()

func _set_mode(team_size: int) -> void:
	if mode == team_size and not actors.is_empty(): return
	mode = team_size
	for actor in actors:
		units.remove_child(actor.node)
		actor.node.queue_free()
	actors.clear()
	for item in overlays.get_children():
		overlays.remove_child(item)
		item.queue_free()
	var names := [["玉虚修士", "青岚", "玄舟"], ["对练·甲", "对练·乙", "对练·丙"]]
	for side in range(2):
		for member in range(team_size):
			var gender := (side+member)%2
			var node: Node2D = FemaleRuntime.new() if gender==1 else NativeRuntime.new()
			node.name = "SameOutfit_%d_%d" % [side, member]
			units.add_child(node)
			assert(node.build(), str(node.load_errors))
			node.set_skin_mode("experiment")
			var header := Node2D.new()
			overlays.add_child(header)
			var plate := Panel.new()
			plate.position = Vector2(-78,0)
			plate.size = Vector2(156,44)
			plate.mouse_filter = Control.MOUSE_FILTER_IGNORE
			var plate_style := StyleBoxFlat.new()
			plate_style.bg_color = Color(0.91,0.92,0.87,0.82)
			plate_style.border_color = Color(0.43,0.51,0.45,0.35)
			plate_style.border_width_bottom = 1
			plate_style.set_corner_radius_all(2)
			plate.add_theme_stylebox_override("panel",plate_style)
			header.add_child(plate)
			_label(header, names[side][member], Rect2(-72, 6, 144, 25), 18, INK, true)
			var hp := ColorRect.new()
			hp.position = Vector2(-60, 35)
			hp.size = Vector2(120, 4)
			hp.color = Color("648575") if side == 0 else Color("ae7568")
			hp.mouse_filter = Control.MOUSE_FILTER_IGNORE
			header.add_child(hp)
			actors.append({"node":node, "header":header, "side":side, "member":member, "gender":gender, "phase":float(member)*0.43 + float(side)*0.31})
	_relayout()
	for index in range(mode_buttons.size()):
		mode_buttons[index].modulate = Color("b9cabe") if (index == 0 and mode == 1) or (index == 1 and mode == 3) else Color.WHITE

func _relayout() -> void:
	nameplates.clear()
	var height := (DUEL_HEIGHT if mode == 1 else TEAM_HEIGHT) * size_factor
	for actor in actors:
		var point: Vector2 = Vector2(430, 610) if mode == 1 else [Vector2(550,570),Vector2(245,535),Vector2(385,620)][int(actor.member)]
		if actor.side == 1: point.x = CANVAS.x - point.x
		actor.node.position = point
		actor.node.z_index = int(point.y)
		actor.node.wanderer_skin.display_scale = height / float(actor.node.wanderer_skin.native_height)
		actor.header.position = point + Vector2(0, -height-60)
		nameplates.append(Rect2(actor.header.position+Vector2(-78,0), Vector2(156,44)))
		actor.height = height
	size_label.text = "%d%%" % roundi(size_factor*100)
	_tick(0.0)
	queue_redraw()

func _tick(delta: float) -> void:
	clock += delta
	for actor in actors:
		var facing := 1.0 if actor.side == 0 else -1.0
		actor.node.wanderer_skin.set_motion_context(delta, Vector2.ZERO, clock+actor.phase, facing)
		actor.node.set_character_pose("Idle", clock+actor.phase, facing)

func _process(delta: float) -> void:
	if not paused: _tick(minf(delta, 0.05))

func _toggle_pause() -> void:
	paused = not paused
	pause_button.text = "继续" if paused else "暂停"

func _change_size(change: float) -> void:
	size_factor = clampf(size_factor+change, 0.80, 1.15)
	_relayout()

func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo: return
	match event.physical_keycode:
		KEY_1: _set_mode(1)
		KEY_3: _set_mode(3)
		KEY_SPACE: _toggle_pause()
		KEY_EQUAL, KEY_KP_ADD: _change_size(0.05)
		KEY_MINUS, KEY_KP_SUBTRACT: _change_size(-0.05)
		KEY_0: size_factor = 1.0; _relayout()
		KEY_ESCAPE: get_tree().quit()

func _draw_hud_lines(painter: Node2D) -> void:
	# Static HUD samples reserve the same ten-skill footprint in both layouts.
	painter.draw_line(Vector2(298, 676),Vector2(298, 769),Color("a6aea0"),1.0,true)
	for index in range(10):
		var rect := Rect2(326+99*index, 668, 76, 82)
		painter.draw_style_box(_slot_style(index == 0),rect)
	for index in range(6):
		var bean := Rect2(178+18*index,716,13,6)
		painter.draw_style_box(_bean_style(Color("6e8e7d") if index<3 else Color("c3cbbd")),bean)
	for index in range(3):
		painter.draw_style_box(_bean_style(Color("c1cbca")),Rect2(178+18*index,748,13,6))

func _draw() -> void:
	for actor in actors:
		if actor.side == 0 and actor.member == 0:
			var center: Vector2 = actor.node.position + Vector2(0,3)
			var points := PackedVector2Array()
			for i in range(65):
				var t := TAU*float(i)/64.0
				points.append(center+Vector2(cos(t)*38,sin(t)*8))
			draw_polyline(points,Color(0.24,0.37,0.31,0.45),1.6,true)

func _slot_style(selected: bool) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.96,0.95,0.88,0.26)
	style.border_color = Color("4e6d5e") if selected else Color("a8ae9d")
	style.set_border_width_all(2 if selected else 1)
	style.set_corner_radius_all(2)
	return style

func _bean_style(color: Color) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(3)
	return style

func _capture() -> void:
	var output := _path(DEFAULT_OUTPUT)
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): output = argument.trim_prefix("--capture-dir=")
	assert(DirAccess.make_dir_recursive_absolute(output) == OK, "Cannot create preview output: " + output)
	var records := []
	for team_size in [1, 3]:
		_set_mode(team_size)
		for frame in range(12):
			_tick(1.0/30.0)
			await get_tree().process_frame
		for resolution in [Vector2i(1440,810),Vector2i(1280,720)]:
			get_window().size = resolution
			for frame in range(4): await get_tree().process_frame
			await RenderingServer.frame_post_draw
			var image := get_viewport().get_texture().get_image()
			var file_name := ("单挑" if team_size == 1 else "三对三") + "_%dx%d.png" % [resolution.x,resolution.y]
			if image.save_png(output.path_join(file_name)) != OK: failures.append("PNG capture failed: "+file_name)
			if image.get_size() != resolution: failures.append("Capture resolution mismatch: "+file_name)
			if actors.size() != team_size*2: failures.append("Actor count mismatch")
			if actors.filter(func(a: Dictionary)->bool: return a.gender == 1).size() != team_size: failures.append("Female count mismatch")
			for i in range(nameplates.size()):
				if not Rect2(0,130,1440,510).encloses(nameplates[i]): failures.append("Nameplate outside stage")
				for j in range(i):
					if nameplates[i].intersects(nameplates[j]): failures.append("Nameplate overlap")
			records.append({"file":file_name,"resolution":[resolution.x,resolution.y],"count":actors.size(),"display_height_at_1440":DUEL_HEIGHT if mode==1 else TEAM_HEIGHT,"actor_records":actors.map(func(a: Dictionary)->Dictionary: return {"side":a.side,"gender":a.gender,"foot":[a.node.position.x,a.node.position.y],"height":a.height,"native_height":a.node.wanderer_skin.native_height,"native_meshes":a.node.wanderer_skin.meshes.size(),"loaded":a.node.loaded,"art":a.node.custom_art_folder})})
	# Check the user controls on the same scene before closing the capture process.
	_change_size(-0.05)
	if not is_equal_approx(size_factor,0.95): failures.append("Size control failed")
	var before := clock
	_toggle_pause()
	_process(0.1)
	if clock != before: failures.append("Pause failed")
	_toggle_pause()
	_process(0.01)
	if clock <= before: failures.append("Resume failed")
	var report := {"passed":failures.is_empty(),"scope":"Isolated rendered composition only. Static HUD fixtures; no combat outcomes or world saves.","same_basic_pair_for_both_layouts":true,"character_sources":["guigu_native_first_runtime.gd + original white-blue male outfit/bun","turn_based_female_runtime_20261106.gd + generated female outfit and current 清簪 hair"],"background_source":"textures/battle/ink_terrace_multiteam_20261009_v2.png with preview-only shader wash","canvas":[1440,810],"records":records,"failures":failures}
	var report_file := FileAccess.open(output.path_join("预览检查.json"),FileAccess.WRITE)
	assert(report_file != null, "Cannot write preview report")
	report_file.store_string(JSON.stringify(report,"\t"))
	print("TURN_BASED_STAGE_PREVIEW ", "PASS" if failures.is_empty() else "FAIL", ": ", failures)
	get_tree().quit(0 if failures.is_empty() else 1)
