extends Node2D

class SourceHDRig extends "guigu_rig_runtime.gd":
	func load_hd() -> bool:
		custom_art_folder = "yuxu_fullbody_v15"
		if not _build_character_hierarchy(_read_json("res://guigu_rig/rig.json")): return false
		if not _load_texture_sets("res://art"): return false
		if not _build_weighted_meshes(_read_json("res://guigu_rig/skins.json"),"res://art"): return false
		wanderer_skin = load("res://guigu_model_copy_skin.gd").new()
		wanderer_skin.strict_required = true
		if OS.get_cmdline_user_args().has("--research-data"):
			wanderer_skin.source_data_path = "res://output/guigu_source_reference_v2/male101/source_reference_vertex_frames.json"
		bone_bound_art_ready = wanderer_skin.build(self,"res://art/yuxu_fullbody_v15")
		_load_animation_tracks(_read_json("res://guigu_rig/animations.json"))
		loaded = bone_bound_art_ready
		return loaded

var checks := 0
var failures: Array[String] = []
var details: Dictionary = {}

func _check(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures.append(label)
		push_error(label)

func _save_and_exit() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output"))
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"details":details}
	var file := FileAccess.open("res://output/source_model_hd_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("SOURCE MODEL HD ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks; ",JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)

func _mapped(skin, value: Array) -> Vector2:
	return Vector2(float(value[0])-skin.source_center_x,float(value[1])-skin.source_foot_y)*float(skin.source_scale)

func _frame_pair(clip: Dictionary, time: float) -> Dictionary:
	var duration := float(clip.duration)
	var looped := bool(clip.get("loop",false))
	var phase := fposmod(time/duration,1.0) if looped else clampf(time/duration,0.0,1.0)
	var count: int = clip.frames.size()
	var sample := phase*count if looped else phase*(count-1)
	var first := mini(floori(sample),count-1)
	return {"first":first,"second":(first+1)%count if looped else mini(first+1,count-1),"mix":sample-floorf(sample),"phase":phase}

func _expected_bone(skin, clip: Dictionary, pair: Dictionary, index: int) -> Vector2:
	return _mapped(skin,clip.boneFrames[pair.first][index]).lerp(_mapped(skin,clip.boneFrames[pair.second][index]),float(pair.mix))

func _ready() -> void:
	var rig := SourceHDRig.new()
	add_child(rig)
	var built := rig.load_hd()
	_check(built,"strict source model and all required HD ink sprites build")
	if not built:
		details.build_errors = rig.wanderer_skin.validation_errors if rig.wanderer_skin!=null else rig.load_errors
		_save_and_exit()
		return
	rig.set_skin_mode("experiment")
	var skin = rig.wanderer_skin
	_check(skin.strict_required and skin.all_hd_materials,"strict mode reports exclusively high-definition materials")
	_check(skin.meshes.size()==7,"seven original visible source meshes are preserved")
	var unique_sprites := {}
	var textures := []
	var maximum_uv_error := 0.0
	var maximum_registration_anchor_error := 0.0
	var minimum_long_edge := 100000
	for index in range(skin.meshes.size()):
		var record: Dictionary = skin.mesh_records[index]
		var mesh: Polygon2D = skin.meshes[index]
		var source: Dictionary = skin.source_document.meshes[index]
		unique_sprites[str(source.spriteId)] = true
		var width := mesh.texture.get_width()
		var height := mesh.texture.get_height()
		minimum_long_edge = mini(minimum_long_edge,maxi(width,height))
		_check(bool(record.high_definition_sprite) and str(record.texture_path).contains("guigu_male101_ink_hd") and maxi(width,height)>=1024,"sprite "+str(source.spriteId)+" uses a native HD image")
		_check(mesh.polygons.size()==source.triangles.size() and mesh.polygon.size()==source.uv.size(),"sprite "+str(source.spriteId)+" retains source topology")
		for triangle in range(mesh.polygons.size()):
			var actual: PackedInt32Array = mesh.polygons[triangle]
			var expected: Array = source.triangles[triangle]
			var same_triangle := actual.size()==expected.size()
			for corner in range(mini(actual.size(),expected.size())):
				same_triangle = same_triangle and actual[corner]==int(expected[corner])
			_check(same_triangle,"source triangle "+str(index)+":"+str(triangle))
		for vertex in range(mesh.uv.size()):
			var uv: Array = source.uv[vertex]
			maximum_uv_error = maxf(maximum_uv_error,mesh.uv[vertex].distance_to(Vector2(float(uv[0])*width,float(uv[1])*height)))
		var material: ShaderMaterial = mesh.material
		_check(material.get_shader_parameter("use_sprite_paint")==true and material.get_shader_parameter("paint_enabled")!=true,"sprite "+str(source.spriteId)+" samples its new RGB/alpha directly")
		var id := str(int(source.spriteId))
		var registered: bool = skin.registration_metadata.has(id) and skin.registration_metadata[id].valid==true
		_check(registered and int(material.get_shader_parameter("registration_mode"))>0,"sprite "+id+" has an explicit source-pivot to HD-pixel registration")
		if registered:
			for control in skin.hd_registration[id].anchors:
				var uv: Array = control.source_uv
				var target: Array = control.target_px
				var mapped: Vector2 = skin._registered_hd_uv(id,Vector2(float(uv[0]),float(uv[1])),mesh.texture.get_size())
				maximum_registration_anchor_error = maxf(maximum_registration_anchor_error,mapped.distance_to(Vector2(float(target[0]),float(target[1]))))
			var prepared: Dictionary = skin.hd_registration_prepared[id]
			if prepared.has("triangles"):
				var no_fold := true
				var triangles: PackedInt32Array = prepared.triangles
				for triangle in range(0,triangles.size(),3):
					var a := triangles[triangle]
					var b := triangles[triangle+1]
					var c := triangles[triangle+2]
					var source_area: float = (prepared["from"][b]-prepared["from"][a]).cross(prepared["from"][c]-prepared["from"][a])
					var target_area: float = (prepared.target[b]-prepared.target[a]).cross(prepared.target[c]-prepared.target[a])
					no_fold = no_fold and source_area*target_area>0.0
				_check(no_fold,"sprite "+id+" registration keeps every control triangle's orientation")
		textures.append({"sprite":str(source.spriteId),"path":record.texture_path,"size":[width,height]})
	_check(unique_sprites.size()==6,"six unique HD sprites supply seven meshes")
	_check(maximum_uv_error<0.001,"UV coordinates use actual HD dimensions")
	_check(maximum_registration_anchor_error<0.05,"HD registration maps its declared source pivots to the selected paint pixels")
	var old_visible := 0
	for record in rig.mesh_records:
		if record.node.visible: old_visible += 1
	_check(old_visible==0,"all low-resolution reference meshes remain hidden")
	var body_layer := 33
	for record in skin.mesh_records:
		if record.category=="body": body_layer = record.mesh.z_index
	_check(skin.shoulder_patches.size()==2,"two shoulder-root underlays prevent triangular clipping")
	for patch in skin.shoulder_patches:
		var side := "near" if str(patch.name).ends_with("near") else "far"
		_check(patch.get_parent()==skin.bones[side+"_upper"] and patch.z_index<body_layer,"shoulder "+side+" follows its source upper-arm bone under the body")
	# Disable transitions and optional secondary cloth for an exact comparison
	# against the immutable source frames, not against another adapter instance.
	skin.pose_blending = false
	skin.use_secondary_frames = false
	skin.motion_context = false
	var paths: Array = skin.source_document.bonePaths
	var near_hand_index := -1
	for index in range(paths.size()):
		if str(paths[index]).ends_with("/youbi3"): near_hand_index = index
	_check(near_hand_index>=0 and skin.bones.has("near_hand"),"real source near-hand attachment exists")
	var maximum_vertex_error := 0.0
	var maximum_hand_error := 0.0
	var maximum_world_hand_error := 0.0
	var sampled_vertices := 0
	var source_clips := {}
	for requested in ["Idle","Run_New1","BackstepWalk","AttackIdle","AttackRun","Step1"]:
		var selected: String = "Run_New2" if requested=="BackstepWalk" else requested
		var clip: Dictionary = skin.source_document.clips[selected]
		_check(clip.frames.size()==144 and clip.boneFrames.size()==144,selected+" contains 144 trusted source phases")
		for facing in [1.0,-1.0]:
			for phase in [0.0,0.037,0.193,0.427,0.763,0.998]:
				var time := float(phase)*float(clip.duration)
				skin.update_cloth(requested,time,float(clip.duration),{})
				rig.scale = Vector2(facing*skin.display_scale,skin.display_scale)
				var pair := _frame_pair(clip,time)
				for mesh_index in range(skin.meshes.size()):
					var actual: PackedVector2Array = skin.meshes[mesh_index].polygon
					for vertex in range(actual.size()):
						var expected := _mapped(skin,clip.frames[pair.first][mesh_index][vertex]).lerp(_mapped(skin,clip.frames[pair.second][mesh_index][vertex]),float(pair.mix))
						maximum_vertex_error = maxf(maximum_vertex_error,actual[vertex].distance_to(expected))
						sampled_vertices += 1
				if near_hand_index>=0:
					var expected := _expected_bone(skin,clip,pair,near_hand_index)
					maximum_hand_error = maxf(maximum_hand_error,skin.bones.near_hand.position.distance_to(expected))
					maximum_world_hand_error = maxf(maximum_world_hand_error,skin.bones.near_hand.global_position.distance_to(rig.rig_root_node.to_global(expected)))
			_check(signf(rig.scale.x)==facing and skin.sampled_source_clip==selected,selected+" respects facing "+str(facing))
		source_clips[requested] = selected
	_check(maximum_vertex_error<0.0003,"rendered source vertices equal independent source-frame interpolation")
	_check(maximum_hand_error<0.0003 and maximum_world_hand_error<0.0003,"hand attachments match source frames in both local and world space")
	# The actual runtime keeps facing and clips while walking starts/stops,
	# casts/guards/combos take priority, and pause freezes the sampled result.
	skin.pose_blending = true
	skin.use_secondary_frames = true
	var clock := 0.0
	for frame in range(150):
		clock += 1.0/60.0
		var moving := frame>=30 and frame<90
		var velocity := Vector2(114,0) if moving else Vector2.ZERO
		skin.set_motion_context(1.0/60.0,velocity,clock,1.0)
		skin.set_action_context(false,false)
		skin.set_combo_context(0,0.0)
		rig.set_character_pose("Run_New1" if moving else "Idle",clock,1.0)
		if frame in [29,59,89,149]: _check(skin.sampled_source_clip==("Run_New1" if moving else "Idle"),"start/stop chooses the matching original clip at frame "+str(frame))
	_check(skin.previous_velocity.is_zero_approx() and skin.sampled_source_clip=="Idle","stopping returns to source Idle")
	# A local garment transition is a project-authored stop proxy. Upper body
	# and hand settle normally while the hem retains a longer visual release.
	for frame in range(90):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(114,0),clock,1.0)
		rig.set_character_pose("Run_New1",clock,1.0)
	var release_samples := {}
	for frame in range(42):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,1.0)
		rig.set_character_pose("Idle",clock,1.0)
		if frame not in [0,11,35,41]: continue
		var clip: Dictionary = skin.source_document.clips.Idle
		var pair := _frame_pair(clip,clock)
		var upper_error := 0.0
		var hem_error := 0.0
		for mesh_index in range(skin.mesh_records.size()):
			if skin.mesh_records[mesh_index].category!="body": continue
			for vertex in range(skin.meshes[mesh_index].polygon.size()):
				var expected := _mapped(skin,clip.secondaryFrames[pair.first][mesh_index][vertex]).lerp(_mapped(skin,clip.secondaryFrames[pair.second][mesh_index][vertex]),float(pair.mix))
				var error: float = skin.meshes[mesh_index].polygon[vertex].distance_to(expected)
				var uv: Array = skin.mesh_records[mesh_index].source.uv[vertex]
				if float(uv[1])<=0.39: upper_error = maxf(upper_error,error)
				if float(uv[1])>=0.90: hem_error = maxf(hem_error,error)
		var hand_error: float = skin.bones.near_hand.position.distance_to(_expected_bone(skin,clip,pair,near_hand_index))
		release_samples[str(frame+1)] = {"seconds":float(frame+1)/60.0,"upper_error_from_current_idle":upper_error,"hem_error_from_current_idle":hem_error,"hand_error_from_current_idle":hand_error}
	_check(release_samples["12"].upper_error_from_current_idle<0.0003 and release_samples["12"].hand_error_from_current_idle<0.0003 and release_samples["12"].hem_error_from_current_idle>0.01,"at 0.2s upper body/hand have settled while hem still carries release")
	_check(release_samples["36"].hem_error_from_current_idle<0.0003,"hem reaches current Idle by 0.6s without retaining a frozen running shape")
	for action in ["guard","preparing","combo1","combo2","combo3","combo4"]:
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(114,0),clock,1.0)
		skin.set_action_context(action=="guard",action=="preparing")
		skin.set_combo_context(int(action.trim_prefix("combo")) if action.begins_with("combo") else 0,0.4)
		rig.set_character_pose("Run_New1",clock,1.0)
		_check(skin.sampled_source_clip=="AttackRun",action+" uses the original dedicated action clip during movement")
	var frozen := []
	for mesh in skin.meshes: frozen.append(mesh.polygon.duplicate())
	var frozen_hand: Transform2D = skin.bones.near_hand.transform
	var frozen_phase: float = skin.sampled_source_phase
	for frame in range(12):
		skin.set_motion_context(0.0,Vector2.ZERO,clock,1.0)
		rig.set_character_pose("Idle",0.0,1.0)
	var frozen_pass: bool = frozen_hand==skin.bones.near_hand.transform and frozen_phase==skin.sampled_source_phase
	for index in range(frozen.size()): frozen_pass = frozen_pass and frozen[index]==skin.meshes[index].polygon
	_check(frozen_pass,"pause freezes source meshes, hand attachment and phase")
	details = {"source_data":skin.source_data_path,"textures":textures,"unique_sprite_count":unique_sprites.size(),"minimum_hd_long_edge":minimum_long_edge,"low_resolution_reference_meshes_visible":old_visible,"maximum_uv_error":maximum_uv_error,"maximum_registration_anchor_pixel_error":maximum_registration_anchor_error,"maximum_source_vertex_error":maximum_vertex_error,"sampled_source_vertices":sampled_vertices,"maximum_source_hand_error":maximum_hand_error,"maximum_world_hand_error":maximum_world_hand_error,"mapped_clips":source_clips,"shoulder_underlays":skin.shoulder_patches.size(),"native_height":skin.target_native_height,"display_scale":skin.display_scale,"pause_frozen":frozen_pass,"release_proxy_samples":release_samples,"visual_review_required":"Actual HD sharpness, rounded shoulder blending, faceless head and ink style still require rendered image review."}
	_save_and_exit()
