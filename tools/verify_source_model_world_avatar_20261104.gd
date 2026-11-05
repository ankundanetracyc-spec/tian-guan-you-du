extends SceneTree

var checks := 0
var failures: Array[String] = []

func verify(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures.append(label)
		push_error(label)

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var traveler = load("res://gdscripts/world/WorldActionTraveler.gd").new()
	root.add_child(traveler)
	verify(traveler.build(80.0),"world avatar loads the current HD character")
	if not traveler.loaded:
		quit(1)
		return
	var skin = traveler.rig.wanderer_skin
	verify(skin.strict_required and skin.all_hd_materials,"world uses the complete native HD material set")
	verify(is_equal_approx(traveler.native_height*traveler.scale.y,80.0),"world marker retains its requested display height")
	verify(is_equal_approx(traveler.native_height,skin.get_native_visual_height()),"marker ruler comes from the assembled model, not a legacy atlas")
	for frame in range(100):
		var velocity := Vector2(20,0) if frame<30 else (Vector2(-20,0) if frame<60 else Vector2.ZERO)
		traveler.update_motion(0.05,velocity)
		if frame==20: verify(traveler.current_clip=="Run_New1" and traveler.facing==1.0 and skin.sampled_source_clip=="Run_New1","world forward travel uses the current running model")
		if frame==50: verify(traveler.current_clip=="Run_New1" and traveler.facing==-1.0,"world reverse travel turns the assembled character")
	verify(traveler.current_clip=="Idle" and skin.sampled_source_clip=="Idle","stopping returns to current idle")
	var frozen := []
	for mesh in skin.meshes: frozen.append(mesh.polygon.duplicate())
	var clock: float = traveler.motion_clock
	var hand: Transform2D = skin.bones.near_hand.transform
	traveler.visible = false
	for frame in range(8): traveler.update_motion(0.0,Vector2.ZERO)
	var unchanged: bool = traveler.motion_clock==clock and skin.bones.near_hand.transform==hand
	for i in range(frozen.size()): unchanged = unchanged and frozen[i]==skin.meshes[i].polygon
	verify(unchanged,"hidden zero-delta world avatar freezes mesh and hand attachment")
	var summary: Dictionary = traveler.get_visual_summary()
	verify(summary.world_avatar_loaded and summary.clip=="Idle" and summary.facing==-1.0,"world summary stays valid after hide")
	traveler.queue_free()
	await process_frame
	print("SOURCE MODEL WORLD: ",checks," checks; failures=",failures.size())
	quit(0 if failures.is_empty() else 1)
