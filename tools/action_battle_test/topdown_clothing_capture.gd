extends SceneTree

var arena: Node2D
var frame := 0
var capture_index := -1
var folder := "res://../../output/topdown_clothing_20261104/motion"
var previous_id := ""


func _initialize() -> void:
 call_deferred("start_capture")


func start_capture() -> void:
 root.size = Vector2i(1280, 720)
 arena = load("res://TopDownBattleTrial.tscn").instantiate()
 root.add_child(arena)
 arena.set_process(false)
 arena.restart_trial()
 arena.clothing_open = true
 arena.battle_hud.set_process(false)
 DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(folder))
 RenderingServer.frame_post_draw.connect(save_capture_frame)


func save_capture_frame() -> void:
 if capture_index < 0:
  return
 root.get_texture().get_image().save_png(ProjectSettings.globalize_path(folder).path_join("frame_%04d.png" % capture_index))
 capture_index = -1


func _process(_delta: float) -> bool:
 if arena == null:
  return false
 var time := frame / 30.0
 var index := mini(int(time / 2.0), 6)
 var order := [5, 4, 6, 0, 1, 2, 3]
 var profile: Dictionary = arena.avatar.library.PROFILES[order[index]]
 if str(profile.id) != previous_id:
  arena.apply_clothing(str(profile.id))
  previous_id = str(profile.id)
  arena.battle_hud.preview_clock = 0.0
 arena.battle_hud.preview_action = 1 if index % 3 == 0 else (2 if index % 3 == 1 else 3)
 arena.battle_hud._process(1.0 / 30.0)
 arena.battle_hud.queue_redraw()
 if frame % 2 == 0:
  capture_index = frame
 frame += 1
 if frame >= 420:
  print("TOPDOWN_CLOTHING_MOTION frames=", frame, " outfits=7 native_bones=", arena.avatar.skeleton.get_bone_count())
  arena.saved_window_state.clear()
  quit()
 return false
