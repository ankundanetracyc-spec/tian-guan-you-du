extends Node2D

## Deterministic comparison. The scene owns two isolated rigs and never changes
## the game's default importer. Mouse position cannot change replay facing.
class ReplayBattle extends "action_battle_feel_test.gd":
	var replay_direction := Vector2.ZERO
	var driver_facing := 1.0
	func movement_direction() -> Vector2:
		return replay_direction
	func update_skinned_player():
		facing = driver_facing
		moving_backward = replay_direction.x * facing < -0.1
		super.update_skinned_player()
	func _enter_trial_fullscreen(_fullscreen := true) -> void:
		super._enter_trial_fullscreen(false)

class ReviewRig extends "guigu_rig_runtime.gd":
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
		if not _build_weighted_meshes(skins_data, art_root): return false
		if comparison_mode == "old":
			wanderer_skin = load("res://yuxu_approved_skin.gd").new()
		else:
			wanderer_skin = load("res://leg_supported_robe_skin.gd").new()
		bone_bound_art_ready = wanderer_skin.build(self, art_root.path_join(custom_art_folder))
		_load_animation_tracks(animations_data)
		loaded = bone_bound_art_ready
		return loaded

const PROBE_FRAMES := [0, 18, 30, 48, 51, 54, 57, 60, 63, 66, 69, 89, 90, 93, 99, 110, 125, 139, 172, 190, 204, 205, 207, 209, 211, 220, 239, 240, 245, 251, 252, 269]
var ui_font := SystemFont.new()

func _label(parent: Node, value: String, at: Vector2, size: int) -> Label:
	var label := Label.new()
	label.text = value
	label.position = at
	label.add_theme_font_override("font", ui_font)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", Color("e7e9e4"))
	parent.add_child(label)
	return label

