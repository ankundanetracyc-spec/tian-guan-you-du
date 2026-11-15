extends "pawn_two_view_playground.gd"

const EightAvatar = preload("pawn_eight_way_avatar.gd")
const Sword = preload("pawn_eight_way_sword.gd")
var sword: Node2D
var automatic := false
var slow_motion := false
var stage_label: Label
var held_attack := false

func _ready() -> void:
	get_window().title="水墨八向 · 持剑试演"
	get_window().size=Vector2i(1280,800)
	get_window().content_scale_size=Vector2i(1280,800)
	get_window().content_scale_mode=Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect=Window.CONTENT_SCALE_ASPECT_KEEP
	font.font_names=PackedStringArray(["Microsoft YaHei"])
	var background:=Sprite2D.new()
	background.texture=ImageTexture.create_from_image(Image.load_from_file(FIELD))
	background.centered=false
	background.scale=Vector2(WORLD_SIZE.x/background.texture.get_width(),WORLD_SIZE.y/background.texture.get_height())
	background.z_index=-1000
	add_child(background)
	camera=Camera2D.new()
	camera.position=SPAWN
	camera.zoom=Vector2.ONE*CAMERA_ZOOM
	camera.limit_right=int(WORLD_SIZE.x)
	camera.limit_bottom=int(WORLD_SIZE.y)
	add_child(camera)
	player=EightAvatar.new()
	player.name="EightWayPawn"
	add_child(player)
	if not player.build():
		push_error("Eight-way actor could not load its atlas")
		return
	player.position=SPAWN
	player.scale=Vector2.ONE*1.65
	_update_camera(0.0,true)
	sword=Sword.new()
	sword.name="SwordChoreography"
	add_child(sword)
	sword.build(player)
	for at in [SPAWN+Vector2(160,-40),SPAWN+Vector2(-225,140),Vector2(1830,1140),Vector2(520,1270)]:
		var post:=PracticePost.new()
		add_child(post)
		post.position=at
		post.z_index=int(at.y)
		posts.append(post)
	_make_ui()
	for arg in OS.get_cmdline_user_args():
		if arg=="--capture":
			capture_active=true
			get_window().position=Vector2i(-7000,-7000)
			get_window().unfocusable=true
			call_deferred("_capture_demo")

func _make_ui() -> void:
	var ui:=CanvasLayer.new()
	add_child(ui)
	var bar:=ColorRect.new()
	bar.color=Color(0.95,0.93,0.87,0.94)
	bar.position=Vector2(22,20)
	bar.size=Vector2(1236,90)
	ui.add_child(bar)
	for data in [["八向持剑 · 剑式试演",Vector2(40,29),24],["WASD 移动 · 鼠标瞄准 · 左键连招 · 1—4 单段",Vector2(490,29),17],["Tab 连续演示 · 空格 慢放 · R 归位 · Esc 暂停",Vector2(490,64),17]]:
		var label:=Label.new()
		label.text=data[0]
		label.position=data[1]
		label.add_theme_font_override("font",font)
		label.add_theme_font_size_override("font_size",data[2])
		label.add_theme_color_override("font_color",Color("3b4b42"))
		ui.add_child(label)
	stage_label=Label.new()
	stage_label.position=Vector2(40,68)
	stage_label.add_theme_font_override("font",font)
	stage_label.add_theme_font_size_override("font_size",20)
	stage_label.add_theme_color_override("font_color",Color("5b685c"))
	ui.add_child(stage_label)
	status=Label.new()
	status.position=Vector2(40,748)
	status.add_theme_font_override("font",font)
	status.add_theme_font_size_override("font_size",18)
	status.add_theme_color_override("font_color",Color("3b4b42"))
	ui.add_child(status)
	_update_status()

