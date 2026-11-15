extends Node2D

var rig_root_node: Node2D
var wanderer_skin: RefCounted
var loaded := false
var load_errors: Array[String] = []
var display_zoom := 5.5

func build(art_dir: String = "") -> bool:
	rig_root_node = Node2D.new()
	rig_root_node.name = "FittingRoot"
	add_child(rig_root_node)
	wanderer_skin = load(get_script().resource_path.get_base_dir().path_join("ink_outfit_fitting_skin.gd")).new()
	var folder := art_dir
	if folder.is_empty(): folder = get_script().resource_path.get_base_dir().path_join("art/ink_outfit_01_20261104")
	loaded = wanderer_skin.build(self,folder)
	if not loaded: load_errors.assign(wanderer_skin.validation_errors)
	return loaded

func set_character_pose(clip: String, time: float, facing: float = 1.0) -> void:
	if not loaded: return
	scale = Vector2((-1.0 if facing<0.0 else 1.0)*wanderer_skin.display_scale,wanderer_skin.display_scale)*display_zoom
	var source_clip := "Run_New2" if clip=="BackstepWalk" else clip
	var data: Dictionary = wanderer_skin.source_document.clips.get(source_clip,wanderer_skin.source_document.clips.Idle)
	wanderer_skin.update_cloth(clip,time,float(data.duration),{})

func get_runtime_summary() -> Dictionary:
	return {"loaded":loaded,"errors":load_errors,"skin":wanderer_skin.summary() if wanderer_skin!=null else {}}
