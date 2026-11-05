extends SceneTree
const RULES = preload("res://tools/action_battle_test/action_manual_rules.gd")
var checks := 0
var failures := 0
var capture := false
var evidence := "res://docs/美术规范/百骸云息篇即时对练_20261104"

func _initialize() -> void:
 capture = "--capture" in OS.get_cmdline_user_args()
 call_deferred("verify")

func check(condition: bool, message: String) -> void:
 checks += 1
 if not condition:
  failures += 1
  push_error(message)

func frames(count := 2) -> void:
 for index in range(count): await process_frame

func picture(name: String) -> void:
 if not capture: return
 await frames(3)
 await RenderingServer.frame_post_draw
 DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(evidence))
 var result := root.get_texture().get_image().save_png(evidence.path_join(name + ".png"))
 check(result == OK, "保存实机画面 " + name)

func choose(control: OptionButton, index: int) -> void:
 control.select(index)
 control.emit_signal("item_selected", index)

func verify() -> void:
 root.size = Vector2i(1280, 720)
 root.content_scale_size = Vector2i(1280, 720)
 root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
 root.position = Vector2i(-4096, -4096)
 root.gui_disable_input = true
 var menu: Node = load("res://tscn/scene/StartMenu.tscn").instantiate()
 root.add_child(menu)
 current_scene = menu
 await frames()
 menu.get_node("StartMenuMenu/DemoToggle").emit_signal("pressed")
 var entry: Button = menu.find_child("ActionManualBattle", true, false)
 check(entry != null and entry.text.contains("自选"), "首页提供明确的新版秘籍自选入口")
 if entry == null:
  quit(1)
  return
 await picture("menu")
 entry.emit_signal("pressed")
 await frames(3)
 var setup: Control = current_scene
 check(setup.name == "ActionManualBattleSetup", "入口先打开双方自选配置")
 check(setup.enemy_count.selected == 0, "自选入口默认一名陪练")
 check(setup.sides.has("player") and setup.sides.has("enemy"), "双方各有独立秘籍、卷章、心法、技能与化法选择")
 var width := setup.size.x
 var last_skill: OptionButton = setup.sides.enemy.skills[4]
 check(last_skill.get_global_rect().end.x <= width - 20, "双方五个主动槽完整位于窗口内")
 if last_skill.get_global_rect().end.x > width - 20:
  for node in setup.find_children("*", "Control", true, false):
   if node.get_combined_minimum_size().x > 600:
    print("LAYOUT_DIAG ", node.name, " min=", node.get_combined_minimum_size(), " rect=", node.get_global_rect())
 check(setup.read_side("player").bindings.all(func(value: String): return value.is_empty()), "学会卷章不自动绑定化法")
 var player: Dictionary = setup.sides.player
 var enemy: Dictionary = setup.sides.enemy
 check(player.bindings[3].get_item_text(1) == "拳掌" and player.bindings[3].get_parent().get_child(0).text == "卷4 · 拳掌", "第四卷化法选项与标题均使用拳掌正式名称")
 choose(player.chapter, 0)
 check(RULES.SKILL not in player.skills[4].get_meta("identities"), "第一卷不可装配尚未习得的赤潮奔涌")
 check(player.bindings[1].disabled and player.bindings[3].disabled, "后续卷化法须先习得")
 choose(player.chapter, 3)
 var choices: Array = player.skills[4].get_meta("identities")
 choose(player.skills[4], choices.find(RULES.SKILL))
 for binding in player.bindings: binding.select(1)
 choose(player.role, 2)
 check(player.bindings.all(func(control: OptionButton): return control.disabled), "卸下心法暂停全部秘籍化法")
 check(setup.read_side("player").skills[4] == RULES.SKILL, "卸下心法仍保留已习得招式")
 choose(player.role, 1)
 check(not player.bindings[3].disabled and player.bindings[3].selected == 1, "换为辅修恢复原化法绑定")
 choose(player.skills[0], player.skills[0].get_meta("identities").find("架招"))
 check(setup.start_button.disabled, "重复装配同一招式会阻止开战")
 choose(player.skills[0], player.skills[0].get_meta("identities").find("痛击"))
 choose(player.role, 0)
 choose(enemy.book, 1)
 check(setup.start_button.disabled and setup.validation.text.contains("敌方需云息"), "凡人不可借自选入口越阶使用云息秘籍")
 choose(enemy.realm, 1)
 choose(enemy.chapter, 1)
 choose(enemy.role, 1)
 var enemy_choices: Array = enemy.skills[4].get_meta("identities")
 choose(enemy.skills[4], enemy_choices.find(RULES.SKILL))
 enemy.bindings[0].select(1)
 enemy.bindings[1].select(1)
 setup.enemy_count.select(1)
 check(not setup.start_button.disabled, "双方合法选择可以开战")
 await frames(3)
 check(enemy.skills[4].get_global_rect().end.x <= setup.size.x - 20, "双方选入四字招式后仍不横向溢出")
 await picture("setup")
 setup.start_button.emit_signal("pressed")
 await frames(3)
 var arena: Node2D = current_scene
 check(arena.name == "ActionManualBattleScene", "自选配置进入实际即时战斗场景")
 check(arena.npcs.size() == 2 and arena.attribute_runtime.get_unit_count() == 3, "所选两名敌人对应两份真实快照")
 check(arena.action_manual.chapter == 4 and arena.action_manual.role == 1, "我方卷章和主修正确带入")
 check(arena.action_manual.rewrite_active(3), "我方选定的拳掌距离化法实际生效")
 check(arena.npcs.all(func(npc: Node2D): return npc.action_manual.chapter == 2 and npc.action_manual.role == 2), "敌方卷章和辅修配置分别带入NPC")
 check(arena.npcs.all(func(npc: Node2D): return RULES.SKILL in npc.skill_slots), "NPC可使用所选专属技能")
 check(arena.simulation_paused and arena.qi_blood == 0, "开战先准备，气血不因选书自动生成")
 await picture("ready")
 var key := InputEventKey.new()
 key.pressed = true
 key.keycode = KEY_P
 arena._input(key)
 for frame in range(35): await process_frame
 check(not arena.simulation_paused, "P启动双方实时战斗")
 check(arena.npcs.any(func(npc: Node2D): return npc.pending_self_skill == "催血" or npc.qi_blood > 0), "NPC按同一施放流程运功获得气血")
 arena.set_simulation_paused(true)
 var before: Array = arena.npcs.map(func(npc: Node2D): return [npc.player, npc.qi_blood, npc.self_cast_remaining])
 await frames(5)
 check(arena.npcs.map(func(npc: Node2D): return [npc.player, npc.qi_blood, npc.self_cast_remaining]) == before, "暂停冻结所有NPC位置、运功及资源")
 arena.configuring = true
 var selected_skill: String = arena.skill_slots[4]
 var duration: float = arena.skill_cooldown_remaining(RULES.SKILL)
 check(arena.assign_skill_slot(0, RULES.SKILL), "局内暂停装配支持新招式换位")
 check(arena.skill_slots[0] == selected_skill and arena.skill_cooldown_remaining(RULES.SKILL) == duration, "换位跟随技能身份，不刷新CD")
 arena.configuring = false
 key.keycode = KEY_T
 arena._input(key)
 check(arena.simulation_paused and arena.npcs.all(func(npc: Node2D): return npc.qi_blood == 0), "T同组重开清空双方派生资源")
 key.keycode = KEY_C
 arena._input(key)
 await frames(3)
 check(current_scene.name == "ActionManualBattleSetup", "C返回配置页可重新选择")
 setup = current_scene
 check(setup.enemy_count.selected == 1 and setup.read_side("player").chapter == 4, "重新配置保留同会话选择")
 setup.get_node_or_null("BackToMenu")
 var back: Button = setup.find_child("BackToMenu", true, false)
 back.emit_signal("pressed")
 await frames(3)
 check(current_scene.name == "StartMenu", "配置页可安全返回首页")
 print("MANUAL_BATTLE_MENU_CHECKS ", checks, " failures=", failures)
 quit(1 if failures else 0)
