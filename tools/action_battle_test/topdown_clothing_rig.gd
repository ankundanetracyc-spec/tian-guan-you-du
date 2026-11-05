extends Node2D

# A real Skeleton2D with weighted Polygon2D clothing, plus stable hand sockets.
# All inputs below are visual snapshots; this node never mutates battle state.
const Library = preload("topdown_clothing_library.gd")
var library := Library.new()
var skeleton: Skeleton2D
var bones: Dictionary = {}
var rest_points: Dictionary = {}
var rest_globals: Dictionary = {}
var meshes: Dictionary = {}
var garment_keys: Array[String] = []
var outfit_id := "male_clothes_05"
var motion := 0.0
var movement := 0.0
var local_velocity := Vector2.ZERO
var attack := 0.0
var guarding := false
var dashing := false
var hurt := false
var defeated := false
var spring_angles: Dictionary = {}
var spring_speeds: Dictionary = {}
var spring_offsets: Dictionary = {}
var offset_speeds: Dictionary = {}
var previous_velocity := Vector2.ZERO
var previous_heading := 0.0
var heading_initialized := false
var weapon_socket: Marker2D
var offhand_socket: Marker2D
var mounted_visuals: Dictionary = {}


func _ready() -> void:
 skeleton = Skeleton2D.new()
 skeleton.name = "OverheadSkeleton"
 add_child(skeleton)
 _bone("pelvis", "", Vector2(-14, 0))
 _bone("torso", "pelvis", Vector2(-4, 0))
 _bone("head", "torso", Vector2(9, 0))
 for side in ["left", "right"]:
  var sign_y := -1.0 if side == "left" else 1.0
  _bone(side + "_upper", "torso", Vector2(-3, sign_y * 15))
  _bone(side + "_forearm", side + "_upper", Vector2(10, sign_y * 30))
  _bone(side + "_hand", side + "_forearm", Vector2(29, sign_y * 20))
  _bone(side + "_sleeve_tip", side + "_forearm", Vector2(17, sign_y * 35))
  _bone(side + "_thigh", "pelvis", Vector2(-16, sign_y * 10))
  _bone(side + "_knee", side + "_thigh", Vector2(-24, sign_y * 11))
  _bone(side + "_foot", side + "_knee", Vector2(-30, sign_y * 11))
  _bone("robe_" + side + "_root", "pelvis", Vector2(-15, sign_y * 8))
  _bone("robe_" + side + "_middle", "robe_" + side + "_root", Vector2(-28, sign_y * 12))
  _bone("robe_" + side + "_hem", "robe_" + side + "_middle", Vector2(-42, sign_y * 15))
 _bone("ribbon_root", "pelvis", Vector2(-15, 3))
 _bone("ribbon_middle", "ribbon_root", Vector2(-29, 6))
 _bone("ribbon_tip", "ribbon_middle", Vector2(-41, 11))
 weapon_socket = Marker2D.new()
 weapon_socket.name = "WeaponSocket"
 bones.left_hand.add_child(weapon_socket)
 weapon_socket.position = Vector2(4, 0)
 weapon_socket.rotation = (Vector2(rest_points.left_hand) - Vector2(rest_points.left_forearm)).angle()
 offhand_socket = Marker2D.new()
 offhand_socket.name = "OffhandSocket"
 bones.right_hand.add_child(offhand_socket)
 offhand_socket.position = Vector2(4, 0)
 offhand_socket.rotation = (Vector2(rest_points.right_hand) - Vector2(rest_points.right_forearm)).angle()
 _basic("left_foot", Rect2(-40, -17, 25, 13), ["left_foot"], -3)
 _basic("right_foot", Rect2(-40, 4, 25, 13), ["right_foot"], -3)
 _basic("left_hand", Rect2(23, -29, 18, 17), ["left_hand"], 5)
 _basic("right_hand", Rect2(23, 12, 18, 17), ["right_hand"], 5)
 _basic("head", Rect2(-8, -15, 36, 30), ["head"], 8)
 set_outfit(outfit_id)
 update_pose(0.0)


