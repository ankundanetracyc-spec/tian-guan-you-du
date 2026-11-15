extends "action_battle_feel_test.gd"

# A separate presentation of the existing four-skill / single-wolf fixture.
# Costs, damage, cooldowns, delayed release and wolf contact stay in the parent.
const INK := Color("303e37")
const JADE := Color("54766c")
const RUST := Color("9c5142")
const PAPER := Color("ede7d4")
var aim_direction := Vector2.RIGHT
var paused := false
var focus_paused := false
var ready_to_start := false
var clothing_open := false
var clothing_source := "男修衣着样例"
var floating_marks: Array[Dictionary] = []
var dash_echoes: Array[Dictionary] = []
var actor_velocity := Vector2.ZERO
var wolf_motion := 0.0
var visual_time := 0.0
var avatar: Node2D
var wolf_avatar: Node2D
var forest: Node2D
var _echo_clock := 0.0
var _pose_delta := 0.0


func _ready() -> void:
 name = "TopDownBattleTrial"
 available_self_skills.clear() # Separate overhead experiment retains four skills.
 _enter_trial_fullscreen(false)
 strike_visual_rng.randomize()
 _load_strike_fx()
 forest = load(_action_test_path("topdown_forest.gd")).new()
 forest.z_index = -10
 add_child(forest)
 var actor_script = load(_action_test_path("topdown_clothing_rig.gd"))
 avatar = actor_script.new()
 avatar.z_index = -1
 add_child(avatar)
 wolf_avatar = load(_action_test_path("topdown_actor.gd")).new()
 wolf_avatar.is_wolf = true
 wolf_avatar.z_index = -1
 add_child(wolf_avatar)
 var hud_layer := CanvasLayer.new()
 hud_layer.name = "TopDownHudLayer"
 hud_layer.layer = 20
 add_child(hud_layer)
 battle_hud = load(_action_test_path("topdown_hud.gd")).new()
 battle_hud.battle = self
 hud_layer.add_child(battle_hud)
 read_creation_clothing()
 restart_trial()
 ready_to_start = true
 paused = true
 clothing_open = "--clothing-preview" in OS.get_cmdline_user_args()


func restart_trial() -> void:
 if fist_combo != null: fist_combo.clear()
 hp = max_hp
 enemy_hp = enemy_max_hp
 player = Vector2(480, 430)
 enemy = Vector2(825, 385)
 aim_direction = (enemy - player).normalized()
 stamina = 6.0
 mana = mana_capacity
 stamina_clock = 0.0
 wolf_stamina = 6.0
 wolf_stamina_clock = 0.0
 wolf_state = "chase"
 wolf_clock = 0.0
 wolf_direction = Vector2.LEFT
 wolf_contact_resolved = false
 pending_attack = ""
 release_timer = 0.0
 attack_lock = 0.0
 guard_window = 0.0
 dash_move = 0.0
 dash_timer = 0.0
 invulnerable = 0.0
 hurt_visual = 0.0
 basic_strike_effect = 0.0
 visual_attack_time = -1.0
 feedback = ""
 feedback_timer = 0.0
 _echo_clock = 0.0
 visual_time = 0.0
 wolf_motion = 0.0
 held_basic_attack = false
 paused = false
 focus_paused = false
 ready_to_start = false
 clothing_open = false
 configuring = false
 selected_slot = -1
 actor_velocity = Vector2.ZERO
 strike_fx.clear()
 guard_fx.clear()
 floating_marks.clear()
 dash_echoes.clear()
 for skill in skill_cooldowns:
  skill_cooldowns[skill] = 0.0
 skill_cooldown_totals.clear()
 if battle_hud != null:
  battle_hud.help_open = false
  battle_hud.reset_feedback()
 if avatar != null:
  avatar.reset_motion()
 _update_actors()
 queue_redraw()


func _notification(what: int) -> void:
 if what == NOTIFICATION_WM_WINDOW_FOCUS_OUT:
  held_basic_attack = false
  clear_combo_input()
  if hp > 0 and enemy_hp > 0:
   paused = true
   focus_paused = true


func toggle_pause() -> void:
 paused = not paused
 focus_paused = false
 ready_to_start = false
 held_basic_attack = false
 # A pause also drops any queued next strike.
 clear_combo_input()


