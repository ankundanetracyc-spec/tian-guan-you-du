extends "action_battle_hud.gd"

var clothing_preview: Node2D
var preview_clock := 0.0
var preview_action := 1
var preview_outfit := ""


func _ready() -> void:
 super._ready()
 clothing_preview = load(battle._action_test_path("topdown_clothing_rig.gd")).new()
 clothing_preview.visible = false
 add_child(clothing_preview)


func _process(delta: float) -> void:
 super._process(delta)
 if clothing_preview == null:
  return
 clothing_preview.visible = battle.clothing_open
 if not battle.clothing_open:
  return
 preview_clock += minf(delta, 0.05)
 if preview_outfit != battle.avatar.outfit_id:
  preview_outfit = battle.avatar.outfit_id
  clothing_preview.set_outfit(preview_outfit)
 clothing_preview.position = canvas_size * 0.5 + Vector2(-250, -8)
 clothing_preview.scale = Vector2.ONE * 2.6
 clothing_preview.rotation = sin(preview_clock * 0.45) * 0.8 - 0.25
 clothing_preview.motion = preview_clock * 10
 clothing_preview.movement = 0.85 if preview_action == 1 else 0.0
 clothing_preview.local_velocity = Vector2(0.8, sin(preview_clock) * 0.3) if preview_action == 1 else Vector2.ZERO
 clothing_preview.attack = maxf(0.0, sin(fmod(preview_clock, 1.8) / 0.42 * PI)) if preview_action == 2 and fmod(preview_clock, 1.8) < 0.42 else 0.0
 clothing_preview.guarding = preview_action == 3
 clothing_preview.update_pose(delta)


func clothing_row_rect(index: int) -> Rect2:
 return Rect2(canvas_size * 0.5 + Vector2(35, -144 + index * 48), Vector2(425, 40))


func reset_feedback() -> void:
 last_stamina = int(battle.stamina)
 last_mana = int(battle.mana)
 shortage_timer = 0.0
 for timers in [bead_pulses, spent_cells, mana_pulses, skill_presses, previous_cooldowns]:
  timers.clear()


func pointer_over_ui() -> bool:
 _update_layout()
 return dock_rect().has_point(pointer) or Rect2(24, 20, 315, 165).has_point(pointer) or Rect2(24, canvas_size.y - 78, 410, 64).has_point(pointer) or battle.paused or battle.clothing_open or battle.hp <= 0 or battle.enemy_hp <= 0


func _input(event: InputEvent) -> void:
 _update_layout()
 if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
  battle.held_basic_attack = false
 if event is InputEventKey and event.pressed and event.keycode == KEY_F1:
  battle.held_basic_attack = false
 if battle.clothing_open:
  if event is InputEventKey and event.pressed and not event.echo and event.keycode in [KEY_ESCAPE, KEY_V]:
   battle.toggle_clothing()
  if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
   if Rect2(canvas_size * 0.5 + Vector2(401, -217), Vector2(60, 29)).has_point(pointer):
    battle.toggle_clothing()
   for index in range(battle.avatar.library.PROFILES.size()):
    if clothing_row_rect(index).has_point(pointer):
     battle.apply_clothing(str(battle.avatar.library.PROFILES[index].id))
   for index in range(4):
    if Rect2(canvas_size * 0.5 + Vector2(-445 + index * 98, 148), Vector2(90, 31)).has_point(pointer):
     preview_action = index
  if event is InputEventKey or event is InputEventMouseButton:
   get_viewport().set_input_as_handled()
  return
 if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT and not help_open and not battle.configuring:
  if Rect2(28, canvas_size.y - 68, 74, 29).has_point(pointer):
   battle.restart_trial()
   get_viewport().set_input_as_handled()
   return
  if Rect2(112, canvas_size.y - 68, 82, 29).has_point(pointer):
   battle.toggle_pause()
   get_viewport().set_input_as_handled()
   return
  if Rect2(204, canvas_size.y - 68, 78, 29).has_point(pointer):
   help_open = true
   battle.held_basic_attack = false
   get_viewport().set_input_as_handled()
   return
  if Rect2(292, canvas_size.y - 68, 90, 29).has_point(pointer):
   battle.toggle_clothing()
   get_viewport().set_input_as_handled()
   return
  if battle.paused and Rect2(canvas_size * 0.5 + Vector2(-80, 22), Vector2(160, 37)).has_point(pointer):
   battle.toggle_pause()
   get_viewport().set_input_as_handled()
   return
 super._input(event)


