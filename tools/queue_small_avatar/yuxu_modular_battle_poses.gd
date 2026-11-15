extends "res://tools/queue_small_avatar/yuxu_battle_key_poses.gd"

const PARTS := "res://textures/characters/yuxu_modular_ink_20261114/"
const HeadLayerShader := preload("res://tools/queue_small_avatar/yuxu_head_layers.gdshader")
const HAIRSTYLES := {
	"ribbon": ["head_ribbon.png", Vector2(518, 616), Vector4(483, 555, 569, 625)],
	"pin": ["head_pin.png", Vector2(651, 616), Vector4(614, 555, 698, 625)],
}

# Body texture, ground origin, neck socket, scale, head tilt, chest, cast/strike hand.
const BODY := {
	"idle": ["idle.png", Vector2(310, 574), Vector2(331, 143), 1.0, 0.0, Vector2(324, 180), Vector2(424, 313)],
	"move": ["move.png", Vector2(296, 572), Vector2(342, 166), 1.0, 0.11, Vector2(328, 213), Vector2(471, 212)],
	"dodge": ["dodge.png", Vector2(275, 545), Vector2(326, 177), 1.0, 0.15, Vector2(302, 223), Vector2(511, 419)],
	"guard": ["guard.png", Vector2(302, 560), Vector2(317, 127), 1.0, 0.0, Vector2(322, 175), Vector2(496, 119)],
	"hurt": ["hurt.png", Vector2(400, 477), Vector2(344, 100), 1.1, -0.24, Vector2(375, 129), Vector2(533, 99)],
	"stagger": ["stagger.png", Vector2(341, 478), Vector2(420, 129), 1.1, 0.23, Vector2(408, 167), Vector2(453, 314)],
	"attack": ["palm.png", Vector2(354, 468), Vector2(462, 120), 1.1, 0.08, Vector2(447, 159), Vector2(629, 100)],
	"sword_finish": ["sword.png", Vector2(276, 468), Vector2(333, 130), 1.1, 0.10, Vector2(326, 173), Vector2(454, 250)],
}

var _face: Sprite2D
var _neck: Sprite2D
var _hair_back: Sprite2D
var _hair_front: Sprite2D
var _hair_textures := {}
var _hair_style := "ribbon"

func configure(height: float) -> bool:
	for key in BODY:
		var texture := _load_part(BODY[key][0])
		if texture == null:
			return false
		_textures[key] = texture
	for key in HAIRSTYLES:
		var texture := _load_part(HAIRSTYLES[key][0])
		if texture == null:
			return false
		_hair_textures[key] = texture
	_display_scale = height / 660.0
	if _face == null:
		_hair_back = _make_head_layer(2)
		_neck = _make_head_layer(0)
		_sprite = Sprite2D.new()
		_sprite.centered = false
		_sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		add_child(_sprite)
		_face = _make_head_layer(1)
		_hair_front = _make_head_layer(3)
	_pose = ""
	_show_pose("idle")
	set_facing(_facing_left)
	return true

func _load_part(file: String) -> Texture2D:
	var path := PARTS + file
	if ResourceLoader.exists(path, "Texture2D"):
		return load(path) as Texture2D
	return null

func _make_head_layer(layer_index: int) -> Sprite2D:
	var layer := Sprite2D.new()
	layer.centered = false
	layer.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	var mask := ShaderMaterial.new()
	mask.shader = HeadLayerShader
	mask.set_shader_parameter("layer", layer_index)
	layer.material = mask
	add_child(layer)
	return layer

func set_hair_style(style: String) -> bool:
	if not _hair_textures.has(style):
		return false
	_hair_style = style
	_apply_hair()
	return true

func show_pose_for_review(pose: String) -> void:
	if BODY.has(pose):
		_show_pose(pose)

func _show_pose(pose: String) -> void:
	if _pose == pose:
		return
	_pose = pose
	var frame: Array = BODY[pose]
	var body_scale: float = frame[3]
	_sprite.texture = _textures[pose]
	_sprite.scale = Vector2.ONE * body_scale
	_sprite.position = -(frame[1] as Vector2) * body_scale
	_apply_head_pose(frame)

func _apply_head_pose(frame: Array) -> void:
	for layer: Sprite2D in [_hair_back, _neck, _face, _hair_front]:
		layer.position = _body_point(frame, 2)
		layer.scale = Vector2.ONE * 0.42
		layer.rotation = frame[4]
	_set_layer_source(_neck, "ribbon")
	_set_layer_source(_face, "ribbon")
	_apply_hair()

func _apply_hair() -> void:
	_set_layer_source(_hair_back, _hair_style)
	_set_layer_source(_hair_front, _hair_style)

func _set_layer_source(layer: Sprite2D, style: String) -> void:
	var source: Array = HAIRSTYLES[style]
	var anchor: Vector2 = source[1]
	layer.texture = _hair_textures[style]
	layer.offset = -anchor
	var mask := layer.material as ShaderMaterial
	mask.set_shader_parameter("neck_pixels", source[2])
	mask.set_shader_parameter("anchor_x", anchor.x)

func _body_point(frame: Array, index: int) -> Vector2:
	return ((frame[index] as Vector2) - (frame[1] as Vector2)) * float(frame[3])

func get_local_visible_bounds() -> Rect2:
	return Rect2(Vector2(-220, -680) * _display_scale, Vector2(390, 680) * _display_scale)

func get_anchor(which: String) -> Vector2:
	var frame: Array = BODY["attack" if which == "contact" else "sword_finish" if which == "sword_grip" else _pose]
	var chest := _body_point(frame, 5)
	var neck := _body_point(frame, 2)
	var point := chest
	match which:
		"feet": point = Vector2.ZERO
		"head": point = neck + Vector2(22, -145)
		"hand", "contact", "sword_grip": point = _body_point(frame, 6)
		"shoulder_near": point = neck + Vector2(-73, 16)
		"shoulder_far": point = neck + Vector2(47, 10)
	return point * _display_scale

func get_debug_state() -> Dictionary:
	var state := super.get_debug_state()
	state["source"] = "yuxu_modular_battle_poses"
	state["hair_style"] = _hair_style
	return state