func _update_status() -> void:
	if status==null: return
	status.text=("已暂停" if paused else ("慢放 ×0.4" if slow_motion else "正常速度"))+"  ·  "+("连续演示" if automatic else "自由试剑")+"   斜斩 → 回斩 → 直刺 → 横扫"
	if stage_label!=null and sword!=null:
		stage_label.text=sword.stage_name()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if paused and event.physical_keycode not in [KEY_ESCAPE,KEY_R]: return
		match event.physical_keycode:
			KEY_ESCAPE: paused=not paused
			KEY_SPACE: slow_motion=not slow_motion
			KEY_TAB:
				automatic=not automatic
				if automatic: sword.request_attack(-1,true)
			KEY_R:
				_reset_motion(SPAWN)
				sword.reset()
				automatic=false
			KEY_1,KEY_2,KEY_3,KEY_4:
				automatic=false
				sword.request_attack(event.physical_keycode-KEY_1)
		_update_status()
	if event is InputEventMouseButton and event.button_index==MOUSE_BUTTON_LEFT and event.pressed and not paused:
		automatic=false
		sword.request_attack()

func _process(delta: float) -> void:
	if paused or capture_active or sword==null: return
	var velocity:=Vector2(float(Input.is_physical_key_pressed(KEY_D))-float(Input.is_physical_key_pressed(KEY_A)),float(Input.is_physical_key_pressed(KEY_S))-float(Input.is_physical_key_pressed(KEY_W))).normalized()*MAX_SPEED
	if Rect2(0,115,1280,615).has_point(get_viewport().get_mouse_position()):
		aim=(get_global_mouse_position()-(player.position+Vector2(0,-80))).normalized()
	held_attack=Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT)
	if automatic and not sword.is_active() and sword.cooldown<=0.0: sword.request_attack(-1,true)
	_step(delta*(0.4 if slow_motion else 1.0),velocity)

func _step(delta: float, velocity: Vector2) -> void:
	if paused or delta<=0.0 or sword==null: return
	var dt:=minf(delta,0.05)
	var push: Vector2=sword.advance(dt,aim,held_attack or automatic)
	var target:=velocity.limit_length(MAX_SPEED)
	turn_boost_left=maxf(0.0,turn_boost_left-dt)
	if target.is_zero_approx(): turn_boost_left=0.0
	elif motion_velocity.dot(target)<0.0: turn_boost_left=0.18
	var rate:=BRAKING if target.is_zero_approx() else (TURN_ACCELERATION if turn_boost_left>0.0 else ACCELERATION)
	motion_velocity=motion_velocity.move_toward(target,rate*dt)
	var before: Vector2=player.position
	var requested_position:=player.position+motion_velocity*dt+push
	player.position=requested_position.clamp(AREA.position,AREA.end)
	actual_velocity=(player.position-before)/dt
	if not is_equal_approx(player.position.x,requested_position.x): motion_velocity.x=0.0
	if not is_equal_approx(player.position.y,requested_position.y): motion_velocity.y=0.0
	player.z_index=int(player.position.y)
	player.set_direction(sword.get_facing())
	player.advance(dt,actual_velocity)
	sword.refresh_pose()
	sword.record_blade()
	sword.z_index=int(player.position.y)+1
	_update_camera(dt)
	for post in posts: post.tick(dt)
	_update_status()
	queue_redraw()

func _draw() -> void:
	if player==null or sword==null: return
	var origin: Vector2=player.position+Vector2(0,-45)
	draw_arc(origin+aim*100.0,4,0,TAU,24,Color(0.3,0.4,0.34,0.3),1.2,true)

func _reset_all(at: Vector2 = SPAWN) -> void:
	_reset_motion(at)
	sword.reset()
	automatic=false
	held_attack=false
	paused=false
	slow_motion=false

