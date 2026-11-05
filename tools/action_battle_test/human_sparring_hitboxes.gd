extends Node2D

## Read-only ground projection and horizontal attack plane from live geometry.
const SPACE = preload("human_contact_space.gd")
var arena: Node2D

func draw_sector(origin: Vector2, direction: Vector2, reach: float, tint: Color, filled: bool) -> void:
 var points := PackedVector2Array([origin])
 for index in range(49):
  points.append(origin + Vector2.from_angle(direction.angle() - PI * 0.5 + index * PI / 48.0) * reach)
 if filled: draw_colored_polygon(points, Color(tint, 0.16))
 points.append(origin)
 draw_polyline(points, Color(tint, 0.90), 1.5, true)

func _draw() -> void:
 if arena == null or not arena.show_hitboxes: return
 var source: Node2D = arena
 var origin: Vector2 = source.player
 var direction: Vector2 = source.attack_aim_direction()
 var reach := 0.0
 var height: float = source.attack_plane_height()
 if source.pending_attack == "拳掌" and source.fist_combo != null:
  reach = source.fist_combo.reach
 elif source.pending_attack == "痛击":
  reach = float(source.SKILL_RULES["痛击"].reach)
  direction = source.attack_direction
 elif not source.strike_fx.is_empty():
  var released: Dictionary = source.strike_fx.back()
  origin = released.origin
  direction = released.dir
  reach = released.reach
  height = float(released.get("attack_height", height))
 if reach > 0.0:
  draw_sector(origin, direction, reach, Color("59a5d7"), false)
  var hand := origin - Vector2(0, height)
  draw_sector(hand, direction, reach, Color("e2b76a"), true)
  draw_dashed_line(origin, hand, Color("a7844d"), 1.0, 4.0)
  draw_circle(hand, 3.0, Color("e0b867"))
 for fighter in [arena] + arena.npcs:
  var point: Vector2 = fighter.player
  var included: bool = fighter != arena and reach > 0.0 and arena.contact_contains(origin, direction, reach, point) and SPACE.intersects_height(fighter, height)
  var tint := Color("64cfa0") if included else Color("ed7c68")
  if fighter == arena: tint = Color("78bfe2")
  draw_line(point - Vector2(5, 0), point + Vector2(5, 0), tint, 2.0, true)
  draw_line(point - Vector2(0, 5), point + Vector2(0, 5), tint, 2.0, true)
  draw_circle(point, 2.0, tint)
  var bottom: Vector2 = point - Vector2(20, float(fighter.elevation))
  var top: Vector2 = bottom - Vector2(0, SPACE.body_height(fighter))
  draw_line(bottom, top, Color(tint, 0.72), 1.2, true)
  draw_line(top - Vector2(4, 0), top + Vector2(4, 0), tint, 1.2, true)
  draw_line(bottom - Vector2(4, 0), bottom + Vector2(4, 0), tint, 1.2, true)
  if reach > 0.0 and fighter != arena:
   var contact := point - Vector2(0, height)
   draw_circle(contact, 3.0, tint)
