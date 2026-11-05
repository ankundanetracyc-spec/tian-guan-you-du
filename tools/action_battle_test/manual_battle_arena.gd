extends "human_sparring_arena.gd"

const MANUAL_RULES = preload("action_manual_rules.gd")
const ManualState = preload("action_manual_runtime.gd")
const WaveContact = preload("action_wave_contact.gd")
const BloodWave = preload("blood_wave_vfx.gd")
var manual_battle_setup: Dictionary = {"player":MANUAL_RULES.default_side(), "enemy":MANUAL_RULES.default_side(false), "enemy_count":1}
var active_waves: Array[Dictionary] = []
var wave_release_count := 0
var damage_events: Array[Dictionary] = []
var manual_vfx: Node2D
var _step_positions: Dictionary = {}
var _step_seconds := 0.0

func _ready() -> void:
 if manual_battle_setup.is_empty():
  manual_battle_setup = {"player":MANUAL_RULES.default_side(), "enemy":MANUAL_RULES.default_side(false), "enemy_count":1}
 super._ready()
 _configure_fighter(self, manual_battle_setup.player)
 for npc in npcs: _configure_fighter(npc, manual_battle_setup.enemy)
 var hud_parent := battle_hud.get_parent()
 battle_hud.hide()
 battle_hud.set_process_input(false)
 battle_hud.set_process(false)
 battle_hud.queue_free()
 battle_hud = load(_action_test_path("manual_battle_hud.gd")).new()
 battle_hud.battle = self
 hud_parent.add_child(battle_hud)
 manual_vfx = load(_action_test_path("manual_blood_vfx.gd")).new()
 add_child(manual_vfx)
 restart_battle()

func _configure_fighter(fighter: Node2D, settings: Dictionary) -> void:
 fighter.action_manual = ManualState.new()
 fighter.action_manual.setup(fighter, settings)
 var self_skills: Array[String] = ["催血", "聚炁"]
 fighter.available_self_skills = self_skills
 var allowed := MANUAL_RULES.available_skills(settings)
 var chosen: Array = settings.get("skills", [])
 fighter.skill_slots = ["", "", "", "", ""]
 for index in range(mini(5, chosen.size())):
  var skill := str(chosen[index])
  if skill in allowed and (skill.is_empty() or skill not in fighter.skill_slots): fighter.skill_slots[index] = skill

func configurable_skills() -> Array[String]:
 return MANUAL_RULES.available_skills(manual_battle_setup.player)

func unit_side(fighter: Node2D) -> String:
 return "player" if fighter == self else "npc_%d" % fighter.unit_index

func all_fighters() -> Array:
 var result: Array = [self]
 result.append_array(npcs)
 return result

func opposing_fighters(source: Node2D) -> Array:
 return npcs if source == self else [self]

func _input(event: InputEvent) -> void:
 if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_C:
  manual_battle_setup.player.skills = skill_slots.duplicate()
  get_tree().set_meta("action_manual_last_setup", manual_battle_setup.duplicate(true))
  get_viewport().set_input_as_handled()
  get_tree().change_scene_to_file("res://tscn/scene/ActionManualBattleSetupScene.tscn")
  return
 super._input(event)

func _process(delta: float) -> void:
 var paused := combat_paused()
 _step_seconds = minf(delta, 0.05)
 _step_positions.clear()
 for fighter in all_fighters(): _step_positions[fighter.manual_unit_index()] = fighter.player
 super._process(delta)
 if paused or combat_paused() or hp <= 0 or enemy_hp <= 0: return
 advance_waves(_step_seconds)
 if manual_vfx != null: manual_vfx.advance(_step_seconds)

func manual_think(npc: Node2D) -> bool:
 if not npc.pending_attack.is_empty() or not npc.pending_self_skill.is_empty() or npc.dash_move > 0.0: return false
 if npc.fist_combo != null and npc.fist_combo.active: return false
 if npc.stamina < 1.0: return false
 if npc.qi_blood < mini(4, npc.qi_blood_capacity) and "催血" in npc.skill_slots and npc.skill_cooldown_remaining("催血") == 0.0:
  npc.use_skill("催血")
  npc.action_label = "催血"
  return not npc.pending_self_skill.is_empty()
 if MANUAL_RULES.SKILL in npc.skill_slots and npc.qi_blood >= 2 and npc.action_manual.buff_remaining <= 0.0 and npc.skill_cooldown_remaining(MANUAL_RULES.SKILL) == 0.0:
  npc.use_skill(MANUAL_RULES.SKILL)
  npc.action_label = MANUAL_RULES.SKILL
  return npc.action_manual.buff_remaining > 0.0
 return false

