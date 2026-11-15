extends Node2D

# Static drawing commands are cached by Godot. All vegetation is outside the
# existing trial boundary; it introduces no obstacles or new collision rules.
var rng := RandomNumberGenerator.new()
const SOIL := Color("d8d2b9")
const INK := Color("4a5946")


func _draw() -> void:
 rng.seed = 20261104
 draw_rect(Rect2(0, 0, 1280, 720), Color("a8b09a"))
 var clearing := PackedVector2Array([Vector2(40, 170), Vector2(130, 140), Vector2(325, 162), Vector2(470, 141), Vector2(690, 161), Vector2(900, 143), Vector2(1150, 157), Vector2(1240, 184), Vector2(1250, 370), Vector2(1238, 550), Vector2(1245, 692), Vector2(1030, 703), Vector2(830, 687), Vector2(620, 699), Vector2(400, 686), Vector2(220, 701), Vector2(39, 688), Vector2(27, 490), Vector2(42, 330)])
 draw_colored_polygon(clearing, SOIL)
 # Faint soil wash, pebbles and sparse dry grass avoid a uniform empty arena.
 for index in range(75):
  var at := Vector2(rng.randf_range(70, 1210), rng.randf_range(175, 675))
  _ellipse(at, Vector2(rng.randf_range(18, 72), rng.randf_range(8, 25)), Color("c7c6ac", 0.08))
 for index in range(1250):
  var at := Vector2(rng.randf_range(50, 1230), rng.randf_range(160, 680))
  draw_line(at, at + Vector2(rng.randf_range(1, 4), rng.randf_range(-1, 1)), Color("777e62", rng.randf_range(0.04, 0.11)), 0.7, true)
 for index in range(70):
  var at := Vector2(rng.randf_range(74, 1205), rng.randf_range(182, 655))
  if index % 3 == 0:
   _ellipse(at, Vector2(2.4, 1.6), Color("a6aa8d", 0.7))
   draw_line(at - Vector2(1, 1), at + Vector2(1, -1), Color("ede5c9", 0.8), 0.8, true)
  else:
   for blade in range(3):
    var angle := -PI * 0.5 + (blade - 1) * 0.5
    draw_line(at, at + Vector2.from_angle(angle) * rng.randf_range(3, 6), Color("83906b", 0.35), 0.9, true)
 # The old clamp remains x=50..1230, y=160..680. Mark that edge gently.
 for side in [0, 1]:
  var edge_y := 160.0 if side == 0 else 680.0
  for index in range(60):
   var x := 50.0 + index * 20.0
   draw_line(Vector2(x, edge_y), Vector2(x + 7, edge_y), Color(INK, 0.13), 0.8, true)
 for index in range(22):
  var x := index * 61.0 + rng.randf_range(-16, 16)
  _tree(Vector2(x, rng.randf_range(28, 100)), rng.randf_range(0.68, 1.2))
 for index in range(18):
  _tree(Vector2(index * 77.0 + rng.randf_range(-14, 14), rng.randf_range(-9, 27)), rng.randf_range(0.85, 1.25))
 for index in range(14):
  _shrub(Vector2(index * 99.0, rng.randf_range(711, 748)), rng.randf_range(0.7, 1.3))
 for index in range(8):
  _shrub(Vector2(rng.randf_range(-19, 3), 202.0 + index * 65), rng.randf_range(0.65, 1.0))
  _shrub(Vector2(rng.randf_range(1278, 1295), 181.0 + index * 73), rng.randf_range(0.65, 1.0))
 _stones(Vector2(34, 176))
 _stones(Vector2(1247, 668))


func _ellipse(at: Vector2, radius: Vector2, tint: Color) -> void:
 var points := PackedVector2Array()
 for index in range(24):
  points.append(at + Vector2(cos(index * TAU / 24.0), sin(index * TAU / 24.0)) * radius)
 draw_colored_polygon(points, tint)


func _tree(at: Vector2, scale_factor: float) -> void:
 _ellipse(at + Vector2(7, 8), Vector2(43, 34) * scale_factor, Color("455a44", 0.12))
 var crown := PackedVector2Array()
 var sides := 32
 for index in range(sides):
  var angle := index * TAU / sides
  var radius := scale_factor * (34.0 + (8.0 if index % 2 == 0 else 0.0) + rng.randf_range(-3, 3))
  crown.append(at + Vector2.from_angle(angle) * radius)
 draw_colored_polygon(crown, Color("6b8167"))
 crown.append(crown[0])
 draw_polyline(crown, Color("4d674f", 0.7), 1.0, true)
 for branch in range(9):
  var angle := branch * TAU / 9.0 + 0.2
  var tip := at + Vector2.from_angle(angle) * 32.0 * scale_factor
  draw_line(at, tip, Color("526d52", 0.6), 1.3, true)
  for needles in range(4):
   var p := at.lerp(tip, 0.32 + needles * 0.17)
   for side in [-1.0, 1.0]:
    draw_line(p, p + Vector2.from_angle(angle + side * 1.0) * (9.0 - needles) * scale_factor, Color("9ca887", 0.6), 1.1, true)
 _ellipse(at - Vector2(4, 5) * scale_factor, Vector2(12, 10) * scale_factor, Color("97a383", 0.55))


func _shrub(at: Vector2, scale_factor: float) -> void:
 for index in range(7):
  var offset := Vector2.from_angle(index * TAU / 7.0) * 20.0 * scale_factor
  _ellipse(at + offset, Vector2(18, 13) * scale_factor, Color("7d9274"))
  draw_arc(at + offset, 11.0 * scale_factor, 3.3, 5.7, 12, Color("acb499", 0.65), 0.9, true)


func _stones(at: Vector2) -> void:
 for index in range(4):
  var origin := at + Vector2(rng.randf_range(-18, 18), rng.randf_range(-12, 12))
  var radius := rng.randf_range(5, 12)
  var points := PackedVector2Array()
  for vertex in range(6):
   points.append(origin + Vector2.from_angle(vertex * TAU / 6) * radius * rng.randf_range(0.8, 1.1))
  draw_colored_polygon(points, Color("a3aa95"))
  draw_polyline(PackedVector2Array([points[3], points[4], points[5], points[0]]), Color("d9d8bd"), 1.0, true)
