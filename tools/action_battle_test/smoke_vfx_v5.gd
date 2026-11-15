extends SceneTree

var arena: Node2D
var count := 0
var failures := 0

func _initialize() -> void:
 call_deferred("run_checks")

func check(value: bool, label: String) -> void:
 count += 1
 if not value:
  failures += 1
  push_error(label)

func release(kind: String, direction := Vector2.RIGHT) -> void:
 arena.player = Vector2(400, 450)
 arena.enemy = arena.player + direction * 35
 arena.enemy_hp = 1000
 arena.hp = 100
 arena.pending_attack = kind
 arena.attack_direction = direction
 arena.resolve_pending_attack()

func reset_guard() -> void:
 arena.guard_fx.clear()
 arena.pending_attack = ""
 arena.guard_window = 0.0
 arena.dash_move = 0.0
 arena.invulnerable = 0.0
 arena.stamina = 6
 arena.hp = 100
 arena.enemy_hp = 100
 arena.player = Vector2(400, 450)
 arena.enemy = Vector2(450, 450)
 arena.skill_cooldowns["架招"] = 0.0

func run_checks() -> void:
 var local_scene := "res://ActionBattleFeelTest.tscn"
 var scene_path := local_scene if ResourceLoader.exists(local_scene) else "res://tscn/scene/ActionBattleTrialScene.tscn"
 arena = load(scene_path).instantiate()
 root.add_child(arena)
 arena.set_process(false)
 var paths := {
  "拳掌": "art/palm_qi_v4/fist_atlas.png",
  "拳掌_掌": "art/palm_qi_v4/heavy_palm_atlas.png",
  "痛击": "art/palm_qi_v5/heavy_atlas.png",
  "架招": "art/palm_qi_v5/guard_atlas.png",
 }
 check(arena.strike_fx_textures.size() == 4, "四项视觉图集都已加载")
 for progress in [0.0, 0.2, 0.4, 0.6, 0.8, 0.999]:
  check(arena.guard_fx_frame_index(false, progress) < 4, "空放架招不播放命中爆点")
  check(arena.guard_fx_frame_index(true, progress) >= 4, "成功反馈使用后半段命中序列")
 for key in paths:
  check(arena.STRIKE_FX_ATLASES[key] == paths[key], key + "读取正确资源")
  var texture: Texture2D = arena.strike_fx_textures[key]
  check(not texture.get_image().is_invisible(), key + "不是空透明资源")
  check(texture.get_image().get_format() == Image.FORMAT_RGBA8, key + "保留RGBA")
  var dimensions := Vector2(texture.get_size())
  for frame in range(8):
   var source: Rect2 = arena.strike_fx_frame_rect(texture, frame)
   check(source.position.x >= 1 and source.position.y >= 1 and source.end.x <= dimensions.x - 1 and source.end.y <= dimensions.y - 1, key + "帧%d在图集内缩边界内" % frame)
  var source: Rect2 = arena.strike_fx_frame_rect(texture, 0)
  var destination: Rect2 = arena.strike_fx_destination_rect(texture, 90, 0.5)
  check(is_equal_approx(source.size.x / source.size.y, destination.size.x / destination.size.y), key + "不拉伸宽高比例")
 arena.strike_visual_rng.seed = 17413
 var variants := {}
 for index in range(64):
  arena.strike_fx.clear()
  release("拳掌")
  var effect: Dictionary = arena.strike_fx[0]
  variants[effect.visual_key] = true
  check(effect.kind == "拳掌" and effect.visual_key in ["拳掌", "拳掌_掌"], "第%d次只随机外观，身份仍是拳掌" % index)
  check(arena.enemy_hp == 989 and effect.reach == 70, "第%d次伤害与范围相同" % index)
  var chosen := str(effect.visual_key)
  arena.update_strike_fx(0.08)
  check(arena.strike_fx[0].visual_key == chosen, "第%d次播放不重抽" % index)
 check(variants.size() == 2, "固定种子样本中拳/掌两种外观均会出现")
 seed(18273)
 var expected := randf()
 seed(18273)
 release("拳掌")
 check(randf() == expected, "外观随机不消耗战斗全局随机序列")
 arena.strike_fx.clear()
 release("拳掌", Vector2.LEFT)
 var first_visual := str(arena.strike_fx[0].visual_key)
 release("痛击", Vector2.UP)
 check(arena.strike_fx[0].kind == "拳掌" and arena.strike_fx[0].visual_key == first_visual and arena.strike_fx[0].dir == Vector2.LEFT, "痛击不覆盖前次拳掌身份外观方向")
 check(arena.strike_fx[1].visual_key == "痛击" and arena.strike_fx[1].dir == Vector2.UP and arena.strike_fx[1].reach == 90, "痛击独立图集及方向范围正确")
 reset_guard()
 arena.use_skill("架招")
 check(arena.guard_fx.size() == 1 and not arena.guard_fx[0].success, "架招发动时播放普通守势")
 check(arena.stamina == 5 and is_equal_approx(arena.guard_window, 0.2), "架招仍1体力、0.20秒判定")
 var stored_direction: Vector2 = arena.guard_fx[0].dir
 arena.facing = -1
 check(arena.guard_fx[0].dir == stored_direction, "守势方向不随后续转身改变")
 arena.receive_wolf_bite()
 check(arena.guard_fx.size() == 1 and arena.guard_fx[0].success, "实际化解才替换为成功反馈")
 check(arena.guard_fx[0].dir == Vector2.RIGHT, "成功反馈朝向实际来袭")
 check(arena.guard_window == 0 and arena.hp == 84, "成功后判定消散，原化解6及剩余16伤害不变")
 check(arena.stamina == 5 and arena.invulnerable == 0 and is_equal_approx(arena.skill_cooldown_remaining("架招"), 4.76), "成功特效不返费、不无敌、不重置冷却")
 arena.receive_wolf_bite()
 check(arena.hp == 62 and arena.guard_fx.size() == 1, "成功特效不继续化解后续攻击")
 arena.configuring = true
 var guard_age: float = arena.guard_fx[0].age
 var strike_age: float = arena.strike_fx[0].age
 arena._process(0.05)
 check(arena.guard_fx[0].age == guard_age and arena.strike_fx[0].age == strike_age, "配置界面暂停守势和攻击特效")
 arena.configuring = false
 arena.battle_hud.help_open = true
 arena._process(0.05)
 check(arena.guard_fx[0].age == guard_age, "帮助界面暂停守势反馈")
 arena.battle_hud.help_open = false
 arena.update_guard_fx(0.401)
 check(arena.guard_fx.is_empty() and arena.guard_window == 0, "成功反馈0.40秒结束且不回开窗口")
 reset_guard()
 arena.use_skill("架招")
 arena.update_guard_fx(0.451)
 check(arena.guard_fx.is_empty() and is_equal_approx(arena.guard_window, 0.2), "普通守势余韵0.45秒清理，视觉推进不篡改判定字段")
 reset_guard()
 arena.receive_wolf_bite()
 check(arena.hp == 78 and arena.guard_fx.is_empty(), "未架招受击没有成功反馈")
 check(arena.skill_cost("拳掌") == 0 and arena.skill_cost("痛击") == 1 and arena.skill_cost("架招") == 1, "资源费用不变")
 check(is_equal_approx(arena.skill_cooldown_duration("拳掌"), 0.784) and is_equal_approx(arena.skill_cooldown_duration("痛击"), 5.0 / 1.16) and is_equal_approx(arena.skill_cooldown_duration("架招"), 4.76), "冷却按当前六招节奏读取")
 check(arena.STRIKE_FX_SECONDS == 0.65 and arena.HEAVY_FX_SECONDS == 0.90 and arena.GUARD_FX_SECONDS == 0.45, "攻击和守势显示时长正确")
 # Persistent visuals must never become persistent gameplay effects.
 arena.strike_fx.clear()
 release("拳掌")
 var hp_before: int = arena.enemy_hp
 arena.update_strike_fx(0.40)
 check(arena.strike_fx.size() == 1, "拳掌在下一次普攻时仍有留场残影")
 check(arena.enemy_hp == hp_before, "留场不追加伤害")
 check(arena.strike_fx_frame_index("拳掌", 0.15) == 3, "快速出手后保留清楚拳掌轮廓")
 var fixed_origin: Vector2 = arena.strike_fx[0].origin
 arena.player += Vector2(100, 0)
 check(arena.strike_fx[0].origin == fixed_origin, "玩家离开后残影保持出手位置")
 arena.update_strike_fx(0.251)
 check(arena.strike_fx.is_empty(), "拳掌0.65秒自然消散")
 release("痛击")
 arena.update_strike_fx(0.70)
 check(arena.strike_fx.size() == 1, "痛击余韵比普攻更长")
 arena.update_strike_fx(0.201)
 check(arena.strike_fx.is_empty(), "痛击0.90秒清理")
 reset_guard()
 arena.use_skill("架招")
 arena.guard_window = 0.0
 arena.update_guard_fx(0.25)
 check(arena.guard_fx.size() == 1, "判定结束后架招仍有视觉余韵")
 arena.receive_wolf_bite()
 check(arena.hp == 78 and not arena.guard_fx[0].success, "残留劲纹不能化解后续攻击")
 var guard_origin: Vector2 = arena.guard_fx[0].origin
 arena.player += Vector2(100, 0)
 arena.update_guard_fx(0.05)
 check(arena.guard_fx[0].origin == guard_origin, "过期劲纹留在原位不跟随人物")
 arena.update_guard_fx(0.151)
 check(arena.guard_fx.is_empty(), "架招视觉0.45秒清理")
 print("PALM_QI_V5_CHECKS ", count, " failures=", failures)
 arena.queue_free()
 await process_frame
 quit(1 if failures > 0 else 0)
