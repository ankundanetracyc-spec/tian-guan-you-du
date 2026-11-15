extends Control

const ART := "res://textures/characters/yuxu_modular_ink_20261114/"
const IDLE := ART + "idle_separate_layers_v4/"
const ACTIONS := ART + "battle_review/"
const FOREST := "res://textures/battle/forest_stone_small_20261109/"
const STANDING_HEIGHT := 1960.0
const LABELS := {
	"idle": "待机", "move": "移动", "dodge": "闪避", "guard": "架招",
	"hurt": "受击", "stagger": "失神", "palm": "出掌", "sword": "尽锋",
}
const SEQUENCE := [
	["idle", "idle", 0.7, 0.0, 0.0], ["move", "guard", 0.55, 275.0, 0.0],
	["palm", "hurt", 0.48, 275.0, 12.0], ["idle", "stagger", 0.65, 250.0, 12.0],
	["guard", "move", 0.5, 195.0, -31.0], ["dodge", "sword", 0.5, 126.0, -38.0],
	["move", "idle", 0.5, 180.0, -82.0], ["sword", "guard", 0.55, 180.0, -60.0],
	["idle", "idle", 0.8, 0.0, 0.0],
]
const HOME := Vector2(460, 456)
const OPPONENT := Vector2(830, 429)

class Fighter:
	extends Node2D

	var layers: Array[Sprite2D] = []

	func _init() -> void:
		for layer_name in ["BackHair", "BodyAndHead", "FrontHair"]:
			var part := Sprite2D.new()
			part.name = layer_name
			part.centered = false
			part.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
			add_child(part)
			layers.append(part)

	func pose(frame: Dictionary, rear: Texture2D, front: Texture2D, hair_anchors: Array[Vector2]) -> void:
		var ground := Vector2(float(frame.ground_anchor[0]), float(frame.ground_anchor[1]))
		var scalp := Vector2(float(frame.hair_anchor[0]), float(frame.hair_anchor[1])) - ground
		layers[0].position = scalp - hair_anchors[0]
		layers[2].position = scalp - hair_anchors[1]
		layers[0].texture = rear
		layers[1].texture = frame.texture
		layers[1].position = -ground
		layers[2].texture = front

var _frames := {}
var _hair_anchors: Array[Vector2] = []
var _rear: Texture2D
var _front: Texture2D
var _player := Fighter.new()
var _opponent := Fighter.new()
var _stage := Node2D.new()
var _effect := Sprite2D.new()
var _palm_effect: Texture2D
var _sword_effect: Texture2D
var _ground: Texture2D
var _pine: Texture2D
var _rocks: Texture2D
var _time := 0.0
var _step := 0
var _playing := false
var _zoom := false
var _caption := Label.new()
var _play_button := Button.new()
var _shadows: Array[Sprite2D] = []
var _fixed_pose := "idle"

func _ready() -> void:
	set_process(false)
	_ground = _texture(FOREST + "ground.png")
	_pine = _texture(FOREST + "pines.png")
	_rocks = _texture(FOREST + "rocks.png")
	_rear = _texture(IDLE + "hair_back.png")
	_front = _texture(IDLE + "hair_front.png")
	_palm_effect = _texture("res://textures/battle/sword_impact_ink_v3.png")
	_sword_effect = _texture("res://textures/battle/sword_cleave_ink_v4.png")
	var metadata = JSON.parse_string(FileAccess.get_file_as_string(ACTIONS + "poses.json"))
	if not metadata is Dictionary or _rear == null or _front == null or _ground == null or _pine == null or _rocks == null:
		_fail("人物图层尚未载入")
		return
	for key in LABELS:
		if not metadata.has(key):
			_fail("缺少姿势：" + LABELS[key])
			return
		var frame: Dictionary = metadata[key]
		frame.texture = _texture((IDLE if key == "idle" else ACTIONS) + frame.file)
		if frame.texture == null:
			_fail("无法载入姿势：" + LABELS[key])
			return
		_frames[key] = frame
	var placement = JSON.parse_string(FileAccess.get_file_as_string(IDLE + "placement.json"))
	var idle_anchor: Array = _frames["idle"].hair_anchor
	var scalp := Vector2(idle_anchor[0], idle_anchor[1])
	for key in ["hair_back", "hair_front"]:
		var offset: Array = placement.layers[key].offset
		_hair_anchors.append(scalp - Vector2(offset[0], offset[1]))
	add_child(_stage)
	for prop in [
		[_pine, Rect2(-95, -126, 430, 331), false],
		[_pine, Rect2(954, -102, 430, 331), true],
		[_pine, Rect2(-150, 408, 350, 330), false],
		[_pine, Rect2(1090, 383, 350, 330), true],
		[_rocks, Rect2(1010, 506, 231, 121), true],
	]:
		var sprite := Sprite2D.new()
		sprite.texture = prop[0]
		var box: Rect2 = prop[1]
		sprite.position = box.get_center()
		sprite.scale = box.size / sprite.texture.get_size()
		sprite.flip_h = prop[2]
		_stage.add_child(sprite)
	var shadow := _make_shadow()
	for fighter: Fighter in [_player, _opponent]:
		var sprite := Sprite2D.new()
		sprite.texture = shadow
		_stage.add_child(sprite)
		_shadows.append(sprite)
		_stage.add_child(fighter)
	_stage.move_child(_opponent, _player.get_index())
	_opponent.modulate = Color("#c0c8cb")
	_effect.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	_stage.add_child(_effect)
	_build_controls()
	resized.connect(_fit_stage)
	_fit_stage()
	_show("idle")
	var arguments := OS.get_cmdline_user_args()
	for argument in arguments:
		if argument.begins_with("--pose=") and LABELS.has(argument.substr(7)):
			_show(argument.substr(7))
		elif argument == "--closeup":
			_zoom = true
			_show(_fixed_pose)
	for argument in arguments:
		if argument.begins_with("--capture="):
			_capture(argument.substr(10))
		elif argument == "--verify":
			_verify_playback()

