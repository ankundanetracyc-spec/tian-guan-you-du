extends "approved_outfit_fitting_room.gd"

## Native continuous capture: one camera/window, original UI and fitted actor.
func _capture_fitting(destination: String) -> void:
	if fitting_rig==null or not fitting_rig.loaded:
		get_tree().quit(1)
		return
	await get_tree().process_frame
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	get_window().size = Vector2i(1000,830)
	get_window().content_scale_size = Vector2i(1000,830)
	for child in get_children():
		if child is ColorRect: child.size.y = 830
	facing = 1.0
	zoom = 5.0
	simulation_clock = 0.0
	var journal := []
	for frame in range(180):
		if frame==0: _select_sword("hold")
		if frame==60: _select_pose("Run_New1")
		if frame==120: _select_sword("swing")
		_pose_step(1.0/30.0)
		await _snapshot(output.path_join("sword_demo_%03d.png"%frame))
		journal.append({"frame":frame,"seconds":float(frame+1)/30.0,"clip":current_clip,"mode":fitting_rig.wanderer_skin.approved_sword.mode,"swing_phase":fitting_rig.wanderer_skin.approved_sword.last.get("phase",0.0),"actor_position":fitting_rig.position,"zoom":zoom,"facing":facing})
	var file := FileAccess.open(output.path_join("sword_demo_record.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"frames":180,"fps":30,"native_window":[1000,830],"segments":{"hold_idle":[0,59],"hold_run":[60,119],"swing":[120,179]},"runtime":fitting_rig.get_runtime_summary(),"journal":journal,"scope":"native renderer continuous frames; no cropped images or composites"},"\t"))
	file.close()
	print("APPROVED OUTFIT SWORD DEMO CAPTURE COMPLETE: ",output)
	get_tree().quit()
