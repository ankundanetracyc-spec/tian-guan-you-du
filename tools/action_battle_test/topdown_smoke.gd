extends SceneTree

var scene: Node2D
var count := 0
var failures := 0
var captured := false
var artifact_folder := "res://../../output/topdown_trial_20261104"


func _initialize() -> void:
 call_deferred("run_checks")


func check(value: bool, label: String) -> void:
 count += 1
 if not value:
  failures += 1
  push_error(label)


func reset_case() -> void:
 scene.restart_trial()
 scene.set_process(false)
 scene.skill_slots = ["痛击", "架招", "", "", ""]
 scene.player = Vector2(500, 430)
 scene.enemy = Vector2(550, 430)
 scene.aim_direction = Vector2.RIGHT


func cast(skill: String) -> void:
 scene.use_skill(skill)


func send_key(code: Key, pressed := true) -> void:
 var event := InputEventKey.new()
 event.keycode = code
 event.physical_keycode = code
 event.pressed = pressed
 Input.parse_input_event(event)
 Input.flush_buffered_events()


func run_checks() -> void:
 scene = load("res://TopDownBattleTrial.tscn").instantiate()
 root.add_child(scene)
 scene.set_process(false)
 check(scene.paused and scene.ready_to_start, "首次进入等待开始，避免读说明时遭到攻击")
 reset_case()
 check(scene.attribute_runtime == null and scene.skill_slots.size() == 5, "试玩隔离正式角色与装配，保留五个自由槽")
 check(scene.move_speed == 235.0 and scene.wolf_physique == 9.75 and scene.max_hp == 100, "沿用独立样例属性")
 for angle in range(8):
  reset_case()
  scene.aim_direction = Vector2.from_angle(angle * TAU / 8)
  scene.enemy = scene.player + scene.aim_direction * 50
  scene.stamina = 0
  cast("拳掌")
  check(scene.enemy_hp == 100 and scene.pending_attack == "拳掌", "八方向零费拳掌有释放延迟 %d" % angle)
  scene.update_skill_timers(0.181)
  check(scene.enemy_hp == 96 and scene.stamina == 0, "八方向伤害及费用一致 %d" % angle)
  scene.update_strike_fx(0.3)
  check(scene.enemy_hp == 96, "残影不追加伤害 %d" % angle)
 reset_case()
 scene.enemy = scene.player + Vector2(-60, 0)
 cast("拳掌")
 scene.update_skill_timers(0.181)
 check(scene.enemy_hp == 100, "背后目标不命中")
 reset_case()
 scene.enemy = scene.player + Vector2(117, 0)
 cast("拳掌")
 scene.update_skill_timers(0.181)
 check(scene.enemy_hp == 100, "翻倍后的探拳116.4半径外不命中")
 reset_case()
 cast("痛击")
 check(scene.stamina == 5 and scene.release_timer == 0.2, "痛击1体力及0.20秒延迟")
 scene.update_skill_timers(0.201)
 check(scene.enemy_hp == 83, "痛击伤害17")
 cast("痛击")
 check(scene.stamina == 5 and scene.pending_attack.is_empty(), "冷却不能重复扣费")
 cast("拳掌")
 check(scene.pending_attack == "拳掌", "无额外公共冷却")
 reset_case()
 scene.enemy = Vector2(1000, 430)
 cast("拳掌")
 scene.player = Vector2(950, 430)
 scene.update_skill_timers(0.181)
 check(scene.enemy_hp == 96 and scene.basic_strike_origin == scene.player, "移动施放使用实际释放位置")
 reset_case()
 scene.aim_direction = Vector2(0, -1)
 cast("痛击")
 cast("错步")
 check(scene.stamina == 4 and scene.pending_attack.is_empty(), "错步取消攻击不返费")
 check(scene.dash_direction == Vector2.DOWN, "静止错步按鼠标全方向向后撤")
 check(scene.dash_move == 0.2 and scene.invulnerable == 0.15, "错步时长与无敌窗口未变")
 scene.receive_wolf_bite()
 check(scene.hp == 100, "错步无敌期间不受伤")
 scene.invulnerable = 0.0
 scene.dash_move = 0.04
 scene.receive_wolf_bite()
 check(scene.hp == 78, "错步末段没有额外无敌")
 reset_case()
 cast("架招")
 cast("架招")
 check(scene.stamina == 5 and scene.guard_window == 0.2, "架招空放扣费且不能连按刷新")
 scene.receive_wolf_bite()
 check(scene.hp == 84 and scene.guard_window == 0, "架招只化解6点，承受剩余16点")
 scene.receive_wolf_bite()
 check(scene.hp == 62, "已消耗的架招不保护下一次攻击")
 reset_case()
 scene.stamina = 0
 for skill in ["痛击", "架招", "错步"]:
  cast(skill)
  check(scene.skill_cooldown_remaining(skill) == 0.0, "体力不足不进入冷却 " + skill)
 reset_case()
 cast("拳掌")
 scene.hp = 0
 scene.update_skill_timers(0.181)
 check(scene.enemy_hp == 100, "死亡取消准备中的伤害")
 reset_case()
 cast("拳掌")
 scene.paused = true
 var cooldown: float = scene.skill_cooldown_remaining("拳掌")
 scene._process(0.05)
 check(scene.enemy_hp == 100 and scene.skill_cooldown_remaining("拳掌") == cooldown, "暂停冻结释放、资源与冷却")
 scene.paused = false
 scene.configuring = true
 scene._process(0.05)
 check(scene.enemy_hp == 100 and scene.skill_cooldown_remaining("拳掌") == cooldown, "换槽界面冻结战斗")
 scene.configuring = false
 scene.battle_hud.help_open = true
 scene._process(0.05)
 check(scene.enemy_hp == 100 and scene.skill_cooldown_remaining("拳掌") == cooldown, "帮助界面冻结战斗")
 scene._notification(Node.NOTIFICATION_WM_WINDOW_FOCUS_OUT)
 check(scene.paused and scene.focus_paused and not scene.held_basic_attack, "失去焦点自动暂停并清除连发")
 reset_case()
 scene.stamina = 3
 scene.stamina_clock = 1.97
 scene._process(0.04)
 check(scene.stamina == 4 and scene.mana == 3, "体力每2秒恢复1点，法力保持")
 reset_case()
 scene.wolf_direction = Vector2.LEFT
 scene.update_wolf(0.01)
 check(scene.wolf_state == "windup" and scene.wolf_stamina == 5, "近身凶狼支付体力进入蓄势")
 var locked: Vector2 = scene.wolf_direction
 scene.player = Vector2(500, 600)
 scene.update_wolf(0.81)
 check(scene.wolf_state == "lunge" and scene.wolf_direction == locked, "凶狼蓄势后锁定方向扑咬")
 scene.update_wolf(0.21)
 check(scene.wolf_state == "recover", "扑咬后进入恢复")
 scene.update_wolf(1.51)
 check(scene.wolf_state == "chase", "恢复后继续追击")
 reset_case()
 scene.enemy = Vector2(600, 430)
 scene.player = Vector2(650, 430)
 scene.wolf_state = "lunge"
 scene.wolf_direction = Vector2.RIGHT
 scene.wolf_clock = 0.2
 scene.wolf_contact_resolved = false
 scene.update_wolf(0.01)
 scene.update_wolf(0.01)
 check(scene.hp == 78, "凶狼同次扑咬最多命中一次")
 scene.battle_hud.help_open = true
 scene.restart_trial()
 check(scene.hp == 100 and scene.enemy_hp == 100 and scene.stamina == 6 and not scene.battle_hud.help_open and scene.guard_fx.is_empty() and scene.strike_fx.is_empty(), "重来清除生命、资源、特效和弹窗")
 check(scene.battle_hud.stamina_cell_fill(0) == 1.0 and scene.battle_hud.stamina_cell_fill(5) == 1.0, "HUD读取真实体力")
 scene.held_basic_attack = true
 send_key(KEY_F1)
 send_key(KEY_F1, false)
 check(scene.battle_hud.help_open and not scene.held_basic_attack, "真实F1输入打开帮助并清除连发")
 send_key(KEY_ESCAPE)
 send_key(KEY_ESCAPE, false)
 check(not scene.battle_hud.help_open and scene.is_inside_tree(), "Esc先关闭帮助，不退出场景")
 send_key(KEY_TAB)
 send_key(KEY_TAB, false)
 check(scene.configuring, "真实Tab输入打开槽位配置")
 scene.skill_cooldowns["痛击"] = 0.6
 send_key(KEY_1)
 send_key(KEY_1, false)
 send_key(KEY_2)
 send_key(KEY_2, false)
 check(scene.skill_slots[0] == "架招" and scene.skill_slots[1] == "痛击" and scene.skill_cooldown_remaining("痛击") == 0.6, "换槽保留技能身份冷却")
 send_key(KEY_ESCAPE)
 send_key(KEY_ESCAPE, false)
 check(not scene.configuring, "Esc关闭配置")
 send_key(KEY_P)
 send_key(KEY_P, false)
 check(scene.paused, "真实P输入暂停")
 send_key(KEY_P)
 send_key(KEY_P, false)
 send_key(KEY_W)
 var start_position: Vector2 = scene.player
 scene._process(0.04)
 send_key(KEY_W, false)
 check(scene.player.y < start_position.y and is_equal_approx(scene.player.x, start_position.x), "真实W输入沿俯视地面移动")
 send_key(KEY_T)
 send_key(KEY_T, false)
 check(scene.hp == 100 and scene.player == Vector2(480, 430), "真实T输入在战斗中重开")
 if "--capture" in OS.get_cmdline_user_args():
  await capture_views()
 print("TOPDOWN_CHECKS ", count, " failures=", failures)
 scene.saved_window_state.clear()
 scene.queue_free()
 await process_frame
 quit(1 if failures > 0 else 0)


