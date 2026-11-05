extends RefCounted
## Per-fighter manual state. Attack identity comes from release metadata, not a weapon name.

const RULES = preload("action_manual_rules.gd")
var fighter: Node2D
var config: Dictionary = {}
var chapter := 0
var role := 0
var qualified := false
var buff_remaining := 0.0
var buff_fan := RULES.WAVE_FAN
var next_combo_ready := false
var combo_bonus := 0.0
var combo_id := -1
var last_wave_combo_id := -1
var blood_cast_bonus := false
var guard_amount := 0
var knockback_remaining := 0.0
var knockback_velocity := Vector2.ZERO
var step_seconds := 0.0
var step_buff := 0.0
var last_skill_failure := ""

func setup(owner_fighter: Node2D, settings: Dictionary) -> void:
 fighter = owner_fighter
 config = settings.duplicate(true)
 chapter = clampi(int(config.get("chapter", 0)), 1, 4) if bool(config.get("book", false)) else 0
 role = clampi(int(config.get("role", 0)), 0, 2) if chapter > 0 else 0
 qualified = int(fighter.get_manual_arena().attribute_runtime.get_control_profile(fighter.manual_unit_index()).realm_tier) >= 1
 fighter.skill_cooldowns[RULES.SKILL] = 0.0
 reset()

func reset() -> void:
 buff_remaining = 0.0
 step_buff = 0.0
 step_seconds = 0.0
 buff_fan = RULES.WAVE_FAN
 next_combo_ready = false
 combo_bonus = 0.0
 combo_id = -1
 last_wave_combo_id = -1
 blood_cast_bonus = false
 guard_amount = 0
 last_skill_failure = ""
 knockback_remaining = 0.0
 knockback_velocity = Vector2.ZERO

func heart_active() -> bool:
 return chapter > 0 and role > 0 and qualified

func rewrite_active(index: int) -> bool:
 if not heart_active() or chapter <= index: return false
 var bindings: Array = config.get("bindings", [])
 if index >= bindings.size() or str(bindings[index]) != RULES.REWRITE_TARGETS[index]: return false
 return index == 3 or RULES.REWRITE_TARGETS[index] in fighter.skill_slots

func basic_reach_multiplier() -> float:
 return RULES.BASIC_REACH_MULTIPLIER if rewrite_active(3) else 1.0

func blood_extension(blood: int) -> float:
 return (RULES.BLOOD_REACH_BASE + RULES.BLOOD_REACH_PER_LAYER * blood) * (RULES.MAIN_REACH_MULTIPLIER if role == 1 else 1.0)

func blood_fraction() -> float:
 return RULES.BLOOD_FOURTH if chapter >= 4 else RULES.BLOOD_BASE

func combo_started(identity: int) -> void:
 combo_id = identity
 combo_bonus = 0.0
 if next_combo_ready and (fighter.qi_blood > 0 or buff_remaining > 0.0):
  combo_bonus = RULES.NEXT_COMBO_BONUS
  next_combo_ready = false

func combo_finished() -> void:
 combo_id = -1
 combo_bonus = 0.0

func skill_started(name: String) -> void:
 if name == "催血": blood_cast_bonus = rewrite_active(0) and fighter.qi_blood <= 2
 if name == "架招":
  guard_amount = 3 + floori(fighter.physique / 4.0) + floori(minf(fighter.physique * 0.25, fighter.soul * 0.20))
  if rewrite_active(1) and fighter.qi_blood >= 4: guard_amount = roundi(guard_amount * 1.20)

func blood_gain(base: int) -> int:
 var result := base + (1 if blood_cast_bonus else 0)
 blood_cast_bonus = false
 return result

func lower_blood(count: int) -> int:
 var spent := mini(maxi(count, 0), fighter.qi_blood)
 if spent <= 0: return 0
 fighter.qi_blood -= spent
 var arena: Node2D = fighter.get_manual_arena()
 var healed: int = arena.attribute_runtime.heal_blood_fall(fighter.manual_unit_index(), spent)
 arena.refresh_health()
 if healed > 0: arena.add_combat_text(fighter.player, healed, arena.unit_side(fighter), "heal")
 if heart_active() and chapter >= 3: next_combo_ready = true
 fighter.sync_resource_statuses()
 return spent

func skill_block_reason() -> String:
 if chapter < 2: return "需学会第二卷"
 if not qualified: return "需云息及以上境界"
 if RULES.SKILL not in fighter.skill_slots: return "招式未装配"
 if fighter.hp <= 0 or fighter.enemy_hp <= 0: return "战斗已结束"
 if fighter.combat_paused(): return "对练尚未开始或已暂停"
 if buff_remaining > 0.0: return "效果尚未结束"
 var cooldown: float = fighter.skill_cooldown_remaining(RULES.SKILL)
 if cooldown > 0.0: return "冷却中 · 剩余%.1f秒" % (ceilf(cooldown * 10.0) / 10.0)
 if fighter.fist_combo != null and fighter.fist_combo.active: return "连段中 · 收势后施展"
 if not fighter.pending_self_skill.is_empty(): return "运功中 · 完成后施展"
 if not fighter.pending_attack.is_empty(): return "出招中 · 收势后施展"
 if fighter.dash_move > 0.0: return "错步中 · 结束后施展"
 if fighter.qi_blood < RULES.SKILL_BLOOD_COST: return "气血不足 · 至少需%d层" % RULES.SKILL_BLOOD_COST
 if fighter.stamina < RULES.SKILL_COST: return "体力不足"
 return ""

func try_use_skill() -> bool:
 var reason := skill_block_reason()
 if not reason.is_empty():
  last_skill_failure = reason
  fighter.show_feedback(reason)
  fighter.feedback_timer = 1.5
  return false
 last_skill_failure = ""
 var extended: bool = rewrite_active(2) and fighter.qi_blood >= 4
 fighter.stamina -= RULES.SKILL_COST
 lower_blood(RULES.SKILL_BLOOD_COST + (1 if extended else 0))
 buff_remaining = RULES.BUFF_SECONDS
 step_buff = buff_remaining
 buff_fan = RULES.WAVE_REWRITE_FAN if extended else RULES.WAVE_FAN
 fighter.show_feedback(RULES.SKILL)
 return true

func begin_step(seconds: float) -> void:
 step_seconds = seconds
 step_buff = buff_remaining

func release_buff_active(offset: float) -> bool:
 return step_buff > maxf(0.0, offset) + 0.000001

func finish_step(seconds: float) -> void:
 if buff_remaining <= 0.0: return
 var before := buff_remaining
 buff_remaining = maxf(0.0, before - seconds)
 if buff_remaining <= 0.000001:
  buff_remaining = 0.0
  last_skill_failure = ""
  fighter.start_skill_cooldown(RULES.SKILL, maxf(0.0, seconds - before))
  fighter.get_manual_arena().expire_source_waves(fighter.manual_unit_index(), before)

func apply_knockback(direction: Vector2, profile: Dictionary) -> void:
 knockback_remaining = maxf(0.0, float(profile.seconds))
 knockback_velocity = direction.normalized() * float(profile.distance) / knockback_remaining if knockback_remaining > 0.0 else Vector2.ZERO

func advance_displacement(seconds: float) -> void:
 var step := minf(maxf(seconds, 0.0), knockback_remaining)
 if step <= 0.0: return
 fighter.player += knockback_velocity * step
 fighter.clamp_player_to_arena()
 knockback_remaining = maxf(0.0, knockback_remaining - step)
