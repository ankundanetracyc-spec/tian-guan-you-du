extends SceneTree

class ProbeRuntime extends Node2D:
	var rig_root_node: Node2D

class ProbeSkin extends RefCounted:
	var runtime: Node2D
	var source_scale := 1.0
	var template_document := {"sourcePPU":100}
	var bones: Dictionary = {}
	var mesh_records: Array = []

func _initialize() -> void:
	call_deferred("_probe")

func _probe() -> void:
	var source_path := "E:/stacklands/resources/appearance/guigu_male101/model_frames_20261104.json"
	var document: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(source_path))
	var owner := ProbeRuntime.new()
	owner.rig_root_node = Node2D.new()
	root.add_child(owner)
	owner.add_child(owner.rig_root_node)
	var skin := ProbeSkin.new()
	skin.runtime = owner
	var indices := {}
	for role in {"near_upper":"/youbi1","near_forearm":"/youbi2","near_hand":"/youbi3","far_upper":"/zuobi1","far_forearm":"/zuobi2","far_hand":"/zuobi3"}:
		var suffix: String = {"near_upper":"/youbi1","near_forearm":"/youbi2","near_hand":"/youbi3","far_upper":"/zuobi1","far_forearm":"/zuobi2","far_hand":"/zuobi3"}[role]
		for index in range(document.bonePaths.size()):
			if str(document.bonePaths[index]).ends_with(suffix):
				var node := Node2D.new()
				owner.rig_root_node.add_child(node)
				skin.bones[role] = node
				indices[role] = index
	var images := {}
	for component in ["sleeve_near","sleeve_far"]:
		var image := Image.new()
		image.load("res://art/ink_outfit_01_20261104/assets/"+component+".png")
		images[component] = image
		var mesh := Polygon2D.new()
		mesh.texture = ImageTexture.create_from_image(image)
		owner.rig_root_node.add_child(mesh)
		skin.mesh_records.append({"component":component,"mesh":mesh})
	_set_pose(skin,indices,document.clips.Idle,0)
	var helper: RefCounted = load("res://ink_outfit_sleeve_mesh.gd").new()
	assert(helper.build(skin),str(helper.errors))
	var active_triangles := {}
	for component in helper.sleeves:
		var state: Dictionary = helper.sleeves[component]
		var image: Image = images[component]
		var selected := []
		for tri in state.mesh.polygons:
			var uv: Vector2 = (state.uv_points[tri[0]]+state.uv_points[tri[1]]+state.uv_points[tri[2]])/3.0
			var pixel := Vector2i(clampi(int(uv.x*image.get_width()),0,image.get_width()-1),clampi(int(uv.y*image.get_height()),0,image.get_height()-1))
			if image.get_pixelv(pixel).a>=0.5:
				var a: Vector2 = state.uv_points[tri[0]]
				var b: Vector2 = state.uv_points[tri[1]]
				var c: Vector2 = state.uv_points[tri[2]]
				selected.append({"indices":tri,"rest_sign":signf((b-a).cross(c-a))})
		active_triangles[component] = selected
	var count := 0
	var max_hand_error := 0.0
	var non_finite := 0
	var flipped := 0
	var min_area := INF
	var by_clip := {}
	var examples := []
	var flips_by_component := {}
	for clip_name in document.clips:
		var clip: Dictionary = document.clips[clip_name]
		var clip_flips := 0
		for frame in range(clip.boneFrames.size()):
			_set_pose(skin,indices,clip,frame)
			helper.update_pose(1.0/60.0,clip_name)
			for component in helper.sleeves:
				var state: Dictionary = helper.sleeves[component]
				var vertices: PackedVector2Array = state.mesh.polygon
				max_hand_error = maxf(max_hand_error,float(state.last.hand_scale_error))
				for point in vertices:
					if not point.is_finite(): non_finite += 1
				for tri in active_triangles[component]:
					var ix: PackedInt32Array = tri.indices
					var area := (vertices[ix[1]]-vertices[ix[0]]).cross(vertices[ix[2]]-vertices[ix[0]])*float(tri.rest_sign)
					min_area = minf(min_area,area)
					if area<=0.0:
						flipped += 1
						clip_flips += 1
						flips_by_component[component] = int(flips_by_component.get(component,0))+1
						if examples.size()<12:
							examples.append({"clip":clip_name,"frame":frame,"component":component,"area":area,"uv":[state.uv_points[ix[0]],state.uv_points[ix[1]],state.uv_points[ix[2]]]})
			count += 1
		by_clip[clip_name] = {"frames":clip.boneFrames.size(),"visible_triangle_flip_observations":clip_flips}
	_set_pose(skin,indices,document.clips.Idle,0)
	var limb_lengths := {}
	for side in ["near","far"]:
		limb_lengths[side] = [skin.bones[side+"_upper"].position.distance_to(skin.bones[side+"_forearm"].position),skin.bones[side+"_forearm"].position.distance_to(skin.bones[side+"_hand"].position)]
	var synthetic := {}
	for label in {"straight":PI/2.0,"bent_90":0.0,"bent_130":-0.6981317}:
		var angle: float = {"straight":PI/2.0,"bent_90":0.0,"bent_130":-0.6981317}[label]
		for component in helper.sleeves:
			var state: Dictionary = helper.sleeves[component]
			var side: String = state.side
			var lengths: Array = limb_lengths[side]
			var shoulder := Vector2(10,10)
			var elbow := shoulder+Vector2.DOWN*float(lengths[0])
			var direction := Vector2.from_angle(angle)
			skin.bones[side+"_upper"].position = shoulder
			skin.bones[side+"_forearm"].position = elbow
			skin.bones[side+"_hand"].position = elbow+direction*float(lengths[1])
			skin.bones[side+"_hand"].rotation = angle-(state.wrist-state.elbow).angle()-float(state.hand_angle_offset)
		helper.update_pose()
		var synthetic_flips := 0
		for component in helper.sleeves:
			var vertices: PackedVector2Array = helper.sleeves[component].mesh.polygon
			for tri in active_triangles[component]:
				var ix: PackedInt32Array = tri.indices
				if (vertices[ix[1]]-vertices[ix[0]]).cross(vertices[ix[2]]-vertices[ix[0]])*float(tri.rest_sign)<=0.0: synthetic_flips += 1
		synthetic[label] = {"visible_triangle_flip_observations":synthetic_flips}
	var texture_hashes := {}
	for component in images: texture_hashes[component] = FileAccess.get_sha256("res://art/ink_outfit_01_20261104/assets/"+component+".png")
	var report := {"scope":"standalone source bone-pose geometry probe, not a visual approval", "poses":count,"clips":by_clip,"synthetic_both_arms":synthetic,"coordinate_scale":"exported Godot/source pixels; scale1 probe, uniform source_scale preserves topology","non_finite_vertices":non_finite,"maximum_rigid_hand_width_error":max_hand_error,"visible_triangle_flip_observations":flipped,"minimum_visible_signed_double_area":min_area,"flip_examples":examples,"flips_by_component":flips_by_component,"helper":helper.get_diagnostics(),"helper_sha256":FileAccess.get_sha256("res://ink_outfit_sleeve_mesh.gd"),"pose_data_sha256":FileAccess.get_sha256(source_path),"texture_sha256":texture_hashes}
	var path := "E:/stacklands/docs/美术规范/首套高清水墨长衣_20261104/袖carrier几何与锚位自检_20261105.json"
	FileAccess.open(path,FileAccess.WRITE).store_string(JSON.stringify(report,"  "))
	print(JSON.stringify({"poses":count,"flipped":flipped,"min_area":min_area,"non_finite":non_finite,"hand_error":max_hand_error}))
	helper.dispose()
	owner.queue_free()
	quit(0 if non_finite==0 and flipped==0 else 1)

func _set_pose(skin: RefCounted, indices: Dictionary, clip: Dictionary, frame: int) -> void:
	for role in indices:
		var index: int = indices[role]
		var xy: Array = clip.boneFrames[frame][index]
		skin.bones[role].position = Vector2(float(xy[0]),float(xy[1]))
		skin.bones[role].rotation = float(clip.boneTransforms[frame][index][2])
