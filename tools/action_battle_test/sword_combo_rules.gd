extends RefCounted
## Sole numerical source for the independent sword-combo trial.
const SKILL := "剑式"
const SKILL_ID := "basic_sword_combo"
const KEYWORDS := ["剑"]
const ATTACK_STYLE := "melee"
const CONNECT_WINDOW := 0.30
const ATTACK_RANGE = preload("attack_range_rules.gd")
const CAPSULE_RADIUS := 10.0 * ATTACK_RANGE.MULTIPLIER
const STAGES := [
 {"name":"斜斩","share":0.40,"duration":0.40,"hit":0.40,"shape":"sector","angle":80.0,"reach_base":80.0 * ATTACK_RANGE.MULTIPLIER,"reach_scale":0.50 * ATTACK_RANGE.MULTIPLIER,"reach_cap":110.0 * ATTACK_RANGE.MULTIPLIER,"step":0.0,"life":0.30,"reveal":0.09,"stroke":8.0},
 {"name":"回斩","share":0.45,"duration":0.44,"hit":0.40,"shape":"sector","angle":120.0,"reach_base":85.0 * ATTACK_RANGE.MULTIPLIER,"reach_scale":0.50 * ATTACK_RANGE.MULTIPLIER,"reach_cap":115.0 * ATTACK_RANGE.MULTIPLIER,"step":6.0,"life":0.34,"reveal":0.11,"stroke":11.0},
 {"name":"直刺","share":0.60,"duration":0.52,"hit":0.45,"shape":"capsule","angle":0.0,"reach_base":120.0 * ATTACK_RANGE.MULTIPLIER,"reach_scale":0.50 * ATTACK_RANGE.MULTIPLIER,"reach_cap":145.0 * ATTACK_RANGE.MULTIPLIER,"step":14.0,"life":0.26,"reveal":0.07,"stroke":10.0},
 {"name":"横扫","share":0.80,"duration":0.74,"hit":0.45,"shape":"sector","angle":170.0,"reach_base":100.0 * ATTACK_RANGE.MULTIPLIER,"reach_scale":0.60 * ATTACK_RANGE.MULTIPLIER,"reach_cap":135.0 * ATTACK_RANGE.MULTIPLIER,"step":0.0,"life":0.44,"reveal":0.14,"stroke":17.0}
]

static func tempo(p: float, s: float, j: float) -> float:
 return maxf(0.65,1.0/(1.0+0.02*maxf(j,0.0)+0.002*minf(maxf(p,0.0),maxf(s,0.0))))

static func cooldown(p: float, j: float) -> float:
 return maxf(0.45,1.0/(1.0+0.035*maxf(j,0.0)+0.002*maxf(p,0.0)))

static func preview(index: int, p: float, s: float, j: float, q := -1.0) -> Dictionary:
 var rule: Dictionary = STAGES[index]
 var base := roundi(6.0+0.40*maxf(p,0.0)+0.60*maxf(j,0.0))
 var result := rule.duplicate(true)
 result.damage = roundi(base*float(rule.share))
 result.reach = minf(float(rule.reach_cap),float(rule.reach_base)+float(rule.reach_scale)*maxf(p,0.0))
 result.duration = float(rule.duration)*(tempo(p,s,j) if q<0.0 else q)
 result.index = index
 result.base = base
 result.skill_name = SKILL
 result.skill_id = SKILL_ID
 result.keywords = KEYWORDS.duplicate()
 result.attack_style = ATTACK_STYLE
 return result

static func contains(release: Dictionary, point: Vector2) -> bool:
 var direction: Vector2 = release.direction
 var relative: Vector2 = point-Vector2(release.origin)
 var along := relative.dot(direction)
 var across := relative.dot(direction.orthogonal())
 var reach: float = release.reach
 if str(release.shape)=="capsule":
  return Vector2(along-clampf(along,CAPSULE_RADIUS,reach-CAPSULE_RADIUS),across).length()<=CAPSULE_RADIUS+0.0001
 if relative.length()>reach+0.0001: return false
 if relative.length_squared()<0.000001: return true
 return absf(atan2(across,along))<=deg_to_rad(float(release.angle)*0.5)+0.000001

static func outline(release: Dictionary, samples := 64) -> PackedVector2Array:
 var shape := PackedVector2Array()
 var origin: Vector2 = release.origin
 var d: Vector2 = release.direction
 if str(release.shape)=="capsule":
  for step in range(samples+1):
   var t := -PI/2.0+PI*step/samples
   shape.append(origin+d*(float(release.reach)-CAPSULE_RADIUS)+Vector2.from_angle(d.angle()+t)*CAPSULE_RADIUS)
  for step in range(samples+1):
   var t := PI/2.0+PI*step/samples
   shape.append(origin+d*CAPSULE_RADIUS+Vector2.from_angle(d.angle()+t)*CAPSULE_RADIUS)
 else:
  shape.append(origin)
  var half := deg_to_rad(float(release.angle)*0.5)
  for step in range(samples+1): shape.append(origin+Vector2.from_angle(d.angle()-half+2.0*half*step/samples)*float(release.reach))
  shape.append(origin)
 return shape
