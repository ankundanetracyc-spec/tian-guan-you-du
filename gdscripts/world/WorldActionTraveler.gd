extends Node2D
## The world-map appearance, driven by travel and airflow rather than camera motion.
const TravelerSkin = preload("res://gdscripts/world/WorldTravelerSkin.gd")

var rig: TravelerSkin
var loaded := false
var facing := 1.0
var motion_clock := 0.0
var current_clip := "Idle"
var native_height := TravelerSkin.HEIGHT
var motion_state := "standing"

func build(envelope_height: float) -> bool:
	rig = TravelerSkin.new()
	rig.name = "WorldTravelerRig"
	add_child(rig)
	if not rig.build():
		push_error("百川行旅人物画件加载失败")
		rig.queue_free()
		rig = null
		return false
	scale = Vector2.ONE * envelope_height / native_height
	loaded = true
	update_motion(0.0, Vector2.ZERO)
	return true

func update_motion(delta: float, velocity: Vector2) -> void:
	if not loaded: return
	_update_presentation(delta, velocity if velocity.is_finite() else Vector2.ZERO, 0.0)

func update_flight(delta: float, air_pressure: float) -> void:
	if not loaded: return
	_update_presentation(delta, Vector2.ZERO, clampf(air_pressure, 0.0, 1.0))

func _update_presentation(delta: float, velocity: Vector2, air_pressure: float) -> void:
	var moving := velocity.length_squared() > 0.01
	if absf(velocity.x) > 0.01: facing = signf(velocity.x)
	var step := clampf(delta, 0.0, 0.05)
	motion_clock += step
	current_clip = "Run_New1" if moving else "Idle"
	motion_state = "flight" if air_pressure > 0.01 else ("running" if moving else "standing")
	rig.pose(step, moving, motion_clock, facing, air_pressure)

func reset_motion_after_placement() -> void:
	if not loaded: return
	rig.reset_secondary_motion()
	update_motion(0.0, Vector2.ZERO)

func carry_wind_from(other: Node2D) -> void:
	if loaded and other.loaded:
		rig.carry_wind_from(other.rig, facing)

func mirror_wind() -> void:
	if loaded: rig.mirror_wind()

func get_airflow() -> float:
	return rig.airflow if loaded else 0.0

func get_visual_summary() -> Dictionary:
	var summary := {"anatomical_skin": rig.summary() if loaded else {}, "bone_bound_art_ready": loaded}
	summary["world_avatar_loaded"] = loaded
	summary["appearance"] = TravelerSkin.APPEARANCE
	summary["clip"] = current_clip
	summary["facing"] = facing
	summary["motion_clock"] = motion_clock
	summary["native_height"] = native_height
	summary["motion_state"] = motion_state
	return summary
