extends "capture_reference_upper_short.gd"

## A genuine old-reference versus new-reference comparison. Both schemes use
## the same live motion input; cape is not substituted for the old scheme.
class RunningComparisonRig extends "guigu_rig_runtime.gd":
	var comparison_mode := "new"
	func load_runtime(rig_path: String, skins_path: String, animations_path: String, art_root: String, art_folder: String = "yuxu_fullbody_v15") -> bool:
		custom_art_folder = art_folder
		load_errors.clear()
		var rig_data: Variant = _read_json(rig_path)
		var skins_data: Variant = _read_json(skins_path)
		var animations_data: Variant = _read_json(animations_path)
		if not rig_data is Dictionary or not skins_data is Dictionary or not animations_data is Dictionary: return false
		if not _build_character_hierarchy(rig_data): return false
		if not _load_texture_sets(art_root): return false
		if not _build_weighted_meshes(skins_data,art_root): return false
		wanderer_skin = load("res://reference_walk_upper_open_baseline_20261104.gd" if comparison_mode=="old" else "res://reference_walk_skin.gd").new()
		bone_bound_art_ready = wanderer_skin.build(self,art_root.path_join(custom_art_folder))
		_load_animation_tracks(animations_data)
		loaded = bone_bound_art_ready
		return loaded

func _make_detail(mode: String, overlay: Node, index: int) -> Dictionary:
	var detail := SubViewport.new()
	detail.size = Vector2i(440,500)
	detail.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(detail)
	var backdrop := ColorRect.new()
	backdrop.color = Color("58646a")
	backdrop.size = Vector2(440,500)
	detail.add_child(backdrop)
	var rig := RunningComparisonRig.new()
	rig.comparison_mode = mode
	detail.add_child(rig)
	assert(rig.load_runtime("res://guigu_rig/rig.json","res://guigu_rig/skins.json","res://guigu_rig/animations.json","res://art","yuxu_fullbody_v15"))
	rig.set_skin_mode("experiment")
	rig.position = Vector2(220,440)
	var display := TextureRect.new()
	display.texture = detail.get_texture()
	display.position = Vector2(index*440,48)
	display.size = Vector2(440,500)
	overlay.add_child(display)
	_label(overlay,short_header_old if mode=="old" else short_header_new,Vector2(index*440+22,9),25)
	return {"rig":rig,"viewport":detail,"mode":mode}

func _record_rig(rig) -> Dictionary:
	var result := super._record_rig(rig)
	var skin = rig.wanderer_skin
	result.body_rotations = {}
	for key in ["pelvis","torso","head","near_thigh","near_calf","near_foot","far_thigh","far_calf","far_foot"]:
		result.body_rotations[key] = skin.bones[key].rotation
	result.neck_chain_distance = skin.bones.head.position.length()
	result.rest_neck_chain_distance = skin.bones.head.rest.origin.length()
	return result

func _ready() -> void:
	short_header_old = "上一版固定持手"
	short_header_new = "本次倾身跑动"
	short_default_output = "running_swing_short_probe"
	short_frame_count = 165
	short_fixture_distance = 650.0
	short_stop_frame = 120
	short_even_capture_until = 120
	short_extra_key_frames = [120,124,132,144,156,164]
	await super._ready()
