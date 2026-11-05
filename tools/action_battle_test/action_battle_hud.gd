extends Node2D

# Presentation only. Battle state, input bindings and skill resolution stay in the arena.
const INK := Color("293c39")
const MUTED := Color("65716a")
const LINE := Color("899083")
const PAPER := Color("eee9d9")
const JADE := Color("557f70")
const MANA := Color("527b8b")
const VERMILION := Color("a34c43")
const KEYS := ["右键", "Q", "E", "R", "F"]
const SKILL_ART := {
 "拳掌": "textures/ui/combat/skill_icons/凡俗_应敌_v5.png",
 "痛击": "textures/ui/combat/skill_icons/凡俗_痛击_v5.png",
 "架招": "textures/ui/combat/skill_icons/凡俗_架招_v5.png",
 "错步": "textures/ui/combat/status_icons/身法_v1.png",
 "催血": "textures/ui/combat/skill_icons/基础技能_催血_v3.png",
 "聚炁": "textures/ui/combat/skill_icons/基础技能_聚炁_v2.png",
}

var battle: Node2D
var help_open := false
var font: Font
var paper_texture: Texture2D
var icons := {}
var canvas_size := Vector2(1280, 720)
var ui_scale := 1.0
var pointer := Vector2(-100, -100)
var hovered_skill := ""
var hovered_status := ""
var ink_textures := {}
var ink_noise := FastNoiseLite.new()
var last_stamina := 6
var bead_pulses := {}
var status_icons := {}
var spent_cells := {}
var mana_pulses := {}
var last_mana := 3
var shortage_timer := 0.0
var skill_presses := {}
var previous_cooldowns := {}



func _ready() -> void:
 name = "ActionBattleHud"
 ink_noise.seed = 1703
 ink_noise.frequency = 0.065
 ink_noise.fractal_octaves = 3
 last_stamina = int(battle.stamina)
 last_mana = int(battle.mana)
 font = ThemeDB.fallback_font
 var font_path: String = battle._resolve_project_art_path("resources/font/LXGWZhenKaiSlabGB-Regular.ttf")
 if not font_path.is_empty():
  var project_font := FontFile.new()
  project_font.data = FileAccess.get_file_as_bytes(font_path)
  font = project_font
 paper_texture = _load_art("textures/ui/world_hud_20261011/paper_panel_v1.png")
 for skill in SKILL_ART:
  var texture := _load_art(SKILL_ART[skill])
  if texture != null:
   icons[skill] = _circular_texture(texture)
 _update_layout()


func _load_art(relative_path: String) -> Texture2D:
 var path: String = battle._resolve_project_art_path(relative_path)
 if path.is_empty(): return null
 var bitmap := Image.new()
 if bitmap.load(path) != OK: return null
 return ImageTexture.create_from_image(bitmap)


func _update_layout() -> void:
 var viewport_size := get_viewport_rect().size
 ui_scale = minf(viewport_size.x / 1280.0, viewport_size.y / 720.0)
 canvas_size = viewport_size / ui_scale
 scale = Vector2.ONE * ui_scale
 pointer = get_viewport().get_mouse_position() / ui_scale


func _process(delta: float) -> void:
 _update_layout()
 if help_open or battle.configuring:
  queue_redraw()
  return
 var current := int(battle.stamina)
 if current > last_stamina:
  for index in range(last_stamina, current):
   bead_pulses[index] = 0.22
   spent_cells.erase(index)
 elif current < last_stamina:
  for index in range(current, last_stamina):
   spent_cells[index] = 0.18
   bead_pulses.erase(index)
 last_stamina = current
 var current_mana := int(battle.mana)
 if current_mana > last_mana:
  for index in range(last_mana, current_mana): mana_pulses[index] = 0.22
 elif current_mana < last_mana:
  for index in range(current_mana, last_mana): mana_pulses.erase(index)
 last_mana = current_mana
 for skill in SKILL_ART:
  var remaining: float = battle.skill_cooldown_remaining(skill)
  if remaining > float(previous_cooldowns.get(skill, 0.0)) + 0.001:
   skill_presses[skill] = 0.16
  previous_cooldowns[skill] = remaining
 for timers in [bead_pulses, spent_cells, mana_pulses, skill_presses]:
  for key in timers.keys():
   timers[key] = maxf(0, float(timers[key]) - delta)
   if timers[key] <= 0: timers.erase(key)
 shortage_timer = maxf(0.0, shortage_timer - delta)
 queue_redraw()


