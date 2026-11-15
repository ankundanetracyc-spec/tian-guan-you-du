extends Node2D

const OutfitSkin = preload("ink_outfit_fitting_skin.gd")
var probes: Array[Node2D] = []
var font := SystemFont.new()

class Probe extends Node2D:
	var rig_root_node: Node2D
	var skin: RefCounted

class JointOverlay extends Node2D:
	var probes: Array[Node2D] = []
	var font: Font
	func _draw() -> void:
		for probe in probes:
			var bones: Dictionary = probe.skin.bones
			var gold := Color("b27b20")
			var near := Color("208d9b")
			var far := Color("a34888")
			draw_line(bones.head.global_position,bones.pelvis.global_position,gold,2.0,true)
			for role in ["head","pelvis"]: draw_circle(bones[role].global_position,4.0,gold)
			draw_string(font,bones.head.global_position+Vector2(10,-8),"颈",HORIZONTAL_ALIGNMENT_LEFT,-1,16,gold)
			draw_string(font,bones.pelvis.global_position+Vector2(10,-8),"腰",HORIZONTAL_ALIGNMENT_LEFT,-1,16,gold)
			for side in ["near","far"]:
				var color: Color = near if side=="near" else far
				var upper: Node2D = bones[side+"_upper"]
				draw_circle(upper.global_position,3.5,color)
				var hip: Node2D = bones[side+"_thigh"]
				var knee: Node2D = bones[side+"_calf"]
				var ankle: Node2D = bones[side+"_foot"]
				draw_line(hip.global_position,knee.global_position,color,1.5,true)
				draw_line(knee.global_position,ankle.global_position,color,1.5,true)
				for joint in [hip,knee,ankle]: draw_circle(joint.global_position,3.5,color)
				var offset := Vector2(10,7) if side=="near" else Vector2(-44,-5)
				for pair in [[hip,"髋"],[knee,"膝"],[ankle,"踝"]]: draw_string(font,pair[0].global_position+offset,pair[1],HORIZONTAL_ALIGNMENT_LEFT,-1,14,color)

func _caption(text: String, rect: Rect2, size: int) -> void:
	var label := Label.new()
	label.text = text
	label.position = rect.position
	label.size = rect.size
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",Color("424b48"))
	add_child(label)

func _make_probe(location: Vector2, original: bool) -> Node2D:
	var probe := Probe.new()
	add_child(probe)
	probe.position = location
	probe.rig_root_node = Node2D.new()
	probe.add_child(probe.rig_root_node)
	probe.skin = OutfitSkin.new()
	if original:
		probe.skin.closed_robe_carrier = false
		probe.skin.hand_variants_enabled = false
		probe.skin.sleeve_reconstruction_enabled = false
		probe.skin.hem_secondary_enabled = false
		var texture_root := "../../docs/美术规范/鬼谷局内素材拼接研究_20261104/source_full_textures"
		for pair in [["body",85649],["sleeve_near",79543],["sleeve_far",84807],["leg",87557]]:
			probe.skin.outfit_material_paths[pair[0]] = ProjectSettings.globalize_path("res://").path_join(texture_root).path_join("sprite_"+str(pair[1])+"_full_texture.png").simplify_path()
	assert(probe.skin.build(probe,"res://art/ink_outfit_01_20261104"))
	probe.skin.motion_context = false
	probe.skin.pose_blending = false
	if original:
		for record in probe.skin.mesh_records:
			if record.category=="leg": record.mesh.material = null
	probe.scale = Vector2.ONE*probe.skin.display_scale*7.1
	probes.append(probe)
	return probe

func _set_pose(probe: Node2D, clip: String, time: float) -> void:
	probe.skin.update_cloth(clip,time,float(probe.skin.source_document.clips[clip].duration),{})

func _metrics(probe: Node2D) -> Dictionary:
	var bones: Dictionary = probe.skin.bones
	var unit: float = probe.skin.source_scale
	var torso: float = bones.head.position.distance_to(bones.pelvis.position)/unit
	var result := {"unit":"source pixels at PPU100; not native artwork pixels","neck_to_waist":torso,"hip_mount_separation":bones.near_thigh.position.distance_to(bones.far_thigh.position)/unit,"joints":{}}
	var hip_delta: Vector2 = (bones.near_thigh.position-bones.far_thigh.position)/unit
	result.hip_mount_horizontal_projection = absf(hip_delta.x)
	result.hip_mount_vertical_projection = absf(hip_delta.y)
	for role in ["head","pelvis","near_upper","far_upper","near_thigh","near_calf","near_foot","far_thigh","far_calf","far_foot"]:
		var p: Vector2 = bones[role].position/unit
		result.joints[role] = [p.x,p.y]
	for side in ["near","far"]:
		var thigh: float = bones[side+"_thigh"].position.distance_to(bones[side+"_calf"].position)/unit
		var calf: float = bones[side+"_calf"].position.distance_to(bones[side+"_foot"].position)/unit
		result[side] = {"hip_to_knee":thigh,"knee_to_ankle":calf,"hip_knee_ankle_sum":thigh+calf,"leg_chain_over_neck_waist":(thigh+calf)/torso}
	return result

func _ready() -> void:
	get_window().size = Vector2i(1400,900)
	get_window().content_scale_size = Vector2i(1400,900)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var background := ColorRect.new()
	background.size = Vector2(1400,900)
	background.color = Color("dddcd1")
	add_child(background)
	_caption("同相位衣身与腿部比例",Rect2(0,25,1400,48),30)
	_caption("原素材与原承载面",Rect2(100,100,500,36),22)
	_caption("当前闭袍与腰面裁切",Rect2(800,100,500,36),22)
	_caption("共同无五官人台 · 同骨链与显示尺标 · 浅色原图仅供研究",Rect2(0,155,1400,30),17)
	_make_probe(Vector2(350,790),true)
	_make_probe(Vector2(1050,790),false)
	var ranges := {}
	for clip in ["Idle","Run_New1","Run_New2","AttackIdle","AttackRun","Step1"]:
		var duration: float = probes[0].skin.source_document.clips[clip].duration
		var samples := []
		for index in range(24):
			_set_pose(probes[0],clip,duration*float(index)/24.0)
			samples.append(_metrics(probes[0]))
		ranges[clip] = samples
	for probe in probes: _set_pose(probe,"Idle",42.0/60.0)
	await get_tree().process_frame
	var overlay := JointOverlay.new()
	overlay.z_index = 100
	overlay.probes = probes
	overlay.font = font
	add_child(overlay)
	_caption("黄：颈—腰　青：近侧肩/髋膝踝　紫：远侧肩/髋膝踝",Rect2(0,840,1400,30),17)
	var output := ProjectSettings.globalize_path("res://output/ink_outfit_01_proportion_audit")
	DirAccess.make_dir_recursive_absolute(output)
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	assert(get_viewport().get_texture().get_image().save_png(output.path_join("source_vs_current_idle_joints.png"))==OK)
	var file := FileAccess.open(output.path_join("source_vs_current_bone_metrics.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"scope":"read-only proportion audit; bone equality does not establish fitting quality","same_phase_seconds":42.0/60.0,"source_reference_clothing_only":true,"head":"same neutral fitting head; original hair omitted","source":_metrics(probes[0]),"current":_metrics(probes[1]),"source_24_phase_samples":ranges,"current_skin_sha256":FileAccess.get_sha256(probes[1].skin.get_script().resource_path),"current_garment":probes[1].skin.get_garment_diagnostics()},"\t"))
	file.close()
	print("OUTFIT SOURCE/CURRENT PROPORTION AUDIT: ",output)
	get_tree().quit()
