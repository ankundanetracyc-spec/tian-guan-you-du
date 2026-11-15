extends SceneTree

var scene: Node2D
var failures := 0
var checks := 0

func _initialize() -> void:
 call_deferred("run_checks")

func check(value: bool, label: String) -> void:
 checks += 1
 if not value:
  failures += 1
  push_error(label)

func reset_case() -> void:
 scene.configuring = false
 scene.battle_hud.help_open = false
 scene.available_self_skills.assign(["催血", "聚炁"])
 scene.restart_battle()
 scene.enemy = Vector2(1800, 900)
 scene.qi_blood_capacity = 8
 scene.mana_capacity = 3
 scene.mana = 3

func finish_cast(skill: String) -> void:
 scene.use_skill(skill)
 scene.update_skill_timers(1.0)

func run_checks() -> void:
 var nested := ResourceLoader.exists("res://ActionBattleFeelTest.tscn")
 var path := "res://ActionBattleFeelTest.tscn" if nested else "res://tscn/scene/ActionBattleOriginalWandererScene.tscn"
 scene = load(path).instantiate()
 scene.original_wanderer_enabled = true
 scene.original_wanderer_art_folder = "yuxu_fullbody_v15"
 root.add_child(scene)
 scene.set_process(false)
 scene.set_process_input(false)
 scene.battle_hud.set_process(false)
 scene.battle_hud.set_process_input(false)
 await process_frame
 check(scene.skill_slots == ["痛击", "架招", "催血", "聚炁", ""], "独立样例保留五槽并提供E催血、R聚炁")
 check(scene.battle_hud.icons.has("催血") and scene.battle_hud.icons.has("聚炁"), "复用两招真实图标")
 reset_case()
 var base_speed: float = scene.move_speed
 var start: Vector2 = scene.player
 scene.use_skill("催血")
 check(scene.stamina == 5 and scene.pending_self_skill == "催血", "催血发动即扣1体力并开始运功")
 check(scene.skill_cooldown_remaining("催血") == 0.0 and scene.skill_is_active("催血"), "运功中禁止重入，尚未开始计算冷却")
 check(scene.qi_blood == 0 and scene.visible_statuses.is_empty(), "运功前不提前授予气血且不施加减速状态")
 scene.move_character(Vector2(1, 1), 0.4)
 check(is_equal_approx(scene.player.distance_to(start), base_speed * 0.2), "斜向移动也精确使用50%移速")
 scene.update_skill_timers(0.4)
 check(scene.qi_blood == 0 and is_equal_approx(scene.self_cast_progress(), 0.4), "效果与进度读取同一运功时钟")
 for skill in ["拳掌", "痛击", "架招", "聚炁", "催血"]: scene.use_skill(skill)
 check(scene.stamina == 5 and scene.pending_attack.is_empty() and scene.pending_self_skill == "催血", "运功中不重入或同时准备其他招式")
 scene.update_skill_timers(0.6)
 check(scene.qi_blood == 2 and scene.pending_self_skill.is_empty(), "满1秒才获得气血2且只结算一次")
 check(scene.visible_statuses == ["气血"] and scene.movement_multiplier() == 1.0 and scene.move_speed == base_speed, "完成即恢复移速，只显示真实气血")
 scene.update_skill_timers(0.5)
 check(scene.qi_blood == 2, "气血不按时间回落也不重复增加")
 scene.use_skill("催血")
 check(scene.stamina == 5, "效果完成不提前结束冷却")
 reset_case()
 scene.qi_blood = 7
 scene.hp = 75
 finish_cast("催血")
 check(scene.qi_blood == 8 and scene.hp == 75 and scene.feedback == "气血+1", "上限只增加实际1层，不虚报或自动治疗")
 scene.update_skill_timers(scene.skill_cooldown_duration("催血"))
 finish_cast("催血")
 check(scene.qi_blood == 8 and scene.feedback == "气血已满", "满气血不溢出也不虚报增层")
 reset_case()
 scene.mana = 0
 finish_cast("聚炁")
 check(scene.qi == 2 and scene.mana == 0 and scene.visible_statuses == ["炁"], "未达阈值只产炁，不回复法力")
 scene.update_skill_timers(scene.skill_cooldown_duration("聚炁"))
 finish_cast("聚炁")
 check(scene.qi == 4 and scene.mana == 0, "炁跨次保留，阈值按当前法力容量计算")
 scene.update_skill_timers(scene.skill_cooldown_duration("聚炁"))
 finish_cast("聚炁")
 check(scene.qi == 0 and scene.mana == 3 and scene.visible_statuses.is_empty(), "达到6炁清空并回满3法力，移除零值图标")
 reset_case()
 scene.mana_capacity = 1
 scene.mana = 0
 scene.qi = 1
 finish_cast("聚炁")
 check(scene.qi == 0 and scene.mana == 1, "超过周天阈值不保留余炁")
 reset_case()
 scene.mana_capacity = 0
 scene.mana = 0
 finish_cast("聚炁")
 check(scene.qi == 2 and scene.mana == 0, "零法力容量不触发周天")
 for skill in ["催血", "聚炁"]:
  reset_case()
  scene.use_skill(skill)
  scene.update_skill_timers(0.3)
  scene.use_skill("错步")
  check(scene.pending_self_skill.is_empty() and scene.self_cast_remaining == 0 and scene.stamina == 4, "%s被错步中断，各扣原费用" % skill)
  check(is_equal_approx(scene.skill_cooldown_remaining(skill), scene.skill_cooldown_duration(skill)) and scene.movement_multiplier() == 1.0, "%s从中断时计算完整属性冷却且不残留减速" % skill)
  scene.update_skill_timers(1.0)
  check(scene.qi == 0 and scene.qi_blood == 0, "%s中断后不补发效果" % skill)
 reset_case()
 scene.stamina = 0
 for skill in ["催血", "聚炁"]: scene.use_skill(skill)
 check(scene.pending_self_skill.is_empty() and scene.skill_cooldown_remaining("催血") == 0 and scene.skill_cooldown_remaining("聚炁") == 0, "体力不足不开始运功、减速或冷却")
 reset_case()
 scene.use_skill("催血")
 scene.self_cast_remaining = 0.01
 start = scene.player
 scene.move_character(Vector2.RIGHT, 0.05)
 check(is_equal_approx(scene.player.x - start.x, base_speed * 0.045), "结束帧只对剩余0.01秒减速")
 for pause_kind in ["configuring", "help"]:
  reset_case()
  scene.use_skill("聚炁")
  scene.update_skill_timers(0.4)
  if pause_kind == "configuring": scene.configuring = true
  else: scene.battle_hud.help_open = true
  scene._process(0.05)
  scene.use_skill("错步")
  check(is_equal_approx(scene.self_cast_remaining, 0.6) and scene.skill_cooldown_remaining("聚炁") == 0 and scene.stamina == 5, "%s冻结运功并禁止提前计算冷却和其他施放" % pause_kind)
 for ended_side in ["player", "enemy"]:
  reset_case()
  scene.qi_blood = 2
  scene.qi = 2
  scene.sync_resource_statuses()
  scene.use_skill("催血")
  if ended_side == "player": scene.hp = 0
  else: scene.enemy_hp = 0
  scene._process(0.05)
  check(scene.pending_self_skill.is_empty() and scene.qi_blood == 0 and scene.qi == 0 and scene.visible_statuses.is_empty(), "%s死亡结束战斗，清理运功与局内资源" % ended_side)
 reset_case()
 scene.qi = 4
 scene.qi_blood = 2
 scene.mana = 0
 scene.use_skill("催血")
 scene.restart_battle()
 check(scene.pending_self_skill.is_empty() and scene.qi_blood == 0 and scene.qi == 0 and scene.mana == 3 and scene.stamina == 6, "重开恢复初始资源并清理未完成运功")
 check(scene.skill_cooldowns.values().all(func(value): return value == 0.0), "重开清理六招的全部冷却")
 reset_case()
 scene.use_skill("催血")
 scene.use_skill("错步")
 scene.configuring = true
 check(scene.assign_skill_slot(4, "催血") and scene.skill_slots[4] == "催血" and scene.skill_slots[2] == "", "五槽装配可移动技能且不复制")
 check(scene.skill_cooldown_remaining("催血") == scene.skill_cooldown_duration("催血"), "换位不重置中断后完整属性冷却")
 check(scene.assign_skill_slot(4, ""), "支持卸下技能")
 reset_case()
 scene.available_self_skills.clear()
 scene.configuring = true
 check(not scene.assign_skill_slot(4, "聚炁"), "未学会技能不进入可装配池")
 scene.configuring = false
 scene.use_skill("聚炁")
 check(scene.pending_self_skill.is_empty() and scene.stamina == 6, "未学会不能绕过装配入口施放")
 if "--capture" in OS.get_cmdline_user_args() or "--attribute-capture" in OS.get_cmdline_user_args():
  root.mode = Window.MODE_WINDOWED
  await process_frame
  await process_frame
  reset_case()
  scene.skill_slots = ["痛击", "架招", "催血", "聚炁", ""]
  scene.player = Vector2(1300, 900)
  scene.enemy = Vector2(1550, 930)
  finish_cast("催血")
  scene.use_skill("聚炁")
  scene.update_skill_timers(0.45)
  scene.update_skinned_player()
  var output_root := "res://../../output" if nested else "res://output"
  var capture_tag := "attribute_cooldowns" if "--attribute-capture" in OS.get_cmdline_user_args() else "instant_six_skills"
  for dimensions in [Vector2i(1280, 720), Vector2i(1920, 1080)]:
   root.size = dimensions
   await process_frame
   await process_frame
   scene._update_trial_camera(0.0)
   scene.battle_hud._update_layout()
   scene.queue_redraw()
   scene.battle_hud.queue_redraw()
   await process_frame
   await RenderingServer.frame_post_draw
   var output := output_root.path_join("%s_%d_20261104.png" % [capture_tag, dimensions.x])
   var capture := root.get_texture().get_image()
   check(capture.get_size() == dimensions, "实际渲染尺寸与请求窗口一致")
   check(capture.save_png(ProjectSettings.globalize_path(output)) == OK, "实际窗口保存运功与资源画面")
  scene.configuring = true
  scene.selected_slot = 2
  scene.battle_hud.queue_redraw()
  await process_frame
  await RenderingServer.frame_post_draw
  check(root.get_texture().get_image().save_png(ProjectSettings.globalize_path(output_root.path_join("%s_configuration_20261104.png" % capture_tag))) == OK, "实际窗口保存六招装配面板")
  scene.configuring = false
  scene.battle_hud.pointer = scene.battle_hud.slot_rect(3).get_center()
  scene.battle_hud.queue_redraw()
  await process_frame
  await RenderingServer.frame_post_draw
  check(scene.battle_hud.hovered_skill == "聚炁", "聚炁悬停说明实际进入绘制")
  check(root.get_texture().get_image().save_png(ProjectSettings.globalize_path(output_root.path_join("%s_tooltip_20261104.png" % capture_tag))) == OK, "实际窗口保存聚炁说明")
 print("CULTIVATION_SKILLS_CHECKS ", checks, " failures=", failures)
 scene.queue_free()
 await process_frame
 quit(1 if failures else 0)
