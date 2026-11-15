extends Node2D

# Original puppet based on the project's existing 碧岚云叶袍.
@export var experimental_bilan_parts := false
@export var original_wanderer_enabled := false
@export var original_wanderer_art_folder := "original_wanderer"
var original_wanderer: Node2D
var outfit_sword_visible := false
var player_parts: Texture2D
var reference_parts: Array = []
var reference_mode = false
var skinned_player: Node2D = null
var skinned_player_ready := false
var original_clips: Dictionary = {}
var motion_time = 0.0
var visual_attack_time = -1.0
var battle_ground: Texture2D
var battle_hud: Node2D
var skill_vfx: Node2D
var impact_feedback: Node2D
var wolf_idle: Texture2D
var wolf_attack: Texture2D
var skinned_wolf: Node2D
var wolf_visual_position := Vector2.ZERO
var wolf_visual_hp := 100
const WOLF_VISUAL_SCALE := 0.55
const WOLF_VISUAL_FOOT_OFFSET := Vector2(0, 3)
const WOLF_ART_DIR := "art/forest_wolf_frames_v2"
# Trial-only tuning for movement; wolf physique is the template mean.
const ATTACK_RANGE = preload("attack_range_rules.gd")
const WOLF_SPEED = 95.0
const WOLF_REACH = 70.0 * ATTACK_RANGE.MULTIPLIER
const WOLF_WINDUP = 0.8
const WOLF_LUNGE_SECONDS = 0.2
const WOLF_LUNGE_SPEED = 300.0
const WOLF_RECOVERY = 1.5
var wolf_physique = 9.75
var wolf_state = "chase"
var wolf_clock = 0.0
var wolf_direction = Vector2.LEFT
var wolf_stamina = 6.0
var wolf_stamina_clock = 0.0
var wolf_facing = -1.0
var wolf_contact_resolved = false
var player = Vector2(400, 500)
var enemy = Vector2(1000, 400)
var shots = []
# Sample attributes from the project attribute preview; not a formal character.
var physique = 8.0
var soul = 8.0
var intelligence := 8.0
# Existing derived dodge formula, evaluated for this standalone 8/8/8 fixture.
var dodge := 60.0
var stamina = 6.0
var stamina_clock = 0.0
# Same sample mana capacity as the existing artifact combat preview.
var mana = 3
var mana_capacity = 3
var visible_statuses: Array[String] = []
# Scene-local resources and preparation, never a saved movement debuff.
var available_self_skills: Array[String] = ["催血", "聚炁"]
var qi_blood := 0
var qi_blood_capacity := 8
var qi := 0
var pending_self_skill := ""
var self_cast_remaining := 0.0
var attack_lock = 0.0
const ACTION_SKILL_RULES = preload("action_skill_rules.gd")
const SKILL_RULES = ACTION_SKILL_RULES.RULES
var skill_cooldowns = {"拳掌": 0.0, "痛击": 0.0, "错步": 0.0, "架招": 0.0, "催血": 0.0, "聚炁": 0.0}
var skill_cooldown_totals: Dictionary = {}
var pending_attack = ""
var release_timer = 0.0
var strike_kind = "拳掌"
var strike_direction = Vector2.RIGHT
# Each release owns its own immutable aim/origin; rapid skill changes cannot redirect it.
const STRIKE_FX_SECONDS := 0.65
const HEAVY_FX_SECONDS := 0.90
const STRIKE_FX_FRAMES := 8
const ATTACK_INK_SOURCE = preload("attack_ink_source.gd")
const STRIKE_FX_ATLASES = ATTACK_INK_SOURCE.ATLASES
var strike_fx: Array[Dictionary] = []
var strike_fx_textures: Dictionary = {}
var strike_visual_rng := RandomNumberGenerator.new()
var guard_fx: Array[Dictionary] = []
const GUARD_FX_SECONDS := 0.45
const GUARD_SUCCESS_FX_SECONDS := 0.40
var held_basic_attack = false
var fist_combo: RefCounted
var action_manual: RefCounted = null
var guard_window = 0.0
var dash_move = 0.0
var dash_direction = Vector2.ZERO
var skill_slots = ["痛击", "架招", "", "", ""]
var configuring = false
var selected_slot = -1
var feedback = ""
var feedback_timer = 0.0
# Structured, presentation-only marks. Their clock follows arena pause.
var combat_texts: Array[Dictionary] = []
var attack_effect = 0.0
var basic_strike_effect = 0.0
var basic_strike_hit = false
var basic_strike_origin = Vector2.ZERO
var basic_strike_impact = Vector2.ZERO
var hurt_visual = 0.0
var attack_direction = Vector2.RIGHT
var attack_reach = 70.0 * ATTACK_RANGE.MULTIPLIER
var enemy_shots = []
var hp = 100
var enemy_hp = 100
var max_hp = 100
var enemy_max_hp = 100
var move_speed = 235.0
var stamina_recovery_interval = 2.0
# Only the current-character drill injects this; standalone samples keep their fixture data.
var attribute_runtime: Node = null
var character_name = ""
var fire_timer = 0.0
var enemy_timer = 1.0
var dash_timer = 0.0
var invulnerable = 0.0
var elapsed = 0.0
var facing = 1.0
# Deterministic offscreen review only; live play always leaves this at zero.
var inspection_facing_override := 0.0
var message = ""
var walk_phase = 0.0
var walk_amount = 0.0
var moving_backward := false
var visual_motion_delta := 0.0
var visual_motion_velocity := Vector2.ZERO
var saved_window_state := {}
const TRIAL_WORLD_SIZE := Vector2(3344, 1882)
const TRIAL_ZOOM := 1.35
var trial_camera: Camera2D
var trial_camera_framer: RefCounted
# Additional live units can opt into framing without becoming combat fixtures.
var camera_extra_units: Array[Dictionary] = []

func _ready():
 # The native full-stride character uses the existing235 fixture pace.
 # The previous114 short-gait override no longer fits its stride. Personal
 # character movement continues to come from its actual attributes below.
 if attribute_runtime != null:
  sync_character_attributes()
 else:
  # Fixtures expose both skills; the character drill requires learning and
  # explicit local equipment instead of granting the personal skill nodes.
  skill_slots[2] = "催血" if "催血" in available_self_skills else ""
  skill_slots[3] = "聚炁" if "聚炁" in available_self_skills else ""
 strike_visual_rng.randomize()
 _load_strike_fx()
 skill_vfx = load(_action_test_path("quiet_skill_vfx.gd")).new()
 skill_vfx.setup(self, _action_test_path("quiet_force.gdshader"))
 add_child(skill_vfx)
 var ground_image = Image.new()
 if ground_image.load(ProjectSettings.globalize_path(_action_test_path("art/battle_ground_stone_v1.png"))) == OK:
  battle_ground = ImageTexture.create_from_image(ground_image)
 _enter_trial_fullscreen(original_wanderer_enabled)
 # Load a local-only copy of the original sample for direct comparison.
 var clip_data = JSON.parse_string(FileAccess.get_file_as_string(_action_test_path("art/guigu_reference/animation_curves.json")))
 if clip_data is Dictionary: original_clips = clip_data
 var reference_layout = FileAccess.get_file_as_string(_action_test_path("art/guigu_reference/layout.json"))
 var parsed = JSON.parse_string(reference_layout)
 if parsed is Array:
  for entry in parsed:
   var reference_image = Image.new()
   if reference_image.load(ProjectSettings.globalize_path(_action_test_path("art/guigu_reference/" + entry.sprite))) == OK:
    reference_parts.append({"data": entry, "texture": ImageTexture.create_from_image(reference_image)})
 var puppet_image = Image.new()
 if puppet_image.load(ProjectSettings.globalize_path(_action_test_path("art/bilan_puppet_parts_v2.png"))) == OK:
  player_parts = ImageTexture.create_from_image(puppet_image)
 else:
  message = "未找到角色素材"
 _load_enemy_art()
 _setup_skinned_player()
 _setup_skinned_wolf()
 impact_feedback = load(_action_test_path("combat_impact_feedback.gd")).new()
 impact_feedback.setup(self)
 add_child(impact_feedback)
 impact_feedback.register_actor(self)
 if original_wanderer_enabled:
  trial_camera = Camera2D.new()
  trial_camera_framer = load(_action_test_path("adaptive_combat_camera.gd")).new()
  trial_camera.zoom = Vector2.ONE * TRIAL_ZOOM
  add_child(trial_camera)
  _update_trial_camera(0.0, true)
  trial_camera.make_current()
 # Keep screen UI independent of the battle camera and the project's canvas size.
 var hud_layer = CanvasLayer.new()
 hud_layer.name = "BattleHudLayer"
 hud_layer.layer = 20
 add_child(hud_layer)
 battle_hud = load(_action_test_path("action_battle_hud.gd")).new()
 battle_hud.battle = self
 hud_layer.add_child(battle_hud)
 queue_redraw()


