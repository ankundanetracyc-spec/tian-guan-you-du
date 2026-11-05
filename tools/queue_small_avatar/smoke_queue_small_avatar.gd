extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var actor = load("res://tools/queue_small_avatar/queue_small_avatar.gd").new()
	root.add_child(actor)
	assert(actor.configure(160.0),"Approved source must load without the imported reference runtime")
	await process_frame
	assert(actor.get_debug_state().bone_count == 21)
	assert(actor.get_debug_state().mesh_count == 7)
	var checked := 0
	for facing in [false,true]:
		actor.set_facing(facing)
		for action in ["idle","attack","hurt","guard","dodge"]:
			var duration := 0.58 if action=="attack" else 1.2
			for index in range(61):
				actor.update_visual(1.0/60.0,action,duration*index/60.0,duration)
				for bone in actor.skin.bones.values():
					assert(bone.global_position.is_finite(),"Finite skeleton through every action")
					assert(bone.global_transform.determinant()>0.0 if not facing else bone.global_transform.determinant()<0.0)
				for contact in actor.skin.ground_contacts.values():
					assert(contact.center.is_finite())
				checked += 1
	var before = actor.get_debug_state()
	var hand: Vector2 = actor.get_anchor("hand")
	actor.set_paused(true)
	actor.update_visual(0.1,"attack",0.3,0.58)
	assert(actor.get_debug_state().clock == before.clock,"Pause freezes visual clock")
	assert(actor.get_anchor("hand").is_equal_approx(hand),"Pause freezes pose")
	actor.set_paused(false)
	actor.update_visual(0.02,"idle",0,1)
	assert(actor.get_debug_state().clock > before.clock,"Idle resumes")
	assert(is_equal_approx(actor.get_local_visible_bounds().size.y,160.0))
	print("QUEUE_SMALL_AVATAR_SMOKE_OK phases=",checked," bones=21 meshes=7 actions=5 both_facings pause_resume=pass")
	actor.queue_free()
	await process_frame
	quit()
