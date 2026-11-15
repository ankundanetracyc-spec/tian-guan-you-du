extends SceneTree

const RULES = preload("action_manual_rules.gd")
const CONTACT = preload("action_wave_contact.gd")
class FixedArena extends "manual_battle_arena.gd":
 var fixed_aim := Vector2.RIGHT
 var fixed_move := Vector2.ZERO
 var release_override: Dictionary = {}
 func prepare_attack_release(elapsed_after_end := 0.0) -> Dictionary:
  if not release_override.is_empty():
   var result := release_override
   release_override = {}
   return result
  return super.prepare_attack_release(elapsed_after_end)
 func attack_aim_direction() -> Vector2:
  return fixed_aim
 func movement_direction() -> Vector2:
  return fixed_move

var arena: Node2D
var runtime: Node
var initial: Array[Resource] = []
var checks := 0
var failures := 0

func _initialize() -> void:
 call_deferred("verify")

func check(condition: bool, text: String) -> void:
 checks += 1
 if not condition:
  failures += 1
  push_error(text)

func settings(chapter := 4, role := 1, bind := true) -> Dictionary:
 var config := RULES.default_side()
 config.chapter = chapter
 config.role = role
 config.bindings = RULES.REWRITE_TARGETS.duplicate() if bind else ["", "", "", ""]
 if chapter < 2: config.skills[4] = ""
 return config

func reset_case(chapter := 4, role := 1, bind := true) -> void:
 var player: Resource = initial[0].duplicate(true)
 var opponents: Array = []
 var units: Array = [player]
 for index in range(1, 4):
  var enemy: Resource = initial[index].duplicate(true)
  opponents.append(enemy)
  units.append(enemy)
 for unit in units:
  unit.current_health = 1000
  unit.max_health = 1000
  unit.physique = 8.0
  unit.soul = 8.0
  unit.intelligence = 8.0
  unit.precision = 1000000000
  unit.dodge = 0
  unit.critical = 0
  unit.crit_resist = 1000000000
  unit.physical_defense = 0
  unit.physical_damage_reduction = 0.0
  unit.healing_received_bonus = 0.0
  unit.cultivation_realm = "云息一层"
 runtime.Configure(player, opponents)
 arena.manual_battle_setup.player = settings(chapter, role, bind)
 arena._configure_fighter(arena, arena.manual_battle_setup.player)
 for npc in arena.npcs:
  arena._configure_fighter(npc, RULES.default_side(false))
 arena.restart_battle()
 arena.player = Vector2(1400, 1000)
 arena.fixed_aim = Vector2.RIGHT
 arena.fixed_move = Vector2.ZERO
 arena.release_override = {}
 for npc in arena.npcs:
  npc.ai_enabled = false
  npc.player = arena.player + Vector2(500 + npc.unit_index * 50, 0)
 arena.set_simulation_paused(false)
 seed(1703)

func first_hit() -> void:
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.18)

func events(kind: String, target := 1) -> Array:
 return arena.damage_events.filter(func(event: Dictionary): return event.kind == kind and int(event.target) == target)

