extends Node2D

## Restrained ink-force samples beside the separate round-hand pawn. No combat or audio.
const Shapes = preload("pawn_paper_vfx_shapes.gd")
const PAPER = preload("pawn_paper_wash.gdshader")
const KINDS := ["fist","heavy","guard","blood","qi","wave"]
const LENGTHS := {"fist":0.52,"heavy":0.64,"guard":0.72,"blood":0.55,"qi":1.35,"wave":1.05}
const INK := Color("66584d")
const CREAM := Color("eee3ca")
const BLUE := Color("929f9c")
const GOLD := Color("b8a477")
const RED := Color("a95749")
const DARK_RED := Color("703f38")
const MAX_EFFECTS := 18
var effects: Array[Dictionary] = []
var released := 0
var pigment: ShaderMaterial
var follow_actor: Node2D
var _layer: Node2D
var _is_front := false
var _angle := 0.0
var _height := 0.0
var _offset := Vector2.ZERO

func _ready() -> void:
	pigment = ShaderMaterial.new()
	pigment.shader = PAPER

func bind_actor(actor: Node2D) -> void:
	follow_actor=actor

func actor_height() -> float:
	if follow_actor!=null and follow_actor.get("layers") is Dictionary and follow_actor.layers.has("HandRight"):
		return maxf(0.0,(follow_actor.global_position.y-follow_actor.layers.HandRight.global_position.y)/follow_actor.global_scale.y)
	return 23.52

func play(kind: String, direction := Vector2.RIGHT) -> void:
	if kind not in KINDS: return
	if direction.is_zero_approx(): direction=Vector2.RIGHT
	if effects.size()>=MAX_EFFECTS: _dispose(effects.pop_front())
	var hand := Vector2(28,-actor_height())
	if follow_actor!=null and follow_actor.get("layers") is Dictionary:
		hand=(follow_actor.layers.HandRight.global_position-follow_actor.global_position)/follow_actor.global_scale
	var effect: Dictionary={"kind":kind,"direction":direction.normalized(),"age":0.0,
		"height":actor_height(),"hand":hand,"duration":float(LENGTHS[kind])}
	var effect_material:=pigment.duplicate() as ShaderMaterial
	effect["material"]=effect_material
	for is_front in [false,true]:
		var layer:=Node2D.new()
		layer.material=effect_material
		layer.draw.connect(_draw_layer.bind(layer,is_front,effect))
		add_child(layer)
		effect["front" if is_front else "rear"]=layer
	_update_anchor(effect)
	effects.append(effect)
	released+=1
	_redraw()

func advance(delta: float, paused := false) -> void:
	for effect in effects:
		if str(effect.kind) in ["qi","guard"]: _update_anchor(effect)
	if not paused:
		for i in range(effects.size()-1,-1,-1):
			effects[i].age=float(effects[i].age)+maxf(delta,0.0)
			effects[i].material.set_shader_parameter("ink_age",effects[i].age)
			if float(effects[i].age)>=float(effects[i].duration):
				_dispose(effects[i])
				effects.remove_at(i)
	_redraw()

func clear() -> void:
	for effect in effects: _dispose(effect)
	effects.clear()
	_redraw()

func _redraw() -> void:
	for effect in effects:
		effect.rear.queue_redraw()
		effect.front.queue_redraw()

func _dispose(effect: Dictionary) -> void:
	for key in ["rear","front"]:
		var layer: Node2D=effect[key]
		layer.draw.disconnect(_draw_layer.bind(layer,key=="front",effect))
		layer.queue_free()
	effect.erase("rear")
	effect.erase("front")

func _update_anchor(effect: Dictionary) -> void:
	if follow_actor==null or not is_instance_valid(follow_actor): return
	for is_front in [false,true]:
		var layer: Node2D=effect["front" if is_front else "rear"]
		layer.global_position=follow_actor.global_position
		layer.global_scale=follow_actor.global_scale
		layer.global_rotation=0.0
		layer.z_as_relative=false
		layer.z_index=clampi(follow_actor.z_index+(90 if is_front else 5),-4096,4095)
	if str(effect.kind) in ["qi","guard"]:
		effect.height=actor_height()
		effect.hand=(follow_actor.layers.HandRight.global_position-follow_actor.global_position)/follow_actor.global_scale

func _rotated(points: PackedVector2Array) -> PackedVector2Array:
	var result := PackedVector2Array()
	for point in points: result.append(point.rotated(_angle)+_offset)
	return result

func _shape(points: PackedVector2Array, fill: Color, _edge: float=0.0) -> void:
	var world := _rotated(points)
	# Absorbed ink edges use translucent nested washes, without an illustrative outline.
	for band in range(3):
		var outlines: Array=[world] if band==0 else Geometry2D.offset_polygon(world,-0.45*band)
		for outline in outlines:
			var clipped := Shapes.clip_depth(outline,_is_front)
			if clipped.size()>=3 and Geometry2D.triangulate_polygon(clipped).size()>0:
				_layer.draw_colored_polygon(Shapes.project_outline(clipped,0.0,Vector2.ZERO,_height),Color(fill,fill.a*[0.24,0.48,0.72][band]))

func _stroke(path: PackedVector2Array, width: float, tint: Color, edge:=0.0) -> void:
	_shape(Shapes.stroke_strip(path,width,0.04,0.02,1.0,false),tint,edge)

