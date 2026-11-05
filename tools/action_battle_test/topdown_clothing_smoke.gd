extends SceneTree

var arena: Node2D
var count := 0
var failures := 0
var output_dir := "res://../../output/topdown_clothing_20261104"


func _initialize() -> void:
 call_deferred("run_checks")


func check(value: bool, label: String) -> void:
 count += 1
 if not value:
  failures += 1
  push_error(label)


func run_checks() -> void:
 arena = load("res://TopDownBattleTrial.tscn").instantiate()
 root.add_child(arena)
 arena.set_process(false)
 arena.restart_trial()
 await process_frame
 var rig: Node2D = arena.avatar
 check(rig.skeleton is Skeleton2D and rig.skeleton.get_bone_count() == 26, "真实Skeleton2D包含26根骨骼")
 check(rig.meshes.size() == 10 and rig.garment_keys.size() == 5, "衣服和身体部件为独立蒙皮网格")
 var blended := false
 var valid := true
 for mesh: Polygon2D in rig.meshes.values():
  valid = valid and mesh.skeleton == mesh.get_path_to(rig.skeleton) and mesh.get_bone_count() > 0
  for vertex in range(mesh.polygon.size()):
   var total := 0.0
   var contributors := 0
   for index in range(mesh.get_bone_count()):
    var weights := mesh.get_bone_weights(index)
    valid = valid and weights.size() == mesh.polygon.size() and weights[vertex] >= 0
    total += weights[vertex]
    contributors += 1 if weights[vertex] > 0.001 else 0
   valid = valid and absf(total - 1.0) < 0.0001
   blended = blended or contributors > 1
 check(valid, "每个网格使用真实骨骼路径且逐顶点权重归一化")
 check(blended, "衣料存在跨关节混合权重")
 var socket_id: int = rig.weapon_socket.get_instance_id()
 var skeleton_id: int = rig.skeleton.get_instance_id()
 var hashes: Dictionary = {}
 for profile: Dictionary in rig.library.PROFILES:
  check(arena.apply_clothing(profile.id), "真实衣着身份可试装 " + str(profile.id))
  var mesh: Polygon2D = rig.meshes.left_sleeve
  var bitmap := mesh.texture.get_image()
  hashes[bitmap.get_data().hex_encode().sha256_text()] = true
  check(bitmap.get_used_rect().has_area() and rig.skeleton.get_instance_id() == skeleton_id and rig.weapon_socket.get_instance_id() == socket_id, "换装有透明材质且保留骨架和武器挂点 " + str(profile.id))
 check(hashes.size() == 7, "七套袖料轮廓与纹样材质各有实际差异")
 var original_id: String = rig.outfit_id
 check(not arena.apply_clothing("female_clothes_05") and rig.outfit_id == original_id, "未制作的身份不会被冒充为已支持换装")
 arena.apply_clothing("male_clothes_05")
 rig.movement = 0
 rig.attack = 0
 rig.rotation = 0
 rig.update_pose(0)
 var rest_vertices: PackedVector2Array = rig.get_deformed_vertices("left_sleeve")
 rig.attack = 1
 rig.update_pose(0.016)
 var attack_vertices: PackedVector2Array = rig.get_deformed_vertices("left_sleeve")
 var changed := 0
 for index in range(rest_vertices.size()):
  changed += 1 if rest_vertices[index].distance_to(attack_vertices[index]) > 0.2 else 0
 check(changed > 30, "出掌实际改变衣袖蒙皮，不是整张人物旋转")
 var finite := true
 for pose in range(64):
  rig.rotation = pose * TAU / 64
  rig.motion = pose * 0.45
  rig.movement = 1
  rig.local_velocity = Vector2.from_angle(pose * TAU / 32)
  rig.attack = float(pose % 9) / 8
  rig.guarding = pose % 13 == 0
  rig.dashing = pose % 17 == 0
  rig.update_pose(1.0 / 30)
  for key in rig.garment_keys:
   for point in rig.get_deformed_vertices(key):
    finite = finite and point.is_finite() and point.length() < 180
 check(finite, "64个朝向及动作相位衣料顶点有限且不爆散")
 rig.rotation = 0
 rig.local_velocity = Vector2.ZERO
 rig.update_pose(0.016)
 rig.rotation = PI * 0.5
 rig.update_pose(0.016)
 check(absf(rig.spring_angles.robe_left_hem) > 0.001, "转身驱动衣摆惯性")
 var frozen: Dictionary = rig.spring_angles.duplicate(true)
 rig.update_pose(0)
 check(rig.spring_angles == frozen, "零模拟步长冻结衣摆惯性")
 rig.attack = 0
 rig.guarding = false
 rig.dashing = false
 rig.update_pose(0)
 var prop := Node2D.new()
 prop.name = "AttachmentProbe"
 rig.mount_visual(prop)
 var old_position := prop.global_position
 rig.attack = 1
 rig.update_pose(0.016)
 check(prop.get_parent() == rig.weapon_socket and prop.global_position.distance_to(old_position) > 1, "手持挂点随手臂骨骼出掌而移动")
 check(rig.offhand_socket.get_parent() == rig.bones.right_hand, "副手也有独立骨骼挂点")
 var socket_direction: Vector2 = (rig.weapon_socket.to_global(Vector2.RIGHT) - rig.weapon_socket.global_position).normalized()
 var forearm_direction: Vector2 = (rig.bones.left_hand.global_position - rig.bones.left_forearm.global_position).normalized()
 check(socket_direction.dot(forearm_direction) > 0.999, "主手挂点前向与前臂握持方向一致")
 var hp: int = arena.hp
 var stamina: float = arena.stamina
 arena.use_skill("拳掌")
 var cooldown: float = arena.skill_cooldown_remaining("拳掌")
 arena.toggle_clothing()
 arena._process(0.05)
 arena.apply_clothing("male_clothes_04")
 check(arena.hp == hp and arena.stamina == stamina and arena.skill_cooldown_remaining("拳掌") == cooldown, "衣着试装暂停战斗并且不改费用、生命或冷却")
 arena.toggle_clothing()
 if "--capture" in OS.get_cmdline_user_args():
  await capture_views()
 print("TOPDOWN_CLOTHING_CHECKS ", count, " failures=", failures)
 arena.saved_window_state.clear()
 arena.queue_free()
 await process_frame
 quit(1 if failures else 0)