func verify() -> void:
 arena = FixedArena.new()
 arena.manual_battle_setup.enemy_count = 3
 runtime = load("res://csharp/battle/ActionManualBattleRuntime.cs").new()
 arena.add_child(runtime)
 root.add_child(arena)
 arena.set_process(false)
 arena.set_process_input(false)
 arena.battle_hud.set_process_input(false)
 for index in range(4): initial.append(runtime.get_snapshot(index))
 check(runtime.get_unit_count() == 4 and arena.npcs.size() == 3, "自选场景有独立人物快照和三名NPC")
 check(not "\n".join(RULES.skill_text(2, 8)).contains("%%"), "玩家公式不显示重复百分号")
 check(RULES.skill_text(2, 8)[3].contains("8×（50%＋2×10%）"), "详细模式将当前末段基础值和气血代入公式")
 check(RULES.skill_text(2, 8, 120.0, false)[3].contains("6点") and not RULES.skill_text(2, 8, 120.0, false)[3].contains("×"), "简略模式保留同一估算结果，省略公式")
 reset_case(1)
 arena.qi_blood = 2
 arena.npcs[0].player = arena.player + Vector2(45, 0)
 first_hit()
 check(arena.npcs[0].hp == 994, "第一卷近身本体4与血气2独立双判")
 check(arena.qi_blood == 2 and arena.stamina == 6, "外放不耗气血、不重复支付普攻费用")
 check(events("本体").size() == 1 and events("血气").size() == 1, "一段对每目标只各判一次本体与血气")
 reset_case(1)
 arena.qi_blood = 2
 arena.npcs[0].player = arena.player + Vector2(170, 0)
 arena.npcs[1].player = arena.player + Vector2(-10, 0)
 arena.npcs[2].player = arena.player + Vector2(60, 120)
 first_hit()
 check(arena.npcs[0].hp == 998 and events("本体").is_empty(), "本体范围外仍能被血气延伸命中")
 check(arena.npcs[1].hp == 1000 and arena.npcs[2].hp == 1000, "血气不打身后，前向延伸不扩大横宽")
 reset_case(1)
 arena.npcs[0].player = arena.player + Vector2(45, 0)
 first_hit()
 check(arena.npcs[0].hp == 996 and events("血气").is_empty(), "无气血时保留本体，不凭空外放")
 reset_case(2)
 arena.qi_blood = 2
 arena.npcs[0].player = arena.player + Vector2(45, 0)
 first_hit()
 check(arena.npcs[0].hp == 993 and int(events("血气")[0].base) == 3, "第二卷近身血气加成25%，本体仍为4")
 reset_case(4)
 arena.qi_blood = 2
 arena.npcs[0].player = arena.player + Vector2(45, 0)
 first_hit()
 check(arena.npcs[0].hp == 992 and int(events("血气")[0].base) == 4, "第四卷70%基础比例与近身增伤结算一次")
 reset_case(4)
 arena.ensure_fist_combo()
 var base_reach := minf(160.0, 110.0 + 8.0 * 0.8)
 check(is_equal_approx(arena.fist_combo.preview(0).reach, base_reach * 1.25), "拳掌化法使每段本体距离提高25%")
 check(is_equal_approx(arena.action_manual.blood_extension(2), 69.0), "主修15%只乘额外延伸距离")
 arena.qi_blood = 2
 arena.npcs[0].player = arena.player + Vector2(base_reach * 1.25 + 68.8, 0)
 first_hit()
 check(arena.npcs[0].hp == 997 and events("本体").is_empty(), "血气接在化法后的本体范围外延伸")
 reset_case(4, 2)
 check(is_equal_approx(arena.action_manual.blood_extension(2), 60.0), "辅修不获得主修15%距离")
 arena.ensure_fist_combo()
 check(is_equal_approx(arena.fist_combo.preview(0).reach, base_reach * 1.25), "辅修启用绑定后距离化法生效")
 reset_case(4, 1, false)
 arena.ensure_fist_combo()
 check(is_equal_approx(arena.fist_combo.preview(0).reach, base_reach), "未绑定不自动启用化法")
 reset_case(4, 0)
 arena.qi_blood = 4
 arena.npcs[0].player = arena.player + Vector2(170, 0)
 first_hit()
 check(arena.npcs[0].hp == 1000, "不装备心法时保留已学技能，不自动产生被动")
 check(RULES.SKILL in arena.configurable_skills(), "技能习得不依赖心法当前装备")
 reset_case(1)
 arena.use_skill("催血")
 arena.update_skill_timers(1.0)
 check(arena.qi_blood == 3 and arena.stamina == 5, "第一卷低血起手催血完成获得3层")
 reset_case(1)
 arena.qi_blood = 4
 arena.use_skill("催血")
 arena.qi_blood = 0
 arena.update_skill_timers(1.0)
 check(arena.qi_blood == 2, "催血化法读取起手层数，不读取完成时层数")
 reset_case(1)
 arena.qi_blood = 7
 arena.use_skill("催血")
 arena.update_skill_timers(1.0)
 check(arena.qi_blood == 8, "气血获得受实际公共上限约束")
 reset_case(1)
 arena.use_skill("催血")
 arena.use_skill("错步")
 arena.update_skill_timers(1.0)
 check(arena.qi_blood == 0 and arena.stamina == 4, "中断运功不给血、不退费")
 reset_case(2)
 var defender: Node2D = arena.npcs[0]
 arena._configure_fighter(defender, settings(2))
 defender.qi_blood = 4
 defender.player = arena.player + Vector2(45, 0)
 defender.use_skill("架招")
 check(defender.action_manual.guard_amount == 7, "架招起手高血使基础化解6提高至7")
 arena.qi_blood = 2
 first_hit()
 check(defender.hp == 1000 and events("本体")[0].blocked == 4 and events("血气")[0].blocked == 3, "同次本体与血气共用化解池，完全化解仍属于本体命中")
 check(defender.guard_window == 0, "同次伤害段结算结束移除剩余化解")
 reset_case(4)
 arena.use_skill(RULES.SKILL)
 check(arena.stamina == 6 and arena.action_manual.buff_remaining == 0, "血量不足不得发动赤潮奔涌")
 reset_case(1)
 arena.qi_blood = 8
 arena.use_skill(RULES.SKILL)
 check(arena.action_manual.buff_remaining == 0 and arena.stamina == 6, "第一卷未习得专属招式")
 reset_case(4)
 arena.qi_blood = 4
 arena.use_skill(RULES.SKILL)
 check(arena.qi_blood == 1 and arena.stamina == 5 and arena.action_manual.buff_fan == 180.0, "第三卷化法额外降血1并扩大扇面")
 check(arena.action_manual.next_combo_ready and arena.skill_cooldown_remaining(RULES.SKILL) == 0, "主动降血准备下一套40%，强化期间尚未起算CD")
 arena.use_skill(RULES.SKILL)
 check(arena.qi_blood == 1 and arena.stamina == 5, "强化不可刷新或重复扣费")
 arena.use_skill("拳掌")
 check(not arena.action_manual.next_combo_ready and is_equal_approx(arena.action_manual.combo_bonus, 0.4), "下一套起手消费40%强化")
 arena.use_skill("错步")
 check(arena.action_manual.combo_bonus == 0 and not arena.action_manual.next_combo_ready, "中断不返还已消费的强化")
 reset_case(4)
 arena.qi_blood = 3
 arena.use_skill(RULES.SKILL)
 check(arena.qi_blood == 1 and arena.action_manual.buff_fan == 120.0, "起手低于4层不额外降血或扩大扇面")
 arena.update_skill_timers(6.5)
 check(arena.action_manual.buff_remaining == 0 and is_equal_approx(arena.skill_cooldown_remaining(RULES.SKILL), 23.5), "强化6秒结束后起算24秒CD并正确扣除跨界帧余时")
 reset_case(2)
 arena.qi_blood = 2
 arena.use_skill(RULES.SKILL)
 arena.held_basic_attack = true
 arena.use_skill("拳掌")
 for seconds in [0.45, 0.50, 0.55, 0.28]: arena.update_skill_timers(seconds)
 check(arena.wave_release_count == 1 and arena.active_waves.size() == 1, "本体落空仍在末段释放一道波")
 check(arena.qi_blood == 0 and events("血气").is_empty(), "降至零血后不补被动血气，末段血浪继续")
 if arena.active_waves.is_empty():
  print("WAVE_DIAG stage=", arena.fist_combo.stage, " age=", arena.fist_combo.age, " pending=", arena.pending_attack, " active=", arena.fist_combo.active,
   " buff=", arena.action_manual.buff_remaining, " step_buff=", arena.action_manual.step_buff, " events=", arena.damage_events)
  arena.queue_free()
  await process_frame
  quit(1)
  return
 var wave: Dictionary = arena.active_waves[0]
 check(wave.blood == 0 and wave.base == 8 and wave.fan == 120.0, "末段防御前基础值与出手气血正确快照")
 arena.npcs[0].player = Vector2(wave.origin) + Vector2(400, 0)
 arena.npcs[1].player = Vector2(wave.origin) + Vector2(-40, 0)
 arena.npcs[2].player = Vector2(wave.origin) + Vector2(20, 220)
 arena.advance_waves(float(wave.delay))
 arena.advance_waves(0.20)
 check(events("血浪").is_empty(), "浪头未扫到时不提前结算整个扇面")
 arena.advance_waves(0.58)
 check(events("血浪").size() == 1 and arena.npcs[0].hp == 996, "0.78秒波前扫到后造成正确伤害")
 check(arena.npcs[1].hp == 1000 and arena.npcs[2].hp == 1000, "血浪不打身后及扇面之外")
 arena.advance_waves(0.4)
 check(events("血浪").size() == 1, "余迹不追加判伤")
 check(events("击退").size() == 1, "每道命中血浪只独立判控一次")
 reset_case(4)
 arena.qi_blood = 8
 arena.use_skill("拳掌")
 arena.use_skill(RULES.SKILL)
 check(arena.qi_blood == 8 and arena.stamina == 6 and arena.action_manual.buff_remaining == 0, "连段内不能发动赤潮奔涌或取消连段偷放")
 reset_case(4)
 arena.qi_blood = 2
 arena.use_skill(RULES.SKILL)
 arena.set_simulation_paused(true)
 arena.update_skill_timers(2.0)
 check(is_equal_approx(arena.action_manual.buff_remaining, 6.0), "暂停冻结强化计时")
 arena.restart_battle()
 check(arena.active_waves.is_empty() and arena.qi_blood == 0 and arena.action_manual.buff_remaining == 0 and not arena.action_manual.next_combo_ready, "重开清除派生波、气血、强化与蓄劲")
 check(arena.simulation_paused and arena.npcs.all(func(npc: Node2D): return npc.qi_blood == 0), "同组重开回到准备并清除双方资源")
 verify_additional_boundaries()
 verify_sweeps_and_control()
 verify_skill_input_and_feedback()
 verify_expanded_wave_and_guard()
 arena.queue_free()
 await process_frame
 print("MANUAL_BATTLE_CHECKS ", checks, " failures=", failures)
 quit(1 if failures else 0)

