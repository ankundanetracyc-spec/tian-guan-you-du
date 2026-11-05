extends SceneTree

var checks := 0
var failures: Array[String] = []

func verify(value: bool, description: String) -> void:
	checks += 1
	if not value:
		failures.append(description)
		if failures.size() < 12: push_error(description)

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var rig = load("res://guigu_rig_runtime.gd").new()
	root.add_child(rig)
	verify(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"),"closed garment runtime loads")
	var skin = rig.wanderer_skin
	if skin == null:
		quit(1)
		return
	rig.set_skin_mode("experiment")
	verify(skin.garment_mode == &"closed_robe","current Yuxu explicitly uses closed robe policy")
	verify(skin.meshes.size() == 6 and skin.bones.size() == 21,"one continuous robe plus complete 21-bone actor")
	verify(skin.garment_body.name == "YuxuUnified_robe_closed","body is a single continuous grid")
	verify(skin.garment_body.get_child_count() == 0,"no independent skirt overlays or slit panels")
	verify(skin.validation_errors.is_empty(),"all continuous cloth weights normalized")
	var clock := 0.0
	var peak_angle := 0.0
	var peak_stop := 0.0
	var first_stop := 0.0
	var max_floor := -INF
	var max_shoe_floor := -INF
	var maximum_knee_x := -INF
	var final_settle := 0.0
	for facing in [1.0,-1.0]:
		for frame in range(390):
			var velocity := Vector2.ZERO
			var clip := "Idle"
			var pose_time := clock
			var actual_facing: float = facing
			if frame >= 20 and frame < 100:
				velocity = Vector2(114.0*facing,0.0)
				clip = "Run_New1"
			elif frame >= 100 and frame < 150:
				velocity = Vector2(-114.0*facing,0.0)
				clip = "BackstepWalk"
			elif frame >= 150 and frame < 195:
				velocity = Vector2(-235.0*facing,0.0)
				actual_facing = -facing
				clip = "Run_New1"
			elif frame >= 195 and frame < 213:
				clip = "AttackIdle"
				pose_time = float(frame-195)/60.0
			elif frame >= 213 and frame < 225:
				clip = "Step1"
				velocity = Vector2(600.0*facing,0.0)
				pose_time = float(frame-213)/60.0
			clock += 1.0/60.0
			skin.set_motion_context(1.0/60.0,velocity,clock,actual_facing)
			rig.set_character_pose(clip,pose_time,actual_facing)
			var diagnostic: Dictionary = skin.get_garment_diagnostics()
			var angles: Vector3 = diagnostic.angles
			peak_angle = maxf(peak_angle,angles.length())
			max_floor = maxf(max_floor,float(diagnostic.lowest_cloth_y))
			verify(float(diagnostic.lowest_cloth_y) <= -0.49,"opaque lower silhouette remains above floor")
			verify(float(diagnostic.lowest_cloth_y) > -0.70,"opaque lower silhouette stays grounded")
			verify(is_finite(angles.length()) and angles.length()<0.45,"coupled pendulums remain stable")
			for side in ["near","far"]:
				var knee: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_calf"].global_position)
				maximum_knee_x = maxf(maximum_knee_x,knee.x)
				# At knee height the opaque robe extends roughly twelve units
				# forward. Keep the joint centre inside 8.5, leaving trouser width.
				verify(knee.x < 8.5 and knee.x > -8.5,"complete knee centre stays within narrow upper-skirt stance")
				var foot: Bone2D = skin.bones[side+"_foot"]
				var ankle: Vector2 = rig.rig_root_node.to_local(foot.global_position)
				verify(ankle.x >= -6.51 and ankle.x <= 5.01,"full ankle stays inside closed garment stance")
				var contact: Dictionary = skin.ground_contacts[side]
				max_shoe_floor = maxf(max_shoe_floor,float(contact.center.y))
				verify(float(contact.center.y)<0.10,"boot sole never sinks through floor")
			if frame == 225: first_stop = angles.length()
			if frame >= 225 and frame < 245: peak_stop = maxf(peak_stop,angles.length())
			if frame == 389: final_settle = angles.length()
		# Exactly the same pose and simulation clock must not advance under a
		# pause, even when the view repeatedly asks for the actor to redraw.
		var transforms := {}
		for key in skin.bones: transforms[key] = skin.bones[key].transform
		var before: Dictionary = skin.get_garment_diagnostics()
		for pause_frame in range(15):
			skin.set_motion_context(0.0,Vector2.ZERO,clock,facing)
			rig.set_character_pose("Idle",clock,facing)
		for key in skin.bones: verify(skin.bones[key].transform == transforms[key],"pause freezes every actor bone")
		verify(skin.get_garment_diagnostics().simulation_steps == before.simulation_steps,"pause freezes cloth simulation")
	verify(peak_angle > 0.055,"acceleration and reversals visibly excite cloth")
	verify(peak_stop > 0.025 and first_stop > 0.015,"stop preserves and releases existing momentum")
	verify(final_settle < 0.006,"gravity and damping settle idle cloth")
	# Restart/teleport must discard old angular momentum without an oversized
	# catch-up impulse. Both operations occur at normal scene transitions.
	clock += 1.0/60.0
	skin.set_motion_context(1.0/60.0,Vector2(235.0,0.0),clock,1.0)
	rig.set_character_pose("Run_New1",clock,1.0)
	verify(skin.garment_angular_velocity.length() > 0.01,"restart test begins with real motion")
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,0.0,1.0)
	rig.set_character_pose("Idle",0.0,1.0)
	verify(skin.garment_angles.length()<0.005 and skin.garment_angular_velocity.length()<0.05,"reset clock clears old garment momentum")
	rig.position += Vector2(1200.0,0.0)
	skin.set_motion_context(1.0/60.0,Vector2.ZERO,1.0/60.0,1.0)
	rig.set_character_pose("Idle",1.0/60.0,1.0)
	verify(skin.garment_angles.length()<0.005 and skin.garment_drive==0.0,"teleport clears old momentum without acceleration burst")
	# Structural compatibility uses the same source and keeps original split
	# geometry independently, without labelling any other inventory clothing.
	var legacy = load("res://realtime_garment_skin.gd").new()
	legacy.garment_mode = &"split_outfit"
	var legacy_owner = load("res://guigu_rig_runtime.gd").new()
	root.add_child(legacy_owner)
	legacy_owner.skeleton = Skeleton2D.new()
	legacy_owner.rig_root_node = Bone2D.new()
	legacy_owner.rig_root_node.set_autocalculate_length_and_angle(false)
	legacy_owner.rig_root_node.length = 0.0
	legacy_owner.rig_root_node.rest = Transform2D.IDENTITY
	legacy_owner.add_child(legacy_owner.skeleton)
	legacy_owner.skeleton.add_child(legacy_owner.rig_root_node)
	verify(legacy.build(legacy_owner,"res://art/yuxu_fullbody_v15"),"split policy builds the unchanged approved source")
	verify(legacy.meshes.size() == 7 and legacy.garment_body == null,"split policy retains legacy panel geometry")
	print("REALTIME GARMENT CHECKS: ",checks,"; failures: ",failures.size(),"; peak angular magnitude: ",peak_angle,"; stop peak: ",peak_stop,"; settled: ",final_settle,"; max cloth floor y: ",max_floor,"; max boot floor y: ",max_shoe_floor,"; max knee x: ",maximum_knee_x)
	rig.queue_free()
	legacy_owner.queue_free()
	quit(0 if failures.is_empty() else 1)
