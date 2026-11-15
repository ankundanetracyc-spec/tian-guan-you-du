extends "human_sparring_hud.gd"
const MANUAL_RULES = preload("action_manual_rules.gd")

func _ready() -> void:
 super._ready()
 var icon := _load_art("textures/ui/combat/skill_icons/百骸_赤潮催劲.svg")
 if icon != null: icons[MANUAL_RULES.SKILL] = _circular_texture(icon)

func trial_title() -> String:
 return "新版秘籍 · 自选对练"

func ready_title() -> String:
 return "百骸云息 · 对练"

func trial_intro() -> String:
 var manual: RefCounted = battle.action_manual
 return "百骸化生 · 第%d卷 · %s" % [manual.chapter, "主修" if manual.role == 1 else "辅修"] if manual.heart_active() else "按所选招式与化法开始对练。"

func trial_goal() -> String:
 return "击倒%d名人类陪练获胜，T 同组重开，C 重新配置。" % battle.npcs.size()

func trial_controls() -> String:
 return "WASD 移动  ·  左键连招  ·  P 暂停  ·  T 重开  ·  C 配置  ·  F1 操作"

func start_rect() -> Rect2:
 return Rect2(canvas_size * 0.5 + Vector2(-100, 38), Vector2(200, 38))

func _draw() -> void:
 super._draw()
 if help_open or battle.configuring: return
 var manual: RefCounted = battle.action_manual
 var notice := skill_failure_text()
 if not notice.is_empty():
  var width := font.get_string_size(notice, HORIZONTAL_ALIGNMENT_LEFT, -1, 14).x + 32
  var notice_rect := Rect2(Vector2((canvas_size.x - width) / 2.0, 100), Vector2(width, 34))
  _paper(notice_rect)
  _center(notice, notice_rect.get_center() + Vector2(0, 5), 14, VERMILION)
 if not manual.heart_active(): return
 var rect := Rect2(28, 241, 304, 79)
 _paper(rect)
 _text("百骸化生 · 卷%d · %s" % [manual.chapter, "主修" if manual.role == 1 else "辅修"], rect.position + Vector2(12, 22), 15, JADE)
 _text("持血外放，近身双判", rect.position + Vector2(12, 44), 14)
 _text("下一套血气伤害＋40%" if manual.next_combo_ready else ("本套血气伤害＋40%" if manual.combo_bonus > 0.0 else ""), rect.position + Vector2(12, 65), 13, VERMILION)
 if rect.has_point(pointer) and manual.heart_active():
  var lines := MANUAL_RULES.heart_text(manual.chapter, manual.role == 1, battle.qi_blood, detailed_effects()).split("\n")
  var tooltip := Rect2(346, 241, minf(650.0, canvas_size.x - 370), 48 + lines.size() * 24)
  _paper(tooltip)
  _text("百骸化生 · 气血", tooltip.position + Vector2(15, 27), 20)
  for index in range(lines.size()): _text(lines[index], tooltip.position + Vector2(15, 54 + index * 24), 14)

func pointer_over_ui() -> bool:
 return super.pointer_over_ui() or Rect2(28, 241, 304, 79).has_point(pointer)

func detailed_effects() -> bool:
 return bool(battle.attribute_runtime.detailed_effects())

func active_skill_text(skill: String, in_dialog := false) -> String:
 if skill == MANUAL_RULES.SKILL: return ""
 return super.active_skill_text(skill, in_dialog)

func skill_failure_text() -> String:
 var manual: RefCounted = battle.action_manual
 if manual.buff_remaining > 0.0 or battle.feedback_timer <= 0.0 or manual.last_skill_failure != battle.feedback: return ""
 return manual.last_skill_failure

func skill_duration_progress(skill: String) -> float:
 if skill != MANUAL_RULES.SKILL: return 0.0
 return clampf(battle.action_manual.buff_remaining / MANUAL_RULES.BUFF_SECONDS, 0.0, 1.0)

