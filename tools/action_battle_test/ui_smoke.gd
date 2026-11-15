extends SceneTree

var scene: Node2D
var failures := 0
var capture := false

func _initialize() -> void:
 capture = "--ui-capture" in OS.get_cmdline_user_args()
 call_deferred("run_checks")

func check(condition: bool, label: String) -> void:
 if not condition:
  failures += 1
  push_error(label)

func save_frame(tag: String) -> void:
 if not capture: return
 scene.queue_redraw()
 scene.battle_hud.queue_redraw()
 await process_frame
 await RenderingServer.frame_post_draw
 var output_root := "res://../../output" if ResourceLoader.exists("res://ActionBattleFeelTest.tscn") else "res://output"
 var output := ProjectSettings.globalize_path(output_root.path_join("action_battle_ui_%s_20261103.png" % tag))
 var error := root.get_texture().get_image().save_png(output)
 check(error == OK, "实机截图保存")
 print("UI_CAPTURE ", output)

func run_checks() -> void:
 root.size = Vector2i(1280, 720)
 var scene_path := "res://ActionBattleFeelTest.tscn" if ResourceLoader.exists("res://ActionBattleFeelTest.tscn") else "res://tscn/scene/ActionBattleBilanExperimentScene.tscn"
 scene = load(scene_path).instantiate()
 scene.experimental_bilan_parts = true
 root.add_child(scene)
 scene.set_process(false)
 await process_frame
 var hud: Node2D = scene.battle_hud
 check(hud != null and hud.get_parent() is CanvasLayer, "HUD独立画布")
 check(scene.visible_statuses.is_empty() and hud.status_icons.is_empty(), "默认不展示示例状态")
 check(scene.skill_slots.size() == 5, "五主动槽保持")
 check(hud.cooldown_text(1.2) == "1.2", "短冷却显示十分之一秒")
 check(hud.cooldown_text(0.65) == "0.7", "短冷却向上取整显示")
 check(hud.cooldown_text(0.001) == "0.1", "剩余冷却不提前显示零")
 check(hud.cooldown_text(0) == "", "冷却结束隐藏倒计时")
 check(hud.cooldown_text(10.2) == "11", "长冷却显示整数秒")
 check(hud.dock_rect().end.x <= hud.canvas_size.x - 20, "技能栏右侧留白")
 check(hud.slot_rect(4).end.x <= hud.canvas_size.x - 20, "末槽不越界")
 await save_frame("full_1280")
 scene.stamina = 4
 scene.stamina_clock = 1.0
 scene.mana = 2
 check(is_equal_approx(hud.stamina_cell_fill(3), 1), "已有体力实色")
 check(is_zero_approx(hud.stamina_cell_fill(4)), "尚未到账的恢复不提前填充可支付体力")
 scene.stamina = 4.5
 check(is_equal_approx(hud.stamina_cell_fill(4), 0.5), "实际可支付的小数体力显示半格")
 scene.stamina = 4
 check(is_zero_approx(hud.stamina_cell_fill(5)), "未轮到的格子为空")
 check(scene.stamina == 4 and scene.mana == 2, "视觉恢复不发放资源")
 hud.bead_pulses.clear()
 await save_frame("recovering_1280")
 # The real two-second boundary still awards exactly one point.
 scene.stamina_clock = 1.99
 scene._process(0.02)
 scene.set_process(false)
 check(scene.stamina == 5, "体力仍在两秒边界获得一点")
 check(scene.mana == 2, "法力不被UI自动恢复")
 check(hud.stamina_cell_fill(4) == 1, "已恢复的格子完整填满")
 # UI feedback must never change resource availability or skill rules.
 hud._process(0.0)
 scene.stamina = 0
 scene.pending_attack = ""
 scene.dash_move = 0.0
 scene.skill_cooldowns["痛击"] = 0.0
 scene.use_skill("痛击")
 check(hud.shortage_timer > 0, "真实费用不足触发局部提示")
 check(scene.stamina == 0 and scene.mana == 2, "不足提示不生成资源")
 check(scene.skill_cooldown_remaining("痛击") == 0, "不足提示不开始技能冷却")
 hud._process(0.01)
 check(hud.spent_cells.size() > 0, "消耗格子获得短暂描边反馈")
 scene.configuring = true
 var frozen_feedback: float = hud.shortage_timer
 hud._process(0.1)
 check(hud.shortage_timer == frozen_feedback, "配置窗口冻结反馈动画")
 scene.configuring = false
 hud._process(0.5)
 check(hud.shortage_timer == 0, "不足提示自行消退")
 scene.stamina = 1
 hud._process(0.01)
 check(hud.bead_pulses.has(0), "真实恢复触发满格回弹")
 scene.stamina = 0
 hud._process(0.01)
 check(not hud.bead_pulses.has(0), "消耗立即清除该格恢复回弹")
 check(scene.skill_cost("拳掌") == 0, "UI不改变当前零费基础攻击")
 scene.stamina = 5
 hud._process(0.3)
 # Keyboard reconfiguration still uses the existing swap operation.
 scene.configuring = true
 for keycode in [KEY_1, KEY_5]:
  var key := InputEventKey.new()
  key.keycode = keycode
  key.pressed = true
  scene._input(key)
 check(scene.skill_slots[4] == "痛击" and scene.skill_slots[0] == "", "五槽换位保持")
 scene.configuring = false
 scene.skill_slots = ["痛击", "架招", "", "", ""]
 # Same logical canvas at the project's larger actual window size.
 root.size = Vector2i(1920, 1080)
 await process_frame
 await process_frame
 hud._update_layout()
 check(hud.slot_rect(4).end.x <= hud.canvas_size.x - 20, "1080p末槽不越界")
 await save_frame("recovering_1920")
 # World camera movement never changes the HUD's screen transform.
 var camera := Camera2D.new()
 scene.add_child(camera)
 camera.position = Vector2(900, 700)
 camera.make_current()
 await process_frame
 check(hud.get_canvas_transform().origin.is_zero_approx(), "镜头移动不移动HUD")
 if failures == 0: print("ACTION_BATTLE_UI_CHECKS_PASSED")
 scene.queue_free()
 await process_frame
 quit(1 if failures > 0 else 0)