func notify_feedback(value: String) -> void:
 if value == "体力不足": shortage_timer = 0.42

func reset_feedback() -> void:
 last_stamina = int(battle.stamina)
 last_mana = int(battle.mana)
 shortage_timer = 0.0
 for timers in [bead_pulses, spent_cells, mana_pulses, skill_presses, previous_cooldowns]: timers.clear()


func dock_rect() -> Rect2:
 return Rect2(canvas_size.x - 444, canvas_size.y - 86, 418, 68)


func slot_rect(index: int, in_dialog := false) -> Rect2:
 if in_dialog:
  return Rect2(canvas_size.x / 2.0 - 236 + index * 98, canvas_size.y / 2.0 - 17, 80, 80)
 return Rect2(dock_rect().position + Vector2(130 + index * 60, 0), Vector2(48, 48))


func close_rect() -> Rect2:
 return Rect2(canvas_size / 2.0 + Vector2(206, -133), Vector2(54, 27))

func available_skill_rect(index: int) -> Rect2:
 return Rect2(canvas_size / 2.0 + Vector2(-248 + index * 100, 146), Vector2(92, 30))


func _input(event: InputEvent) -> void:
 _update_layout()
 if event is InputEventKey and event.pressed and not event.echo:
  if event.keycode == KEY_F1:
   help_open = not help_open
   battle.held_basic_attack = false
   get_viewport().set_input_as_handled()
   return
  if event.keycode == KEY_ESCAPE and (help_open or battle.configuring):
   if help_open: help_open = false
   else:
    battle.configuring = false
    battle.selected_slot = -1
   get_viewport().set_input_as_handled()
   return
 if help_open:
  if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT and close_rect().has_point(pointer):
   help_open = false
  if event is InputEventKey or event is InputEventMouseButton:
   get_viewport().set_input_as_handled()
  return
 if not event is InputEventMouseButton: return
 # A mouse action over the HUD must not leak through as an attack on the arena.
 if battle.configuring:
  if event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
   if close_rect().has_point(pointer):
    battle.configuring = false
    battle.selected_slot = -1
   else:
    if battle.selected_slot >= 0:
     var choices: Array[String] = battle.configurable_skills()
     for index in range(choices.size()):
      if available_skill_rect(index).has_point(pointer):
       battle.assign_skill_slot(battle.selected_slot, choices[index])
       get_viewport().set_input_as_handled()
       return
    for index in range(5):
     if slot_rect(index, true).has_point(pointer):
      # Reuse the same swap path as the existing numeric-key interaction.
      var key := InputEventKey.new()
      key.keycode = KEY_1 + index
      key.pressed = true
      battle._input(key)
  get_viewport().set_input_as_handled()
  return
 if dock_rect().has_point(pointer) or Rect2(24, 20, 310, 215).has_point(pointer):
  get_viewport().set_input_as_handled()


func _text(value: String, point: Vector2, size_px := 16, color := INK) -> void:
 draw_string(font, point, value, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px, color)


func _right(value: String, point: Vector2, size_px := 16, color := INK) -> void:
 _text(value, point - Vector2(font.get_string_size(value, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px).x, 0), size_px, color)


func _center(value: String, point: Vector2, size_px := 16, color := INK) -> void:
 _text(value, point - Vector2(font.get_string_size(value, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px).x / 2.0, 0), size_px, color)


func _paper(rect: Rect2, opacity := 0.95) -> void:
 draw_rect(rect, Color(PAPER, opacity))
 if paper_texture != null:
  # Sample the flat interior; keep the frame crisp at every window size.
  draw_texture_rect_region(paper_texture, rect, Rect2(100, 130, 1900, 450), Color(1, 1, 1, 0.30))
 draw_rect(rect, Color(LINE, 0.52), false, 1.0)
 for corner in [rect.position, rect.end]:
  var direction := 1.0 if corner == rect.position else -1.0
  draw_line(corner, corner + Vector2(16 * direction, 0), INK, 1.5, true)
  draw_line(corner, corner + Vector2(0, 9 * direction), INK, 1.5, true)


func _button(rect: Rect2, label: String) -> void:
 var hover := rect.has_point(pointer)
 draw_rect(rect, Color(INK, 0.92) if hover else Color(PAPER, 0.88))
 draw_rect(rect, Color(LINE, 0.7), false, 1)
 _center(label, rect.position + Vector2(rect.size.x / 2.0, 19), 14, PAPER if hover else INK)


