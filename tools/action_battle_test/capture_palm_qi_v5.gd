extends SceneTree

func _initialize():
 call_deferred("capture")

func capture():
 root.size = Vector2i(1280, 720)
 var scene = load("res://ActionBattleFeelTest.tscn").instantiate()
 scene.original_wanderer_enabled = true
 scene.original_wanderer_art_folder = "yuxu_fullbody_v15"
 root.add_child(scene)
 DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
 DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
 scene.set_process(false)
 scene.player = Vector2(1500, 950)
 scene._update_trial_camera()
 var output = ProjectSettings.globalize_path("res://../../output/palm_qi_v5")
 DirAccess.make_dir_recursive_absolute(output)
 await process_frame
 var frame_number = 0
 scene.strike_visual_rng.seed = 17413
 for case_index in range(7):
  var skill = "拳掌" if case_index < 4 else ("痛击" if case_index == 4 else "架招")
  var direction = Vector2.RIGHT
  scene.hp = 100
  scene.enemy_hp = 100
  scene.stamina = 6
  scene.enemy = scene.player + direction * 120.0
  scene.wolf_state = "recover"
  scene.wolf_clock = 100.0
  scene.pending_attack = ""
  scene.release_timer = 0.0
  scene.guard_window = 0.0
  scene.guard_fx.clear()
  scene.strike_fx.clear()
  for name in scene.skill_cooldowns: scene.skill_cooldowns[name] = 0.0
  scene.use_skill(skill)
  if skill == "架招":
   scene.guard_fx[-1].dir = direction
  else:
   scene.attack_direction = direction
   scene.update_skill_timers(0.081 if skill == "拳掌" else 0.201)
  for step in range(14):
   if step > 0:
    scene.update_strike_fx(1.0 / 30.0)
    scene.update_guard_fx(1.0 / 30.0)
    scene.guard_window = maxf(0.0, scene.guard_window - 1.0 / 30.0)
   if case_index == 6 and step == 2:
    scene.enemy = scene.player + direction * 65.0
    scene.wolf_state = "lunge"
    scene.wolf_direction = -direction
    scene.wolf_clock = 0.20
    scene.wolf_contact_resolved = false
    scene.update_wolf(0.001, scene.player)
   scene.visual_attack_time = minf(step / 30.0, 0.166) if skill != "架招" and step < 6 else -1.0
   scene.motion_time += 1.0 / 30.0
   scene.visual_motion_delta = 1.0 / 30.0
   scene.facing = direction.x
   scene.update_skinned_player()
   scene.queue_redraw()
   scene.battle_hud.queue_redraw()
   await process_frame
   RenderingServer.force_draw(false)
   var image = root.get_texture().get_image()
   assert(image.save_png(output.path_join("motion_%04d.png" % frame_number)) == OK)
   if step == 3:
    assert(image.save_png(output.path_join("case_%d.png" % case_index)) == OK)
   frame_number += 1
 print("PALM_QI_V5_CAPTURE ", frame_number, " actual window frames")
 scene.queue_free()
 await process_frame
 quit()
