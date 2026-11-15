extends SceneTree

var count := 0
var failures := 0


func _initialize() -> void:
 call_deferred("run_checks")


func check(value: bool, label: String) -> void:
 count += 1
 if not value:
  failures += 1
  push_error(label)


func key_input(code: Key) -> void:
 var event := InputEventKey.new()
 event.keycode = code
 event.physical_keycode = code
 event.pressed = true
 Input.parse_input_event(event)
 Input.flush_buffered_events()
 event = InputEventKey.new()
 event.keycode = code
 event.physical_keycode = code
 Input.parse_input_event(event)
 Input.flush_buffered_events()


func run_checks() -> void:
 var source := root.get_node("GameStartProfile")
 var catalog := root.get_node("AppearanceModCatalog")
 var before: Resource = source.call("create_player_appearance_preview")
 var original_clothing := int(before.get("appearance_clothes_style"))
 var original_seed := int(before.get("appearance_seed"))
 var scene: Node2D = load("res://tscn/scene/TopDownBattleTrialScene.tscn").instantiate()
 root.add_child(scene)
 current_scene = scene
 scene.set_process(false)
 await process_frame
 var entries: Array = catalog.call("get_parts", "male", "clothes")
 for profile: Dictionary in scene.avatar.library.PROFILES:
  var matches := false
  for entry: Dictionary in entries:
   if str(entry.id) == str(profile.id) and str(entry.package_id) == scene.avatar.library.PACKAGE:
    matches = str(entry.resource_path) == scene.avatar.library.source_path(str(profile.id)) and str(entry.display_name) == str(profile.name)
  check(matches, "俯视映射与活动衣着目录ID、名称和文件一致 " + str(profile.id))
 key_input(KEY_V)
 check(scene.clothing_open, "V打开衣着试装")
 scene.apply_clothing("male_clothes_02")
 var after: Resource = source.call("create_player_appearance_preview")
 check(int(after.get("appearance_clothes_style")) == original_clothing and int(after.get("appearance_seed")) == original_seed, "试装不改建角外观或随机身份")
 key_input(KEY_ESCAPE)
 check(not scene.clothing_open and current_scene == scene, "Esc先关闭衣着试装")
 check(scene.hp == 100 and scene.enemy_hp == 100 and scene.stamina == 6, "衣着预览不消耗战斗资源")
 print("TOPDOWN_CATALOG_CHECKS ", count, " failures=", failures)
 scene.saved_window_state.clear()
 scene.queue_free()
 await process_frame
 quit(1 if failures else 0)
