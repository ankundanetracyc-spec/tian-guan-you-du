extends Node2D

# Native overhead shapes keep all directions and limb animation continuous.
# Local +X is forward for both characters; no side-view sprite is rotated.
var is_wolf := false
var motion := 0.0
var movement := 0.0
var local_velocity := Vector2.ZERO
var attack := 0.0
var guarding := false
var dashing := false
var hurt := false
var defeated := false
const OUTLINE := Color("34403a")


func _shape(points: Array, fill: Color, outline := OUTLINE, thickness := 1.1) -> void:
 # Round the joins slightly so cloth and fur retain organic silhouettes.
 var polygon := PackedVector2Array()
 for index in range(points.size()):
  var current: Vector2 = points[index]
  var before: Vector2 = points[(index - 1 + points.size()) % points.size()]
  var after: Vector2 = points[(index + 1) % points.size()]
  var incoming := current.lerp(before, 0.15)
  var outgoing := current.lerp(after, 0.15)
  for step in range(4):
   var t := step / 3.0
   polygon.append(incoming.lerp(current, t).lerp(current.lerp(outgoing, t), t))
 draw_colored_polygon(polygon, fill)
 polygon.append(polygon[0])
 draw_polyline(polygon, outline, thickness, true)


func _ellipse(at: Vector2, radius: Vector2, tint: Color) -> void:
 var points := PackedVector2Array()
 for index in range(32):
  points.append(at + Vector2(cos(index * TAU / 32.0), sin(index * TAU / 32.0)) * radius)
 draw_colored_polygon(points, tint)


func _draw() -> void:
 modulate = Color(0.66, 0.65, 0.60, 0.62) if defeated else Color.WHITE
 if is_wolf:
  _draw_wolf()
 else:
  _draw_monk()


func _draw_monk() -> void:
 var stride := sin(motion) * movement
 var sway := sin(motion * 0.5) * movement * 1.8
 var cloth_drag: float = clampf(-local_velocity.y * 4.0, -5.0, 5.0) + sway
 var robe := Color("73968a")
 var light := Color("a2b8a2")
 if hurt:
  robe = robe.lerp(Color("c58b72"), 0.65)
 _ellipse(Vector2(-1, 3), Vector2(34, 28), Color("374a3b", 0.12))
 # Feet alternately advance underneath the robe, in the actual facing plane.
 for side in [-1.0, 1.0]:
  var foot := Vector2(-18 + stride * side * 9.0, side * 12)
  _ellipse(foot, Vector2(10, 5), Color("3b443c"))
  draw_line(foot + Vector2(1, -2), foot + Vector2(7, -2), Color("a8ab94"), 1.0, true)
 _shape([Vector2(-30, -15 + cloth_drag), Vector2(-13, -19), Vector2(0, -20), Vector2(13, -11), Vector2(15, 9), Vector2(-5, 19), Vector2(-28, 17 + cloth_drag), Vector2(-23, 2 + cloth_drag)], robe)
 _shape([Vector2(-26, -9 + cloth_drag), Vector2(-11, -14), Vector2(8, -9), Vector2(-12, 1), Vector2(-25, 11 + cloth_drag), Vector2(-20, 1)], light, Color("526e61"), 0.7)
 draw_polyline(PackedVector2Array([Vector2(-27, 12 + cloth_drag), Vector2(-10, 9), Vector2(5, 0)]), Color("c5cbb0"), 1.1, true)
 draw_line(Vector2(-16, -17), Vector2(-16, 17), Color("4b6559"), 4.0, true)
 draw_line(Vector2(-16, -16), Vector2(-16, 16), Color("b4ae82"), 1.1, true)
 # Sleeves hinge at the shoulders. Only the striking hand extends on attack.
 for side in [-1.0, 1.0]:
  var extension := attack * 23.0 if side < 0 else attack * 6.0
  var lift := 8.0 if guarding else 0.0
  var hand := Vector2(22 + extension + lift, side * (18 - attack * 6 - lift * 0.6))
  var elbow := Vector2(5 + extension * 0.4, side * (25 + stride * side * 2))
  _shape([Vector2(-5, side * 15), Vector2(-3, side * 25), elbow + Vector2(-1, side * 5), hand + Vector2(-3, side * 5), hand + Vector2(1, -side * 5), elbow + Vector2(3, -side * 4), Vector2(5, side * 12)], robe)
  draw_line(elbow + Vector2(0, side * 2), hand - Vector2(3, 0), Color("b5c4ab"), 2.0, true)
  draw_line(hand + Vector2(-4, -5), hand + Vector2(-4, 5), Color("e1dbc3"), 3.0, true)
  _ellipse(hand, Vector2(5.5, 4.0), Color("d6bda0"))
  draw_arc(hand, 5.0, -0.8, 0.8, 10, OUTLINE, 0.7, true)
 # Collar and crown are visible from above, with a small nose for direction.
 _shape([Vector2(-2, -13), Vector2(12, -10), Vector2(8, 0), Vector2(12, 10), Vector2(-2, 13), Vector2(1, 0)], Color("e7e3cc"), Color("536858"), 0.8)
 _ellipse(Vector2(10, 1.4), Vector2(12, 11), Color("465046", 0.24))
 _shape([Vector2(16, -6), Vector2(22, -3), Vector2(24, 0), Vector2(21, 3), Vector2(16, 6)], Color("d4bda1"), Color("807b64"), 0.6)
 _ellipse(Vector2(9, 0), Vector2(11.8, 11.5), Color("303c35"))
 draw_arc(Vector2(9, 0), 9.4, 1.9, 4.4, 22, Color("657064"), 1.1, true)
 _ellipse(Vector2(1, 0), Vector2(5.7, 6), Color("26342e"))
 draw_line(Vector2(0, -5), Vector2(2, 5), Color("9a9a79"), 1.7, true)
 draw_polyline(PackedVector2Array([Vector2(-2, 3), Vector2(-16, 7 + sway), Vector2(-27, 6 + sway * 1.4)]), Color("b7b391"), 1.9, true)
 if dashing:
  draw_arc(Vector2.ZERO, 31.0, 1.4, 4.8, 30, Color("b7c5b1", 0.6), 1.1, true)


