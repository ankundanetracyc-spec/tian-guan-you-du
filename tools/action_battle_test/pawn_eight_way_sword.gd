extends Node2D
## Visual-only sword choreography. Uses the archived four-stage timing reference;
## it does not spend resources, deal damage or replace turn-based combat.
const Rules = preload("sword_combo_rules.gd")
const SWORD_PATH := "res://tools/action_battle_test/art/approved_outfit_sword_20261105/sword.png"
const BLADE_LENGTH := 72.0
const SIZE_MULTIPLIER := BLADE_LENGTH / 55.0
const VIEW_DEPTH := 0.7071067811865476
const VIEW_HEIGHT := 0.7071067811865476
const INK_LIFE := 0.16
var actor: Node2D
var blade: Polygon2D
var stage := -1
var age := 0.0
var cooldown := 0.0
var connect_left := 0.0
var queued := false
var auto_combo := false
var released := false
var locked_aim := Vector2.RIGHT
var live_aim := Vector2.RIGHT
var step_spent := 0.0
var clock := 0.0
var samples: Array[Dictionary] = []
var path_by_stage: Dictionary = {}
var release_events: Array[Dictionary] = []
var stage_events: Array[int] = []
var last_tip := Vector2.ZERO
var last_grip := Vector2.ZERO
var peak_grip_error := 0.0
var pose: Dictionary = {}
var blade_axis := Vector3.RIGHT
var blade_width_axis := Vector3.UP
var blade_depth := 0.0
var blade_tip_local := Vector2.ZERO
var blade_edge: Node2D

class BladeEdge extends Node2D:
	var start := Vector2.ZERO
	var tip := Vector2.ZERO
	var width := 0.65
	func _draw() -> void:
		draw_line(start,tip,Color(0.22,0.26,0.24,0.86),width,true)

func build(value: Node2D) -> void:
	actor=value
	var source := Image.load_from_file(SWORD_PATH)
	source.generate_mipmaps()
	blade=Polygon2D.new()
	blade.name="HeldSword"
	blade.texture=ImageTexture.create_from_image(source)
	blade.antialiased=true
	blade.uv=PackedVector2Array([Vector2(0,0),Vector2(1024,0),Vector2(1024,1536),Vector2(0,1536)])
	blade.texture_filter=CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	var shader:=Shader.new()
	shader.code="shader_type canvas_item; render_mode unshaded; void fragment(){ vec4 p=texture(TEXTURE,UV); p.a=smoothstep(0.77,0.94,p.a); COLOR=p; }"
	var material:=ShaderMaterial.new()
	material.shader=shader
	blade.material=material
	actor.visual.add_child(blade)
	blade_edge=BladeEdge.new()
	blade_edge.width=0.65*SIZE_MULTIPLIER
	blade.add_child(blade_edge)
	_apply_pose()

func request_attack(single_stage: int = -1, whole_combo: bool = false) -> bool:
	if actor==null: return false
	if single_stage>=0:
		_reset_action()
		_begin(clampi(single_stage,0,3))
		return true
	if stage>=0:
		queued=true
		if whole_combo: auto_combo=true
		return true
	if cooldown>0.0: return false
	auto_combo=whole_combo
	_begin(0)
	return true

func _begin(index: int) -> void:
	stage=index
	age=0.0
	connect_left=0.0
	released=false
	step_spent=0.0
	queued=false
	locked_aim=live_aim
	stage_events.append(stage)
	_apply_pose()

func is_active() -> bool:
	return stage>=0

func stage_name() -> String:
	return str(Rules.STAGES[stage].name) if stage>=0 else ("收剑" if cooldown>0.0 else "持剑待势")

func get_facing() -> Vector2:
	return locked_aim if released and stage>=0 else live_aim

