extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")
const TOLERANCE := 0.002
var failures: Array[String] = []

func _ready() -> void:
	var rig := OutfitRuntime.new()
	add_child(rig)
	if not rig.build():
		push_error("Rigid sleeve regression cannot build: "+str(rig.load_errors))
		get_tree().quit(1)
		return
	var skin = rig.wanderer_skin
	skin.motion_context = false
	skin.pose_blending = false
	skin.hem_secondary_enabled = false
	var helper = skin.sleeve_reconstruction
	var baselines := {}
	var active_triangles := {}
	var rigid_pairs := {}
	rig.set_character_pose("Idle",0.0,1.0)
	for key in helper.sleeves:
		var state: Dictionary = helper.sleeves[key]
		var vertices: PackedVector2Array = state.mesh.polygon
		baselines[key] = vertices.duplicate()
		var image: Image = state.mesh.texture.get_image()
		var triangles := []
		var pairs := []
		var seen := {}
		for tri in state.mesh.polygons:
			var uv: Vector2 = (state.uv_points[tri[0]]+state.uv_points[tri[1]]+state.uv_points[tri[2]])/3.0
			var pixel := Vector2i(clampi(int(uv.x*image.get_width()),0,image.get_width()-1),clampi(int(uv.y*image.get_height()),0,image.get_height()-1))
			if image.get_pixelv(pixel).a>=0.5:
				var a: Vector2 = state.uv_points[tri[0]]
				var b: Vector2 = state.uv_points[tri[1]]
				var c: Vector2 = state.uv_points[tri[2]]
				triangles.append({"indices":tri,"sign":signf((b-a).cross(c-a))})
			for edge in [[tri[0],tri[1]],[tri[1],tri[2]],[tri[2],tri[0]]]:
				var ia: int = edge[0]
				var ib: int = edge[1]
				# Every connected edge belongs entirely to one rigid sleeve piece;
				# duplicate elbow rows are deliberately not connected across hinge.
				var token := str(mini(ia,ib))+":"+str(maxi(ia,ib))
				if seen.has(token): continue
				seen[token] = true
				pairs.append({"a":ia,"b":ib,"baseline":vertices[ia].distance_to(vertices[ib])})
		active_triangles[key] = triangles
		rigid_pairs[key] = pairs
	var pose_count := 0
	var pair_checks := 0
	var max_rigid_error := 0.0
	var max_length_error := 0.0
	var max_hand_error := 0.0
	var flipped := 0
	var joint_flipped := 0
	var joint_degenerate := 0
	var joint_triangles_checked := 0
	var minimum_joint_triangle_area := INF
	var non_finite := 0
	var fold_examples := []
	var clips := {}
	for clip_name in skin.source_document.clips:
		var clip: Dictionary = skin.source_document.clips[clip_name]
		var samples: int = clip.boneFrames.size()
		var local_flips := 0
		for facing in [-1.0,1.0]:
			for frame in range(samples):
				var phase := float(frame)/maxf(1.0,float(samples-1))
				rig.set_character_pose(clip_name,phase*float(clip.duration),facing)
				pose_count += 1
				for key in helper.sleeves:
					var state: Dictionary = helper.sleeves[key]
					var vertices: PackedVector2Array = state.mesh.polygon
					for pair in rigid_pairs[key]:
						max_rigid_error = maxf(max_rigid_error,absf(vertices[pair.a].distance_to(vertices[pair.b])-float(pair.baseline)))
						pair_checks += 1
					var last: Dictionary = state.last
					max_length_error = maxf(max_length_error,absf(last.shoulder.distance_to(last.elbow)-float(helper.fixed_upper_length)))
					max_length_error = maxf(max_length_error,absf(last.elbow.distance_to(last.wrist)-float(helper.fixed_lower_length)))
					max_hand_error = maxf(max_hand_error,float(last.hand_scale_error))
					for point in vertices:
						if not point.is_finite(): non_finite += 1
					for tri in active_triangles[key]:
						var ids: PackedInt32Array = tri.indices
						var area := (vertices[ids[1]]-vertices[ids[0]]).cross(vertices[ids[2]]-vertices[ids[0]])
						if area*float(tri.sign)<-0.00001:
							flipped += 1
							local_flips += 1
							if fold_examples.size()<12: fold_examples.append({"side":key,"clip":clip_name,"phase":phase,"facing":facing,"indices":Array(ids),"area":area})
					var joint: Polygon2D = state.joint_mesh
					if joint.visible:
						var joint_sign := 0.0
						for ids in joint.polygons:
							var pa: Vector2 = joint.polygon[ids[0]]
							var pb: Vector2 = joint.polygon[ids[1]]
							var pc: Vector2 = joint.polygon[ids[2]]
							var area := (pb-pa).cross(pc-pa)
							joint_triangles_checked += 1
							minimum_joint_triangle_area = minf(minimum_joint_triangle_area,absf(area))
							if absf(area)<0.00000001: joint_degenerate += 1
							if joint_sign==0.0 and absf(area)>=0.00000001: joint_sign = signf(area)
							var expected_sign := joint_sign
							if joint.uv.size()==joint.polygon.size():
								var ua: Vector2 = joint.uv[ids[0]]
								var ub: Vector2 = joint.uv[ids[1]]
								var uc: Vector2 = joint.uv[ids[2]]
								var uv_area := (ub-ua).cross(uc-ua)
								if absf(uv_area)>0.00000001: expected_sign = signf(uv_area)
							if area*expected_sign<0.0: joint_flipped += 1
		clips[clip_name] = {"frames":samples,"facings":2,"visible_triangle_flips":local_flips}
	if max_rigid_error>TOLERANCE: failures.append("Rigid segment distances changed: "+str(max_rigid_error))
	if max_length_error>TOLERANCE: failures.append("Rendered arm lengths changed: "+str(max_length_error))
	if max_hand_error>TOLERANCE: failures.append("Hand changed size: "+str(max_hand_error))
	if non_finite>0: failures.append("Nonfinite sleeve vertices: "+str(non_finite))
	if flipped>0: failures.append("Visible sleeve triangles flipped: "+str(flipped))
	if joint_flipped>0: failures.append("Outer elbow fill triangles flipped: "+str(joint_flipped))
	if joint_degenerate>0: failures.append("Outer elbow fill triangles degenerated: "+str(joint_degenerate))
	var report := {"passed":failures.is_empty(),"failures":failures,"poses":pose_count,"rigid_pair_checks":pair_checks,"maximum_rigid_distance_error":max_rigid_error,"maximum_rendered_bone_length_error":max_length_error,"maximum_hand_width_error":max_hand_error,"visible_triangle_flips":flipped,"joint_triangle_checks":joint_triangles_checked,"joint_triangle_flips":joint_flipped,"joint_triangle_degeneracies":joint_degenerate,"minimum_joint_triangle_area":minimum_joint_triangle_area,"nonfinite_vertices":non_finite,"fold_examples":fold_examples,"clips":clips,"scope":"All evaluated source frames in both facings; every upper/forearm/cuff/hand mesh edge remains invariant, same fixed rendered lengths for both arms, native alpha>=0.5 triangle-centroid fold test, outer elbow fill fan UV orientation and area checks. This is geometry validation, not silhouette/art approval."}
	var file := FileAccess.open("res://output/ink_outfit_rigid_segments_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("RIGID SLEEVE REGRESSION ","PASS" if failures.is_empty() else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if failures.is_empty() else 1)
