extends Node2D

const Avatar = preload("pawn_two_view_avatar.gd")
const Adapter = preload("pawn_appearance_profile.gd")
const FIELD := "res://tools/action_battle_test/art/pawn_two_view_20261105/battlefield.png"
const WORLD_SIZE := Vector2(2560,1600)
const WORLD_RECT := Rect2(Vector2.ZERO,WORLD_SIZE)
const AREA := Rect2(96,190,2368,1314)
const SPAWN := Vector2(980,800)
const CAMERA_ZOOM := 0.82
const CAMERA_FOLLOW_RATE := 7.0
const MAX_SPEED := 255.0
const ACCELERATION := 1500.0
const BRAKING := 2000.0
const TURN_ACCELERATION := 3000.0
var player: Node2D
var adapter: RefCounted
var posts: Array[Node2D] = []
var aim := Vector2.RIGHT
var hit_count := 0
var strike_time := 0.0
var paused := false
var capture_active := false
var status: Label
var font := SystemFont.new()
var motion_velocity := Vector2.ZERO
var actual_velocity := Vector2.ZERO
var turn_boost_left := 0.0
var camera: Camera2D

class PracticePost extends Node2D:
	var reaction := 0.0
	func _draw() -> void:
		draw_set_transform(Vector2.ZERO,0,Vector2(1,0.22))
		draw_circle(Vector2.ZERO,27,Color(0.15,0.2,0.17,0.14),true,-1,true)
		draw_set_transform(Vector2.ZERO)
		var sway := sin(reaction*30.0)*reaction*12.0
		draw_set_transform(Vector2(sway,0),sway*0.006)
		draw_style_box(_wood(Color("716654"),Color("41483f")),Rect2(-10,-83,20,81))
		draw_style_box(_wood(Color("a79c80"),Color("545b4e")),Rect2(-35,-61,70,13))
		draw_style_box(_wood(Color("a29b83"),Color("545b4e")),Rect2(-15,-81,30,41))
		for y in [-74,-64,-54]: draw_line(Vector2(-14,y),Vector2(14,y+1),Color("d2c7a7"),2,true)
		if reaction>0.0: draw_arc(Vector2(0,-59),25+(0.35-reaction)*40,0,TAU,32,Color(0.34,0.46,0.42,reaction*2.0),2,true)
		draw_set_transform(Vector2.ZERO)
	func _wood(fill: Color,border: Color) -> StyleBoxFlat:
		var style:=StyleBoxFlat.new()
		style.bg_color=fill
		style.border_color=border
		style.set_border_width_all(2)
		style.set_corner_radius_all(4)
		return style
	func tick(delta: float) -> void:
		reaction=maxf(0.0,reaction-delta)
		queue_redraw()

func _ready() -> void:
	get_window().title="水墨圆袖 · 双视试玩"
	get_window().size=Vector2i(1280,800)
	get_window().content_scale_size=Vector2i(1280,800)
	get_window().content_scale_mode=Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect=Window.CONTENT_SCALE_ASPECT_KEEP
	font.font_names=PackedStringArray(["Microsoft YaHei"])
	var background:=Sprite2D.new()
	background.texture=ImageTexture.create_from_image(Image.load_from_file(FIELD))
	background.centered=false
	background.scale=Vector2(WORLD_SIZE.x/background.texture.get_width(),WORLD_SIZE.y/background.texture.get_height())
	background.position=Vector2.ZERO
	background.z_index=-1000
	add_child(background)
	camera=Camera2D.new()
	camera.position=SPAWN
	camera.zoom=Vector2.ONE*CAMERA_ZOOM
	camera.limit_left=0
	camera.limit_top=0
	camera.limit_right=int(WORLD_SIZE.x)
	camera.limit_bottom=int(WORLD_SIZE.y)
	add_child(camera)
	adapter=Adapter.new()
	adapter.build(self)
	player=Avatar.new()
	add_child(player)
	player.build()
	player.set_appearance(adapter.get_demo_sample_profile())
	player.position=SPAWN
	player.scale=Vector2.ONE*1.65
	_update_camera(0.0,true)
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
	bar.color=Color(0.95,0.93,0.87,0.92)
	bar.position=Vector2(22,20)
	bar.size=Vector2(1236,70)
	ui.add_child(bar)
	for data in [["圆袖 · 竹间试练",Vector2(40,30),24],["WASD 移动   鼠标瞄准   左键试打   R 归位   Esc 暂停",Vector2(440,39),17]]:
		var label:=Label.new()
		label.text=data[0]
		label.position=data[1]
		label.add_theme_font_override("font",font)
		label.add_theme_font_size_override("font_size",data[2])
		label.add_theme_color_override("font_color",Color("3b4b42"))
		ui.add_child(label)
	status=Label.new()
	status.position=Vector2(40,748)
	status.add_theme_font_override("font",font)
	status.add_theme_font_size_override("font_size",18)
	status.add_theme_color_override("font_color",Color("3b4b42"))
	ui.add_child(status)
	_update_status()