func verify_expanded_wave_and_guard() -> void:
 reset_case(2)
 arena.qi_blood = 2
 arena.use_skill(RULES.SKILL)
 var wave := test_wave()
 var defender: Node2D = arena.npcs[0]
 defender.player = Vector2(wave.origin) + Vector2(400, 0)
 arena.npcs[1].player = Vector2(wave.origin) + Vector2(440.01, 0)
 arena.npcs[2].player = Vector2(wave.origin) + Vector2(-400, 0)
 arena.advance_waves(0.65)
 check(events("血浪").is_empty(), "扩展到440后仍等波前实际到达，不提前结算400距离")
 defender.use_skill("架招")
 arena.advance_waves(0.13)
 check(defender.hp == 1000 and events("血浪").size() == 1 and events("血浪")[0].blocked == 4, "架招在自身受击时化解400距离外来源的波前，不设防御距离")
 check(arena.npcs[1].hp == 1000 and arena.npcs[2].hp == 1000, "440距离之外及背向目标均不被血浪命中")
 check(is_equal_approx(wave.visual.max_radius, 440.0), "血浪画面与实际判定共用440最远距离")
 check(is_equal_approx(wave.visual._radius(0.39), RULES.wave_radius(0.39)), "波前表现与判定在半程读取同一倍增曲线")

