extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")
var checks := 0
var failures: Array[String] = []
var report: Dictionary = {}

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)
		push_error(message)

func _finish() -> void:
	report.merge({"passed":failures.is_empty(),"checks":checks,"failures":failures})
	var file := FileAccess.open("res://output/ink_outfit_fitting_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("INK OUTFIT FITTING ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks; ",JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)

func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output"))
	var template_path := "res://art/ink_outfit_01_20261104/assets/template/fullcanvas_template_anchors.json"
	var template: Variant = JSON.parse_string(FileAccess.get_file_as_string(template_path))
	_check(template is Dictionary and template.has("components"),"full-canvas template is present")
	if not template is Dictionary or not template.has("components"):
		_finish()
		return
	_check(float(template.sourcePPU)==100.0 and float(template.densityMultiplier)==24.0 and float(template.targetPPU)==2400.0,"one source/template pixel-density ruler applies to all components")
	var maximum_template_anchor_error := 0.0
	var maximum_crop_affine_error := 0.0
	for key in ["body","sleeve_near","sleeve_far","leg"]:
		var component: Dictionary = template.components[key]
		var source_size: Array = component.sourceCanvasSize
		var target_size: Array = component.templateCanvasSize
		_check(float(target_size[0])==float(source_size[0])*24.0 and float(target_size[1])==float(source_size[1])*24.0,key+" uses its complete source canvas at k24")
		var trim: Array = component.sourceTextureRectPixels
		var rows: Array = component.cropUvToFullCanvasNormalized
		for uv in [Vector2.ZERO,Vector2.ONE,Vector2(0.5,0.5),Vector2(0.17,0.83)]:
			var actual := Vector2(float(rows[0][0])*uv.x+float(rows[0][1])*uv.y+float(rows[0][2]),float(rows[1][0])*uv.x+float(rows[1][1])*uv.y+float(rows[1][2]))
			var expected := Vector2((float(trim[0])+uv.x*float(trim[2]))/float(source_size[0]),(float(source_size[1])-float(trim[1])-float(trim[3])+uv.y*float(trim[3]))/float(source_size[1]))
			maximum_crop_affine_error = maxf(maximum_crop_affine_error,actual.distance_to(expected))
		for anchor in component.anchors:
			var before: Array = anchor.sourceCanvasPx
			var after: Array = anchor.templatePx
			maximum_template_anchor_error = maxf(maximum_template_anchor_error,Vector2(float(before[0])*24.0,float(before[1])*24.0).distance_to(Vector2(float(after[0]),float(after[1]))))
	_check(maximum_crop_affine_error<0.000001,"crop UV maps to complete Sprite canvas with the correct trim/Y convention")
	_check(maximum_template_anchor_error<0.002,"all source joints and coverage anchors share the k24 construction scale")
	report.template = {"path":template_path,"maximum_crop_affine_error":maximum_crop_affine_error,"maximum_anchor_pixel_error":maximum_template_anchor_error}
	if OS.get_cmdline_user_args().has("--template-only"):
		report.scope = "template_only; new outfit material and rendered appearance not verified"
		_finish()
		return
	var rig := OutfitRuntime.new()
	add_child(rig)
	var built: bool = rig.build()
	_check(built,"all four newly drawn outfit textures build into the independent fitting room")
	if not built:
		report.build_errors = rig.load_errors
		_finish()
		return
	var skin = rig.wanderer_skin
	_check(skin.hem_secondary_enabled,"ordinary fitting enables the added robe secondary layer by default")
	# Mathematical source-frame comparisons deliberately bypass visual layers.
	skin.hem_secondary_enabled = false
	skin.sleeve_reconstruction_enabled = false
	skin.leg_reconstruction_enabled = false
	_check(skin.meshes.size()==5 and skin.material_diagnostics.size()==4,"five clothing meshes use four new native textures")
	_check(skin.hd_registration.is_empty() and skin.hd_registration_prepared.is_empty(),"no previous nonlinear HD registration is loaded")
	_check(skin.fixed_head!=null and skin.fixed_head.texture!=null and not skin.neutral_head.visible,"selected featureless side head replaces the mannequin oval")
	_check(skin.original_pose_document.meshes.size()==7 and skin.source_document.meshes.size()==5,"clothing selection leaves the immutable source document intact")
	_check(not skin.use_secondary_frames,"fitting preview samples original source poses without the prior secondary-motion surrogate")
	var maximum_native_uv_error := 0.0
	var maximum_native_aspect_error := 0.0
	var body_layer := 0
	var near_layer := 0
	var far_layer := 0
	for record in skin.mesh_records:
		var key := str(record.component)
		var mesh: Polygon2D = record.mesh
		var component: Dictionary = template.components[key]
		var rows: Array = component.cropUvToFullCanvasNormalized
		for index in range(mesh.uv.size()):
			var uv: Array = record.source.uv[index]
			var expected := Vector2(float(rows[0][0])*float(uv[0])+float(rows[0][1])*float(uv[1])+float(rows[0][2]),float(rows[1][0])*float(uv[0])+float(rows[1][1])*float(uv[1])+float(rows[1][2]))*mesh.texture.get_size()
			maximum_native_uv_error = maxf(maximum_native_uv_error,mesh.uv[index].distance_to(expected))
		if key=="leg": _check(mesh.material==skin.pants_waist_material,"leg uses only the source-waist insertion mask with unchanged native UV")
		elif key in ["sleeve_near","sleeve_far"]: _check(mesh.material==skin.hand_materials[key],key+" retains the open sleeve and samples hand variants only beyond the wrist")
		else: _check(mesh.material==null,"component "+key+" samples its native full canvas directly")
		if key=="body": body_layer = mesh.z_index
		elif key=="sleeve_near": near_layer = mesh.z_index
		elif key=="sleeve_far": far_layer = mesh.z_index
	for key in skin.material_diagnostics:
		var d: Dictionary = skin.material_diagnostics[key]
		var x: float = d.native_per_template_x
		var y: float = d.native_per_template_y
		maximum_native_aspect_error = maxf(maximum_native_aspect_error,absf(x-y)/maxf(x,y))
		_check(str(d.path).contains("ink_outfit_01_20261104/assets/") and not str(d.path).contains("template"),key+" uses new art rather than source pixels or guide alpha")
	_check(maximum_native_uv_error<0.002,"actual native PNG dimensions are applied only after the full-canvas affine")
	_check(maximum_native_aspect_error<0.02,"new PNG complete-canvas proportions agree with the shared-density template")
	_check(far_layer<body_layer and body_layer<near_layer,"far sleeve is behind the body and near sleeve is in front")
	var sleeve_textures := {}
	for record in skin.mesh_records:
		if record.category=="sleeve": sleeve_textures[record.component] = record.mesh.texture
	skin.motion_context = false
	skin.pose_blending = false
	for pair in [["Idle","open"],["Run_New1","fist"],["Run_New2","fist"],["AttackIdle","open"],["Step1","open"]]:
		rig.set_character_pose(pair[0],0.0,1.0)
		_check(skin.active_hand_poses.sleeve_near==pair[1] and skin.active_hand_poses.sleeve_far==pair[1],pair[0]+" uses normal "+pair[1]+" hands without an automatic sword gesture")
	for record in skin.mesh_records:
		if record.category=="sleeve": _check(record.mesh.texture==sleeve_textures[record.component],record.component+" shoulder and sleeve base texture stays unchanged across hand poses")
	if skin.hand_variant_textures.sleeve_near.has("sword"):
		skin.set_hand_pose_override("sword_near")
		rig.set_character_pose("Idle",0.0,1.0)
		_check(skin.active_hand_poses.sleeve_near=="sword" and skin.active_hand_poses.sleeve_far=="open","explicit sword variant affects only the near hand")
		skin.set_hand_pose_override("")
	if skin.hand_variant_textures.sleeve_near.has("grip"):
		skin.set_hand_pose_override("grip_near")
		rig.set_character_pose("Idle",0.0,1.0)
		_check(skin.active_hand_poses.sleeve_near=="grip" and skin.active_hand_poses.sleeve_far=="open","explicit sword-grip variant affects only the near hand")
		skin.set_hand_pose_override("")
	skin.motion_context = false
	skin.pose_blending = false
	skin.use_secondary_frames = false
	var maximum_source_position_error := 0.0
	var sampled_vertices := 0
	for clip_name in ["Idle","Run_New1","Run_New2","AttackIdle","Step1"]:
		var clip: Dictionary = skin.source_document.clips[clip_name]
		for facing in [1.0,-1.0]:
			for phase in [0.0,0.125,0.25,0.5,0.875]:
				var duration := float(clip.duration)
				var time := float(phase)*duration
				rig.set_character_pose(clip_name,time,facing)
				var frames: int = clip.frames.size()
				var source_sample := float(phase)*frames if bool(clip.loop) else float(phase)*(frames-1)
				var first := floori(source_sample)
				var second := (first+1)%frames if bool(clip.loop) else mini(first+1,frames-1)
				var mix := source_sample-first
				for mesh_index in range(skin.meshes.size()):
					var source_index: int = skin.selected_source_mesh_indices[mesh_index]
					var raw: Dictionary = skin.original_pose_document.clips[clip_name]
					for vertex in range(raw.frames[first][source_index].size()):
						var a: Array = raw.frames[first][source_index][vertex]
						var b: Array = raw.frames[second][source_index][vertex]
						var p := Vector2(lerpf(float(a[0]),float(b[0]),mix)-skin.source_center_x,lerpf(float(a[1]),float(b[1]),mix)-skin.source_foot_y)*float(skin.source_scale)
						maximum_source_position_error = maxf(maximum_source_position_error,skin.meshes[mesh_index].polygon[vertex].distance_to(p))
						sampled_vertices += 1
	_check(maximum_source_position_error<0.0003,"standing/running/backing/action poses preserve the selected source-mesh positions in both facings")
	var extension: Dictionary = skin.body_carrier_extension
	_check(extension.get("original_vertices_unchanged",false) and extension.get("added_triangles",[]).size()==3,"continuous rear hem has only one disclosed carrier vertex and three added triangles")
	var minimum_carrier_area := INF
	var maximum_extension_error := 0.0
	var body_index := -1
	for index in range(skin.mesh_records.size()):
		if skin.mesh_records[index].category=="body": body_index = index
	var original_body_index: int = skin.selected_source_mesh_indices[body_index]
	var original_body: Dictionary = skin.original_pose_document.meshes[original_body_index]
	var source_immutable: bool = original_body.uv.size()==40 and original_body.triangles.size()==43
	for clip_name in skin.source_document.clips:
		var original: Dictionary = skin.original_pose_document.clips[clip_name]
		var modified: Dictionary = skin.source_document.clips[clip_name]
		for frame_index in range(modified.frames.size()):
			source_immutable = source_immutable and original.frames[frame_index][original_body_index].size()==40
			var values: Array = modified.frames[frame_index][body_index]
			var expected := Vector2.ZERO
			for index in range(3):
				var p: Array = original.frames[frame_index][skin.selected_source_mesh_indices[body_index]][int(extension.source_basis[index])]
				expected += Vector2(float(p[0]),float(p[1]))*float(extension.affine_weights[index])
			var actual: Array = values[int(extension.new_vertex)]
			maximum_extension_error = maxf(maximum_extension_error,expected.distance_to(Vector2(float(actual[0]),float(actual[1]))))
			for triangle in extension.added_triangles:
				var a := Vector2(float(values[triangle[0]][0]),float(values[triangle[0]][1]))
				var b := Vector2(float(values[triangle[1]][0]),float(values[triangle[1]][1]))
				var c := Vector2(float(values[triangle[2]][0]),float(values[triangle[2]][1]))
				minimum_carrier_area = minf(minimum_carrier_area,(b-a).cross(c-a))
	_check(maximum_extension_error<0.0003 and minimum_carrier_area>0.01,"added rear carrier follows adjacent cloth across every source frame without inverted/degenerate triangles")
	_check(source_immutable,"all original body UVs, triangles and 864 source-frame vertex counts remain unchanged")
	var near_extension: Dictionary = skin.near_hand_carrier_extension
	var near_index := -1
	for index in range(skin.mesh_records.size()):
		if skin.mesh_records[index].component=="sleeve_near": near_index = index
	var original_near_index: int = skin.selected_source_mesh_indices[near_index]
	var original_near: Dictionary = skin.original_pose_document.meshes[original_near_index]
	var near_source_immutable: bool = original_near.uv.size()==35 and original_near.triangles.size()==47
	var minimum_hand_carrier_area := INF
	var maximum_hand_carrier_error := 0.0
	for clip_name in skin.source_document.clips:
		var original: Dictionary = skin.original_pose_document.clips[clip_name]
		var modified: Dictionary = skin.source_document.clips[clip_name]
		for frame_index in range(modified.frames.size()):
			near_source_immutable = near_source_immutable and original.frames[frame_index][original_near_index].size()==35
			var values: Array = modified.frames[frame_index][near_index]
			var expected := Vector2.ZERO
			for index in range(3):
				var p: Array = original.frames[frame_index][original_near_index][int(near_extension.source_basis[index])]
				expected += Vector2(float(p[0]),float(p[1]))*float(near_extension.affine_weights[index])
			var actual: Array = values[int(near_extension.new_vertex)]
			maximum_hand_carrier_error = maxf(maximum_hand_carrier_error,expected.distance_to(Vector2(float(actual[0]),float(actual[1]))))
			for triangle in near_extension.added_triangles:
				var a := Vector2(float(values[triangle[0]][0]),float(values[triangle[0]][1]))
				var b := Vector2(float(values[triangle[1]][0]),float(values[triangle[1]][1]))
				var c := Vector2(float(values[triangle[2]][0]),float(values[triangle[2]][1]))
				minimum_hand_carrier_area = minf(minimum_hand_carrier_area,(b-a).cross(c-a))
	_check(near_source_immutable,"near hand carrier keeps all original 35 source vertices and 47 source triangles immutable")
	_check(maximum_hand_carrier_error<0.0003 and minimum_hand_carrier_area>0.01,"new natural open-hand carrier follows the source hand over all clips without inverted/degenerate triangles")
	rig.display_zoom = 7.1
	rig.position = Vector2(300,705)
	rig.set_character_pose("Idle",42.0/60.0,1.0)
	var previous_dark_point := (Vector2(340,393)-rig.position)/rig.scale
	var old_dark_point_masked: bool = not skin.is_leg_pixel_below_waist(previous_dark_point)
	_check(old_dark_point_masked,"the traced old front-waist pants protrusion lies in the masked insertion region")
	_check(skin.pants_waist_point.distance_to(skin.bones.pelvis.position)<0.0001 and skin.pants_up_normal.dot((skin.bones.head.position-skin.bones.pelvis.position).normalized())>0.9999,"leg mask passes through the real source waist and follows the torso up direction")
	var lower_leg_preserved := true
	var lower_leg_vertices_checked := 0
	for clip_name in ["Idle","Run_New1","Run_New2","Step1"]:
		var data: Dictionary = skin.source_document.clips[clip_name]
		for phase_index in range(24):
			rig.set_character_pose(clip_name,float(data.duration)*float(phase_index)/24.0,1.0)
			for record in skin.mesh_records:
				if record.category!="leg": continue
				for vertex in range(record.source.uv.size()):
					if float(record.source.uv[vertex][1])<0.55: continue
					lower_leg_preserved = lower_leg_preserved and skin.is_leg_pixel_below_waist(record.mesh.polygon[vertex])
					lower_leg_vertices_checked += 1
	_check(lower_leg_preserved,"both lower legs and boots stay below the mask over complete Idle/Run1/Run2/Step cycles")
	skin.pose_blending = true
	skin.use_secondary_frames = false
	var clock := 0.0
	for frame in range(120):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(114,0),clock,1.0)
		rig.set_character_pose("Run_New1",clock,1.0)
	var frozen := []
	for mesh in skin.meshes: frozen.append(mesh.polygon.duplicate())
	var frozen_head: Transform2D = skin.bones.head.transform
	var frozen_waist: Vector2 = skin.pants_waist_point
	var frozen_normal: Vector2 = skin.pants_up_normal
	var frozen_hands: Dictionary = skin.active_hand_poses.duplicate(true)
	for frame in range(12):
		skin.set_motion_context(0.0,Vector2.ZERO,clock,1.0)
		rig.set_character_pose("Idle",0.0,1.0)
	var pause_pass: bool = frozen_head==skin.bones.head.transform
	for index in range(frozen.size()): pause_pass = pause_pass and frozen[index]==skin.meshes[index].polygon
	_check(pause_pass,"pause freezes clothing and neutral head")
	var mask_pause_pass: bool = frozen_waist==skin.pants_waist_point and frozen_normal==skin.pants_up_normal
	_check(mask_pause_pass,"pause freezes the source-waist mask plane")
	_check(frozen_hands==skin.active_hand_poses,"pause freezes normal-hand pose selection")
	for frame in range(60):
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,clock,1.0)
		rig.set_character_pose("Idle",clock,1.0)
	_check(skin.sampled_source_clip=="Idle","released running returns to Idle")
	report.merge({"scope":"new outfit data and pose checks; native rendered appearance requires separate visual review","materials":skin.material_diagnostics,"hand_variants":skin.get_garment_diagnostics().hand_pose_variants,"maximum_native_uv_error":maximum_native_uv_error,"maximum_native_aspect_error":maximum_native_aspect_error,"maximum_source_position_error":maximum_source_position_error,"sampled_source_vertices":sampled_vertices,"near_hand_carrier":{"minimum_signed_double_area":minimum_hand_carrier_area,"maximum_affine_error":maximum_hand_carrier_error,"description":near_extension},"rear_carrier":{"minimum_signed_double_area":minimum_carrier_area,"maximum_affine_error":maximum_extension_error,"description":extension},"upper_pants_insertion_mask":{"lower_leg_vertices_checked":lower_leg_vertices_checked,"lower_legs_and_boots_preserved":lower_leg_preserved,"old_dark_point_masked":old_dark_point_masked,"pause_frozen":mask_pause_pass},"pause_frozen":pause_pass,"default_entry_changed":false})
	_finish()