func _update_status() -> void:
	status.text=("已暂停 · Esc继续" if paused else "靠近木桩试打 · 命中 "+str(hit_count))+"    正背双视 · 首套试穿"

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.physical_keycode==KEY_ESCAPE: paused=not paused; _update_status()
		if event.physical_keycode==KEY_R:
			_reset_motion(SPAWN)
			hit_count=0
			_update_status()
	if event is InputEventMouseButton and event.button_index==MOUSE_BUTTON_LEFT and event.pressed and not paused:
		_attack()

func _process(delta: float) -> void:
	if paused or capture_active: return
	var velocity:=Vector2(float(Input.is_physical_key_pressed(KEY_D))-float(Input.is_physical_key_pressed(KEY_A)),float(Input.is_physical_key_pressed(KEY_S))-float(Input.is_physical_key_pressed(KEY_W))).normalized()*MAX_SPEED
	var mouse:=get_global_mouse_position()
	if Rect2(0,95,1280,635).has_point(get_viewport().get_mouse_position()):
		aim=(mouse-(player.position+Vector2(0,-80))).normalized()
	elif velocity.length_squared()>0: aim=velocity.normalized()
	_step(delta,velocity)

func _step(delta: float, velocity: Vector2) -> void:
	if paused or delta<=0.0: return
	var dt := minf(delta,0.05)
	var target := velocity.limit_length(MAX_SPEED)
	turn_boost_left = maxf(0.0,turn_boost_left-dt)
	if target.is_zero_approx(): turn_boost_left = 0.0
	elif motion_velocity.dot(target)<0.0: turn_boost_left = 0.18
	var rate := BRAKING if target.is_zero_approx() else (TURN_ACCELERATION if turn_boost_left>0.0 else ACCELERATION)
	motion_velocity = motion_velocity.move_toward(target,rate*dt)
	var before: Vector2 = player.position
	var requested_position := player.position+motion_velocity*dt
	player.position=requested_position.clamp(AREA.position,AREA.end)
	actual_velocity = (player.position-before)/dt
	if not is_equal_approx(player.position.x,requested_position.x): motion_velocity.x = 0.0
	if not is_equal_approx(player.position.y,requested_position.y): motion_velocity.y = 0.0
	player.z_index=int(player.position.y)
	player.set_direction(aim)
	player.advance(dt,actual_velocity)
	_update_camera(dt)
	strike_time=maxf(0.0,strike_time-dt)
	for post in posts: post.tick(dt)
	queue_redraw()

func _reset_motion(at: Vector2) -> void:
	player.position = at
	player.z_index = int(at.y)
	motion_velocity = Vector2.ZERO
	actual_velocity = Vector2.ZERO
	turn_boost_left = 0.0
	player.reset_motion()
	_update_camera(0.0,true)

func _camera_target() -> Vector2:
	var half_view := Vector2(640,400)/CAMERA_ZOOM
	return player.position.clamp(WORLD_RECT.position+half_view,WORLD_RECT.end-half_view)

func _update_camera(delta: float, instant: bool = false) -> void:
	if camera==null or player==null: return
	var target := _camera_target()
	camera.position = target if instant else camera.position.lerp(target,1.0-exp(-delta*CAMERA_FOLLOW_RATE))