func save_frame(filename: String) -> void:
 arena.queue_redraw()
 arena.battle_hud.queue_redraw()
 await process_frame
 await RenderingServer.frame_post_draw
 var folder := ProjectSettings.globalize_path(output_dir)
 DirAccess.make_dir_recursive_absolute(folder)
 check(root.get_texture().get_image().save_png(folder.path_join(filename)) == OK, "真实窗口截图 " + filename)


func click_at(point: Vector2) -> void:
 root.warp_mouse(point * arena.battle_hud.ui_scale)
 await process_frame
 var event := InputEventMouseButton.new()
 event.button_index = MOUSE_BUTTON_LEFT
 event.pressed = true
 event.position = point * arena.battle_hud.ui_scale
 Input.parse_input_event(event)
 Input.flush_buffered_events()
 event = InputEventMouseButton.new()
 event.button_index = MOUSE_BUTTON_LEFT
 event.position = point * arena.battle_hud.ui_scale
 Input.parse_input_event(event)
 Input.flush_buffered_events()
 await process_frame


func capture_views() -> void:
 arena.clothing_open = true
 await click_at(arena.battle_hud.clothing_row_rect(2).get_center())
 check(arena.avatar.outfit_id == "male_clothes_02", "真实鼠标点击衣着行更换俯视蒙皮")
 await click_at(arena.battle_hud.canvas_size * 0.5 + Vector2(431, -203))
 check(not arena.clothing_open, "真实鼠标关闭试装面板")
 arena.restart_trial()
 arena.avatar.rotation = 0
 arena.apply_clothing("male_clothes_05")
 arena.player = Vector2(490, 430)
 arena.enemy = Vector2(800, 390)
 arena.aim_direction = Vector2.RIGHT
 arena._update_actors()
 await save_frame("01_clothing_arena.png")
 arena.toggle_clothing()
 arena.battle_hud.preview_action = 1
 for profile: Dictionary in arena.avatar.library.PROFILES:
  arena.apply_clothing(profile.id)
  arena.battle_hud._process(0.033)
  await save_frame(str(profile.id) + ".png")
 arena.apply_clothing("male_clothes_05")
 arena.battle_hud.preview_action = 2
 arena.battle_hud.preview_clock = 0.18
 arena.battle_hud._process(0.016)
 await save_frame("02_sleeve_attack.png")
