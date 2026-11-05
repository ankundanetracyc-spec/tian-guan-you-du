extends "action_battle_feel_test.gd"

const CONTACT_SPACE = preload("human_contact_space.gd")
var arena: Node2D
var elevation := 0.0
var unit_index := 0
var approach_angle := 0.0
var ai_move := Vector2.ZERO
var ai_enabled := true
var action_label := "接近"
var original_tint := Color.WHITE
# Tactical pause is a decision delay, not a skill cooldown.
var ai_reposition_time := 0.0
var ai_was_attacking := false
var ai_circle_sign := 1.0

func _ready() -> void:
 pass # The enclosing arena owns the camera, input, ground and HUD.

func _input(_event: InputEvent) -> void:
 pass

func initialize(owner_arena: Node2D, index: int) -> void:
 arena = owner_arena
 unit_index = index
 name = "HumanNPC%d" % index
 original_wanderer_enabled = true
 original_wanderer_art_folder = "yuxu_fullbody_v15"
 available_self_skills = []
 strike_fx_textures = arena.strike_fx_textures
 approach_angle = (index - 1) * TAU / 3.0
 original_tint = [Color("e9d9ca"), Color("c7d9d7"), Color("ddd3e5")][index - 1]
 load_state()
 var rig: Node2D = load(_action_test_path("guigu_rig_runtime.gd")).new()
 add_child(rig)
 var art_root := _action_test_path("art/own_guigu_fit/manifest.json").get_base_dir().get_base_dir()
 assert(rig.load_runtime(_action_test_path("guigu_rig/rig.json"), _action_test_path("guigu_rig/skins.json"), _action_test_path("guigu_rig/animations.json"), art_root, original_wanderer_art_folder))
 rig.set_skin_mode("experiment")
 rig.modulate = original_tint
 original_wanderer = rig
 skinned_player = rig
 skinned_player_ready = true
 impact_feedback = arena.impact_feedback
 if impact_feedback != null: impact_feedback.register_actor(self)
 skill_vfx = load(_action_test_path("quiet_skill_vfx.gd")).new()
 skill_vfx.setup(self, _action_test_path("quiet_force.gdshader"))
 add_child(skill_vfx)
 set_process(false)
 set_process_input(false)
 update_skinned_player()

func load_state() -> void:
 elevation = 0.0
 clear_finished_battle()
 var state: Dictionary = arena.attribute_runtime.get_unit_state(unit_index)
 character_name = state.name
 hp = state.hp
 max_hp = state.max_hp
 physique = state.physique
 soul = state.soul
 intelligence = state.intelligence
 dodge = state.dodge
 mana = state.mana
 mana_capacity = state.mana_capacity
 qi_blood_capacity = state.qi_blood_capacity
 move_speed = arena.attribute_runtime.get_move_speed()
 stamina_recovery_interval = 1.0 / arena.attribute_runtime.get_stamina_recovery_base()
 enemy_hp = arena.hp
 enemy_max_hp = arena.max_hp
 stamina = 6.0
 stamina_clock = 0.0
 for skill in skill_cooldowns: skill_cooldowns[skill] = 0.0
 skill_cooldown_totals.clear()
 strike_fx.clear()
 guard_fx.clear()
 visual_attack_time = -1.0
 hurt_visual = 0.0
 motion_time = 0.0
 walk_amount = 0.0
 held_basic_attack = false
 ai_move = Vector2.ZERO
 ai_reposition_time = 0.0
 ai_was_attacking = false
 ai_circle_sign = 1.0 if unit_index % 2 == 1 else -1.0
 action_label = "接近"

func combat_paused() -> bool:
 return arena == null or arena.combat_paused()

func clear_finished_battle() -> void:
 super.clear_finished_battle()
 strike_fx.clear()
 guard_fx.clear()

func attack_aim_direction() -> Vector2:
 var direction: Vector2 = (arena.player - player).normalized()
 return direction if not direction.is_zero_approx() else Vector2(facing, 0)

func attack_plane_height() -> float:
 return CONTACT_SPACE.hand_height(self)

func body_effect_origin() -> Vector2:
 return player - Vector2(0, elevation)

func movement_direction() -> Vector2:
 return ai_move

func get_manual_arena() -> Node2D:
 return arena

func manual_unit_index() -> int:
 return unit_index

func use_skill(skill: String) -> void:
 if action_manual != null:
  if skill not in ["拳掌", "错步"] and skill not in skill_slots: return
 elif skill not in ["拳掌", "痛击", "错步", "架招"]: return
 super.use_skill(skill)

func resolve_pending_attack(elapsed_after_end := 0.0) -> void:
 arena.resolve_fighter_attack(self, elapsed_after_end)