func _draw() -> void:
 if battle == null or font == null: return
 hovered_skill = ""
 hovered_status = ""
 _draw_combat_texts()
 _draw_player()
 _draw_dock()
 if not hovered_skill.is_empty() and not battle.configuring and not help_open:
  _draw_tooltip(hovered_skill)
 elif not hovered_status.is_empty() and not battle.configuring and not help_open:
  _draw_resource_tooltip(hovered_status)
 if battle.configuring: _draw_configuration()
 if help_open: _draw_help()
 if not battle.message.is_empty():
  _center(battle.message, Vector2(canvas_size.x / 2, 240), 20, VERMILION)
 if battle.hp <= 0 or battle.enemy_hp <= 0: _draw_result()


func _draw_combat_texts() -> void:
 for mark in battle.combat_texts:
  var age := float(mark.age)
  var progress := clampf(age/float(mark.lifetime),0.0,1.0)
  var alpha := 1.0-smoothstep(0.55,1.0,progress)
  var kind := str(mark.kind)
  var critical := bool(mark.critical)
  var size_px := 31 if critical else (25 if kind=="damage" else 17)
  var text := str(int(mark.value))
  var color := Color("f2e7cc") if str(mark.side)=="enemy" else Color("df6555")
  if kind=="damage":
   if str(mark.side)=="player": text="−"+text
   if critical: color=Color("edbb59")
  else:
   text="闪避" if kind=="evade" else "化解"
   color=Color("a8bdaf") if kind=="evade" else Color("bad2bd")
  var point: Vector2 = (battle.get_global_transform_with_canvas()*Vector2(mark.point))/ui_scale
  var lane := int(mark.lane)
  point+=Vector2((lane-1)*13.0,-8.0-28.0*pow(progress,0.75))
  if kind!="damage": point+=Vector2(15,-23)
  var width := font.get_string_size(text,HORIZONTAL_ALIGNMENT_LEFT,-1,size_px).x
  point.x-=width*0.5
  draw_string_outline(font,point,text,HORIZONTAL_ALIGNMENT_LEFT,-1,size_px,3,Color(0.12,0.15,0.14,alpha*0.94))
  draw_string(font,point,text,HORIZONTAL_ALIGNMENT_LEFT,-1,size_px,Color(color,alpha))


func battle_heading() -> String:
 return "%s · 即时技能演练（无奖励）" % battle.character_name if battle.attribute_runtime != null else ""

func _draw_player() -> void:
 var heading := battle_heading()
 if not heading.is_empty():
  _center(heading, Vector2(canvas_size.x / 2.0, 28), 15, MUTED)
 # The three strips share only a fine stem, with no enclosing panel.
 draw_line(Vector2(27, 28), Vector2(27, 149), Color("827b66"), 0.8, true)
 _ink_box(Rect2(28, 24, 304, 45), PAPER, 1.0, 11.0)
 _text("生命", Vector2(40, 44), 16)
 _right("%d / %d" % [maxi(battle.hp, 0), battle.max_hp], Vector2(318, 44), 15)
 var gauge := Rect2(40, 53, 278, 7)
 _rounded(gauge, Color("c9c4b3"), 3.5)
 _ink_box(gauge, VERMILION, clampf(float(battle.hp) / maxi(battle.max_hp, 1), 0, 1), 3.5, false)
 _resource("体力", int(battle.stamina), 6, Vector2(28, 77), JADE)
 _resource("法力", battle.mana, battle.mana_capacity, Vector2(28, 117), MANA)
 # No demonstration buffs or empty status heading. Use only actual status data.
 var index := 0
 for status in battle.visible_statuses:
  if index >= 8: break
  if not status_icons.has(status):
   status_icons[status] = _load_art("textures/ui/combat/status_icons/%s_v1.png" % status)
  var texture: Texture2D = status_icons[status]
  if texture == null: continue
  var rect := Rect2(97 + index * 30, 158, 24, 24)
  _ink_box(rect, PAPER, 1, 4)
  draw_texture_rect(texture, rect.grow(-2), false)
  if status in ["气血", "炁"]:
   var count: int = battle.qi_blood if status == "气血" else battle.qi
   _rounded(Rect2(rect.end - Vector2(13, 12), Vector2(16, 15)), Color(INK, 0.92), 3)
   _center(str(count), rect.end + Vector2(-5, 0), 12, PAPER)
   if rect.has_point(pointer): hovered_status = status
  index += 1
 _draw_self_cast()