func _texture(path: String) -> Texture2D:
	if ResourceLoader.exists(path, "Texture2D"):
		return load(path) as Texture2D
	var image := Image.load_from_file(ProjectSettings.globalize_path(path))
	if image == null or image.is_empty():
		return null
	image.generate_mipmaps()
	return ImageTexture.create_from_image(image)

func _fit_stage() -> void:
	var ratio := minf(size.x / 1280.0, size.y / 720.0)
	_stage.scale = Vector2.ONE * ratio
	_stage.position = (size - Vector2(1280, 720) * ratio) * 0.5
	queue_redraw()

func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color("#252d2b"))
	if _ground:
		draw_texture_rect(_ground, Rect2(Vector2.ZERO, size), false, Color("#b9c0b5"))
	draw_rect(Rect2(0, 0, size.x, 77), Color(0.07, 0.10, 0.09, 0.91))
	draw_rect(Rect2(0, size.y - 77, size.x, 77), Color(0.07, 0.10, 0.09, 0.95))

func _make_shadow() -> Texture2D:
	var image := Image.create_empty(100, 30, false, Image.FORMAT_RGBA8)
	for y in range(image.get_height()):
		for x in range(image.get_width()):
			var distance := Vector2((x - 49.5) / 49.5, (y - 14.5) / 14.5).length()
			image.set_pixel(x, y, Color(0.10, 0.13, 0.11, 0.48 * pow(maxf(0, 1.0 - distance), 1.5)))
	return ImageTexture.create_from_image(image)

func _build_controls() -> void:
	var theme_override := Theme.new()
	theme_override.default_font = load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf")
	theme_override.default_font_size = 19
	for state in ["normal", "hover", "pressed", "focus"]:
		var style := StyleBoxFlat.new()
		style.bg_color = Color("#344440") if state == "normal" else Color("#486559")
		style.border_color = Color("#70867b")
		style.set_border_width_all(1)
		style.set_corner_radius_all(4)
		style.content_margin_left = 13
		style.content_margin_right = 13
		style.content_margin_top = 8
		style.content_margin_bottom = 8
		theme_override.set_stylebox(state, "Button", style)
	theme = theme_override
	var overlay := CanvasLayer.new()
	add_child(overlay)
	var ui := Control.new()
	ui.theme = theme_override
	ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	overlay.add_child(ui)
	ui.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var title := Label.new()
	title.text = "玉虚内门常服"
	title.add_theme_font_size_override("font_size", 29)
	title.modulate = Color("#f3ebda")
	ui.add_child(title)
	title.position = Vector2(28, 12)
	_caption.modulate = Color("#b8c8be")
	ui.add_child(_caption)
	_caption.position = Vector2(31, 46)
	if ResourceLoader.exists("res://tscn/scene/StartMenu.tscn", "PackedScene"):
		var back := Button.new()
		back.name = "ReturnToTitle"
		back.text = "返回首页"
		ui.add_child(back)
		back.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
		back.offset_left = -138
		back.offset_right = -24
		back.offset_top = 17
		back.pressed.connect(func(): get_tree().change_scene_to_file("res://tscn/scene/StartMenu.tscn"))
	var footer := HBoxContainer.new()
	footer.alignment = BoxContainer.ALIGNMENT_CENTER
	footer.add_theme_constant_override("separation", 7)
	ui.add_child(footer)
	footer.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	footer.offset_top = -60
	footer.offset_bottom = -16
	_play_button.text = "播放对练"
	_play_button.pressed.connect(_toggle_play)
	footer.add_child(_play_button)
	for key in LABELS:
		var button := Button.new()
		button.text = LABELS[key]
		button.pressed.connect(_show.bind(key))
		footer.add_child(button)
	var zoom := Button.new()
	zoom.text = "近看"
	zoom.toggle_mode = true
	zoom.toggled.connect(func(value: bool):
		_zoom = value
		if not _playing:
			_show(_fixed_pose))
	footer.add_child(zoom)