func resolve_fighter_attack(source: Node2D, elapsed_after_end := 0.0) -> void:
 var release: Dictionary = source.prepare_attack_release(elapsed_after_end)
 if release.is_empty(): return
 var manual: RefCounted = source.action_manual
 var height: float = source.attack_plane_height()
 var is_basic := bool(release.get("is_basic_attack", false))
 var blood: int = source.qi_blood
 var blood_active: bool = is_basic and manual != null and manual.heart_active() and blood > 0
 var extension: float = manual.blood_extension(blood) if blood_active else 0.0
 var base_hits: Dictionary = {}
 var impacts: Array[Vector2] = []
 var guarding: Dictionary = {}
 for target in opposing_fighters(source):
  if target.hp <= 0: continue
  var index: int = target.manual_unit_index()
  guarding[index] = {"remaining":target.action_manual.guard_amount if target.guard_window > 0.0 else 0, "used":false}
  var relative: Vector2 = target.player - source.player
  if not CONTACT_SPACE.intersects_height(target, height): continue
  if contact_contains(source.player, source.strike_direction, source.attack_reach, target.player):
   var hit := settle_segment(source, target, int(release.damage), "本体", guarding[index])
   base_hits[index] = bool(hit.get("hit", false))
   if bool(hit.get("hit", false)): impacts.append(target.player)
  if blood_active and target.hp > 0 and WaveContact.blood_contains(relative, source.strike_direction, source.attack_reach, extension):
   var bonus: float = manual.combo_bonus + (MANUAL_RULES.CLOSE_BONUS if manual.chapter >= 2 and bool(base_hits.get(index, false)) else 0.0)
   var blood_base := roundi(int(release.damage) * manual.blood_fraction())
   var amount := roundi(blood_base * (1.0 + bonus))
   settle_segment(source, target, amount, "血气", guarding[index])
  if guarding[index].used:
   target.finish_guard()
   target.start_guard_visual(true, source.player - target.player)
 source.basic_strike_hit = not impacts.is_empty()
 release.effect.hit = source.basic_strike_hit
 release.effect.attack_height = height
 release.effect.impact_positions = impacts
 if not impacts.is_empty(): release.effect.impact = impacts[0]
 source.strike_fx.append(release.effect)
 if blood_active and manual_vfx != null:
  manual_vfx.emit_blood(source.player, source.strike_direction, source.attack_reach, extension, height, release.effect, source)
 if is_basic and bool(release.get("is_combo_last", false)) and int(release.combo_id) != manual.last_wave_combo_id and manual.release_buff_active(float(release.release_offset)):
  manual.last_wave_combo_id = int(release.combo_id)
  release_wave(source, release, base_hits, height)
 refresh_health()
 if hp <= 0 or enemy_hp <= 0: clear_finished_battle()

func settle_segment(source: Node2D, target: Node2D, amount: int, kind: String, guard: Dictionary, impact_height := -1.0) -> Dictionary:
 if target.hp <= 0 or amount <= 0: return {"hit":false}
 if impact_height < 0.0: impact_height = source.attack_plane_height()
 if target.invulnerable > 0.0:
  add_combat_text(target.player, 0, unit_side(target), "evade")
  if impact_feedback != null: impact_feedback.report(source,target,{"evaded":true},str(source.strike_kind),kind,impact_height)
  damage_events.append({"kind":kind, "source":source.manual_unit_index(), "target":target.manual_unit_index(), "damage":0, "hit":false})
  return {"hit":false}
 var result: Dictionary = attribute_runtime.resolve_manual_contact(source.manual_unit_index(), target.manual_unit_index(), amount, int(guard.remaining))
 if result.get("inactive", true): return {"hit":false}
 if impact_feedback != null:
  impact_feedback.report(source,target,result,str(source.strike_kind),kind,impact_height)
 var hit := not bool(result.evaded)
 if not hit: add_combat_text(target.player, 0, unit_side(target), "evade")
 else:
  if target.guard_window > 0.0:
   guard.used = true
   guard.remaining = maxi(0, int(guard.remaining) - int(result.blocked))
   if int(result.blocked) > 0: add_combat_text(target.player, int(result.blocked), unit_side(target), "block")
  if int(result.damage) > 0:
   target.hurt_visual = 0.2
   add_combat_text(target.player, int(result.damage), unit_side(target), "damage", bool(result.critical))
 damage_events.append({"kind":kind, "source":source.manual_unit_index(), "target":target.manual_unit_index(),
  "base":amount, "damage":int(result.damage), "hit":hit, "blocked":int(result.blocked)})
 if damage_events.size() > 256: damage_events.pop_front()
 refresh_health()
 return {"hit":hit, "damage":int(result.damage)}

func release_wave(source: Node2D, release: Dictionary, base_hits: Dictionary, height: float) -> void:
 var visual: Node2D = BloodWave.new()
 visual.auto_advance = false
 visual.fan_degrees = source.action_manual.buff_fan
 visual.max_radius = MANUAL_RULES.WAVE_REACH
 visual.expansion_seconds = MANUAL_RULES.WAVE_SECONDS
 # The arena already stores ground positions in its displayed XY plane.
 # Reusing the art sample's extra Y squash would separate crest and contact.
 visual.y_projection = 1.0
 visual.crest_lift = 14.0
 visual.z_index = int(source.player.y) + 6
 add_child(visual)
 visual.play(to_global(source.player), source.strike_direction, height)
 active_waves.append({"source":source.manual_unit_index(), "origin":source.player,
  "direction":source.strike_direction, "height":height, "fan":source.action_manual.buff_fan,
  "base":int(release.get("final_stage_base_damage", release.damage)), "blood":source.qi_blood, "bonus":source.action_manual.combo_bonus,
  "close_hits":base_hits.duplicate(), "close_bonus":source.action_manual.heart_active() and source.action_manual.chapter >= 2,
  "age":0.0, "delay":float(release.release_offset), "contacted":{}, "visual":visual})
 wave_release_count += 1

