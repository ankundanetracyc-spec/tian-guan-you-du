extends "pawn_four_way_avatar.gd"

# Exactly two body/head frames; front and rear hairstyle slots stay independent.
const STEP_CYCLE_DISTANCE := 150.0
const FOOT_TRAVEL := 4.0
const FOOT_LIFT := 2.0
var desired_look := Vector2.DOWN
var gaze := Vector2.ZERO
var feet: Node2D
var swing_left := 0.0
var last_velocity := Vector2.ZERO
var gait_weight := 0.0
var gait_direction := Vector2.ZERO
var motion_lag := Vector2.ZERO
var last_acceleration := Vector2.ZERO
var travelled_distance := 0.0

class RoundFeet extends Node2D:
	var phase := 0.0
	var velocity := Vector2.ZERO
	var centres: Array[Vector2] = [Vector2(-8.0,-5.8),Vector2(8.0,-5.8)]
	var ground_centres: Array[Vector2] = [Vector2(-8.0,1.0),Vector2(8.0,1.0)]
	var trouser := Color("4e5252")
	var shoe := Color("687e83")
	func _draw() -> void:
		for index in range(2):
			var centre := centres[index]
			draw_set_transform(ground_centres[index],0,Vector2(1,0.28))
			draw_circle(Vector2.ZERO,6.3,Color(0.17,0.22,0.19,0.16),true,-1,true)
			draw_set_transform(Vector2.ZERO)
			for half in range(2):
				var polygon:=PackedVector2Array([centre])
				for point in range(25):
					var angle:=float(half)*PI+float(point)/24.0*PI
					polygon.append(centre+Vector2(cos(angle),sin(angle))*5.8)
				draw_colored_polygon(polygon,shoe if half==0 else trouser)
			draw_arc(centre,5.8,0,TAU,40,Color("303a35"),0.8,true)
			draw_line(centre-Vector2(5.1,0),centre+Vector2(5.1,0),Color("3e4c49"),0.5,true)
	func get_pose() -> Dictionary:
		return {"centres":centres.duplicate(),"ground_centres":ground_centres.duplicate(),"phase":phase,"velocity":velocity}

func _load_sheet(key: String, file: String) -> bool:
	if key=="base": return super._load_sheet(key,file)
	var path := _folder().path_join("art/pawn_two_view_20261105/black_hair.png")
	if not _shared_assets.has(path):
		var source := Image.load_from_file(path)
		if source==null: errors.append("Missing black hair atlas"); return false
		var width := source.get_width()/3
		var pieces := []
		for index in range(3):
			var region := Rect2i(index*width,0,width,source.get_height())
			pieces.append({"region":region,"bounds":_bounds(source,region)})
		source.generate_mipmaps()
		_shared_assets[path]={"texture":ImageTexture.create_from_image(source),"size":source.get_size(),"cell":Vector2i(width,source.get_height()),"pieces":pieces,"path":path,"sha256":FileAccess.get_sha256(path)}
	var record: Dictionary = _shared_assets[path].duplicate()
	var columns := []
	for col in range(4): columns.append(record.pieces[0 if key=="front" else (2 if col==2 else 1)])
	# This MVP has one entry per independent hairstyle slot, not pair-specific art.
	record.regions=[columns,columns,columns]
	assets[key]=record
	return true

func _hair_registration(part: String, _style: int, col: int) -> Dictionary:
	var bounds: Rect2 = assets[part].regions[0][col].bounds
	var height := 67.0 if part=="front" else 82.0
	var fraction := 0.76 if part=="front" else 0.69
	return {"anchor":Vector2(bounds.get_center().x,bounds.position.y+bounds.size.y*fraction),"offset":Vector2.ZERO,"scale":height/bounds.size.y}

func build() -> bool:
	if not super.build(): return false
	if feet==null:
		feet=RoundFeet.new()
		feet.name="RoundFeet"
		feet.z_index=15
		add_child(feet)
	_apply_motion()
	return true

func set_appearance(value: Dictionary) -> void:
	super.set_appearance(value)
	supported=supported and front_style==0 and back_style==0
	if loaded: visual.visible=supported

func set_direction(value: Vector2) -> void:
	if value.is_zero_approx(): return
	desired_look=value.normalized()
	var next := direction
	if desired_look.y< -0.35: next="back"
	elif desired_look.y> -0.10: next="front"
	if next!=direction:
		direction=next
		_render()
	_apply_motion()

