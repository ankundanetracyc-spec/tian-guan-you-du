extends Node2D

## Queue-only bridge to the already-approved v21 small character. This reuses
## the original seven meshes and 21 bones, not the later whole-body portraits.
const SkinScript = preload("res://tools/action_battle_test/queue_robed_skin.gd")
const ART_ROOT := "res://tools/action_battle_test/art/yuxu_fullbody_v15"
const SOURCE_SCALE := 16.0 / 186.0
const NATIVE_HEIGHT := (1474.0 - 75.0) * SOURCE_SCALE
var skeleton: Skeleton2D
var rig_root_node: Node2D
var skin: RefCounted
var visual_root: Node2D
var _display_scale := 1.0
var _facing_left := false
var _paused := false
var _clock := 0.0
var _action := "idle"
var _elapsed := 0.0
var _duration := 1.0
var _ready := false
var _layer_normalizations: Array[Dictionary] = []
var _motion_velocity := Vector2.ZERO
var _source_velocity := Vector2.ZERO
var _gait := "idle"
var _clip := "Idle"
var _queue_gait_amount := 0.0
var _travel_mode := "idle"
var _travel_lean := 0.0
var _travel_cloth := 0.0
var _travel_progress := 0.0
var _flight_height := 0.0
var _travel_stage := "grounded"
var _outfit_id := "male_clothes_05"
var _art_directory := ART_ROOT

func configure(visible_height: float, art_directory: String = ART_ROOT, outfit_id: String = "male_clothes_05") -> bool:
	name = "QueueSmallAvatar"
	_outfit_id = outfit_id
	_art_directory = art_directory
	_display_scale = maxf(1.0, visible_height) / NATIVE_HEIGHT
	if not _ready:
		visual_root = Node2D.new()
		visual_root.name = "ApprovedSmallCharacter"
		add_child(visual_root)
		skeleton = Skeleton2D.new()
		skeleton.name = "OriginalApprovedSkeleton"
		visual_root.add_child(skeleton)
		# Bone2D ancestry must reach Skeleton2D through Bone2D nodes only.
		# Use the skeleton itself as the visual root, avoiding an intervening Node2D.
		rig_root_node = skeleton
		skin = SkinScript.new()
		_ready = skin.build(self, art_directory)
		if not _ready:
			push_error("Approved queue small character: " + str(skin.validation_errors))
			return false
		skin.set_visible(true)
		# Ground shadow must not inherit the airborne skeleton offset.
		skin.contact_shadow.reparent(visual_root, false)
		_normalize_character_layers(visual_root, 0)
	_apply_scale()
	update_visual(0.0, "idle", 0.0, 1.0)
	return true

func set_facing(facing_left: bool) -> void:
	_facing_left = facing_left
	_apply_scale()

func _apply_scale() -> void:
	if visual_root != null:
		visual_root.scale = Vector2(-_display_scale if _facing_left else _display_scale, _display_scale)

func set_paused(paused: bool) -> void:
	_paused = paused

func set_motion(local_velocity: Vector2) -> void:
	# C# supplies displacement/second in the configured character's local units.
	_motion_velocity = local_velocity if local_velocity.is_finite() else Vector2.ZERO

func set_travel_mode(mode: String) -> void:
	var next_mode := mode if mode in ["approach","retreat"] else "idle"
	if next_mode != _travel_mode:
		_travel_progress = 0.0
	_travel_mode = next_mode

func set_travel_progress(progress: float) -> void:
	if not _paused:
		_travel_progress = clampf(progress, 0.0, 1.0)

