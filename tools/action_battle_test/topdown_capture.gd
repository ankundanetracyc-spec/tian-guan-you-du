extends SceneTree

# Drives the actual trial through its existing movement and skill entry points.
# Rendering is captured from the real OpenGL viewport, without staged overlays.
var arena: Node2D
var frame := 0
var keys_down: Array[int] = []
var folder := "res://../../output/topdown_trial_20261104/motion"
var used_dash := false
var guards := 0
var attacks := 0
var snapshot_hp := 100
var capture_index := -1


func _initialize() -> void:
 call_deferred("start_capture")


func start_capture() -> void:
 if "--clothing" in OS.get_cmdline_user_args():
  folder = "res://../../output/topdown_clothing_20261104/combat"
 root.size = Vector2i(1280, 720)
 arena = load("res://TopDownBattleTrial.tscn").instantiate()
 root.add_child(arena)
 arena.set_process(false)
 if "--clothing" in OS.get_cmdline_user_args():
  # Keep desktop input out of a scripted recording. Movement still uses the
  # real Input state; skills still enter the actual trial's public API.
  arena.set_process_input(false)
  arena.battle_hud.set_process_input(false)
 arena.restart_trial()
 DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(folder))
 RenderingServer.frame_post_draw.connect(save_capture_frame)


func save_capture_frame() -> void:
 if capture_index < 0:
  return
 var result := root.get_texture().get_image().save_png(ProjectSettings.globalize_path(folder).path_join("frame_%04d.png" % capture_index))
 if result != OK:
  push_error("连续实机截图保存失败")
 capture_index = -1


func set_movement(direction: Vector2) -> void:
 var desired: Array[int] = []
 if direction.x > 0.25: desired.append(KEY_D)
 if direction.x < -0.25: desired.append(KEY_A)
 if direction.y > 0.25: desired.append(KEY_S)
 if direction.y < -0.25: desired.append(KEY_W)
 for code in [KEY_W, KEY_A, KEY_S, KEY_D]:
  if desired.has(code) == keys_down.has(code):
   continue
  var event := InputEventKey.new()
  event.keycode = code
  event.physical_keycode = code
  event.pressed = desired.has(code)
  Input.parse_input_event(event)
 keys_down = desired


func _process(_delta: float) -> bool:
 if arena == null:
  return false
 var time := frame / 30.0
 arena.paused = false
 arena.focus_paused = false
 root.warp_mouse(arena.enemy)
 var difference: Vector2 = arena.enemy - arena.player
 arena.aim_direction = difference.normalized()
 var direction := difference.normalized() if difference.length() > 63 else Vector2.ZERO
 if time < 1.5:
  direction = difference.normalized() if difference.length() > 64 else Vector2.ZERO
 elif time < 3.8:
  if arena.wolf_state == "windup" and arena.wolf_clock <= 0.1 and arena.skill_cooldown_remaining("架招") <= 0.0:
   arena.use_skill("架招")
   guards += 1
  if arena.wolf_state == "recover" and difference.length() < 85:
   arena.use_skill("痛击")
 elif time < 5.9:
  direction = Vector2.UP if arena.player.y > 300 else Vector2.RIGHT
  if arena.wolf_state == "windup" and arena.wolf_clock < 0.14 and not used_dash:
   arena.use_skill("错步")
   used_dash = true
 elif time < 10.5:
  if difference.length() < 89:
   arena.use_skill("痛击")
  if difference.length() < 69:
   arena.use_skill("拳掌")
  if arena.wolf_state == "windup" and arena.wolf_clock < 0.09:
   arena.use_skill("架招")
 set_movement(direction if arena.hp > 0 and arena.enemy_hp > 0 else Vector2.ZERO)
 arena._process(1.0 / 30.0)
 if arena.enemy_hp < snapshot_hp:
  attacks += 1
 snapshot_hp = arena.enemy_hp
 arena._update_actors()
 arena.queue_redraw()
 arena.battle_hud.queue_redraw()
 if frame % 2 == 0:
  capture_index = frame
 frame += 1
 if frame >= 360:
  set_movement(Vector2.ZERO)
  print("TOPDOWN_MOTION frames=", frame, " guards=", guards, " dash=", used_dash, " hits=", attacks, " player_hp=", arena.hp, " wolf_hp=", arena.enemy_hp)
  arena.saved_window_state.clear()
  quit()
 return false
