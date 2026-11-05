extends SceneTree

const STEP := 1.0 / 32.0
const SKILLS := ["拳掌", "痛击", "错步", "架招", "催血", "聚炁"]
const FLOORS := [0.35, 2.0, 2.0, 1.5, 4.0, 4.0]
var arena: Node2D
var count := 0
var failures := 0

func _initialize() -> void:
 call_deferred("verify")

func check(value: bool, label: String) -> void:
 count += 1
 if not value:
  failures += 1
  push_error(label)

func reset_case(attribute := 0.0) -> void:
 arena.configuring = false
 arena.battle_hud.help_open = false
 arena.restart_battle()
 arena.physique = attribute
 arena.soul = attribute
 arena.intelligence = attribute
 arena.dodge = attribute
 arena.player = Vector2(400, 500)
 arena.enemy = Vector2(1200, 500)
 arena.wolf_state = "pacing_fixture"
 arena.enemy_hp = 100000
 arena.enemy_max_hp = 100000
 arena.mana = 0
 arena.mana_capacity = 3

func replay(skill: String, seconds: float) -> Dictionary:
 var starts: Array[float] = []
 var gains: Array[float] = []
 var cycles: Array[float] = []
 for tick in range(roundi(seconds / STEP)):
  var active: bool = arena.skill_is_active(skill)
  arena.use_skill(skill)
  if not active and arena.skill_is_active(skill): starts.append(tick * STEP)
  var blood: int = arena.qi_blood
  var mana: int = arena.mana
  arena._process(STEP)
  if arena.qi_blood > blood: gains.append((tick + 1) * STEP)
  if arena.mana > mana: cycles.append((tick + 1) * STEP)
 return {"starts": starts, "gains": gains, "cycles": cycles}

