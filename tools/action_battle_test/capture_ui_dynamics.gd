extends SceneTree

# Controlled capture of the real UI and real skill/recovery code; no sample buffs.
func _initialize() -> void:
 call_deferred("capture_demo")

func capture_demo() -> void:
 root.size = Vector2i(1280, 720)
 var standalone := ResourceLoader.exists("res://ActionBattleFeelTest.tscn")
 var scene_path := "res://ActionBattleFeelTest.tscn" if standalone else "res://tscn/scene/ActionBattleBilanExperimentScene.tscn"
 var scene: Node2D = load(scene_path).instantiate()
 root.add_child(scene)
 scene.set_process(false)
 scene.battle_hud.set_process(false)
 # Keep this a resource/cooldown study, without staging combat outcomes.
 scene.enemy = Vector2(10000, 10000)
 var output_dir := ProjectSettings.globalize_path("res://../../output/ui_dynamic_frames_20261103" if standalone else "res://output/ui_dynamic_frames_20261103")
 DirAccess.make_dir_recursive_absolute(output_dir)
 var ignore := FileAccess.open(output_dir.path_join(".gdignore"), FileAccess.WRITE)
 if ignore != null: ignore.close()
 var next_action := 0.5
 for frame in range(165):
  var time := frame / 15.0
  if time >= next_action and time < 8.0:
   scene.use_skill("架招")
   next_action += 0.7
  # Drive the actual game in two substeps; preserve its 0.05-second delta limit.
  for step in range(2):
   scene._process(1.0 / 30.0)
   scene.battle_hud._process(1.0 / 30.0)
  scene.queue_redraw()
  scene.battle_hud.queue_redraw()
  await process_frame
  await RenderingServer.frame_post_draw
  var screenshot := root.get_texture().get_image()
  # Two unscaled viewport crops expose resource motion and actual skill cooldowns.
  var detail := Image.create(840, 205, false, screenshot.get_format())
  detail.fill(Color("eee9d9"))
  detail.blit_rect(screenshot, Rect2i(0, 0, 360, 205), Vector2i.ZERO)
  detail.blit_rect(screenshot, Rect2i(800, 605, 480, 115), Vector2i(360, 45))
  var error := detail.save_png(output_dir.path_join("frame_%03d.png" % frame))
  if error != OK:
   push_error("动态预览帧保存失败")
   quit(1)
   return
 print("UI_DYNAMIC_CAPTURE_COMPLETE ", output_dir)
 scene.queue_free()
 await process_frame
 quit()