func _load_enemy_art() -> void:
 for item in [["textures/bestiary/static/forest_wolf.png", false], ["textures/bestiary/static/forest_wolf_pounce.png", true]]:
  var img = Image.new()
  var image_path = _resolve_project_art_path(str(item[0]))
  if not image_path.is_empty() and img.load(image_path) == OK:
   if item[1]: wolf_attack = ImageTexture.create_from_image(img)
   else: wolf_idle = ImageTexture.create_from_image(img)

func _load_strike_fx() -> void:
 for kind in STRIKE_FX_ATLASES:
  var bitmap := Image.new()
  var source_path := _action_test_path(str(STRIKE_FX_ATLASES[kind]))
  if bitmap.load(ProjectSettings.globalize_path(source_path)) == OK:
   strike_fx_textures[kind] = ImageTexture.create_from_image(bitmap)
  else:
   push_error("拳掌劲序列帧加载失败：" + source_path)


func strike_fx_frame_rect(texture: Texture2D, frame: int) -> Rect2:
 # The generated 1774x887 atlas is not divisible by four. Retain fractional cells,
 # and inset the sampled rectangle to prevent filtering from adjacent frames.
 return ATTACK_INK_SOURCE.frame_rect(texture, frame)


func strike_fx_destination_rect(texture: Texture2D, reach: float, progress: float) -> Rect2:
 return ATTACK_INK_SOURCE.destination_rect(texture, reach, progress)


func update_strike_fx(delta: float) -> void:
 if skill_vfx != null: skill_vfx.advance(delta)
 for index in range(strike_fx.size() - 1, -1, -1):
  strike_fx[index]["age"] = float(strike_fx[index].age) + delta
  if float(strike_fx[index].age) >= strike_fx_duration(str(strike_fx[index].kind)):
   strike_fx.remove_at(index)

func strike_fx_duration(kind: String) -> float:
 return HEAVY_FX_SECONDS if kind == "痛击" else STRIKE_FX_SECONDS

func strike_fx_frame_index(kind: String, age: float) -> int:
 return ATTACK_INK_SOURCE.frame_index(age, strike_fx_duration(kind))


func start_guard_visual(success: bool, direction: Vector2) -> void:
 if direction.is_zero_approx(): direction = Vector2(facing, 0)
 if success:
  # The successful contact replaces only the remaining visual anticipation.
  # It never reopens or prolongs the actual guard_window.
  guard_fx.clear()
 var height: float = skill_vfx.actor_hand_height() if skill_vfx != null else (58.0 if original_wanderer_enabled else 32.0)
 guard_fx.append({"age": 0.0, "success": success, "dir": direction.normalized(), "kind": "架招", "origin": player, "attack_height": height})


func update_guard_fx(delta: float) -> void:
 for index in range(guard_fx.size() - 1, -1, -1):
  if not bool(guard_fx[index].success) and float(guard_fx[index].age) < 0.20:
   guard_fx[index]["origin"] = player
   if skill_vfx != null: guard_fx[index]["attack_height"] = skill_vfx.actor_hand_height()
  guard_fx[index]["age"] = float(guard_fx[index].age) + delta
  var duration := GUARD_SUCCESS_FX_SECONDS if bool(guard_fx[index].success) else GUARD_FX_SECONDS
  if float(guard_fx[index].age) >= duration:
   guard_fx.remove_at(index)


func guard_fx_frame_index(success: bool, progress: float) -> int:
 # The second atlas row contains the contact flash. An empty guard must never
 # imply a successful collision, so anticipation stays on the first four cells.
 var phase := clampf(progress, 0.0, 1.0)
 return mini(4 + int(phase * 4), 7) if success else mini(int(phase * GUARD_FX_SECONDS / 0.20 * 4), 3)


func get_camera_units() -> Array[Dictionary]:
 var units: Array[Dictionary] = []
 if hp > 0: units.append({"position":player, "head_height":130.0, "alive":true})
 if enemy_hp > 0: units.append({"position":enemy, "head_height":46.0 if skinned_wolf != null else 85.0, "alive":true})
 units.append_array(camera_extra_units)
 return units

func _update_trial_camera(delta := 0.0, snap := false) -> void:
 if trial_camera == null: return
 if trial_camera_framer == null: return
 var frame: Dictionary = trial_camera_framer.update(player,get_camera_units(),get_viewport_rect().size,TRIAL_WORLD_SIZE,delta,snap or delta <= 0.0)
 trial_camera.zoom = Vector2.ONE*float(frame.zoom)
 trial_camera.position = frame.center
 trial_camera.force_update_scroll()


func _enter_trial_fullscreen(fullscreen := true) -> void:
 var window := get_window()
 saved_window_state = {
  "mode": DisplayServer.window_get_mode(),
  "size": DisplayServer.window_get_size(),
  "position": DisplayServer.window_get_position(),
  "scale_size": window.content_scale_size,
  "scale_mode": window.content_scale_mode,
  "scale_aspect": window.content_scale_aspect,
 }
 window.content_scale_size = Vector2i(1280, 720)
 window.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
 window.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
 if fullscreen:
  DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)


func _exit_tree() -> void:
 if impact_feedback != null and impact_feedback.battle == self: impact_feedback.reset()
 if saved_window_state.is_empty():
  return
 var window := get_window()
 window.content_scale_size = saved_window_state.scale_size
 window.content_scale_mode = saved_window_state.scale_mode
 window.content_scale_aspect = saved_window_state.scale_aspect
 var restore_mode: int = saved_window_state.mode
 var restore_size: Vector2i = saved_window_state.size
 var restore_position: Vector2i = saved_window_state.position
 DisplayServer.window_set_mode(restore_mode)
 if restore_mode == DisplayServer.WINDOW_MODE_WINDOWED:
  DisplayServer.window_set_size(restore_size)
  DisplayServer.window_set_position(restore_position)
 # The returning menu reapplies video preferences during initialization.
 # Restore the actual entry state once the new scene has finished entering.
 get_tree().create_timer(0.1).timeout.connect(func():
  DisplayServer.window_set_mode(restore_mode)
  if restore_mode == DisplayServer.WINDOW_MODE_WINDOWED:
   DisplayServer.window_set_size(restore_size)
   DisplayServer.window_set_position(restore_position)
 , CONNECT_ONE_SHOT)
 saved_window_state.clear()


func _action_test_path(relative_path: String) -> String:
 var main_project_path = "res://tools/action_battle_test/" + relative_path
 if FileAccess.file_exists(main_project_path): return main_project_path
 return "res://" + relative_path


func _resolve_project_art_path(relative_path: String) -> String:
 var local_resource_path = "res://" + relative_path
 if FileAccess.file_exists(local_resource_path):
  return ProjectSettings.globalize_path(local_resource_path)
 var project_root = ProjectSettings.globalize_path("res://").simplify_path()
 var parent_project_path = project_root.path_join("..").path_join("..").path_join(relative_path).simplify_path()
 return parent_project_path if FileAccess.file_exists(parent_project_path) else ""


func _setup_skinned_wolf() -> void:
 var rig_script = load(_action_test_path("forest_wolf_sequence.gd"))
 if rig_script == null: return
 var rig: Node2D = rig_script.new()
 rig.name = "ForestWolfSequence"
 rig.scale = Vector2.ONE * WOLF_VISUAL_SCALE
 rig.position = enemy + WOLF_VISUAL_FOOT_OFFSET
 add_child(rig)
 if not rig.build(_action_test_path(WOLF_ART_DIR.path_join("sequence.json")).get_base_dir()):
  rig.queue_free()
  push_error("局内凶狼序列帧加载失败")
  return
 skinned_wolf = rig
 wolf_visual_position = enemy
 wolf_visual_hp = enemy_hp
 update_skinned_wolf(0.0)