func _attack() -> void:
	if not player.strike(): return
	strike_time=0.32
	for post in posts:
		var toward: Vector2 = post.position+Vector2(0,-45)-(player.position+Vector2(0,-45))
		if toward.length()<135 and toward.normalized().dot(aim)>0.35:
			post.reaction=0.35
			hit_count+=1
	_update_status()

func _draw() -> void:
	if player==null: return
	var origin: Vector2 = player.position+Vector2(0,-45)
	var tip: Vector2 = origin+aim*88.0
	draw_arc(tip,5,0,TAU,24,Color(0.3,0.4,0.34,0.35),1.3,true)
	if strike_time>0:
		var t:=1.0-strike_time/0.32
		var angle:=aim.angle()
		draw_arc(origin,60+30*sin(t*PI),angle-0.85+t*0.7,angle+0.7+t*0.7,26,Color(0.27,0.37,0.32,sin(t*PI)*0.5),2.5,true)

func _snapshot(path: String) -> void:
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(path)

func get_motion_diagnostics() -> Dictionary:
	return {"position":player.position,"requested_motion_velocity":motion_velocity,"actual_velocity":actual_velocity,"speed":actual_velocity.length(),"phase":player.phase,"distance":player.travelled_distance,"gait_weight":player.gait_weight,"foot_centres":player.feet.centres.duplicate(),"motion_lag":player.motion_lag,"body_rotation":player.visual.rotation,"camera_position":camera.position,"camera_target":_camera_target(),"view":player.direction}

func _trace_motion(frames: int, target: Vector2, delta: float = 1.0/60.0) -> Array[Dictionary]:
	var trace: Array[Dictionary] = []
	for frame in range(frames):
		_step(delta,target)
		trace.append(get_motion_diagnostics())
	return trace

