extends SceneTree

const Wake = preload("res://gdscripts/world/SpiritBoatWake.gd")
var checks := 0


func _initialize() -> void:
	var wake_scene := load("res://tscn/world/bai_chuan/SpiritBoatWake.tscn") as PackedScene
	if not _check(wake_scene != null, "the reusable scene and its imported texture must load"): return
	var packed_wake = wake_scene.instantiate()
	root.add_child(packed_wake)
	if not _check(packed_wake.configure() and packed_wake.wisp_texture != null, "the reusable scene supplies its exportable texture"): return
	packed_wake.queue_free()
	var wake = Wake.new()
	root.add_child(wake)
	if not _check(wake.configure(), "texture must load"): return
	var ground := Vector2(100, 200)
	var altitude := Vector2(0, -90)
	wake.update_flight(0.0, ground, altitude, 1.0, 1.0, true)
	for _i in range(24):
		ground.x += 1.0
		wake.update_flight(0.05, ground, altitude, 1.0, 1.0, true)
	var wisps: Array = wake.get("_wisps")
	if not _check(wisps.size() > 0 and wisps.size() <= 18, "a moving flying boat emits a bounded wake"): return
	if not _check(float(wisps.front()["side"]) == 1.0 and (wisps.front()["point"] as Vector2).y < -85.0, "rightward wake follows the boat into the air"): return
	var old_wisp: Dictionary = wisps.back()
	var age_before := float(old_wisp["age"])
	var point_before: Vector2 = old_wisp["point"]
	wake.update_flight(0.0, ground, altitude, 2.0, -1.0, false)
	if not _check((old_wisp["point"] as Vector2).is_equal_approx(point_before), "zoom preserves the old wisp's world location"): return
	if not _check(is_equal_approx(float(old_wisp["age"]), age_before), "paused effects do not age"): return
	point_before = old_wisp["point"]
	ground.x -= 3.0
	wake.update_flight(0.0, ground, altitude, 2.0, -1.0, true)
	if not _check((old_wisp["point"] as Vector2).is_equal_approx(point_before + Vector2(3, 0)), "old wake stays anchored as the boat moves"): return
	for _i in range(4):
		ground.x -= 1.0
		wake.update_flight(0.05, ground, altitude, 2.0, -1.0, true)
	if not _check(float(old_wisp["side"]) == 1.0 and float(wisps.back()["side"]) == -1.0, "turning preserves old orientation and reverses new emissions"): return
	for _i in range(40):
		wake.update_flight(0.05, ground, altitude, 2.0, -1.0, false)
	if not _check(wisps.is_empty(), "the wake fades away after stopping"): return
	for _i in range(10):
		wake.update_flight(0.05, ground, altitude, 2.0, 1.0, true)
	if not _check(wisps.is_empty(), "a stationary boat emits no wake"): return
	wake.clear_wake()
	if not _check(not bool(wake.get("_has_last_ground")), "teleport reset clears the motion sample"): return
	wake.update_flight(0.0, ground, altitude, 2.0, 1.0, true)
	for _i in range(5):
		ground.y -= 1.0
		wake.update_flight(0.05, ground, altitude, 2.0, 1.0, true)
	if not _check(not wisps.is_empty() and absf(float(wisps.back()["tilt"]) + PI * 0.5) < 0.04, "northward flight points new wisps back along its path"): return
	if not _check((wisps.back()["drift"] as Vector2).y > 0.0, "northward flight drifts backwards"): return
	print("SPIRIT_BOAT_WAKE_SMOKE_OK: %d checks" % checks)
	wake.queue_free()
	quit(0)


func _check(condition: bool, description: String) -> bool:
	checks += 1
	if condition:
		return true
	push_error("Spirit boat wake smoke failed: " + description)
	quit(1)
	return false
