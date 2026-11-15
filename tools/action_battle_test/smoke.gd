extends SceneTree

class FixedArena extends "action_battle_feel_test.gd":
 func attack_aim_direction() -> Vector2:
  return Vector2.LEFT

func _initialize():
 call_deferred("run_checks")

func run_checks():
 var scene = FixedArena.new()
 root.add_child(scene)
 assert(scene.skinned_player_ready and scene.skinned_player != null, "试玩场应接入原版完整骨架蒙皮运行时")
 var rig_summary: Dictionary = scene.skinned_player.get_runtime_summary()
 assert(rig_summary.skeleton_transform_nodes == 73 and rig_summary.source_sprite_bones == 32, "原版完整层级和SpriteBone数量")
 assert(rig_summary.meshes == 8 and rig_summary.weighted_meshes == 7 and rig_summary.polygon_bone_bindings == 26, "自有贴图映射应绑定8个部件和原权重")
 assert(rig_summary.bone_bound_art_ready and rig_summary.bone_bound_art_parts == 8, "新云叶袍分件应完整挂到原骨骼")
 assert(rig_summary.clips.has("Idle") and rig_summary.clips.has("Run_New1") and rig_summary.clips.has("AttackIdle") and rig_summary.clips.has("Step1"), "原版动作曲线应可采样")
 assert(scene.skinned_player.mesh_nodes[0].visible and not scene.skinned_player.bone_bound_art_nodes[0].visible, "默认项目模式应恢复可用蒙皮贴图")
 scene.skinned_player.set_skin_mode("reference")
 assert(scene.skinned_player.active_skin == "reference" and scene.skinned_player.mesh_nodes[0].texture != null, "G对照模式应切回本机原始纹理")
 assert(scene.skinned_player.mesh_nodes[0].visible and not scene.skinned_player.bone_bound_art_nodes[0].visible, "原版对照应显示原网格并隐藏新分件")
 scene.skinned_player.set_character_pose("Idle", 0.817, 1.0)
 assert(is_equal_approx(scene.skinned_player.scale.y, 0.72), "原版对照显示比例保持旧值")
 scene.skinned_player.set_skin_mode("experiment")
 assert(scene.skinned_player.bone_bound_art_nodes[0].visible, "实验模式应显示新绑定分件")
 assert(scene.skinned_player.experiment_front_overlay != null and scene.skinned_player.experiment_front_overlay.visible, "实验衣襟应覆盖腿部")
 assert(scene.skinned_player.get_runtime_summary().polygon_bone_bindings == 28, "实验模式应使用衣摆骨")
 scene.skinned_player.set_character_pose("Idle", 0.817, 1.0)
 assert(is_equal_approx(scene.skinned_player.scale.y, 0.96 * 0.95), "新分件实验显示比例应适度增大到0.96")
 assert(scene.skinned_player.experiment_upper_leg_overlays.size() == 2, "实验应为双腿分别创建前景大腿")
 scene.skinned_player.set_character_pose("Run_New1", 0.385, 1.0)
 assert(scene.skinned_player.experiment_upper_leg_overlays["zuotui"].modulate.a > 0.9, "后伸的大腿应渐显到前摆之前")
 scene.skinned_player.set_skin_mode("project")
 assert(not scene.skinned_player.experiment_upper_leg_overlays["zuotui"].visible and not scene.skinned_player.experiment_upper_leg_overlays["youtui"].visible, "旧外观不应显示实验大腿层")
 assert(not scene.skinned_player.experiment_front_overlay.visible and scene.skinned_player.get_runtime_summary().polygon_bone_bindings == 26, "旧外观应恢复原衣摆权重")
 assert(scene.skinned_player.mesh_nodes[0].visible and not scene.skinned_player.bone_bound_art_nodes[0].visible, "返回项目模式应恢复旧蒙皮贴图")
 scene.skinned_player.set_character_pose("Idle", 0.817, 1.0)
 assert(is_equal_approx(scene.skinned_player.scale.y, 0.72), "旧项目蒙皮显示比例恢复旧值")
 var animated_leg_path = "/10002/Root/Body/Foot/zuotui/zuotui/tui1"
 var animated_leg: Bone2D = scene.skinned_player.node_by_rig_path[animated_leg_path]
 var leg_rest: Transform2D = scene.skinned_player.rest_by_rig_path[animated_leg_path]
 scene.skinned_player.set_character_pose("Run_New1", 0.05, 1.0)
 assert(absf(animated_leg.rotation - leg_rest.get_rotation()) > 0.1, "跑步曲线应驱动真实腿部骨骼")
 assert(is_equal_approx(scene.skinned_player.scale.y, 0.72), "战场尺寸缩放应保持对照尺度")
 assert(is_equal_approx(scene.attack_reach, 70.0), "实验显示缩放不应改动攻击碰撞距离")
 var idle_signature = sample_action_signature(scene.skinned_player, "Idle", 0.817)
 var run_signature = sample_action_signature(scene.skinned_player, "Run_New1", 0.267)
 var attack_signature = sample_action_signature(scene.skinned_player, "AttackIdle", 0.067)
 var step_signature = sample_action_signature(scene.skinned_player, "Step1", 0.083)
 assert(idle_signature.distance_to(run_signature) > 0.1, "待机与跑动的骨骼姿态必须有可见差异")
 assert(run_signature.distance_to(attack_signature) > 0.1, "跑动与攻击的骨骼姿态必须有可见差异")
 assert(attack_signature.distance_to(step_signature) > 0.1, "攻击与错步的骨骼姿态必须有可见差异")
 print("四动作局部骨骼姿态差异：idle=", idle_signature, " run=", run_signature, " attack=", attack_signature, " step=", step_signature)
 scene.player = Vector2(400, 500)
 scene.enemy = Vector2(350, 500)
 scene.enemy_hp = 20
 scene.use_skill("拳掌")
 assert(scene.stamina == 6.0 and scene.enemy_hp == 20, "拳掌0费且延迟释放")
 scene.update_skill_timers(0.181)
 assert(scene.enemy_hp == 16, "四段首式按属性与份额造成4伤害")
 scene.stamina = 6
 scene.enemy_hp = 20
 scene.use_slot(0)
 assert(scene.stamina == 5.0 and scene.enemy_hp == 20, "痛击1体力且延迟释放")
 scene.update_skill_timers(0.201)
 assert(scene.enemy_hp == 3, "痛击按原公式造成17伤害")
 scene.stamina = 6
 scene.use_slot(1)
 assert(is_equal_approx(scene.guard_window, 0.2) and scene.stamina == 5, "架招判定和费用")
 scene.receive_wolf_bite()
 assert(scene.hp == 84 and scene.guard_window == 0, "撕咬22点、化解6点后应受到16点伤害")
 scene.hp = 100
 scene.receive_wolf_bite()
 assert(scene.hp == 78, "未架招时应按撕咬公式受到22点伤害")
 scene.hp = 100
 scene.stamina = 0
 if scene.fist_combo != null: scene.fist_combo.clear()
 scene.skill_cooldowns["拳掌"] = 0.0
 scene.enemy_hp = 100
 scene.use_skill("拳掌")
 scene.update_skill_timers(0.181)
 assert(scene.enemy_hp == 96 and scene.stamina == 0, "零体力仍可拳掌攻击")
 scene.stamina = 4
 scene.stamina_clock = 1.99
 scene._process(0.02)
 assert(scene.stamina == 5, "体力满两秒恢复1点")
 scene.configuring = true
 var first = InputEventKey.new()
 first.keycode = KEY_1
 first.pressed = true
 var fifth = InputEventKey.new()
 fifth.keycode = KEY_5
 fifth.pressed = true
 scene._input(first)
 scene._input(fifth)
 assert(scene.skill_slots[0] == "" and scene.skill_slots[4] == "痛击", "技能槽应可自由换位")
 scene.configuring = false
 scene.enemy = scene.player + Vector2(40, 0)
 scene.wolf_state = "chase"
 scene.wolf_stamina = 6
 scene.update_wolf(0.01)
 assert(scene.wolf_state == "windup" and scene.wolf_stamina == 5, "凶狼接近后应蓄势并消耗体力")
 scene.wolf_clock = 0.01
 scene.update_wolf(0.02)
 assert(scene.wolf_state == "lunge", "蓄势结束进入扑咬")
 # Moving contact is resolved once even if the attacker remains in range.
 scene.player = Vector2(460, 500)
 scene.enemy = Vector2(400, 500)
 scene.hp = 100
 scene.guard_window = 0
 scene.invulnerable = 0
 scene.wolf_state = "lunge"
 scene.wolf_direction = Vector2.RIGHT
 scene.wolf_clock = 0.2
 scene.wolf_contact_resolved = false
 scene.update_wolf(0.05, scene.player)
 assert(scene.hp == 78 and scene.wolf_contact_resolved, "扑咬接触时结算")
 scene.update_wolf(0.05, scene.player)
 assert(scene.hp == 78, "单次扑咬不能重复命中")
 scene.player = Vector2(350, 500)
 scene.enemy = Vector2(400, 500)
 scene.hp = 100
 scene.wolf_clock = 0.2
 scene.wolf_contact_resolved = false
 scene.update_wolf(0.05, scene.player)
 assert(scene.hp == 100, "扑咬方向身后不应命中")
 scene.player = Vector2(460, 500)
 scene.enemy = Vector2(400, 500)
 scene.invulnerable = 0.18
 scene.wolf_clock = 0.2
 scene.wolf_contact_resolved = false
 scene.update_wolf(0.05, scene.player)
 assert(scene.hp == 100 and scene.wolf_contact_resolved, "错步接触时免伤且不重复结算")
 var run_bone = scene.original_bone_delta("Run_New1", 0.05, "Foot/zuotui/zuotui/tui1", -84.506)
 var attack_bone = scene.original_bone_delta("AttackIdle", 0.066, "yifu/yifu/shenti1/youbi/youbi/youbi1", -62.844)
 assert(absf(run_bone) > 0.3 and absf(attack_bone) > 0.3, "原动画关节曲线应真实驱动部件")
 print("动作战斗原版骨架/权重/动画、自有贴图切换、技能、碰撞与凶狼检查通过")
 scene.queue_free()
 quit(0)

func sample_action_signature(runtime: Node2D, clip_name: String, sample_time: float) -> Vector3:
 runtime.set_character_pose(clip_name, sample_time, 1.0)
 var arm_path = "/10002/Root/Body/yifu/yifu/shenti1/youbi/youbi/youbi1"
 var forearm_path = arm_path + "/youbi2"
 var near_leg_path = "/10002/Root/Body/Foot/zuotui/zuotui/tui1"
 var arm: Node2D = runtime.node_by_rig_path[arm_path]
 var forearm: Node2D = runtime.node_by_rig_path[forearm_path]
 var near_leg: Node2D = runtime.node_by_rig_path[near_leg_path]
 return Vector3(arm.rotation, forearm.rotation, near_leg.rotation)