func _draw_wolf() -> void:
 var stride := sin(motion) * movement
 var stretch := attack * 6.0
 var fur := Color("8e9380")
 var pale := Color("bab9a0")
 _ellipse(Vector2(-4, 3), Vector2(44, 21), Color("374a3b", 0.15))
 var tail_sway := sin(motion * 0.5) * 4
 _shape([Vector2(-23 - stretch, -4), Vector2(-37 - stretch, -1), Vector2(-47 - stretch, 5 + tail_sway), Vector2(-55 - stretch, 3 + tail_sway), Vector2(-48 - stretch, 12 + tail_sway), Vector2(-34 - stretch, 9), Vector2(-24 - stretch, 5)], fur)
 for side in [-1.0, 1.0]:
  for front in [-1.0, 1.0]:
   var gait: float = stride * side * front
   var hip := Vector2(front * 16, side * 12)
   var paw := Vector2(front * 17 + gait * 10 + stretch, side * (22 - attack * 4))
   draw_line(hip, paw, OUTLINE, 7, true)
   draw_line(hip, paw, fur, 5.5, true)
   _ellipse(paw + Vector2(2, 0), Vector2(5, 3.4), pale)
 _shape([Vector2(-31 - stretch, -5), Vector2(-24 - stretch, -13), Vector2(-10, -15), Vector2(8 + stretch, -15), Vector2(19 + stretch, -11), Vector2(23 + stretch, 0), Vector2(18 + stretch, 11), Vector2(7, 15), Vector2(-14, 14), Vector2(-28 - stretch, 10)], fur)
 _shape([Vector2(-27, 0), Vector2(-17, -9), Vector2(-4, -9), Vector2(8, -7), Vector2(17 + stretch, 0), Vector2(6, 6), Vector2(-7, 8), Vector2(-20, 6)], Color("707a68"), Color("7e8773"), 0.5)
 draw_polyline(PackedVector2Array([Vector2(-22, -6), Vector2(-8, -5), Vector2(4, -7), Vector2(14, -4)]), Color("a5a88f"), 1.2, true)
 var head_x := 23.0 + stretch - (3.0 if guarding else 0.0)
 _shape([Vector2(head_x - 8, -9), Vector2(head_x - 14, -17), Vector2(head_x - 2, -13), Vector2(head_x + 3, -6), Vector2(head_x + 13, -4), Vector2(head_x + 18, 0), Vector2(head_x + 12, 5), Vector2(head_x + 3, 7), Vector2(head_x - 2, 13), Vector2(head_x - 14, 17), Vector2(head_x - 8, 7)], pale)
 _shape([Vector2(head_x - 8, -10), Vector2(head_x - 10, -14), Vector2(head_x - 2, -11)], Color("646f5f"), OUTLINE, 0.6)
 _shape([Vector2(head_x - 8, 10), Vector2(head_x - 10, 14), Vector2(head_x - 2, 11)], Color("646f5f"), OUTLINE, 0.6)
 _shape([Vector2(head_x - 5, -6), Vector2(head_x + 3, -5), Vector2(head_x + 11, 0), Vector2(head_x + 3, 5), Vector2(head_x - 5, 6), Vector2(head_x - 1, 0)], fur, Color("939881"), 0.5)
 draw_circle(Vector2(head_x + 4, -5), 1.5, Color("754c35"), true, -1, true)
 draw_circle(Vector2(head_x + 4, 5), 1.5, Color("754c35"), true, -1, true)
 _ellipse(Vector2(head_x + 16, 0), Vector2(3.0, 2.7), OUTLINE)
 for side in [-1.0, 1.0]:
  for index in range(3):
   draw_line(Vector2(-12 + index * 7, side * 9), Vector2(-16 + index * 7, side * 12), Color("acaf95"), 0.8, true)
