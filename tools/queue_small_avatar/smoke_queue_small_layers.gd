extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func _named(actor: Node) -> Dictionary:
	var result := {}
	for layer in actor.get_layer_state():
		result[layer.name] = layer
	return result

func run() -> void:
	var depth_parent := Node2D.new()
	root.add_child(depth_parent)
	var actor = load("res://tools/queue_small_avatar/queue_small_avatar.gd").new()
	depth_parent.add_child(actor)
	assert(actor.configure(160.0))
	await process_frame
	var fixes: Array = actor.get_debug_state().layer_normalizations
	assert(fixes.size()==3,"Rear skirt, inner facing and shoulder cap all normalized")
	print("QUEUE_LAYER_FIXES ",JSON.stringify(fixes))
	# Reproduce the old scene-depth bug with the original node properties only.
	# This test actor is isolated and never used by the playable battle scene.
	for fix in fixes:
		var mesh = actor.rig_root_node.find_child(fix.node,true,false)
		mesh.z_as_relative = false
		mesh.z_index = fix.old_absolute
	depth_parent.z_index = 40
	var before := _named(actor)
	assert(before.YuxuUnified_near_leg.effective_z > before.OverlappingRearSkirt.effective_z,
		"Original absolute rear skirt incorrectly renders behind the near leg at parent Z40")
	print("QUEUE_LAYER_BEFORE parent=40 near_leg=",before.YuxuUnified_near_leg.effective_z,
		" rear_skirt=",before.OverlappingRearSkirt.effective_z,
		" lining=",before.RearSkirtInnerFacing.effective_z,
		" shoulder_cap=",before.ShoulderUnderClothing.effective_z)
	actor._normalize_character_layers(actor.visual_root,0)
	var after := _named(actor)
	print("QUEUE_LAYER_AFTER parent=40 near_leg=",after.YuxuUnified_near_leg.effective_z,
		" rear_skirt=",after.OverlappingRearSkirt.effective_z,
		" lining=",after.RearSkirtInnerFacing.effective_z,
		" shoulder_cap=",after.ShoulderUnderClothing.effective_z)
	depth_parent.z_index = 0
	var reference := _named(actor)
	var phases := 0
	var layer_checks := 0
	for parent_z in [-100,0,20,40,100,2000]:
		depth_parent.z_index = parent_z
		for facing in [false,true]:
			actor.set_facing(facing)
			for action in ["idle","attack","hurt","guard","dodge"]:
				var duration := 0.58 if action=="attack" else 1.2
				for index in range(61):
					actor.update_visual(1.0/60.0,action,index*duration/60.0,duration)
					var current := _named(actor)
					for mesh_name in current:
						assert(current[mesh_name].relative,"All body/cover meshes follow character depth")
						assert(current[mesh_name].effective_z-parent_z == reference[mesh_name].effective_z,
							"Parent depth must not change head/sleeve/robe/leg internal ordering")
						layer_checks += 1
					for leg_name in ["YuxuUnified_near_leg","YuxuUnified_far_leg"]:
						for cover_name in ["OverlappingRearSkirt","RearSkirtInnerFacing","YuxuUnified_robe_front"]:
							assert(current[leg_name].effective_z < current[cover_name].effective_z,
								"Both legs remain behind every lower robe cover")
					phases += 1
	print("QUEUE_SMALL_LAYERS_OK phases=",phases," layer_checks=",layer_checks,
		" parent_z=-100,0,20,40,100,2000 both_facings=pass actions=5 all_mesh_order=stable")
	depth_parent.queue_free()
	await process_frame
	quit()
