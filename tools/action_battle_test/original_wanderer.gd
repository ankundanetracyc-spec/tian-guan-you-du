extends Node2D
# Original test puppet. Each drawing segment pivots at its anatomical joint.
var parts := {}
var clip := "Idle"
var clock := 0.0
var direction := 1.0
var tint := 0.0

func _ready() -> void:
 var root := "res://tools/action_battle_test/art/original_wanderer/"
 if not FileAccess.file_exists(root + "head.png"):
  root = "res://art/original_wanderer/"
 for name in ["head", "body", "far_arm", "near_arm", "far_upper", "far_lower", "near_upper", "near_lower", "front", "back"]:
  var im := Image.new()
  if im.load(ProjectSettings.globalize_path(root + name + ".png")) == OK:
   parts[name] = ImageTexture.create_from_image(im)

func set_character_pose(action: String, time: float, facing: float, hit_tint: float = 0.0) -> void:
 clip = action
 clock = time
 direction = -1.0 if facing < 0.0 else 1.0
 tint = hit_tint
 scale.x = direction
 queue_redraw()

func _part(name: String, pivot: Vector2, size: Vector2, offset: Vector2, angle: float = 0.0) -> void:
 if not parts.has(name): return
 draw_set_transform(pivot, angle)
 draw_texture_rect(parts[name], Rect2(offset, size), false, Color(1.0, 1.0 - 0.2 * tint, 1.0 - 0.2 * tint))
 draw_set_transform(Vector2.ZERO)

func _leg(side: String, hip: Vector2, thigh: float, knee: float) -> void:
 var length := 18.0
 var knee_pos := hip + Vector2(0, length).rotated(thigh)
 _part(side + "_upper", hip, Vector2(13, 21), Vector2(-7, -2), thigh)
 _part(side + "_lower", knee_pos, Vector2(15, 20), Vector2(-7, -2), thigh + knee)

func _draw() -> void:
 var run := clip == "Run_New1"
 var phase := clock * TAU / 0.53
 var swing := sin(phase) if run else 0.0
 var bounce := absf(sin(phase)) * 1.2 if run else sin(clock * 2.0) * 0.35
 var hip := Vector2(0, -35 - bounce)
 var far_thigh := swing * 0.75
 var near_thigh := -swing * 0.75
 var far_knee := maxf(0.0, -swing) * 1.0
 var near_knee := maxf(0.0, swing) * 1.0
 if clip == "Step1":
  near_thigh = -0.65
  far_thigh = 0.6
  far_knee = 0.8
 if clip == "AttackIdle":
  near_thigh = -0.22
  far_thigh = 0.2
 # Long rear cloth is behind both legs and trails opposite movement.
 var trail := -0.18 - (0.36 + 0.09 * sin(phase - 0.7) if run else 0.0)
 _part("back", hip + Vector2(-5, -5), Vector2(31, 31), Vector2(-29, 0), trail)
 _leg("far", hip + Vector2(-2, 0), far_thigh, far_knee)
 _part("far_arm", hip + Vector2(-2, -31), Vector2(12, 27), Vector2(-7, -2), -swing * 0.45 if run else 0.05)
 _part("body", hip + Vector2(0, -30), Vector2(20, 37), Vector2(-10, -3))
 # The narrow front panel sits behind the nearer leg to keep its silhouette continuous.
 _part("front", hip + Vector2(1, -3), Vector2(12, 27), Vector2(-5, 0), 0.13 + swing * 0.08 if run else 0.04)
 _leg("near", hip + Vector2(2, 0), near_thigh, near_knee)
 _part("head", hip + Vector2(0, -41), Vector2(17, 24), Vector2(-10, -6))
 var arm_angle := swing * 0.45 if run else -0.08
 if clip == "AttackIdle": arm_angle = -1.2 * sin(clampf(clock / 0.18, 0.0, 1.0) * PI)
 if clip == "Step1": arm_angle = -0.5
 _part("near_arm", hip + Vector2(2, -29), Vector2(12, 28), Vector2(-6, -2), arm_angle)