func update_skinned_wolf(delta: float) -> void:
 if skinned_wolf == null: return
 var velocity: Vector2 = (enemy - wolf_visual_position) / delta if delta > 0.0 else Vector2.ZERO
 if enemy_hp < wolf_visual_hp and enemy_hp > 0: skinned_wolf.trigger_hurt()
 skinned_wolf.position = enemy + WOLF_VISUAL_FOOT_OFFSET
 skinned_wolf.set_motion_context(delta, velocity, wolf_state, wolf_clock, wolf_facing, enemy_hp > 0)
 wolf_visual_position = enemy
 wolf_visual_hp = enemy_hp

func _setup_skinned_player():
 var runtime_script = load(_action_test_path("guigu_rig_runtime.gd"))
 if runtime_script == null or not runtime_script.can_instantiate():
  push_warning("原版蒙皮运行时脚本未能加载，保留旧刚性分件预览")
  return
 var runtime_instance: Node2D = runtime_script.new()
 add_child(runtime_instance)
 var own_manifest_path = _action_test_path("art/own_guigu_fit/manifest.json")
 var art_root = own_manifest_path.get_base_dir().get_base_dir()
 var loaded_rig = runtime_instance.load_runtime(
  _action_test_path("guigu_rig/rig.json"),
  _action_test_path("guigu_rig/skins.json"),
  _action_test_path("guigu_rig/animations.json"),
  art_root
 )
 if not loaded_rig:
  push_warning("原版骨架/蒙皮数据未接入：%s" % str(runtime_instance.get_runtime_summary().errors))
  runtime_instance.queue_free()
  return
 skinned_player = runtime_instance
 skinned_player_ready = true
 skinned_player.set_skin_mode(_active_project_skin_mode())
 skinned_player.position = player
 if original_wanderer_enabled:
  original_wanderer = runtime_script.new()
  add_child(original_wanderer)
  var original_loaded = original_wanderer.load_runtime(
   _action_test_path("guigu_rig/rig.json"), _action_test_path("guigu_rig/skins.json"),
   _action_test_path("guigu_rig/animations.json"), art_root, original_wanderer_art_folder)
  if original_loaded:
   original_wanderer.set_skin_mode("experiment")
  else:
   push_error("原创行旅者骨架加载失败：%s" % str(original_wanderer.get_runtime_summary().errors))
 update_skinned_player()
 print("动作试玩已加载当前人物：", original_wanderer.get_runtime_summary() if original_wanderer != null and original_wanderer.loaded else skinned_player.get_runtime_summary())


func _current_player_animation() -> Array:
 if fist_combo != null and fist_combo.active:
  return ["AttackIdle", fist_combo.progress() * 0.167]
 if visual_attack_time >= 0.0:
  return ["AttackIdle", visual_attack_time]
 if dash_move > 0.0:
  return ["Step1", 0.20 - dash_move]
 if walk_amount > 0.1:
  return ["BackstepWalk" if original_wanderer_enabled and moving_backward else "Run_New1", motion_time]
 return ["Idle", motion_time]


func _active_project_skin_mode() -> String:
 return "experiment" if experimental_bilan_parts else "project"


func update_skinned_player():
 if not skinned_player_ready or skinned_player == null:
  return
 var animation = _current_player_animation()
 # Human combat supplies a raised body origin. Place it before sampling the
 # rig/cloth, while keeping the new sole shadow on the actual ground anchor.
 var visual_origin: Vector2 = call("body_effect_origin") if has_method("body_effect_origin") else player
 skinned_player.position = visual_origin
 var show_original = original_wanderer_enabled and not reference_mode and not experimental_bilan_parts
 skinned_player.visible = not show_original
 if original_wanderer != null:
  original_wanderer.visible = show_original
  original_wanderer.position = visual_origin
  if original_wanderer.wanderer_skin != null and original_wanderer.wanderer_skin.has_method("set_motion_context"):
   if original_wanderer.wanderer_skin.has_method("set_ground_elevation"):
    original_wanderer.wanderer_skin.set_ground_elevation(player.y-visual_origin.y)
   original_wanderer.wanderer_skin.set_motion_context(visual_motion_delta, visual_motion_velocity, motion_time, facing)
   if original_wanderer.wanderer_skin.has_method("set_action_context"):
    original_wanderer.wanderer_skin.set_action_context(guard_window > 0.0 or not pending_self_skill.is_empty(), not pending_attack.is_empty())
   if original_wanderer.wanderer_skin.has_method("set_combo_context"):
    original_wanderer.wanderer_skin.set_combo_context(fist_combo.stage + 1 if fist_combo != null and fist_combo.active else 0, fist_combo.progress() if fist_combo != null and fist_combo.active else 0.0)
  _update_outfit_weapon_context()
  original_wanderer.set_character_pose(str(animation[0]), float(animation[1]), facing, hurt_visual / 0.2)
 skinned_player.set_character_pose(str(animation[0]), float(animation[1]), facing, hurt_visual / 0.2)

func _outfit_sword_layer() -> RefCounted:
 if not original_wanderer_enabled or original_wanderer_art_folder != "yuxu_fullbody_v15" or original_wanderer == null: return null
 var skin: Variant = original_wanderer.wanderer_skin
 return skin.approved_sword if skin != null else null

func _outfit_unarmed_action() -> bool:
 return not pending_attack.is_empty() or not pending_self_skill.is_empty() or guard_window > 0.0 or visual_attack_time >= 0.0 or (fist_combo != null and fist_combo.active)

func _update_outfit_weapon_context() -> void:
 var sword: Variant = _outfit_sword_layer()
 if sword == null: return
 # The arena's existing attacks retain their own unarmed animation and rules.
 # This switch only exposes the already-approved prop and inspection motion.
 if not outfit_sword_visible or _outfit_unarmed_action():
  if sword.mode != "off": sword.set_mode("off")
 elif sword.mode == "off":
  sword.set_mode("hold")

func set_outfit_sword_visible(value: bool) -> bool:
 if _outfit_sword_layer() == null: return false
 outfit_sword_visible = value
 _update_outfit_weapon_context()
 return true

func preview_outfit_swing() -> bool:
 var sword: Variant = _outfit_sword_layer()
 if sword == null or combat_paused() or _outfit_unarmed_action() or hp <= 0 or enemy_hp <= 0: return false
 outfit_sword_visible = true
 sword.set_mode("hold")
 sword.restart_swing(false)
 return true


func export_user_capture(source_path: String, destination_path: String) -> String:
 var image = Image.new()
 var load_error = image.load(source_path)
 if load_error != OK:
  return "load error %d" % load_error
 var save_error = image.save_png(destination_path)
 return "saved" if save_error == OK else "save error %d" % save_error

func _input(event):
 if event is InputEventKey and event.pressed and not event.echo:
  if event.keycode in [KEY_9, KEY_0] and not configuring and not (battle_hud != null and battle_hud.help_open):
   var accepted := set_outfit_sword_visible(not outfit_sword_visible) if event.keycode == KEY_9 else preview_outfit_swing()
   if accepted:
    get_viewport().set_input_as_handled()
    return
  if event.keycode == KEY_ESCAPE:
   var viewport = get_viewport()
   if viewport != null:
    viewport.set_input_as_handled()
   if attribute_runtime != null:
    attribute_runtime.request_close()
    return
   var menu_scene = "res://tscn/scene/StartMenu.tscn"
   if ResourceLoader.exists(menu_scene):
    var error = get_tree().change_scene_to_file(menu_scene)
    if error != OK:
     push_error("返回游戏首页失败：%s" % error_string(error))
   else:
    get_tree().quit()
   return
  if event.keycode == KEY_G:
   reference_mode = not reference_mode
   if skinned_player_ready:
    skinned_player.set_skin_mode("reference" if reference_mode else _active_project_skin_mode())
   queue_redraw()
   return
  if event.keycode == KEY_H:
   experimental_bilan_parts = not experimental_bilan_parts
   reference_mode = false
   if skinned_player_ready:
    skinned_player.set_skin_mode(_active_project_skin_mode())
   queue_redraw()
   return
  if event.keycode == KEY_TAB:
   configuring = not configuring
   held_basic_attack = false
   selected_slot = -1
   get_viewport().set_input_as_handled()
   return
  if configuring and event.keycode >= KEY_1 and event.keycode <= KEY_5:
   var slot = event.keycode - KEY_1
   if selected_slot < 0: selected_slot = slot
   else:
    var previous = skill_slots[slot]
    skill_slots[slot] = skill_slots[selected_slot]
    skill_slots[selected_slot] = previous
    selected_slot = -1
   return
  if not configuring:
   if event.keycode == KEY_Q: use_slot(1)
   if event.keycode == KEY_E: use_slot(2)
   if event.keycode == KEY_R: use_slot(3)
   if event.keycode == KEY_F: use_slot(4)
   if event.keycode == KEY_SPACE: use_skill("错步")
 if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
  held_basic_attack = event.pressed and not configuring
  if held_basic_attack: use_skill("拳掌")
 if not configuring and event is InputEventMouseButton and event.pressed:
  if event.button_index == MOUSE_BUTTON_RIGHT: use_slot(0)