func toggle_clothing() -> void:
 clothing_open = not clothing_open
 held_basic_attack = false
 clear_combo_input()
 configuring = false
 battle_hud.help_open = false


func apply_clothing(id: String) -> bool:
 if not avatar.set_outfit(id):
  return false
 clothing_source = "仅本次试玩试装"
 _update_actors()
 return true


func read_creation_clothing() -> bool:
 var source := get_node_or_null("/root/GameStartProfile")
 var catalog := get_node_or_null("/root/PortraitCatalog")
 if source == null or catalog == null:
  return false
 var data: Resource = source.call("create_player_appearance_preview")
 if data == null or int(data.get("gender")) != 0:
  clothing_source = "男修衣着样例 · 女修俯视衣服待制作"
  return false
 var context: Dictionary = catalog.call("get_context", data, "male", 16, "", "")
 var requested: Dictionary = catalog.call("get_requested_appearance", data)
 var selected: Dictionary = catalog.call("select_portrait", context, requested)
 var clothing: Dictionary = selected.get("clothes", {})
 if str(clothing.get("package_id", "")) != avatar.library.PACKAGE:
  return false
 if not avatar.set_outfit(str(clothing.get("id", ""))):
  return false
 clothing_source = "读取建角衣着 · 不改建角设定"
 return true


func _input(event: InputEvent) -> void:
 if battle_hud == null:
  return
 # Always release the held input, including when the pointer is over a panel.
 if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
  held_basic_attack = false
 if event is InputEventMouse:
  var direction: Vector2 = get_global_mouse_position() - player
  if direction.length_squared() > 0.001:
   aim_direction = direction.normalized()
 if event is InputEventKey and event.pressed and not event.echo:
  if event.keycode == KEY_V:
   toggle_clothing()
   get_viewport().set_input_as_handled()
   return
  if event.keycode == KEY_ESCAPE and clothing_open:
   toggle_clothing()
   get_viewport().set_input_as_handled()
   return
  if event.keycode == KEY_F1:
   held_basic_attack = false
   return # The HUD owns help and closes it before Escape can leave the scene.
  if event.keycode == KEY_ESCAPE and (battle_hud.help_open or configuring):
   return
  if event.keycode == KEY_ESCAPE:
   super._input(event)
   return
  if event.keycode == KEY_P and not battle_hud.help_open and not configuring:
   toggle_pause()
   get_viewport().set_input_as_handled()
   return
  if event.keycode == KEY_T and not battle_hud.help_open and not configuring:
   restart_trial()
   get_viewport().set_input_as_handled()
   return
  if event.keycode in [KEY_G, KEY_H]:
   return
 if battle_hud.help_open or paused or clothing_open:
  held_basic_attack = false
  return
 if event is InputEventMouse and battle_hud.pointer_over_ui():
  return
 super._input(event)


func attack_aim_direction() -> Vector2:
 return aim_direction

func use_skill(skill: String) -> void:
 if paused or configuring or clothing_open or (battle_hud != null and battle_hud.help_open):
  return
 var previous_cd := skill_cooldown_remaining(skill)
 var was_active := skill_is_active(skill)
 super.use_skill(skill)
 if previous_cd > 0.0 or was_active or not skill_is_active(skill):
  return
 # Facing is now a full overhead direction. These adaptations change no costs,
 # damage, radius, travel length, invulnerability or cooldowns.
 if skill == pending_attack:
  attack_direction = aim_direction
 if skill == "错步" and movement_direction() == Vector2.ZERO:
  dash_direction = -aim_direction


func resolve_pending_attack(elapsed_after_end := 0.0) -> void:
 var previous_hp: int = enemy_hp
 super.resolve_pending_attack(elapsed_after_end)
 if enemy_hp < previous_hp:
  floating_marks.append({"at": enemy + Vector2(0, -78), "text": str(previous_hp - enemy_hp), "age": 0.0, "color": INK})


func receive_wolf_bite() -> void:
 var previous_hp: int = hp
 var had_guard: bool = guard_window > 0.0
 super.receive_wolf_bite()
 if hp < previous_hp:
  floating_marks.append({"at": player + Vector2(0, -40), "text": "−%d" % (previous_hp - hp), "age": 0.0, "color": RUST})
 if had_guard and guard_window <= 0.0:
  floating_marks.append({"at": player + Vector2(0, -63), "text": "化解", "age": 0.0, "color": JADE})


