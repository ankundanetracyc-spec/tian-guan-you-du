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
 var output = ProjectSettings.globalize_path("res://../../output/palm_qi_v4")
 DirAccess.make_dir_recursive_absolute(output)
 await process_frame
 var frame_number = 0
 for skill in ["拳掌", "痛击"]:
  for direction in [Vector2.RIGHT, Vector2.LEFT]:
   scene.hp = 100
   scene.enemy_hp = 100
   scene.stamina = 6
   scene.enemy = scene.player + direction * 120.0
   scene.wolf_state = "recover"
   scene.wolf_clock = 100.0
   scene.pending_attack = ""
   scene.release_timer = 0.0
   scene.strike_fx.clear()
   for name in scene.skill_cooldowns: scene.skill_cooldowns[name] = 0.0
   scene.use_skill(skill)
   scene.attack_direction = direction
   scene.update_skill_timers(0.081 if skill == "拳掌" else 0.201)
   for step in range(14):
    if step > 0: scene.update_strike_fx(1.0 / 30.0)
    scene.visual_attack_time = minf(step / 30.0, 0.166) if step < 6 else -1.0
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
     assert(image.save_png(output.path_join("%s_%s.png" % ["fist" if skill == "拳掌" else "palm", "right" if direction.x > 0 else "left"])) == OK)
    frame_number += 1
 print("PALM_QI_V4_CAPTURE ", frame_number, " actual window frames")
 scene.queue_free()
 await process_frame
 quit()