func _validate_eight_way() -> Dictionary:
	var checks:=[]
	var directions: Array[Vector2]=[Vector2.DOWN,Vector2(1,1).normalized(),Vector2.RIGHT,Vector2(1,-1).normalized(),Vector2.UP,Vector2(-1,-1).normalized(),Vector2.LEFT,Vector2(-1,1).normalized()]
	var views:=[]
	for index in range(8):
		_reset_all()
		aim=directions[index]
		_step(1.0/30.0,Vector2.ZERO)
		views.append(player.get_diagnostics())
	checks.append({"eight_actual_direction_frames":views.map(func(v):return v.direction_index)==[0,1,2,3,4,5,6,7]})
	_reset_all()
	aim=Vector2.RIGHT
	sword.request_attack(-1,true)
	var moving_feet:=false
	var peak_roll:=0.0
	var total_push:=0.0
	var projected_min:=INF
	var projected_max:=0.0
	var near_far: Array[int]=[]
	var axes_are_rigid:=true
	for frame in range(140):
		var before_spent: float=sword.step_spent
		_step(1.0/60.0,Vector2.RIGHT*80.0)
		moving_feet=moving_feet or (sword.is_active() and player.moving and player.gait_weight>0.2)
		peak_roll=maxf(peak_roll,absf(player.visual.rotation))
		projected_min=minf(projected_min,sword.blade_tip_local.length())
		projected_max=maxf(projected_max,sword.blade_tip_local.length())
		if not near_far.has(sword.blade.z_index): near_far.append(sword.blade.z_index)
		axes_are_rigid=axes_are_rigid and absf(sword.blade_axis.length()-1.0)<0.00001 and absf(sword.blade_axis.dot(sword.blade_width_axis))<0.00001
		total_push+=maxf(0.0,sword.step_spent-before_spent)
	var paths: Dictionary=sword.path_by_stage
	var path_metrics:=[]
	var four_paths:=paths.size()==4
	for index in range(4):
		var samples: Array=paths.get(index,[])
		var minimum:=Vector2(INF,INF)
		var maximum:=Vector2(-INF,-INF)
		for sample in samples:
			minimum=minimum.min(sample.tip)
			maximum=maximum.max(sample.tip)
		path_metrics.append({"stage":index,"samples":samples.size(),"span":maximum-minimum})
		four_paths=four_paths and samples.size()>10 and (maximum-minimum).length()>20.0
	checks.append({"four_distinct_complete_sword_paths":four_paths,"paths":path_metrics})
	checks.append({"physical_sword_stays_attached_to_right_hand":sword.peak_grip_error<0.001,"maximum_world_grip_error":sword.peak_grip_error})
	checks.append({"walking_continues_while_attacking":moving_feet})
	checks.append({"attack_body_tension_is_visible":peak_roll>0.10 and peak_roll<0.22,"maximum_body_rotation_radians":peak_roll})
	checks.append({"rigid_3d_blade_projects_and_changes_occlusion":axes_are_rigid and projected_max-projected_min>5.0 and near_far.size()>1,"physical_length":Sword.BLADE_LENGTH,"projected_min":projected_min,"projected_max":projected_max,"sword_depth_layers":near_far})
	checks.append({"planned_four_stages_and_step_budget":sword.stage_events==[0,1,2,3] and sword.release_events.size()==4 and absf(total_push-20.0)<0.1,"stage_events":sword.stage_events,"push_budget":total_push})
	var state: Dictionary=sword.get_diagnostics()
	var position_before: Vector2=player.position
	paused=true
	_step(0.05,Vector2.LEFT*MAX_SPEED)
	checks.append({"pause_freezes_sword_and_actor":state==sword.get_diagnostics() and player.position==position_before})
	_reset_all()
	checks.append({"reset_clears_combo_and_trails":not sword.is_active() and sword.samples.is_empty() and player.position==SPAWN})
	for frame in range(20): _step(1.0/60.0,Vector2.RIGHT*MAX_SPEED)
	checks.append({"normal_walking_has_no_torso_sway":is_zero_approx(player.visual.rotation)})
	_reset_all(AREA.end)
	for frame in range(12): _step(1.0/60.0,Vector2.ONE*MAX_SPEED)
	checks.append({"world_boundary_and_camera_remain_valid":player.position==AREA.end and actual_velocity.is_zero_approx() and camera.position.is_equal_approx(WORLD_SIZE-Vector2(640,400)/CAMERA_ZOOM)})
	_reset_all()
	var passed:=true
	for check in checks: passed=passed and bool(check[check.keys()[0]])
	return {"passed":passed,"checks":checks,"views":views,"scope":"Independent eight-direction visual sword prototype; no damage, resource, formal combat or save changes."}

