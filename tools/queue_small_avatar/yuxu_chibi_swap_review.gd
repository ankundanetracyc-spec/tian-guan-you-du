extends Control

const ART := "res://textures/characters/yuxu_chibi_linework_review_20261115/"
const TITLE := "res://tscn/scene/StartMenu.tscn"
const FONTPATH := "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"

var _assets: Dictionary
var _outfit := OptionButton.new()
var _front := OptionButton.new()
var _back := OptionButton.new()
var _strike := CheckButton.new()
var _zoom := CheckButton.new()
var _fighters: Array[Node2D] = []
var _stage := Node2D.new()

func _ready() -> void:
	var data = JSON.parse_string(FileAccess.get_file_as_string(ART + "neck_revision/anchors.json"))
	if not data is Dictionary:
		push_error("玉虚换装试样缺少素材定位数据")
		return
	_assets = data
	for outfit: Dictionary in _assets.outfits.values():
		for pose: Dictionary in outfit.poses.values():
			pose.texture = _read_texture(pose.file)
	for section in ["front", "back"]:
		for hair: Dictionary in _assets[section].values():
			hair.texture = _read_texture(hair.file)

	var shadow := GradientTexture2D.new()
	var fade := Gradient.new()
	fade.colors = PackedColorArray([Color(0.08, 0.11, 0.09, 0.33), Color(0.08, 0.11, 0.09, 0.0)])
	shadow.gradient = fade
	shadow.fill = GradientTexture2D.FILL_RADIAL
	shadow.fill_from = Vector2(0.5, 0.5)
	shadow.fill_to = Vector2(1.0, 0.5)
	add_child(_stage)
	for index in range(2):
		var fighter := Node2D.new()
		_stage.add_child(fighter)
		_fighters.append(fighter)
		var contact_shadow := Sprite2D.new()
		contact_shadow.name = "FootShadow"
		contact_shadow.texture = shadow
		fighter.add_child(contact_shadow)
		for part_name in ["BackHair", "BodyAndFace", "FrontHair"]:
			var sprite := Sprite2D.new()
			sprite.name = part_name
			sprite.centered = false
			sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
			fighter.add_child(sprite)
	_build_controls()
	resized.connect(_refresh)
	for arg in OS.get_cmdline_user_args():
		if arg == "--verify":
			_verify()
			return
		elif arg == "--strike":
			_strike.button_pressed = true
		elif arg == "--closeup":
			_zoom.button_pressed = true
		elif arg.begins_with("--capture="):
			_capture(arg.substr(10))
	_refresh()

func _read_texture(file: String) -> Texture2D:
	var path := ART + file
	var texture: Texture2D
	if ResourceLoader.exists(path, "Texture2D"):
		texture = load(path)
	var image := texture.get_image() if texture else Image.load_from_file(ProjectSettings.globalize_path(path))
	if image == null or image.is_empty():
		push_error("无法载入 " + path)
		return null
	if not image.has_mipmaps():
		image.generate_mipmaps()
	return ImageTexture.create_from_image(image)

func _build_controls() -> void:
	var paper_theme := Theme.new()
	paper_theme.default_font = load(FONTPATH)
	paper_theme.default_font_size = 22
	theme = paper_theme
	var top := PanelContainer.new()
	top.add_theme_stylebox_override("panel", _bar_style())
	add_child(top)
	top.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	top.offset_bottom = 77
	var title_row := HBoxContainer.new()
	top.add_child(title_row)
	var title := Label.new()
	title.text = "玉虚 · Q 版换装试样"
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.add_theme_font_size_override("font_size", 28)
	title_row.add_child(title)
	var back_button := Button.new()
	back_button.name = "ReturnToTitle"
	back_button.text = "返回首页"
	back_button.pressed.connect(func(): get_tree().change_scene_to_file(TITLE))
	title_row.add_child(back_button)

	var bottom := PanelContainer.new()
	bottom.add_theme_stylebox_override("panel", _bar_style())
	add_child(bottom)
	bottom.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	bottom.offset_top = -81
	var controls := HBoxContainer.new()
	controls.alignment = BoxContainer.ALIGNMENT_CENTER
	controls.add_theme_constant_override("separation", 16)
	bottom.add_child(controls)

	_add_choice(controls, "服装", _outfit, _assets.outfits)
	_add_choice(controls, "前发", _front, _assets.front)
	_add_choice(controls, "后发", _back, _assets.back)
	_strike.text = "出掌"
	_zoom.text = "近看"
	for button in [_strike, _zoom]:
		button.toggled.connect(func(_value: bool): _refresh())
		controls.add_child(button)