func _bone(key: String, parent_key: String, point: Vector2) -> void:
 var bone := Bone2D.new()
 bone.name = key
 bone.set_autocalculate_length_and_angle(false)
 bone.length = 8.0
 var parent: Node2D = skeleton if parent_key.is_empty() else bones[parent_key]
 parent.add_child(bone)
 bone.position = point if parent_key.is_empty() else point - Vector2(rest_points[parent_key])
 bone.rest = bone.transform
 bones[key] = bone
 rest_points[key] = point
 rest_globals[key] = Transform2D(0, point)


func set_outfit(id: String) -> bool:
 if library.get_profile(id).is_empty():
  return false
 outfit_id = id
 if skeleton == null:
  return true
 for key in garment_keys:
  var mesh: Polygon2D = meshes[key]
  mesh.get_parent().remove_child(mesh)
  mesh.queue_free()
  meshes.erase(key)
 garment_keys.clear()
 _garment("robe", Rect2(-52, -24, 48, 48), ["pelvis", "robe_left_root", "robe_left_middle", "robe_left_hem", "robe_right_root", "robe_right_middle", "robe_right_hem"], -2)
 _garment("body", Rect2(-22, -23, 46, 46), ["pelvis", "torso"], 1)
 _garment("left_sleeve", Rect2(-15, -43, 50, 34), ["torso", "left_upper", "left_forearm", "left_hand", "left_sleeve_tip"], 2)
 _garment("right_sleeve", Rect2(-15, 9, 50, 34), ["torso", "right_upper", "right_forearm", "right_hand", "right_sleeve_tip"], 2)
 _garment("ribbon", Rect2(-46, -7, 36, 25), ["pelvis", "ribbon_root", "ribbon_middle", "ribbon_tip"], 3)
 return true


func _basic(piece: String, bounds: Rect2, keys: Array, layer: int) -> void:
 _mesh(piece, bounds, keys, library.basic_texture(piece, bounds), layer)


func _garment(piece: String, bounds: Rect2, keys: Array, layer: int) -> void:
 _mesh(piece, bounds, keys, library.garment_texture(outfit_id, piece, bounds), layer)
 garment_keys.append(piece)


func _weights(piece: String, point: Vector2, keys: Array) -> PackedFloat32Array:
 var result := PackedFloat32Array()
 result.resize(keys.size())
 if keys.size() == 1:
  result[0] = 1.0
  return result
 if piece == "body":
  result[1] = smoothstep(-19, -1, point.x)
  result[0] = 1.0 - result[1]
 elif piece.ends_with("sleeve"):
  if point.x <= 0:
   result[0] = 1.0 - smoothstep(-10, 0, point.x)
   result[1] = 1.0 - result[0]
  elif point.x <= 13:
   result[2] = smoothstep(0, 13, point.x)
   result[1] = 1.0 - result[2]
  else:
   result[3] = smoothstep(17, 30, point.x)
   var cloth_tip := (1.0 - result[3]) * smoothstep(25, 40, absf(point.y)) * 0.45
   result[4] = cloth_tip
   result[2] = 1.0 - result[3] - cloth_tip
 elif piece == "ribbon":
  var t := clampf((-point.x - 13) / 29.0, 0, 1) * 3
  var index := mini(int(t), 2)
  result[index] = 1.0 - (t - index)
  result[index + 1] = t - index
 else: # Bilateral hem chains share the waist anchor and remain continuous.
  var far_weight := smoothstep(-6, 6, point.y)
  for side in range(2):
   var lateral := 1.0 - far_weight if side == 0 else far_weight
   var t := clampf((-point.x - 12) / 34.0, 0, 1) * 3
   var index := mini(int(t), 2)
   var first := 0 if index == 0 else 1 + side * 3 + index - 1
   var second := 1 + side * 3 + index
   result[first] += lateral * (1.0 - (t - index))
   result[second] += lateral * (t - index)
 return result