func _draw() -> void:
 if battle == null or font == null:
  return
 hovered_skill = ""
 _draw_player()
 _paper(Rect2(canvas_size.x - 213, 17, 190, 76), 0.86)
 _right("林间试手", Vector2(canvas_size.x - 30, 47), 27, INK)
 _right("俯视战斗 · 四招试演", Vector2(canvas_size.x - 30, 71), 14, MUTED)
 _draw_dock()
 _paper(Rect2(20, canvas_size.y - 79, 383, 70), 0.86)
 _button(Rect2(28, canvas_size.y - 68, 74, 29), "重来 T")
 _button(Rect2(112, canvas_size.y - 68, 82, 29), "继续 P" if battle.paused else "暂停 P")
 _button(Rect2(204, canvas_size.y - 68, 78, 29), "操作 F1")
 _button(Rect2(292, canvas_size.y - 68, 90, 29), "衣着 V")
 _text("WASD 移动   鼠标瞄准   Tab 换槽   Esc 返回", Vector2(28, canvas_size.y - 17), 13, INK)
 if not battle.feedback.is_empty() and battle.feedback_timer > 0:
  _center(battle.feedback, Vector2(canvas_size.x * 0.5, canvas_size.y - 35), 15, VERMILION if battle.feedback == "体力不足" else INK)
 if not hovered_skill.is_empty() and not battle.paused:
  _draw_tooltip(hovered_skill)
 if battle.configuring:
  _draw_configuration()
 if battle.clothing_open:
  _draw_clothing()
  return
 if battle.paused and not help_open:
  draw_rect(Rect2(Vector2.ZERO, canvas_size), Color(INK, 0.5))
  var pause_box := Rect2(canvas_size * 0.5 - Vector2(230, 100), Vector2(460, 200))
  _paper(pause_box, 1.0)
  _center("准备就绪" if battle.ready_to_start else "已暂停", pause_box.position + Vector2(230, 42), 26)
  var caption: String = "鼠标瞄准，左键拳掌，空格错步。" if battle.ready_to_start else ("窗口失去焦点，点击继续后恢复。" if battle.focus_paused else "停一停，观察下一次出手。")
  _center(caption, pause_box.position + Vector2(230, 78), 15, MUTED)
  _button(Rect2(canvas_size * 0.5 + Vector2(-80, 22), Vector2(160, 37)), "开始试演  P" if battle.ready_to_start else "继续战斗  P")
 if help_open:
  _draw_help()
 if battle.hp <= 0 or battle.enemy_hp <= 0:
  _draw_result()


func _draw_help() -> void:
 var origin := _dialog("林间试手", "看清狼的蓄势方向，走位避让，再近身出招。")
 var rows := [
  ["W A S D", "移动", "鼠标", "转身与瞄准"],
  ["左键 / 按住", "拳掌 · 0体力", "右键", "痛击 · 1体力"],
  ["空格", "错步 · 1体力", "Q", "架招 · 1体力"],
  ["Tab / V", "招式槽 / 衣着试装", "P / T", "暂停 / 重来"],
  ["F1", "关闭操作说明", "Esc", "返回首页"],
 ]
 for index in range(rows.size()):
  var row: Array = rows[index]
  var y := origin.y + 113 + index * 34
  _text(row[0], Vector2(origin.x + 28, y), 14, JADE)
  _text(row[1], Vector2(origin.x + 137, y), 14)
  _text(row[2], Vector2(origin.x + 309, y), 14, JADE)
  _text(row[3], Vector2(origin.x + 383, y), 14)


func _draw_clothing() -> void:
 draw_rect(Rect2(Vector2.ZERO, canvas_size), Color(INK, 0.57))
 var center := canvas_size * 0.5
 var panel := Rect2(center - Vector2(500, 235), Vector2(1000, 470))
 _paper(panel, 1.0)
 _text("衣着试装", panel.position + Vector2(28, 39), 27)
 _text("仅本次试玩生效", panel.position + Vector2(28, 65), 14, MUTED)
 _button(Rect2(center + Vector2(401, -217), Vector2(60, 29)), "关闭")
 draw_line(center + Vector2(2, -150), center + Vector2(2, 199), Color(LINE, 0.45), 1, true)
 draw_circle(center + Vector2(-250, 8), 133, Color("d9decb"), true, -1, true)
 draw_arc(center + Vector2(-250, 8), 142, 0, TAU, 100, Color(LINE, 0.28), 0.8, true)
 var profile: Dictionary = battle.avatar.library.get_profile(battle.avatar.outfit_id)
 _center(str(profile.name), center + Vector2(-250, -151), 21)
 _right(str(battle.clothing_source), panel.position + Vector2(960, 65), 13, MUTED)
 for index in range(4):
  var rect := Rect2(center + Vector2(-445 + index * 98, 148), Vector2(90, 31))
  _button(rect, ["待机", "行走", "出掌", "架招"][index])
  if index == preview_action:
   draw_line(rect.position + Vector2(8, 29), rect.end - Vector2(8, 2), JADE, 2, true)
 for index in range(battle.avatar.library.PROFILES.size()):
  var item: Dictionary = battle.avatar.library.PROFILES[index]
  var rect := clothing_row_rect(index)
  _button(rect, "")
  draw_circle(rect.position + Vector2(20, 20), 8, Color(str(item.cloth)), true, -1, true)
  _text(str(item.name), rect.position + Vector2(40, 26), 18)
  if str(item.id) == battle.avatar.outfit_id:
   _right("已试装", rect.end - Vector2(15, 14), 13, JADE)
   draw_rect(rect.grow(-1), Color(JADE, 0.6), false, 1.0)
 _text("发型保持原样；衣着试装不改变招式和属性。", panel.position + Vector2(28, 442), 14, MUTED)