func use_slot(index: int):
 if index < 0 or index >= skill_slots.size(): return
 use_skill(skill_slots[index])

func configurable_skills() -> Array[String]:
 var result: Array[String] = ["痛击", "架招"]
 result.append_array(available_self_skills)
 result.append("")
 return result

func assign_skill_slot(index: int, skill: String) -> bool:
 if not configuring or index < 0 or index >= skill_slots.size(): return false
 if skill not in configurable_skills(): return false
 var other := skill_slots.find(skill) if not skill.is_empty() else -1
 if other >= 0: skill_slots[other] = skill_slots[index]
 skill_slots[index] = skill
 selected_slot = -1
 return true

func _notification(what: int) -> void:
 if what == NOTIFICATION_WM_WINDOW_FOCUS_OUT:
  held_basic_attack = false
  clear_combo_input()

func update_stamina_recovery(seconds: float) -> void:
 if stamina < 6.0:
  stamina_clock += seconds
  while stamina_clock >= stamina_recovery_interval:
   stamina = minf(6.0, stamina+1.0)
   stamina_clock -= stamina_recovery_interval
 else:
  stamina_clock = 0.0

func ensure_fist_combo() -> void:
 if fist_combo != null: return
 fist_combo = load(_action_test_path("fist_combo_runtime.gd")).new()
 fist_combo.setup(self)

func get_manual_arena() -> Node2D:
 return self

func manual_unit_index() -> int:
 return 0

func basic_attack_reach_multiplier() -> float:
 return action_manual.basic_reach_multiplier() if action_manual != null else 1.0

func clear_combo_input() -> void:
 if fist_combo != null: fist_combo.clear_input()

func attack_aim_direction() -> Vector2:
 var direction: Vector2 = (get_global_mouse_position() - player).normalized()
 return direction if not direction.is_zero_approx() else Vector2(facing, 0)

func clamp_player_to_arena() -> void:
 if original_wanderer_enabled:
  player = player.clamp(Vector2(50, 50), TRIAL_WORLD_SIZE - Vector2(50, 50))
 else:
  player = player.clamp(Vector2(50, 160), Vector2(1230, 680))

func use_skill(name: String):
 if action_manual != null and name == action_manual.RULES.SKILL:
  action_manual.try_use_skill()
  return
 if hp <= 0 or enemy_hp <= 0 or not SKILL_RULES.has(name): return
 if combat_paused(): return
 if name == "拳掌":
  ensure_fist_combo()
  fist_combo.request()
  return
 if name in ["催血", "聚炁"] and name not in available_self_skills: return
 if float(skill_cooldowns[name]) > 0 or skill_is_active(name) or dash_move > 0: return
 if (not pending_attack.is_empty() or not pending_self_skill.is_empty()) and name != "错步": return
 var cost = skill_cost(name)
 if stamina < cost:
  show_feedback("体力不足")
  return
 if fist_combo != null and fist_combo.active: fist_combo.finish()
 stamina -= cost
 if action_manual != null: action_manual.skill_started(name)
 if name == "痛击":
  pending_attack = name
  release_timer = float(SKILL_RULES[name].delay)
  attack_lock = release_timer
  attack_direction = attack_aim_direction()
  visual_attack_time = 0.0
 elif name == "架招":
  guard_window = 0.2
  start_guard_visual(false, get_global_mouse_position() - player)
  show_feedback("架招")
 elif name == "错步":
  var direction = movement_direction()
  if direction == Vector2.ZERO: direction = Vector2(-facing, 0)
  cancel_pending_attack()
  cancel_self_cast()
  if guard_window > 0.0: finish_guard()
  dash_direction = direction
  dash_timer = 0.0
  dash_move = 0.20
  invulnerable = 0.15
  show_feedback("错步")
 elif name in ["催血", "聚炁"]:
  pending_self_skill = name
  self_cast_remaining = float(SKILL_RULES[name].delay)
  held_basic_attack = false
  show_feedback("%s · 运功" % name)

func combat_paused() -> bool:
 return configuring or (battle_hud != null and battle_hud.help_open)

func skill_is_active(name: String) -> bool:
 if action_manual != null and name == action_manual.RULES.SKILL: return action_manual.buff_remaining > 0.0
 return (name == "拳掌" and fist_combo != null and fist_combo.active) or pending_attack == name or pending_self_skill == name or (name == "错步" and dash_move > 0.0) or (name == "架招" and guard_window > 0.0)

func start_skill_cooldown(name: String, elapsed_after_end := 0.0) -> void:
 var duration := skill_cooldown_duration(name)
 skill_cooldown_totals[name] = duration
 skill_cooldowns[name] = maxf(0.0, duration - elapsed_after_end)

func cancel_pending_attack() -> void:
 if not pending_attack.is_empty(): start_skill_cooldown(pending_attack)
 pending_attack = ""
 release_timer = 0.0
 attack_lock = 0.0

func finish_guard(elapsed_after_end := 0.0) -> void:
 guard_window = 0.0
 start_skill_cooldown("架招", elapsed_after_end)

func cancel_self_cast(start_cooldown := true) -> void:
 if start_cooldown and not pending_self_skill.is_empty(): start_skill_cooldown(pending_self_skill)
 pending_self_skill = ""
 self_cast_remaining = 0.0

func clear_finished_battle() -> void:
 if action_manual != null: action_manual.reset()
 if skill_vfx != null: skill_vfx.clear_visuals()
 if fist_combo != null: fist_combo.clear()
 pending_attack = ""
 release_timer = 0.0
 attack_lock = 0.0
 held_basic_attack = false
 cancel_self_cast(false)
 guard_window = 0.0
 dash_move = 0.0
 invulnerable = 0.0
 qi_blood = 0
 qi = 0
 sync_resource_statuses()

func sync_resource_statuses() -> void:
 for status in ["气血", "炁"]: visible_statuses.erase(status)
 if qi_blood > 0: visible_statuses.append("气血")
 if qi > 0: visible_statuses.append("炁")

func complete_self_cast(elapsed_after_end := 0.0) -> void:
 var name := pending_self_skill
 cancel_self_cast(false)
 if hp <= 0 or enemy_hp <= 0 or name.is_empty(): return
 start_skill_cooldown(name, elapsed_after_end)
 var amount := int(SKILL_RULES[name].gain)
 if name == "催血" and action_manual != null: amount = action_manual.blood_gain(amount)
 if name == "催血":
  var gain := mini(amount, maxi(0, qi_blood_capacity - qi_blood))
  qi_blood += gain
  if skill_vfx != null: skill_vfx.complete(name, gain)
  show_feedback("气血+%d" % gain if gain > 0 else "气血已满")
 else:
  qi += amount
  if skill_vfx != null: skill_vfx.complete(name, amount, mana_capacity > 0 and qi >= mana_capacity * 2)
  if mana_capacity > 0 and qi >= mana_capacity * 2:
   qi = 0
   var recovered := maxi(0, mana_capacity - mana)
   mana = mana_capacity
   if action_manual != null: get_manual_arena().attribute_runtime.restore_unit_mana(manual_unit_index())
   elif attribute_runtime != null: attribute_runtime.restore_action_mana()
   show_feedback("周天 · 法力+%d" % recovered if recovered > 0 else "周天 · 法力已满")
  else: show_feedback("炁+%d" % amount)
 sync_resource_statuses()

