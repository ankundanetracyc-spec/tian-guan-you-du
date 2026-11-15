extends SceneTree

var checks := 0
var failures: Array[String] = []

func verify(ok: bool, description: String) -> void:
	checks += 1
	if not ok:
		failures.append(description)
		push_error(description)

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var traveler = load("res://gdscripts/world/WorldActionTraveler.gd").new()
	root.add_child(traveler)
	verify(traveler.build(80.0), "world traveler accepts realtime garment adapter")
	await process_frame
	var skin = traveler.rig.wanderer_skin
	verify(skin.has_method("get_garment_diagnostics"), "world traveler receives the garment adapter")
	var summary: Dictionary = traveler.get_visual_summary()
	verify(summary.world_avatar_loaded and summary.art_folder == "yuxu_fullbody_v15", "world summary retains current avatar")
	verify(traveler.native_height > 0.0 and is_equal_approx(traveler.native_height * traveler.scale.y, 80.0), "world marker height still uses source-space ruler")
	var maximum_floor := -INF
	for frame in range(120):
		var velocity := Vector2(20, 0) if frame < 24 else (Vector2(-20, 0) if frame < 40 else Vector2.ZERO)
		traveler.update_motion(0.05, velocity)
		var diagnostics: Dictionary = skin.get_garment_diagnostics()
		maximum_floor = maxf(maximum_floor, float(diagnostics.lowest_cloth_y))
		if frame == 12: verify(traveler.current_clip == "Run_New1" and traveler.facing == 1.0, "world forward movement keeps current clip and facing")
		if frame == 32: verify(traveler.current_clip == "Run_New1" and traveler.facing == -1.0, "world reverse movement turns current avatar")
	verify(traveler.current_clip == "Idle", "world stop returns to idle")
	verify(maximum_floor < 0.10, "shared floor-length garment stays above world foot anchor")
	var angles: Vector3 = skin.garment_angles
	var speeds: Vector3 = skin.garment_angular_velocity
	verify(angles.length() < 0.002 and speeds.length() < 0.01, "world stop settles shared garment")
	var pose := {}
	for key in skin.bones: pose[key] = skin.bones[key].transform
	var clock: float = traveler.motion_clock
	var steps: int = skin.garment_simulation_steps
	traveler.visible = false
	for frame in range(8): traveler.update_motion(0.0, Vector2.ZERO)
	verify(traveler.motion_clock == clock and skin.garment_simulation_steps == steps, "hidden zero-delta avatar does not advance time or physics")
	var unchanged := true
	for key in skin.bones: unchanged = unchanged and skin.bones[key].transform.is_equal_approx(pose[key])
	verify(unchanged, "hidden zero-delta avatar retains all bone transforms")
	summary = traveler.get_visual_summary()
	verify(summary.world_avatar_loaded and summary.clip == "Idle" and summary.facing == -1.0, "world summary remains valid after movement stop and hide")
	traveler.queue_free()
	await process_frame
	print("WORLD GARMENT INTERFACE: ", checks, " checks; failures=", failures.size())
	quit(0 if failures.is_empty() else 1)
