extends SceneTree
const RULES = preload("sword_combo_rules.gd")
const Runtime = preload("sword_combo_runtime.gd")
class Host extends Node2D:
 var paused := false
 var physique := 8.0
 var soul := 8.0
 var sword_attainment := 6.0
 var cooldown_remaining := 0.0
 var player := Vector2.ZERO
 var held := true
 var direction := Vector2.RIGHT
 var records: Array[Dictionary] = []
 func aim_direction() -> Vector2: return direction
 func clamp_position() -> void: player = player.clamp(Vector2(-100,-100),Vector2(100,100))
 func emit_release(release: Dictionary, _elapsed := 0.0) -> void: records.append(release)

var checks := 0
var failures := 0
var actor: Host
var combo: RefCounted

func _initialize() -> void: call_deferred("verify")
func check(value: bool, text: String) -> void:
 checks += 1
 if not value:
  failures += 1
  push_error(text)

func setup_case() -> void:
 if actor!=null: actor.free()
 actor = Host.new()
 root.add_child(actor)
 combo = Runtime.new()
 combo.setup(actor)

func verify() -> void:
 var expect_damage := [5,6,8,10]
 var expect_reach := [168.0,178.0,248.0,209.6]
 for index in range(4):
  var rule := RULES.preview(index,8,8,6)
  check(rule.damage==expect_damage[index],"四段实际取整基础伤害")
  check(is_equal_approx(rule.reach,expect_reach[index]),"四段实际距离")
 check(is_equal_approx(RULES.tempo(0,0,0)*2.1,2.1),"零属性动作2.1秒")
 check(is_equal_approx(RULES.tempo(8,8,6)*2.1,1.84859155),"示例动作约1.849秒")
 check(is_equal_approx(RULES.cooldown(8,6),0.815660685),"示例结束后CD约0.816秒")
 check(RULES.tempo(99999,99999,99999)==0.65 and RULES.cooldown(99999,99999)==0.45,"动作与CD保留下限")
 check(RULES.tempo(-8,-8,-8)==1.0 and RULES.cooldown(-8,-8)==1.0,"负属性按零读取")
 for axis in range(8):
  var d := Vector2.from_angle(axis*TAU/8.0)
  for index in range(4):
   var release := RULES.preview(index,8,8,6)
   release.origin = Vector2(37,-18)
   release.direction = d
   check(RULES.contains(release,Vector2(release.origin)+d*float(release.reach)),"八向最远端包含")
   check(not RULES.contains(release,Vector2(release.origin)+d*(float(release.reach)+0.01)),"八向射程外排除")
   check(not RULES.contains(release,Vector2(release.origin)-d*0.1),"八向身后排除")
   if index==2:
    check(RULES.contains(release,Vector2(release.origin)+d*120+d.orthogonal()*20),"胶囊宽40边缘包含")
    check(not RULES.contains(release,Vector2(release.origin)+d*120+d.orthogonal()*20.01),"胶囊宽40之外排除")
   else:
    var half := deg_to_rad(float(release.angle)/2)
    check(RULES.contains(release,Vector2(release.origin)+d.rotated(half)*40),"扇面边缘包含")
    check(not RULES.contains(release,Vector2(release.origin)+d.rotated(half+0.001)*40),"扇面角度外排除")
 setup_case()
 check(combo.request(),"就绪时起手")
 combo.advance(2.1*RULES.tempo(8,8,6))
 check(actor.records.size()==4,"连续输入只产生四段")
 check(actor.player.distance_to(Vector2(20,0))<0.00001,"两次前压合计20")
 check(not combo.active and is_equal_approx(actor.cooldown_remaining,RULES.cooldown(8,6)),"完整收势后才开始CD")
 check(not combo.request(),"冷却中拒绝起手")
 combo.advance(actor.cooldown_remaining+0.00001)
 check(combo.request(),"冷却结束可以起下一套")
 setup_case()
 actor.held = false
 combo.request()
 for repeat in range(1000): combo.request()
 combo.advance(3)
 check(actor.records.size()==2,"一千次连点最多缓存下一段一次")
 setup_case()
 actor.held = false
 combo.request()
 combo.advance(float(combo.current.duration))
 check(combo.waiting and actor.cooldown_remaining==0,"未接续时先等待0.3秒")
 combo.advance(0.30)
 check(not combo.active and is_equal_approx(actor.cooldown_remaining,RULES.cooldown(8,6)),"续接超时后开始完整CD")
 setup_case()
 combo.request()
 combo.advance(0.10)
 var age: float = combo.age
 var position := actor.player
 actor.paused = true
 combo.advance(3)
 check(combo.age==age and actor.player==position and actor.records.is_empty(),"暂停冻结准备、前压、出手与CD")
 actor.paused = false
 combo.advance(0.40)
 var position_before := actor.player
 var attacks_before := actor.records.size()
 combo.finish()
 combo.advance(1)
 check(actor.player==position_before and actor.records.size()==attacks_before,"中断不补剩余前压或攻击")
 setup_case()
 combo.request()
 combo.advance(0.36)
 actor.direction = Vector2.UP
 combo.advance(0.20)
 check(actor.player.length()<=6.0001,"准备期转向不重置前压预算")
 setup_case()
 actor.player = Vector2(99,0)
 combo.request()
 combo.advance(2.0)
 check(actor.player.x<=100.0,"前压受边界截断")
 var release: Dictionary = actor.records[0]
 var point: Vector2 = release.origin+release.direction*40.0
 check(combo.take_contact(release,"a",point),"合法目标首次接触")
 check(not combo.take_contact(release,"a",point),"同一段同一目标去重")
 check(combo.take_contact(release,"b",point),"不同目标独立接触")
 check(actor.records.back().is_basic_attack and actor.records.back().is_combo_last and actor.records.back().final_stage_base_damage==10,"普攻末段协议带整段防御前基础值")
 check(actor.records.all(func(record: Dictionary): return record.name==RULES.SKILL and record.skill_id==RULES.SKILL_ID and record.keywords==["剑"] and record.attack_style=="melee"),"四段共用剑式技能身份、剑关键词与近身方式")
 actor.free()
 actor = null
 var scene: Node2D = load("res://sword_combo_preview.tscn").instantiate()
 root.add_child(scene)
 scene.set_process(false)
 scene.auto_loop = false
 var play_buttons := scene.get_children().filter(func(node: Node): return node is Button and node.text=="完整连招")
 check(play_buttons.size()==1,"原生界面具有单次完整连招按钮")
 play_buttons[0].emit_signal("pressed")
 for frame in range(240): scene.advance(1.0/60.0)
 check(scene.combo.releases.size()==4 and not scene.combo.active and not scene.held,"完整连招按钮仅播放一套并回到就绪")
 scene.restart()
 scene.held = true
 var mouse := InputEventMouseButton.new()
 mouse.button_index = MOUSE_BUTTON_LEFT
 mouse.pressed = false
 scene._input(mouse)
 check(not scene.held,"鼠标移到GUI上释放也清除按住状态")
 var key := InputEventKey.new()
 key.pressed = true
 key.keycode = KEY_L
 scene._unhandled_input(key)
 for frame in range(360): scene.advance(1.0/60.0)
 check(scene.auto_loop and scene.combo.releases.size()>=8,"L开启连续演示后可跨CD播放多套")
 scene._unhandled_input(key)
 var count_before: int = scene.combo.releases.size()
 for frame in range(240): scene.advance(1.0/60.0)
 check(not scene.auto_loop and not scene.held and not scene.combo.active and scene.combo.releases.size()<=count_before+1,"L关闭循环后不残留按住输入或启动新套")
 key.keycode = KEY_G
 var geometry_before: bool = scene.show_geometry
 scene._unhandled_input(key)
 check(scene.show_geometry!=geometry_before,"G切换实际判定对照")
 key.keycode = KEY_F
 scene._unhandled_input(key)
 check(scene.aim_direction()==Vector2.LEFT,"F切换左向预览")
 scene.aim_override = Vector2.RIGHT
 scene.start_single(2)
 for frame in range(18): scene.advance(1.0/60.0)
 var before: Transform2D = scene.rig.wanderer_skin.approved_sword.sword_sprite.global_transform
 var torso: float = scene.rig.wanderer_skin.bones.torso.rotation
 scene.paused = true
 for frame in range(12): scene.advance(1.0/60.0)
 check(scene.rig.wanderer_skin.approved_sword.sword_sprite.global_transform==before and scene.rig.wanderer_skin.bones.torso.rotation==torso,"暂停时握柄、剑图与躯干不累计旋转")
 scene.paused = false
 var original_torso: float = scene.rig.wanderer_skin.bones.torso.rotation
 for frame in range(40): scene.update_actor(0.000001)
 check(absf(scene.rig.wanderer_skin.bones.torso.rotation-original_torso)<0.002,"相同阶段重复采样不会累计躯干倾斜")
 var release_height: float = scene.last_release.attack_height
 check(release_height>0 and scene.vfx.marks[0].attack_height==release_height,"墨痕读取并固定实际释放手高")
 check(scene.rig.wanderer_skin.approved_sword.last.attachment_error<0.002,"独立四段姿态仍使用真实握柄挂点")
 scene.restart()
 check(scene.vfx.marks.is_empty() and scene.combo.releases.is_empty() and not scene.combo.active,"重开清除派生墨痕、记录和连段")
 scene.queue_free()
 await process_frame
 print("SWORD_COMBO_CHECKS ",checks," failures=",failures)
 quit(1 if failures else 0)
