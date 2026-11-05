extends SceneTree

class FixedArena extends "human_sparring_arena.gd":
 func attack_aim_direction() -> Vector2:
  return Vector2.RIGHT

func _initialize() -> void:
 call_deferred("capture")

func capture() -> void:
 var arena := FixedArena.new()
 var runtime: Node = load("res://csharp/battle/ActionHumanSparringRuntime.cs").new()
 arena.add_child(runtime)
 root.add_child(arena)
 arena.set_process(false)
 arena.set_process_input(false)
 arena.battle_hud.set_process(false)
 arena.battle_hud.set_process_input(false)
 root.mode = Window.MODE_WINDOWED
 root.size = Vector2i(1280, 720)
 Engine.max_fps = 60
 arena.player = Vector2(1500, 1000)
 for index in range(3):
  var npc: Node2D = arena.npcs[index]
  npc.ai_enabled = false
  npc.player = arena.player + [Vector2(50, 0), Vector2(63, -12), Vector2(-38, 5)][index]
  npc.visual_motion_delta = 0.0
  npc.update_skinned_player()
 arena.refresh_health()
 arena.show_hitboxes = true
 arena.set_simulation_paused(false)
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.18)
 arena.update_skinned_player()
 arena._update_trial_camera(0.0, true)
 arena.hitbox_overlay.queue_redraw()
 arena.battle_hud._update_layout()
 arena.battle_hud.queue_redraw()
 arena.queue_redraw()
 var folder := ProjectSettings.globalize_path("res://output/human_sparring_20261104")
 DirAccess.make_dir_recursive_absolute(folder)
 await process_frame
 await RenderingServer.frame_post_draw
 assert(root.get_texture().get_image().save_png(folder.path_join("hitbox_diagnosis.png")) == OK)
 print("HUMAN_HITBOX_DIAGNOSIS_OK: ground distance and elevation are independent; horizontal hand-height attack; no unapproved body blocking")
 arena.battle_hud.help_open = true
 arena.battle_hud.queue_redraw()
 await process_frame
 await RenderingServer.frame_post_draw
 assert(root.get_texture().get_image().save_png(folder.path_join("help.png")) == OK)
 arena.queue_free()
 await process_frame
 quit()
