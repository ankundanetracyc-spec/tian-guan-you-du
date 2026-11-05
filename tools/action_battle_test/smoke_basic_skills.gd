extends SceneTree

class FixedArena extends "action_battle_feel_test.gd":
 func attack_aim_direction() -> Vector2:
  return Vector2.LEFT

var failures = 0
var count = 0
var scene: Node2D

func _initialize():
 call_deferred("run_checks")

func check(value: bool, label: String):
 count += 1
 if not value:
  failures += 1
  push_error(label)

func cast(name: String):
 scene.use_skill(name)
 # Fixed test aim: desktop pointer position must not change these scenarios.
 if not scene.pending_attack.is_empty(): scene.attack_direction = Vector2.LEFT

func reset_case():
 if scene.fist_combo != null: scene.fist_combo.clear()
 scene.hp = 100
 scene.enemy_hp = 100
 scene.stamina = 6
 scene.pending_attack = ""
 scene.release_timer = 0.0
 scene.dash_move = 0.0
 scene.invulnerable = 0.0
 scene.guard_window = 0.0
 scene.held_basic_attack = false
 scene.player = Vector2(400, 500)
 scene.enemy = Vector2(350, 500)
 for name in scene.skill_cooldowns: scene.skill_cooldowns[name] = 0.0

func run_checks():
 scene = FixedArena.new()
 root.add_child(scene)
 scene.set_process(false)
 reset_case()
 scene.stamina = 0
 cast("拳掌")
 check(scene.stamina == 0 and scene.enemy_hp == 100, "零体力拳掌可发动但不提前命中")
 scene.update_skill_timers(0.179)
 check(scene.enemy_hp == 100, "释放延迟前无伤害")
 scene.update_skill_timers(0.002)
 check(scene.enemy_hp == 96, "探拳延迟后按四段首段公式命中一次")
 check(scene.skill_cooldown_remaining("拳掌") == 0, "连段进行中不提前计算CD")
 scene.update_skill_timers(0.569)
 check(is_equal_approx(scene.skill_cooldown_remaining("拳掌"), 0.784), "单段未续接超时后开始完整属性CD")
 scene.update_skill_timers(0.783)
 cast("拳掌")
 check(scene.pending_attack.is_empty(), "整套结束后的CD尚未走完不可重发")
 scene.update_skill_timers(0.002)
 cast("拳掌")
 check(scene.pending_attack == "拳掌", "释放后经过体魄8的0.784秒冷却才允许再发动")
 reset_case()
 cast("痛击")
 check(scene.stamina == 5 and scene.enemy_hp == 100, "痛击只扣1体力且延迟释放")
 scene.update_skill_timers(0.201)
 check(scene.enemy_hp == 83, "痛击原公式伤害17")
 cast("痛击")
 check(scene.stamina == 5 and scene.pending_attack.is_empty(), "痛击冷却阻止重复扣费")
 cast("拳掌")
 check(scene.pending_attack == "拳掌", "不同技能没有额外公共冷却")
 scene.update_skill_timers(0.181)
 scene.update_skill_timers(4.130)
 cast("痛击")
 check(scene.pending_attack == "痛击" and scene.stamina == 4, "痛击释放后完整计算独立属性冷却")
 reset_case()
 cast("痛击")
 scene.facing = 1
 cast("错步")
 check(scene.pending_attack.is_empty() and scene.stamina == 4, "错步取消痛击但不返费")
 check(scene.dash_direction == Vector2.LEFT, "无移动输入时向面向反方向后撤")
 check(is_equal_approx(scene.invulnerable, 0.15) and is_equal_approx(scene.dash_move, 0.20), "无敌与位移分别计时")
 check(is_equal_approx(scene.skill_cooldown_remaining("痛击"), 5.0 / 1.16), "取消时开始完整痛击属性冷却")
 scene.update_skill_timers(0.3)
 check(scene.enemy_hp == 100, "取消的攻击不结算")
 reset_case()
 cast("拳掌")
 scene.enemy = Vector2(800, 500)
 scene.update_skill_timers(0.181)
 check(scene.enemy_hp == 100, "敌人离开范围则落空")
 reset_case()
 scene.player = Vector2(800, 500)
 scene.enemy = Vector2(350, 500)
 cast("拳掌")
 scene.player = Vector2(400, 500)
 scene.update_skill_timers(0.181)
 check(scene.enemy_hp == 96 and scene.basic_strike_origin == scene.player, "移动施放使用释放时位置")
 reset_case()
 scene.stamina = 0
 cast("痛击")
 check(scene.pending_attack.is_empty() and scene.skill_cooldown_remaining("痛击") == 0, "体力不足不扣费不进冷却")
 reset_case()
 cast("架招")
 cast("架招")
 check(scene.stamina == 5 and is_equal_approx(scene.guard_window, 0.20), "架招空放扣费且不能连按刷新")
 scene.receive_wolf_bite()
 check(scene.hp == 84 and scene.guard_window == 0, "架招按原公式化解6点，剩余16伤害")
 cast("架招")
 check(scene.stamina == 5, "成功不重置架招冷却")
 reset_case()
 cast("拳掌")
 scene.hp = 0
 scene.update_skill_timers(0.1)
 check(scene.enemy_hp == 100, "死亡后不结算准备中的攻击")
 reset_case()
 cast("拳掌")
 scene.configuring = true
 var remaining = scene.skill_cooldown_remaining("拳掌")
 scene._process(0.05)
 check(scene.skill_cooldown_remaining("拳掌") == remaining and scene.enemy_hp == 100, "配置界面暂停冷却与释放")
 scene.configuring = false
 check(scene.skill_slots.size() == 5 and scene.skill_cost("拳掌") == 0 and scene.skill_cost("痛击") == 1, "五槽和显示费用来源一致")
 if "--capture" in OS.get_cmdline_user_args():
  reset_case()
  scene.player = Vector2(600, 500)
  scene.enemy = Vector2(550, 500)
  cast("痛击")
  scene.update_skill_timers(0.201)
  scene.basic_strike_effect = 0.20
  scene.update_skinned_player()
  scene.queue_redraw()
  scene.battle_hud.queue_redraw()
  await process_frame
  await RenderingServer.frame_post_draw
  var output = ProjectSettings.globalize_path("res://../../output/basic_skills_effect_20261103.png")
  check(root.get_texture().get_image().save_png(output) == OK, "窗口特效截图保存")
 print("BASIC_SKILLS_CHECKS ", count, " failures=", failures)
 scene.queue_free()
 await process_frame
 quit(1 if failures else 0)