func cast_rect() -> Rect2:
 return Rect2(40, 214, 278, 6)

func _draw_self_cast() -> void:
 if battle.fist_combo != null and battle.fist_combo.active and battle.hp > 0 and battle.enemy_hp > 0:
  var combo: RefCounted = battle.fist_combo
  _ink_box(Rect2(28, 186, 304, 44), PAPER, 1.0, 8)
  _text("拳掌 · 第%d式" % (combo.stage + 1), Vector2(40, 204), 16, JADE)
  _right("接续%.1f秒" % combo.wait_remaining if combo.waiting else str(combo.STAGES[combo.stage].name), Vector2(318, 204), 13, MUTED)
  _rounded(cast_rect(), Color("c9c4b3"), 3)
  _ink_box(cast_rect(), JADE, combo.progress(), 3, false)
  return
 if battle.pending_self_skill.is_empty() or battle.hp <= 0 or battle.enemy_hp <= 0: return
 var tint := VERMILION if battle.pending_self_skill == "催血" else MANA
 _ink_box(Rect2(28, 186, 304, 44), PAPER, 1.0, 8)
 _text("%s · 运功" % battle.pending_self_skill, Vector2(40, 204), 16, tint)
 _right("%.1f秒 · 移速50%%" % battle.self_cast_remaining, Vector2(318, 204), 13, MUTED)
 _rounded(cast_rect(), Color("c9c4b3"), 3)
 _ink_box(cast_rect(), tint, battle.self_cast_progress(), 3, false)

func _draw_resource_tooltip(status: String) -> void:
 var rect := Rect2(28, 237, 385, 110)
 _paper(rect)
 _text(status, rect.position + Vector2(16, 27), 20)
 if status == "气血":
  _text("当前层数：%d/%d" % [battle.qi_blood, battle.qi_blood_capacity], rect.position + Vector2(16, 52), 14)
  _text("不随时间回落；获得气血不会立即回复生命。", rect.position + Vector2(16, 76), 14)
 else:
  var threshold := str(battle.mana_capacity * 2) if battle.mana_capacity > 0 else "无周天"
  _text("当前数值：%d · 周天阈值：%s" % [battle.qi, threshold], rect.position + Vector2(16, 52), 14)
  _text("达到阈值清炁、回满法力；阈值不是储存上限。", rect.position + Vector2(16, 76), 14)
 _text("战斗结束清空。", rect.position + Vector2(16, 98), 13, MUTED)


func _resource(label: String, value: int, capacity: int, origin: Vector2, tint: Color) -> void:
 var count := maxi(capacity, 0)
 var cell_width := minf(25, 180.0 / maxf(count, 1) - 5)
 cell_width = maxf(cell_width, 3)
 var row_width := 76.0 + count * (cell_width + 6)
 var is_stamina := label == "体力"
 if is_stamina and shortage_timer > 0:
  origin.x += sin(shortage_timer / 0.42 * TAU * 2) * 1.8 * shortage_timer / 0.42
 var row := Rect2(origin, Vector2(maxf(row_width, 130), 32))
 _ink_box(row, PAPER, 1, 11)
 _text(label, origin + Vector2(12, 21), 16, VERMILION if is_stamina and shortage_timer > 0 else INK)
 if is_stamina and shortage_timer > 0:
  _outline(row, Color(VERMILION, shortage_timer / 0.42 * 0.7), 11, 0.65)
 for index in range(count):
  var rect := Rect2(origin + Vector2(69 + index * (cell_width + 6), 10), Vector2(cell_width, 12))
  var pulses: Dictionary = bead_pulses if is_stamina else mana_pulses
  if pulses.has(index):
   var phase := float(pulses[index]) / 0.22
   rect = rect.grow(sin(phase * PI) * 0.8)
  _rounded(rect, Color("d9d6c7"), rect.size.y / 2)
  var fraction := stamina_cell_fill(index) if label == "体力" else clampf(float(value - index), 0, 1)
  _ink_box(rect, tint, fraction, rect.size.y / 2, false)
  _outline(rect, Color("7f8273"), rect.size.y / 2, 0.65)
  if is_stamina and spent_cells.has(index):
   _outline(rect, Color(VERMILION, float(spent_cells[index]) / 0.18 * 0.65), rect.size.y / 2, 0.65)


