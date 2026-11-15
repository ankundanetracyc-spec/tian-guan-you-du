extends RefCounted

## Four inspection swings only. Rotates the original source branches in 3D;
## no retargeting, joint translations, combat timings, hitboxes or damage.
const FORM_NAMES := ["外横斩","回横斩","下劈","上挑"]
const HELD := [Vector2(55,0),Vector2(-15,0),Vector2(-21,0)]
# Angles are (screen-down pitch, world yaw). A horizontal cut crosses 90
# degrees in yaw, so its actual projected tip travels across the character.
const PREPARED := [
	[Vector2(55,125),Vector2(-10,110),Vector2(2,125)],
	[Vector2(50,-45),Vector2(-8,-40),Vector2(-2,-55)],
	[Vector2(-17,-6),Vector2(-82,-6),Vector2(-103,-6)],
	[Vector2(75,-6),Vector2(30,-6),Vector2(55,-6)]
]
const FOLLOWED := [
	[Vector2(50,-35),Vector2(-8,-40),Vector2(-2,-45)],
	[Vector2(55,125),Vector2(-10,110),Vector2(2,125)],
	[Vector2(72,-6),Vector2(20,-6),Vector2(48,-6)],
	[Vector2(-12,-6),Vector2(-76,-6),Vector2(-101,-6)]
]

var near_arm: Polygon2D
var body: Polygon2D
var near_arm_base_z := 35
var body_base_z := 33
var body_path := ""

func build(skin: RefCounted) -> void:
	for record in skin.mesh_records:
		if int(record.sprite_id)==79543:
			near_arm = record.mesh
			near_arm_base_z = near_arm.z_index
		elif int(record.sprite_id)==85649:
			body = record.mesh
			body_base_z = body.z_index
			body_path = str(record.renderer_path)

func _direction(angles: Vector2) -> Vector3:
	var pitch := deg_to_rad(angles.x)
	var yaw := deg_to_rad(angles.y)
	return Vector3(cos(pitch)*cos(yaw),-sin(pitch),cos(pitch)*sin(yaw))

func _hand_basis(angles: Vector2) -> Basis:
	var pitch := deg_to_rad(angles.x)
	var yaw := deg_to_rad(angles.y)
	var forward := _direction(angles)
	var up := Vector3(sin(pitch)*cos(yaw),cos(pitch),sin(pitch)*sin(yaw))
	return Basis(forward,up,forward.cross(up)).orthonormalized()

func _joint_track(phase: float, held: Vector2, prepared: Vector2, followed: Vector2, index: int) -> Vector2:
	var lead := float(index)*0.025
	var result := held.lerp(prepared,smoothstep(lead,0.24+lead,phase))
	result = result.lerp(followed,smoothstep(0.29+lead,0.55+lead,phase))
	return result.lerp(held,smoothstep(0.65+lead,1.0,phase))

func restore_depth() -> void:
	if is_instance_valid(near_arm): near_arm.z_index = near_arm_base_z

func apply(skin: RefCounted, form: int, phase: float, swinging: bool) -> Dictionary:
	var angles: Array[Vector2] = []
	for index in range(3):
		angles.append(_joint_track(phase,HELD[index],PREPARED[form][index],FOLLOWED[form][index],index) if swinging else HELD[index])
	var upper: String = skin.attachment_paths.near_upper
	var lower: String = skin.attachment_paths.near_forearm
	var hand: String = skin.attachment_paths.near_hand
	var shoulder: Vector3 = (skin.world_transforms[upper] as Transform3D).origin
	var elbow: Vector3 = (skin.world_transforms[lower] as Transform3D).origin
	skin._rotate_source_branch(upper,shoulder,Quaternion((elbow-shoulder).normalized(),_direction(angles[0])))
	elbow = (skin.world_transforms[lower] as Transform3D).origin
	var wrist: Vector3 = (skin.world_transforms[hand] as Transform3D).origin
	skin._rotate_source_branch(lower,elbow,Quaternion((wrist-elbow).normalized(),_direction(angles[1])))
	wrist = (skin.world_transforms[hand] as Transform3D).origin
	var current: Basis = (skin.world_transforms[hand] as Transform3D).basis.orthonormalized()
	var rotation := _hand_basis(angles[2])*current.transposed()
	skin._rotate_source_branch(hand,wrist,rotation.get_rotation_quaternion())
	skin._refresh_attachments()
	var body_depth := float((skin.world_transforms[body_path] as Transform3D).origin.z)
	# Source rendering uses smaller Z in front. The same original right-arm
	# mesh follows the palm depth so the original fingers and the hilt always
	# enter/leave the clothing plane together; the source arm stays one mesh.
	var arm_depth := wrist.z
	near_arm.z_index = body_base_z-1 if swinging and arm_depth>body_depth else near_arm_base_z
	var flex := rad_to_deg(_direction(angles[0]).angle_to(_direction(angles[1])))
	var wrist_bend := rad_to_deg(_direction(angles[1]).angle_to(_direction(angles[2])))
	return {"form_index":form,"form_name":FORM_NAMES[form],"upper_pitch_yaw":angles[0],"forearm_pitch_yaw":angles[1],"blade_pitch_yaw":angles[2],"elbow_flex_degrees":flex,"wrist_bend_degrees":wrist_bend,"shoulder_3d":shoulder,"elbow_3d":elbow,"wrist_3d":wrist,"upper_length_3d":shoulder.distance_to(elbow),"lower_length_3d":elbow.distance_to(wrist),"body_depth":body_depth,"arm_depth":arm_depth,"arm_z_index":near_arm.z_index,"body_z_index":body_base_z,"near_arm_base_z_index":near_arm_base_z,"rotations_only":true}
