extends SceneTree

class FixedArena extends "action_battle_feel_test.gd":
 var fixed_aim := Vector2.RIGHT
 func attack_aim_direction() -> Vector2:
  return fixed_aim

var arena: Node2D
var checks := 0
var failures := 0

func _initialize() -> void:
 call_deferred("verify")

func check(value: bool, label: String) -> void:
 checks += 1
 if not value:
  failures += 1
  push_error(label)

func reset_case() -> void:
 arena.configuring = false
 arena.battle_hud.help_open = false
 arena.restart_battle()
 arena.player = Vector2(400, 500)
 arena.enemy = Vector2(450, 500)
 arena.fixed_aim = Vector2.RIGHT
 arena.physique = 8.0
 arena.soul = 8.0
 arena.wolf_state = "combo_fixture"
 arena.ensure_fist_combo()

func verify() -> void:
 arena = FixedArena.new()
 root.add_child(arena)
 arena.set_process(false)
 arena.set_process_input(false)
 arena.battle_hud.set_process(false)
 arena.battle_hud.set_process_input(false)
 reset_case()
 arena.held_basic_attack = true
 arena.use_skill("拳掌")
 check(arena.fist_combo.active and arena.fist_combo.stage == 0 and arena.stamina == 6 and arena.mana == 3, "左键启动第1段，整套0费")
 check(arena.skill_cooldown_remaining("拳掌") == 0, "连招开始不提前计算CD")
 arena.update_skill_timers(0.179)
 check(arena.enemy_hp == 100, "探拳接触前无伤害")
 arena.update_skill_timers(0.001)
 check(arena.enemy_hp == 96 and arena.player == Vector2(400, 500), "探拳0.18秒命中4点且无贴步")
 arena.update_skill_timers(0.27)
 check(arena.fist_combo.stage == 1 and arena.player == Vector2(400, 500), "0.45秒进入横掌，第一段不移动")
 arena.update_skill_timers(0.20)
 check(arena.enemy_hp == 92 and is_equal_approx(arena.player.x, 408), "横掌0.65秒命中4点，贴步共8")
 arena.update_skill_timers(0.30)
 check(arena.fist_combo.stage == 2 and is_equal_approx(arena.player.x, 408), "0.95秒进入第三段")
 arena.update_skill_timers(0.22)
 check(arena.enemy_hp == 86 and is_equal_approx(arena.player.x, 408), "穿拳1.17秒命中6点，无附加移动")
 arena.update_skill_timers(0.33)
 check(arena.fist_combo.stage == 3 and is_equal_approx(arena.player.x, 408), "1.50秒进入末段")
 arena.update_skill_timers(0.28)
 check(arena.enemy_hp == 78 and is_equal_approx(arena.player.x, 420), "末掌1.78秒命中8点，新增贴步12")
 check(arena.skill_cooldown_remaining("拳掌") == 0 and arena.fist_combo.active, "末段收势尚未完成，不提前起算CD")
 arena.update_skill_timers(0.42)
 check(not arena.fist_combo.active and is_equal_approx(arena.skill_cooldown_remaining("拳掌"), 0.784), "完整2.20秒结束后才开始体魄CD")
 check(arena.stamina == 6 and arena.enemy_hp == 78 and arena.visible_statuses.is_empty(), "四段共22基础伤害，无额外付费或状态")
 check(arena.strike_fx.size() == 4 and arena.strike_fx.map(func(fx): return fx.combo_stage) == [1,2,3,4], "四段各自只生成一项命中特效")
 check(arena.strike_fx.map(func(fx): return fx.visual_key) == ["拳掌","拳掌_掌","拳掌","拳掌_掌"], "四段拳掌外观按动作选择")
 for index in range(4):
  var expected_reach: float = [116.4,128.0,139.6,161.2][index]
  check(is_equal_approx(arena.strike_fx[index].reach, expected_reach), "第%d段伤害判定和特效使用同一属性半径" % (index+1))
 arena.update_strike_fx(1.0)
 check(arena.enemy_hp == 78 and arena.strike_fx.is_empty(), "所有残影清理且不重复伤害")
 reset_case()
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.45)
 check(arena.fist_combo.waiting and arena.fist_combo.stage == 0 and arena.enemy_hp == 96, "单次点击不自动追加第二段")
 arena.update_skill_timers(0.29)
 arena.use_skill("拳掌")
 check(arena.fist_combo.stage == 1 and not arena.fist_combo.waiting, "续接窗口内新点击接第二段")
 arena.update_skill_timers(0.50)
 arena.update_skill_timers(0.30)
 check(not arena.fist_combo.active and arena.enemy_hp == 92 and is_equal_approx(arena.skill_cooldown_remaining("拳掌"), 0.784), "停手超时后结束，不补第三第四段")
 reset_case()
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.10)
 for click in range(1000): arena.use_skill("拳掌")
 arena.update_skill_timers(0.35)
 check(arena.fist_combo.stage == 1, "快速连点只缓冲一次下一段")
 arena.update_skill_timers(0.50)
 check(arena.fist_combo.waiting and arena.fist_combo.stage == 1, "缓冲不会跨越多段连续释放")
 arena.update_skill_timers(0.30)
 arena.use_skill("拳掌")
 check(not arena.fist_combo.active, "重复输入不能绕过整套CD开启下一套")
 reset_case()
 arena.held_basic_attack = true
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.40)
 arena.fixed_aim = Vector2.LEFT
 arena.enemy = Vector2(350, 500)
 arena.update_skill_timers(0.05)
 check(arena.fist_combo.direction == Vector2.LEFT, "每段开始重新瞄准")
 arena.fixed_aim = Vector2.DOWN
 arena.enemy = Vector2(400, 550)
 arena.update_skill_timers(0.20)
 check(arena.player.x == 400 and is_equal_approx(arena.player.y, 508) and arena.enemy_hp == 92, "准备中调整鼠标，贴步与释放朝新的鼠标方向")
 check(arena.strike_fx[1].dir == Vector2.DOWN, "实际伤害与拳掌劲读取释放时的鼠标方向")
 reset_case()
 arena.held_basic_attack = true
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.55)
 var half_step: Vector2 = arena.player
 arena.fixed_aim = Vector2.UP
 arena.update_skill_timers(0.10)
 check(half_step.is_equal_approx(Vector2(404, 500)) and arena.player.is_equal_approx(Vector2(404, 496)), "贴步中途转鼠标只改变剩余路程方向，不重置或追加位移")
 reset_case()
 arena.facing = -1
 arena.use_skill("拳掌")
 arena._process(0.01)
 check(arena.facing == 1, "人物出手面向与本段实际攻击方向一致")
 arena.fixed_aim = Vector2.LEFT
 arena._process(0.01)
 check(arena.facing == -1, "准备中鼠标跨身时人物跟随瞄准")
 arena.update_skill_timers(0.16)
 arena.fixed_aim = Vector2.RIGHT
 arena._process(0.01)
 check(arena.facing == -1 and arena.strike_fx[0].dir == Vector2.LEFT, "释放后本段动作与已经打出的拳掌劲不被鼠标转回")
 reset_case()
 arena.enemy = Vector2(517, 500)
 arena.held_basic_attack = true
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.18)
 check(arena.enemy_hp == 100, "探拳实际半径外落空")
 arena.update_skill_timers(0.47)
 check(arena.enemy_hp == 96, "后续横掌按自身范围与贴步后实际位置命中")
 reset_case()
 arena.held_basic_attack = true
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.55)
 var before_cancel: Vector2 = arena.player
 arena.use_skill("错步")
 check(not arena.fist_combo.active and arena.stamina == 5 and is_equal_approx(arena.skill_cooldown_remaining("拳掌"), 0.784), "错步中断并开始完整拳掌CD，仅支付错步费用")
 arena.update_skill_timers(1.0)
 check(arena.enemy_hp == 96 and arena.player == before_cancel, "中断不补后续段或未完成贴步")
 reset_case()
 arena.use_skill("拳掌")
 arena.use_skill("痛击")
 check(arena.stamina == 6 and arena.pending_attack == "拳掌", "准备未释放时其他主动技能不能插入")
 arena.update_skill_timers(0.18)
 arena.use_skill("痛击")
 check(not arena.fist_combo.active and arena.pending_attack == "痛击" and arena.stamina == 5, "本段已释放后可结束连招并使用其他技能")
 reset_case()
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.18)
 arena.stamina = 0
 arena.use_skill("痛击")
 check(arena.fist_combo.active and arena.skill_cooldown_remaining("拳掌") == 0, "费用不足的其他技能不会取消连招")
 arena.stamina = 6
 arena.skill_cooldowns["痛击"] = 1.0
 arena.use_skill("痛击")
 check(arena.fist_combo.active and arena.stamina == 6, "冷却中的其他技能不会取消连招或扣费")
 reset_case()
 arena.use_skill("拳掌")
 arena.use_skill("拳掌")
 arena.held_basic_attack = true
 arena.configuring = true
 arena._process(0.05)
 check(arena.fist_combo.age == 0 and not arena.fist_combo.buffered and not arena.held_basic_attack, "配置暂停连招并清除按住与缓冲输入")
 arena.configuring = false
 arena.update_skill_timers(0.75)
 check(not arena.fist_combo.active and arena.enemy_hp == 96, "关闭配置后不自动续出后续段")
 reset_case()
 arena.use_skill("拳掌")
 arena.use_skill("拳掌")
 arena._notification(Node.NOTIFICATION_WM_WINDOW_FOCUS_OUT)
 check(not arena.fist_combo.buffered and not arena.held_basic_attack, "失焦清除接续输入")
 arena.hp = 0
 arena.update_skill_timers(2.0)
 check(not arena.fist_combo.active and arena.enemy_hp == 100, "死亡后不结算后续攻击")
 reset_case()
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.55)
 arena.restart_battle()
 check(not arena.fist_combo.active and arena.fist_combo.stage == -1 and arena.skill_cooldown_remaining("拳掌") == 0, "重开清除连段、贴步和CD")
 reset_case()
 arena.physique = 1000
 for index in range(4):
  var data: Dictionary = arena.fist_combo.preview(index)
  check(data.reach == [160.0,180.0,200.0,220.0][index], "第%d段属性范围有明确上限" % (index+1))
 reset_case()
 arena.player = Vector2(1229, 500)
 arena.enemy = Vector2(1180, 500)
 arena.held_basic_attack = true
 arena.use_skill("拳掌")
 arena.update_skill_timers(2.20)
 check(arena.player.x == 1230, "场地边界截断贴步，不穿出边界")
 var source_state: Array = arena.battle_hud.skill_description("拳掌")
 check(" ".join(PackedStringArray(source_state)).contains("四段连招"), "技能说明与连招数据同步")
 print("FIST_COMBO_CHECKS ", checks, " failures=", failures)
 arena.queue_free()
 await process_frame
 quit(1 if failures else 0)
