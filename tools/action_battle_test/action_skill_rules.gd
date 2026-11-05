extends RefCounted

# Shared by the arena and the character panel. Queue resources retain their own rules.
const ATTACK_RANGE = preload("attack_range_rules.gd")
const RULES = {
 "拳掌": {"cost": 0, "cooldown": 0.8, "min_cooldown": 0.35, "cooldown_scale": 0.002, "delay": 0.08, "reach": 70.0 * ATTACK_RANGE.MULTIPLIER},
 "痛击": {"cost": 1, "cooldown": 5.0, "min_cooldown": 2.0, "cooldown_scale": 50.0, "delay": 0.20, "reach": 90.0 * ATTACK_RANGE.MULTIPLIER},
 "错步": {"cost": 1, "cooldown": 6.0, "min_cooldown": 2.0, "cooldown_scale": 100.0, "delay": 0.0},
 "架招": {"cost": 1, "cooldown": 5.0, "min_cooldown": 1.5, "cooldown_scale": 0.03, "delay": 0.0},
 "催血": {"cost": 1, "cooldown": 12.0, "min_cooldown": 4.0, "cooldown_scale": 0.05, "delay": 1.0, "move_multiplier": 0.5, "gain": 2},
 "聚炁": {"cost": 1, "cooldown": 12.0, "min_cooldown": 4.0, "cooldown_scale": 80.0, "delay": 1.0, "move_multiplier": 0.5, "gain": 2},
}
const COMBO = preload("fist_combo_runtime.gd")

static func cooldown(name: String, physique: float, soul: float, dodge: float, intelligence: float) -> float:
 if not RULES.has(name): return 0.0
 var rule: Dictionary = RULES[name]
 var attribute := maxf(0.0, physique)
 if name == "错步": attribute = maxf(0.0, dodge)
 elif name == "聚炁": attribute = maxf(0.0, intelligence)
 elif name == "架招": attribute = maxf(0.0, minf(physique, soul))
 if name in ["拳掌", "架招", "催血"]:
  return maxf(rule.min_cooldown, rule.cooldown - attribute * rule.cooldown_scale)
 return maxf(rule.min_cooldown, rule.cooldown / (1.0 + attribute / rule.cooldown_scale))

static func cooldown_formula(name: String) -> String:
 if not RULES.has(name): return ""
 var rule: Dictionary = RULES[name]
 var attribute := "身法" if name == "错步" else ("灵慧" if name == "聚炁" else ("min(体魄,神魂)" if name == "架招" else "体魄"))
 var floor_text := String.num(rule.min_cooldown, 2)
 var base_text := String.num(rule.cooldown, 2)
 var scale_text := String.num(rule.cooldown_scale, 3)
 if name in ["拳掌", "架招", "催血"]:
  return "max(%s, %s−%s×%s)" % [floor_text, base_text, attribute, scale_text]
 return "max(%s, %s÷(1+%s÷%s))" % [floor_text, base_text, attribute, scale_text]

func preview(name: String, physique: float, soul: float, dodge: float, intelligence: float, blood_capacity: int, mana_capacity: int) -> Dictionary:
 if not RULES.has(name): return {}
 var rule: Dictionary = RULES[name]
 var body := ""
 var phase := "立即施展"
 var tags := "无"
 var target := "自身"
 match name:
  "拳掌":
   target = "前方半圆内的敌人"
   phase = "四段连招 · 完整2.2秒"
   var base := roundi(7.0 + maxf(0.0, physique) * 0.45)
   var lines: Array[String] = []
   for stage in COMBO.STAGES:
    var damage := roundi(base * stage.scale)
    var reach := minf(stage.reach_cap, stage.base_reach + maxf(0.0, physique) * stage.reach_scale)
    lines.append("%s：伤害%d · 半径%s" % [stage.name, damage, String.num(reach, 1)])
   body = "连点或按住左键接续四段；每段准备时跟随鼠标，释放后锁定方向。\n\n" + "\n".join(lines)
   body += "\n\n第2／4段分别贴步8／12；各段独立判定命中、会心与防御。错步可中断连招。\n\n伤害公式：A=round(7+体魄×0.45)，各段取40%／40%／50%／70%，分别取整。"
  "痛击":
   phase = "出手延迟0.2秒"
   target = "前方%s半径的半圆内敌人" % String.num(float(rule.reach), 0)
   body = "造成%d点基础物理伤害。\n\n实际伤害再经命中、会心与防御结算。准备时仍可移动。\n\n伤害公式：伤害=round(11+体魄×0.75)。" % roundi(11.0 + physique * 0.75)
  "错步":
   body = "沿移动方向错步；静止时向鼠标朝向的反方向后撤。\n\n0.2秒内位移最多100，前0.15秒无敌。可中断拳掌或运功；已支付费用不退。"
  "架招":
   tags = "化解 · 应对"
   var block := 3 + floori(physique / 4.0) + floori(minf(physique * 0.25, soul * 0.20))
   body = "0.2秒内化解首次命中的物理来袭，本次最多化解%d点伤害。未用完的窗口不保留。\n\n化解公式：化解=3+floor(体魄÷4)+floor(min(体魄×0.25,神魂×0.20))。" % block
  "催血":
   tags = "生命"
   phase = "运功1秒 · 期间移速50%"
   body = "完成后获得气血%d层，当前气血上限%d；不立即治疗。\n\n运功中可移动，错步可将其中断。中断不获得气血，已支付的体力不退，随后计算完整冷却。" % [rule.gain, blood_capacity]
  "聚炁":
   tags = "周天"
   phase = "运功1秒 · 期间移速50%"
   body = "完成后获得炁%d。\n\n" % rule.gain
   body += "当前法力容量%d，炁达到%d时完成一次公共周天：清空全部炁，法力回满，不保留超额炁。" % [mana_capacity, mana_capacity * 2] if mana_capacity > 0 else "当前法力容量为0，不触发公共周天。"
   body += "\n\n运功中可移动，错步可将其中断。中断不获得炁，体力不退。心法的周天追加尚待适配。"
 return {"cost":int(rule.cost), "cooldown":cooldown(name, physique, soul, dodge, intelligence), "minimum":float(rule.min_cooldown), "formula":cooldown_formula(name), "phase":phase, "tags":tags, "target":target, "body":body}