func _capture_demo() -> void:
	var dir:=ProjectSettings.globalize_path("res://output/pawn_eight_way_sword_high_view_20261105")
	DirAccess.make_dir_recursive_absolute(dir)
	var report:=_validate_eight_way()
	var directions: Array[Vector2]=[Vector2.DOWN,Vector2(1,1).normalized(),Vector2.RIGHT,Vector2(1,-1).normalized(),Vector2.UP,Vector2(-1,-1).normalized(),Vector2.LEFT,Vector2(-1,1).normalized()]
	for index in range(8):
		_reset_all()
		aim=directions[index]
		for frame in range(10): _step(1.0/30.0,Vector2.ZERO)
		await _snapshot(dir.path_join("direction_%d.png"%index))
	_reset_all()
	aim=Vector2.RIGHT
	sword.request_attack(-1,true)
	var captured: Array[int]=[]
	var captured_wind: Array[int]=[]
	var captured_follow: Array[int]=[]
	var motion:=[]
	for frame in range(270):
		if frame==105:
			_reset_all()
			aim=Vector2(1,-0.45).normalized()
			sword.request_attack(-1,true)
		if frame==198:
			_reset_all()
			aim=Vector2(-1,-0.45).normalized()
			sword.request_attack(-1,true)
		var target:=Vector2.ZERO if frame<105 else (Vector2(0.65,-0.3).normalized()*100.0 if frame<198 else Vector2(-0.7,0.15).normalized()*80.0)
		_step(1.0/30.0,target)
		if sword.stage>=0 and frame<105:
			var t: float=sword.age/float(Sword.Rules.STAGES[sword.stage].duration)
			if not captured_wind.has(sword.stage) and t>=0.19:
				captured_wind.append(sword.stage)
				await _snapshot(dir.path_join("stage_%d_windup.png"%(sword.stage+1)))
			if not captured_follow.has(sword.stage) and t>=0.65:
				captured_follow.append(sword.stage)
				await _snapshot(dir.path_join("stage_%d_follow.png"%(sword.stage+1)))
		if sword.stage>=0 and not captured.has(sword.stage) and sword.age/float(Sword.Rules.STAGES[sword.stage].duration)>0.40:
			captured.append(sword.stage)
			await _snapshot(dir.path_join("stage_%d.png"%(sword.stage+1)))
		motion.append({"frame":frame,"stage":sword.stage,"age":sword.age,"position":player.position,"direction":player.direction_index,"tip":sword.last_tip,"body_rotation":player.visual.rotation,"feet_moving":player.moving})
		await _snapshot(dir.path_join("move_%03d.png"%frame))
	report["motion"]=motion
	report["fps"]=30
	report["capture_frames"]=270
	report["view_projection"]={"elevation_degrees":45.0,"ground_depth":Sword.VIEW_DEPTH,"height":Sword.VIEW_HEIGHT,"physical_blade_length":Sword.BLADE_LENGTH,"fixed_sword_size_multiplier":Sword.SIZE_MULTIPLIER}
	report["eight_view_art"]={"body":"res://tools/action_battle_test/art/pawn_eight_way_20261105/body.png","head_hair":"res://tools/action_battle_test/art/pawn_eight_way_20261105/head_hair.png"}
	FileAccess.open(dir.path_join("validation.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("EIGHT WAY SWORD CAPTURE ","PASS" if report.passed else "FAIL",": ",report.checks)
	get_tree().quit(0 if report.passed else 1)