func advance(delta: float, direction: Vector2, held: bool = false) -> Vector2:
	if actor==null or delta<=0.0: return Vector2.ZERO
	clock+=delta
	live_aim=direction.normalized() if not direction.is_zero_approx() else live_aim
	for index in range(samples.size()-1,-1,-1):
		samples[index].age+=delta
		if float(samples[index].age)>INK_LIFE: samples.remove_at(index)
	cooldown=maxf(0.0,cooldown-delta)
	var push:=Vector2.ZERO
	if stage>=0:
		if held: queued=true
		var rule: Dictionary=Rules.STAGES[stage]
		var duration:=float(rule.duration)
		var old_age:=age
		age=minf(age+delta,duration)
		var contact:=duration*float(rule.hit)
		if not released:
			locked_aim=live_aim
			var cumulative:=float(rule.step)*smoothstep(0.0,contact,age)
			push=live_aim*maxf(0.0,cumulative-step_spent)
			step_spent=cumulative
			if old_age<contact and age>=contact:
				released=true
				release_events.append({"stage":stage,"clock":clock,"direction":locked_aim,"position":actor.position,"step":step_spent})
		if age>=duration:
			if stage<3 and (queued or auto_combo): _begin(stage+1)
			elif stage<3:
				connect_left+=delta
				if connect_left>=Rules.CONNECT_WINDOW: _finish()
			else: _finish()
	_apply_pose()
	queue_redraw()
	return push

func record_blade() -> void:
	if actor==null or blade==null: return
	last_grip=blade.global_position
	last_tip=blade.to_global(blade_tip_local)
	peak_grip_error=maxf(peak_grip_error,last_grip.distance_to(actor.layers.HandRight.global_position))
	if stage>=0:
		if not path_by_stage.has(stage): path_by_stage[stage]=[]
		path_by_stage[stage].append({"grip":last_grip,"tip":last_tip,"t":age/float(Rules.STAGES[stage].duration),"body_rotation":actor.visual.rotation})
		var t:=age/float(Rules.STAGES[stage].duration)
		if t>0.25 and t<0.78:
			samples.append({"grip":last_grip,"tip":last_tip,"age":0.0,"stage":stage})
			if samples.size()>40: samples.pop_front()
	queue_redraw()

func _finish() -> void:
	stage=-1
	age=0.0
	queued=false
	auto_combo=false
	released=false
	cooldown=1.0
	connect_left=0.0

func _reset_action() -> void:
	stage=-1
	age=0.0
	cooldown=0.0
	connect_left=0.0
	queued=false
	auto_combo=false
	released=false
	step_spent=0.0

func reset() -> void:
	_reset_action()
	samples.clear()
	path_by_stage.clear()
	release_events.clear()
	stage_events.clear()
	peak_grip_error=0.0
	clock=0.0
	if actor!=null: _apply_pose()
	queue_redraw()

func _keys(index: int) -> Array:
	# time, forward, anatomical right, height, blade angle, torso roll, body forward.
	match index:
		0: return [[0.0,4,16,-2,-1.08,0.02,0],[0.22,22,23,-22,-1.42,0.115,-2],[0.40,18,1,-7,-0.05,-0.045,2.7],[0.66,6,-24,6,1.58,-0.12,3.0],[1.0,2,-15,0,1.28,-0.045,0]]
		1: return [[0.0,2,-15,0,1.28,-0.045,0],[0.23,-4,-22,-3,1.93,-0.10,-1.4],[0.40,20,-1,-5,0.03,0.04,3],[0.68,6,23,-1,-1.55,0.12,3.2],[1.0,-3,16,-1,-0.35,0.025,0]]
		2: return [[0.0,-3,16,-1,-0.35,0.025,0],[0.29,-13,12,3,0.08,-0.07,-2],[0.45,28,3,-2,0.0,0.10,5.0],[0.63,31,2,-2,0.0,0.12,5.8],[1.0,0,15,0,-0.40,0.01,0]]
		_: return [[0.0,0,15,0,-0.40,0.01,0],[0.28,-28,25,-8,-2.24,0.145,-4],[0.45,23,-1,-6,0.0,-0.05,4],[0.69,9,-28,3,2.04,-0.145,4.7],[0.88,-3,-14,-1,1.10,-0.065,1.2],[1.0,3,16,-2,-0.85,0.0,0]]