func _think() -> void:
 ai_move = Vector2.ZERO
 if not ai_enabled: return
 var to_player: Vector2 = arena.player - player
 var distance := to_player.length()
 var threatened: bool = arena.player_is_preparing_toward(player, self)
 if threatened and dash_move <= 0.0 and stamina >= 1.0:
  if pending_attack.is_empty() and skill_cooldown_remaining("架招") == 0 and guard_window <= 0.0 and (action_manual == null or "架招" in skill_slots):
   held_basic_attack = false
   use_skill("架招")
   action_label = "架招"
   return
  if guard_window <= 0.0 and skill_cooldown_remaining("错步") == 0:
   held_basic_attack = false
   ai_move = -to_player.normalized()
   use_skill("错步")
   action_label = "错步"
   return
 if dash_move > 0.0 or guard_window > 0.0: return
 ensure_fist_combo()
 var reach: float = fist_combo.preview(0).reach
 # Prepare longer self skills only with room, after checking immediate danger.
 if not threatened and distance > reach * 1.4 and action_manual != null and arena.manual_think(self): return
 if fist_combo.active or not pending_attack.is_empty():
  ai_was_attacking = true
  if distance > reach * 1.15: ai_move = to_player.normalized()
  action_label = fist_combo.STAGES[fist_combo.stage].name if fist_combo.active else pending_attack
  return
 held_basic_attack = false
 if ai_was_attacking:
  ai_was_attacking = false
  ai_reposition_time = 1.1
  ai_circle_sign = -ai_circle_sign
 if ai_reposition_time > 0.0:
  # Create space if crowded, otherwise circle while watching the player.
  if distance < reach * 1.15:
   ai_move = -to_player.normalized()
   action_label = "拉开"
  elif distance < reach * 2.5:
   ai_move = to_player.normalized().orthogonal() * ai_circle_sign * 0.55
   action_label = "侧移"
  else:
   action_label = "观察"
  return
 if distance <= float(SKILL_RULES["痛击"].reach) and stamina >= 1.0 and skill_cooldown_remaining("痛击") == 0 and (action_manual == null or "痛击" in skill_slots):
  use_skill("痛击")
  ai_was_attacking = not pending_attack.is_empty()
  action_label = "痛击"
 elif distance <= reach and skill_cooldown_remaining("拳掌") == 0:
  held_basic_attack = true
  use_skill("拳掌")
  ai_was_attacking = fist_combo.active or not pending_attack.is_empty()
  action_label = "探拳"
 else:
  var goal: Vector2 = arena.player + Vector2.from_angle(approach_angle) * reach * 0.80
  var travel: Vector2 = goal - player
  ai_move = travel.normalized() if travel.length() > move_speed * 0.05 else Vector2.ZERO
  # Steering only: allies neither block nor push one another.
  var avoidance := Vector2.ZERO
  for other in arena.npcs:
   if other == self or other.hp <= 0: continue
   var apart: Vector2 = player - other.player
   if apart.length() > 0.001 and apart.length() < reach * 0.8:
    avoidance += apart.normalized() * (1.0 - apart.length() / (reach * 0.8))
  if not ai_move.is_zero_approx(): ai_move = (ai_move + avoidance).normalized()
  action_label = "接近" if not ai_move.is_zero_approx() else "观察"

func advance_fighter(delta: float) -> void:
 enemy_hp = arena.hp
 enemy = arena.player
 if hp <= 0 or enemy_hp <= 0:
  clear_finished_battle()
  action_label = "倒地" if hp <= 0 else "收势"
  update_skinned_player()
  return
 if combat_paused(): return
 var previous: Vector2 = player
 ai_reposition_time = maxf(0.0, ai_reposition_time - delta)
 _think()
 motion_time += delta
 update_stamina_recovery(delta)
 update_strike_fx(delta)
 update_guard_fx(delta)
 hurt_visual = maxf(0.0, hurt_visual - delta)
 if visual_attack_time >= 0.0:
  visual_attack_time += delta
  if visual_attack_time > 0.167: visual_attack_time = -1.0
 var dash_step := minf(delta, dash_move)
 if dash_step > 0.0: player += dash_direction * 500.0 * dash_step
 else: move_character(ai_move, delta)
 if action_manual != null: action_manual.advance_displacement(delta)
 clamp_player_to_arena()
 update_skill_timers(delta)
 visual_motion_delta = delta
 visual_motion_velocity = (player - previous) / maxf(delta, 0.001)
 walk_amount = move_toward(walk_amount, 1.0 if not visual_motion_velocity.is_zero_approx() else 0.0, delta * 8.0)
 var aim: Vector2 = fist_combo.direction if fist_combo != null and fist_combo.active else attack_aim_direction()
 if absf(aim.x) > 0.000001: facing = signf(aim.x)
 moving_backward = ai_move.x * facing < -0.1
 update_skinned_player()
 queue_redraw()

func update_skinned_player() -> void:
 if original_wanderer == null: return
 var animation: Array = _current_player_animation()
 original_wanderer.position = body_effect_origin()
 original_wanderer.z_index = int(player.y)
 original_wanderer.modulate = Color(original_tint, 0.28 if hp <= 0 else 1.0)
 var skin: RefCounted = original_wanderer.wanderer_skin
 if skin.has_method("set_ground_elevation"): skin.set_ground_elevation(elevation)
 skin.set_motion_context(visual_motion_delta, visual_motion_velocity if hp > 0 else Vector2.ZERO, motion_time, facing)
 skin.set_action_context(guard_window > 0.0 or not pending_self_skill.is_empty(), not pending_attack.is_empty())
 skin.set_combo_context(fist_combo.stage + 1 if fist_combo != null and fist_combo.active else 0, fist_combo.progress() if fist_combo != null and fist_combo.active else 0.0)
 original_wanderer.set_character_pose(str(animation[0]), float(animation[1]), facing, hurt_visual / 0.2)

func _draw() -> void:
 if skill_vfx != null: skill_vfx.sync_visuals()
 if not guard_fx.is_empty(): draw_guard_fx()