func verify_skill_input_and_feedback() -> void:
 reset_case(2, 1, false)
 var key := InputEventKey.new()
 key.pressed = true
 key.keycode = KEY_F
 arena._input(key)
 check(arena.feedback.contains("气血不足") and arena.feedback.contains("2层") and arena.stamina == 6, "实际F键缺血时明确提示所需层数且不扣费")
 check(arena.battle_hud.skill_failure_text().contains("气血不足"), "施放失败原因实际进入界面提示，不能仅存于调试字段")
 key.keycode = KEY_E
 arena._input(key)
 var before: float = arena.stamina
 key.keycode = KEY_F
 arena._input(key)
 check(arena.feedback.contains("运功中") and arena.stamina == before and arena.pending_self_skill == "催血", "催血运功期间按F提示原因且不打断或多扣费")
 arena.update_skill_timers(1.0)
 check(arena.qi_blood == 2 and arena.pending_self_skill.is_empty(), "正常完成催血得到开招所需2层气血")
 arena._input(key)
 check(is_equal_approx(arena.action_manual.buff_remaining, 6.0) and arena.qi_blood == 0 and arena.stamina == before - 1, "实际F键开启6秒强化并支付1体力与2层气血")
 check(arena.wave_release_count == 0 and arena.battle_hud.skill_failure_text().is_empty(), "开招先给予持续效果，尚未凭空产生血浪或额外提示")
 check(arena.battle_hud.active_skill_text(RULES.SKILL).is_empty() and arena.battle_hud.active_skill_text(RULES.SKILL, true).is_empty(), "持续期间不以强化文字或秒数遮挡技能图标")
 check(is_equal_approx(arena.battle_hud.skill_duration_progress(RULES.SKILL), 1.0), "开启时外沿倒计时圈为完整一圈")
 var mouse := InputEventMouseButton.new()
 mouse.button_index = MOUSE_BUTTON_LEFT
 mouse.pressed = true
 arena._input(mouse)
 for seconds in [0.45, 0.50, 0.55, 0.28]: arena.update_skill_timers(seconds)
 check(arena.wave_release_count == 1, "实际左键输入在拳掌末段触发血浪")
 var remaining: float = arena.action_manual.buff_remaining
 check(is_equal_approx(arena.battle_hud.skill_duration_progress(RULES.SKILL), remaining / 6.0), "外圈随实际剩余持续时间缩短")
 before = arena.stamina
 arena._input(key)
 check(arena.feedback.contains("尚未结束") and arena.action_manual.buff_remaining == remaining and arena.stamina == before and arena.battle_hud.skill_failure_text().is_empty(), "持续期间重复按F仍只显示外圈，不刷新持续时间或扣费")
 mouse.pressed = false
 arena._input(mouse)
 arena.update_skill_timers(remaining)
 check(arena.action_manual.buff_remaining == 0 and is_equal_approx(arena.skill_cooldown_remaining(RULES.SKILL), 24.0), "完整6秒强化结束时开始24秒冷却")
 check(arena.battle_hud.skill_duration_progress(RULES.SKILL) == 0, "持续结束后移除外圈并转入原冷却显示")
 arena.update_skill_timers(12.0)
 key.keycode = KEY_E
 arena._input(key)
 arena.update_skill_timers(1.0)
 check(arena.qi_blood == 2, "冷却期间仍可通过正常催血准备下次开启")
 arena.update_skill_timers(10.9)
 key.keycode = KEY_F
 before = arena.stamina
 arena._input(key)
 check(arena.feedback.contains("冷却中") and arena.action_manual.buff_remaining == 0 and arena.qi_blood == 2 and arena.stamina == before, "开启后29.9秒即使资源足够也不能重开，并提示剩余冷却")
 arena.update_skill_timers(0.100001)
 arena._input(key)
 check(is_equal_approx(arena.action_manual.buff_remaining, 6.0) and arena.qi_blood == 0, "开启后满30秒才可再次支付并获得完整强化")
 reset_case(4)
 arena.use_skill("拳掌")
 arena.qi_blood = 2
 arena._input(key)
 check(arena.feedback.contains("连段中") and arena.action_manual.buff_remaining == 0 and arena.qi_blood == 2, "拳掌连段中按F给出收势提示，保留既定连段互斥")
 check(RULES.REWRITE_TARGETS[3] == "拳掌" and arena.battle_hud.skill_description("拳掌").any(func(line: String): return line.contains("化法：拳掌")), "第四卷按拳掌正式名称绑定并展示化法")
 check(arena.cooldown_formula_text(RULES.SKILL).contains("24秒") and RULES.skill_text(2, 8).back().contains("24秒"), "运行时技能说明与冷却公式共用24秒定值")