func stamina_cell_fill(index: int) -> float:
 # Actual fractional stamina is shared with skill affordability. A pending
 # recovery must not look like a full spendable bead or jump on fast-step entry.
 return clampf(float(battle.stamina)-index, 0.0, 1.0)


func _rounded(rect: Rect2, tint: Color, radius: float) -> void:
 var style := StyleBoxFlat.new()
 style.bg_color = tint
 style.set_corner_radius_all(roundi(radius))
 style.anti_aliasing = true
 draw_style_box(style, rect)


func _outline(rect: Rect2, tint: Color, radius: float, width := 1.0) -> void:
 var style := StyleBoxFlat.new()
 style.bg_color = Color.TRANSPARENT
 style.border_color = tint
 style.set_border_width_all(1)
 style.set_corner_radius_all(roundi(radius))
 style.anti_aliasing = true
 style.border_blend = width < 1.0
 draw_style_box(style, rect)


func _ink_box(rect: Rect2, tint: Color, fraction := 1.0, radius := 8.0, border := true) -> void:
 # Static procedural ink washes: smooth geometry, matte density, no animated noise or glow.
 var key := "%dx%d_%s_%d" % [roundi(rect.size.x), roundi(rect.size.y), tint.to_html(), roundi(radius)]
 if not ink_textures.has(key):
  var dimensions := Vector2i(maxi(roundi(rect.size.x * 2), 4), maxi(roundi(rect.size.y * 2), 4))
  var bitmap := Image.create(dimensions.x, dimensions.y, false, Image.FORMAT_RGBA8)
  var half_size := Vector2(dimensions) / 2.0
  var rad := minf(radius * 2.0, half_size.y)
  for y in range(dimensions.y):
   var depth := float(y) / maxf(dimensions.y - 1, 1)
   for x in range(dimensions.x):
    var point := Vector2(x + 0.5, y + 0.5)
    var q := (point - half_size).abs() - (half_size - Vector2.ONE * rad)
    var distance := q.max(Vector2.ZERO).length() + minf(maxf(q.x, q.y), 0.0) - rad
    var alpha := clampf(0.5 - distance, 0, 1)
    var wash := (ink_noise.get_noise_2d(x * 0.5, y * 0.5) + 1.0) * 0.5
    var color: Color
    if tint == PAPER:
     color = tint.lerp(Color("c8bfa6"), (wash * 0.09 + depth * 0.035))
     alpha *= 0.95
    else:
     var density := clampf(depth * 0.60 + wash * 0.65 - 0.15, 0, 1)
     color = tint.lerp(PAPER, (1.0 - density) * 0.32).lerp(tint.darkened(0.25), density * 0.55)
    color.a = alpha
    bitmap.set_pixel(x, y, color)
  ink_textures[key] = ImageTexture.create_from_image(bitmap)
 var texture: Texture2D = ink_textures[key]
 var amount := clampf(fraction, 0, 1)
 if amount > 0:
  draw_texture_rect_region(texture, Rect2(rect.position, Vector2(rect.size.x * amount, rect.size.y)), Rect2(0, 0, texture.get_width() * amount, texture.get_height()))
 if border: _outline(rect, Color("88806b"), radius, 0.7)


func _circular_texture(texture: Texture2D) -> Texture2D:
 # Runtime circular clipping of existing illustrations; source assets remain intact.
 var bitmap := texture.get_image()
 bitmap.convert(Image.FORMAT_RGBA8)
 bitmap.resize(160, 160, Image.INTERPOLATE_LANCZOS)
 for y in range(160):
  for x in range(160):
   var color := bitmap.get_pixel(x, y)
   color.a *= clampf(79.0 - Vector2(x - 79.5, y - 79.5).length(), 0, 1)
   bitmap.set_pixel(x, y, color)
 return ImageTexture.create_from_image(bitmap)


func _draw_dock() -> void:
 for index in range(2):
  var rect := Rect2(dock_rect().position + Vector2(index * 57, 4), Vector2(42, 42))
  _draw_skill_circle(rect, "拳掌" if index == 0 else "错步", "左键" if index == 0 else "空格")
 for index in range(5):
  _draw_skill_circle(slot_rect(index), str(battle.skill_slots[index]), KEYS[index])


