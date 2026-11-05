extends SceneTree
const RULES = preload("sword_combo_rules.gd")
const SWEEP = preload("sword_sweep_motion.gd")
const VIEW_SIZE := Vector2i(1280,720)
var arena: Node2D
var target := "E:/stacklands/docs/美术规范/持剑普攻剑式四段_20261105"
var frames_dir := "E:/stacklands/output/sword_combo_capture_20261105"
var checks := 0
var failed := 0
var journal: Array = []
var stages_only := false
var include_directions := false
var capsule_vertices := 0
var capsule_violations := 0
var capsule_max_outside := 0.0
var fps := 30.0
var frame_count := -1
var motion_journal: Array = []

func _initialize() -> void: call_deferred("capture")

func check(value: bool, label: String) -> void:
 checks += 1
 if not value:
  failed += 1
  push_error(label)

func render_frame() -> void:
 arena.queue_redraw()
 await process_frame
 await RenderingServer.frame_post_draw

func advance_render(delta: float) -> void:
 arena.advance(delta)
 verify_capsule_bounds()
 await render_frame()

func verify_capsule_bounds() -> void:
 for mark in arena.vfx.marks:
  if str(mark.shape)!="capsule": continue
  var direction: Vector2 = mark.direction
  var origin: Vector2 = mark.origin
  for polygon in mark.polygons:
   for vertex in polygon.polygon:
    var point := SWEEP.unproject_ground(Vector2(vertex)+Vector2(0.0,float(mark.attack_height)),float(mark.get("view_depth",1.0)))
    var relative := point-origin
    var along := relative.dot(direction)
    var across := relative.dot(direction.orthogonal())
    var outside := Vector2(along-clampf(along,10.0,float(mark.reach)-10.0),across).length()-10.0
    capsule_vertices += 1
    capsule_max_outside = maxf(capsule_max_outside,outside)
    if not RULES.contains(mark,point): capsule_violations += 1

func picture(path: String) -> void:
 await render_frame()
 var image := root.get_texture().get_image()
 check(image.get_size()==VIEW_SIZE,"实际原生画面保持1280×720")
 check(image.save_png(path)==OK,"保存原生画面："+path)

func visual_bounds() -> Array:
 var bounds := Rect2()
 var found := false
 for mark in arena.vfx.marks:
  for polygon in mark.polygons:
   for vertex in polygon.polygon:
    var point: Vector2 = polygon.get_global_transform_with_canvas()*Vector2(vertex)
    if not found:
     bounds = Rect2(point,Vector2.ZERO)
     found = true
    else: bounds = bounds.expand(point)
 return [bounds.position.x,bounds.position.y,bounds.end.x,bounds.end.y] if found else []

func write_manifest(samples: Array[String]) -> void:
 check(capsule_vertices>0 and capsule_violations==0,"逐帧直刺墨锋保持实际胶囊边界内")
 var file := FileAccess.open(target.path_join("capture_manifest.json"),FileAccess.WRITE)
 check(file!=null,"保存采集记录")
 if file==null: return
 file.store_string(JSON.stringify({"frames":samples,"frames_dir":frames_dir,"capture_dir":target,
  "fps":fps,"native_size":[VIEW_SIZE.x,VIEW_SIZE.y],"stages":journal,"stages_only":stages_only,
  "motion_samples":motion_journal,
  "checks":checks,"failures":failed,"render_each_advance":true,
  "clip_boundary":{"capsule_vertices":capsule_vertices,"violations":capsule_violations,"max_outside_distance":capsule_max_outside},
  "scope":"Actual Godot renderer; geometric markers only, no simulated health deductions"},"  "))
 file.close()

