extends RefCounted
const RULES = preload("action_manual_rules.gd")

static func sector_contains(relative: Vector2, direction: Vector2, fan: float) -> bool:
 if relative.is_zero_approx(): return true
 return direction.dot(relative.normalized()) >= cos(deg_to_rad(fan * 0.5)) - 0.000001

static func blood_contains(relative: Vector2, direction: Vector2, reach: float, extension: float) -> bool:
 var forward := relative.dot(direction)
 var sideways := relative.cross(direction)
 if forward <= 0.0 or absf(sideways) > reach: return false
 # Extrude only forward: lateral width remains the original attack's width.
 return forward <= sqrt(maxf(0.0, reach * reach - sideways * sideways)) + extension + 0.000001

static func _gap(t: float, from: Vector2, to: Vector2, age: float, seconds: float) -> float:
 return from.lerp(to, t).length() - RULES.wave_radius(age + t * seconds)

static func _root(lo: float, hi: float, from: Vector2, to: Vector2, age: float, seconds: float) -> float:
 var negative_at_lo := _gap(lo, from, to, age, seconds) <= 0.0
 for iteration in range(32):
  var middle := (lo + hi) * 0.5
  if (_gap(middle, from, to, age, seconds) <= 0.0) == negative_at_lo: lo = middle
  else: hi = middle
 return (lo + hi) * 0.5

static func first_contact(from: Vector2, to: Vector2, direction: Vector2, fan: float, age: float, seconds: float, first_step: bool) -> float:
 if first_step and from.length() <= RULES.wave_radius(age) and sector_contains(from, direction, fan): return 0.0
 if seconds <= 0.0: return -1.0
 var first := _gap(0.0, from, to, age, seconds)
 var last := _gap(1.0, from, to, age, seconds)
 var candidates: Array[float] = []
 if absf(first) <= 0.000001: candidates.append(0.0)
 if first * last < 0.0:
  candidates.append(_root(0.0, 1.0, from, to, age, seconds))
 elif first > 0.0 and last > 0.0:
  # Distance to a linearly moving point minus this concave radius is convex.
  # Its minimum reveals two crossings even when both frame endpoints miss.
  var lo := 0.0
  var hi := 1.0
  for iteration in range(32):
   var left := (2.0 * lo + hi) / 3.0
   var right := (lo + 2.0 * hi) / 3.0
   if _gap(left, from, to, age, seconds) <= _gap(right, from, to, age, seconds): hi = right
   else: lo = left
  var minimum := (lo + hi) * 0.5
  if _gap(minimum, from, to, age, seconds) <= 0.000001:
   candidates.append(_root(0.0, minimum, from, to, age, seconds))
   candidates.append(_root(minimum, 1.0, from, to, age, seconds))
 if absf(last) <= 0.000001: candidates.append(1.0)
 for candidate in candidates:
  if sector_contains(from.lerp(to, candidate), direction, fan): return candidate
 return -1.0
