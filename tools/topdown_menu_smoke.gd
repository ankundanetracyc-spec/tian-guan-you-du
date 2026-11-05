extends SceneTree

var failures := 0


func _initialize() -> void:
 call_deferred("run_checks")


func check(value: bool, message: String) -> void:
 if not value:
  failures += 1
  push_error(message)


func run_checks() -> void:
 var menu: Node = load("res://tscn/scene/StartMenu.tscn").instantiate()
 root.add_child(menu)
 current_scene = menu
 await process_frame
 menu.get_node("StartMenuMenu/DemoToggle").emit_signal("pressed")
 var entry: Button = menu.find_child("TopDownBattleTrial", true, false)
 check(entry != null, "首页试演与预览包含俯视试玩入口")
 if entry != null:
  entry.emit_signal("pressed")
  await process_frame
  await process_frame
  check(current_scene != null and current_scene.name == "TopDownBattleTrial", "点击入口进入独立俯视场景")
  if current_scene != null and current_scene.name == "TopDownBattleTrial":
   check(current_scene.attribute_runtime == null and current_scene.enemy_hp == 100, "入口使用隔离样例")
   var key := InputEventKey.new()
   key.keycode = KEY_ESCAPE
   key.pressed = true
   current_scene._input(key)
   await process_frame
   await process_frame
   check(current_scene != null and current_scene.name == "StartMenu", "Esc安全返回首页")
 print("TOPDOWN_MENU_CHECKS failures=", failures)
 quit(1 if failures > 0 else 0)
