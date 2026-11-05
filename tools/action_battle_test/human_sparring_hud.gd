extends "action_battle_hud.gd"

func battle_heading() -> String:
 return ""

func trial_title() -> String:
 return "一对一 · 人类陪练"

func ready_title() -> String:
 return "一对一 · 人类NPC"

func trial_intro() -> String:
 return "一名凡人陪练，会调整距离、出招、架招与错步。"

func trial_goal() -> String:
 return "玩家保留六招；击倒陪练获胜，T 可随时重开。"

func trial_controls() -> String:
 return "WASD 移动  ·  左键连招  ·  P 暂停  ·  T 重开  ·  F1 操作  ·  F2 判定"

func _draw_combat_texts() -> void:
 for mark in battle.combat_texts:
  var progress := clampf(float(mark.age) / float(mark.lifetime), 0.0, 1.0)
  var alpha := 1.0 - smoothstep(0.55, 1.0, progress)
  var kind := str(mark.kind)
  var size_px := 27 if bool(mark.critical) else (22 if kind == "damage" else 15)
  var text := str(int(mark.value))
  var color := Color("df6555") if str(mark.side) == "player" else Color("f2e7cc")
  if kind == "damage":
   if str(mark.side) == "player": text = "−" + text
   if bool(mark.critical): color = Color("edbb59")
  else:
   text = "闪避" if kind == "evade" else "化解"
   color = Color("a8bdaf") if kind == "evade" else Color("bad2bd")
  var point: Vector2 = (battle.get_global_transform_with_canvas() * Vector2(mark.point)) / ui_scale
  var lane := int(mark.lane)
  point += Vector2((lane - 2) * 34.0, -8.0 - (lane % 2) * 12.0 - 28.0 * pow(progress, 0.75))
  if kind != "damage": point += Vector2(0, -23)
  point.x -= font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px).x * 0.5
  draw_string_outline(font, point, text, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px, 3, Color(0.12, 0.15, 0.14, alpha * 0.94))
  draw_string(font, point, text, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px, Color(color, alpha))

func start_rect() -> Rect2:
 return Rect2(canvas_size * 0.5 + Vector2(-100, 55), Vector2(200, 42))

func pause_rect() -> Rect2:
 return Rect2(Vector2(canvas_size.x * 0.5 - 54, 54), Vector2(108, 30))

func pointer_over_ui() -> bool:
 return help_open or battle.configuring or dock_rect().grow(12).has_point(pointer) or pause_rect().has_point(pointer) or Rect2(24, 20, 315, 228).has_point(pointer) or Rect2(canvas_size.x - 254, 24, 230, 184).has_point(pointer) or (battle.simulation_paused and start_rect().has_point(pointer))

func _input(event: InputEvent) -> void:
 _update_layout()
 if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
  if not help_open and not battle.configuring and battle.hp > 0 and battle.enemy_hp > 0:
   if battle.simulation_paused and start_rect().has_point(pointer):
    battle.set_simulation_paused(false)
    get_viewport().set_input_as_handled()
    return
   if pause_rect().has_point(pointer):
    battle.set_simulation_paused(not battle.simulation_paused)
    get_viewport().set_input_as_handled()
    return
 super._input(event)

func _draw() -> void:
 super._draw()
 if help_open or battle.configuring: return
 _center(trial_title(), Vector2(canvas_size.x * 0.5, 30), 21, INK)
 if battle.hp <= 0 or battle.enemy_hp <= 0: return
 _button(pause_rect(), "P  继续" if battle.simulation_paused else "P  暂停")
 var panel := Rect2(Vector2(canvas_size.x - 254, 24), Vector2(230, 184))
 _paper(panel)
 var alive := 0
 for npc in battle.npcs:
  if npc.hp > 0: alive += 1
 _text("陪练  %d / %d" % [alive, battle.npcs.size()], panel.position + Vector2(14, 23), 16, JADE)
 for index in range(battle.npcs.size()):
  var npc: Node2D = battle.npcs[index]
  var origin := panel.position + Vector2(14, 46 + index * 43)
  _text("%d  %s" % [index + 1, npc.character_name], origin, 14, INK)
  _right("%d / %d" % [npc.hp, npc.max_hp], origin + Vector2(200, 0), 12, VERMILION)
  _text("体力 %s / 6   %s" % [String.num(npc.stamina, 0), npc.action_label], origin + Vector2(0, 17), 12, MUTED)
 _text(trial_controls(), Vector2(28, canvas_size.y - 25), 13, INK)
 if battle.show_hitboxes:
  var legend := Rect2(Vector2(28, canvas_size.y - 166), Vector2(480, 112))
  _paper(legend)
  _text("判定对照 · F2关闭", legend.position + Vector2(14, 23), 16, JADE)
  _text("脚圈定位地面；蓝线是属性射程的地面投影。", legend.position + Vector2(14, 47), 14)
  _text("橙色是手部高度的水平攻击层；竖线是身体高度。", legend.position + Vector2(14, 70), 14)
  _text("绿点同时满足距离与高度；离地高度另算。", legend.position + Vector2(14, 93), 14, JADE)
 if battle.simulation_paused:
  draw_rect(Rect2(Vector2.ZERO, canvas_size), Color(INK, 0.35))
  var card := Rect2(canvas_size * 0.5 - Vector2(260, 125), Vector2(520, 250))
  _paper(card)
  _center("演练暂停" if battle.has_started else ready_title(), card.position + Vector2(260, 45), 25)
  _center(trial_intro(), card.position + Vector2(260, 88), 16, MUTED)
  _center(trial_goal(), card.position + Vector2(260, 119), 15, MUTED)
  _button(start_rect(), "继续演练" if battle.has_started else "开始演练")
  _center("P 开始 / 继续    ·    Esc 返回首页", card.position + Vector2(260, 230), 13, MUTED)

func _draw_result() -> void:
 draw_rect(Rect2(Vector2.ZERO, canvas_size), Color(INK, 0.58))
 var rect := Rect2(canvas_size * 0.5 - Vector2(210, 100), Vector2(420, 200))
 _paper(rect)
 _center("演练结束", rect.position + Vector2(210, 36), 15, MUTED)
 _center("胜" if battle.enemy_hp <= 0 else "负", rect.position + Vector2(210, 103), 48, JADE if battle.enemy_hp <= 0 else VERMILION)
 _center("T  同组再试    ·    Esc  返回首页", rect.position + Vector2(210, 164), 15)

func _draw_help() -> void:
 var origin := _dialog("人类演练操作", "自由移动，每段拳掌朝鼠标方向释放。", 367.0)
 var lines := ["WASD / 方向键：移动     左键：连点或按住接续四段", "空格：错步     右键：痛击     Q：架招", "E：催血     R：聚炁     Tab：调整五个主动槽", "P：暂停／继续     T：同组重开     Esc：关闭面板／返回首页", "范围内逐个命中；各NPC有独立生命、体力和属性冷却。", "F2：对照地面射程、水平攻击层与身体高度。", "9：持剑／收剑     0：试挥（无判伤）"]
 for index in range(lines.size()):
  _text(lines[index], origin + Vector2(28, 113 + index * 32), 14)