func _draw_skill_circle(rect: Rect2, skill: String, binding: String) -> void:
 super._draw_skill_circle(rect, skill, binding)
 var progress := skill_duration_progress(skill)
 if progress <= 0.0: return
 draw_arc(rect.get_center(), rect.size.x / 2.0 + 2, -PI / 2, -PI / 2 + TAU, 64, Color(VERMILION, 0.18), 2, true)
 draw_arc(rect.get_center(), rect.size.x / 2.0 + 2, -PI / 2, -PI / 2 + TAU * progress, 64, VERMILION, 2, true)

func available_skill_rect(index: int) -> Rect2:
 return Rect2(canvas_size / 2.0 + Vector2(-255 + index * 86, 146), Vector2(80, 30))

func skill_description(skill: String) -> Array:
 if skill == MANUAL_RULES.SKILL:
  battle.ensure_fist_combo()
  var last: Dictionary = battle.fist_combo.preview(battle.fist_combo.STAGES.size() - 1)
  var fan: float = MANUAL_RULES.WAVE_REWRITE_FAN if battle.action_manual.rewrite_active(2) and battle.qi_blood >= 4 else MANUAL_RULES.WAVE_FAN
  if battle.action_manual.buff_remaining > 0.0: fan = battle.action_manual.buff_fan
  var lines: Array = MANUAL_RULES.skill_text(battle.qi_blood, int(last.damage), fan, detailed_effects())
  if battle.action_manual.rewrite_active(2): lines.append("化法：" + MANUAL_RULES.REWRITE_TEXTS[2])
  return lines
 var lines := super.skill_description(skill)
 if skill == "催血" and not lines.is_empty():
  lines[0] = "主动技能 · 自身 · 气血"
  var gain := 3 if battle.action_manual.rewrite_active(0) and battle.qi_blood <= 2 else 2
  lines[1] = "运功1秒后获得气血%d层，冷却%s秒。" % [gain, String.num(battle.skill_cooldown_duration(skill), 2)]
 for index in range(4):
  if MANUAL_RULES.REWRITE_TARGETS[index] == skill and battle.action_manual.rewrite_active(index):
   lines.append("化法：" + MANUAL_RULES.REWRITE_TEXTS[index])
 return lines

func _draw_combat_texts() -> void:
 for mark in battle.combat_texts:
  var progress := clampf(float(mark.age) / float(mark.lifetime), 0.0, 1.0)
  var alpha := 1.0 - smoothstep(0.55, 1.0, progress)
  var kind := str(mark.kind)
  var size_px := 27 if bool(mark.critical) else (22 if kind == "damage" else 15)
  var text := str(int(mark.value))
  var color := Color("df6555") if str(mark.side) == "player" else Color("f2e7cc")
  match kind:
   "damage":
    if str(mark.side) == "player": text = "−" + text
    if bool(mark.critical): color = Color("edbb59")
   "heal":
    text = "+" + text
    color = Color("92c9a6")
   "evade":
    text = "闪避"
    color = Color("a8bdaf")
   "block":
    text = "化解"
    color = Color("bad2bd")
   "resist":
    text = "抵抗"
    color = Color("a8bdaf")
  var point: Vector2 = (battle.get_global_transform_with_canvas() * Vector2(mark.point)) / ui_scale
  var lane := int(mark.lane)
  point += Vector2((lane - 2) * 34.0, -8.0 - (lane % 2) * 12.0 - 28.0 * pow(progress, 0.75))
  if kind not in ["damage", "heal"]: point += Vector2(0, -23)
  point.x -= font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px).x * 0.5
  draw_string_outline(font, point, text, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px, 3, Color(0.12, 0.15, 0.14, alpha * 0.94))
  draw_string(font, point, text, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px, Color(color, alpha))

func _draw_help() -> void:
 var origin := _dialog("新版秘籍对练", "血浪随普攻末段释放，浪头扫到时判伤。", 360.0)
 var lines := ["WASD：移动    左键：拳掌连招    空格：错步", "右键 / Q E R F：所选主动技能    Tab：调整招式", "%s：连段外发动，消耗1体力并降低气血2层。" % MANUAL_RULES.SKILL, "持续6秒，连段末段放浪；结束后冷却24秒。", "P：开始／暂停    T：同组重开    C：重新配置", "F2：判定对照    Esc：关闭面板／返回首页"]
 for index in range(lines.size()): _text(lines[index], origin + Vector2(28, 113 + index * 31), 14)
