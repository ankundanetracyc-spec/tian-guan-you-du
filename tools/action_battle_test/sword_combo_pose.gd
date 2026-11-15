extends RefCounted
## Rotation-only sword phrasing after the approved garment/locomotion pose.
const SWEEP = preload("sword_sweep_motion.gd")
const RULES = preload("sword_combo_rules.gd")
const ARM_KEYS := [
 [Vector3(72,25,-60),Vector3(-20,-65,-75),Vector3(40,2,32),Vector3(75,40,60)],
 [Vector3(75,40,60),Vector3(70,35,78),Vector3(25,-20,-22),Vector3(-5,-50,-60)],
 [Vector3(-5,-50,-60),Vector3(75,-25,-95),Vector3(8,-8,0),Vector3(45,10,-35)],
 [Vector3(45,10,-35),Vector3(-20,-80,-110),Vector3(35,5,40),Vector3(72,25,-60)]
]
var last: Dictionary = {}

static func _hermite(a: float, b: float, va: float, vb: float, age: float, duration: float) -> float:
 var t := clampf(age/maxf(duration,0.000001),0.0,1.0)
 var t2 := t*t
 var t3 := t2*t
 return (2.0*t3-3.0*t2+1.0)*a+(t3-2.0*t2+t)*duration*va+(-2.0*t3+3.0*t2)*b+(t3-t2)*duration*vb

static func _key(stage: int, knot: int, channel: int) -> float:
 var value: Vector3 = ARM_KEYS[stage][knot]
 # Interpolate elbow flex independently, keeping the bend on its safe side.
 return value.x-value.y if channel==1 else value[channel]

static func _ready_fraction(channel: int) -> float:
 return 0.38 if channel==0 else (0.53 if channel==1 else 0.45)

static func _entry_velocity(stage: int, channel: int, tempo: float) -> float:
 var duration := float(RULES.STAGES[stage].duration)*tempo
 var ready := duration*float(RULES.STAGES[stage].hit)*_ready_fraction(channel)
 return clampf((_key(stage,1,channel)-_key(stage,0,channel))*0.35/maxf(ready,0.000001),-480.0,480.0)

static func _joint(stage: int, rule: Dictionary, age: float, channel: int) -> float:
 var duration: float = rule.duration
 var contact := duration*float(rule.hit)
 var ready := contact*_ready_fraction(channel)
 var tempo := duration/float(RULES.STAGES[stage].duration)
 var a := _key(stage,0,channel)
 var b := _key(stage,1,channel)
 var c := _key(stage,2,channel)
 var d := _key(stage,3,channel)
 var entry := _entry_velocity(stage,channel,tempo)
 var exit := _entry_velocity((stage+1)%4,channel,tempo)
 var vb := (c-a)*0.70/contact
 var vc := (d-b)*0.75/(duration-ready)
 if age<ready: return _hermite(a,b,entry,vb,age,ready)
 if age<contact: return _hermite(b,c,vb,vc,age-ready,contact-ready)
 return _hermite(c,d,vc,exit,age-contact,duration-contact)

static func _sword_angle(stage: int, rule: Dictionary, age: float) -> float:
 var duration: float = rule.duration
 var contact := duration*float(rule.hit)
 var ready := contact*0.45
 var tempo := duration/float(RULES.STAGES[stage].duration)
 var entry := _entry_velocity(stage,2,tempo)
 var exit := _entry_velocity((stage+1)%4,2,tempo)
 var a := _key(stage,0,2)
 var windup := _key(stage,1,2)
 if str(rule.shape)=="capsule":
  # A thrust crosses the contact with an extending arm; its blade stays axial.
  if age<ready: return _hermite(a,windup,entry,0.0,age,ready)
  if age<contact: return _hermite(windup,0.0,0.0,0.0,age-ready,contact-ready)
  var hold_end := minf(duration,contact+float(rule.reveal))
  if age<hold_end: return 0.0
  return _hermite(0.0,_key(stage,3,2),0.0,exit,age-hold_end,duration-hold_end)
 var direction := -1.0 if stage==1 else 1.0
 var contact_speed := direction*float(rule.angle)*1.5/float(rule.reveal)
 if age<ready: return _hermite(a,windup,entry,0.0,age,ready)
 if age<contact: return _hermite(windup,0.0,0.0,contact_speed,age-ready,contact-ready)
 var reveal_end := contact+float(rule.reveal)
 if age<=reveal_end: return rad_to_deg(SWEEP.angle(stage,float(rule.angle),age-contact,float(rule.reveal)))
 return _hermite(direction*float(rule.angle)*0.5,_key(stage,3,2),0.0,exit,age-reveal_end,duration-reveal_end)