func _mesh(key: String, bounds: Rect2, keys: Array, texture: Texture2D, layer: int) -> void:
 var mesh := Polygon2D.new()
 mesh.name = key
 mesh.z_index = layer
 mesh.texture = texture
 mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
 var vertices := PackedVector2Array()
 var uv := PackedVector2Array()
 var triangles: Array[PackedInt32Array] = []
 var weight_sets: Array[PackedFloat32Array] = []
 var columns := 10
 var rows := 8
 for _index in keys:
  weight_sets.append(PackedFloat32Array())
 for row in range(rows + 1):
  for column in range(columns + 1):
   var fraction := Vector2(column / float(columns), row / float(rows))
   var point := bounds.position + bounds.size * fraction
   vertices.append(point)
   uv.append(fraction * Vector2(texture.get_size()))
   var weights := _weights(key, point, keys)
   for index in range(keys.size()):
    weight_sets[index].append(weights[index])
   if row < rows and column < columns:
    var a := row * (columns + 1) + column
    var b := a + columns + 1
    triangles.append(PackedInt32Array([a, a + 1, b]))
    triangles.append(PackedInt32Array([a + 1, b + 1, b]))
 mesh.polygon = vertices
 mesh.uv = uv
 mesh.polygons = triangles
 add_child(mesh)
 mesh.skeleton = mesh.get_path_to(skeleton)
 for index in range(keys.size()):
  mesh.add_bone(skeleton.get_path_to(bones[keys[index]]), weight_sets[index])
 meshes[key] = mesh


func _actor_point(bone: Bone2D) -> Vector2:
 return to_local(bone.global_position)


func _arm(side: String, target: Vector2) -> void:
 var upper: Bone2D = bones[side + "_upper"]
 var forearm: Bone2D = bones[side + "_forearm"]
 var hand: Bone2D = bones[side + "_hand"]
 var shoulder := _actor_point(upper)
 var first_length := Vector2(rest_points[side + "_forearm"]).distance_to(rest_points[side + "_upper"])
 var second_length := Vector2(rest_points[side + "_hand"]).distance_to(rest_points[side + "_forearm"])
 var offset := target - shoulder
 var distance := clampf(offset.length(), 2, first_length + second_length - 0.01)
 var angle := acos(clampf((first_length * first_length + distance * distance - second_length * second_length) / (2.0 * first_length * distance), -1, 1))
 var upper_angle := offset.angle() + angle * (-1.0 if side == "left" else 1.0)
 upper.rotation += wrapf(upper_angle - (_actor_point(forearm) - shoulder).angle(), -PI, PI)
 var elbow := _actor_point(forearm)
 var direction := (shoulder + offset.normalized() * distance - elbow).angle()
 forearm.rotation += wrapf(direction - (_actor_point(hand) - elbow).angle(), -PI, PI)
 # Keep the grip aligned with the forearm; mounted visuals inherit the wrist.
 hand.rotation = 0.0


