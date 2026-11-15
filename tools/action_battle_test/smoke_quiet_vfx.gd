extends SceneTree

class FixedArena extends "action_battle_feel_test.gd":
	var fixed_aim := Vector2.RIGHT
	func attack_aim_direction() -> Vector2:
		return fixed_aim

var arena: Node2D
var count := 0
var failures := 0

func _initialize() -> void:
	call_deferred("verify")

func check(condition: bool, label: String) -> void:
	count += 1
	if not condition:
		failures += 1
		push_error(label)

func state() -> Array:
	return [arena.hp, arena.enemy_hp, arena.stamina, arena.mana, arena.qi, arena.qi_blood,
		arena.player, arena.enemy, arena.skill_cooldowns.duplicate(true), arena.pending_attack,
		arena.release_timer, arena.pending_self_skill, arena.self_cast_remaining, arena.invulnerable]

func reset() -> void:
	arena.restart_battle()
	arena.player = Vector2(1500, 950)
	arena.enemy = arena.player + Vector2(55, 0)
	arena.enemy_hp = 1000
	arena.enemy_max_hp = 1000
	arena.wolf_state = "recover"
	arena.wolf_clock = 100
	arena.mana = 1
	arena.facing = 1
	arena.fixed_aim = Vector2.RIGHT

func verify() -> void:
	arena = FixedArena.new()
	arena.original_wanderer_enabled = true
	arena.original_wanderer_art_folder = "yuxu_fullbody_v15"
	root.add_child(arena)
	arena.set_process(false)
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	var vfx: Node2D = arena.skill_vfx
	check(vfx != null and vfx.force_shader != null, "特效与材质成功加载")
	for kind in ["拳掌", "痛击"]:
		for direction in [Vector2.RIGHT, Vector2.LEFT, Vector2.UP, Vector2.DOWN, Vector2(1, -1).normalized()]:
			reset()
			arena.enemy = arena.player + direction * 55.0
			arena.fixed_aim = direction
			arena.use_skill(kind)
			arena.attack_direction = direction
			arena.update_skill_timers(0.181 if kind == "拳掌" else 0.201)
			check(arena.enemy_hp == (996 if kind == "拳掌" else 983), kind + "当前属性伤害且只结算一次")
			check(arena.strike_fx[0].hit, "真实接触才有爆点")
			var before := state()
			vfx.sync_visuals()
			check(state() == before, "绘制不修改任何战斗属性")
			check(vfx.stamps[0].global_position.distance_to(arena.player) < 280, "拳掌劲锚点随攻击范围翻倍，保持在对应覆盖区")
			check(vfx.stamps[0].material.get_shader_parameter("opacity") <= 1, "无过曝加色")
			arena.update_strike_fx(0.41)
			vfx.sync_visuals()
			check(arena.enemy_hp == before[1], "残影不追加伤害")
			arena.update_strike_fx(1)
			vfx.sync_visuals()
			check(arena.strike_fx.is_empty() and not vfx.stamps[0].visible, "残影到期隐藏")
	reset()
	arena.enemy = arena.player + Vector2(250, 0)
	arena.use_skill("拳掌")
	arena.attack_direction = Vector2.RIGHT
	arena.update_skill_timers(0.181)
	check(not arena.strike_fx[0].hit and arena.enemy_hp == 1000, "落空无爆点和伤害")
	for kind in ["催血", "聚炁"]:
		reset()
		arena.use_skill(kind)
		arena.update_skill_timers(0.6)
		check(vfx.completions.is_empty() and arena.qi == 0 and arena.qi_blood == 0, "运功未完成不播放获得效果")
		var before := state()
		vfx.sync_visuals()
		check(state() == before, "运功特效只读取状态")
		arena.configuring = true
		arena._process(0.05)
		check(state() == before, "配置暂停运功与画面")
		arena.configuring = false
		arena.update_skill_timers(0.401)
		check(vfx.completions.size() == 1 and vfx.completions[0].kind == kind, "完成后仅产生一次正确余韵")
		check((arena.qi_blood == 2 if kind == "催血" else arena.qi == 2) and arena.stamina == 5, "原资源收益与扣费保持")
		arena.update_skill_timers(0.01)
		check(vfx.completions.size() == 1, "不会重复获得资源或播放完成")
		arena.battle_hud.help_open = true
		arena._process(0.05)
		check(float(vfx.completions[0].age) == 0.0, "帮助暂停完成余韵")
		arena.battle_hud.help_open = false
		var completed := state()
		arena.update_strike_fx(0.49)
		if kind == "催血":
			check(vfx.completions.size() == 1 and state() == completed, "催血0.49秒仍保留上升收尾且不追加资源")
			arena.update_strike_fx(0.16)
		check(vfx.completions.is_empty() and state() == completed, "收尾按各自寿命结束且不修改战斗")
		reset()
		arena.use_skill(kind)
		arena.update_skill_timers(0.5)
		arena.use_skill("错步")
		arena.update_skill_timers(1.0)
		check(vfx.completions.is_empty() and arena.pending_self_skill.is_empty() and arena.qi == 0 and arena.qi_blood == 0, "错步中断没有完成特效与收益")
		check(arena.stamina == 4, "中断不返运功和错步费用")
	reset()
	arena.qi_blood = arena.qi_blood_capacity
	arena.use_skill("催血")
	arena.update_skill_timers(1.01)
	check(vfx.completions.is_empty(), "气血已满不伪造获得特效")
	reset()
	arena.qi = 4
	arena.use_skill("聚炁")
	arena.update_skill_timers(1.01)
	check(arena.qi == 0 and arena.mana == 3 and vfx.completions[0].cycle, "真实周天才有一笔额外收束")
	arena.restart_battle()
	check(vfx.completions.is_empty() and arena.strike_fx.is_empty(), "重开清理所有实例")
	reset()
	arena.use_skill("催血")
	arena.hp = 0
	arena.update_skill_timers(1.01)
	check(vfx.completions.is_empty() and arena.pending_self_skill.is_empty(), "死亡没有完成余韵")
	reset()
	arena.use_skill("拳掌")
	arena.attack_direction = Vector2.RIGHT
	arena.update_skill_timers(0.181)
	vfx.sync_visuals()
	print("QUIET_VFX_GEOMETRY player=", arena.player, " enemy=", arena.enemy, " sprite=", vfx.stamps[0].global_position, " rect=", vfx.stamps[0].get_rect(), " camera=", arena.trial_camera.zoom)
	print("QUIET_VFX_CHECKS ", count, " failures=", failures)
	arena.queue_free()
	await process_frame
	quit(1 if failures else 0)
