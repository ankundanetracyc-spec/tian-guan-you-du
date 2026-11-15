extends "res://tools/action_battle_test/yuxu_approved_skin.gd"

## Queue-only narrow-robe adaptation. Keep the approved complete leg meshes,
## joints and robe; constrain the actual gait, rather than cutting legs away.
const QUEUE_STRIDE := 16.0
const QUEUE_MOVE_SPEED := 38.0

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	var original := "res://tools/action_battle_test/art/yuxu_fullbody_v15"
	if not super.build(owner_runtime, original): return false
	if art_dir == original: return true
	var replacements: Dictionary = {}
	for part in ["body", "arm_relaxed_v19"]:
		var image := Image.new()
		if image.load(ProjectSettings.globalize_path(art_dir.path_join(part+".png"))) != OK: return false
		if image.get_size() != Vector2i(1024,1536): return false
		image.generate_mipmaps()
		replacements[part] = ImageTexture.create_from_image(image)
	var old_body: Texture2D = textures.body
	var old_arm: Texture2D = textures.arm_relaxed_v19
	for mesh in runtime.rig_root_node.find_children("*", "Polygon2D", true, false):
		if mesh.texture == old_body: mesh.texture = replacements.body
		elif mesh.texture == old_arm: mesh.texture = replacements.arm_relaxed_v19
	textures.body = replacements.body
	textures.arm_relaxed_v19 = replacements.arm_relaxed_v19
	for material in arm_materials.values():
		material.shader = load("res://tools/action_battle_test/queue_outfit_hand_blend.gdshader")
		material.set_shader_parameter("open_hand", textures.arm_open_v19)
		material.set_shader_parameter("sword_hand", textures.arm_sword_fingers_v21)
	return true

func _foot_sample(phase: float, backwards: float) -> Vector3:
	var sample: Vector3 = super._foot_sample(phase, backwards)
	sample.x *= QUEUE_STRIDE / ROBED_STRIDE
	sample.y *= 0.12
	sample.z *= 0.50
	return sample

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	super.update_cloth(clip, time, duration, tracks)
	if clip == "BindPose":
		return
	var amount: float = gait_amount if motion_context and pose_blending else (1.0 if clip in ["Run_New1","Run_New2","BackstepWalk"] else 0.0)
	if amount <= 0.001:
		return
	var clock: float = motion_clock if motion_context else time
	var ankles := {}
	for side in ["near","far"]:
		var contact: Dictionary = ground_contacts[side]
		ankles[side] = contact.center - _sole_contact(side,contact.pitch)
	# The source run lowers the pelvis by about five units, creating a large
	# forward knee even with a shorter stride. Use the minimum reachable hip
	# height for these small grounded steps, then solve both complete legs again.
	bones.pelvis.position.y = bones.pelvis.rest.origin.y + 0.25*amount + sin(clock*TAU/3.4)*0.24*(1.0-amount)
	for side in ["near","far"]:
		var hip: Vector2 = runtime.rig_root_node.to_local(bones[side+"_thigh"].global_position)
		var reach: float = bones[side+"_calf"].rest.origin.length()+bones[side+"_foot"].rest.origin.length()-0.12
		var ankle: Vector2 = ankles[side]
		var required_y := ankle.y-sqrt(maxf(1.0,reach*reach-pow(ankle.x-hip.x,2.0)))
		bones.pelvis.position.y += maxf(0.0,required_y-hip.y)
	for side in ["near","far"]:
		_solve_leg(side,ankles[side],ground_contacts[side].pitch)
	var old_torso: float = bones.torso.rotation
	bones.torso.rotation *= lerpf(1.0,0.45,amount)
	bones.head.rotation += (old_torso-bones.torso.rotation)*0.62
