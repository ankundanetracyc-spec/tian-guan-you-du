extends RefCounted

const ATTACK_RANGE = preload("attack_range_rules.gd")
const STAGES := [
 {"name":"探拳", "duration":0.45, "scale":0.40, "base_reach":55.0 * ATTACK_RANGE.MULTIPLIER, "reach_scale":0.40 * ATTACK_RANGE.MULTIPLIER, "reach_cap":80.0 * ATTACK_RANGE.MULTIPLIER, "step":0.0, "visual":"拳掌", "height":0.78},
 {"name":"进步横掌", "duration":0.50, "scale":0.40, "base_reach":60.0 * ATTACK_RANGE.MULTIPLIER, "reach_scale":0.50 * ATTACK_RANGE.MULTIPLIER, "reach_cap":90.0 * ATTACK_RANGE.MULTIPLIER, "step":8.0, "visual":"拳掌_掌", "height":0.92},
 {"name":"回身穿拳", "duration":0.55, "scale":0.50, "base_reach":65.0 * ATTACK_RANGE.MULTIPLIER, "reach_scale":0.60 * ATTACK_RANGE.MULTIPLIER, "reach_cap":100.0 * ATTACK_RANGE.MULTIPLIER, "step":0.0, "visual":"拳掌", "height":0.88},
 {"name":"推掌收势", "duration":0.70, "scale":0.70, "base_reach":75.0 * ATTACK_RANGE.MULTIPLIER, "reach_scale":0.70 * ATTACK_RANGE.MULTIPLIER, "reach_cap":110.0 * ATTACK_RANGE.MULTIPLIER, "step":12.0, "visual":"拳掌_掌", "height":1.10},
]
const CONNECT_WINDOW := 0.30
const HIT_PHASE := 0.40
var battle: Node2D
var active := false
var stage := -1
var age := 0.0
var waiting := false
var wait_remaining := 0.0
var buffered := false
var hit_done := false
var direction := Vector2.RIGHT
var reach := 0.0
var damage := 0
var chain_id := 0

func setup(owner_battle: Node2D) -> void:
 battle = owner_battle

func preview(index: int) -> Dictionary:
 var rule: Dictionary = STAGES[index]
 var physique: float = maxf(0.0, battle.physique)
 return {"damage":roundi(roundi(7.0 + physique * 0.45) * rule.scale), "reach":minf(rule.reach_cap, rule.base_reach + physique * rule.reach_scale) * battle.basic_attack_reach_multiplier()}

func is_last_stage() -> bool:
 return active and stage == STAGES.size() - 1

func clear() -> void:
 active = false
 stage = -1
 age = 0.0
 waiting = false
 wait_remaining = 0.0
 buffered = false
 hit_done = false

func clear_input() -> void:
 buffered = false

func request() -> void:
 if active:
  if stage >= STAGES.size() - 1: return
  if waiting: _begin_stage(stage + 1)
  else: buffered = true
  return
 if battle.hp <= 0 or battle.enemy_hp <= 0 or battle.combat_paused(): return
 if battle.skill_cooldown_remaining("拳掌") > 0 or battle.dash_move > 0: return
 if not battle.pending_attack.is_empty() or not battle.pending_self_skill.is_empty(): return
 chain_id += 1
 active = true
 if battle.action_manual != null: battle.action_manual.combo_started(chain_id)
 _begin_stage(0)

func _refresh_aim() -> void:
 direction = battle.attack_aim_direction()
 battle.attack_direction = direction

func _begin_stage(index: int) -> void:
 stage = index
 age = 0.0
 waiting = false
 wait_remaining = 0.0
 buffered = false
 hit_done = false
 _refresh_aim()
 var rule: Dictionary = STAGES[index]
 var values := preview(index)
 reach = values.reach
 damage = values.damage
 battle.pending_attack = "拳掌"
 battle.attack_lock = float(rule.duration) * HIT_PHASE
 battle.release_timer = battle.attack_lock
 battle.visual_attack_time = 0.0

func progress() -> float:
 if not active or stage < 0: return 0.0
 return clampf(age / float(STAGES[stage].duration), 0.0, 1.0)

func finish(start_cooldown := true, elapsed_after_end := 0.0) -> void:
 if not active: return
 if battle.action_manual != null: battle.action_manual.combo_finished()
 if battle.pending_attack == "拳掌":
  battle.pending_attack = ""
  battle.release_timer = 0.0
  battle.attack_lock = 0.0
 clear()
 if start_cooldown: battle.start_skill_cooldown("拳掌", elapsed_after_end)

func advance(delta: float) -> void:
 if not active or battle.combat_paused(): return
 if battle.hp <= 0 or battle.enemy_hp <= 0:
  finish(false)
  return
 var remaining := delta
 while active and remaining > 0.000001:
  if waiting:
   if battle.held_basic_attack or buffered:
    _begin_stage(stage + 1)
    continue
   var wait_step := minf(remaining, wait_remaining)
   wait_remaining -= wait_step
   remaining -= wait_step
   if wait_remaining <= 0.000001: finish(true, remaining)
   continue
  var rule: Dictionary = STAGES[stage]
  if not hit_done: _refresh_aim()
  var duration: float = rule.duration
  var hit_time := duration * HIT_PHASE
  var boundary := hit_time if not hit_done else duration
  var step := minf(remaining, maxf(0.0, boundary - age))
  var before_fraction := clampf(age / hit_time, 0.0, 1.0)
  age += step
  remaining -= step
  var after_fraction := clampf(age / hit_time, 0.0, 1.0)
  var distance := float(rule.step) * (smoothstep(0.0, 1.0, after_fraction) - smoothstep(0.0, 1.0, before_fraction))
  if distance > 0.0:
   battle.player += direction * distance
   battle.clamp_player_to_arena()
  if not hit_done:
   battle.release_timer = maxf(0.0, hit_time - age)
   battle.attack_lock = battle.release_timer
   if age >= hit_time - 0.000001:
    _refresh_aim()
    hit_done = true
    battle.resolve_pending_attack(remaining)
    if battle.hp <= 0 or battle.enemy_hp <= 0:
     finish(false)
     return
  if age >= duration - 0.000001:
   if stage == STAGES.size() - 1:
    finish(true, remaining)
   elif buffered or battle.held_basic_attack:
    _begin_stage(stage + 1)
   else:
    waiting = true
    wait_remaining = CONNECT_WINDOW
