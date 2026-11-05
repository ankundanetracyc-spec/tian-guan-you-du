extends Control
const RULES = preload("action_manual_rules.gd")
const ARENA_PATH := "res://tscn/scene/ActionManualBattleScene.tscn"
const REALMS := ["凡人", "云息一层", "玄基初期", "金丹初期", "神胎初期"]
const INK := Color("293c39")
const PAPER := Color("eee9d9")
var sides: Dictionary = {}
var enemy_count: OptionButton
var start_button: Button
var validation: Label
var saved: Dictionary = {}

func _ready() -> void:
 name = "ActionManualBattleSetup"
 set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
 var stored: Variant = get_tree().get_meta("action_manual_last_setup", {})
 saved = stored.duplicate(true) if stored is Dictionary else {}
 var background := ColorRect.new()
 background.color = Color("d9d5c7")
 background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
 add_child(background)
 var theme_data := Theme.new()
 theme_data.default_font_size = 17
 var font_path := "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"
 if ResourceLoader.exists(font_path): theme_data.default_font = load(font_path)
 for kind in ["Label", "Button", "OptionButton", "CheckButton", "LineEdit", "SpinBox"]:
  theme_data.set_color("font_color", kind, INK)
  theme_data.set_color("font_hover_color", kind, INK)
  theme_data.set_color("font_pressed_color", kind, INK)
 for kind in ["Button", "OptionButton", "LineEdit"]:
  theme_data.set_stylebox("normal", kind, paper_style())
  theme_data.set_stylebox("hover", kind, paper_style(Color("f5efdf")))
  theme_data.set_stylebox("pressed", kind, paper_style(Color("c9d8c7")))
 theme_data.set_stylebox("panel", "PanelContainer", paper_style())
 theme_data.set_stylebox("panel", "PopupMenu", paper_style())
 theme_data.set_color("font_color", "PopupMenu", INK)
 theme = theme_data
 var margin := MarginContainer.new()
 margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
 for side in ["left", "right", "top", "bottom"]: margin.add_theme_constant_override("margin_" + side, 24)
 add_child(margin)
 var root := VBoxContainer.new()
 root.add_theme_constant_override("separation", 12)
 margin.add_child(root)
 var heading := label("新版秘籍 · 即时对练", 27)
 heading.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
 root.add_child(heading)
 var description := label("选择双方的秘籍、卷章、招式与化法，再进入人类陪练。", 17)
 description.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
 root.add_child(description)
 var count_row := HBoxContainer.new()
 count_row.alignment = BoxContainer.ALIGNMENT_CENTER
 count_row.add_theme_constant_override("separation", 12)
 root.add_child(count_row)
 count_row.add_child(label("人类陪练人数"))
 enemy_count = selector(["1人", "2人", "3人"])
 enemy_count.name = "EnemyCount"
 enemy_count.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
 enemy_count.select(clampi(int(saved.get("enemy_count", 1)) - 1, 0, 2))
 enemy_count.custom_minimum_size.x = 100
 count_row.add_child(enemy_count)
 var columns := HBoxContainer.new()
 columns.add_theme_constant_override("separation", 18)
 columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
 root.add_child(columns)
 build_side(columns, "player", "我方", saved.get("player", RULES.default_side()))
 build_side(columns, "enemy", "敌方 · 同组陪练", saved.get("enemy", RULES.default_side(false)))
 validation = label("", 16)
 validation.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
 root.add_child(validation)
 var footer := HBoxContainer.new()
 footer.alignment = BoxContainer.ALIGNMENT_CENTER
 footer.add_theme_constant_override("separation", 18)
 root.add_child(footer)
 var back := Button.new()
 back.name = "BackToMenu"
 back.text = "返回首页"
 back.custom_minimum_size = Vector2(190, 44)
 back.pressed.connect(func(): get_tree().change_scene_to_file("res://tscn/scene/StartMenu.tscn"))
 footer.add_child(back)
 start_button = Button.new()
 start_button.name = "StartManualBattle"
 start_button.text = "进入对练"
 start_button.custom_minimum_size = Vector2(240, 44)
 start_button.pressed.connect(start_battle)
 footer.add_child(start_button)
 refresh_side("player")
 refresh_side("enemy")

func paper_style(color := PAPER) -> StyleBoxFlat:
 var style := StyleBoxFlat.new()
 style.bg_color = color
 style.border_color = Color("a0a08c")
 style.set_border_width_all(1)
 style.set_corner_radius_all(5)
 style.content_margin_left = 12
 style.content_margin_right = 12
 style.content_margin_top = 7
 style.content_margin_bottom = 7
 return style

func label(text: String, font_size := 17) -> Label:
 var result := Label.new()
 result.text = text
 result.add_theme_font_size_override("font_size", font_size)
 result.add_theme_color_override("font_color", INK)
 return result

func selector(options: Array) -> OptionButton:
 var result := OptionButton.new()
 result.size_flags_horizontal = Control.SIZE_EXPAND_FILL
 result.custom_minimum_size.y = 34
 result.clip_text = true
 result.fit_to_longest_item = false
 for option in options: result.add_item(str(option))
 return result