func _make_detail(mode: String, overlay: Node, index: int) -> Dictionary:
	var detail := SubViewport.new()
	detail.size = Vector2i(440, 500)
	detail.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(detail)
	var backdrop := ColorRect.new()
	backdrop.color = Color("58646a")
	backdrop.size = Vector2(440, 500)
	detail.add_child(backdrop)
	var rig := ReviewRig.new()
	rig.comparison_mode = mode
	detail.add_child(rig)
	assert(rig.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art", "yuxu_fullbody_v15"))
	rig.set_skin_mode("experiment")
	rig.position = Vector2(220, 440)
	var display := TextureRect.new()
	display.texture = detail.get_texture()
	display.position = Vector2(index * 440, 48)
	display.size = Vector2(440, 500)
	overlay.add_child(display)
	_label(overlay, "原版 · 原袍长与露鞋" if mode == "old" else "新 · 仅移动撑布", Vector2(index * 440 + 22, 9), 25)
	return {"rig": rig, "viewport": detail, "mode": mode}

func _serial(value: Variant) -> Variant:
	if value is Vector2: return [value.x, value.y]
	if value is Vector3: return [value.x, value.y, value.z]
	if value is PackedVector2Array:
		var result := []
		for item in value: result.append([item.x, item.y])
		return result
	if value is Dictionary:
		var result := {}
		for key in value: result[key] = _serial(value[key])
		return result
	if value is Array:
		var result := []
		for item in value: result.append(_serial(item))
		return result
	return value

func _record_rig(rig) -> Dictionary:
	var skin = rig.wanderer_skin
	var legs := []
	for mesh in skin.meshes:
		if str(mesh.name).contains("leg"): legs.append({"name":str(mesh.name),"visible":mesh.visible,"in_tree":mesh.is_visible_in_tree()})
	var bone_points := {}
	for name in ["pelvis","torso","head","near_thigh","near_calf","near_foot","far_thigh","far_calf","far_foot"]:
		bone_points[name] = rig.rig_root_node.to_local(skin.bones[name].global_position)
	var garment := {"mode":"original_approved_v21","complete_leg_bones":skin.leg_chains.size()==2}
	if skin.has_method("get_garment_diagnostics"): garment = skin.get_garment_diagnostics()
	return {"garment":_serial(garment),"legs":legs,"bones":skin.bones.size(),"meshes":skin.meshes.size(),"contacts":_serial(skin.ground_contacts),"bone_points":_serial(bone_points)}

func _ready() -> void:
	ui_font.font_names = PackedStringArray(["Microsoft YaHei"])
	var args := OS.get_cmdline_user_args()
	var probe_only := args.has("--probe-only")
	var orientation := -1.0 if args.has("--mirror") else 1.0
	var output_name := "leg_supported_robe_shoes_probe" if probe_only else "leg_supported_robe_shoes_full"
	for argument in args:
		if argument.begins_with("--output="): output_name = argument.trim_prefix("--output=")
	var output := ProjectSettings.globalize_path("res://output/" + output_name)
	DirAccess.make_dir_recursive_absolute(output)
	var battle := ReplayBattle.new()
	battle.original_wanderer_enabled = true
	battle.original_wanderer_art_folder = "yuxu_fullbody_v15"
	add_child(battle)
	battle.set_process(false)
	battle.set_process_input(false)
	battle.player = Vector2(1500, 950)
	battle.enemy = Vector2(2700, 950)
	battle._update_trial_camera(0.0, true)
	# Only this isolated capture scene gets the candidate in its arena.
	var retired_rig = battle.original_wanderer
	retired_rig.queue_free()
	var arena_rig := ReviewRig.new()
	battle.add_child(arena_rig)
	assert(arena_rig.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art", "yuxu_fullbody_v15"))
	arena_rig.set_skin_mode("experiment")
	battle.original_wanderer = arena_rig
	get_window().size = Vector2i(880, 600)
	get_window().content_scale_size = Vector2i(880, 600)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	var overlay := CanvasLayer.new()
	overlay.layer = 100
	add_child(overlay)
	var panel := ColorRect.new()
	panel.color = Color("424d53")
	panel.size = Vector2(880, 600)
	overlay.add_child(panel)
	var details := [_make_detail("old", overlay, 0),_make_detail("new", overlay, 1)]
	var phase_label := _label(overlay, "", Vector2(18, 551), 21)
	_label(overlay, "同输入、同相位 · 原生骨骼渲染", Vector2(18, 578), 15)
	await get_tree().process_frame
	var journal := FileAccess.open(output.path_join("frames.jsonl"), FileAccess.WRITE)
	var intended_facing := orientation
	var phase_name := "待机"
	for frame in range(270):
		battle.enemy = Vector2(2700, 950)
		match frame:
			18:
				battle.replay_direction = Vector2.RIGHT * orientation
				phase_name = "起步与持续前进"
			90:
				battle.replay_direction = Vector2.ZERO
				phase_name = "停下后衣料回落"
			126:
				battle.replay_direction = Vector2.LEFT * orientation
				intended_facing = -orientation
				phase_name = "转向前进"
			156:
				battle.replay_direction = Vector2.RIGHT * orientation
				phase_name = "后退"
			186:
				intended_facing = orientation
				phase_name = "转身前进"
			204: phase_name = "错步中间相位"
			220:
				battle.replay_direction = Vector2.ZERO
				phase_name = "急停与惯性回落"
			240:
				battle.configuring = true
				phase_name = "暂停冻结"
			252:
				battle.configuring = false
				phase_name = "恢复回落"
		battle.driver_facing = intended_facing
		await get_tree().process_frame
		if frame == 204: battle.use_skill("错步")
		battle._process(1.0 / 30.0)
		assert(battle.facing == intended_facing)
		var selected: Array = battle._current_player_animation()
		var record := {"frame":frame,"seconds":frame/30.0,"label":phase_name,"facing":battle.facing,"expected_facing":intended_facing,"clip":selected[0],"phase":selected[1],"backward":battle.moving_backward,"delta":battle.visual_motion_delta,"clock":battle.motion_time,"velocity":[battle.visual_motion_velocity.x,battle.visual_motion_velocity.y],"profiles":{}}
		for item in details:
			var rig = item.rig
			var skin = rig.wanderer_skin
			skin.set_motion_context(battle.visual_motion_delta,battle.visual_motion_velocity,battle.motion_time,battle.facing)
			skin.set_action_context(battle.guard_window>0.0,not battle.pending_attack.is_empty())
			rig.set_character_pose(str(selected[0]),float(selected[1]),battle.facing)
			rig.scale *= 4.0
			record.profiles[item.mode] = _record_rig(rig)
		phase_label.text = "%04.2fs / 9.00s    %s" % [frame / 30.0, phase_name]
		journal.store_line(JSON.stringify(record))
		journal.flush()
		if probe_only and frame not in PROBE_FRAMES: continue
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		assert(get_viewport().get_texture().get_image().save_png(output.path_join("compare_%04d.png" % frame)) == OK)
		for item in details:
			assert(item.viewport.get_texture().get_image().save_png(output.path_join("%s_%04d.png" % [item.mode,frame])) == OK)
		if frame in [0,54,69,93,139,172,207,239,269]:
			overlay.hide()
			await get_tree().process_frame
			await RenderingServer.frame_post_draw
			assert(get_viewport().get_texture().get_image().save_png(output.path_join("arena_%04d.png" % frame)) == OK)
			overlay.show()
		if frame % 60 == 0: print("LEG SUPPORTED ROBE CAPTURE ",frame,"/270")
	journal.close()
	print("LEG SUPPORTED ROBE COMPLETE: ",output)
	get_tree().quit()