func self_cast_progress() -> float:
 if pending_self_skill.is_empty(): return 0.0
 return clampf(1.0 - self_cast_remaining / float(SKILL_RULES[pending_self_skill].delay), 0.0, 1.0)

func movement_multiplier() -> float:
 if pending_self_skill.is_empty() or hp <= 0 or enemy_hp <= 0: return 1.0
 return float(SKILL_RULES[pending_self_skill].move_multiplier)

func skill_cost(name: String) -> int:
 if action_manual != null and name == action_manual.RULES.SKILL: return action_manual.RULES.SKILL_COST
 return int(SKILL_RULES[name].cost) if SKILL_RULES.has(name) else 0

func skill_cooldown_duration(name: String) -> float:
 if action_manual != null and name == action_manual.RULES.SKILL: return action_manual.RULES.COOLDOWN
 match name:
  "拳掌": return fist_cooldown()
  "痛击": return heavy_strike_cooldown()
  "错步": return dodge_cooldown()
  "架招": return guard_cooldown()
  "催血": return blood_cooldown()
  "聚炁": return qi_cooldown()
 return 0.0

func fist_cooldown() -> float:
 return ACTION_SKILL_RULES.cooldown("拳掌", physique, soul, dodge, intelligence)

func heavy_strike_cooldown() -> float:
 return ACTION_SKILL_RULES.cooldown("痛击", physique, soul, dodge, intelligence)

func dodge_cooldown() -> float:
 return ACTION_SKILL_RULES.cooldown("错步", physique, soul, dodge, intelligence)

func guard_cooldown() -> float:
 return ACTION_SKILL_RULES.cooldown("架招", physique, soul, dodge, intelligence)

func blood_cooldown() -> float:
 return ACTION_SKILL_RULES.cooldown("催血", physique, soul, dodge, intelligence)

func qi_cooldown() -> float:
 return ACTION_SKILL_RULES.cooldown("聚炁", physique, soul, dodge, intelligence)

func skill_minimum_cooldown(name: String) -> float:
 if action_manual != null and name == action_manual.RULES.SKILL: return action_manual.RULES.COOLDOWN
 return float(SKILL_RULES[name].min_cooldown) if SKILL_RULES.has(name) else 0.0

func skill_cooldown_total(name: String) -> float:
 return float(skill_cooldown_totals.get(name, skill_cooldown_duration(name)))

func cooldown_formula_text(name: String) -> String:
 if action_manual != null and name == action_manual.RULES.SKILL: return "强化结束后冷却%s秒" % String.num(action_manual.RULES.COOLDOWN, 0)
 return ACTION_SKILL_RULES.cooldown_formula(name)

func skill_cooldown_remaining(name: String) -> float:
 return float(skill_cooldowns.get(name, 0.0))

func prepare_attack_release(elapsed_after_end := 0.0) -> Dictionary:
 var name = pending_attack
 pending_attack = ""
 release_timer = 0.0
 attack_lock = 0.0
 if hp <= 0 or enemy_hp <= 0 or name.is_empty(): return {}
 var combo_hit: bool = name == "拳掌" and fist_combo != null and fist_combo.active
 if not combo_hit: start_skill_cooldown(name, elapsed_after_end)
 strike_kind = name
 strike_direction = attack_direction
 attack_reach = fist_combo.reach if combo_hit else float(SKILL_RULES[name].reach)
 basic_strike_origin = player
 basic_strike_impact = enemy
 basic_strike_effect = 0.22
 var amount: int = fist_combo.damage if combo_hit else (roundi(11.0 + physique * 0.75) if name == "痛击" else roundi(7.0 + physique * 0.45))
 return {"name":name, "damage":amount, "combo_hit":combo_hit,
  "is_basic_attack":combo_hit, "is_combo_last":combo_hit and fist_combo.is_last_stage(),
  "combo_id":fist_combo.chain_id if combo_hit else 0,
  "final_stage_base_damage":amount,
  "release_offset":maxf(0.0, action_manual.step_seconds - elapsed_after_end) if action_manual != null else 0.0,
  "effect":{
  "age": 0.0, "kind": name, "origin": basic_strike_origin,
  "dir": strike_direction, "reach": attack_reach,
  "hit": false, "impact": basic_strike_impact,
  "visual_key": str(fist_combo.STAGES[fist_combo.stage].visual) if combo_hit else ("拳掌_掌" if name == "拳掌" and strike_visual_rng.randi_range(0, 1) == 1 else name),
  "combo_stage": fist_combo.stage + 1 if combo_hit else 0,
  "chain_id": fist_combo.chain_id if combo_hit else 0,
  "visual_height": float(fist_combo.STAGES[fist_combo.stage].height) if combo_hit else 1.0,
 }}

func resolve_pending_attack(elapsed_after_end := 0.0) -> void:
 var release := prepare_attack_release(elapsed_after_end)
 if release.is_empty(): return
 var name: String = release.name
 var combo_hit: bool = release.combo_hit
 var to_enemy = enemy - player
 basic_strike_hit = to_enemy.length() <= attack_reach and strike_direction.dot(to_enemy) > 0
 release.effect.hit = basic_strike_hit
 strike_fx.append(release.effect)
 if basic_strike_hit:
  if attribute_runtime != null:
   var result: Dictionary = attribute_runtime.resolve_combo_attack(fist_combo.damage) if combo_hit else attribute_runtime.resolve_attack(true, name, false)
   apply_attribute_result(result, name)
   if result.get("evaded", false):
    basic_strike_hit = false
    strike_fx.back()["hit"] = false
  else:
   var amount: int = release.damage
   enemy_hp = maxi(0, enemy_hp - amount)
   if impact_feedback != null:
    impact_feedback.report_world(self, null, {"damage":amount}, name, "本体", to_global(enemy-Vector2(0,40)), strike_direction, enemy.y)
   add_combat_text(enemy, amount, "enemy")
   show_feedback("%s 命中 %d" % [name, amount])
 else: show_feedback("%s 落空" % name)

func update_skill_timers(delta: float) -> void:
 if hp <= 0 or enemy_hp <= 0:
  clear_finished_battle()
  return
 if combat_paused(): return
 if action_manual != null: action_manual.begin_step(delta)
 for name in skill_cooldowns:
  skill_cooldowns[name] = maxf(0.0, float(skill_cooldowns[name]) - delta)
  if skill_cooldowns[name] < 0.000001: skill_cooldowns[name] = 0.0
 if guard_window > 0:
  var remaining_guard: float = guard_window - delta
  guard_window = maxf(0.0, remaining_guard)
  if remaining_guard <= 0.000001: finish_guard(maxf(0.0, -remaining_guard))
 if dash_move > 0:
  var remaining_dash: float = dash_move - delta
  dash_move = maxf(0.0, remaining_dash)
  if remaining_dash <= 0.000001:
   dash_move = 0.0
   start_skill_cooldown("错步", maxf(0.0, -remaining_dash))
 invulnerable = maxf(0.0, invulnerable - delta)
 if fist_combo != null and fist_combo.active:
  fist_combo.advance(delta)
 elif not pending_attack.is_empty():
  release_timer -= delta
  attack_lock = maxf(0.0, release_timer)
  if release_timer <= 0.000001: resolve_pending_attack(maxf(0.0, -release_timer))
 if not pending_self_skill.is_empty():
  var remaining_cast := self_cast_remaining - delta
  self_cast_remaining = maxf(0.0, remaining_cast)
  if remaining_cast <= 0.000001: complete_self_cast(maxf(0.0, -remaining_cast))
 dash_timer = skill_cooldown_remaining("错步")
 if action_manual != null: action_manual.finish_step(delta)
 if hp <= 0 or enemy_hp <= 0: clear_finished_battle()

func movement_direction() -> Vector2:
 return Vector2(float(Input.is_physical_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT))-float(Input.is_physical_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT)), float(Input.is_physical_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN))-float(Input.is_physical_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP))).normalized()