func validate_motion() -> Dictionary:
	var checks := []
	var traces := {}
	var dt := 1.0/60.0
	paused = false
	_reset_motion(SPAWN)
	var start := _trace_motion(18,Vector2.RIGHT*MAX_SPEED)
	traces.start = start
	var monotonic := true
	var time_to_95 := INF
	var last_speed := 0.0
	for index in range(start.size()):
		var speed: float = start[index].speed
		monotonic = monotonic and speed>=last_speed-0.02 and speed<=MAX_SPEED+0.02
		if speed>=MAX_SPEED*0.95 and is_inf(time_to_95): time_to_95 = float(index+1)*dt
		last_speed = speed
	checks.append({"acceleration_increases_to_cruise":monotonic and time_to_95<=0.20,"seconds_to_95_percent":time_to_95})
	var before_brake := player.position
	var brake := _trace_motion(24,Vector2.ZERO)
	traces.brake = brake
	var time_to_5 := INF
	monotonic = true
	last_speed = MAX_SPEED
	for index in range(brake.size()):
		var speed: float = brake[index].speed
		monotonic = monotonic and speed<=last_speed+0.02
		if speed<=MAX_SPEED*0.05 and is_inf(time_to_5): time_to_5=float(index+1)*dt
		last_speed = speed
	var stop_distance := player.position.distance_to(before_brake)
	checks.append({"release_brakes_without_instant_stop":monotonic and time_to_5<=0.15 and stop_distance>5.0 and stop_distance<22.0,"seconds_to_5_percent":time_to_5,"stopping_distance":stop_distance})
	var rest_error := 0.0
	for index in range(2): rest_error=maxf(rest_error,player.feet.centres[index].distance_to(Vector2(-8.0 if index==0 else 8.0,-5.8)))
	checks.append({"feet_and_body_smoothly_return_to_rest":rest_error<0.0001 and player.gait_weight==0.0 and is_equal_approx(player.visual.position.y,-11.8),"rest_foot_error":rest_error})
	_reset_motion(SPAWN)
	_trace_motion(18,Vector2.RIGHT*MAX_SPEED)
	var reverse := _trace_motion(24,Vector2.LEFT*MAX_SPEED)
	traces.reverse = reverse
	var cross_time := INF
	var reverse_90 := INF
	for index in range(reverse.size()):
		if float((reverse[index].actual_velocity as Vector2).x)<0.0 and is_inf(cross_time): cross_time=float(index+1)*dt
		if float((reverse[index].actual_velocity as Vector2).x)<=-MAX_SPEED*0.9 and is_inf(reverse_90): reverse_90=float(index+1)*dt
	checks.append({"reverse_is_responsive":cross_time<=0.11 and reverse_90<=0.22,"reverse_crosses_zero_seconds":cross_time,"reverse_90_percent_seconds":reverse_90})
	var max_phase_error := 0.0
	var max_foot_step := 0.0
	var max_lag := 0.0
	var max_body_rotation := 0.0
	for key in traces:
		var previous_feet: Array = []
		for sample in traces[key]:
			max_phase_error=maxf(max_phase_error,absf(float(sample.phase)-float(sample.distance)/player.STEP_CYCLE_DISTANCE*TAU))
			max_lag=maxf(max_lag,(sample.motion_lag as Vector2).length())
			max_body_rotation=maxf(max_body_rotation,absf(float(sample.body_rotation)))
			if not previous_feet.is_empty():
				for index in range(2): max_foot_step=maxf(max_foot_step,(sample.foot_centres[index] as Vector2).distance_to(previous_feet[index]))
			previous_feet=sample.foot_centres
	checks.append({"phase_matches_actual_world_distance":max_phase_error<0.00001,"maximum_phase_error_radians":max_phase_error,"cycle_distance":player.STEP_CYCLE_DISTANCE,"cruise_footfalls_per_second":MAX_SPEED/player.STEP_CYCLE_DISTANCE*2.0})
	checks.append({"feet_do_not_snap_during_start_stop_reverse":max_foot_step<2.1,"maximum_adjacent_foot_step_native_pixels":max_foot_step})
	checks.append({"restrained_lag_and_no_body_sway":max_lag<=0.8001 and max_body_rotation==0.0,"maximum_lag_native_pixels":max_lag,"maximum_body_rotation":max_body_rotation})
	_reset_motion(AREA.end)
	var boundary := _trace_motion(12,Vector2.ONE*MAX_SPEED)
	var boundary_still := true
	for sample in boundary: boundary_still=boundary_still and (sample.actual_velocity as Vector2).is_zero_approx() and is_zero_approx(float(sample.phase))
	checks.append({"world_boundary_stops_motion_and_stride":boundary_still and player.position==AREA.end})
	var half_view := Vector2(640,400)/CAMERA_ZOOM
	checks.append({"camera_limits_stay_inside_expanded_world":camera.position.is_equal_approx(WORLD_SIZE-half_view) and WORLD_SIZE==Vector2(2560,1600),"world_size":WORLD_SIZE,"walk_area":AREA,"maximum_camera_centre":camera.position})
	_reset_motion(Vector2(1800,1100))
	_step(dt,Vector2.RIGHT*MAX_SPEED)
	checks.append({"expanded_world_is_really_walkable":player.position.x>1280.0 and player.position.y>800.0 and AREA.has_point(player.position)})
	_reset_motion(SPAWN)
	var original_camera := camera.position
	_trace_motion(30,Vector2.RIGHT*MAX_SPEED)
	checks.append({"camera_smoothly_follows_actual_player":camera.position.x>original_camera.x+30.0 and camera.position.x<player.position.x-1.0,"camera_position":camera.position,"player_position":player.position})
	var pause_state := get_motion_diagnostics()
	paused = true
	_step(0.05,Vector2.LEFT*MAX_SPEED)
	var pause_after := get_motion_diagnostics()
	checks.append({"pause_freezes_motion_stride_and_camera":pause_state==pause_after})
	paused = false
	var reset_event := InputEventKey.new()
	reset_event.physical_keycode=KEY_R
	reset_event.pressed=true
	_unhandled_input(reset_event)
	checks.append({"reset_clears_velocity_stride_and_inertia":player.position==SPAWN and motion_velocity.is_zero_approx() and actual_velocity.is_zero_approx() and player.motion_lag.is_zero_approx() and player.gait_weight==0.0 and player.phase==0.0})
	checks.append({"latest_visual_boundaries_preserved":not player.has_node("PawnLayers/InkEyes") and player.get_diagnostics().view_count==2 and is_zero_approx(player.visual.rotation) and is_equal_approx(camera.zoom.x,0.82)})
	# The original near/far practice-post demonstration stays visual-only.
	_reset_motion(posts[0].position-Vector2(90,0))
	aim=Vector2.RIGHT
	hit_count=0
	_trace_motion(25,Vector2.ZERO)
	_attack()
	checks.append({"near_target_reacts":hit_count==1 and posts[0].reaction>0.0})
	_trace_motion(25,Vector2.ZERO)
	_reset_motion(SPAWN)
	_attack()
	checks.append({"distant_target_not_hit":hit_count==1})
	_trace_motion(25,Vector2.ZERO)
	_reset_motion(SPAWN)
	hit_count=0
	strike_time=0.0
	_update_status()
	var passed := true
	for check in checks:
		var result_key: String = check.keys()[0]
		passed=passed and bool(check[result_key])
	return {"passed":passed,"checks":checks,"traces":traces,"parameters":{"max_speed":MAX_SPEED,"acceleration":ACCELERATION,"braking":BRAKING,"turn_acceleration":TURN_ACCELERATION,"cycle_distance":player.STEP_CYCLE_DISTANCE,"world_size":WORLD_SIZE,"walk_area":AREA,"camera_zoom":CAMERA_ZOOM,"camera_follow_rate":CAMERA_FOLLOW_RATE},"scope":"Actual runtime movement/feet/camera, source-distance phase, inertial start/stop/reverse, border and pause/reset; visual practice posts only; no production movement, damage or save changes."}

