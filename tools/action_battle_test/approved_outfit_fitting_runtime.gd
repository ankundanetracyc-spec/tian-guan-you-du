extends "guigu_rig_runtime.gd"

## Independent review of the user's selected v15 artwork. Shared game entry
## points and the original textures/animation exports remain unchanged.
var display_zoom := 5.0

func build(_art_dir: String = "") -> bool:
	custom_art_folder = "yuxu_fullbody_v15"
	var resource_dir: String = get_script().resource_path.get_base_dir()
	if not _build_character_hierarchy(_read_json(resource_dir.path_join("guigu_rig/rig.json"))): return false
	wanderer_skin = load(resource_dir.path_join("approved_outfit_crossing_skin.gd")).new()
	bone_bound_art_ready = wanderer_skin.build(self,resource_dir.path_join("art/yuxu_fullbody_v15"))
	if not bone_bound_art_ready:
		load_errors.assign(wanderer_skin.validation_errors)
		return false
	_load_animation_tracks(_read_json(resource_dir.path_join("guigu_rig/animations.json")))
	loaded = true
	set_skin_mode("experiment")
	return true

func set_character_pose(clip_name: String, time: float, facing: float = 1.0, hit_tint: float = 0.0) -> void:
	super.set_character_pose(clip_name,time,facing,hit_tint)
	scale *= display_zoom

func get_runtime_summary() -> Dictionary:
	return {"loaded":loaded,"errors":load_errors,"art":"v15 body/head/legs with regenerated complete arm variants","base_art_originals_preserved":true,"default_entry_modified":false,"skin":wanderer_skin.get_garment_diagnostics() if wanderer_skin!=null else {}}