func save_frame(filename: String) -> void:
 scene._update_actors()
 scene.queue_redraw()
 scene.battle_hud.queue_redraw()
 await process_frame
 await RenderingServer.frame_post_draw
 var folder := ProjectSettings.globalize_path(artifact_folder)
 DirAccess.make_dir_recursive_absolute(folder)
 var code := root.get_texture().get_image().save_png(folder.path_join(filename))
 check(code == OK, "实际窗口截图 " + filename)


func capture_views() -> void:
 if "--large" in OS.get_cmdline_user_args():
  artifact_folder += "_1080"
 reset_case()
 scene.player = Vector2(520, 440)
 scene.enemy = Vector2(775, 380)
 scene.aim_direction = (scene.enemy - scene.player).normalized()
 scene.wolf_state = "chase"
 await save_frame("01_overview.png")
 scene.enemy = Vector2(590, 440)
 scene.wolf_state = "windup"
 scene.wolf_clock = 0.32
 scene.wolf_direction = Vector2.LEFT
 scene.aim_direction = Vector2.RIGHT
 cast("痛击")
 scene.visual_attack_time = 0.09
 scene.update_skill_timers(0.201)
 await save_frame("02_heavy.png")
 reset_case()
 scene.player = Vector2(630, 440)
 scene.enemy = Vector2(685, 440)
 scene.aim_direction = Vector2.RIGHT
 cast("架招")
 scene.receive_wolf_bite()
 await save_frame("03_guard.png")
 for angle in range(8):
  reset_case()
  scene.aim_direction = Vector2.from_angle(angle * TAU / 8)
  scene.enemy = scene.player + scene.aim_direction * 125
  await save_frame("direction_%d.png" % angle)
 reset_case()
 scene.battle_hud.help_open = true
 await save_frame("04_help.png")
 scene.battle_hud.help_open = false
 scene.paused = true
 await save_frame("05_pause.png")