func _capture_demo() -> void:
	var dir := ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().path_join("../../output/pawn_two_view_motion_20261105").simplify_path()
	DirAccess.make_dir_recursive_absolute(dir)
	var report := validate_motion()
	report["field_source"]={"path":FIELD,"sha256":FileAccess.get_sha256(FIELD)}
	report["only_two_views"]=true
	report["eyes"]=false
	report["writes_save"]=false
	report["views"]=[]
	for pair in [["front",Vector2.DOWN],["left",Vector2.LEFT],["right",Vector2.RIGHT],["back",Vector2.UP]]:
		aim=pair[1]
		_trace_motion(25,Vector2.ZERO)
		var diagnostic: Dictionary=player.get_diagnostics()
		report.views.append(diagnostic)
		report.only_two_views=report.only_two_views and diagnostic.direction in ["front","back"]
		await _snapshot(dir.path_join(pair[0]+".png"))
	_reset_motion(AREA.end-Vector2(95,95))
	aim=Vector2.DOWN
	_trace_motion(20,Vector2.ZERO)
	await _snapshot(dir.path_join("world_edge.png"))
	report["edge_view"]=get_motion_diagnostics()
	_reset_motion(SPAWN)
	aim=Vector2.DOWN
	_trace_motion(20,Vector2.ZERO)
	await _snapshot(dir.path_join("world_spawn.png"))
	var movement := []
	for frame in range(120):
		var stage := ""
		var target := Vector2.ZERO
		if frame<30: stage="start_and_cruise";target=Vector2.RIGHT*MAX_SPEED
		elif frame<45: stage="cruise";target=Vector2.RIGHT*MAX_SPEED
		elif frame<60: stage="brake_and_settle"
		elif frame<90: stage="return";target=Vector2.LEFT*MAX_SPEED
		elif frame<105: stage="reverse";target=Vector2.RIGHT*MAX_SPEED
		else: stage="final_stop"
		if not target.is_zero_approx(): aim=target.normalized()
		_step(1.0/30.0,target)
		movement.append({"frame":frame,"stage":stage,"target_velocity":target,"motion":get_motion_diagnostics()})
		await _snapshot(dir.path_join("move_%03d.png"%frame))
	report["movement"]=movement
	report["fps"]=30
	report["capture_frames"]=120
	report["passed"]=report.passed and report.only_two_views
	FileAccess.open(dir.path_join("validation.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("TWO VIEW MOTION CAPTURE ","PASS" if report.passed else "FAIL",": ",report.checks)
	get_tree().quit(0 if report.passed else 1)
