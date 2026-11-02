extends Node2D

# Standalone feel test. The example sprites are read from the local installation.
const BASE = "E:/SteamLibrary/steamapps/common/鬼谷八荒/Mod/modFQA/资源修改教程/ResBuildABProject_Example/Assets/Example/1.BattleHuman（战斗小人模型示例）/Assets/Texture/Battle/Human/"
var parts = []
var player = Vector2(400, 500)
var enemy = Vector2(1000, 400)
var shots = []
var enemy_shots = []
var hp = 5
var enemy_hp = 20
var fire_timer = 0.0
var enemy_timer = 1.0
var dash_timer = 0.0
var invulnerable = 0.0
var elapsed = 0.0
var facing = 1.0
var message = ""
var walk_phase = 0.0
var walk_amount = 0.0

func _ready():
 # Layer order follows the example prefab; hair is aligned for the unskinned sprite.
 for item in [["Man/Body/101/zuobi.png", Vector2(10.66,-59.48)], ["Man/Body/101/tui.png", Vector2(-2.9,-26.1)], ["Man/Body/101/tui.png", Vector2(3.6,-30.1)], ["Man/Body/101/yifu.png", Vector2(2.1,-44.56)], ["Man/Head/101.png", Vector2(7.52,-83.55)], ["Man/Body/101/youbi.png", Vector2(-1.44,-63.34)], ["Man/Hair/101.png", Vector2(0.0,-71.0)]]:
  var img = Image.new()
  if img.load(BASE + item[0]) == OK:
   parts.append([ImageTexture.create_from_image(img), item[1]])
 if parts.size() != 7:
  message = "未找到本机鬼谷八荒示例素材，请检查安装路径"
 queue_redraw()

func _process(delta):
 delta = minf(delta, 0.05)
 elapsed += delta
 fire_timer = maxf(0, fire_timer-delta)
 enemy_timer -= delta
 dash_timer = maxf(0, dash_timer-delta)
 invulnerable = maxf(0, invulnerable-delta)
 if hp > 0 and enemy_hp > 0:
  var direction = Vector2(float(Input.is_key_pressed(KEY_D))-float(Input.is_key_pressed(KEY_A)), float(Input.is_key_pressed(KEY_S))-float(Input.is_key_pressed(KEY_W))).normalized()
  walk_amount = move_toward(walk_amount, 1.0 if direction != Vector2.ZERO else 0.0, delta * 8.0)
  if direction != Vector2.ZERO: walk_phase += delta * 11.0
  var speed = 235.0
  if Input.is_key_pressed(KEY_SPACE) and dash_timer <= 0 and direction != Vector2.ZERO:
   dash_timer = 1.0
   invulnerable = 0.18
  if invulnerable > 0: speed = 600.0
  player += direction * speed * delta
  player = player.clamp(Vector2(50,160), Vector2(1230,680))
  facing = 1.0 if get_global_mouse_position().x >= player.x else -1.0
  if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT) and fire_timer <= 0:
   var aim = (get_global_mouse_position()-player).normalized()
   if aim != Vector2.ZERO:
    shots.append([player + Vector2(0,-35), aim * 620.0])
    fire_timer = 0.25
  var diff = player - enemy
  if diff.length() > 260: enemy += diff.normalized() * 95.0 * delta
  if enemy_timer <= 0:
   var aim = (player-enemy).normalized()
   for angle in [-0.25, 0.0, 0.25]: enemy_shots.append([enemy, aim.rotated(angle)*240.0])
   enemy_timer = 1.5
  for s in shots: s[0] += s[1]*delta
  for s in enemy_shots: s[0] += s[1]*delta
  for i in range(shots.size()-1,-1,-1):
   if shots[i][0].distance_to(enemy) < 35:
    enemy_hp -= 1
    shots.remove_at(i)
   elif not Rect2(-50,-50,1380,820).has_point(shots[i][0]): shots.remove_at(i)
  for i in range(enemy_shots.size()-1,-1,-1):
   if enemy_shots[i][0].distance_to(player) < 24:
    if invulnerable <= 0:
     hp -= 1
     invulnerable = 0.5
    enemy_shots.remove_at(i)
   elif not Rect2(-50,-50,1380,820).has_point(enemy_shots[i][0]): enemy_shots.remove_at(i)
 if Input.is_key_pressed(KEY_R) and (hp <= 0 or enemy_hp <= 0):
  player = Vector2(400,500); enemy = Vector2(1000,400); hp = 5; enemy_hp = 20; shots.clear(); enemy_shots.clear(); enemy_timer = 1.0
 queue_redraw()

