extends SceneTree
const OUT := "res://docs/美术规范/即时战斗文本精简_20261104/"
func _initialize() -> void:
 call_deferred("run")
func run() -> void:
 root.mode=Window.MODE_WINDOWED
 root.size=Vector2i(1440,810)
 root.content_scale_size=Vector2i(1440,810)
 root.content_scale_mode=Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
 root.position=Vector2i(-4096,-4096)
 root.gui_disable_input=true
 var arena=load("res://tscn/scene/ActionBattleOriginalWandererScene.tscn").instantiate()
 root.add_child(arena)
 current_scene=arena
 for i in range(10): await process_frame
 arena.set_process(false)
 arena.player=Vector2(1400,1000)
 arena.enemy=Vector2(1460,1000)
 arena.wolf_state="recover"
 arena.wolf_clock=999.0
 arena.hp=100
 arena.enemy_hp=100
 arena._update_trial_camera(0.0,true)
 DirAccess.make_dir_recursive_absolute(OUT)
 DirAccess.make_dir_recursive_absolute("res://tmp/action_combat_text_capture")
 for frame in range(100):
  if frame==15 or frame==65:
   arena.pending_attack="拳掌" if frame==15 else "痛击"
   arena.attack_direction=Vector2.RIGHT
   arena.resolve_pending_attack()
  if frame==40:
   arena.guard_window=.2
   arena.receive_wolf_bite()
  arena._process(.05)
  arena.facing=1.0
  arena.update_skinned_player()
  arena.queue_redraw()
  arena.battle_hud.queue_redraw()
  await process_frame
  await RenderingServer.frame_post_draw
  var shot=root.get_texture().get_image()
  shot.save_jpg("res://tmp/action_combat_text_capture/frame_%03d.jpg"%frame,.94)
  if frame in [18,43,68]: shot.save_png(OUT+"feedback_%03d.png"%frame)
 print("COMBAT_TEXT_CAPTURE_OK frames=100 seconds=5 actual_hit_and_partial_guard=true")
 quit(0)
