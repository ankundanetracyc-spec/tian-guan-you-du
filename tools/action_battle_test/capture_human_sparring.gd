extends SceneTree

class CaptureArena extends "human_sparring_arena.gd":
 var aim := Vector2.RIGHT
 var move_input := Vector2.ZERO
 func attack_aim_direction() -> Vector2:
  return aim
 func movement_direction() -> Vector2:
  return move_input

func _initialize() -> void:
 call_deferred("capture")

func capture() -> void:
 Engine.max_fps = 60
 var arena := CaptureArena.new()
 var runtime: Node = load("res://csharp/battle/ActionHumanSparringRuntime.cs").new()
 arena.add_child(runtime)
 root.add_child(arena)
 arena.set_process(false)
 arena.set_process_input(false)
 arena.battle_hud.set_process_input(false)
 root.mode = Window.MODE_WINDOWED
 root.size = Vector2i(1280, 720)
 RenderingServer.viewport_set_update_mode(root.get_viewport_rid(), RenderingServer.VIEWPORT_UPDATE_ALWAYS)
 var folder := ProjectSettings.globalize_path("res://output/human_sparring_20261104")
 var frames_name := "frames_%d" % Time.get_ticks_usec()
 DirAccess.make_dir_recursive_absolute(folder.path_join(frames_name))
 await process_frame
 RenderingServer.force_draw(false)
 assert(root.get_texture().get_image().save_png(folder.path_join("ready.png")) == OK)
 arena.set_simulation_paused(false)
 var center: Vector2 = arena.player
 var cast_blood := false
 var cast_qi := false
 for frame in range(240):
  arena.simulation_paused = false # Deterministic capture ignores desktop focus changes.
  var time := frame / 24.0
  var nearest: Node2D = null
  for npc in arena.npcs:
   if npc.hp > 0 and (nearest == null or arena.player.distance_to(npc.player) < arena.player.distance_to(nearest.player)):
    nearest = npc
  if nearest != null: arena.aim = (nearest.player - arena.player).normalized()
  arena.move_input = Vector2.ZERO
  if time > 2.0:
   var goal := center + Vector2.from_angle((time - 2.0) * 1.0) * 110.0
   arena.move_input = (goal - arena.player).normalized() if goal.distance_to(arena.player) > 8.0 else Vector2.ZERO
  if arena.hp > 0 and arena.enemy_hp > 0:
   var threatened := false
   for npc in arena.npcs:
    if npc.hp > 0 and not npc.pending_attack.is_empty() and arena.player.distance_to(npc.player) < 90.0:
     threatened = true
   arena.held_basic_attack = arena.pending_self_skill.is_empty()
   if time > 4.0 and not cast_blood and arena.pending_attack.is_empty():
    arena.use_skill("催血")
    cast_blood = not arena.pending_self_skill.is_empty()
   elif time > 7.0 and not cast_qi and arena.pending_attack.is_empty():
    arena.use_skill("聚炁")
    cast_qi = not arena.pending_self_skill.is_empty()
   elif threatened and arena.pending_attack.is_empty() and arena.pending_self_skill.is_empty() and arena.skill_cooldown_remaining("架招") == 0:
    arena.use_skill("架招")
   elif threatened and arena.skill_cooldown_remaining("错步") == 0 and time > 2.5:
    arena.use_skill("错步")
   elif arena.pending_self_skill.is_empty() and nearest != null and arena.player.distance_to(nearest.player) < 90.0:
    if arena.skill_cooldown_remaining("痛击") == 0: arena.use_skill("痛击")
    elif arena.fist_combo == null or not arena.fist_combo.active: arena.use_skill("拳掌")
  arena._process(1.0 / 24.0)
  arena.queue_redraw()
  arena.battle_hud._update_layout()
  arena.battle_hud.queue_redraw()
  await process_frame
  RenderingServer.force_draw(false)
  var shot := root.get_texture().get_image()
  assert(shot.save_png(folder.path_join(frames_name).path_join("frame_%04d.png" % frame)) == OK)
  if frame in [32, 72, 120, 180, 239]:
   assert(shot.save_png(folder.path_join("combat_%03d.png" % frame)) == OK)
 var manifest := FileAccess.open(folder.path_join("capture_manifest.json"), FileAccess.WRITE)
 manifest.store_string(JSON.stringify({"frames":frames_name, "count":240, "fps":24}))
 manifest.close()
 print("HUMAN_SPARRING_CAPTURE_OK 240 frames / 10s; three human actors; final player_hp=", arena.hp)
 arena.restart_battle()
 arena.battle_hud.help_open = true
 arena.battle_hud.queue_redraw()
 await process_frame
 RenderingServer.force_draw(false)
 assert(root.get_texture().get_image().save_png(folder.path_join("help.png")) == OK)
 arena.queue_free()
 await process_frame
 quit()