func _draw():
 draw_rect(Rect2(0,0,1280,720), Color("d9d2b9"))
 draw_rect(Rect2(22,120,1236,574), Color("aeb99a"))
 for x in range(40,1250,80):
  for y in range(140,690,80): draw_circle(Vector2(x,y), 2, Color("9baa8e"))
 draw_circle(enemy + Vector2(0,12), 30, Color(0,0,0,0.17))
 draw_circle(enemy, 30, Color("804c46"))
 draw_circle(enemy + Vector2(-9,-7), 4, Color("f3dfbd"))
 draw_circle(enemy + Vector2(9,-7), 4, Color("f3dfbd"))
 draw_rect(Rect2(enemy.x-35,enemy.y-54,70,6), Color("493b34"))
 draw_rect(Rect2(enemy.x-35,enemy.y-54,70*maxf(enemy_hp,0)/20.0,6), Color("bd5850"))
 for s in shots: draw_circle(s[0], 7, Color("e8f7dc"))
 for s in enemy_shots: draw_circle(s[0], 9, Color("d75f4b"))
 draw_circle(player + Vector2(0,5), 22, Color(0,0,0,0.18))
 var stride = sin(walk_phase) * walk_amount
 var bounce = absf(sin(walk_phase)) * 2.0 * walk_amount
 for i in range(parts.size()):
  var part = parts[i]
  var texture = part[0]
  var center: Vector2 = part[1]
  var pivot = Vector2.ZERO
  var angle = 0.0
  if i == 1 or i == 2:
   pivot = center + Vector2(0,-25)
   angle = stride * (0.32 if i == 1 else -0.32)
  elif i == 0 or i == 5:
   pivot = center + Vector2(0,-15)
   angle = stride * (0.20 if i == 0 else -0.20)
  else:
   pivot = center
  var origin = player + Vector2(pivot.x * facing, pivot.y - bounce)
  draw_set_transform(origin, angle * facing, Vector2(facing,1))
  var tint = Color(0.91, 0.76, 0.65) if i == 4 else Color.WHITE
  draw_texture(texture, center - pivot - Vector2(texture.get_size())/2.0, tint)
 draw_set_transform(Vector2.ZERO)
 var font = ThemeDB.fallback_font
 draw_string(font, Vector2(35,39), "动作战斗试验场", HORIZONTAL_ALIGNMENT_LEFT, -1, 27, Color("333e38"))
 draw_string(font, Vector2(35,72), "WASD 移动   鼠标瞄准   按住左键发射   空格冲刺躲避", HORIZONTAL_ALIGNMENT_LEFT, -1, 19, Color("333e38"))
 draw_string(font, Vector2(35,101), "生命：%d / 5     敌人：%d / 20     冲刺：%s" % [hp, enemy_hp, "就绪" if dash_timer <= 0 else "冷却中"], HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color("333e38"))
 if message != "": draw_string(font, Vector2(400,350), message, HORIZONTAL_ALIGNMENT_LEFT, -1, 22, Color.RED)
 if hp <= 0 or enemy_hp <= 0:
  draw_rect(Rect2(0,0,1280,720), Color(0,0,0,0.5))
  draw_string(font, Vector2(475,340), "试验结束：%s" % ("胜利" if enemy_hp <= 0 else "失败"), HORIZONTAL_ALIGNMENT_LEFT, -1, 32, Color.WHITE)
  draw_string(font, Vector2(500,380), "按 R 再试一次", HORIZONTAL_ALIGNMENT_LEFT, -1, 22, Color.WHITE)