func test_wave() -> Dictionary:
 arena.ensure_fist_combo()
 arena.strike_direction = Vector2.RIGHT
 arena.release_wave(arena, {"damage":8, "release_offset":0.0}, {}, arena.attack_plane_height())
 return arena.active_waves.back()

func verify_additional_boundaries() -> void:
 reset_case(3)
 var player: Resource = runtime.get_snapshot(0)
 player.current_health = 500
 player.healing_received_bonus = 0.5
 player.combat_tempering_by_gongfa = {"测试修持":{"fall_heal_rate_pp":5.0}}
 var enemies: Array = []
 for index in range(1, 4): enemies.append(runtime.get_snapshot(index))
 runtime.Configure(player, enemies)
 arena.restart_battle()
 arena.set_simulation_paused(false)
 arena.qi_blood = 2
 arena.use_skill(RULES.SKILL)
 check(arena.hp == 800 and arena.qi_blood == 0, "降血回复读取现行修持回复率和受疗加成")
 check(player.current_health == 500, "实际治疗不回写传入人物快照")
 check(arena.action_manual.next_combo_ready, "实际主动降血才准备40%强化")
 arena.action_manual.next_combo_ready = false
 arena.action_manual.lower_blood(2)
 check(not arena.action_manual.next_combo_ready, "零层实际未降低不触发降血联动")
 reset_case(4)
 arena.configuring = true
 check(arena.assign_skill_slot(2, ""), "可以显式卸下催血")
 check(not arena.action_manual.rewrite_active(0), "卸下目标技能后化法暂停")
 check(arena.assign_skill_slot(2, "催血") and arena.action_manual.rewrite_active(0), "重新装配恢复原化法绑定")
 arena.configuring = false
 check(arena.qi_blood == 0 and arena.action_manual.rewrite_active(3), "拳掌距离化法无需血气段也生效")
 reset_case(2)
 arena.qi_blood = 2
 arena.use_skill(RULES.SKILL)
 var wave := test_wave()
 arena.npcs[0].player = Vector2(wave.origin) + Vector2(80, 0)
 arena.npcs[0].invulnerable = 1.0
 arena.advance_waves(0.78)
 check(arena.npcs[0].hp == 1000 and events("血浪").size() == 1 and not events("血浪")[0].hit, "错步无敌先于血浪伤害判定")
 check(events("击退").is_empty(), "无敌没有命中，不额外判控")
 arena.npcs[0].invulnerable = 0.0
 arena.advance_waves(0.2)
 check(events("血浪").size() == 1 and arena.npcs[0].hp == 1000, "无敌结束后余迹不重新命中")
 reset_case(4, 1, false)
 arena.qi_blood = 8
 arena.use_skill(RULES.SKILL)
 arena.action_manual.combo_started(999)
 wave = test_wave()
 var origin: Vector2 = wave.origin
 arena.npcs[0].player = origin + Vector2(150, 0)
 arena.qi_blood = 1
 arena.player += Vector2(500, 0)
 arena.strike_direction = Vector2.LEFT
 arena.advance_waves(0.78)
 check(events("血浪").size() == 1 and events("血浪")[0].base == 13, "血浪先取整基础值9，再应用快照的40%增伤为13")
 check(wave.blood == 6 and wave.origin == origin and wave.direction == Vector2.RIGHT, "气血、位置和方向变化不改写已释放血浪")
 reset_case(2)
 arena.qi_blood = 2
 arena.use_skill(RULES.SKILL)
 wave = test_wave()
 arena.action_manual.buff_remaining = 0.2
 arena.npcs[0].player = Vector2(wave.origin) + Vector2(35, 0)
 arena.npcs[1].player = Vector2(wave.origin) + Vector2(140, 0)
 arena.update_skill_timers(0.25)
 arena.advance_waves(0.25)
 check(arena.npcs[0].hp == 996 and arena.npcs[1].hp == 1000, "跨强化到期帧保留到期前接触，不结算到期后接触")
 check(arena.active_waves.is_empty() and is_equal_approx(arena.skill_cooldown_remaining(RULES.SKILL), 23.95), "到期移除波前，24秒CD扣除正确余时")
 reset_case(2)
 arena.qi_blood = 2
 arena.use_skill(RULES.SKILL)
 arena.attack_reach = 90.0
 arena.strike_direction = Vector2.RIGHT
 arena.release_override = {"name":"其他普攻", "damage":12, "is_basic_attack":true, "is_combo_last":true,
  "combo_id":17, "release_offset":0.0, "effect":{"age":0.0, "kind":"拳掌", "origin":arena.player,
  "dir":Vector2.RIGHT, "reach":90.0, "hit":false, "impact":arena.player, "visual_key":"拳掌"}}
 arena.resolve_fighter_attack(arena)
 check(arena.wave_release_count == 1 and arena.active_waves[0].base == 12, "末段协议接受不同普攻名和基础值，不写死拳掌或第四段")
 arena.release_override = {"name":"其他普攻", "damage":3, "is_basic_attack":true, "is_combo_last":true,
  "combo_id":17, "release_offset":0.0, "effect":{"age":0.0, "kind":"拳掌", "origin":arena.player,
  "dir":Vector2.RIGHT, "reach":90.0, "hit":false, "impact":arena.player, "visual_key":"拳掌"}}
 var next_release: Dictionary = arena.release_override.duplicate(true)
 arena.resolve_fighter_attack(arena)
 check(arena.wave_release_count == 1, "同一末段的后续伤害片段不重复放浪")
 next_release.combo_id = 18
 next_release.final_stage_base_damage = 20
 arena.release_override = next_release
 arena.resolve_fighter_attack(arena)
 check(arena.wave_release_count == 2 and arena.active_waves.back().base == 20, "下一套重新放浪，末段多伤害片段使用整段基础值")
 arena.set_simulation_paused(true)
 var old_age: float = arena.active_waves[0].age
 arena.advance_waves(0.5)
 check(arena.active_waves[0].age == old_age, "暂停也冻结已释放波前和特效")
 arena.set_simulation_paused(false)
 arena.hp = 0
 arena.advance_waves(0.05)
 check(arena.active_waves.is_empty(), "施术者死亡清除未完成血浪")
 reset_case(4)
 var opponent: Resource = runtime.get_snapshot(1)
 opponent.portrait_data.body_refinement_progress = {"bone":675.0}
 var source: Resource = runtime.get_snapshot(0)
 var collection: Array = [opponent, runtime.get_snapshot(2), runtime.get_snapshot(3)]
 runtime.Configure(source, collection)
 check(is_equal_approx(runtime.get_control_profile(1).bearing_resistance_pp, 1.0), "控制实际读取人物骨部位五炼的承载抵抗1个百分点")

