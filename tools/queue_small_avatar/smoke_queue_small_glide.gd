extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var actor=load("res://tools/queue_small_avatar/queue_small_avatar.gd").new()
	root.add_child(actor)
	assert(actor.configure(160.0))
	await process_frame
	var checked:=0
	var max_lift:=0.0
	var peak_height:=0.0
	for facing in [false,true]:
		actor.set_facing(facing)
		for mode in ["approach","retreat"]:
			actor.set_travel_mode(mode)
			var direction:=(-1.0 if facing else 1.0)*(1.0 if mode=="approach" else -1.0)
			for frame in range(90):
				var speed:=direction*700.0*sin(float(frame)*PI/90.0)
				actor.set_motion(Vector2(speed,0))
				actor.set_travel_progress(float(frame)/89.0)
				actor.update_visual(1.0/60.0,"idle",0,1)
				var state:Dictionary=actor.get_debug_state()
				assert(state.travel_mode==mode)
				peak_height=maxf(peak_height,state.flight_height)
				assert(is_equal_approx(actor.skeleton.position.y,-float(state.flight_height)))
				assert(state.clip=="Idle" and not state.walking_cycle)
				assert(is_zero_approx(actor.skin.gait_amount),"No high-speed marching or raised knees")
				for side in ["near","far"]:
					var contact:Dictionary=actor.skin.ground_contacts[side]
					var ankle:Vector2=actor.rig_root_node.to_local(actor.skin.bones[side+"_foot"].global_position)
					var sole:Vector2=actor.skin._sole_contact(side,contact.pitch)
					max_lift=maxf(max_lift,absf((ankle+sole).y))
					assert(absf((ankle+sole).y)<3.0,"Leg tuck remains confined")
					assert(absf(actor.skin.contact_shadow.position.y)<0.001,"Shadow stays on ground")
					if state.travel_progress<0.15 or state.travel_progress>0.82:
						assert(absf((ankle+sole).y)<0.01,"Push-off and landing sole stays grounded")
				if frame>20 and frame<60:
					assert(state.travel_lean>0.0 if mode=="approach" else state.travel_lean<0.0)
				checked+=1
			actor.set_travel_mode("idle")
			actor.set_motion(Vector2.ZERO)
			for frame in range(90): actor.update_visual(1.0/60.0,"idle",0,1)
			assert(absf(actor.get_debug_state().travel_lean)<0.001)
			assert(absf(actor.get_debug_state().travel_cloth)<0.001)
	assert(peak_height>22.5,"Flight must visibly separate shoes from shadow")
	actor.set_travel_mode("approach")
	actor.set_travel_progress(0.45)
	actor.update_visual(0.05,"idle",0,1)
	var frozen:Dictionary=actor.get_debug_state()
	actor.set_paused(true)
	actor.set_travel_progress(0.9)
	actor.update_visual(0.1,"attack",0.3,0.58)
	assert(actor.get_debug_state().travel_progress==frozen.travel_progress)
	assert(actor.get_debug_state().flight_height==frozen.flight_height)
	assert(actor.get_debug_state().travel_lean==frozen.travel_lean)
	assert(actor.get_debug_state().clock==frozen.clock)
	print("QUEUE_SMALL_FLIGHT_OK phases=",checked," approach_retreat=pass both_facings=pass no_walk_cycle=pass max_sole_lift=",max_lift," peak_height=",peak_height," shadow_grounded=pass settle_pause=pass")
	actor.queue_free()
	await process_frame
	quit()

