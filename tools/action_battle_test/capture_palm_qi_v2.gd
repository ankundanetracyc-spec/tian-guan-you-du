extends SceneTree

func _initialize():
 call_deferred("capture")

func capture():
 root.size = Vector2i(1280, 720)
 var scene = load("res://ActionBattleFeelTest.tscn").instantiate()
 scene.original_wanderer_enabled = true
 scene.original_wanderer_art_folder = "yuxu_unified_v14"
 root.add_child(scene)
 DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
 scene.set_process(false)
 scene.player = Vector2(1500, 950)
 scene._update_trial_camera()
 var output = ProjectSettings.globalize_path("res://../../output/palm_qi_v2_final")
 DirAccess.make_dir_recursive_absolute(output)
 await process_frame
 var directions = [Vector2.RIGHT, Vector2.LEFT, Vector2(0.7, -0.7).normalized(), Vector2(0, 1)]
 var frame_number = 0
 for skill in ["拳掌", "痛击"]:
  for direction in directions:
   scene.hp = 100
   scene.enemy_hp = 100
   scene.stamina = 6
   scene.enemy = scene.player + direction * 120.0
   scene.wolf_state = "recover"
   scene.wolf_clock = 100.0
   scene.wolf_contact_resolved = false
   scene.pending_attack = ""
   scene.release_timer = 0.0
   scene.basic_strike_effect = 0.0
   for name in scene.skill_cooldowns: scene.skill_cooldowns[name] = 0.0
   scene.use_skill(skill)
   scene.attack_direction = direction
   for step in range(24):
    scene._process(1.0 / 30.0)
    scene.facing = -1.0 if direction.x < 0 else 1.0
    scene.update_skinned_player()
    scene.queue_redraw()
    scene.battle_hud.queue_redraw()
    await process_frame
    RenderingServer.force_draw(false)
    var image = root.get_texture().get_image()
    assert(image.save_png(output.path_join("motion_%04d.png" % frame_number)) == OK)
    if step == (5 if skill == "拳掌" else 9):
     assert(image.save_png(output.path_join("%s_%d.png" % ["fist" if skill == "拳掌" else "heavy", directions.find(direction)])) == OK)
    frame_number += 1
 print("PALM_QI_V2_CAPTURE ", frame_number, " actual window frames")
 scene.queue_free()
 await process_frame
 quit()
