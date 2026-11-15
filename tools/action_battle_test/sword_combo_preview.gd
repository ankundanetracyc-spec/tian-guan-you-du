extends Node2D
const RULES = preload("sword_combo_rules.gd")
const Combo = preload("sword_combo_runtime.gd")
const Pose = preload("sword_combo_pose.gd")
const Sweep = preload("sword_sweep_motion.gd")
const Rig = preload("approved_outfit_fitting_runtime.gd")
const VFX = preload("sword_combo_vfx.gd")
const PAPER := Color("eee9d9")
const INK := Color("293c39")
const JADE := Color("557f70")
var physique := 8.0
var soul := 8.0
var sword_attainment := 6.0
var player := Vector2.ZERO
var paused := false
var held := false
var cooldown_remaining := 0.0
var aim_override := Vector2.RIGHT
var aim_from_mouse := false
var show_geometry := false
var auto_loop := true
var one_combo_playback := false
var clock := 0.0
var combo: RefCounted
var pose: RefCounted
var rig: Node2D
var vfx: Node2D
var world: Node2D
var font: Font
var ground: Texture2D
var screen_origin := Vector2(646,576)
var zoom := 2.0
var labels: Dictionary = {}
var target_flashes: Dictionary = {}
var last_release: Dictionary = {}
var contact_count := 0
var step_delta := 0.0
var current_direction := Vector2.RIGHT
var frame_velocity := Vector2.ZERO
var actor_motion_clock := -INF
var actor_motion_updates := 0
var actor_pose_base: Dictionary = {}
const POSE_BONES := ["torso","near_upper","near_forearm","near_hand","far_upper","far_forearm","far_hand"]
var markers := [Vector2(60,0),Vector2(112,0),Vector2(70,42),Vector2(70,-48),Vector2(-36,0)]

func _ready() -> void:
 get_window().title = "剑式 · 四段持剑演练"
 get_window().size = Vector2i(1280,720)
 get_window().content_scale_size = Vector2i(1280,720)
 get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
 var face := SystemFont.new()
 face.font_names = PackedStringArray(["Microsoft YaHei"])
 font = face
 var image := Image.new()
 if image.load(ProjectSettings.globalize_path("res://art/battle_ground_stone_v1.png"))==OK: ground = ImageTexture.create_from_image(image)
 world = Node2D.new()
 world.position = screen_origin
 world.scale = Vector2.ONE*zoom
 add_child(world)
 rig = Rig.new()
 world.add_child(rig)
 assert(rig.build(),"剑式演练人物载入失败")
 rig.display_zoom = 1.30
 rig.wanderer_skin.approved_sword.set_mode("hold")
 vfx = VFX.new()
 vfx.z_index = 50
 world.add_child(vfx)
 combo = Combo.new()
 combo.setup(self)
 pose = Pose.new()
 _build_ui()
 update_actor(0.00001)
 refresh_labels()

func _label(value: String, point: Vector2, size_px := 16) -> Label:
 var label := Label.new()
 label.text = value
 label.position = point
 label.add_theme_font_override("font",font)
 label.add_theme_font_size_override("font_size",size_px)
 label.add_theme_color_override("font_color",INK)
 add_child(label)
 return label

func _button(value: String, point: Vector2, callback: Callable, width := 206.0) -> Button:
 var button := Button.new()
 button.text = value
 button.position = point
 button.size = Vector2(width,38)
 button.add_theme_font_override("font",font)
 button.add_theme_font_size_override("font_size",16)
 button.add_theme_color_override("font_color",INK)
 var style := StyleBoxFlat.new()
 style.bg_color = PAPER
 style.border_color = Color("9c9e8b")
 style.set_border_width_all(1)
 style.set_corner_radius_all(4)
 button.add_theme_stylebox_override("normal",style)
 button.pressed.connect(callback)
 add_child(button)
 return button