func add_row(parent: VBoxContainer, caption: String, control: Control) -> void:
 var row := HBoxContainer.new()
 row.add_theme_constant_override("separation", 9)
 var title := label(caption)
 title.custom_minimum_size.x = 92
 row.add_child(title)
 row.add_child(control)
 parent.add_child(row)

func build_side(parent: HBoxContainer, id: String, caption: String, settings: Dictionary) -> void:
 var panel := PanelContainer.new()
 panel.name = "PlayerOptions" if id == "player" else "EnemyOptions"
 panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
 panel.size_flags_stretch_ratio = 1.0
 parent.add_child(panel)
 var scroll := ScrollContainer.new()
 scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
 panel.add_child(scroll)
 var box := VBoxContainer.new()
 box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
 box.add_theme_constant_override("separation", 9)
 scroll.add_child(box)
 box.add_child(label(caption, 23))
 var book := selector(["无已学秘籍", RULES.BOOK_NAME])
 book.name = id + "_book"
 book.select(1 if bool(settings.get("book", false)) else 0)
 add_row(box, "已学秘籍", book)
 var chapter := selector(["第一卷 · 养络", "第二卷 · 行血", "第三卷 · 贯劲", "第四卷 · 归养"])
 chapter.name = id + "_chapter"
 chapter.select(clampi(int(settings.get("chapter", 4)) - 1, 0, 3))
 var role := selector(["主修", "辅修", "不装备心法"])
 role.name = id + "_role"
 role.select({1:0, 2:1, 0:2}.get(int(settings.get("role", 1)), 0))
 var realm := selector(REALMS)
 realm.name = id + "_realm"
 realm.select(maxi(0, REALMS.find(str(settings.get("realm", "云息一层")))))
 var selectors := HBoxContainer.new()
 selectors.add_theme_constant_override("separation", 9)
 box.add_child(selectors)
 for item in [["学至卷章", chapter], ["心法装备", role], ["境界", realm]]:
  var column := VBoxContainer.new()
  column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
  column.add_child(label(str(item[0]), 14))
  column.add_child(item[1])
  selectors.add_child(column)
 box.add_child(HSeparator.new())
 box.add_child(label("招式装配", 20))
 box.add_child(label("左键拳掌、空格错步固定；五个主动槽可自由选择。", 14))
 var skills: Array[OptionButton] = []
 var initial_skills: Array = settings.get("skills", [])
 var options := RULES.available_skills(settings)
 var captions := ["右键", "Q", "E", "R", "F"]
 var skill_row := HBoxContainer.new()
 skill_row.add_theme_constant_override("separation", 7)
 box.add_child(skill_row)
 for index in range(5):
  var skill := selector([])
  skill.name = id + "_skill_" + str(index)
  for option in options: skill.add_item(option if not option.is_empty() else "空槽")
  skill.select(maxi(0, options.find(str(initial_skills[index]) if index < initial_skills.size() else "")))
  skill.set_meta("identities", options)
  var slot_column := VBoxContainer.new()
  slot_column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
  var key := label(captions[index], 14)
  key.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
  slot_column.add_child(key)
  slot_column.add_child(skill)
  skill_row.add_child(slot_column)
  skills.append(skill)
 box.add_child(HSeparator.new())
 box.add_child(label("化法绑定", 20))
 var bindings: Array[OptionButton] = []
 var chosen_bindings: Array = settings.get("bindings", [])
 var rewrite_grid := GridContainer.new()
 rewrite_grid.columns = 2
 rewrite_grid.add_theme_constant_override("h_separation", 12)
 rewrite_grid.add_theme_constant_override("v_separation", 6)
 box.add_child(rewrite_grid)
 for index in range(4):
  var bind := selector(["未绑定", RULES.REWRITE_TARGETS[index]])
  bind.name = id + "_rewrite_" + str(index)
  bind.tooltip_text = RULES.REWRITE_TEXTS[index]
  bind.select(1 if index < chosen_bindings.size() and str(chosen_bindings[index]) == RULES.REWRITE_TARGETS[index] else 0)
  var bind_column := VBoxContainer.new()
  bind_column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
  bind_column.add_child(label("卷%d · %s" % [index + 1, RULES.REWRITE_TARGETS[index]], 14))
  bind_column.add_child(bind)
  rewrite_grid.add_child(bind_column)
  bindings.append(bind)
 var bind_all := Button.new()
 bind_all.text = "绑定当前可用化法"
 bind_all.pressed.connect(func():
  for bind in bindings:
   if not bind.disabled: bind.select(1)
  refresh_validation())
 box.add_child(bind_all)
 var summary := label("", 14)
 summary.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
 box.add_child(summary)
 box.add_child(HSeparator.new())
 var custom := CheckButton.new()
 custom.text = "自定义测试属性"
 custom.button_pressed = bool(settings.get("custom_attributes", false))
 box.add_child(custom)
 var attribute_box := VBoxContainer.new()
 attribute_box.visible = custom.button_pressed
 box.add_child(attribute_box)
 var attributes: Dictionary = {}
 for item in [["体魄", "physique", 8, 0, 9999], ["神魂", "soul", 8, 0, 9999], ["灵慧", "intelligence", 8, 0, 9999],
  ["生命上限", "health", 100, 1, 999999], ["法力容量", "mana", 3, 0, 30], ["额外承载抵抗%", "bearing_resistance_pp", 0, 0, 100]]:
  var value := SpinBox.new()
  value.min_value = item[3]
  value.max_value = item[4]
  value.step = 1.0 if str(item[1]) in ["health", "mana"] else 0.01
  value.value = float(settings.get(item[1], item[2]))
  value.size_flags_horizontal = Control.SIZE_EXPAND_FILL
  value.editable = custom.button_pressed
  add_row(attribute_box, str(item[0]), value)
  attributes[item[1]] = value
 custom.toggled.connect(func(pressed: bool):
  attribute_box.visible = pressed
  for field in attributes.values(): field.editable = pressed)
 var state := {"book":book, "chapter":chapter, "role":role, "realm":realm, "skills":skills,
  "bindings":bindings, "summary":summary, "custom":custom, "attributes":attributes}
 sides[id] = state
 for control in [book, chapter, role, realm]: control.item_selected.connect(func(_index: int): refresh_side(id))
 for skill in skills: skill.item_selected.connect(func(_index: int): refresh_side(id))
 for bind in bindings: bind.item_selected.connect(func(_index: int): refresh_validation())

