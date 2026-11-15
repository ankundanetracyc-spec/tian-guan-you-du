extends Node2D

# Pixel coordinates in the original transparent PNGs. Feet and pelvis set
# the origin, so long hair and the sword do not shift the character's footing.
const ART := "res://textures/characters/yuxu_neimen_sprite_redo_candidate_20261114/"
const POSES := {
	"idle": ["01_idle.png", Vector2(655, 1138), Vector2(655, 495), Vector2(704, 280), Vector2(541, 743), Vector2(574, 434), Vector2(773, 426)],
	"move": ["02_move_contralateral_candidate.png", Vector2(724, 1090), Vector2(758, 567), Vector2(886, 360), Vector2(969, 662), Vector2(664, 466), Vector2(832, 530)],
	"dodge": ["03_evade.png", Vector2(663, 1118), Vector2(684, 564), Vector2(690, 315), Vector2(1056, 453), Vector2(508, 475), Vector2(811, 444)],
	"guard": ["04_guard.png", Vector2(679, 1138), Vector2(681, 547), Vector2(698, 307), Vector2(900, 465), Vector2(514, 477), Vector2(798, 450)],
	"hurt": ["05_hit.png", Vector2(633, 1266), Vector2(545, 494), Vector2(438, 265), Vector2(910, 364), Vector2(356, 461), Vector2(642, 388)],
	"stagger": ["06_stagger.png", Vector2(591, 1246), Vector2(631, 556), Vector2(705, 394), Vector2(739, 887), Vector2(502, 499), Vector2(746, 516)],
	"attack": ["attack_palm_side_candidate.png", Vector2(621, 1118), Vector2(665, 508), Vector2(731, 316), Vector2(985, 436), Vector2(611, 458), Vector2(729, 441)],
	"sword_finish": ["attack_sword_side_candidate.png", Vector2(520, 981), Vector2(583, 473), Vector2(670, 277), Vector2(950, 431), Vector2(550, 408), Vector2(687, 399)]
}

var _sprite: Sprite2D
var _textures: Dictionary = {}
var _pose := ""
var _traveling := false
var _paused := false
var _facing_left := false
var _display_scale := 1.0
var _visible_height := 0.0

func configure(height: float) -> bool:
	for key in POSES:
		var path: String = ART + POSES[key][0]
		if not ResourceLoader.exists(path, "Texture2D"):
			return false
		var texture := load(path) as Texture2D
		if texture == null:
			return false
		_textures[key] = texture
	_visible_height = height
	_display_scale = height / 1066.0
	if _sprite == null:
		_sprite = Sprite2D.new()
		_sprite.name = "YuxuBattlePose"
		_sprite.centered = false
		_sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		add_child(_sprite)
	_pose = ""
	_show_pose("idle")
	set_facing(_facing_left)
	return true

func set_facing(left: bool) -> void:
	_facing_left = left
	scale = Vector2(-_display_scale if left else _display_scale, _display_scale)

func set_paused(value: bool) -> void:
	_paused = value

func set_motion(_velocity: Vector2) -> void:
	pass

func set_travel_mode(mode: String) -> void:
	_traveling = mode in ["approach", "retreat"]

func set_travel_progress(_progress: float) -> void:
	pass

func update_visual(_delta: float, action: String, elapsed: float, duration: float) -> void:
	if _paused:
		return
	if action == "attack" or action == "sword_finish":
		if elapsed >= 0.08 and elapsed < maxf(0.36, duration - 0.10):
			_show_pose(action)
			return
	elif action in ["dodge", "guard", "hurt", "stagger"]:
		_show_pose(action)
		return
	_show_pose("move" if _traveling else "idle")

func _show_pose(pose: String) -> void:
	if _pose == pose:
		return
	_pose = pose
	_sprite.texture = _textures[pose]
	_sprite.position = -(POSES[pose][1] as Vector2)

func get_local_visible_bounds() -> Rect2:
	return Rect2(Vector2(-295, -1066) * _display_scale, Vector2(546, 1066) * _display_scale)

func get_anchor(which: String) -> Vector2:
	if which == "feet":
		return Vector2.ZERO
	var pose := _pose if POSES.has(_pose) else "idle"
	if which == "contact":
		pose = "attack"
	elif which == "sword_grip":
		pose = "sword_finish"
	var frame: Array = POSES[pose]
	var index := 2
	match which:
		"head": index = 3
		"hand", "contact", "sword_grip": index = 4
		"shoulder_near": index = 5
		"shoulder_far": index = 6
	return ((frame[index] as Vector2) - (frame[1] as Vector2)) * _display_scale

func get_debug_state() -> Dictionary:
	return {"ready": _sprite != null, "gender": 0, "outfit_id": "turn_basic_male_20261106",
		"source": "yuxu_battle_key_poses", "pose": _pose, "facing_left": _facing_left}