func _build_ui() -> void:
 _label("剑式",Vector2(28,24),30)
 _label("持剑四段",Vector2(30,66),18)
 _label("斜斩 → 回斩 → 直刺 → 横扫",Vector2(310,28),24)
 _label("两斩逼身，一刺追身，横扫收势",Vector2(312,67),16)
 _button("完整连招",Vector2(30,112),start_combo)
 for index in range(4):
  var stage_index := index
  _button("%d  %s" % [index+1,RULES.STAGES[index].name],Vector2(30,166+index*46),func(): start_single(stage_index))
 _button("P  暂停／继续",Vector2(30,366),func(): paused = not paused)
 _button("F  左右朝向",Vector2(30,412),flip_direction)
 _button("G  判定对照",Vector2(30,458),func(): show_geometry = not show_geometry)
 _button("L  连续演示",Vector2(30,504),toggle_loop)
 _button("R  重开",Vector2(30,550),restart)
 _label("左键连招 · WASD移动",Vector2(30,610),15)
 _label("空格中断 · 鼠标定向",Vector2(30,638),15)
 _label("1—4 单段观察",Vector2(30,666),15)
 labels.action = _label("",Vector2(310,114),22)
 labels.values = _label("",Vector2(312,149),15)
 labels.timing = _label("",Vector2(312,178),15)
 _label("体魄",Vector2(884,37),14)
 _label("神魂",Vector2(1006,37),14)
 _label("剑道",Vector2(1128,37),14)
 for index in range(3):
  var value := SpinBox.new()
  value.position = Vector2(884+index*122,65)
  value.size = Vector2(104,32)
  value.min_value = 0
  value.max_value = 1000
  value.step = 1
  value.value = [physique,soul,sword_attainment][index]
  value.add_theme_font_override("font",font)
  var key: String = ["physique","soul","sword_attainment"][index]
  value.value_changed.connect(func(number: float): set(key,number); refresh_labels())
  add_child(value)
 labels.footer = _label("",Vector2(312,669),15)
 _label("扇面与胶囊共用实际范围；标记仅演示接触",Vector2(312,696),13)

func refresh_labels() -> void:
 var index: int = maxi(0,combo.stage)
 var rule := RULES.preview(index,physique,soul,sword_attainment)
 labels.action.text = "%d / 4  %s" % [index+1,rule.name] if combo.active else ("剑式 · 就绪" if cooldown_remaining<=0 else "收势 · 冷却 %.2f秒" % cooldown_remaining)
 var reaches: Array[String] = []
 var damage: Array[String] = []
 for stage in range(4):
  var item := RULES.preview(stage,physique,soul,sword_attainment)
  damage.append(str(item.damage))
  reaches.append(String.num(float(item.reach),1))
 labels.values.text = "基础伤害 %s　距离 %s" % [" / ".join(damage)," / ".join(reaches)]
 labels.timing.text = "连招 %.3f秒　结束后冷却 %.3f秒　前压 0 / 6 / 14 / 0" % [2.1*RULES.tempo(physique,soul,sword_attainment),RULES.cooldown(physique,sword_attainment)]
 labels.footer.text = "%s　%s　%s" % ["连续演示中" if auto_loop else "手动操作", "已暂停" if paused else "", "判定对照已开启" if show_geometry else ""]

func start_combo() -> void:
 restart()
 auto_loop = false
 one_combo_playback = true
 held = true
 combo.request()

func start_single(index: int) -> void:
 restart()
 auto_loop = false
 combo.chain_id += 1
 combo.q = RULES.tempo(physique,soul,sword_attainment)
 combo.active = true
 combo._begin_stage(index)
 combo.single_stage = true

func restart() -> void:
 paused = false
 held = false
 one_combo_playback = false
 player = Vector2.ZERO
 cooldown_remaining = 0.0
 combo.clear()
 vfx.clear()
 target_flashes.clear()
 last_release.clear()
 contact_count = 0
 clock = 0.0
 frame_velocity = Vector2.ZERO
 actor_motion_clock = -INF
 update_actor(0.00001)

func flip_direction() -> void:
 aim_from_mouse = false
 aim_override = Vector2(-aim_override.x,aim_override.y).normalized()
 if aim_override.is_zero_approx(): aim_override = Vector2.LEFT

func toggle_loop() -> void:
 auto_loop = not auto_loop
 one_combo_playback = false
 if not auto_loop:
  held = false
  combo.buffered = false

func aim_direction() -> Vector2:
 if aim_from_mouse:
  var point := Sweep.unproject_ground(world.get_local_mouse_position()-Sweep.project_ground(player))
  if point.length_squared()>0.001: return point.normalized()
 return aim_override.normalized()

func clamp_position() -> void: player = player.clamp(Vector2(-116,-56),Vector2(116,35))

