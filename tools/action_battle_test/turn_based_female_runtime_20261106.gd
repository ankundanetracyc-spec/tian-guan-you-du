extends "guigu_native_first_runtime.gd"

func build(_art_dir: String = "") -> bool:
	custom_art_folder = "turn_based_female_basic_20261106"
	load_errors.clear()
	rig_root_node = Node2D.new()
	rig_root_node.name = "FemaleBasicSharedRig"
	add_child(rig_root_node)
	wanderer_skin = load(get_script().resource_path.get_base_dir().path_join("turn_based_female_skin_20261106.gd")).new()
	bone_bound_art_ready = wanderer_skin.build(self)
	loaded = bone_bound_art_ready
	if not loaded: load_errors.append_array(wanderer_skin.validation_errors)
	return loaded

func set_skin_mode(_mode: String) -> void:
	active_visual_mode = "female_basic"