func advance_waves(seconds: float) -> void:
 if combat_paused(): return
 for index in range(active_waves.size() - 1, -1, -1):
  var wave: Dictionary = active_waves[index]
  var delay := minf(seconds, float(wave.delay))
  wave.delay = maxf(0.0, float(wave.delay) - seconds)
  var step := maxf(0.0, seconds - delay)
  var previous_age := float(wave.age)
  wave.age = previous_age + step
  wave.visual.advance(step)
  var source: Node2D = self if int(wave.source) == 0 else npcs[int(wave.source) - 1]
  if source.hp <= 0:
   wave.visual.queue_free()
   active_waves.remove_at(index)
   continue
  if previous_age < MANUAL_RULES.WAVE_SECONDS and float(wave.delay) <= 0.0:
   var contact_step := minf(step, MANUAL_RULES.WAVE_SECONDS - previous_age)
   if wave.has("expires_in_step"): contact_step = minf(contact_step, maxf(0.0, float(wave.expires_in_step) - delay))
   for target in opposing_fighters(source):
    var id: int = target.manual_unit_index()
    if target.hp <= 0 or wave.contacted.has(id): continue
    if not CONTACT_SPACE.intersects_height(target, float(wave.height)): continue
    var before: Vector2 = _step_positions.get(id, target.player)
    var start: Vector2 = before.lerp(target.player, delay / seconds) if seconds > 0.0 else target.player
    var end: Vector2 = start.lerp(target.player, contact_step / step) if step > 0.0 else target.player
    var contact := WaveContact.first_contact(start - Vector2(wave.origin), end - Vector2(wave.origin), wave.direction, float(wave.fan), previous_age, contact_step, previous_age <= 0.0)
    if contact < 0.0: continue
    wave.contacted[id] = true
    var bonus := float(wave.bonus) + (MANUAL_RULES.CLOSE_BONUS if wave.close_bonus and bool(wave.close_hits.get(id, false)) else 0.0)
    var wave_base := roundi(int(wave.base) * (MANUAL_RULES.WAVE_BASE + MANUAL_RULES.WAVE_PER_BLOOD * int(wave.blood)))
    var amount := roundi(wave_base * (1.0 + bonus))
    var guard := {"remaining":target.action_manual.guard_amount if target.guard_window > 0.0 else 0, "used":false}
    var result := settle_segment(source, target, amount, "血浪", guard, float(wave.height))
    if guard.used:
     target.finish_guard()
     target.start_guard_visual(true, wave.origin - target.player)
    if result.get("hit", false) and target.hp > 0:
     var profile := MANUAL_RULES.control_profile(attribute_runtime.get_control_profile(int(wave.source)), attribute_runtime.get_control_profile(id))
     var pushed := randf() < float(profile.chance)
     damage_events.append({"kind":"击退", "source":wave.source, "target":id, "chance":profile.chance, "success":pushed})
     if pushed:
      var at: Vector2 = start.lerp(end, contact)
      var direction: Vector2 = (at - Vector2(wave.origin)).normalized()
      if direction.is_zero_approx(): direction = wave.direction
      target.action_manual.apply_knockback(direction, profile)
      target.action_manual.advance_displacement(maxf(0.0, seconds - delay - contact * contact_step))
     else: add_combat_text(target.player, 0, unit_side(target), "resist")
   if hp <= 0 or enemy_hp <= 0:
    clear_finished_battle()
    return
  if not wave.visual.playing or wave.has("expires_in_step"):
   wave.visual.queue_free()
   active_waves.remove_at(index)

func expire_source_waves(unit: int, active_seconds: float) -> void:
 for wave in active_waves:
  if int(wave.source) == unit: wave.expires_in_step = active_seconds

func cancel_source_waves(unit: int) -> void:
 for index in range(active_waves.size() - 1, -1, -1):
  if int(active_waves[index].source) != unit: continue
  active_waves[index].visual.queue_free()
  active_waves.remove_at(index)

func clear_manual_waves() -> void:
 for wave in active_waves:
  if is_instance_valid(wave.visual): wave.visual.queue_free()
 active_waves.clear()
 if manual_vfx != null: manual_vfx.clear_visuals()

func restart_battle() -> void:
 clear_manual_waves()
 wave_release_count = 0
 damage_events.clear()
 super.restart_battle()

func clear_finished_battle() -> void:
 super.clear_finished_battle()
 clear_manual_waves()

func _exit_tree() -> void:
 clear_manual_waves()
 super._exit_tree()
