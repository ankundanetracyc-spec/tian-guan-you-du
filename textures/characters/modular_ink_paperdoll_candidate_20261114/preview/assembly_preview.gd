extends Node2D

const PART_ROOT := "res://textures/characters/modular_ink_paperdoll_candidate_20261114/parts/"

const LAYERS := [
	{"file":"hair_back_01.png", "position":Vector2(0, 0), "scale":Vector2(1.0, 1.0), "z":0},
	{"file":"lower_legs_01.png", "position":Vector2(276, 1138), "scale":Vector2(0.4, 0.4), "z":1},
	{"file":"sleeve_far_01.png", "position":Vector2(523, 547), "scale":Vector2(0.43, 0.43), "z":2},
	{"file":"robe_core_01.png", "position":Vector2(170, 365), "scale":Vector2(0.71, 0.71), "z":3},
	{"file":"head_base_01.png", "position":Vector2(285, 72), "scale":Vector2(0.65, 0.65), "z":4},
	{"file":"face_marks_01.png", "position":Vector2(68, 46), "scale":Vector2(0.9, 0.9), "z":5},
	{"file":"eyes_01.png", "position":Vector2(92, 60), "scale":Vector2(0.85, 0.85), "z":6},
	{"file":"sleeve_near_01.png", "position":Vector2(126, 482), "scale":Vector2(0.47, 0.47), "z":7},
	{"file":"hand_far_01.png", "position":Vector2(85, 95), "scale":Vector2(0.9, 0.9), "z":8},
	{"file":"hand_near_01.png", "position":Vector2(48, 49), "scale":Vector2(0.9, 0.9), "z":9},
	{"file":"hair_front_01.png", "position":Vector2(-72, -75), "scale":Vector2(1.1, 1.1), "z":10}
]

func _ready() -> void:
	for spec in LAYERS:
		var sprite := Sprite2D.new()
		sprite.texture = load(PART_ROOT + spec.file)
		sprite.centered = false
		sprite.position = spec.position
		sprite.scale = spec.scale
		sprite.z_index = spec.z
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		add_child(sprite)
	var camera := Camera2D.new()
	camera.position = Vector2(532, 739)
	camera.zoom = Vector2(0.45, 0.45)
	add_child(camera)
	camera.make_current()

func _draw() -> void:
	draw_rect(Rect2(Vector2(-900, -80), Vector2(2864, 1640)), Color("#29343a"), true)
	draw_line(Vector2(360, 1464), Vector2(795, 1464), Color("#89918d"), 2.0)