func move_character(direction: Vector2, seconds: float) -> void:
 # Split the completion frame: movement is restricted only for the actual
 # remaining cast time, including when the frame straddles its end.
 var forced := minf(seconds, action_manual.knockback_remaining) if action_manual != null else 0.0
 var restricted := maxf(0.0, minf(seconds, self_cast_remaining) - forced) if not pending_self_skill.is_empty() else 0.0
 var movement_seconds := seconds - forced - restricted + restricted * movement_multiplier()
 player += direction.normalized() * move_speed * movement_seconds

func add_combat_text(at: Vector2, value: int, side: String, kind := "damage", critical := false) -> void:
 # Overhead mode owns its own marks; do not duplicate its presentation.
 if battle_hud == null or battle_hud.get_script().resource_path.get_file() != "action_battle_hud.gd": return
 if kind == "damage" and value <= 0: return
 if kind != "damage":
  for mark in combat_texts:
   if str(mark.side) == side and str(mark.kind) == kind and float(mark.age)<0.15: return
 var lane := 0
 for mark in combat_texts:
  if str(mark.side)==side and str(mark.kind)==kind and float(mark.age)<0.28: lane+=1
 # Store the settled hit position, never the current mouse or actor location.
 var height := 83.0 if side=="player" else 57.0
 combat_texts.append({"point":at+Vector2(0,-height),"value":value,"side":side,
  "kind":kind,"critical":critical,"age":0.0,"lifetime":0.76,"lane":lane%3})

func update_combat_texts(delta: float) -> void:
 for index in range(combat_texts.size()-1,-1,-1):
  combat_texts[index]["age"] = float(combat_texts[index].age)+delta
  if float(combat_texts[index].age)>=float(combat_texts[index].lifetime): combat_texts.remove_at(index)

func show_feedback(value: String):
 feedback = value
 feedback_timer = 0.7
 if battle_hud != null: battle_hud.notify_feedback(value)

func _process(delta):
 delta = minf(delta, 0.05)
 visual_motion_delta = delta
 visual_motion_velocity = Vector2.ZERO
 elapsed += delta
 if hp <= 0 or enemy_hp <= 0: clear_finished_battle()
 if configuring or (battle_hud != null and battle_hud.help_open):
  visual_motion_delta = 0.0
  held_basic_attack = false
  clear_combo_input()
  update_skinned_player()
  update_skinned_wolf(0.0)
  queue_redraw()
  return
 motion_time += delta
 if visual_attack_time >= 0:
  visual_attack_time += delta
  if visual_attack_time > 0.167: visual_attack_time = -1.0
 attack_effect = maxf(0, attack_effect-delta)
 basic_strike_effect = maxf(0, basic_strike_effect-delta)
 update_strike_fx(delta)
 update_guard_fx(delta)
 update_combat_texts(delta)
 hurt_visual = maxf(0, hurt_visual-delta)
 feedback_timer = maxf(0, feedback_timer-delta)
 enemy_timer -= delta
 var dash_step = minf(dash_move, delta)
 if hp > 0 and enemy_hp > 0:
  var previous_player = player
  var direction = movement_direction()
  update_stamina_recovery(delta)
  walk_amount = move_toward(walk_amount, 1.0 if direction != Vector2.ZERO else 0.0, delta * 8.0)
  if direction != Vector2.ZERO: walk_phase += delta * 11.0
  if dash_step > 0: player += dash_direction * 500.0 * dash_step
  else: move_character(direction, delta)
  if action_manual != null: action_manual.advance_displacement(delta)
  if original_wanderer_enabled:
   player = player.clamp(Vector2(50, 50), TRIAL_WORLD_SIZE - Vector2(50, 50))
  else:
   player = player.clamp(Vector2(50,160), Vector2(1230,680))
  visual_motion_velocity = (player-previous_player)/maxf(delta,0.001)
  facing = signf(inspection_facing_override) if inspection_facing_override!=0.0 else (1.0 if get_global_mouse_position().x >= player.x else -1.0)
  update_skill_timers(delta)
  visual_motion_velocity = (player-previous_player)/maxf(delta,0.001)
  update_wolf(delta, previous_player)
  if held_basic_attack and not configuring and not (fist_combo != null and fist_combo.active): use_skill("拳掌")
  if fist_combo != null and fist_combo.active and absf(fist_combo.direction.x) > 0.000001:
   facing = signf(fist_combo.direction.x)
  moving_backward = direction.x * facing < -0.1
 if hp <= 0 or enemy_hp <= 0: clear_finished_battle()

 if Input.is_key_pressed(KEY_T) and (hp <= 0 or enemy_hp <= 0): restart_battle()
 update_skinned_player()
 update_skinned_wolf(delta)
 _update_trial_camera(delta)
 queue_redraw()

func restart_battle() -> void:
 if impact_feedback != null and impact_feedback.battle == self: impact_feedback.reset()
 clear_finished_battle()
 if original_wanderer != null and original_wanderer.wanderer_skin != null and original_wanderer.wanderer_skin.has_method("reset_secondary_motion"):
  original_wanderer.wanderer_skin.reset_secondary_motion()
 player = Vector2(400, 500)
 enemy = Vector2(1000, 400)
 hp = max_hp
 enemy_hp = enemy_max_hp
 wolf_state = "chase"
 wolf_clock = 0.0
 wolf_stamina = 6.0
 wolf_stamina_clock = 0.0
 wolf_contact_resolved = false
 stamina = 6.0
 stamina_clock = 0.0
 enemy_shots.clear()
 enemy_timer = 1.0
 guard_window = 0.0
 attack_effect = 0.0
 basic_strike_effect = 0.0
 dash_timer = 0.0
 dash_move = 0.0
 invulnerable = 0.0
 visual_attack_time = -1.0
 hurt_visual = 0.0
 feedback = ""
 feedback_timer = 0.0
 combat_texts.clear()
 strike_fx.clear()
 guard_fx.clear()
 for name in skill_cooldowns: skill_cooldowns[name] = 0.0
 skill_cooldown_totals.clear()
 mana = mana_capacity
 if trial_camera_framer != null: trial_camera_framer.reset()
 if attribute_runtime != null:
  attribute_runtime.reset_battle()
  sync_character_attributes()
 if battle_hud != null: battle_hud.reset_feedback()
 if skinned_wolf != null:
  skinned_wolf.position = enemy + WOLF_VISUAL_FOOT_OFFSET
  skinned_wolf.reset_pose()
  wolf_visual_position = enemy
  wolf_visual_hp = enemy_hp
  update_skinned_wolf(0.0)

# Physical bite uses the existing skill formula and one stamina.
func update_wolf(delta: float, previous_player: Vector2 = Vector2.INF):
 if previous_player == Vector2.INF: previous_player = player
 if wolf_stamina < 6.0:
  wolf_stamina_clock += delta
  if wolf_stamina_clock >= 2.0:
   wolf_stamina = minf(6.0, wolf_stamina + 1.0)
   wolf_stamina_clock -= 2.0
 else: wolf_stamina_clock = 0.0
 var diff = player - enemy
 if diff.x != 0: wolf_facing = -1.0 if diff.x < 0 else 1.0
 if wolf_state == "chase":
  if diff.length() <= WOLF_REACH and wolf_stamina >= 1:
   wolf_stamina -= 1
   wolf_state = "windup"
   wolf_clock = WOLF_WINDUP
   wolf_direction = diff.normalized()
  elif diff.length() > WOLF_REACH:
   enemy += diff.normalized() * WOLF_SPEED * delta
 elif wolf_state == "windup":
  wolf_clock -= delta
  if wolf_clock <= 0:
   wolf_state = "lunge"
   wolf_clock = WOLF_LUNGE_SECONDS
   wolf_contact_resolved = false
 elif wolf_state == "lunge":
  var previous_enemy = enemy
  enemy += wolf_direction * WOLF_LUNGE_SPEED * minf(delta, wolf_clock)
  wolf_clock -= delta
  # Sweep the existing bite radius across both moving actors; resolve one contact.
  var relative_start = previous_player - previous_enemy
  var relative_end = player - enemy
  var closest = Geometry2D.get_closest_point_to_segment(Vector2.ZERO, relative_start, relative_end)
  if not wolf_contact_resolved and relative_start.dot(wolf_direction) >= 0 and closest.length() <= WOLF_REACH:
   wolf_contact_resolved = true
   if invulnerable <= 0: receive_wolf_bite()
   else: add_combat_text(player,0,"player","evade")
  if wolf_clock <= 0:
   wolf_state = "recover"
   wolf_clock = WOLF_RECOVERY
 elif wolf_state == "recover":
  wolf_clock -= delta
  if wolf_clock <= 0: wolf_state = "chase"
 if original_wanderer_enabled:
  enemy = enemy.clamp(Vector2(50, 50), TRIAL_WORLD_SIZE - Vector2(50, 50))

