extends Node2D

class SourceTestRig extends "guigu_rig_runtime.gd":
	func load_copy() -> bool:
		custom_art_folder = "yuxu_fullbody_v15"
		if not _build_character_hierarchy(_read_json("res://guigu_rig/rig.json")): return false
		if not _load_texture_sets("res://art"): return false
		if not _build_weighted_meshes(_read_json("res://guigu_rig/skins.json"),"res://art"): return false
		wanderer_skin = load("res://guigu_model_copy_skin.gd").new()
		if OS.get_cmdline_user_args().has("--research-data"):
			wanderer_skin.source_data_path = "res://output/guigu_source_reference_v2/male101/source_reference_vertex_frames.json"
		wanderer_skin.strict_required = OS.get_cmdline_user_args().has("--strict-hd")
		bone_bound_art_ready = wanderer_skin.build(self,"res://art/yuxu_fullbody_v15")
		_load_animation_tracks(_read_json("res://guigu_rig/animations.json"))
		loaded = bone_bound_art_ready
		return loaded

func _ready() -> void:
	var rig := SourceTestRig.new()
	add_child(rig)
	assert(rig.load_copy())
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	var clips := {}
	for clip in ["Idle","Run_New1","BackstepWalk","AttackIdle","AttackRun","Step1"]:
		for frame in range(24):
			var clock := float(frame+1)/60.0
			skin.set_motion_context(1.0/60.0,Vector2(114,0) if clip=="Run_New1" else Vector2.ZERO,clock,1.0)
			rig.set_character_pose(clip,clock,1.0)
		clips[clip] = {"source_clip":skin.sampled_source_clip,"mesh_count":skin.meshes.size(),"near_hand":str(skin.bones.near_hand.position) if skin.bones.has("near_hand") else "missing"}
	var frozen := PackedVector2Array(skin.meshes[0].polygon)
	skin.set_motion_context(0.0,Vector2.ZERO,1.0,-1.0)
	rig.set_character_pose("Run_New1",0.3,-1.0)
	var pause_pass: bool = frozen==skin.meshes[0].polygon
	var passed: bool = skin.meshes.size()==7 and skin.bones.has("near_hand") and skin.validation_errors.is_empty() and pause_pass
	var report := {"passed":passed,"clips":clips,"errors":skin.validation_errors,"pause_frozen":pause_pass,"summary":skin.summary()}
	var file := FileAccess.open("res://output/source_model_build_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("SOURCE MODEL BUILD ","PASS" if passed else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if passed else 1)
