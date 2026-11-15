extends SceneTree
var failures := 0
var checks := 0
func check(value: bool, label: String) -> void:
 checks += 1
 if not value:
  failures += 1
  push_error(label)
func _initialize() -> void:
 call_deferred("verify")
func verify() -> void:
 var arena: Node2D = load("res://tscn/scene/ActionHumanSparringScene.tscn").instantiate()
 root.add_child(arena)
 arena.set_process(false)
 arena.set_process_input(false)
 var npc: Node2D = arena.npcs[0]
 check(arena.npcs.size() == 1 and arena.attribute_runtime.get_unit_count() == 2, "直接入口只生成一名陪练")
 arena.set_simulation_paused(false)
 arena.player = Vector2(1200, 900)
 npc.player = arena.player + Vector2(65, 0)
 npc.advance_fighter(0.01)
 check(npc.pending_attack == "痛击", "距离合适时仍能主动进攻")
 npc.advance_fighter(0.21)
 npc.advance_fighter(0.01)
 check(npc.ai_reposition_time > 0 and npc.action_label in ["拉开", "侧移"], "攻击后调整距离")
 var before: float = npc.player.distance_to(arena.player)
 npc.advance_fighter(0.1)
 check(npc.player.distance_to(arena.player) > before, "近身时主动后撤")
 var remaining: float = npc.ai_reposition_time
 arena.set_simulation_paused(true)
 npc.advance_fighter(0.5)
 check(is_equal_approx(npc.ai_reposition_time, remaining), "暂停冻结战术计时")
 arena.set_simulation_paused(false)
 npc.player = arena.player + Vector2(140, 0)
 npc.advance_fighter(0.05)
 check(npc.action_label == "侧移" and absf(npc.ai_move.y) > 0.5, "保持距离并侧移")
 npc.ai_reposition_time = 0
 npc.player = arena.player + Vector2(500, 0)
 npc.advance_fighter(0.05)
 check(npc.action_label == "接近" and npc.ai_move.x < 0, "远距离可重新接近")
 arena.restart_battle()
 arena.set_simulation_paused(false)
 arena.player = Vector2(1200, 900)
 npc.player = arena.player + Vector2(50, 0)
 arena.use_skill("痛击")
 arena.attack_direction = Vector2.RIGHT
 npc.advance_fighter(0.01)
 check(npc.guard_window > 0 and npc.action_label == "架招", "看见玩家出招时优先防守")
 arena.restart_battle()
 check(npc.ai_reposition_time == 0 and not npc.ai_was_attacking, "重开清除战术状态")
 print("TACTICAL_AI_CHECKS ", checks, " failures=", failures)
 quit(1 if failures else 0)