func read_side(id: String) -> Dictionary:
 var state: Dictionary = sides[id]
 var skills: Array[String] = []
 for chooser in state.skills:
  var identities: Array = chooser.get_meta("identities", [])
  skills.append(str(identities[chooser.selected]) if chooser.selected >= 0 and chooser.selected < identities.size() else "")
 var bindings: Array[String] = []
 for index in range(4): bindings.append(RULES.REWRITE_TARGETS[index] if state.bindings[index].selected == 1 else "")
 var settings := {"book":state.book.selected == 1, "chapter":state.chapter.selected + 1,
  "role":[1, 2, 0][state.role.selected], "realm":REALMS[state.realm.selected],
  "skills":skills, "bindings":bindings, "custom_attributes":state.custom.button_pressed}
 for key in state.attributes: settings[key] = state.attributes[key].value
 return settings

func refresh_side(id: String) -> void:
 if not sides.has(id): return
 var state: Dictionary = sides[id]
 var settings := read_side(id)
 state.chapter.disabled = not settings.book
 state.role.disabled = not settings.book
 var allowed := RULES.available_skills(settings)
 for chooser in state.skills:
  var before: Array = chooser.get_meta("identities", [])
  var selected := str(before[chooser.selected]) if chooser.selected >= 0 and chooser.selected < before.size() else ""
  chooser.clear()
  for identity in allowed: chooser.add_item(identity if not identity.is_empty() else "空槽")
  chooser.set_meta("identities", allowed)
  chooser.select(allowed.find(selected) if selected in allowed else allowed.size() - 1)
 settings = read_side(id)
 for index in range(4):
  var target := str(RULES.REWRITE_TARGETS[index])
  state.bindings[index].disabled = not settings.book or int(settings.chapter) <= index or int(settings.role) == 0 or (index != 3 and target not in settings.skills)
 state.summary.text = "化法随绑定招式生效；卸下招式后暂停，绑定选择保留。"
 if int(settings.role) == 2 and settings.book: state.summary.text += "\n选择绑定同时启用该卷化法分支。"
 refresh_validation()

func refresh_validation() -> void:
 if start_button == null: return
 var problems: Array[String] = []
 for id in sides:
  var settings := read_side(id)
  var who := "我方" if id == "player" else "敌方"
  if settings.book and str(settings.realm) == "凡人": problems.append(who + "需云息及以上境界才能使用本经。")
  var nonempty: Array = settings.skills.filter(func(skill: String): return not skill.is_empty())
  var unique := {}
  for skill in nonempty: unique[skill] = true
  if unique.size() != nonempty.size(): problems.append(who + "主动槽不可重复装配同一招式。")
 start_button.disabled = not problems.is_empty()
 validation.text = "　" if problems.is_empty() else " ".join(problems)
 validation.add_theme_color_override("font_color", Color("8d3b3b"))

func start_battle() -> void:
 for state in sides.values():
  for field in state.attributes.values(): field.apply()
 refresh_validation()
 if start_button.disabled: return
 var setup := {"player":read_side("player"), "enemy":read_side("enemy"), "enemy_count":enemy_count.selected + 1}
 get_tree().set_meta("action_manual_last_setup", setup.duplicate(true))
 var arena: Node2D = load(ARENA_PATH).instantiate()
 arena.manual_battle_setup = setup
 hide()
 get_tree().root.add_child(arena)
 get_tree().current_scene = arena
 queue_free()

func _unhandled_key_input(event: InputEvent) -> void:
 if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
  get_viewport().set_input_as_handled()
  get_tree().change_scene_to_file("res://tscn/scene/StartMenu.tscn")
