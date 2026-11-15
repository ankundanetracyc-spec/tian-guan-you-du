extends SceneTree

class FixedArena extends "action_battle_feel_test.gd":
 var fixed_aim := Vector2.RIGHT
 func attack_aim_direction() -> Vector2:
  return fixed_aim

func _initialize() -> void:
 call_deferred("capture")

func capture() -> void:
 var arena := FixedArena.new()
 arena.original_wanderer_enabled = true
 arena.original_wanderer_art_folder = "yuxu_fullbody_v15"
 root.add_child(arena)
 root.mode = Window.MODE_WINDOWED
 root.size = Vector2i(1280, 720)
 arena.set_process(false)
 arena.set_process_input(false)
 arena.battle_hud.set_process(false)
 arena.battle_hud.set_process_input(false)
 await process_frame
 await process_frame
 var output := ProjectSettings.globalize_path("res://../../output/fist_combo_20261104")
 DirAccess.make_dir_recursive_absolute(output.path_join("right"))
 var signatures := []
 for side in ["right", "left"]:
  arena.restart_battle()
  arena.player = Vector2(1400, 900)
  arena.fixed_aim = Vector2.RIGHT if side == "right" else Vector2.LEFT
  arena.enemy = arena.player + arena.fixed_aim * 50
  arena.enemy_hp = 1000
  arena.enemy_max_hp = 1000
  arena.wolf_state = "capture_fixture"
  arena.held_basic_attack = true
  arena.use_skill("拳掌")
  for frame in range(84):
   arena.held_basic_attack = frame < 53
   arena._process(1.0 / 24.0)
   arena.queue_redraw()
   arena.battle_hud._update_layout()
   arena.battle_hud.queue_redraw()
   await process_frame
   await RenderingServer.frame_post_draw
   var shot := root.get_texture().get_image()
   if side == "right":
    assert(shot.save_png(output.path_join("right/frame_%04d.png" % frame)) == OK)
   if frame in [6,17,30,45]:
    var stage := [6,17,30,45].find(frame) + 1
    assert(shot.save_png(output.path_join("stage_%d_%s.png" % [stage,side])) == OK)
    var skin: RefCounted = arena.original_wanderer.wanderer_skin
    signatures.append({"stage":stage,"side":side,"position":arena.player,"near":skin.bones.near_upper.rotation,"forearm":skin.bones.near_forearm.rotation,"far":skin.bones.far_upper.rotation,"chest":skin.bones.torso.rotation})
  assert(not arena.fist_combo.active)
 print("FIST_COMBO_POSES ", JSON.stringify(signatures))
 print("FIST_COMBO_CAPTURE_OK 168 frames; 8 contact stills")
 arena.battle_hud.pointer = arena.battle_hud.dock_rect().position + Vector2(21, 25)
 arena.battle_hud.queue_redraw()
 await process_frame
 await RenderingServer.frame_post_draw
 assert(root.get_texture().get_image().save_png(output.path_join("tooltip.png")) == OK)
 arena.queue_free()
 await process_frame
 quit()
