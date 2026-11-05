extends "res://approved_outfit_fitting_runtime.gd"

## Diagnostic frozen copy only. Does not replace a live or default entry.
func build(_art_dir: String = "") -> bool:
	custom_art_folder = "yuxu_fullbody_v15"
	if not _build_character_hierarchy(_read_json("res://guigu_rig/rig.json")): return false
	wanderer_skin = load("res://output/approved_outfit_run_review/before/before_crossing.gd").new()
	bone_bound_art_ready = wanderer_skin.build(self,"res://art/yuxu_fullbody_v15")
	if not bone_bound_art_ready:
		load_errors.assign(wanderer_skin.validation_errors)
		return false
	_load_animation_tracks(_read_json("res://guigu_rig/animations.json"))
	loaded = true
	set_skin_mode("experiment")
	return true
