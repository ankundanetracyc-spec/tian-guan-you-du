extends "approved_outfit_fitting_room.gd"

## Continuous native fitting capture using the current renderer and assets.
func _capture_fitting(destination: String) -> void:
	if fitting_rig==null or not fitting_rig.loaded:
		get_tree().quit(1)
		return
	await get_tree().process_frame
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	facing = 1.0
	zoom = 5.0
	simulation_clock = 0.0
	var journal := []
	for frame in range(300):
		if frame==0:
			_select_hand("auto")
			_select_pose("Idle")
		if frame==30: _select_pose("Run_New1")
		if frame==150: _select_pose("Idle")
		if frame==210: _select_sword("hold")
		if frame==240: _select_pose("Run_New1")
		if frame==270: _select_sword("swing")
		_pose_step(1.0/30.0)
		await _snapshot(output.path_join("frame_%03d.png"%frame))
		if frame%15==0:
			journal.append({"frame":frame,"clip":current_clip,"hand":hand_preview,"sword":fitting_rig.wanderer_skin.approved_sword.mode,"runtime":fitting_rig.get_runtime_summary()})
	var record := {"frames":300,"fps":30,"native_window":[1000,830],"segments":{"idle":[0,29],"run":[30,149],"stop_and_settle":[150,209],"hold":[210,239],"hold_run":[240,269],"swing":[270,299]},"scope":"Current complete-arm sources and released continuous robe; native fitting scene, no image composites","journal":journal}
	FileAccess.open(output.path_join("capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify(record,"\t"))
	print("APPROVED FULL ARM AND HEM DEMO COMPLETE: ",output)
	get_tree().quit()
