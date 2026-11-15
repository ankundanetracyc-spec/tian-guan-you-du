extends SceneTree

class FixedArena extends "human_sparring_arena.gd":
 var fixed_aim := Vector2.RIGHT
 var move_input := Vector2.ZERO
 func attack_aim_direction() -> Vector2:
  return fixed_aim
 func movement_direction() -> Vector2:
  return move_input

var arena: Node2D
var runtime: Node
var checks := 0
var failures := 0
var original_snapshots: Array = []

func _initialize() -> void:
 call_deferred("verify")

func check(value: bool, label: String) -> void:
 checks += 1
 if not value:
  failures += 1
  push_error(label)

func reset_case(player_hp := 1000, enemy_hp := 1000, enemy_precision := 1000000, player_dodge := 0) -> void:
 var player: Resource = original_snapshots[0].duplicate(true)
 player.current_health = player_hp
 player.max_health = player_hp
 player.precision = 1000000
 player.dodge = player_dodge
 player.critical = 1
 player.crit_resist = 1000000
 player.physique = 8.0
 player.soul = 8.0
 player.intelligence = 8.0
 player.physical_defense = 0
 player.physical_damage_reduction = 0.0
 var opponents: Array = []
 for index in range(1, 4):
  var npc: Resource = original_snapshots[index].duplicate(true)
  npc.current_health = enemy_hp
  npc.max_health = enemy_hp
  npc.physique = 8.0
  npc.soul = 8.0
  npc.intelligence = 8.0
  npc.precision = enemy_precision
  npc.dodge = 0
  npc.critical = 1
  npc.crit_resist = 1000000
  npc.physical_defense = 0
  npc.physical_damage_reduction = 0.0
  opponents.append(npc)
 runtime.Configure(player, opponents)
 arena.restart_battle()
 arena.player = Vector2(1400, 1000)
 arena.fixed_aim = Vector2.RIGHT
 arena.move_input = Vector2.ZERO
 for index in range(3):
  var npc: Node2D = arena.npcs[index]
  npc.ai_enabled = false
  npc.player = arena.player + Vector2(50, (index - 1) * 10)
  npc.enemy = arena.player
  npc.enemy_hp = arena.hp
 arena.set_simulation_paused(false)
 seed(1703)

