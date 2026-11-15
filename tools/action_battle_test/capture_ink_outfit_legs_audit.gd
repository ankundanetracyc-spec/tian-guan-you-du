extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")

func _ready() -> void:
	var ankle_only := OS.get_cmdline_user_args().has("--ankle-only")
	var knee_only := OS.get_cmdline_user_args().has("--knee-only") or ankle_only
	get_window().size = Vector2i(1200,600)
	get_window().position = Vector2i(-7000,-7000)
	var background := ColorRect.new()
	background.size = Vector2(1200,600)
	background.color = Color("cbcfc9")
	add_child(background)
	for index in range(4):
		var rig := OutfitRuntime.new()
		add_child(rig)
		rig.position = Vector2(index*300+120,570)
		rig.display_zoom = 30.0 if knee_only else 8.0
		assert(rig.build())
		rig.wanderer_skin.motion_context = false
		rig.wanderer_skin.pose_blending = false
		var clip: Dictionary = rig.wanderer_skin.source_document.clips.Run_New1
		rig.set_character_pose("Run_New1",[0.1142858,0.20,0.7142858,0.8571429][index]*float(clip.duration),1.0)
		rig.wanderer_skin.set_visible(false)
		if rig.wanderer_skin.leg_reconstruction!=null:
			for side in rig.wanderer_skin.leg_reconstruction.legs:
				var state: Dictionary = rig.wanderer_skin.leg_reconstruction.legs[side]
				for mesh in [state.upper_mesh,state.lower_mesh,state.foot_mesh,state.joint_mesh,state.ankle_joint_mesh]: mesh.visible = not knee_only or side=="far"
				if knee_only and side=="far": rig.position += Vector2(400,300)-rig.to_global(state.last.ankle if ankle_only else state.last.knee)
		else:
			for record in rig.wanderer_skin.mesh_records:
				if record.category=="leg": record.mesh.visible = true
		if knee_only: break
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var destination := "res://output/ink_outfit_legs_audit"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): destination = argument.trim_prefix("--capture-dir=")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(destination))
	get_viewport().get_texture().get_image().save_png(destination.path_join("legs.png"))
	get_tree().quit()