func _show(key: String) -> void:
	_playing = false
	set_process(false)
	_play_button.text = "播放对练"
	_fixed_pose = key
	_caption.text = "林间石坪 · " + LABELS[key]
	_effect.hide()
	_display(key, "idle", HOME, OPPONENT)

func _toggle_play() -> void:
	if _playing:
		_show(_fixed_pose)
		return
	_playing = true
	_time = 0.0
	_step = 0
	_play_button.text = "停止对练"
	set_process(true)

func _process(delta: float) -> void:
	_time += delta
	var beat: Array = SEQUENCE[_step]
	if _time >= float(beat[2]):
		_time = 0
		_step = (_step + 1) % SEQUENCE.size()
		beat = SEQUENCE[_step]
	var progress := smoothstep(0.0, float(beat[2]), _time)
	var previous: Array = SEQUENCE[posmod(_step - 1, SEQUENCE.size())]
	var spacing := 0.84 if _zoom else 1.0
	var left := HOME + Vector2(lerpf(previous[3], beat[3], progress) * spacing, 0)
	var right := OPPONENT + Vector2(lerpf(previous[4], beat[4], progress) * spacing, 0)
	_display(beat[0], beat[1], left, right)
	_caption.text = "林间石坪 · " + LABELS[beat[0]] + " / " + LABELS[beat[1]]
	_effect.hide()
	if _step in [2, 5, 7] and _time > 0.11 and _time < 0.36:
		var attacker := _opponent if _step == 5 else _player
		_effect.texture = _palm_effect if _step == 2 else _sword_effect
		if _effect.texture:
			var width := (66.0 if _step == 2 else 116.0) * (1.6 if _zoom else 1.0)
			var ratio := width / _effect.texture.get_width()
			_effect.scale = Vector2(-ratio if _step == 5 else ratio, ratio)
			var frame: Dictionary = _frames[beat[1] if _step == 5 else beat[0]]
			var marker: Array = frame.contact
			var ground: Array = frame.ground_anchor
			_effect.position = attacker.position + Vector2(marker[0] - ground[0], marker[1] - ground[1]) * attacker.scale
			_effect.modulate.a = sin(PI * (_time - 0.11) / 0.25) * 0.87
			_effect.show()

func _display(left: String, right: String, left_feet: Vector2, right_feet: Vector2) -> void:
	var scale_factor := (330.0 if _zoom else 160.0) / STANDING_HEIGHT
	_player.position = left_feet + (Vector2(-19, 80) if _zoom else Vector2.ZERO)
	_opponent.position = right_feet + (Vector2(30, 67) if _zoom else Vector2.ZERO)
	_player.scale = Vector2.ONE * scale_factor
	_opponent.scale = Vector2(-scale_factor, scale_factor)
	_player.pose(_frames[left], _rear, _front, _hair_anchors)
	_opponent.pose(_frames[right], _rear, _front, _hair_anchors)
	for i in range(_shadows.size()):
		var feet := _player.position if i == 0 else _opponent.position
		_shadows[i].position = feet
		_shadows[i].scale = Vector2.ONE * (1.22 if _zoom else 0.67)

func _capture(path: String) -> void:
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(path)
	get_tree().quit()

func _verify_playback() -> void:
	for key in LABELS:
		_show(key)
		for layer in _player.layers:
			assert(layer.texture != null)
		assert(_player.position == HOME)
	_toggle_play()
	for index in range(SEQUENCE.size()):
		_step = index
		_time = 0.18
		_process(0.0)
		assert(_effect.visible == (index in [2, 5, 7]))
	print("Yuxu review: 8 poses and 9 rehearsal beats verified")
	get_tree().quit()

func _fail(reason: String) -> void:
	push_error(reason)
	var error := Label.new()
	error.text = reason
	add_child(error)