func _draw_skill_circle(rect: Rect2, skill: String, binding: String) -> void:
 var center := rect.get_center()
 var radius := rect.size.x / 2.0
 var hover := center.distance_to(pointer) < radius
 var empty := skill.is_empty()
 if skill_presses.has(skill):
  rect = rect.grow(-sin(float(skill_presses[skill]) / 0.16 * PI) * 1.2)
  radius = rect.size.x / 2.0
 draw_circle(center, radius, PAPER, true, -1, true)
 if not empty:
  if icons.has(skill):
   draw_texture_rect(icons[skill], rect.grow(-1.5), false, Color(1, 1, 1, 1.0 if battle.stamina >= battle.skill_cost(skill) else 0.45))
  else:
   _draw_step_icon(center, JADE)
 draw_arc(center, radius, 0, TAU, 64, JADE if hover else Color("7c715b"), 1.5 if hover else 1.0, true)
 draw_arc(center, radius - 2, 0, TAU, 64, Color("ada28a"), 0.5, true)
 _center(binding, Vector2(center.x, rect.end.y + 19), 13)
 if not empty:
  var cost: int = battle.skill_cost(skill)
  for index in range(cost):
   draw_circle(Vector2(center.x + (index - (cost - 1) / 2.0) * 8, rect.end.y - 4), 2.5, JADE, true, -1, true)
  var remaining: float = battle.skill_cooldown_remaining(skill)
  var duration: float = battle.skill_cooldown_total(skill)
  if remaining > 0 and duration > 0:
   var angle := TAU * clampf(remaining / duration, 0, 1)
   var fan := PackedVector2Array([center])
   for step in range(49):
    fan.append(center + Vector2.from_angle(-PI / 2 + angle * step / 48.0) * (radius - 2))
   if angle >= TAU * 0.999:
    draw_circle(center, radius - 2, Color(INK, 0.22), true, -1, true)
   else:
    draw_colored_polygon(fan, Color(INK, 0.22))
   draw_arc(center, radius - 4, -PI / 2, -PI / 2 + angle, 48, Color(INK, 0.8), 2, true)
   var countdown := cooldown_text(remaining)
   var text_width := font.get_string_size(countdown, HORIZONTAL_ALIGNMENT_LEFT, -1, 17).x
   _rounded(Rect2(center - Vector2(text_width / 2.0 + 4, 11), Vector2(text_width + 8, 22)), Color(INK, 0.9), 4)
   _center(countdown, center + Vector2(0, 6), 17, PAPER)
  if shortage_timer > 0 and cost > battle.stamina:
   draw_arc(center, radius, 0, TAU, 64, Color(VERMILION, shortage_timer / 0.42), 1.5, true)
  if battle.skill_is_active(skill):
   var active_text := active_skill_text(skill)
   if not active_text.is_empty():
    _rounded(Rect2(center - Vector2(21, 9), Vector2(42, 20)), Color(INK, 0.88), 4)
    _center(active_text, center + Vector2(0, 5), 13, PAPER)
  if hover: hovered_skill = skill


func _draw_tile(rect: Rect2, skill: String, binding: String, selected := false) -> void:
 var hover := rect.has_point(pointer)
 var empty := skill.is_empty()
 var cost: int = battle.skill_cost(skill)
 var available: bool = battle.stamina >= cost
 draw_rect(rect, Color("dedfce") if empty else Color("e6e2d1"))
 if not empty:
  if icons.has(skill):
   draw_texture_rect(icons[skill], rect.grow(-2), false, Color(1, 1, 1, 1 if available else 0.4))
  else:
   _draw_step_icon(rect.get_center(), JADE if available else MUTED)
 else:
  var center := rect.get_center()
  draw_line(center - Vector2(7, 0), center + Vector2(7, 0), Color(LINE, 0.6), 1)
  draw_line(center - Vector2(0, 7), center + Vector2(0, 7), Color(LINE, 0.6), 1)
 draw_rect(rect, JADE if selected or hover else Color(LINE, 0.7), false, 2 if selected or hover else 1)
 # Key tabs stay readable even against a dark ink illustration.
 var key_width := 32.0 if binding.length() > 1 else 23.0
 draw_rect(Rect2(rect.position + Vector2(-1, -1), Vector2(key_width, 20)), INK if not empty else MUTED)
 _center(binding, rect.position + Vector2(key_width / 2.0 - 1, 13), 12, PAPER)
 if not empty:
  var badge := rect.end - Vector2(10, 10)
  draw_circle(badge, 10, JADE if available else VERMILION, true, -1, true)
  _center(str(cost), badge + Vector2(0, 4), 12, PAPER)
  var remaining: float = battle.skill_cooldown_remaining(skill)
  if remaining > 0:
   draw_rect(rect.grow(-2), Color(INK, 0.62))
   _center(cooldown_text(remaining), rect.get_center() + Vector2(0, 7), 22, PAPER)
  elif battle.skill_is_active(skill):
   var active_text := active_skill_text(skill, true)
   if not active_text.is_empty():
    draw_rect(rect.grow(-2), Color(INK, 0.5))
    _center(active_text, rect.get_center() + Vector2(0, 5), 16, PAPER)
  if hover: hovered_skill = skill
 _center(skill if not empty else "空槽", Vector2(rect.get_center().x, rect.end.y + 22), 16, INK if not empty else MUTED)