func emit_release(release: Dictionary, elapsed_after := 0.0) -> void:
 # Exact contact sampling changes rotations only. Cloth/gait integrate once
 # after the complete real frame displacement is known, never at a tiny dt.
 update_actor(0.0,false)
 release.view_depth = Sweep.VIEW_DEPTH
 release.attack_height = attack_height()
 release.blade_path = _sample_blade_path(release)
 last_release = release
 vfx.emit(release,elapsed_after)
 for index in range(markers.size()):
  if combo.take_contact(release,str(index),markers[index]):
   target_flashes[index] = 0.34
   contact_count += 1
 if bool(release.is_combo_last) and one_combo_playback:
  held = false
  one_combo_playback = false

func _sample_blade_path(release: Dictionary) -> Array[Dictionary]:
 var path: Array[Dictionary] = []
 var saved_last: Dictionary = pose.last.duplicate(true)
 var sword: RefCounted = rig.wanderer_skin.approved_sword
 var saved_sword_transform: Transform2D = sword.sword_sprite.global_transform
 var saved_sword_last: Dictionary = sword.last.duplicate(true)
 var stage := int(release.index)
 var contact := float(release.duration)*float(release.hit)
 var aim: Vector2 = release.direction
 # Every sample reuses the release's fixed base pose, player and aim. This is
 # a spatial read, without changing combo.age, clock, gait or cloth state.
 for index in range(65):
  var u := float(index)/64.0
  _restore_actor_base()
  pose.apply_at(rig,stage,release,contact+Sweep.birth(u,float(release.reveal)),aim)
  path.append({"u":u,"tip":world.to_local(sword.sword_sprite.to_global(sword.source_tip)),
   "grip":world.to_local(sword.sword_sprite.to_global(sword.source_grip))})
 _restore_actor_base()
 pose.apply_at(rig,combo.stage,combo.current,combo.age,current_direction)
 sword.sword_sprite.global_transform = saved_sword_transform
 sword.last = saved_sword_last
 pose.last = saved_last
 return path

func _restore_actor_base() -> void:
 for key in actor_pose_base:
  rig.wanderer_skin.bones[key].transform = actor_pose_base[key]

func update_actor(delta: float, integrate_motion := true) -> void:
 current_direction = combo.direction if combo.active else aim_direction()
 var facing := -1.0 if current_direction.x<0.0 else 1.0
 var skin: RefCounted = rig.wanderer_skin
 rig.position = Sweep.project_ground(player)
 _restore_actor_base()
 if integrate_motion and delta>0.0 and actor_motion_clock!=clock:
  skin.set_motion_context(delta,frame_velocity,clock,facing)
  rig.set_character_pose("Idle",clock,facing)
  for key in POSE_BONES: actor_pose_base[key] = skin.bones[key].transform
  actor_motion_clock = clock
  actor_motion_updates += 1
 else:
  # Preserve the cached locomotion pose but keep a new release's facing exact.
  rig.scale.x = absf(rig.scale.x)*facing
 pose.apply(rig,combo,current_direction)

func attack_height() -> float:
 var hands: RefCounted = rig.wanderer_skin.approved_hands
 var hand_position: Vector2 = world.to_local(hands.get_attachment_transform("near").origin)
 return maxf(0.0,Sweep.project_ground(player).y-hand_position.y)

func advance(delta: float) -> void:
 if paused:
  refresh_labels()
  queue_redraw()
  return
 step_delta = delta
 var player_before := player
 clock += delta
 vfx.advance(delta)
 for key in target_flashes.keys():
  target_flashes[key] = maxf(0.0,float(target_flashes[key])-delta)
  if target_flashes[key]<=0.0: target_flashes.erase(key)
 var move := Vector2(Input.get_axis("ui_left","ui_right"),Input.get_axis("ui_up","ui_down"))
 if Input.is_physical_key_pressed(KEY_A): move.x -= 1.0
 if Input.is_physical_key_pressed(KEY_D): move.x += 1.0
 if Input.is_physical_key_pressed(KEY_W): move.y -= 1.0
 if Input.is_physical_key_pressed(KEY_S): move.y += 1.0
 player += move.limit_length()*35.0*delta
 clamp_position()
 if one_combo_playback and combo.active: held = true
 if auto_loop and not combo.active and cooldown_remaining<=0.0:
  player = Vector2.ZERO
  # A review-loop reset is not a locomotion step. Preserve subsequent real
  # WASD/approach displacement without feeding the reset into cloth inertia.
  player_before = player
  held = true
  combo.request()
 if held and not auto_loop and not combo.active and cooldown_remaining<=0.0: combo.request()
 combo.advance(delta)
 frame_velocity = (player-player_before)/maxf(delta,0.000001)
 update_actor(delta)
 refresh_labels()
 queue_redraw()