func _process(delta: float) -> void:
 if paused or clothing_open:
  held_basic_attack = false
  clear_combo_input()
  queue_redraw()
  return
 var previous_player: Vector2 = player
 var previous_enemy: Vector2 = enemy
 var blocked: bool = configuring or (battle_hud != null and battle_hud.help_open)
 if hp > 0 and enemy_hp > 0 and not blocked:
  var pointer_direction: Vector2 = get_global_mouse_position() - player
  if pointer_direction.length_squared() > 0.001:
   aim_direction = pointer_direction.normalized()
 super._process(delta)
 if blocked:
  return
 var step := minf(delta, 0.05)
 _pose_delta = step
 actor_velocity = (player - previous_player) / maxf(step, 0.001)
 if hp > 0 and enemy_hp > 0:
  visual_time += step
  wolf_motion += previous_enemy.distance_to(enemy) * 0.12
 _echo_clock -= step
 if dash_move > 0.0 and _echo_clock <= 0.0:
  dash_echoes.append({"at": player, "angle": aim_direction.angle(), "age": 0.0})
  _echo_clock = 0.035
 for marks in [floating_marks, dash_echoes]:
  for index in range(marks.size() - 1, -1, -1):
   marks[index]["age"] = float(marks[index].age) + step
   if float(marks[index].age) > (0.8 if marks == floating_marks else 0.24):
    marks.remove_at(index)
 _update_actors()
 queue_redraw()


func _update_actors() -> void:
 if avatar == null or wolf_avatar == null:
  return
 avatar.position = player
 avatar.rotation = aim_direction.angle()
 if fist_combo != null and fist_combo.active:
  avatar.rotation = fist_combo.direction.angle()
 avatar.motion = visual_time * 12.5
 avatar.movement = clampf(actor_velocity.length() / move_speed, 0.0, 1.0)
 avatar.local_velocity = actor_velocity.rotated(-avatar.rotation) / move_speed
 avatar.attack = 0.0
 if visual_attack_time >= 0.0:
  avatar.attack = sin(clampf(visual_attack_time / 0.167, 0.0, 1.0) * PI)
 if fist_combo != null and fist_combo.active:
  avatar.attack = sin(fist_combo.progress() * PI)
 avatar.guarding = guard_window > 0.0
 avatar.dashing = dash_move > 0.0
 avatar.hurt = hurt_visual > 0.0
 avatar.defeated = hp <= 0
 avatar.update_pose(_pose_delta)
 _pose_delta = 0.0
 wolf_avatar.position = enemy
 var wolf_heading: Vector2 = wolf_direction if wolf_state in ["windup", "lunge", "recover"] else (player - enemy).normalized()
 wolf_avatar.rotation = wolf_heading.angle()
 wolf_avatar.motion = wolf_motion
 wolf_avatar.movement = 1.0 if wolf_state in ["chase", "lunge"] and enemy_hp > 0 and hp > 0 else 0.0
 wolf_avatar.attack = 1.0 if wolf_state == "lunge" else 0.0
 wolf_avatar.guarding = wolf_state == "windup"
 wolf_avatar.defeated = enemy_hp <= 0
 wolf_avatar.queue_redraw()


