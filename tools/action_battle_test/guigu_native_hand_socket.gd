extends RefCounted

## Original source hand retained; this adapter supplies only the prop socket.
var owner_ref: WeakRef
var pose_override := "auto"
var socket: Sprite2D
var grip_local := Vector3.ZERO
var grip_axis_local := Vector3.LEFT
var last: Dictionary = {}
var near_arm: Polygon2D

func build(skin: RefCounted) -> bool:
	owner_ref = weakref(skin)
	for record in skin.mesh_records:
		if int(record.sprite_id)!=79543: continue
		near_arm = record.mesh
		var uv: Array = record.source.uvConvention.sourceUvTransform
		# Center of the original three-pixel palm, inside its wrist/finger alpha.
		var local := Vector3((30.0-float(uv[1]))/float(uv[0]),(60.0-44.0-float(uv[3]))/float(uv[2]),0.0)
		grip_local = (record.bindposes[2] as Transform3D)*local
	socket = Sprite2D.new()
	socket.name = "NativeSourceGripSocket"
	socket.visible = false
	socket.z_as_relative = false
	socket.z_index = 35
	skin.runtime.rig_root_node.add_child(socket)
	update_pose()
	return true

func set_pose_override(value: String) -> void:
	pose_override = value

func update_pose() -> void:
	var skin: Variant = owner_ref.get_ref()
	if skin==null or not skin.world_transforms.has(skin.attachment_paths.near_hand): return
	var transform: Transform3D = skin.world_transforms[skin.attachment_paths.near_hand]
	var point: Vector3 = transform*grip_local
	socket.position = Vector2(point.x,-point.y)*skin.PIXELS_PER_UNIT-skin.ground_offset
	# The source hand's local +X runs from the wrist toward the fingers. The
	# registered prop points along socket -X, so socket +X points back along
	# the hand. Use the actual wrist basis, never the elbow-to-wrist segment:
	# a wrist turn must rotate the sword even when both arm joints stay put.
	var hand_axis: Vector3 = transform.basis*grip_axis_local
	var axis := Vector2(hand_axis.x,-hand_axis.y)
	if axis.length_squared()>0.00000001: socket.rotation = axis.angle()
	if is_instance_valid(near_arm): socket.z_index = near_arm.z_index
	socket.visible = false
	last = {"grip_local":grip_local,"grip_axis_local":grip_axis_local,"grip_native":socket.position,"hand_axis_native":axis.normalized(),"source":"projected original near-hand Transform3D basis; no forearm-direction substitute"}

func get_attachment_transform(_side: String = "near") -> Transform2D:
	var transform := socket.global_transform
	return Transform2D(transform.x.normalized(),transform.y.normalized(),transform.origin)

func get_weapon_projection() -> Dictionary:
	var skin: Variant = owner_ref.get_ref()
	var transform: Transform3D = skin.world_transforms[skin.attachment_paths.near_hand]
	var point: Vector3 = transform*grip_local
	var basis := transform.basis.orthonormalized()
	# PNG +Y runs toward the pommel, PNG +X across its width. Preserve the
	# projected columns, including foreshortening and a real physical turnover.
	var width_axis := -basis.y
	var length_axis := -basis.x
	return {"projection":Transform2D(Vector2(width_axis.x,-width_axis.y),Vector2(length_axis.x,-length_axis.y),socket.position),"grip_3d":point,"blade_axis_3d":basis.x,"width_axis_3d":width_axis,"length_axis_3d":length_axis}

func get_hand_node(_side: String = "near") -> Sprite2D:
	return socket

func get_diagnostics() -> Dictionary:
	return last