func receive_wolf_bite():
 if hp <= 0 or enemy_hp <= 0: return
 if invulnerable > 0:
  if impact_feedback != null:
   impact_feedback.report_world(null,self,{"evaded":true},"撕咬","本体",to_global(player-Vector2(0,40)),(player-enemy).normalized(),player.y)
  return
 if attribute_runtime != null:
  var result: Dictionary = attribute_runtime.resolve_attack(false, "撕咬", guard_window > 0)
  if result.get("guarded", false):
   finish_guard()
   start_guard_visual(true, enemy - player)
  apply_attribute_result(result, "撕咬")
  return
 var damage = roundi(7.0 + wolf_physique * 0.7 + wolf_physique * 0.8)
 var actual_block := 0
 if guard_window > 0:
  var block = 3 + floori(physique / 4.0) + floori(minf(physique * 0.25, soul * 0.20))
  damage = maxi(0, damage - block)
  actual_block = block
  finish_guard()
  start_guard_visual(true, enemy - player)
  add_combat_text(player, block, "player", "block")
  show_feedback("架招成功 · 化解 %d，受伤 %d" % [block, damage])
 else: show_feedback("撕咬伤害 %d" % damage)
 if damage > 0: hurt_visual = 0.2
 hp = maxi(0, hp - damage)
 add_combat_text(player, damage, "player")
 if impact_feedback != null:
  impact_feedback.report_world(null,self,{"damage":damage,"blocked":actual_block},"撕咬","本体",to_global(player-Vector2(0,40)),(player-enemy).normalized(),player.y)

func sync_character_attributes() -> void:
 var state: Dictionary = attribute_runtime.get_state()
 character_name = state.player_name
 hp = state.hp
 max_hp = state.max_hp
 enemy_hp = state.enemy_hp
 enemy_max_hp = state.enemy_max_hp
 physique = state.physique
 soul = state.soul
 intelligence = state.intelligence
 dodge = state.dodge
 wolf_physique = state.wolf_physique
 mana = state.mana
 mana_capacity = state.mana_capacity
 qi_blood_capacity = state.qi_blood_capacity
 move_speed = attribute_runtime.get_move_speed()
 stamina_recovery_interval = 1.0 / attribute_runtime.get_stamina_recovery_base()

func apply_attribute_result(result: Dictionary, skill_name: String) -> void:
 if result.get("inactive", true): return
 hp = result.hp
 enemy_hp = result.enemy_hp
 var target_side := "player" if skill_name=="撕咬" else "enemy"
 var target_point: Vector2 = player if target_side=="player" else enemy
 if result.evaded:
  add_combat_text(target_point,0,target_side,"evade")
  show_feedback("%s · 属性闪避" % skill_name)
 elif result.guarded:
  add_combat_text(target_point,int(result.blocked),target_side,"block")
  show_feedback("架招成功 · 化解 %d，受伤 %d" % [result.blocked, result.damage])
 else:
  show_feedback("%s · %s %d" % [skill_name, "会心" if result.critical else "命中", result.damage])
 if not result.evaded: add_combat_text(target_point,int(result.damage),target_side,"damage",bool(result.critical))
 if skill_name == "撕咬" and result.damage > 0: hurt_visual = 0.2
 if impact_feedback != null:
  var incoming := skill_name == "撕咬"
  var impact_direction: Vector2 = (player-enemy).normalized() if incoming else strike_direction
  impact_feedback.report_world(null if incoming else self,self if incoming else null,result,skill_name,"本体",to_global(target_point-Vector2(0,40)),impact_direction,target_point.y)

# Generated ink-force textures are presentation only; damage still resolves once
# in resolve_pending_attack(), independently of frame count or overlapping effects.
func draw_basic_strike():
 if skill_vfx != null: return
 for effect in strike_fx:
  var kind := str(effect.kind)
  var visual_key := str(effect.get("visual_key", kind))
  if not strike_fx_textures.has(visual_key): continue
  var texture: Texture2D = strike_fx_textures[visual_key]
  var progress := clampf(float(effect.age) / strike_fx_duration(kind), 0.0, 1.0)
  var frame := strike_fx_frame_index(kind, float(effect.age))
  var direction: Vector2 = effect.dir
  var origin: Vector2 = effect.origin
  var reach := float(effect.reach)
  # A square source keeps the fist/palm legible. Only an eight-pixel early push
  # is added; its far edge never travels beyond this attack's existing reach.
  var destination := strike_fx_destination_rect(texture, reach, progress)
  var finish_fade := 1.0 - smoothstep(0.55, 1.0, progress)
  # The approved robed character's hand is higher than the older small puppet.
  # Mirror left-facing strikes rather than rotating the upright palm upside down.
  var hand_height := 58.0 if original_wanderer_enabled else 32.0
  var mirrored := direction.x < 0.0
  var angle := direction.angle() + (PI if mirrored else 0.0)
  draw_set_transform(origin + Vector2(0, -hand_height), angle, Vector2(-1.0 if mirrored else 1.0, 1.0))
  draw_texture_rect_region(texture, destination, strike_fx_frame_rect(texture, frame), Color(1, 1, 1, finish_fade), false, true)
  draw_set_transform(Vector2.ZERO)

func draw_guard_fx() -> void:
 if skill_vfx != null: return
 if not strike_fx_textures.has("架招"): return
 var texture: Texture2D = strike_fx_textures["架招"]
 var source_size := strike_fx_frame_rect(texture, 0).size
 for effect in guard_fx:
  var success := bool(effect.success)
  var duration := GUARD_SUCCESS_FX_SECONDS if success else GUARD_FX_SECONDS
  var progress := clampf(float(effect.age) / duration, 0.0, 1.0)
  var frame := guard_fx_frame_index(success, progress)
  var width := 100.0 if original_wanderer_enabled else 82.0
  var height := width * source_size.y / source_size.x
  var destination := Rect2(-width * 0.30, -height * 0.5, width, height)
  var direction: Vector2 = effect.dir
  var mirrored := direction.x < 0.0
  var angle := direction.angle() + (PI if mirrored else 0.0)
  var hand_height := 58.0 if original_wanderer_enabled else 32.0
  var age := float(effect.age)
  var alpha := 1.0 - smoothstep(0.2, 1.0, progress)
  if not success:
   # The faint residue beyond 0.20s is visual only, not a held guard.
   alpha = 1.0 if age <= 0.20 else 0.25 * (1.0 - smoothstep(0.20, GUARD_FX_SECONDS, age))
  var tint := Color(1.12, 1.07, 1.0, alpha) if success else Color(1, 1, 1, alpha)
  var origin: Vector2 = effect.get("origin", player)
  draw_set_transform(origin + Vector2(0, -hand_height), angle, Vector2(-1.0 if mirrored else 1.0, 1.0))
  draw_texture_rect_region(texture, destination, strike_fx_frame_rect(texture, frame), tint, false, true)
  draw_set_transform(Vector2.ZERO)

func draw_enemy() -> void:
 if original_wanderer_enabled:
  draw_set_transform(enemy + (WOLF_VISUAL_FOOT_OFFSET if skinned_wolf != null else Vector2(0, 12)), 0.0, Vector2(1.0, 0.3))
  draw_circle(Vector2.ZERO, 17 if skinned_wolf != null else 25, Color(0, 0, 0, 0.14))
  draw_set_transform(Vector2.ZERO)
 else:
  draw_circle(enemy + Vector2(0,12), 30, Color(0,0,0,0.17))
 var wolf_texture = wolf_attack if wolf_state == "lunge" and wolf_attack != null else wolf_idle
 if skinned_wolf != null:
  pass
 elif wolf_texture != null:
  draw_set_transform(enemy, 0, Vector2(-wolf_facing, 1))
  draw_texture_rect(wolf_texture, Rect2(-70,-76,140,94), false)
  draw_set_transform(Vector2.ZERO)
 else: draw_circle(enemy, 30, Color("804c46"))
 if wolf_state == "windup": draw_arc(enemy, 48, 0, TAU, 40, Color("d75f4b"), 4)
 var wolf_bar := Rect2(enemy.x-25,enemy.y-57,50,5) if skinned_wolf != null else Rect2(enemy.x-35,enemy.y-90,70,6)
 draw_rect(wolf_bar, Color("493b34"))
 draw_rect(Rect2(wolf_bar.position, Vector2(wolf_bar.size.x*clampf(float(enemy_hp)/maxi(enemy_max_hp,1),0.0,1.0),wolf_bar.size.y)), Color("bd5850"))

