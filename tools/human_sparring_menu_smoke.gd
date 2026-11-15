extends SceneTree

var checks := 0
var failures := 0

func _initialize() -> void:
 call_deferred("verify")

func check(value: bool, label: String) -> void:
 checks += 1
 if not value:
  failures += 1
  push_error(label)

func verify() -> void:
 var menu: Node = load("res://tscn/scene/StartMenu.tscn").instantiate()
 root.add_child(menu)
 current_scene = menu
 await process_frame
 menu.get_node("StartMenuMenu/DemoToggle").emit_signal("pressed")
 var entry: Button = menu.find_child("ActionHumanSparring", true, false)
 check(entry != null and entry.text.contains("一对一"), "首页有明确的人类NPC一对一入口")
 if entry != null:
  entry.emit_signal("pressed")
  await process_frame
  await process_frame
  var arena: Node = current_scene
  check(arena != null and arena.name == "ActionHumanSparringScene", "入口打开实际一对三场景")
  if arena != null and arena.name == "ActionHumanSparringScene":
   check(arena.npcs.size() == 1 and arena.attribute_runtime.get_unit_count() == 2, "正式入口包含一名NPC及两份快照")
   check(arena.simulation_paused and arena.hp == 100 and arena.mana_capacity == 3, "进入先准备，玩家保留已有样例资源")
   check(arena.npcs.all(func(npc): return npc.character_name != "未命名" and npc.hp > 0), "显示真实生成的NPC姓名与生命")
   var key := InputEventKey.new()
   key.keycode = KEY_P
   key.pressed = true
   arena._input(key)
   check(not arena.simulation_paused, "P可开始演练")
   arena._input(key)
   check(arena.simulation_paused, "P可暂停全部人物")
   key.keycode = KEY_ESCAPE
   arena.configuring = true
   arena._input(key)
   check(not arena.configuring and current_scene == arena, "Esc先关闭配置面板")
   arena._input(key)
   await process_frame
   await process_frame
   check(current_scene != null and current_scene.name == "StartMenu", "Esc释放四人演练后安全返回首页")
 print("HUMAN_SPARRING_MENU_CHECKS ", checks, " failures=", failures)
 quit(1 if failures else 0)