func active_skill_text(skill: String, in_dialog := false) -> String:
 if in_dialog: return "施放中"
 if skill == "拳掌" and battle.fist_combo != null and battle.fist_combo.active: return "%d/4" % (battle.fist_combo.stage + 1)
 return "运功" if skill == battle.pending_self_skill else "施放"

func cooldown_text(remaining: float) -> String:
 if remaining <= 0: return ""
 # Round upward so a still-cooling skill never misleadingly displays 0.0.
 if remaining < 10:
  return "%.1f" % maxf(0.1, ceilf(remaining * 10.0 - 0.00001) / 10.0)
 return str(ceili(remaining))


func _draw_step_icon(center: Vector2, tint: Color) -> void:
 # A simple movement mark for the existing dodge; no new skill art or keywords.
 draw_arc(center, 20, -PI * 0.75, PI * 0.65, 32, Color(tint, 0.3), 2, true)
 draw_polyline(PackedVector2Array([center + Vector2(-9, -16), center + Vector2(6, -5), center + Vector2(-5, 5), center + Vector2(10, 16)]), tint, 4, true)
 draw_line(center + Vector2(-20, -3), center + Vector2(-10, -3), tint, 2, true)
 draw_line(center + Vector2(10, 4), center + Vector2(22, 4), tint, 2, true)


func skill_description(skill: String) -> Array:
 # Read timing from the same identity used for payment and the cooldown fan.
 var cd := ("%.2f" % battle.skill_cooldown_duration(skill)).trim_suffix("0").trim_suffix("0").trim_suffix(".")
 var descriptions := {
  "痛击": ["主动技能 · 敌方", "1体力，延迟0.20秒，冷却%s秒。" % cd, "向鼠标方向攻击前方半圆，半径%s单位。" % String.num(float(battle.SKILL_RULES["痛击"].reach), 0)],
  "架招": ["主动技能 · 自身", "命中前施放，化解本次物理伤害。", "判定0.20秒，冷却%s秒，空放扣费。" % cd],
  "错步": ["基础闪避 · 自身", "沿移动方向快速位移，短暂无敌。", "无方向时后撤；无敌0.15秒，冷却%s秒。" % cd],
  "催血": ["主动技能 · 自身 · 生命", "运功1秒后获得气血2层，冷却%s秒。" % cd, "运功期间移速50%；错步可中断。", "中断不退体力与冷却；气血不自动回落。"],
  "聚炁": ["主动技能 · 自身 · 周天", "运功1秒后获得炁2，冷却%s秒。" % cd, "运功期间移速50%；错步可中断。", "中断不退体力与冷却。", "炁达法力上限两倍时清炁、回满法力。"],
 }
 var lines: Array = descriptions.get(skill, [])
 if skill == "拳掌":
  battle.ensure_fist_combo()
  lines = ["基础攻击 · 四段连招 · 前方敌人", "连点或按住左键续接，朝鼠标释放，完整2.2秒。", "各式伤害与半径按当前体魄计算。"]
  for index in range(4):
   var values: Dictionary = battle.fist_combo.preview(index)
   lines.append("%s：基础伤害%d／半径%s" % [battle.fist_combo.STAGES[index].name, values.damage, String.num(values.reach, 1)])
  lines.append("第2／4式贴步8／12；错步可随时中断。")
  lines.append("续接窗口0.30秒，整套结束后冷却%s秒。" % cd)
 if not lines.is_empty():
  var floor_text := String.num(battle.skill_minimum_cooldown(skill), 2)
  lines.append("结束或中断后起算，最低%s秒。" % floor_text)
  lines.append(battle.cooldown_formula_text(skill))
 return lines

