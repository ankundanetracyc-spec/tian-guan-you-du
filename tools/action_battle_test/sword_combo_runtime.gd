extends RefCounted
const RULES = preload("sword_combo_rules.gd")
var host: Node2D
var active := false
var stage := -1
var age := 0.0
var q := 1.0
var waiting := false
var wait_remaining := 0.0
var buffered := false
var hit_done := false
var chain_id := 0
var single_stage := false
var current: Dictionary = {}
var direction := Vector2.RIGHT
var releases: Array[Dictionary] = []

func setup(owner_node: Node2D) -> void: host = owner_node

func request() -> bool:
 if host.paused: return false
 if active:
  if stage>=3: return false
  if waiting: _begin_stage(stage+1)
  else: buffered = true
  return true
 if host.cooldown_remaining>0.0: return false
 chain_id += 1
 q = RULES.tempo(host.physique,host.soul,host.sword_attainment)
 active = true
 single_stage = false
 _begin_stage(0)
 return true

func _begin_stage(index: int) -> void:
 stage = index
 age = 0.0
 waiting = false
 wait_remaining = 0.0
 buffered = false
 hit_done = false
 single_stage = false
 current = RULES.preview(index,host.physique,host.soul,host.sword_attainment,q)
 direction = host.aim_direction()

func progress() -> float: return clampf(age/float(current.get("duration",1.0)),0.0,1.0)

func finish(elapsed := 0.0) -> void:
 if not active: return
 active = false
 waiting = false
 buffered = false
 host.cooldown_remaining = maxf(0.0,RULES.cooldown(host.physique,host.sword_attainment)-elapsed)

func clear() -> void:
 active = false
 stage = -1
 age = 0.0
 waiting = false
 buffered = false
 hit_done = false
 current.clear()
 releases.clear()

func take_contact(release: Dictionary, target_id: String, point: Vector2) -> bool:
 if release.targets.has(target_id) or not RULES.contains(release,point): return false
 release.targets[target_id] = true
 return true

func advance(delta: float) -> void:
 if host.paused: return
 host.cooldown_remaining = maxf(0.0,host.cooldown_remaining-delta)
 var remaining := delta
 while active and remaining>0.000001:
  if waiting:
   if host.held or buffered:
    _begin_stage(stage+1)
    continue
   var consumed := minf(remaining,wait_remaining)
   remaining -= consumed
   wait_remaining -= consumed
   if wait_remaining<=0.000001: finish(remaining)
   continue
  var duration: float = current.duration
  var contact := duration*float(current.hit)
  if not hit_done: direction = host.aim_direction()
  var boundary := duration if hit_done else contact
  var consumed := minf(remaining,maxf(0.0,boundary-age))
  var before := smoothstep(0.0,1.0,clampf(age/contact,0.0,1.0))
  age += consumed
  remaining -= consumed
  var after := smoothstep(0.0,1.0,clampf(age/contact,0.0,1.0))
  var step := float(current.step)*(after-before)
  if step>0.0:
   host.player += direction*step
   host.clamp_position()
  if not hit_done and age>=contact-0.000001:
   direction = host.aim_direction()
   hit_done = true
   var release := current.duplicate(true)
   release.stage_name = str(current.name)
   release.name = RULES.SKILL
   release.origin = host.player
   release.direction = direction
   release.targets = {}
   release.combo_id = chain_id
   release.is_basic_attack = true
   release.is_combo_last = stage==3
   release.final_stage_base_damage = int(current.damage) if stage==3 else 0
   release.release_offset = delta-remaining
   releases.append(release)
   host.emit_release(release,remaining)
  if age>=duration-0.000001:
   if stage==3 or single_stage: finish(remaining)
   elif host.held or buffered: _begin_stage(stage+1)
   else:
    waiting = true
    wait_remaining = RULES.CONNECT_WINDOW
