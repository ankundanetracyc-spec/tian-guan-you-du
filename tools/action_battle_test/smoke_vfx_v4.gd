extends SceneTree

var arena: Node2D
var count := 0
var failures := 0

func _initialize() -> void:
 call_deferred("run_checks")

func check(condition: bool, label: String) -> void:
 count += 1
 if not condition:
  failures += 1
  push_error(label)

func release(kind: String, origin: Vector2, direction: Vector2) -> void:
 arena.player = origin
 arena.enemy = origin + direction * 35
 arena.enemy_hp = 1000
 arena.pending_attack = kind
 arena.attack_direction = direction
 arena.resolve_pending_attack()

func run_checks() -> void:
 var local_scene := "res://ActionBattleFeelTest.tscn"
 var scene_path := local_scene if ResourceLoader.exists(local_scene) else "res://tscn/scene/ActionBattleTrialScene.tscn"
 arena = load(scene_path).instantiate()
 root.add_child(arena)
 arena.set_process(false)
 check(arena.STRIKE_FX_SECONDS == 0.65 and arena.STRIKE_FX_FRAMES == 8, "8帧保留，普攻视觉留场0.65秒")
 check(arena.strike_fx_textures.size() == 4, "随机拳掌双图集、独立痛击与架招均已加载")
 for kind in ["拳掌", "拳掌_掌"]:
  check(str(arena.STRIKE_FX_ATLASES[kind]).begins_with("art/palm_qi_v4/"), kind + "使用v4资源")
  var texture: Texture2D = arena.strike_fx_textures[kind]
  check(texture.get_size() == Vector2(1774, 887), kind + "保留实际图集尺寸")
  check(texture.get_image().get_format() == Image.FORMAT_RGBA8, kind + "图集含RGBA")
  for frame in range(8):
   var source: Rect2 = arena.strike_fx_frame_rect(texture, frame)
   check(source.position.x >= 1 and source.position.y >= 1 and source.end.x <= 1773 and source.end.y <= 886, kind + "帧%d采样在1像素内缩范围" % frame)
   check(source.size.is_equal_approx(Vector2(441.5, 441.5)), kind + "帧%d浮点格宽高正确" % frame)
  var destination: Rect2 = arena.strike_fx_destination_rect(texture, 70, 0.5)
  check(is_equal_approx(destination.size.x / destination.size.y, 1), kind + "源图比例保留")
  check(is_equal_approx(destination.end.x, 70), kind + "最远边保持攻击范围")
 var rectangular := ImageTexture.create_from_image(Image.create(1200, 400, false, Image.FORMAT_RGBA8))
 var sample: Rect2 = arena.strike_fx_frame_rect(rectangular, 0)
 var rendered: Rect2 = arena.strike_fx_destination_rect(rectangular, 90, 0.5)
 check(is_equal_approx(sample.size.x / sample.size.y, rendered.size.x / rendered.size.y), "非方格源图也不被压扁")
 arena.strike_fx.clear()
 var origin := Vector2(400, 450)
 release("拳掌", origin, Vector2.RIGHT)
 arena.update_strike_fx(0.05)
 release("痛击", origin + Vector2(20, 10), Vector2(-1, -1).normalized())
 check(arena.strike_fx.size() == 2, "两次释放保留独立实例")
 check(arena.strike_fx[0].origin == origin and arena.strike_fx[0].dir == Vector2.RIGHT and arena.strike_fx[0].kind == "拳掌", "后发技能不改变前次位置方向和类型")
 check(arena.strike_fx[0].age == 0.05 and arena.strike_fx[1].age == 0, "实例计时独立")
 check(arena.strike_fx[0].reach == 70 and arena.strike_fx[1].reach == 90, "70/90范围不变")
 check(arena.strike_fx[0].has("hit") and arena.strike_fx[0].has("impact"), "命中信息被独立保存")
 var remaining_hp: int = arena.enemy_hp
 arena.update_strike_fx(0.05)
 check(arena.enemy_hp == remaining_hp, "动画推进不重复结算伤害")
 arena.configuring = true
 var age: float = arena.strike_fx[0].age
 arena._process(0.04)
 check(arena.strike_fx[0].age == age, "配置暂停特效计时")
 arena.configuring = false
 arena.update_strike_fx(0.56)
 check(arena.strike_fx.size() == 1 and arena.strike_fx[0].kind == "痛击", "先发实例按自己的寿命清理")
 arena.update_strike_fx(0.30)
 check(arena.strike_fx.is_empty(), "尾帧后清除全部实例")
 check(arena.skill_cost("拳掌") == 0 and arena.skill_cost("痛击") == 1, "0/1体力保持")
 check(is_equal_approx(arena.skill_cooldown_duration("拳掌"), 0.784) and is_equal_approx(arena.skill_cooldown_duration("痛击"), 5.0 / 1.16), "冷却按各招当前属性公式读取")
 print("PALM_QI_V4_CHECKS ", count, " failures=", failures)
 arena.queue_free()
 await process_frame
 quit(1 if failures > 0 else 0)
