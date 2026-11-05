extends Node2D
## The current side-view combat character, driven only by world travel visuals.
const RigRuntime = preload("res://tools/action_battle_test/guigu_rig_runtime.gd")
const ART_ROOT := "res://tools/action_battle_test/art"
const ART_FOLDER := "yuxu_fullbody_v15"
const RIG_ROOT := "res://tools/action_battle_test/guigu_rig/"

var rig: Node2D
var loaded := false
var facing := 1.0
var motion_clock := 0.0
var current_clip := "Idle"
var native_height := 1.0

func build(envelope_height: float) -> bool:
	rig = RigRuntime.new()
	rig.name = "CombatCharacterRig"
	add_child(rig)
	if not rig.load_runtime(RIG_ROOT + "rig.json", RIG_ROOT + "skins.json", RIG_ROOT + "animations.json", ART_ROOT, ART_FOLDER):
		push_error("大世界局内形象加载失败：%s" % str(rig.get_runtime_summary().errors))
		return false
	rig.set_skin_mode("experiment")
	var image := Image.new()
	if image.load(ProjectSettings.globalize_path(ART_ROOT.path_join(ART_FOLDER).path_join("cutout.png"))) != OK:
		push_error("大世界局内形象缺少原生轮廓")
		return false
	# The same source-space ruler and scale are used by the battle skin. The
	# extra parent fits that actual silhouette to the existing map-marker box.
	if rig.wanderer_skin.has_method("get_native_visual_height"):
		native_height = rig.wanderer_skin.get_native_visual_height()
	else:
		native_height = image.get_used_rect().size.y * rig.wanderer_skin.PIXEL_SCALE * rig.wanderer_skin.display_scale
	scale = Vector2.ONE * envelope_height / maxf(native_height, 1.0)
	loaded = true
	update_motion(0.0, Vector2.ZERO)
	return true

func update_motion(delta: float, velocity: Vector2) -> void:
	if not loaded: return
	var moving := velocity.length_squared() > 0.01
	if absf(velocity.x) > 0.01: facing = -1.0 if velocity.x < 0.0 else 1.0
	var step := clampf(delta, 0.0, 0.05)
	motion_clock += step
	current_clip = "Run_New1" if moving else "Idle"
	# Route duration and game hours remain owned by the map. Feed the existing
	# robe's accepted gait speed so visual stepping is independent of zoom.
	var visual_velocity: Vector2 = velocity.normalized() * rig.wanderer_skin.ROBED_MOVE_SPEED if moving else Vector2.ZERO
	rig.wanderer_skin.set_motion_context(step, visual_velocity, motion_clock, facing)
	rig.wanderer_skin.set_action_context(false, false)
	rig.set_character_pose(current_clip, motion_clock, facing)

func get_visual_summary() -> Dictionary:
	var summary: Dictionary = rig.get_runtime_summary() if rig != null else {}
	summary["world_avatar_loaded"] = loaded
	summary["art_folder"] = ART_FOLDER
	summary["clip"] = current_clip
	summary["facing"] = facing
	summary["motion_clock"] = motion_clock
	summary["native_height"] = native_height
	return summary