static func sample(stage: int, rule: Dictionary, age: float, arm_lengths := Vector2(11.4799,14.4890)) -> Dictionary:
 var phase := clampf(age/float(rule.duration),0.0,1.0)
 var upper := _joint(stage,rule,age,0)
 # A guard against a future inverted elbow, not a stop at the keyframe range.
 # The current raw curve is 12.9868..100.1765 degrees, so this never clips it.
 var flex := clampf(_joint(stage,rule,age,1),8.0,110.0)
 var compress := 1.0
 if str(rule.shape)=="capsule":
  # Keep the thrust's forward extension at contact while matching the flatter
  # neighbouring sweep keys and their derivatives at both stage boundaries.
  var contact := float(rule.duration)*float(rule.hit)
  compress = 1.0-smoothstep(0.0,contact,age) if age<=contact else smoothstep(contact,float(rule.duration),age)
 upper = lerpf(upper,32.0+(upper-32.0)*0.28,compress)
 flex = lerpf(flex,35.0+(flex-35.0)*0.35,compress)
 if str(rule.shape)!="capsule":
  var contact := float(rule.duration)*float(rule.hit)
  var follow := minf(float(rule.duration),contact+float(rule.reveal))
  var fold := smoothstep(contact,follow,age) if age<=follow else 1.0-smoothstep(follow,float(rule.duration),age)
  flex = lerpf(flex,86.0,fold)
 # Aim the shoulder-to-wrist line just below horizontal while the elbow folds.
 # This changes rotations only; the lengths are measured from the actual rig.
 var bend := deg_to_rad(flex)
 var horizontal_upper := rad_to_deg(atan2(arm_lengths.y*sin(bend),arm_lengths.x+arm_lengths.y*cos(bend)))+8.0
 upper = lerpf(upper,horizontal_upper,compress)
 var angle := _sword_angle(stage,rule,age)
 var lean := sin(phase*PI)*(0.075 if stage==2 else 0.040)+sin(phase*TAU)*0.018
 return {"arm_angles":Vector3(upper,upper-flex,angle),"lean":lean,"phase":phase,"elbow_flex":flex,
  "sword_angle":deg_to_rad(angle),"sweep_age":age-float(rule.duration)*float(rule.hit)}

func apply(rig: Node2D, combo: RefCounted, aim: Vector2) -> void:
 if not combo.active:
  _project_sword(rig.wanderer_skin.approved_sword)
  last = {"active":false}
  return
 apply_at(rig,combo.stage,combo.current,combo.age,aim)

func apply_at(rig: Node2D, stage: int, rule: Dictionary, age: float, aim: Vector2) -> void:
 var skin: RefCounted = rig.wanderer_skin
 var sword: RefCounted = skin.approved_sword
 var hands: RefCounted = skin.approved_hands
 var lengths := Vector2(skin.bones.near_upper.global_position.distance_to(skin.bones.near_forearm.global_position),
  skin.bones.near_forearm.global_position.distance_to(skin.bones.near_hand.global_position))
 var value := sample(stage,rule,age,lengths)
 var angles: Vector3 = value.arm_angles
 var baseline_torso: float = skin.bones.torso.rotation
 var lean: float = value.lean
 skin.bones.torso.rotation += lean
 skin.bones.far_upper.rotation -= lean*0.65
 skin.bones.far_forearm.rotation += sin(float(value.phase)*PI)*0.06
 skin._aim_arm("near",deg_to_rad(angles.x),deg_to_rad(angles.y),0.0)
 hands.update_pose()
 sword.update_attachment()
 var attachment: Transform2D = hands.get_attachment_transform("near")
 var axis: Vector2 = sword.sword_sprite.to_global(sword.source_tip)-attachment.origin
 var sign_value := signf(rig.global_transform.determinant())
 # The sweep is a world angle around aim, including mirrored/diagonal bodies.
 var target_angle := aim.angle()+float(value.sword_angle)
 skin.bones.near_hand.rotation += wrapf(target_angle-axis.angle(),-PI,PI)*sign_value
 hands.update_pose()
 sword.update_attachment()
 rig.skeleton.force_update_transform()
 _project_sword(sword)
 last = {"active":true,"stage":stage,"phase":value.phase,"grip_error":sword.last.get("attachment_error",0.0),
  "arm_angles":angles,"aim":aim,"baseline_torso_rotation":baseline_torso,"elbow_flex":value.elbow_flex,
  "sword_angle":value.sword_angle,"target_angle":target_angle,"sweep_age":value.sweep_age,
  "display_angle":atan2(SWEEP.VIEW_DEPTH*sin(target_angle),cos(target_angle)),"view_depth":SWEEP.VIEW_DEPTH}

func _project_sword(sword: RefCounted) -> void:
 # Rebuild the unprojected attachment first, so repeated exact pose probes
 # cannot apply depth twice. Foreshorten along the blade, retain its width.
 sword.update_attachment()
 var source: Transform2D = sword.sword_sprite.global_transform
 var grip: Vector2 = source*sword.source_grip
 var axis: Vector2 = (sword.source_tip-sword.source_grip).normalized()
 var normal := axis.orthogonal()
 var blade := source.basis_xform(axis)
 var width := source.basis_xform(normal)
 var projected := SWEEP.project_ground(blade)
 var handedness := signf(blade.orthogonal().dot(width))
 var projected_width := projected.normalized().orthogonal()*width.length()*handedness
 var x := projected*axis.x+projected_width*normal.x
 var y := projected*axis.y+projected_width*normal.y
 sword.sword_sprite.global_transform = Transform2D(x,y,grip-x*sword.source_grip.x-y*sword.source_grip.y)
 sword.last.grip_global = sword.sword_sprite.to_global(sword.source_grip)
 sword.last.tip_global = sword.sword_sprite.to_global(sword.source_tip)
