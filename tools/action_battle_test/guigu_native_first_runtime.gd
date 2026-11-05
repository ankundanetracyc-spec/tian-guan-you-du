extends "guigu_rig_runtime.gd"

## API-compatible game actor with the original male101 source pipeline.
func build(_art_dir: String = "") -> bool:
	return load_runtime("","","","","guigu_native_first_20261105")

func load_runtime(_rig_path: String, _skins_path: String, _animations_path: String, _art_root: String, art_folder: String = "guigu_native_first_20261105") -> bool:
	custom_art_folder = art_folder
	load_errors.clear()
	rig_root_node = Node2D.new()
	rig_root_node.name = "OriginalMale101NativeCanvas"
	add_child(rig_root_node)
	wanderer_skin = load(get_script().resource_path.get_base_dir().path_join("guigu_native_first_skin.gd")).new()
	bone_bound_art_ready = wanderer_skin.build(self)
	loaded = bone_bound_art_ready
	if not loaded: load_errors.append_array(wanderer_skin.validation_errors)
	active_visual_mode = "experiment"
	return loaded

func set_character_pose(clip_name: String, time: float, facing: float = 1.0, hit_tint: float = 0.0) -> void:
	if not loaded: return
	scale = Vector2(-1.0 if facing<0.0 else 1.0,1.0)*wanderer_skin.display_scale
	wanderer_skin.update_pose(clip_name,time,facing)
	modulate = Color(1.0,1.0-hit_tint*0.25,1.0-hit_tint*0.25)

func set_skin_mode(mode: String) -> void:
	active_visual_mode = mode
	if wanderer_skin!=null: wanderer_skin.set_source_textures(mode=="reference")

func get_runtime_summary() -> Dictionary:
	return {"loaded":loaded,"errors":load_errors,"bone_bound_art_ready":bone_bound_art_ready,"art":custom_art_folder,"anatomical_skin":wanderer_skin.summary() if wanderer_skin!=null else {},"pipeline":"original male101 full canvases and 3D source animation/skin/DynamicBone"}