func _draw() -> void:
 if battle_hud == null or battle_hud.font == null:
  return
 if enemy_hp > 0:
  if wolf_state == "windup":
   var progress := 1.0 - clampf(wolf_clock / WOLF_WINDUP, 0.0, 1.0)
   var endpoint: Vector2 = enemy + wolf_direction * 130.0
   draw_line(enemy + wolf_direction * 32.0, endpoint, Color(RUST, 0.25 + progress * 0.4), 2.0, true)
   draw_polyline(PackedVector2Array([endpoint - wolf_direction.rotated(-0.65) * 13.0, endpoint, endpoint - wolf_direction.rotated(0.65) * 13.0]), Color(RUST, 0.7), 2.0, true)
   draw_arc(enemy, 48.0, -PI / 2.0, -PI / 2.0 + TAU * maxf(progress, 0.001), 48, Color(RUST, 0.45), 1.5, true)
  var hp_at: Vector2 = enemy + Vector2(-37, -48)
  draw_rect(Rect2(hp_at, Vector2(74, 4)), Color(INK, 0.18))
  draw_rect(Rect2(hp_at, Vector2(74.0 * enemy_hp / enemy_max_hp, 4)), RUST)
  var state_label := {"chase": "林地凶狼", "windup": "蓄势", "lunge": "扑咬", "recover": "收势"}
  _world_text(str(state_label[wolf_state]), enemy + Vector2(0, -56), RUST if wolf_state == "windup" else INK, 14)
 if hp > 0 and enemy_hp > 0 and not paused and not battle_hud.help_open and not configuring:
  draw_arc(player, 32.0, 0, TAU, 48, Color(JADE, 0.24), 1.0, true)
  var heading := aim_direction.angle()
  draw_arc(player, 70.0, heading - 0.45, heading + 0.45, 24, Color(JADE, 0.48), 1.2, true)
  var cursor := get_global_mouse_position()
  if not battle_hud.pointer_over_ui():
   draw_circle(cursor, 4.0, Color(INK, 0.6), false, 1.0, true)
   draw_line(cursor - Vector2(8, 0), cursor - Vector2(5, 0), INK, 1.0, true)
   draw_line(cursor + Vector2(8, 0), cursor + Vector2(5, 0), INK, 1.0, true)
   draw_line(cursor - Vector2(0, 8), cursor - Vector2(0, 5), INK, 1.0, true)
   draw_line(cursor + Vector2(0, 8), cursor + Vector2(0, 5), INK, 1.0, true)
 for echo in dash_echoes:
  var opacity := 0.16 * (1.0 - float(echo.age) / 0.24)
  draw_set_transform(echo.at, float(echo.angle))
  draw_colored_polygon(PackedVector2Array([Vector2(-29, -13), Vector2(10, -22), Vector2(20, 0), Vector2(10, 22), Vector2(-29, 13)]), Color(JADE, opacity))
 draw_set_transform(Vector2.ZERO)
 _draw_overhead_effects()
 for mark in floating_marks:
  _world_text(str(mark.text), mark.at - Vector2(0, float(mark.age) * 28), Color(mark.color, 1.0 - smoothstep(0.4, 0.8, float(mark.age))), 19)


func _world_text(value: String, at: Vector2, tint: Color, size_px: int) -> void:
 var width: float = battle_hud.font.get_string_size(value, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px).x
 draw_string_outline(battle_hud.font, at - Vector2(width * 0.5, 0), value, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px, 3, Color(PAPER, tint.a * 0.85))
 draw_string(battle_hud.font, at - Vector2(width * 0.5, 0), value, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px, tint)


func _draw_overhead_effects() -> void:
 for effect in strike_fx:
  var kind := str(effect.kind)
  var visual_key := str(effect.get("visual_key", kind))
  if not strike_fx_textures.has(visual_key):
   continue
  var texture: Texture2D = strike_fx_textures[visual_key]
  var progress := clampf(float(effect.age) / strike_fx_duration(kind), 0.0, 1.0)
  var direction: Vector2 = effect.dir
  # Zero height offset: both contact and art live on the overhead ground plane.
  draw_set_transform(effect.origin, direction.angle())
  draw_texture_rect_region(texture, strike_fx_destination_rect(texture, float(effect.reach), progress), strike_fx_frame_rect(texture, strike_fx_frame_index(kind, float(effect.age))), Color(1, 1, 1, 1.0 - smoothstep(0.55, 1.0, progress)), false, true)
 for effect in guard_fx:
  var success := bool(effect.success)
  var duration := GUARD_SUCCESS_FX_SECONDS if success else GUARD_FX_SECONDS
  var age := float(effect.age)
  var progress := clampf(age / duration, 0.0, 1.0)
  var alpha := 1.0 - smoothstep(0.2, 1.0, progress)
  if not success and age > 0.20:
   alpha = 0.25 * (1.0 - smoothstep(0.20, GUARD_FX_SECONDS, age))
  var texture: Texture2D = strike_fx_textures.get("架招")
  if texture == null:
   continue
  var source := strike_fx_frame_rect(texture, guard_fx_frame_index(success, progress))
  var direction: Vector2 = effect.dir
  draw_set_transform(effect.get("origin", player), direction.angle())
  draw_texture_rect_region(texture, Rect2(-12, -41.0 * source.size.y / source.size.x, 82, 82.0 * source.size.y / source.size.x), source, Color(1, 1, 1, alpha), false, true)
 draw_set_transform(Vector2.ZERO)
