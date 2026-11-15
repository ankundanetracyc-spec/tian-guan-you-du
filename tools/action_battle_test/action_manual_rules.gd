extends RefCounted
## Shared combat data for the first migrated manual. Cultivation metadata is separate.
const ATTACK_RANGE = preload("attack_range_rules.gd")

const BOOK_ID := "baihai_yunxi"
const BOOK_NAME := "百骸衍枝化生宝册·云息篇"
const HEART_NAME := "百骸化生"
const SKILL := "赤潮奔涌"
const CHAPTERS := ["养络", "行血", "贯劲", "归养"]
const REWRITE_TARGETS := ["催血", "架招", SKILL, "拳掌"]
const REWRITE_TEXTS := [
 "起手气血≤2层，完成后额外获得气血1层。",
 "起手气血≥4层，本次基础化解量提高20%。",
 "起手气血≥4层，多降低气血1层；本次血浪扇面扩大至180°。",
 "拳掌各段攻击距离提高25%。",
]
const BLOOD_BASE := 0.50
const BLOOD_FOURTH := 0.70
const BLOOD_REACH_BASE := 20.0 * ATTACK_RANGE.MULTIPLIER
const BLOOD_REACH_PER_LAYER := 5.0 * ATTACK_RANGE.MULTIPLIER
const MAIN_REACH_MULTIPLIER := 1.15
const BASIC_REACH_MULTIPLIER := 1.25
const CLOSE_BONUS := 0.25
const NEXT_COMBO_BONUS := 0.40
const SKILL_COST := 1
const SKILL_BLOOD_COST := 2
const BUFF_SECONDS := 6.0
const COOLDOWN := 24.0
const WAVE_REACH := 220.0 * ATTACK_RANGE.MULTIPLIER
const WAVE_START_REACH := 6.0 * ATTACK_RANGE.MULTIPLIER
const WAVE_FAN := 120.0
const WAVE_REWRITE_FAN := 180.0
const WAVE_SECONDS := 0.78
const WAVE_BASE := 0.50
const WAVE_PER_BLOOD := 0.10
const KNOCKBACK_DISTANCE := 24.0
const KNOCKBACK_SECONDS := 0.12
const KNOCKBACK_BASE_CHANCE := 0.70
const KNOCKBACK_ATTRIBUTE := 0.01
const CLOUD_CONTROL_BONUS := 0.10

static func default_side(player_side := true) -> Dictionary:
 return {"book":player_side, "chapter":4, "role":1,
  "bindings":["", "", "", ""],
  "skills":["痛击", "架招", "催血", "聚炁", SKILL if player_side else ""],
  "realm":"云息一层" if player_side else "凡人",
  "custom_attributes":false, "physique":8.0, "soul":8.0, "intelligence":8.0,
  "health":100, "mana":3, "bearing_resistance_pp":0.0}

static func available_skills(config: Dictionary) -> Array[String]:
 var skills: Array[String] = ["痛击", "架招", "催血", "聚炁"]
 if bool(config.get("book", false)) and int(config.get("chapter", 0)) >= 2:
  skills.append(SKILL)
 skills.append("")
 return skills

static func heart_text(chapter: int, main: bool, blood: int, detailed := true) -> String:
 var portion := 70 if chapter >= 4 else 50
 var extension := (BLOOD_REACH_BASE + BLOOD_REACH_PER_LAYER * blood) * (MAIN_REACH_MULTIPLIER if main else 1.0)
 var text := "有气血时，普攻每段外放血气，可与本体同时命中。\n血气造成该段%d%%的物理伤害，向前额外延伸%s单位" % [portion, String.num(extension, 1)]
 if detailed:
  var base_text := String.num(BLOOD_REACH_BASE, 0)
  var layer_text := String.num(BLOOD_REACH_PER_LAYER, 0)
  text += ("（（%s＋%s×%d）×1.15）" % [base_text, layer_text, blood]) if main else ("（%s＋%s×%d）" % [base_text, layer_text, blood])
 text += "。\n外放不消耗气血，血气增伤同样作用于血浪。"
 if chapter >= 2: text += "\n本体命中时，同次血气对该敌人伤害＋25%。"
 if chapter >= 3: text += "\n自身主动招式降血后，下一套血气伤害＋40%，不可叠加。"
 if main: text += "\n主修：血气额外距离提高15%。"
 return text

static func skill_text(blood: int, last_base: int, fan := WAVE_FAN, detailed := true) -> Array[String]:
 var amount := roundi(last_base * (WAVE_BASE + WAVE_PER_BLOOD * blood))
 var damage := "血浪基础物理伤害当前估算%d点" % amount
 if detailed: damage += "（%d×（50%%＋%d×10%%））" % [last_base, blood]
 return ["自身 · 气血 · 击退", "体力1，需气血≥2层；连段外发动，降低气血2层。",
  "6秒内，每套普攻末段出手时追加一道血浪。", damage + "，按出手时气血结算。",
  "前方%s°扇面，最远%s单位，0.78秒扩散，贯穿敌人。" % [String.num(fan, 0), String.num(WAVE_REACH, 0)],
  "血浪命中时有几率小幅击退敌人。", "强化结束后冷却%s秒。" % String.num(COOLDOWN, 0)]

static func wave_radius(age: float) -> float:
 var progress := clampf(age / WAVE_SECONDS, 0.0, 1.0)
 return lerpf(WAVE_START_REACH, WAVE_REACH, 1.0 - pow(1.0 - progress, 1.15))

static func control_profile(attacker: Dictionary, target: Dictionary) -> Dictionary:
 var difference := int(attacker.get("realm_tier", 0)) - int(target.get("realm_tier", 0))
 var scale := pow(0.2, maxi(0, -difference))
 var chance := KNOCKBACK_BASE_CHANCE + CLOUD_CONTROL_BONUS
 chance += (float(attacker.physique) - float(target.physique)) * KNOCKBACK_ATTRIBUTE
 chance += mini(2, maxi(0, difference)) * 0.10
 chance -= maxf(0.0, float(target.get("bearing_resistance_pp", 0.0))) / 100.0
 return {"chance":clampf(chance, 0.0, 0.99) * scale,
  "distance":KNOCKBACK_DISTANCE * scale, "seconds":KNOCKBACK_SECONDS * scale}