func _bar_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.10, 0.15, 0.14, 0.92)
	style.content_margin_left = 24
	style.content_margin_right = 24
	style.content_margin_top = 14
	style.content_margin_bottom = 14
	return style

func _add_choice(row: HBoxContainer, title: String, menu: OptionButton, items: Dictionary) -> void:
	var label := Label.new()
	label.text = title
	row.add_child(label)
	for key: String in items:
		menu.add_item(items[key].name)
		menu.set_item_metadata(menu.item_count - 1, key)
	menu.item_selected.connect(func(_index: int): _refresh())
	row.add_child(menu)

func _refresh() -> void:
	var ratio := minf(size.x / 1280.0, size.y / 720.0)
	_stage.scale = Vector2.ONE * ratio
	_stage.position = (size - Vector2(1280, 720) * ratio) * 0.5
	var draw_scale: float = (370.0 if _zoom.button_pressed else 160.0) / float(_assets.reference_height)
	var left := _fighters[0]
	var right := _fighters[1]
	left.position = Vector2(454, 532 if _zoom.button_pressed else 457)
	right.position = Vector2(826, 498 if _zoom.button_pressed else 426)
	left.scale = Vector2.ONE * draw_scale
	right.scale = Vector2(-draw_scale, draw_scale)
	var outfit: Dictionary = _assets.outfits[_outfit.get_selected_metadata()]
	var pose := "palm" if _strike.button_pressed else "idle"
	_apply(left, outfit.poses[pose], _assets.front[_front.get_selected_metadata()], _assets.back[_back.get_selected_metadata()])
	_apply(right, _assets.outfits.outer.poses.idle, _assets.front.xingzhou, _assets.back.xingzhou)

func _apply(fighter: Node2D, pose: Dictionary, front: Dictionary, back: Dictionary) -> void:
	var body_scale := float(pose.pixel_scale)
	var floor_point := _point(pose.ground_anchor) * body_scale
	var hair_point := _point(pose.hair_anchor) * body_scale - floor_point
	var shadow := fighter.get_node("FootShadow") as Sprite2D
	shadow.scale = Vector2(float(pose.shadow_width), 94.0) / shadow.texture.get_size()
	_sprite(fighter, "BackHair", back.texture, hair_point - _point(back.anchor))
	_sprite(fighter, "BodyAndFace", pose.texture, -floor_point, body_scale)
	_sprite(fighter, "FrontHair", front.texture, hair_point - _point(front.anchor))

func _sprite(fighter: Node2D, layer: String, texture: Texture2D, at: Vector2, pixel_scale := 1.0) -> void:
	var sprite := fighter.get_node(layer) as Sprite2D
	sprite.texture = texture
	sprite.position = at
	sprite.scale = Vector2.ONE * pixel_scale

func _point(coords: Array) -> Vector2:
	return Vector2(coords[0], coords[1])

func _verify() -> void:
	for outfit: Dictionary in _assets.outfits.values():
		for pose: Dictionary in outfit.poses.values():
			for front: Dictionary in _assets.front.values():
				for back: Dictionary in _assets.back.values():
					var fighter := _fighters[0]
					_apply(fighter, pose, front, back)
					var body := fighter.get_node("BodyAndFace") as Sprite2D
					assert(body.texture != null)
					assert(fighter.get_node("FrontHair").texture != null)
					assert(fighter.get_node("BackHair").texture != null)
					var front_point: Vector2 = fighter.get_node("FrontHair").position + _point(front.anchor)
					var back_point: Vector2 = fighter.get_node("BackHair").position + _point(back.anchor)
					assert(front_point == back_point)
					assert((body.position + body.scale * _point(pose.hair_anchor)).is_equal_approx(front_point))
					assert((body.position + body.scale * _point(pose.ground_anchor)).is_zero_approx())
	print("Yuxu chibi: 16 pose/outfit/hair combinations")
	get_tree().quit()

func _capture(path: String) -> void:
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(path)
	get_tree().quit()