func _draw_layer(layer: Node2D, is_front: bool, effect: Dictionary) -> void:
	_layer=layer
	_is_front=is_front
	_angle=Vector2(effect.direction).angle()
	_height=float(effect.height)
	_offset=Vector2.ZERO
	var progress:=float(effect.age)/float(effect.duration)
	var fade:=1.0-smoothstep(0.48,1.0,progress)
	var entrance:=smoothstep(0.0,0.06,float(effect.age))
	var alpha:=fade*entrance
	match str(effect.kind):
		"fist","heavy","blood": _draw_strike(effect,alpha)
		"guard": _draw_guard(effect,alpha)
		"qi": _draw_qi(effect,alpha)
		"wave": _draw_wave(effect,alpha)

func _draw_strike(effect: Dictionary, alpha: float) -> void:
	var heavy:=str(effect.kind)=="heavy"
	var blood:=str(effect.kind)=="blood"
	var t:=clampf(float(effect.age)/0.20,0.0,1.0)
	var travel:=lerpf(17.0,57.0,1.0-pow(1.0-t,2.5))
	var bulk:=1.23 if heavy else 1.0
	var tint:=RED if blood else (GOLD if heavy else BLUE)
	var path:=Shapes.cubic_bezier(Vector2(10,4),Vector2(27,-14*bulk),Vector2(travel-3,-10*bulk),Vector2(travel+12,0))
	_stroke(path,24.0*bulk,Color(tint,alpha*0.42))
	_stroke(Shapes.cubic_bezier(Vector2(17,2),Vector2(35,-10*bulk),Vector2(travel+1,-6*bulk),Vector2(travel+10,0)),13.0*bulk,Color(CREAM.lerp(tint,0.62 if blood else 0.30),alpha*0.72))
	var pressure:=Shapes.cubic_bezier(Vector2(travel-4,-23*bulk),Vector2(travel+24,-10*bulk),Vector2(travel+9,12*bulk),Vector2(travel-10,17*bulk))
	_stroke(pressure,16.0*bulk,Color(tint,alpha*0.68))
	_stroke(Shapes.cubic_bezier(Vector2(travel-2,-17*bulk),Vector2(travel+13,-5*bulk),Vector2(travel+8,7*bulk),Vector2(travel-5,13*bulk)),5.8*bulk,Color(Color("d2a18c") if blood else CREAM,alpha*0.72))
	_stroke(Shapes.quadratic_bezier(Vector2(9,10),Vector2(travel*0.64,16),Vector2(travel-1,4)),7.5*bulk,Color(tint,alpha*0.17))

func _draw_guard(effect: Dictionary, alpha: float) -> void:
	var rise:=smoothstep(0.0,0.14,float(effect.age))
	var radius:=lerpf(24.0,43.0,rise)
	var ratio:=radius/43.0
	var brace:=Shapes.cubic_bezier(Vector2(14,-37)*ratio,Vector2(49,-29)*ratio,Vector2(54,8)*ratio,Vector2(18,35)*ratio)
	_stroke(brace,13.0,Color(BLUE,alpha*0.25))
	_stroke(brace,7.7,Color(GOLD,alpha*0.65))
	_stroke(Shapes.quadratic_bezier(Vector2(30,-27)*ratio,Vector2(49,-16)*ratio,Vector2(41,10)*ratio),3.0,Color(CREAM,alpha*0.88))

func _draw_qi(effect: Dictionary, alpha: float) -> void:
	var hand: Vector2=effect.hand
	_angle=0.0
	# Hand-local gathering is upright. It follows the actual detached round hand.
	_height=-hand.y
	_offset=Vector2(hand.x,0)
	var pulse:=0.94+0.06*sin(float(effect.age)*5.0)
	_shape(Shapes.cloud_outline(Vector2(0,0),Vector2(13,22)*pulse,3,0.025,0.5),Color(BLUE,alpha*0.36),0.0)
	_shape(Shapes.cloud_outline(Vector2(1,-2),Vector2(10,18)*pulse,3,0.02,0.3),Color("f6f1df",alpha*0.68),0.0)
	_stroke(Shapes.cubic_bezier(Vector2(-18,9),Vector2(-24,-10),Vector2(-6,-31),Vector2(7,-10)),8.5,Color(BLUE,alpha*0.35))
	_stroke(Shapes.cubic_bezier(Vector2(-8,-17),Vector2(17,-13),Vector2(15,9),Vector2(-4,12)),6.5,Color("f6f1df",alpha*0.68))

func _draw_wave(effect: Dictionary, alpha: float) -> void:
	var t:=clampf(float(effect.age)/0.74,0.0,1.0)
	var radius:=lerpf(19.0,94.0,t)
	# Each full fan is split at world depth before projection, including its rim.
	var fan:=PackedVector2Array([Vector2(5,0)])
	for i in range(41): fan.append(Vector2.from_angle(lerpf(-PI/3.0,PI/3.0,i/40.0))*(radius-2.0))
	_shape(fan,Color(RED,alpha*0.13),0.0)
	_shape(Shapes.tapered_wave_front(radius*0.75,12.0,deg_to_rad(120)),Color(RED,alpha*0.16),0.0)
	_shape(Shapes.tapered_wave_front(radius+1,17.5,deg_to_rad(120)),Color(DARK_RED,alpha*0.43),0.0)
	_shape(Shapes.tapered_wave_front(radius,11.5,deg_to_rad(118),56,0.8),Color(RED,alpha*0.68),0.0)
	_stroke(Shapes.quadratic_bezier(Vector2.from_angle(-0.65)*(radius-1),Vector2.from_angle(-0.05)*(radius+2),Vector2.from_angle(0.67)*(radius-4)),3.2,Color("d2a18c",alpha*0.62))

func diagnostics() -> Dictionary:
	return {"active":effects.size(),"released":released,"projection":Shapes.VIEW_DEPTH,
		"hand_height":actor_height(),"audio":false,"applies_damage":false}

func _exit_tree() -> void:
	clear()