func _sample(index: int,t: float) -> Array[float]:
	var keys:=_keys(index)
	var segment:=0
	while segment<keys.size()-2 and t>float(keys[segment+1][0]): segment+=1
	var first: Array=keys[segment]
	var second: Array=keys[segment+1]
	var u:=clampf((t-float(first[0]))/(float(second[0])-float(first[0])),0.0,1.0)
	var values: Array[float]=[]
	for k in range(1,7):
		var previous: Array=keys[maxi(0,segment-1)]
		var following: Array=keys[mini(keys.size()-1,segment+2)]
		var m0: float=(float(second[k])-float(previous[k]))/maxf(0.001,float(second[0])-float(previous[0]))
		var m1: float=(float(following[k])-float(first[k]))/maxf(0.001,float(following[0])-float(first[0]))
		var span:=float(second[0])-float(first[0])
		values.append((2*u*u*u-3*u*u+1)*float(first[k])+(u*u*u-2*u*u+u)*m0*span+(-2*u*u*u+3*u*u)*float(second[k])+(u*u*u-u*u)*m1*span)
	return values

func _apply_pose() -> void:
	if actor==null or blade==null: return
	var defaults: Dictionary=actor.get_default_hand_pose()
	var right: Vector2=defaults.get("right_hand",defaults.get("right",Vector2(-24,-28)))
	var left: Vector2=defaults.get("left_hand",defaults.get("left",Vector2(24,-28)))
	var d:=get_facing()
	var side:=Vector2(-d.y,d.x*0.42*(VIEW_DEPTH/0.62))
	var forward:=Vector2(d.x,d.y*VIEW_DEPTH)
	var sword_angle:=d.angle()-0.85
	var roll:=0.0
	var offset:=Vector2.ZERO
	var head_roll:=0.0
	var t:=0.0
	if stage>=0:
		t=age/float(Rules.STAGES[stage].duration)
		var p:=_sample(stage,t)
		right=Vector2(0,-32)+forward*p[0]+side*p[1]+Vector2(0,p[2])
		left=Vector2(0,-31)-side*(16.0+7.0*sin(t*PI))-forward*(5.0+8.0*sin(t*PI))+Vector2(0,-4.0*sin(t*PI))
		sword_angle=d.angle()+p[3]
		var sign_x:=1.0 if d.x>=0.0 else -1.0
		roll=p[4]*sign_x
		offset=forward*p[5]+Vector2(0,1.9*sin(t*PI))
		head_roll=-roll*0.42
		pose={"right_hand":right,"left_hand":left,"torso_rotation":roll,"torso_offset":offset,"head_rotation":head_roll,"head_offset":-forward*p[5]*0.22}
	else:
		right+=forward*5.0+Vector2(0,-4.0)
		pose={"right_hand":right,"left_hand":left,"torso_rotation":0.0,"torso_offset":Vector2.ZERO,"head_rotation":0.0}
	_project_blade(sword_angle,t)
	var depth_z:=80 if blade_depth>=-0.12 else 13
	pose["right_hand_z"]=depth_z
	actor.set_sword_pose(pose)
	var anchor: Dictionary=actor.get_sword_anchor()
	blade.position=anchor.position
	blade.z_index=int(anchor.z)-1

func _project(point: Vector3) -> Vector2:
	# Fixed orthographic view. Z is height; Y is depth across the battlefield.
	return Vector2(point.x,point.y*VIEW_DEPTH-point.z*VIEW_HEIGHT)