func _draw():
 if skill_vfx != null: skill_vfx.sync_visuals()
 if battle_ground and original_wanderer_enabled:
  # Repeat at native resolution so the expanded arena does not enlarge the texture.
  for x in range(2):
   for y in range(2):
    # Mirror neighboring copies so their adjoining edges meet continuously.
    draw_set_transform(Vector2(x * 3344, y * 1882), 0.0, Vector2(-1 if x else 1, -1 if y else 1))
    # Keep the stone subdued and slightly cool so the pale figure reads clearly.
    draw_texture(battle_ground, Vector2.ZERO, Color(0.93, 0.96, 1.0))
  draw_set_transform(Vector2.ZERO)
 elif battle_ground:
  draw_texture_rect(battle_ground, Rect2(0,0,1280,720), false)
 else:
  draw_rect(Rect2(Vector2.ZERO, TRIAL_WORLD_SIZE if original_wanderer_enabled else Vector2(1280, 720)), Color("aeb99a"))
 draw_enemy()
 if not strike_fx.is_empty():
  draw_basic_strike()
 if not guard_fx.is_empty(): draw_guard_fx()
 for s in enemy_shots: draw_circle(s[0], 9, Color("d75f4b"))
 if original_wanderer_enabled and not reference_mode and not experimental_bilan_parts:
  var has_shoe_contacts = original_wanderer != null and original_wanderer.wanderer_skin != null and original_wanderer.wanderer_skin.has_method("has_contact_shadow")
  if not has_shoe_contacts:
   draw_set_transform(player + Vector2(0, 4), 0.0, Vector2(1.0, 0.25))
   draw_circle(Vector2.ZERO, 14, Color(0,0,0,0.18))
   draw_set_transform(Vector2.ZERO)
 else:
  draw_circle(player + Vector2(0,5), 22, Color(0,0,0,0.18))
 var stride = sin(walk_phase) * walk_amount
 var bounce = absf(sin(walk_phase)) * 1.5 * walk_amount
 if not skinned_player_ready and reference_mode and not reference_parts.is_empty():
  draw_reference_player(stride, bounce)
 elif not skinned_player_ready and player_parts != null:
  # Drive the project's rigid cutouts with the corresponding original bone curves.
  var clip_name = "Idle"
  var clip_time = motion_time
  if visual_attack_time >= 0:
   clip_name = "AttackIdle"
   clip_time = visual_attack_time
  elif dash_move > 0:
   clip_name = "Step1"
   clip_time = 0.18 - dash_move
  elif walk_amount > 0.1:
   clip_name = "Run_New1"
  var far_arm = clampf(original_bone_delta(clip_name, clip_time, "yifu/yifu/shenti1/zuobi/zuobi/zuobi1", -90.62), -0.40, 0.40)
  var near_arm = clampf(original_bone_delta(clip_name, clip_time, "yifu/yifu/shenti1/youbi/youbi/youbi1", -62.844), -0.40, 0.40)
  var far_leg = clampf(original_bone_delta(clip_name, clip_time, "Foot/youtui/youtui/tui1", -94.618), -0.48, 0.48)
  var near_leg = clampf(original_bone_delta(clip_name, clip_time, "Foot/zuotui/zuotui/tui1", -84.506), -0.48, 0.48)
  draw_player_part(Rect2(265, 557, 125, 345), Rect2(-12, -77, 13, 35), Vector2(-6, -74), far_arm, bounce)
  draw_player_part(Rect2(755, 578, 190, 315), Rect2(-9, -39, 13, 39), Vector2(-4, -36), far_leg, bounce)
  draw_player_part(Rect2(755, 578, 190, 315), Rect2(0, -39, 13, 39), Vector2(4, -36), near_leg, bounce)
  draw_player_part(Rect2(735, 25, 230, 480), Rect2(-14, -82, 29, 65), Vector2.ZERO, 0, bounce)
  draw_player_part(Rect2(1300, 108, 130, 345), Rect2(1, -77, 13, 35), Vector2(5, -74), near_arm, bounce)
  draw_player_part(Rect2(200, 160, 260, 240), Rect2(-17, -103, 30, 29), Vector2.ZERO, 0, bounce)
 draw_set_transform(Vector2.ZERO)
 var font = ThemeDB.fallback_font
 # Combat results are drawn by the screen-space HUD; debug feedback stays internal.

# Streamed animation keys are cubic coefficients from the local source clip.
# Bone rotations are converted from Unity's Y-up degrees to Godot Y-down radians.
func original_bone_delta(clip_name: String, time: float, path: String, rest_degrees: float) -> float:
 if not original_clips.has(clip_name): return 0.0
 var clip: Dictionary = original_clips[clip_name]
 var index = -1
 for binding in clip.bindings:
  if binding.path == path and int(binding.attribute) == 4:
   index = int(binding.index) + 2
   break
 if index < 0 or index >= int(clip.stream_curve_count): return 0.0
 var duration = float(clip.duration)
 var t = fposmod(time, duration) if clip_name in ["Idle", "Run_New1"] and duration > 0 else minf(time, duration)
 var key: Array = []
 var key_time = 0.0
 for frame in clip.frames:
  if float(frame[0]) > t: break
  for candidate in frame[1]:
   if int(candidate[0]) == index:
    key = candidate
    key_time = float(frame[0])
    break
 if key.is_empty(): return 0.0
 var dt = maxf(0.0, t - key_time)
 var value = ((float(key[1]) * dt + float(key[2])) * dt + float(key[3])) * dt + float(key[4])
 return deg_to_rad(-(value - rest_degrees))


func draw_reference_player(stride: float, bounce: float):
 # Exact source sprite offsets / rotations from the original sample prefab;
 # limb rotations below are provisional Godot animation, not extracted clips.
 var order = ["/Foot/youtui/", "/Foot/zuotui/", "/Body/yifu/yifu", "/shenti1/zuobi/", "/shenti1/youbi/youbi", "/tou/101(Clone)/lian", "/tou/toufa/"]
 for key in order:
  for part in reference_parts:
   var data = part.data
   if key == "/Body/yifu/yifu":
    if not str(data.path).ends_with(key): continue
   elif key == "/shenti1/youbi/youbi":
    if not str(data.path).ends_with(key): continue
   elif not key in str(data.path): continue
   var p = Vector2(float(data.position[0]), float(data.position[1]))
   var angle = float(data.angle)
   if "/Foot/youtui/" in key: angle += stride * 0.25
   if "/Foot/zuotui/" in key: angle -= stride * 0.25
   if "/shenti1/zuobi/" in key: angle += stride * 0.15
   if "/shenti1/youbi/" in key:
    angle -= stride * 0.15
    if attack_effect > 0: angle -= 0.35
    elif guard_window > 0: angle -= 0.2
   var tex: Texture2D = part.texture
   var origin = player + Vector2(p.x * facing, p.y - bounce)
   draw_set_transform(origin, angle * facing, Vector2(facing, 1))
   draw_texture(tex, -Vector2(tex.get_size()) * 0.5)


func draw_player_part(source: Rect2, dest: Rect2, pivot: Vector2, angle: float, bounce: float):
 var recoil = -facing * 3.0 * minf(hurt_visual / 0.2, 1.0)
 var origin = player + Vector2(pivot.x * facing + recoil, pivot.y - bounce)
 draw_set_transform(origin, angle * facing, Vector2(facing, 1))
 var tint = Color(1.0, 0.78, 0.78) if hurt_visual > 0 else Color.WHITE
 draw_texture_rect_region(player_parts, Rect2(dest.position - pivot, dest.size), source, tint)


