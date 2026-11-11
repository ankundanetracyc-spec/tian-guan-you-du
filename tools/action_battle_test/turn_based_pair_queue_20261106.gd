extends Node2D

## Appearance adapter for the original queue events. It never emits combat effects.
const MaleRuntime = preload("guigu_native_first_runtime.gd")
const FemaleRuntime = preload("turn_based_female_runtime_20261106.gd")
var actor: Node2D
var gender := 0
var display_scale := 1.0
var clock := 0.0
var facing_left := false
var paused := false
var motion := Vector2.ZERO
var action := "idle"
var elapsed := 0.0
var travel_mode := "idle"
var travel_progress := 0.0
var source_velocity := Vector2.ZERO

func configure(height: float, _art_directory: String = "", outfit_id: String = "turn_basic_male_20261106") -> bool:
	gender = 1 if outfit_id.contains("female") else 0
	if actor == null:
		actor = FemaleRuntime.new() if gender == 1 else MaleRuntime.new()
		add_child(actor)
		if not actor.build(): return false
		actor.set_skin_mode("experiment")
	display_scale = height / float(actor.wanderer_skin.native_height)
	actor.wanderer_skin.display_scale = display_scale
	update_visual(0.0,"idle",0.0,1.0)
	return true

func set_facing(left: bool) -> void:
	facing_left = left
	if actor != null: actor.scale = Vector2(-display_scale if left else display_scale,display_scale)

func set_paused(value: bool) -> void:
	paused = value

func set_motion(value: Vector2) -> void:
	motion = value if value.is_finite() else Vector2.ZERO

func set_travel_mode(value: String) -> void:
	travel_mode = value

func set_travel_progress(value: float) -> void:
	travel_progress = clampf(value,0.0,1.0)

func update_visual(delta: float, kind: String, time: float, duration: float) -> void:
	if actor == null or paused: return
	clock += maxf(delta,0.0)
	action = kind
	elapsed = time
	var p := clampf(time/maxf(duration,0.001),0.0,1.0)
	var skin: RefCounted = actor.wanderer_skin
	var facing := -1.0 if facing_left else 1.0
	source_velocity = motion / maxf(display_scale,0.001)
	# Queue movement stays a staging event. The source rig supplies bounded steps.
	var gait_velocity := source_velocity.limit_length(235.0)
	skin.set_motion_context(maxf(delta,0.0),gait_velocity,clock,facing)
	skin.set_action_context(kind == "guard",kind == "attack" and time<0.15)
	var clip := "Run_New1" if gait_velocity.length()>0.5 else "Idle"
	var sample := clock
	if kind == "attack":
		clip = "AttackIdle"
		sample = time/0.30*0.40 if time<=0.30 else 0.40+(time-0.30)/maxf(duration-0.30,0.001)*0.60
	elif kind == "guard":
		clip = "AttackIdle"
		sample = 0.12
	elif kind == "dodge":
		clip = "Step1"
		sample = p * float(skin.clips[clip].duration)
	actor.set_character_pose(clip,sample,facing)
	# Reapply from the saved base pose so repeated zero-delta calls never add rotations.
	if kind in ["hurt","guard"] and not skin.pre_weapon_world.is_empty():
		skin.world_transforms = skin.pre_weapon_world.duplicate()
		var pivot: Vector3 = (skin.world_transforms[skin.attachment_paths.torso] as Transform3D).origin
		var angle := -0.12*sin(p*PI) if kind == "hurt" else 0.035*sin(p*PI)
		skin._rotate_source_branch(skin.attachment_paths.torso,pivot,Quaternion(Vector3.BACK,angle))
		skin._finish_pose(0.0,clip,sample)

func get_local_visible_bounds() -> Rect2:
	if actor == null: return Rect2()
	var height: float = actor.wanderer_skin.native_height*display_scale
	return Rect2(-height*0.26,-height,height*0.52,height)

func get_anchor(which: String) -> Vector2:
	if actor == null or which == "feet": return Vector2.ZERO
	var skin: RefCounted = actor.wanderer_skin
	var key := "head" if which == "head" else "torso"
	if which in ["hand","contact"]: key = "near_hand"
	if which == "shoulder_near": key = "near_upper"
	if which == "shoulder_far": key = "far_upper"
	var point: Vector2 = skin.bones[key].position + actor.rig_root_node.position
	if which == "head": point += Vector2(0,-8)
	return point*display_scale

func get_debug_state() -> Dictionary:
	return {"ready":actor != null and actor.loaded,"gender":gender,"clock":clock,"action":action,"elapsed":elapsed,"paused":paused,"bone_count":actor.wanderer_skin.rig_data.nodes.size() if actor!=null else 0,"mesh_count":actor.wanderer_skin.meshes.size() if actor!=null else 0,"source":"turn_based_pair_20261106","outfit_id":"turn_basic_female_20261106" if gender==1 else "turn_basic_male_20261106","facing_left":facing_left,"clip":actor.wanderer_skin.last_clip if actor!=null else "","travel_mode":travel_mode,"travel_progress":travel_progress,"source_velocity":source_velocity,"hair_independent":false,"visual_only":true}
