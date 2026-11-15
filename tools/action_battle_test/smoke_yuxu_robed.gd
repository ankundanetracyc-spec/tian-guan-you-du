extends SceneTree

var failures: Array[String] = []
var checks := 0
var maximum_support_speed := 0.0

func verify(value: bool, description: String) -> void:
	checks += 1
	if not value:
		failures.append(description)
		push_error(description)

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var rig = load("res://guigu_rig_runtime.gd").new()
	root.add_child(rig)
	verify(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"),"玉虚长袍运行时载入")
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	var clock := 0.0
	# Track the actual transformed shoe in world coordinates, rather than
	# merely comparing the movement constant to an animation constant.
	for facing in [1.0,-1.0]:
		for direction in [Vector2.RIGHT,Vector2.LEFT,Vector2(1,-1).normalized()]:
			rig.position = Vector2.ZERO
			var velocity: Vector2 = direction*114.0
			var backward: bool = velocity.x*facing<0.0
			skin.gait_amount=1.0
			skin.gait_backward=1.0 if backward else 0.0
			skin.motion_facing=facing
			skin.previous_velocity=velocity
			var previous := {}
			for frame in range(180):
				clock += 1.0/60.0
				rig.position += velocity/60.0
				skin.set_motion_context(1.0/60.0,velocity,clock,facing)
				rig.set_character_pose("BackstepWalk" if backward else "Run_New1",clock,facing)
				for index in range(2):
					var side := "near" if index==0 else "far"
					var phase: float = fposmod(skin.gait_phase+float(index)*0.5,1.0)
					var planted: bool = phase>0.10 and phase<0.36
					var point: Vector2 = skin.bones[side+"_foot"].global_position
					if planted and previous.has(side) and previous[side].planted:
						var speed: float = absf(point.x-previous[side].point.x)*60.0
						maximum_support_speed=maxf(maximum_support_speed,speed)
						verify(speed<0.1,"稳定支撑阶段世界横向滑移低于0.1px/s（双向/前退/斜走）")
					previous[side]={"point":point,"planted":planted}
	# Inertia must survive the release frame and then dissipate without drift.
	var peak_after_stop := 0.0
	for frame in range(180):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,-1.0)
		rig.set_character_pose("Idle",clock,-1.0)
		for side in ["front","back"]:
			var edge: float = absf(skin.bones[side+"_edge"].rotation)
			if frame<12: peak_after_stop=maxf(peak_after_stop,edge)
			if frame==179:
				verify(edge<0.004,"无驱动力三秒后末摆在重力与阻尼下回正")
	verify(peak_after_stop>0.015,"停止后仍有短暂惯性回落")
	var body_source: Image = skin.textures.body.get_image()
	verify(body_source.get_size()==Vector2i(1024,1536),"继续使用认可原袍纹理")
	print("YUXU ROBED CHECKS: ",checks,"; failures: ",failures.size(),"; maximum planted shoe horizontal speed px/s: ",maximum_support_speed)
	rig.queue_free()
	quit(0 if failures.is_empty() else 1)