func update_visual(delta: float, action: String, elapsed: float, duration: float) -> void:
	if not _ready or _paused:
		return
	var step := clampf(delta, 0.0, 0.05)
	_clock += step
	_action = action if action in ["idle", "attack", "hurt", "guard", "dodge"] else "idle"
	_elapsed = maxf(0.0, elapsed)
	_duration = maxf(0.001, duration)
	var p := clampf(_elapsed / _duration, 0.0, 1.0)
	var facing := -1.0 if _facing_left else 1.0
	# Sliding is intentional: fast staging keeps a readable, bounded walking pace.
	_source_velocity = _motion_velocity / maxf(_display_scale,0.001)
	_gait = "idle" if _source_velocity.length()<1.0 else ("backward" if _source_velocity.x*facing<0.0 else "forward")
	var prior_phase: float = skin.gait_phase
	if skin.motion_facing != facing:
		prior_phase = fposmod(0.5-prior_phase,1.0)
	var visual_velocity := _source_velocity.limit_length(skin.QUEUE_MOVE_SPEED)
	skin.set_motion_context(step,visual_velocity,_clock,facing)
	var target_amount: float = clampf(_source_velocity.length()/skin.QUEUE_MOVE_SPEED,0.0,1.0)
	_queue_gait_amount = lerpf(_queue_gait_amount,target_amount,1.0-exp(-(15.0 if target_amount>_queue_gait_amount else 19.0)*step))
	skin.gait_amount = _queue_gait_amount
	if _source_velocity.length()>=1.0:
		skin.gait_phase = fposmod(prior_phase+step*1.8,1.0)
	_flight_height = 0.0
	_travel_stage = "walking" if _gait!="idle" else "grounded"
	_travel_lean = 0.0
	_travel_cloth = 0.0
	skeleton.position = Vector2.ZERO
	skin.contact_shadow.scale = Vector2.ONE
	skin.contact_shadow.modulate = Color.WHITE
	skin.set_action_context(_action == "guard", _action == "attack" and _elapsed < 0.15)
	var clip := "Idle"
	var sample := _clock
	var clip_duration := 3.4
	if _action == "attack":
		clip = "AttackIdle"
		# Existing queue contact remains at 0.30s; no animation-side damage event.
		var contact_time := minf(0.30, _duration * 0.60)
		sample = (_elapsed / maxf(contact_time,0.001)) * 0.40 if _elapsed <= contact_time else 0.40 + ((_elapsed-contact_time) / maxf(_duration-contact_time,0.001)) * 0.60
		sample = clampf(sample,0.0,1.0)
		clip_duration = 1.0

	elif _gait!="idle" or skin.gait_amount>0.08:
		clip = "BackstepWalk" if skin.gait_backward>0.5 else "Run_New1"

	_clip=clip
	skin.update_cloth(clip, sample, clip_duration, {})
	_apply_queue_expression(p)
	if _action == "idle" and _gait == "idle":
		var variant: float = {"male_clothes_03":-0.025,"male_clothes_04":0.018,"male_clothes_06":0.032}.get(_outfit_id,0.0)
		skin.bones.near_upper.rotation += float(variant)
		skin.bones.far_upper.rotation -= float(variant)*0.65
	skeleton.force_update_transform()

func _apply_queue_expression(p: float) -> void:
	var torso: Bone2D = skin.bones.torso
	var head: Bone2D = skin.bones.head
	var pelvis: Bone2D = skin.bones.pelvis
	visual_root.modulate = Color.WHITE
	if _action == "attack":
		var preparation := smoothstep(0.0,0.10,_elapsed) * (1.0-smoothstep(0.10,0.23,_elapsed))
		var strike := smoothstep(0.20,0.30,_elapsed) * (1.0-smoothstep(0.32,_duration,_elapsed))
		torso.rotation += -0.055*preparation + 0.045*strike
		head.rotation += 0.035*preparation - 0.025*strike
		# Tiny hip load is absorbed by the robe. Grounded feet stay untouched.
		pelvis.position.x += -0.65*preparation + 0.45*strike
	elif _action == "hurt":
		var recoil := 1.0-smoothstep(0.08,0.88,p)
		torso.rotation -= 0.17*recoil
		head.rotation += 0.10*recoil
		torso.position.x -= 1.5*recoil
		skin._aim_arm("near",1.45-0.28*recoil,0.4-0.8*recoil,-0.07)
		skin._aim_arm("far",1.95,1.0+0.2*recoil,0.04)
		visual_root.modulate = Color(1.0,1.0-0.15*recoil,1.0-0.15*recoil,1.0)
	elif _action == "guard":
		var guard := smoothstep(0.0,0.09,p)*(1.0-smoothstep(0.72,1.0,p))
		torso.rotation -= 0.045*guard
		head.rotation += 0.025*guard
		skin._aim_arm("near",lerpf(1.72,1.40,guard),lerpf(1.05,-0.85,guard),-0.05*guard)
		skin._aim_arm("far",lerpf(1.94,1.72,guard),lerpf(1.45,-0.22,guard),0.02)
		skin.arm_materials.near.set_shader_parameter("open_amount",guard*0.78)
	elif _action == "dodge":
		var dodge := sin(p*PI)
		torso.rotation -= 0.12*dodge
		head.rotation += 0.07*dodge
		skin._aim_arm("near",1.62+0.10*dodge,0.52-0.58*dodge,-0.05)
		skin._aim_arm("far",2.0,1.2,0.03)