func _project_blade(angle: float,t: float) -> void:
	var pitch:=0.24
	var roll:=0.12
	if stage>=0:
		match stage:
			0:
				pitch=lerpf(0.95,-0.38,smoothstep(0.20,0.70,t))
				roll=lerpf(-0.45,0.42,t)
			1:
				pitch=0.10+0.15*sin(t*PI)
				roll=lerpf(0.55,-0.65,t)
			2:
				pitch=0.11-0.10*sin(t*PI)
				roll=0.20+0.30*sin(t*PI)
			3:
				pitch=0.17+0.18*sin(t*PI*2.0)
				roll=lerpf(-0.70,0.60,t)
	blade_axis=Vector3(cos(angle)*cos(pitch),sin(angle)*cos(pitch),sin(pitch)).normalized()
	var width:=Vector3(-sin(angle),cos(angle),0.0)
	blade_width_axis=width.rotated(blade_axis,roll).normalized()
	blade_depth=blade_axis.y
	var vertices:=PackedVector2Array()
	var scale_value:=BLADE_LENGTH/1253.0
	for point in [Vector2(0,0),Vector2(1024,0),Vector2(1024,1536),Vector2(0,1536)]:
		var offset_3d: Vector3=blade_width_axis*(point.x-512.0)*scale_value*1.5+blade_axis*(1303.0-point.y)*scale_value
		vertices.append(_project(offset_3d))
	blade.polygon=vertices
	blade_tip_local=_project(blade_axis*BLADE_LENGTH)
	if blade_edge!=null:
		blade_edge.start=_project((blade_axis*8.0+blade_width_axis*1.35)*SIZE_MULTIPLIER)
		blade_edge.tip=blade_tip_local
		blade_edge.queue_redraw()

func refresh_pose() -> void:
	_apply_pose()

func _draw() -> void:
	# The ribbon consists solely of positions the actual physical blade swept.
	# Old samples stay in world space and dry out individually.
	for index in range(1,samples.size()):
		var a: Dictionary=samples[index-1]
		var b: Dictionary=samples[index]
		if int(a.stage)!=int(b.stage): continue
		var opacity:=pow(maxf(0.0,1.0-float(b.age)/INK_LIFE),1.7)
		var tip_a:=to_local(a.tip)
		var tip_b:=to_local(b.tip)
		var inner_a:=to_local((a.grip as Vector2).lerp(a.tip,0.85))
		var inner_b:=to_local((b.grip as Vector2).lerp(b.tip,0.85))
		# A reversing blade can make a four-corner strip self-intersect. Keep its
		# two physical swept triangles separate, and omit zero-area samples.
		for triangle in [[tip_a,tip_b,inner_b],[tip_a,inner_b,inner_a]]:
			var edge_a: Vector2=triangle[1]-triangle[0]
			var edge_b: Vector2=triangle[2]-triangle[0]
			if absf(edge_a.cross(edge_b))>0.015:
				draw_colored_polygon(PackedVector2Array(triangle),Color(0.42,0.48,0.44,opacity*0.075))
		draw_line(tip_a,tip_b,Color(0.74,0.78,0.73,opacity*0.52),1.8,true)
		if index%3!=0: draw_line(tip_a.lerp(inner_a,0.10),tip_b.lerp(inner_b,0.12),Color(0.32,0.39,0.36,opacity*0.23),0.7,true)

func get_diagnostics() -> Dictionary:
	return {"stage":stage,"stage_name":stage_name(),"stage_age":age,"released":released,"aim":get_facing(),"tip":last_tip,"grip":last_grip,"maximum_grip_error":peak_grip_error,"pose":pose,"step_spent":step_spent,"cooldown":cooldown,"sample_count":samples.size(),"stage_events":stage_events.duplicate(),"release_events":release_events.duplicate(true),"path_by_stage":path_by_stage.duplicate(true),"blade_axis_3d":blade_axis,"blade_width_axis_3d":blade_width_axis,"blade_depth":blade_depth,"projected_blade_length":blade_tip_local.length(),"physical_blade_length":BLADE_LENGTH,"view_depth":VIEW_DEPTH,"view_height":VIEW_HEIGHT,"view_elevation_degrees":45.0,"fixed_sword_size_multiplier":SIZE_MULTIPLIER}
