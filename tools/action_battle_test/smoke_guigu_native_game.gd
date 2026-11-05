extends Node2D

var checks := 0
var failures: Array[String] = []
var maximum_arm_error := 0.0
var maximum_grip_error := 0.0
var maximum_grip_wrist_distance := 0.0

func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		push_error(label)

func _ready() -> void:
	call_deferred("_run")

func _run() -> void:
	var arena: Node2D = load("res://tscn/scene/ActionBattleOriginalWandererScene.tscn").instantiate()
	add_child(arena)
	arena.set_process(false)
	arena.set_process_input(false)
	var skin: Variant = arena.original_wanderer.wanderer_skin
	_check(arena.original_wanderer.native_source_mode,"actual arena selects original male101 source pipeline")
	_check(skin.rig_data.rigRootPath=="/HumanRootMan/Root","actual source rig is male, not the female fitting hierarchy")
	_check(skin.meshes.size()==7 and skin.bones.has("near_hand"),"seven original meshes and actual hand anchor available")
	for key in ["near_hand", "head"]:
		var source: Transform3D = skin.world_transforms[skin.attachment_paths[key]]
		var expected: Vector2 = Vector2(source.origin.x, -source.origin.y) * skin.PIXELS_PER_UNIT - skin.ground_offset
		_check(skin.bones[key].position.distance_to(expected) < 0.0001, "actual " + key + " anchor is the projected original3D source point")
	_check(skin.bones.head.position.y < skin.bones.near_hand.position.y and skin.bones.near_hand.position.y < 0.0,"actual source hand/head anchors have usable height above the arena foot point")
	var world_avatar: Node2D = load("res://gdscripts/world/WorldActionTraveler.gd").new()
	add_child(world_avatar)
	_check(world_avatar.build(32.0),"actual world traveler builds the native source")
	_check(world_avatar.rig.native_source_mode,"world traveler shares native pipeline")
	_check(absf(world_avatar.native_height*world_avatar.scale.y-32.0)<0.0001,"world marker keeps its existing 32 pixel envelope")
	world_avatar.update_motion(1.0/30.0,Vector2.RIGHT*114)
	_check(world_avatar.current_clip=="Run_New1","actual world travel uses original run")
	var before := {"hp":arena.hp,"enemy_hp":arena.enemy_hp,"stamina":arena.stamina,"mana":arena.mana,"qi":arena.qi,"qi_blood":arena.qi_blood,"cd":arena.skill_cooldowns.duplicate(true)}
	var key := InputEventKey.new()
	key.keycode = KEY_9
	key.pressed = true
	arena._input(key)
	_check(arena.outfit_sword_visible and skin.approved_sword.mode=="hold","actual 9 key selects original hand with independent sword")
	key.keycode = KEY_0
	arena._input(key)
	_check(skin.approved_sword.mode=="swing","actual 0 key starts one inspection swing")
	for direction in [1.0,-1.0]:
		arena.facing = direction
		_check(arena.preview_outfit_swing(),"single source-arm swing starts in both facings")
		for frame in range(60):
			arena.motion_time += 1.0/60.0
			arena.visual_motion_delta = 1.0/60.0
			arena.visual_motion_velocity = Vector2.ZERO
			arena.update_skinned_player()
			for pair in [["near_upper", "near_forearm"], ["near_forearm", "near_hand"]]:
				var upper_path: String = skin.attachment_paths[pair[0]]
				var lower_path: String = skin.attachment_paths[pair[1]]
				var actual_upper: Transform3D = skin.world_transforms[upper_path]
				var actual_lower: Transform3D = skin.world_transforms[lower_path]
				var source_upper: Transform3D = skin.animated_world[upper_path]
				var source_lower: Transform3D = skin.animated_world[lower_path]
				maximum_arm_error = maxf(maximum_arm_error, absf(actual_upper.origin.distance_to(actual_lower.origin) - source_upper.origin.distance_to(source_lower.origin)) * skin.PIXELS_PER_UNIT)
			maximum_grip_error = maxf(maximum_grip_error,float(skin.approved_sword.last.get("attachment_error",0.0)))
			maximum_grip_wrist_distance = maxf(maximum_grip_wrist_distance, skin.native_hand_socket.socket.position.distance_to(skin.bones.near_hand.position))
		_check(skin.approved_sword.mode=="hold","single swing returns to holding")
	_check(maximum_arm_error<0.001,"source arm lengths retained during adapted holding and swinging")
	_check(maximum_grip_error<0.001,"sword stays at the original source-palm attachment")
	_check(maximum_grip_wrist_distance <= 8.0, "actual grip socket stays within8 native pixels of original near-hand anchor")
	_check(arena.hp==before.hp and arena.enemy_hp==before.enemy_hp and arena.stamina==before.stamina and arena.mana==before.mana and arena.qi==before.qi and arena.qi_blood==before.qi_blood and arena.skill_cooldowns==before.cd,"inspection swing creates no damage, expense, cooldown or resources")
	_check(arena.pending_attack.is_empty() and (arena.fist_combo==null or not arena.fist_combo.active),"inspection does not create a combat attack")
	arena.preview_outfit_swing()
	var elapsed: float = skin.approved_sword.swing_elapsed
	arena.visual_motion_delta = 0.0
	arena.update_skinned_player()
	_check(skin.approved_sword.swing_elapsed==elapsed,"pause freezes swing and source physics")
	arena.pending_attack = "拳掌"
	arena._update_outfit_weapon_context()
	_check(skin.approved_sword.mode=="off","existing unarmed attack retains its own presentation")
	arena.pending_attack = ""
	arena._update_outfit_weapon_context()
	_check(skin.approved_sword.mode=="hold","holding resumes after original unarmed action")
	key.keycode = KEY_9
	arena._input(key)
	_check(not arena.outfit_sword_visible and skin.approved_sword.mode=="off","9 key sheathes the sword")
	arena.restart_battle()
	_check(not skin.physics_initialized,"actual restart discards pre-restart cloth transport history")
	arena.motion_time += 1.0/60.0
	arena.visual_motion_delta = 1.0/60.0
	arena.visual_motion_velocity = Vector2.ZERO
	arena.update_skinned_player()
	var cloth_record: Dictionary = {}
	for record in skin.mesh_records:
		if int(record.sprite_id)==85649: cloth_record = record
	var source_cloth: PackedVector2Array = skin.deform_mesh(cloth_record,skin.animated_world)
	var first_pose_error := 0.0
	for vertex in range(source_cloth.size()): first_pose_error = maxf(first_pose_error,source_cloth[vertex].distance_to(cloth_record.mesh.polygon[vertex]))
	_check(first_pose_error<0.02,"placing or restarting actor does not inject a large cloth impulse")
	var cached_cloth: PackedVector2Array = cloth_record.mesh.polygon.duplicate()
	arena.update_skinned_player()
	_check(cached_cloth==cloth_record.mesh.polygon,"duplicate real-game pose update preserves the solved hem")
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"maximum_source_arm_length_error_pixels":maximum_arm_error,"maximum_grip_error_world_pixels":maximum_grip_error,"maximum_grip_to_source_hand_distance_native_pixels":maximum_grip_wrist_distance,"first_pose_after_restart_cloth_error_pixels":first_pose_error,"scope":"Actual root arena and WorldActionTraveler, original male101 skin, source hand/head anchors, independent sword holding and inspection swing, restart placement and duplicate pose stability; original attack rules retained."}
	var folder := "res://output/guigu_native_first_20261105"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(folder))
	FileAccess.open(folder.path_join("actual_game_smoke.json"),FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("NATIVE OUTFIT ACTUAL GAME ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks; ",JSON.stringify(failures))
	arena.queue_free()
	world_avatar.queue_free()
	await get_tree().process_frame
	await get_tree().create_timer(0.15).timeout
	get_tree().quit(0 if failures.is_empty() else 1)