func _process(delta: float) -> void: advance(minf(delta,0.05))

func _input(event: InputEvent) -> void:
 # GUI buttons may consume the release after a drag out of the stage.
 if event is InputEventMouseButton and event.button_index==MOUSE_BUTTON_LEFT and not event.pressed:
  held = false
  one_combo_playback = false

func _unhandled_input(event: InputEvent) -> void:
 if event is InputEventMouseMotion: aim_from_mouse = true
 if event is InputEventMouseButton and event.button_index==MOUSE_BUTTON_LEFT:
  auto_loop = false
  one_combo_playback = false
  held = event.pressed
  if held: combo.request()
 if event is InputEventKey and event.pressed and not event.echo:
  var key: int = event.keycode
  if key>=KEY_1 and key<=KEY_4: start_single(key-KEY_1)
  if key==KEY_P: paused = not paused
  if key==KEY_F: flip_direction()
  if key==KEY_G: show_geometry = not show_geometry
  if key==KEY_L: toggle_loop()
  if key==KEY_R: restart()
  if key==KEY_SPACE:
   held = false
   one_combo_playback = false
   auto_loop = false
   combo.finish()
  if key==KEY_ESCAPE: get_tree().quit()

func _notification(what: int) -> void:
 if what==NOTIFICATION_APPLICATION_FOCUS_OUT:
  held = false
  if combo!=null: combo.buffered = false

func _exit_tree() -> void:
 if vfx!=null: vfx.clear()
 if combo!=null: combo.clear()
 if rig!=null and is_instance_valid(rig) and rig.wanderer_skin!=null: rig.wanderer_skin.approved_sword.set_mode("off")

func ground_to_screen(point: Vector2, height := 0.0) -> Vector2:
 return screen_origin+(Sweep.project_ground(point)-Vector2(0.0,height))*zoom

func display_outline(release: Dictionary, height := 0.0) -> PackedVector2Array:
 var result := PackedVector2Array()
 for point in RULES.outline(release): result.append(ground_to_screen(point,height))
 return result

func _draw() -> void:
 draw_rect(Rect2(0,0,1280,720),Color("a5aea3"))
 if ground!=null: draw_texture_rect(ground,Rect2(270,100,1010,620),false,Color(0.85,0.90,0.88,0.70))
 draw_rect(Rect2(0,0,270,720),PAPER)
 draw_rect(Rect2(270,0,1010,212),Color("e8e6d9"))
 draw_line(Vector2(270,0),Vector2(270,720),Color("939b87"),1.0,true)
 if combo==null: return
 for index in range(markers.size()):
  var point := ground_to_screen(Vector2(markers[index]))
  var tint := Color("cee6cc") if target_flashes.has(index) else Color(0.27,0.36,0.29,0.5)
  draw_arc(point,9,0,TAU,24,tint,1.5,true)
  draw_line(point-Vector2(5,0),point+Vector2(5,0),tint,1.0,true)
  draw_line(point-Vector2(0,5),point+Vector2(0,5),tint,1.0,true)
 if show_geometry:
  var release: Dictionary
  if combo.active:
   release = combo.current.duplicate(true)
   release.origin = player
   release.direction = combo.direction
   release.attack_height = attack_height()
  elif not last_release.is_empty(): release = last_release
  else: return
  var feet := display_outline(release)
  var raised := display_outline(release,float(release.get("attack_height",58.0)))
  draw_polyline(feet,Color(0.26,0.42,0.34,0.65),1.25,true)
  draw_polyline(raised,Color(0.87,0.91,0.77,0.65),1.0,true)
  var center := ground_to_screen(Vector2(release.origin))
  draw_line(center,center-Vector2(0,float(release.get("attack_height",58.0))*zoom),Color(0.8,0.88,0.74,0.4),1.0,true)