func advance(delta: float, velocity: Vector2) -> void:
	if not loaded or delta<=0.0: return
	var previous_velocity := last_velocity
	last_velocity=velocity
	gaze=gaze.lerp(desired_look,1.0-exp(-delta*14.0))
	swing_left=maxf(0.0,swing_left-delta)
	clock += delta
	moving = velocity.length_squared()>0.01
	var distance := velocity.length()*delta
	travelled_distance += distance
	phase += distance/STEP_CYCLE_DISTANCE*TAU
	var target_weight := smoothstep(0.0,95.0,velocity.length())
	gait_weight = move_toward(gait_weight,target_weight,delta/(0.075 if target_weight>gait_weight else 0.10))
	if moving: gait_direction = gait_direction.lerp(velocity.normalized(),1.0-exp(-delta*22.0))
	elif is_zero_approx(gait_weight): gait_direction = Vector2.ZERO
	last_acceleration = (velocity-previous_velocity)/delta
	var target_lag := (-last_acceleration/1800.0*0.8).limit_length(0.8)
	motion_lag = motion_lag.lerp(target_lag,1.0-exp(-delta*14.0))
	_update_feet()
	_apply_motion()
	queue_redraw()

func _update_feet() -> void:
	if feet==null: return
	feet.phase = phase
	feet.velocity = last_velocity
	for index in range(2):
		var side := -1.0 if index==0 else 1.0
		var stride := sin(phase+float(index)*PI)
		var travel := gait_direction*stride*FOOT_TRAVEL*gait_weight
		var lift := maxf(0.0,stride)*FOOT_LIFT*gait_weight
		feet.centres[index] = Vector2(side*8.0,-5.8)+travel-Vector2(0,lift)
		feet.ground_centres[index] = Vector2(side*8.0,1.0)+travel
	feet.queue_redraw()

func reset_motion() -> void:
	last_velocity = Vector2.ZERO
	last_acceleration = Vector2.ZERO
	gait_weight = 0.0
	gait_direction = Vector2.ZERO
	motion_lag = Vector2.ZERO
	phase = 0.0
	travelled_distance = 0.0
	moving = false
	swing_left = 0.0
	_update_feet()
	_apply_motion()
	queue_redraw()

func strike() -> bool:
	if swing_left>0.0: return false
	swing_left=0.32
	return true

func _apply_motion() -> void:
	super._apply_motion()
	if not loaded: return
	# Ground origin is the feet contact plane. The robe ends above the circles.
	visual.position.y=-11.8+(0.28-absf(sin(phase))*0.65)*gait_weight
	visual.rotation=0.0
	if feet!=null:
		feet.queue_redraw()
	var shift := Vector2(gaze.x*1.6,0)
	for part in ["Head","HairFront","HairBack"]:
		if render_registration.has(part):
			layers[part].position=render_registration[part].at+shift+(motion_lag if part!="Head" else Vector2.ZERO)
			layers[part].rotation=gaze.x*0.025
	for item in _hand_placements():
		layers[item.name].position = item.at+Vector2(0,sin(phase)*item.phase_sign*0.45*gait_weight)+motion_lag*0.44
	if swing_left>0.0:
		var envelope := sin((1.0-swing_left/0.32)*PI)
		var hand: Sprite2D = layers.HandRight if desired_look.x>=0 else layers.HandLeft
		hand.position+=desired_look*envelope*15.0

func _hand_placements() -> Array[Dictionary]:
	# Use the waist width, rather than the wider hem, to seat the round hands.
	var x := body_width*0.39+HAND_DIAMETER*0.5+1.2
	return [
		{"name":"HandLeft","at":Vector2(-x,-BODY_HEIGHT*0.49),"z":80,"phase_sign":-1.0},
		{"name":"HandRight","at":Vector2(x,-BODY_HEIGHT*0.49),"z":80,"phase_sign":1.0},
	]

func get_diagnostics() -> Dictionary:
	var result := super.get_diagnostics()
	result["view_count"]=2
	result["look_direction"]=desired_look
	result["gaze"]=gaze
	result["eyes_visible"]=false
	result["front_options"]=1
	result["rear_options"]=1
	result["round_feet"]=feet!=null
	result["foot_radius"]=5.8
	result["body_rotation"]=visual.rotation
	result["step_cycle_distance"]=STEP_CYCLE_DISTANCE
	result["gait_weight"]=gait_weight
	result["gait_direction"]=gait_direction
	result["actual_velocity"]=last_velocity
	result["actual_acceleration"]=last_acceleration
	result["travelled_distance"]=travelled_distance
	result["motion_lag"]=motion_lag
	result["feet_pose"]=feet.get_pose() if feet!=null else {}
	result["scope"]="two-view first outfit playground; independent front/rear slots, no hairstyle-pair atlas"
	return result