func verify() -> void:
 arena = FixedArena.new()
 runtime = load("res://csharp/battle/ActionHumanSparringRuntime.cs").new()
 runtime.DemoOpponentCount = 3
 arena.add_child(runtime)
 root.add_child(arena)
 arena.set_process(false)
 arena.set_process_input(false)
 arena.battle_hud.set_process_input(false)
 check(runtime.get_unit_count() == 4 and arena.npcs.size() == 3, "恰有一名玩家与三名NPC")
 check(arena.simulation_paused and arena.get_camera_units().size() == 4, "准备画面冻结战斗并完整取景四人")
 check(arena.skinned_wolf == null and arena.wolf_idle == null and arena.wolf_attack == null, "模拟不创建或加载狼")
 var ids := {}
 for index in range(4):
  var state: Dictionary = runtime.get_unit_state(index)
  original_snapshots.append(runtime.get_snapshot(index))
  ids[state.uid] = true
  check(state.is_human and state.realm == "凡人", "第%d名角色来自人物快照" % index)
  if index > 0:
   check(state.physique >= 2 and state.physique <= 10 and state.soul >= 2 and state.soul <= 10, "NPC属性沿现有凡人生成范围")
   check(arena.npcs[index - 1].original_wanderer != null and arena.npcs[index - 1].skinned_player_ready, "NPC接入真实人物骨骼")
   check(is_equal_approx(arena.npcs[index - 1].skill_cooldown_duration("痛击"), maxf(2.0, 5.0 / (1.0 + float(state.physique) / 50.0))), "NPC冷却读取自身最终属性")
 check(ids.size() == 4, "四名角色身份独立")
 check(arena.skill_slots == ["痛击", "架招", "催血", "聚炁", ""], "玩家六招仍可使用")
 reset_case()
 arena.npcs[2].player = arena.player + Vector2(-50, 0)
 arena.held_basic_attack = true
 arena.use_skill("拳掌")
 for duration in [0.45, 0.50, 0.55, 0.70]:
  arena.update_skill_timers(duration)
 check(arena.npcs[0].hp == 978 and arena.npcs[1].hp == 978 and arena.npcs[2].hp == 1000, "四段逐人命中前方各22点，身后不命中")
 check(arena.strike_fx.size() == 4 and arena.strike_fx[0].impact_positions.size() == 2, "每段只有一份招式、分别记录两个接触点")
 check(arena.stamina == 6 and is_equal_approx(arena.skill_cooldown_remaining("拳掌"), 0.784), "整套0费且2.2秒结束后只起算一次属性CD")
 check(arena.npcs.all(func(npc): return npc.skill_cooldown_remaining("拳掌") == 0 and npc.stamina == 6), "玩家施放不消耗NPC资源或共享冷却")
 var healths: Array = arena.npcs.map(func(npc): return npc.hp)
 arena.update_strike_fx(1.0)
 check(arena.npcs.map(func(npc): return npc.hp) == healths, "多接触点残影不补伤害")
 reset_case()
 arena.npcs[0].player = arena.player + Vector2(100, 0)
 arena.npcs[1].player = arena.player + Vector2(117, 0)
 arena.npcs[2].player = arena.player + Vector2(-100, 0)
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.18)
 check(arena.npcs[0].hp == 996 and arena.npcs[1].hp == 1000 and arena.npcs[2].hp == 1000, "探拳命中原范围外的新扩展区，排除新半径外及身后")
 reset_case()
 arena.npcs[0].player = arena.player + Vector2(170, 0)
 arena.npcs[1].player = arena.player + Vector2(181, 0)
 arena.npcs[2].player = arena.player + Vector2(-80, 0)
 arena.use_skill("痛击")
 arena.update_skill_timers(0.20)
 check(arena.npcs[0].hp == 983 and arena.npcs[1].hp == 1000 and arena.npcs[2].hp == 1000, "痛击180半圆逐目标判定，命中新扩展区且不打范围外与身后")
 check(arena.stamina == 5 and is_equal_approx(arena.skill_cooldown_remaining("痛击"), 5.0 / 1.16), "多人目标不重复支付痛击费用或缩短CD")
 reset_case()
 var ranged_attacker: Node2D = arena.npcs[0]
 ranged_attacker.player = arena.player + Vector2(170, 0)
 ranged_attacker.use_skill("痛击")
 ranged_attacker.update_skill_timers(0.20)
 check(arena.hp == 983 and ranged_attacker.attack_reach == 180.0 and ranged_attacker.stamina == 5, "NPC同样在扩展后的170距离命中，只支付原费用")
 reset_case()
 arena.use_skill("架招")
 for index in range(2):
  var npc: Node2D = arena.npcs[index]
  npc.use_skill("痛击")
  npc.update_skill_timers(0.20)
 check(arena.hp == 972 and arena.guard_window == 0, "架招只化解第一人6点，第二人仍按17点结算")
 check(is_equal_approx(arena.skill_cooldown_remaining("架招"), 4.76), "多来袭不重复打开架招或重置CD")
 check(arena.npcs[0].stamina == 5 and arena.npcs[1].stamina == 5 and arena.npcs[2].stamina == 6, "NPC各自支付1体力")
 reset_case()
 var attacker: Node2D = arena.npcs[0]
 attacker.use_skill("痛击")
 attacker.update_skill_timers(0.10)
 arena.use_skill("错步")
 arena.update_skill_timers(0.10)
 attacker.update_skill_timers(0.10)
 check(arena.hp == 1000, "错步0.15秒无敌先于NPC属性攻击结算")
 arena.update_skill_timers(0.10)
 var second: Node2D = arena.npcs[1]
 second.use_skill("痛击")
 second.update_skill_timers(0.20)
 check(arena.hp == 983 and arena.invulnerable == 0, "无敌结束后其他NPC可正常命中")
 reset_case(1000, 1000, 1, 1000000)
 arena.use_skill("架招")
 attacker = arena.npcs[0]
 attacker.use_skill("痛击")
 attacker.update_skill_timers(0.20)
 check(arena.hp == 1000 and arena.guard_window > 0 and arena.skill_cooldown_remaining("架招") == 0, "NPC属性闪避不消耗架招窗口")
 reset_case()
 arena.use_skill("拳掌")
 arena.use_skill("拳掌")
 attacker = arena.npcs[0]
 attacker.use_skill("痛击")
 var before: Array = [arena.player, arena.fist_combo.age, attacker.release_timer, attacker.stamina, attacker.skill_cooldowns.duplicate(true)]
 arena.set_simulation_paused(true)
 for frame in range(8): arena._process(0.05)
 check([arena.player, arena.fist_combo.age, attacker.release_timer, attacker.stamina, attacker.skill_cooldowns] == before, "暂停同时冻结玩家与NPC准备、资源、位置与CD")
 check(not arena.held_basic_attack and not arena.fist_combo.buffered, "暂停清除按住及下一段缓冲")
 arena.set_simulation_paused(false)
 arena.configuring = true
 arena._process(0.05)
 check(attacker.release_timer == before[2] and arena.fist_combo.age == before[1], "装配同样冻结全部NPC")
 arena.configuring = false
 arena._notification(Node.NOTIFICATION_WM_WINDOW_FOCUS_OUT)
 check(arena.simulation_paused, "失焦暂停一对三模拟")
 reset_case(100000, 1000)
 for npc in arena.npcs: npc.ai_enabled = true
 var saw_attack := [false, false, false]
 var travelled := [0.0, 0.0, 0.0]
 var initial_health: int = arena.hp
 for frame in range(240):
  var old: Array = arena.npcs.map(func(npc): return npc.player)
  arena._process(0.05)
  for index in range(3):
   var npc: Node2D = arena.npcs[index]
   travelled[index] += old[index].distance_to(npc.player)
   saw_attack[index] = saw_attack[index] or npc.fist_combo.chain_id > 0 or npc.skill_cooldown_remaining("痛击") > 0
 check(saw_attack.all(func(value): return value), "三名NPC均独立实际出招")
 check(arena.hp < initial_health and travelled.all(func(value): return value > 0), "NPC追击与人物攻击能实际造成伤害")
 check(arena.npcs.all(func(npc): return npc.stamina >= 0 and npc.stamina <= 6), "持续AI出招不透支或超量恢复体力")
 reset_case()
 attacker = arena.npcs[0]
 attacker.ai_enabled = true
 arena.use_skill("拳掌")
 attacker.advance_fighter(0.05)
 check(attacker.guard_window > 0 and attacker.stamina == 5 and attacker.action_label == "架招", "NPC识别当前准备并通过真实施放入口架招")
 arena.update_skill_timers(0.18)
 check(attacker.hp == 1000 and attacker.guard_window == 0 and attacker.skill_cooldown_remaining("架招") > 0, "NPC架招实际化解来袭并消耗窗口")
 attacker.advance_fighter(0.05)
 arena.use_skill("拳掌")
 arena.update_skill_timers(0.27)
 attacker.advance_fighter(0.05)
 check(attacker.dash_move > 0 and attacker.invulnerable > 0 and attacker.action_label == "错步", "NPC架招冷却时能用真实错步避让后续段")
 reset_case(1000, 1)
 arena.npcs[1].player = arena.player + Vector2(300, 0)
 arena.npcs[2].player = arena.player + Vector2(-300, 0)
 attacker = arena.npcs[0]
 attacker.use_skill("拳掌")
 arena.use_skill("痛击")
 arena.update_skill_timers(0.20)
 check(attacker.hp == 0 and arena.enemy_hp == 2, "一名NPC倒地不会提前判整场获胜")
 var hp_before: int = arena.hp
 attacker.advance_fighter(1.0)
 check(arena.hp == hp_before and not attacker.fist_combo.active and attacker.pending_attack.is_empty(), "倒地NPC取消未释放攻击且不补伤害")
 check(arena.get_camera_units().size() == 3, "镜头只剔除倒地NPC，保留其余三人")
 arena.skill_cooldowns["痛击"] = 0.0
 arena.npcs[1].player = arena.player + Vector2(40, 0)
 arena.npcs[2].player = arena.player + Vector2(45, 10)
 arena.use_skill("痛击")
 arena.update_skill_timers(0.20)
 check(arena.enemy_hp == 0 and arena.npcs.all(func(npc): return npc.pending_attack.is_empty() and not (npc.fist_combo != null and npc.fist_combo.active)), "击倒三人才获胜，结束清理全部NPC准备")
 arena.restart_battle()
 check(arena.simulation_paused and arena.enemy_hp == 3 and arena.npcs.all(func(npc): return npc.hp == 1 and npc.stamina == 6), "重开恢复同一批初始快照、资源与准备画面")
 check(original_snapshots[0].current_health == 100 and original_snapshots[1].current_health == original_snapshots[1].max_health, "模拟不修改生成源快照")
 reset_case()
 arena.use_skill("催血")
 arena.update_skill_timers(1.0)
 check(arena.qi_blood == 2 and arena.stamina == 5 and arena.visible_statuses == ["气血"], "一对三保留催血效果且运功限制不变成减益")
 var finish_position: Vector2 = arena.player
 arena.set_simulation_paused(false)
 arena.npcs[0].use_skill("拳掌")
 arena.npcs[0].use_skill("催血")
 check(arena.npcs[0].pending_self_skill.is_empty(), "NPC只能使用确认的基础四招")
 check(arena.player == finish_position, "切换施放不额外移动人物")
 reset_case(1, 1000)
 arena.use_skill("聚炁")
 arena.npcs[0].use_skill("痛击")
 arena.npcs[0].update_skill_timers(0.20)
 check(arena.hp == 0 and arena.pending_self_skill.is_empty() and arena.qi == 0 and arena.npcs.all(func(npc): return npc.pending_attack.is_empty()), "玩家失败取消所有未完成施放，不补资源或后续攻击")
 var source_skill: Resource = load("res://resources/activeskills/应敌.tres")
 check(source_skill.base_damage == 7 and source_skill.damage_bonuses.size() > 0, "多人逐段伤害未修改共享基础技能资源")
 reset_case()
 var hand_height: float = arena.attack_plane_height()
 var body_height: float = arena.CONTACT_SPACE.body_height(arena)
 check(hand_height > 0 and body_height > hand_height, "手高与身体高度来自实际人物，不把脚点当整个人")
 var horizontal_pointer: Vector2 = arena.player + Vector2(100, -hand_height)
 check(arena.CONTACT_SPACE.pointer_direction(arena, horizontal_pointer).is_equal_approx(Vector2.RIGHT), "鼠标指向同手高身体时拳掌水平出手，不从脚底朝上扎")
 var original_ground: Vector2 = arena.npcs[0].player
 arena.npcs[0].elevation = 200.0
 arena.npcs[0].update_skinned_player()
 check(arena.npcs[0].player == original_ground and arena.npcs[0].original_wanderer.position == original_ground - Vector2(0, 200), "离地高度改变显示高度，不改脚圈的地面位置")
 arena.use_skill("痛击")
 arena.update_skill_timers(0.20)
 check(arena.npcs[0].hp == 1000 and arena.npcs[1].hp == 983 and arena.npcs[2].hp == 983, "同一地面射程内，水平拳掌不命中高于攻击层的身体")
 reset_case()
 arena.elevation = 200.0
 arena.update_skinned_player()
 arena.npcs[0].elevation = 200.0
 arena.npcs[0].update_skinned_player()
 arena.use_skill("痛击")
 arena.update_skill_timers(0.20)
 var released_height: float = arena.strike_fx[0].attack_height
 check(arena.npcs[0].hp == 983 and arena.npcs[1].hp == 1000 and arena.npcs[2].hp == 1000, "攻击层随出手者离地高度抬升，同高身体可被命中")
 check(released_height > 200.0 and arena.skill_cost("痛击") == 1, "空间高度不是额外人物属性或额外费用")
 arena.elevation = 0.0
 arena.update_skinned_player()
 check(arena.strike_fx[0].attack_height == released_height and arena.attack_plane_height() < released_height, "已释放气劲固定原高度，不随后续升降改变")
 arena.restart_battle()
 check(arena.elevation == 0 and arena.npcs.all(func(npc): return npc.elevation == 0), "重开回到站地状态，不自动启用空中动作")
 print("HUMAN_SPARRING_CHECKS ", checks, " failures=", failures)
 arena.queue_free()
 await process_frame
 quit(1 if failures else 0)