func _draw_tooltip(skill: String) -> void:
 var lines := skill_description(skill)
 var height := 56.0 + 24.0 * lines.size()
 var width := 380.0
 for line in lines: width = maxf(width, font.get_string_size(str(line), HORIZONTAL_ALIGNMENT_LEFT, -1, 14).x + 36)
 width = minf(width, canvas_size.x - 52)
 var rect := Rect2(canvas_size.x - width - 26, dock_rect().position.y - height - 17, width, height)
 _paper(rect)
 _text(skill, rect.position + Vector2(18, 30), 22)
 _right("体力 %d" % battle.skill_cost(skill), rect.position + Vector2(width - 18, 28), 14, JADE)
 for index in range(lines.size()):
  _text(str(lines[index]), rect.position + Vector2(18, 54 + 24 * index), 14, MUTED if index == 0 else INK)


func _dialog(title: String, subtitle: String, height := 300.0) -> Vector2:
 draw_rect(Rect2(Vector2.ZERO, canvas_size), Color(INK, 0.50))
 var origin := canvas_size / 2.0 - Vector2(280, 150)
 _paper(Rect2(origin, Vector2(560, height)), 1.0)
 draw_rect(Rect2(origin, Vector2(3, height)), JADE)
 _text(title, origin + Vector2(28, 39), 25)
 _text(subtitle, origin + Vector2(28, 68), 14, MUTED)
 _button(close_rect(), "关闭")
 draw_line(origin + Vector2(28, 83), origin + Vector2(532, 83), Color(LINE, 0.45), 1)
 return origin


func _draw_configuration() -> void:
 var origin := _dialog("调整招式", "先选槽位，再选下方招式装配；选择两格可交换。", 370.0)
 for index in range(5):
  var rect := slot_rect(index, true)
  _center(str(index + 1), Vector2(rect.get_center().x, rect.position.y - 12), 14, MUTED)
  _draw_tile(rect, str(battle.skill_slots[index]), KEYS[index], battle.selected_slot == index)
 _center("请选择第一格" if battle.selected_slot < 0 else "已选第 %d 格 · 选择招式装配，或选择槽位交换" % (battle.selected_slot + 1), origin + Vector2(280, 251), 15, JADE)
 _center("数字键 1—5 选择槽位；装配不重置冷却。", origin + Vector2(280, 278), 13, MUTED)
 var choices: Array[String] = battle.configurable_skills()
 for index in range(choices.size()):
  _button(available_skill_rect(index), choices[index] if not choices[index].is_empty() else "卸下")
 _center("Tab / Esc  返回战斗", origin + Vector2(280, 350), 13, MUTED)


func _draw_help() -> void:
 var origin := _dialog("战斗操作", "自由移动，朝鼠标方向出手。", 300.0)
 var rows := [
  ["W A S D", "移动", "左键", "四段拳掌"],
  ["空格", "错步 / 无方向后撤", "右键 / Q E R F", "主动技能"],
  ["Tab", "调整技能槽", "Esc", "返回首页"],
  ["G / H", "外观对照 / 切换衣装", "T", "战斗结束后重试"],
  ["9", "持剑 / 收剑", "0", "试挥（无判伤）"],
 ]
 for index in range(rows.size()):
  var y := origin.y + 115 + index * 35
  _text(rows[index][0], Vector2(origin.x + 28, y), 14, JADE)
  _text(rows[index][1], Vector2(origin.x + 111, y), 14)
  _text(rows[index][2], Vector2(origin.x + 296, y), 13, JADE)
  _text(rows[index][3], Vector2(origin.x + 410, y), 14)
 _text("方向键也可移动；朝鼠标方向出招。", origin + Vector2(28, 280), 13, MUTED)


func _draw_result() -> void:
 draw_rect(Rect2(Vector2.ZERO, canvas_size), Color(INK, 0.58))
 var rect := Rect2(canvas_size / 2.0 - Vector2(200, 100), Vector2(400, 200))
 _paper(rect, 1.0)
 _center("试演结束", rect.position + Vector2(200, 37), 14, MUTED)
 _center("胜" if battle.enemy_hp <= 0 else "负", rect.position + Vector2(200, 100), 48, JADE if battle.enemy_hp <= 0 else VERMILION)
 _center("T  再试一次       Esc  返回角色" if battle.attribute_runtime != null else "T  再试一次       Esc  返回首页", rect.position + Vector2(200, 162), 16)