func verify_sweeps_and_control() -> void:
 check(CONTACT.first_contact(Vector2(150, 0), Vector2(150, 0), Vector2.RIGHT, 120, 0, 0.78, true) >= 0.0, "固定目标被实际波前扫到")
 check(CONTACT.first_contact(Vector2(-150, 0), Vector2(-150, 0), Vector2.RIGHT, 120, 0, 0.78, true) < 0.0, "反向目标无接触")
 check(CONTACT.first_contact(Vector2(30, 0), Vector2(30, 0), Vector2.RIGHT, 120, 0.50, 0.05, false) < 0.0, "已经经过的余迹不重新命中")
 check(CONTACT.first_contact(Vector2(-500, 0), Vector2(500, 0), Vector2.RIGHT, 120, 0.10, 0.05, false) >= 0.0, "高速目标两端都在范围外仍检测中间交会")
 var at30 := -1.0
 var at120 := -1.0
 for fps in [30, 120]:
  var age := 0.0
  var found := -1.0
  while age < 0.78 and found < 0.0:
   var delta := minf(1.0 / fps, 0.78 - age)
   var crossing := CONTACT.first_contact(Vector2(180, 0), Vector2(180, 0), Vector2.RIGHT, 120, age, delta, age == 0.0)
   if crossing >= 0.0: found = age + delta * crossing
   age += delta
  if fps == 30: at30 = found
  else: at120 = found
 check(at30 > 0.0 and absf(at30 - at120) < 0.00001, "30与120帧使用同一波前曲线得到相同接触时刻")
 var source := {"physique":8.0, "realm_tier":1}
 var target := {"physique":8.0, "realm_tier":1, "bearing_resistance_pp":0.0}
 var profile := RULES.control_profile(source, target)
 check(is_equal_approx(profile.chance, 0.8) and is_equal_approx(profile.distance, 24.0) and is_equal_approx(profile.seconds, 0.12), "同境同体魄击退80%、24单位、0.12秒")
 target.realm_tier = 2
 profile = RULES.control_profile(source, target)
 check(is_equal_approx(profile.chance, 0.16) and is_equal_approx(profile.distance, 4.8) and is_equal_approx(profile.seconds, 0.024), "低一大境界概率与距离、时长同时乘20%")
 target.realm_tier = 3
 profile = RULES.control_profile(source, target)
 check(is_equal_approx(profile.chance, 0.032) and is_equal_approx(profile.distance, 0.96) and is_equal_approx(profile.seconds, 0.0048), "低两大境界继续连乘，不在一境界封顶")
 target.realm_tier = 1
 target.bearing_resistance_pp = 1.0
 check(is_equal_approx(RULES.control_profile(source, target).chance, 0.79), "承载抵抗按百分点扣成功率")
 target.bearing_resistance_pp = 1000.0
 check(RULES.control_profile(source, target).chance == 0, "控制概率可降到零，无1%保底")
 source.physique = 10000.0
 target.bearing_resistance_pp = 0.0
 check(is_equal_approx(RULES.control_profile(source, target).chance, 0.99), "控制成功率最高99%")
 reset_case(4)
 var before: Vector2 = arena.npcs[0].player
 arena.npcs[0].action_manual.apply_knockback(Vector2.RIGHT, {"distance":24.0, "seconds":0.12})
 for seconds in [0.05, 0.05, 0.05]: arena.npcs[0].action_manual.advance_displacement(seconds)
 check(is_equal_approx(arena.npcs[0].player.x - before.x, 24.0), "跨帧击退实际位移恰为24，不多走最后余帧")
 arena.npcs[0].player = before
 arena.npcs[0].action_manual.apply_knockback(Vector2.RIGHT, {"distance":24.0, "seconds":0.12})
 for index in range(2):
  arena.npcs[0].move_character(Vector2.LEFT, 0.06)
  arena.npcs[0].action_manual.advance_displacement(0.06)
 check(is_equal_approx(arena.npcs[0].player.x - before.x, 24.0), "击退期间普通前进不能抵消推移，招式仍能继续")
 arena.npcs[0].action_manual.apply_knockback(Vector2.RIGHT, {"distance":24.0, "seconds":0.12})
 arena.npcs[0].action_manual.advance_displacement(0.06)
 var halfway: Vector2 = arena.npcs[0].player
 arena.npcs[0].action_manual.apply_knockback(Vector2.LEFT, {"distance":24.0, "seconds":0.12})
 arena.npcs[0].action_manual.advance_displacement(0.12)
 check(is_equal_approx(arena.npcs[0].player.x - halfway.x, -24.0), "重复击退覆盖，不叠加速度或排队旧位移")
 arena.npcs[0].player.x = arena.TRIAL_WORLD_SIZE.x - 55
 arena.npcs[0].action_manual.apply_knockback(Vector2.RIGHT, {"distance":24.0, "seconds":0.12})
 arena.npcs[0].action_manual.advance_displacement(0.12)
 check(is_equal_approx(arena.npcs[0].player.x, arena.TRIAL_WORLD_SIZE.x - 50), "场地边界截断击退，不越界回弹")
 arena.npcs[0].player = before
 arena.npcs[0].use_skill("拳掌")
 var npc: Node2D = arena.npcs[0]
 var old_pending: String = npc.pending_attack
 npc.action_manual.apply_knockback(Vector2.RIGHT, {"distance":24.0, "seconds":0.12})
 npc.action_manual.advance_displacement(0.06)
 check(npc.pending_attack == old_pending and npc.fist_combo.active, "击退不自动取消已有出招或连段")