func update_pose(delta: float) -> void:
 if skeleton == null:
  return
 var step := clampf(delta, 0, 0.05)
 var turning := 0.0
 var acceleration := Vector2.ZERO
 if heading_initialized and step > 0:
  turning = wrapf(rotation - previous_heading, -PI, PI) / step
  acceleration = (local_velocity - previous_velocity) / step
 previous_heading = rotation
 previous_velocity = local_velocity
 heading_initialized = true
 for bone in bones.values():
  bone.transform = bone.rest
 var stride := sin(motion) * movement
 bones.pelvis.position += Vector2(sin(motion * 2.0) * movement * 0.6, 0)
 bones.torso.rotation = sin(motion) * movement * 0.035
 bones.torso.position.x += sin(motion * 0.22) * 0.35
 bones.head.rotation = -bones.torso.rotation * 0.7
 for side in ["left", "right"]:
  var sign_y := -1.0 if side == "left" else 1.0
  var extension: float = attack * (14.0 if side == "left" else 5.0)
  var target := Vector2(23 + extension, sign_y * (23 - attack * 10))
  target += local_velocity.limit_length(1.0) * stride * sign_y * 2.5
  if guarding:
   target = Vector2(30, sign_y * 10)
  if dashing:
   target = Vector2(16, sign_y * 22)
  _arm(side, target)
  bones[side + "_thigh"].rotation = stride * sign_y * 0.16
  bones[side + "_knee"].rotation = -stride * sign_y * 0.1
  var step_direction := local_velocity.normalized() if local_velocity.length_squared() > 0.001 else Vector2.RIGHT
  bones[side + "_foot"].position += step_direction * stride * sign_y * 6.0
 for key in ["robe_left_root", "robe_left_middle", "robe_left_hem", "robe_right_root", "robe_right_middle", "robe_right_hem", "ribbon_root", "ribbon_middle", "ribbon_tip", "left_sleeve_tip", "right_sleeve_tip"]:
  var target := clampf(-turning * 0.018 - local_velocity.y * 0.10, -0.32, 0.32)
  var suffix := 1.0 if key.ends_with("root") else (1.25 if key.ends_with("middle") else 1.5)
  target += sin(motion * 0.5 + suffix) * movement * 0.035
  if key.contains("sleeve"):
   target *= 0.5
  if not spring_angles.has(key):
   spring_angles[key] = 0.0
   spring_speeds[key] = 0.0
   spring_offsets[key] = 0.0
   offset_speeds[key] = 0.0
  if step > 0:
   spring_speeds[key] += (target * suffix - float(spring_angles[key])) * 48.0 * step
   spring_speeds[key] *= exp(-9.0 * step)
   spring_angles[key] = clampf(float(spring_angles[key]) + float(spring_speeds[key]) * step, -0.4, 0.4)
  bones[key].rotation = spring_angles[key]
  if key.begins_with("robe"):
   var offset_target := -movement * 0.7 - clampf(acceleration.x * 0.13, -1.5, 1.5) - (1.2 if dashing else 0.0)
   if step > 0:
    offset_speeds[key] += (offset_target * suffix - float(spring_offsets[key])) * 48.0 * step
    offset_speeds[key] *= exp(-10.0 * step)
    spring_offsets[key] = clampf(float(spring_offsets[key]) + float(offset_speeds[key]) * step, -3.5, 3.5)
   bones[key].position.x += spring_offsets[key]
 modulate = Color(0.7, 0.69, 0.63, 0.7) if defeated else (Color(1.1, 0.85, 0.76) if hurt else Color.WHITE)
 queue_redraw()


func mount_visual(visual: Node2D, offhand := false) -> void:
 var key := "offhand" if offhand else "main"
 if mounted_visuals.has(key) and is_instance_valid(mounted_visuals[key]):
  var previous: Node2D = mounted_visuals[key]
  previous.get_parent().remove_child(previous)
  previous.queue_free()
 var socket: Marker2D = offhand_socket if offhand else weapon_socket
 if visual.get_parent() != null:
  visual.get_parent().remove_child(visual)
 socket.add_child(visual)
 visual.position = Vector2.ZERO
 visual.rotation = 0.0
 mounted_visuals[key] = visual


func get_deformed_vertices(key: String) -> PackedVector2Array:
 # CPU reconstruction for seam / finite-pose checks of the native skin data.
 var mesh: Polygon2D = meshes[key]
 var result := PackedVector2Array()
 for index in range(mesh.polygon.size()):
  var point := Vector2.ZERO
  for bone_index in range(mesh.get_bone_count()):
   var bone: Bone2D = skeleton.get_node(mesh.get_bone_path(bone_index))
   var current := global_transform.affine_inverse() * bone.global_transform
   var rest: Transform2D = rest_globals[bone.name]
   point += (current * rest.affine_inverse() * mesh.polygon[index]) * mesh.get_bone_weights(bone_index)[index]
  result.append(point)
 return result


func reset_motion() -> void:
 spring_angles.clear()
 spring_speeds.clear()
 spring_offsets.clear()
 offset_speeds.clear()
 previous_velocity = Vector2.ZERO
 heading_initialized = false
 update_pose(0.0)


func _draw() -> void:
 for ring in range(4):
  var points := PackedVector2Array()
  for index in range(40):
   points.append(Vector2(-6, 3) + Vector2(cos(index * TAU / 40), sin(index * TAU / 40)) * Vector2(38 - ring * 3, 28 - ring * 2))
  draw_colored_polygon(points, Color(0.16, 0.23, 0.16, 0.025))