func capture() -> void:
 for argument in OS.get_cmdline_user_args():
  if argument.begins_with("--capture-dir="): target = argument.trim_prefix("--capture-dir=")
  elif argument.begins_with("--frames-dir="): frames_dir = argument.trim_prefix("--frames-dir=")
  elif argument.begins_with("--fps="): fps = float(argument.trim_prefix("--fps="))
  elif argument.begins_with("--frames="): frame_count = int(argument.trim_prefix("--frames="))
  elif argument=="--stages-only": stages_only = true
  elif argument=="--with-directions": include_directions = true
 check(fps>0.0,"采集帧率为正")
 if fps<=0.0:
  quit(1)
  return
 if frame_count<0: frame_count = roundi(3.4*fps)
 check(frame_count>0,"采集帧数为正")
 if frame_count<=0:
  quit(1)
  return
 root.mode = Window.MODE_WINDOWED
 root.size = VIEW_SIZE
 root.content_scale_size = VIEW_SIZE
 root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
 root.position = Vector2i(-4096,-4096)
 root.gui_disable_input = true
 root.unfocusable = true
 DirAccess.make_dir_recursive_absolute(target)
 DirAccess.make_dir_recursive_absolute(frames_dir)
 arena = load("res://sword_combo_preview.tscn").instantiate()
 root.add_child(arena)
 current_scene = arena
 arena.set_process(false)
 check(arena.combo!=null and arena.vfx!=null and arena.pose!=null,"真实预览场景已完成初始化")
 if arena.combo==null or arena.vfx==null or arena.pose==null:
  print("SWORD_CAPTURE_ABORT failures=",failed)
  quit(1)
  return
 arena.auto_loop = false
 arena.aim_from_mouse = false
 for warmup in range(3): await render_frame()
 for index in range(4):
  arena.start_single(index)
  var rule := RULES.preview(index,8,8,6)
  var duration: float = rule.duration
  var age := duration*float(rule.hit)+float(rule.reveal)*0.90
  for step in range(ceili(age*120)):
   await advance_render(minf(1.0/120.0,maxf(0.0,age-float(step)/120.0)))
  var bounds := visual_bounds()
  await picture(target.path_join("stage_%d.png" % (index+1)))
  arena.show_geometry = true
  await picture(target.path_join("stage_%d_geometry.png" % (index+1)))
  arena.show_geometry = false
  check(arena.combo.releases.size()==1,"单段观察只释放一段")
  check(absf(float(arena.rig.wanderer_skin.approved_sword.last.attachment_error))<0.002,"握柄贴合右手")
  check(not arena.vfx.marks.is_empty(),"峰值存在实际剑痕")
  journal.append({"stage":index,"release":arena.last_release.duplicate(true),"pose":arena.pose.last.duplicate(true),"visual_bounds":bounds,"peak_time":age})
  print("SWORD_STAGE ",index+1," height=",arena.last_release.attack_height," marks=",arena.vfx.marks.size()," peak_time=",age," bounds=",bounds)
  if index==0: print("SWORD_FIRST_PREVIEW ",target.path_join("stage_1.png"))
 if not stages_only or include_directions:
  for direction in [Vector2.LEFT,Vector2(1,-1).normalized(),Vector2(-1,1).normalized()]:
   arena.aim_override = direction
   # The broad final sweep makes the horizontal plane visible in these views.
   var direction_stage := 3 if stages_only else 2
   arena.start_single(direction_stage)
   var direction_rule := RULES.preview(direction_stage,8,8,6)
   var direction_time := float(direction_rule.duration)*float(direction_rule.hit)+float(direction_rule.reveal)*0.90
   for step in range(ceili(direction_time*120)):
    await advance_render(minf(1.0/120.0,maxf(0.0,direction_time-float(step)/120.0)))
   await picture(target.path_join("direction_%s.png" % ("left" if direction==Vector2.LEFT else ("up_right" if direction.x>0 else "down_left"))))
   check(float(arena.rig.wanderer_skin.approved_sword.last.attachment_error)<0.002,"斜向和镜像时握柄不脱落")
 if stages_only:
  var empty_frames: Array[String] = []
  write_manifest(empty_frames)
  print("SWORD_STAGE_CAPTURE_CHECKS ",checks," failures=",failed)
  arena.queue_free()
  await process_frame
  quit(1 if failed else 0)
  return
 arena.aim_override = Vector2.RIGHT
 arena.start_combo()
 var samples: Array[String] = []
 for frame in range(frame_count):
  arena.held = arena.combo.stage<3
  arena.advance(1.0/fps)
  verify_capsule_bounds()
  if arena.combo.stage==3: arena.held = false
  var filename := "frame_%03d.png" % frame
  await picture(frames_dir.path_join(filename))
  samples.append(filename)
  motion_journal.append({"frame":frame,"time":(frame+1)/fps,"pose":arena.pose.last.duplicate(true),
   "player":arena.player,"velocity":arena.frame_velocity,"visual_bounds":visual_bounds()})
 check(arena.combo.releases.size()==4,"连续录制实际释放完整四段")
 check(arena.player.distance_to(Vector2(20,0))<0.002,"完整连招累计前压20单位")
 check(arena.contact_count>0,"接触标记来自真实范围")
 check(not arena.combo.active and arena.cooldown_remaining<=0,"完整收势和冷却后回到就绪")
 write_manifest(samples)
 print("SWORD_CAPTURE_CHECKS ",checks," failures=",failed)
 arena.queue_free()
 await process_frame
 quit(1 if failed else 0)