func get_local_visible_bounds() -> Rect2:
	# Layout is measured from the approved rest figure and never from a raised arm.
	return Rect2(Vector2(-25.0,-NATIVE_HEIGHT)*_display_scale,Vector2(50.0,NATIVE_HEIGHT)*_display_scale)

func get_anchor(which: String) -> Vector2:
	if not _ready:
		return Vector2.ZERO
	if which == "feet":
		return Vector2.ZERO
	if which == "contact":
		return Vector2(25.5,-87.0)*_display_scale
	var key := "head" if which == "head" else ("near_hand" if which == "hand" else "torso")
	if which == "shoulder_near": key = "near_upper"
	if which == "shoulder_far": key = "far_upper"
	var point := rig_root_node.to_local(skin.bones[key].global_position)
	if which == "head":
		point += Vector2(0.0,-8.0)
	return (point+skeleton.position)*_display_scale

func get_debug_state() -> Dictionary:
	return {"ready":_ready,"clock":_clock,"action":_action,"elapsed":_elapsed,"paused":_paused,
		"bone_count":skin.bones.size() if skin != null else 0,
		"mesh_count":skin.meshes.size() if skin != null else 0,"facing_left":_facing_left,
		"source":"yuxu_approved_skin_v21","wardrobe":_art_directory,"outfit_id":_outfit_id,"hair_independent":false,
		"gait":_gait,"clip":_clip,"source_velocity":_source_velocity,"gait_phase":skin.gait_phase if skin!=null else 0.0,
		"travel_mode":_travel_mode,"travel_lean":_travel_lean,"travel_cloth":_travel_cloth,
		"travel_style":"robed_walk","walking_cycle":_gait!="idle",
		"travel_progress":_travel_progress,"flight_height":_flight_height,"travel_stage":_travel_stage,
		"layer_normalizations":_layer_normalizations.duplicate(true)}

func _normalize_character_layers(node: Node, parent_layer: int) -> void:
	var desired_layer := parent_layer
	if node is CanvasItem:
		desired_layer = parent_layer + node.z_index if node.z_as_relative else node.z_index
		if not node.z_as_relative:
			# The original standalone actor used absolute 55/56/59 for nested
			# skirt/lining/shoulder covers. A battle parent's Z lifts legs but
			# leaves those covers behind. Preserve the original local ordering,
			# while letting EVERY piece inherit its character's scene depth.
			var old_layer: int = node.z_index
			node.z_index = desired_layer-parent_layer
			node.z_as_relative = true
			_layer_normalizations.append({"node":str(node.name),"old_absolute":old_layer,
				"new_relative":node.z_index,"character_layer":desired_layer})
	for child in node.get_children():
		_normalize_character_layers(child, desired_layer)

func _effective_z(node: CanvasItem) -> int:
	var result := 0
	var cursor: Node = node
	while cursor != null:
		if cursor is CanvasLayer:
			break
		if cursor is CanvasItem:
			result += cursor.z_index
			if not cursor.z_as_relative:
				break
		cursor = cursor.get_parent()
	return result

func get_layer_state() -> Array[Dictionary]:
	var layers: Array[Dictionary] = []
	if not _ready:
		return layers
	for mesh in rig_root_node.find_children("*", "Polygon2D", true, false):
		layers.append({"name":str(mesh.name),"path":str(visual_root.get_path_to(mesh)),
			"relative":mesh.z_as_relative,"z":mesh.z_index,"effective_z":_effective_z(mesh)})
	return layers

func _exit_tree() -> void:
	# Detach skin bindings before the queued renderer update sees deleted bones.
	if is_instance_valid(rig_root_node):
		for child in rig_root_node.get_children():
			if child is Polygon2D:
				child.skeleton = NodePath("")
				child.clear_bones()