func verify() -> void:
 arena = load("res://ActionBattleFeelTest.tscn").instantiate()
 root.add_child(arena)
 arena.set_process(false)
 arena.set_process_input(false)
 arena.battle_hud.set_process(false)
 arena.battle_hud.set_process_input(false)
 # Fixed reference results from the design, not from the runtime's formula data.
 var matrix := {0.0: [0.8, 5.0, 6.0, 5.0, 12.0, 12.0],
  50.0: [0.7, 2.5, 4.0, 3.5, 9.5, 7.3846153846],
  100.0: [0.6, 2.0, 3.0, 2.0, 7.0, 5.3333333333],
  200.0: [0.4, 2.0, 2.0, 1.5, 4.0, 4.0],
  1000.0: [0.35, 2.0, 2.0, 1.5, 4.0, 4.0]}
 var previous := [INF, INF, INF, INF, INF, INF]
 for attribute in matrix:
  reset_case(attribute)
  for index in range(SKILLS.size()):
   var actual: float = arena.skill_cooldown_duration(SKILLS[index])
   check(is_equal_approx(actual, matrix[attribute][index]), "%s属性%.0f时符合独立公式" % [SKILLS[index], attribute])
   check(actual >= FLOORS[index] and actual <= previous[index], "%s属性增长单调缩短且不突破下限" % SKILLS[index])
   previous[index] = actual
 reset_case(8.0)
 var before: Array[float] = []
 for skill in SKILLS: before.append(arena.skill_cooldown_duration(skill))
 arena.intelligence = 1000
 for index in range(SKILLS.size()):
  check(arena.skill_cooldown_duration(SKILLS[index]) == (4.0 if SKILLS[index] == "聚炁" else before[index]), "灵慧只直接参与聚炁公式：%s" % SKILLS[index])
 arena.physique = 1000
 check(is_equal_approx(arena.skill_cooldown_duration("架招"), 4.76), "架招取体魄神魂较低值，单堆体魄不能绕过神魂约束")
 arena.soul = 1000
 check(arena.skill_cooldown_duration("架招") == 1.5, "架招双属性足够时达到自身下限")
 reset_case()
 var intervals := {}
 for skill in SKILLS:
  reset_case()
  var action_seconds := 1.0 if skill in ["催血", "聚炁"] else (2.20 if skill == "拳掌" else 0.20)
  var expected_interval: float = arena.skill_cooldown_duration(skill) + action_seconds
  var result := replay(skill, 28.0)
  var starts: Array = result.starts
  var shortest := INF
  var longest := 0.0
  for index in range(1, starts.size()):
   var interval: float = starts[index] - starts[index - 1]
   shortest = minf(shortest, interval)
   longest = maxf(longest, interval)
  check(starts.size() >= 2 and shortest >= expected_interval - 0.00001 and longest <= expected_interval + STEP + 0.00001, "%s从动作结束起算，真实频率包含动作时间" % skill)
  check(arena.stamina >= 0 and arena.stamina <= 6, "%s真实恢复和费用不越界" % skill)
  var description := " ".join(PackedStringArray(arena.battle_hud.skill_description(skill)))
  check(description.contains("结束或中断后起算") and description.contains("最低") and description.contains("max("), "%s展示起算点、最低CD及独立公式" % skill)
  intervals[skill] = shortest
 reset_case()
 arena.use_skill("催血")
 check(arena.skill_cooldown_remaining("催血") == 0 and arena.skill_is_active("催血"), "运功开始只扣费，不提前进入冷却")
 for attempt in range(1000): arena.use_skill("催血")
 check(arena.stamina == 5 and arena.qi_blood == 0, "施放状态独立阻止同招重入，不依赖提前开始CD")
 arena.update_skill_timers(0.25)
 arena.use_skill("错步")
 check(arena.stamina == 4 and arena.qi_blood == 0 and arena.skill_cooldown_remaining("催血") == 12.0, "中断时开始完整12秒CD，运功时间不能抵扣")
 arena.update_skill_timers(11.96875)
 arena.use_skill("催血")
 check(arena.pending_self_skill.is_empty(), "中断后未满完整CD时仍不可施放")
 arena.update_skill_timers(0.03125)
 arena.use_skill("催血")
 check(arena.pending_self_skill == "催血", "中断后满完整CD才恢复使用")
 reset_case()
 arena.use_skill("架招")
 check(arena.skill_cooldown_remaining("架招") == 0, "架招窗口期间尚未开始CD")
 arena.receive_wolf_bite()
 check(arena.guard_window == 0 and arena.skill_cooldown_remaining("架招") == 5.0, "架招真实消耗时立即开始完整CD")
 reset_case()
 arena.use_skill("错步")
 arena.update_skill_timers(0.15)
 check(arena.skill_cooldown_remaining("错步") == 0 and arena.dash_move > 0, "无敌结束不等于位移结束，不提前开始错步CD")
 arena.update_skill_timers(0.05)
 check(is_equal_approx(arena.skill_cooldown_remaining("错步"), 6.0) and arena.dash_move == 0, "完整位移结束才开始错步CD")
 reset_case()
 arena.use_skill("催血")
 arena.update_skill_timers(1.0)
 arena.physique = 1000
 check(arena.skill_cooldown_remaining("催血") == 12 and arena.skill_cooldown_total("催血") == 12 and arena.skill_cooldown_duration("催血") == 4, "正在进行的CD保留结束时快照，后续属性只影响下次CD")
 arena.configuring = true
 arena.assign_skill_slot(4, "催血")
 arena.update_skill_timers(5.0)
 check(arena.skill_cooldown_remaining("催血") == 12, "配置暂停且换槽不重置CD")
 arena.configuring = false
 arena.update_skill_timers(12.0)
 arena.use_slot(4)
 arena.update_skill_timers(1.0)
 check(arena.skill_cooldown_remaining("催血") == 4.0, "下一次结束使用新属性计算CD")
 reset_case()
 var slow := replay("催血", 40.0)
 check(slow.starts == [0.0, 13.0, 26.0, 39.0] and slow.gains == [1.0, 14.0, 27.0, 40.0], "零属性基准需40秒达到基础8层气血")
 reset_case(1000.0)
 var fastest_blood := replay("催血", 16.0)
 check(fastest_blood.gains == [1.0, 6.0, 11.0, 16.0] and arena.qi_blood == 8, "即使高属性也要最早16秒堆满8层")
 reset_case(1000.0)
 var fastest_qi := replay("聚炁", 11.0)
 check(fastest_qi.cycles == [11.0] and arena.qi == 0 and arena.mana == 3, "法力容量3时即使高属性也要最早11秒完成首次周天")
 print("ATTRIBUTE_COOLDOWN_REPLAY ", JSON.stringify({"zero_attribute_intervals":intervals,"zero_attribute_blood_fill":40,"minimum_blood_fill":16,"minimum_first_cycle_capacity_3":11}))
 print("ATTRIBUTE_COOLDOWN_CHECKS ", count, " failures=", failures)
 arena.queue_free()
 await process_frame
 quit(1 if failures else 0)
