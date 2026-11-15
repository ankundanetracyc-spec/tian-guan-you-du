extends SceneTree
const RULES = preload("res://tools/action_battle_test/action_manual_rules.gd")
var arena: Node2D
var checks := 0
var failures := 0
var folder := "res://docs/美术规范/百骸云息篇即时对练_20261104"
var frames_folder := "res://output/baihai_yunxi_capture_20261104"

func _initialize() -> void:
 call_deferred("capture")

func check(condition: bool, message: String) -> void:
 checks += 1
 if not condition:
  failures += 1
  push_error(message)

func frame_to(name: String) -> void:
 arena.queue_redraw()
 arena.battle_hud.queue_redraw()
 await process_frame
 await RenderingServer.frame_post_draw
 var frame_image := root.get_texture().get_image()
 var destination := folder
 if name.begins_with("frame_"):
  destination = frames_folder
  frame_image.resize(1280, 720, Image.INTERPOLATE_LANCZOS)
 check(frame_image.save_png(destination.path_join(name + ".png")) == OK, "保存" + name)

func capture() -> void:
 root.size = Vector2i(1280, 720)
 root.content_scale_size = Vector2i(1280, 720)
 root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
 root.position = Vector2i(-4096, -4096)
 root.gui_disable_input = true
 DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(folder))
 DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(frames_folder))
 var player := RULES.default_side()
 player.bindings = RULES.REWRITE_TARGETS.duplicate()
 player.custom_attributes = true
 player.health = 350
 var enemy := RULES.default_side(false)
 enemy.book = true
 enemy.chapter = 2
 enemy.role = 2
 enemy.realm = "云息一层"
 enemy.skills[4] = RULES.SKILL
 enemy.bindings = ["催血", "架招", "", ""]
 enemy.custom_attributes = true
 enemy.health = 300
 arena = load("res://tscn/scene/ActionManualBattleScene.tscn").instantiate()
 arena.manual_battle_setup = {"player":player, "enemy":enemy, "enemy_count":2}
 root.add_child(arena)
 current_scene = arena
 arena.set_process(false)
 arena.battle_hud.set_process_input(false)
 await process_frame
 if "--details-only" in OS.get_cmdline_user_args():
  arena.set_simulation_paused(false)
  arena.battle_hud._update_layout()
  root.warp_mouse(arena.battle_hud.slot_rect(4).get_center() * arena.battle_hud.ui_scale)
  await frame_to("skill_detail")
  check(not "\n".join(arena.battle_hud.skill_description(RULES.SKILL)).contains("%%"), "技能详情使用正常百分号")
  print("MANUAL_SKILL_DETAIL_CHECKS ", checks, " failures=", failures)
  arena.queue_free()
  await process_frame
  quit(1 if failures else 0)
  return
 arena.set_simulation_paused(false)
 var failed_key := InputEventKey.new()
 failed_key.pressed = true
 failed_key.keycode = KEY_F
 arena._input(failed_key)
 check(arena.battle_hud.skill_failure_text().contains("气血不足"), "缺血施放时实际显示失败原因")
 await frame_to("activation_blocked")
 var prepared := false
 var released := false
 var shown_wave := false
 var shown_blood := false
 var frames_manifest: Array[String] = []
 var delta := 1.0 / 30.0
 seed(1703)
 for frame in range(210):
  arena.set_simulation_paused(false)
  var nearest: Node2D
  for npc in arena.npcs:
   if npc.hp <= 0: continue
   if nearest == null or arena.player.distance_squared_to(npc.player) < arena.player.distance_squared_to(nearest.player): nearest = npc
  if nearest != null:
   var point: Vector2 = nearest.player - Vector2(0, arena.attack_plane_height())
   root.warp_mouse(arena.get_global_transform_with_canvas() * point)
  if frame == 0:
   var key := InputEventKey.new()
   key.pressed = true
   key.keycode = KEY_E
   arena._input(key)
  if not prepared and frame > 30 and arena.pending_self_skill.is_empty():
   var key := InputEventKey.new()
   key.pressed = true
   key.keycode = KEY_F
   arena._input(key)
   prepared = arena.action_manual.buff_remaining > 0.0
   if prepared: await frame_to("duration_started")
  if prepared and not released:
   var mouse := InputEventMouseButton.new()
   mouse.button_index = MOUSE_BUTTON_LEFT
   mouse.pressed = true
   arena._input(mouse)
   released = arena.fist_combo != null and arena.fist_combo.active
  arena.held_basic_attack = released
  arena._process(delta)
  if not shown_blood and arena.manual_vfx != null and not arena.manual_vfx.marks.is_empty():
   await frame_to("blood_contact")
   shown_blood = true
  if not shown_wave:
   for wave in arena.active_waves:
    if wave.source == 0 and float(wave.age) >= 0.34:
     await frame_to("wave_combat")
     shown_wave = true
     break
  if frame % 3 == 0:
   var filename := "frame_%03d" % frame
   await frame_to(filename)
   frames_manifest.append(filename + ".png")
  if arena.hp <= 0 or arena.enemy_hp <= 0: break
 check(prepared and released, "捕获经正常催血、付费降血与左键连招推进")
 check(shown_blood and shown_wave, "实际渲染同时包含被动血气与末段血浪")
 check(arena.wave_release_count > 0, "实战流程实际释放血浪")
 check(arena.damage_events.any(func(event: Dictionary): return event.kind == "血浪" and event.get("hit", false)), "血浪在实际NPC战斗中造成命中")
 check(arena.damage_events.any(func(event: Dictionary): return event.kind == "击退"), "实际战斗进入独立击退判定")
 for step in range(4): arena._process(delta)
 check(arena.action_manual.buff_remaining == 0 and arena.skill_cooldown_remaining(RULES.SKILL) > 23, "实际窗口强化结束后显示24秒冷却")
 await frame_to("cooldown")
 var manifest := FileAccess.open(folder.path_join("capture_manifest.json"), FileAccess.WRITE)
 manifest.store_string(JSON.stringify({"frames":frames_manifest, "frames_dir":frames_folder, "seconds_per_frame":0.10,
  "player":player, "enemy":enemy, "enemy_count":2, "checks":checks, "failures":failures}, "  "))
 manifest.close()
 arena.configuring = true
 await frame_to("skills")
 arena.configuring = false
 arena.battle_hud.help_open = true
 await frame_to("help")
 print("MANUAL_BATTLE_CAPTURE_CHECKS ", checks, " failures=", failures)
 arena.queue_free()
 await process_frame
 quit(1 if failures else 0)
