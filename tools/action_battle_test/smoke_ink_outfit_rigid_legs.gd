extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")
const TOLERANCE := 0.002

func _triangles(mesh: Polygon2D) -> Array:
	if not mesh.polygons.is_empty(): return mesh.polygons
	var result := []
	var indices := Geometry2D.triangulate_polygon(mesh.polygon)
	for offset in range(0,indices.size(),3): result.append(PackedInt32Array([indices[offset],indices[offset+1],indices[offset+2]]))
	return result

func _inside_or_on_boundary(point: Vector2, polygon: PackedVector2Array) -> bool:
	if polygon.size()>=3 and Geometry2D.is_point_in_polygon(point,polygon): return true
	for index in range(polygon.size()):
		var a: Vector2 = polygon[index]
		var b: Vector2 = polygon[(index+1)%polygon.size()]
		var delta := b-a
		var nearest := a+delta*clampf((point-a).dot(delta)/maxf(delta.length_squared(),0.00000001),0.0,1.0)
		if nearest.distance_to(point)<=TOLERANCE: return true
	return false

func _ready() -> void:
	var failures: Array[String] = []
	var rig := OutfitRuntime.new()
	add_child(rig)
	if not rig.build():
		push_error("Rigid leg regression cannot build: "+str(rig.load_errors))
		get_tree().quit(1)
		return
	var skin = rig.wanderer_skin
	skin.motion_context = false
	skin.pose_blending = false
	skin.hem_secondary_enabled = false
	var helper = skin.leg_reconstruction
	if helper==null:
		push_error("Fixed-length leg helper missing")
		get_tree().quit(1)
		return
	rig.set_character_pose("Idle",0.0,1.0)
	var regions := {}
	var ankle_images := {}
	for side in helper.legs:
		var state: Dictionary = helper.legs[side]
		ankle_images[side] = state.ankle_joint_mesh.texture.get_image()
		for piece in ["upper_mesh","lower_mesh","foot_mesh","joint_mesh"]:
			var mesh: Polygon2D = state[piece]
			var image := mesh.texture.get_image()
			var pairs := []
			var seen := {}
			var triangles := []
			for ids in _triangles(mesh):
				var ua: Vector2 = mesh.uv[ids[0]]
				var ub: Vector2 = mesh.uv[ids[1]]
				var uc: Vector2 = mesh.uv[ids[2]]
				var uv := (ua+ub+uc)/3.0
				var pixel := Vector2i(clampi(int(uv.x),0,image.get_width()-1),clampi(int(uv.y),0,image.get_height()-1))
				if image.get_pixelv(pixel).a>=0.5: triangles.append({"ids":ids,"sign":signf((ub-ua).cross(uc-ua))})
				for edge in [[ids[0],ids[1]],[ids[1],ids[2]],[ids[2],ids[0]]]:
					var ia: int = edge[0]
					var ib: int = edge[1]
					var token := str(mini(ia,ib))+":"+str(maxi(ia,ib))
					if seen.has(token): continue
					seen[token] = true
					pairs.append({"a":ia,"b":ib,"baseline":mesh.polygon[ia].distance_to(mesh.polygon[ib])})
			regions[str(side)+"_"+piece] = {"mesh":mesh,"pairs":pairs,"triangles":triangles}
	var max_rigid_error := 0.0
	var max_length_error := 0.0
	var nonfinite := 0
	var flips := 0
	var pose_count := 0
	var edge_checks := 0
	var clips := {}
	var examples := []
	var foot_relative_angles := {"near":[],"far":[]}
	var ankle_triangle_checks := 0
	var ankle_flips := 0
	var ankle_degeneracies := 0
	var ankle_boundary_checks := 0
	var ankle_boundary_misses := 0
	var minimum_ankle_alpha := 1.0
	for clip_name in skin.source_document.clips:
		var clip: Dictionary = skin.source_document.clips[clip_name]
		var samples: int = clip.boneFrames.size()
		var clip_flips := 0
		for facing in [-1.0,1.0]:
			for frame in range(samples):
				var phase := float(frame)/maxf(1.0,float(samples-1))
				rig.set_character_pose(clip_name,phase*float(clip.duration),facing)
				pose_count += 1
				for side in helper.legs:
					var last: Dictionary = helper.legs[side].last
					max_length_error = maxf(max_length_error,absf(last.hip.distance_to(last.knee)-float(helper.fixed_upper_length)))
					max_length_error = maxf(max_length_error,absf(last.knee.distance_to(last.ankle)-float(helper.fixed_lower_length)))
					var state: Dictionary = helper.legs[side]
					var ankle_mesh: Polygon2D = state.ankle_joint_mesh
					var ankle_poly: PackedVector2Array = ankle_mesh.polygon
					for point in ankle_poly:
						if not point.is_finite(): nonfinite += 1
					# Joint topology may change between triangular/quadrilateral and
					# a collapsed edge in a straight pose. Triangulate current hull.
					for ids in _triangles(ankle_mesh):
						var pa: Vector2 = ankle_poly[ids[0]]
						var pb: Vector2 = ankle_poly[ids[1]]
						var pc: Vector2 = ankle_poly[ids[2]]
						var ua: Vector2 = ankle_mesh.uv[ids[0]]
						var ub: Vector2 = ankle_mesh.uv[ids[1]]
						var uc: Vector2 = ankle_mesh.uv[ids[2]]
						var area := (pb-pa).cross(pc-pa)
						var uv_area := (ub-ua).cross(uc-ua)
						ankle_triangle_checks += 1
						if absf(area)<0.00000001: ankle_degeneracies += 1
						if area*uv_area<0.0: ankle_flips += 1
						var image: Image = ankle_images[side]
						var uv := (ua+ub+uc)/3.0
						var pixel := Vector2i(clampi(int(uv.x),0,image.get_width()-1),clampi(int(uv.y),0,image.get_height()-1))
						minimum_ankle_alpha = minf(minimum_ankle_alpha,image.get_pixelv(pixel).a)
					for x in [17.65,24.75]:
						var fraction := float(x)/50.0
						var calf_edge: Vector2 = state.lower_mesh.polygon[3].lerp(state.lower_mesh.polygon[2],fraction)
						var foot_edge: Vector2 = state.foot_mesh.polygon[0].lerp(state.foot_mesh.polygon[1],fraction)
						for point in [calf_edge,foot_edge,calf_edge.lerp(foot_edge,0.5)]:
							ankle_boundary_checks += 1
							if not _inside_or_on_boundary(point,ankle_poly): ankle_boundary_misses += 1
					if clip_name=="Run_New1" and facing>0.0:
						var calf_mesh: Polygon2D = helper.legs[side].lower_mesh
						var foot_mesh: Polygon2D = helper.legs[side].foot_mesh
						var calf_axis := calf_mesh.polygon[1]-calf_mesh.polygon[0]
						var foot_axis := foot_mesh.polygon[1]-foot_mesh.polygon[0]
						foot_relative_angles[side].append(calf_axis.angle_to(foot_axis))
				for key in regions:
					var region: Dictionary = regions[key]
					var vertices: PackedVector2Array = region.mesh.polygon
					for point in vertices:
						if not point.is_finite(): nonfinite += 1
					for edge in region.pairs:
						max_rigid_error = maxf(max_rigid_error,absf(vertices[edge.a].distance_to(vertices[edge.b])-float(edge.baseline)))
						edge_checks += 1
					for triangle in region.triangles:
						var ids: PackedInt32Array = triangle.ids
						var area := (vertices[ids[1]]-vertices[ids[0]]).cross(vertices[ids[2]]-vertices[ids[0]])
						if area*float(triangle.sign)<-0.00001:
							flips += 1
							clip_flips += 1
							if examples.size()<10: examples.append({"region":key,"clip":clip_name,"phase":phase,"facing":facing,"indices":Array(ids),"area":area})
		clips[clip_name] = {"frames":samples,"facings":2,"visible_triangle_flips":clip_flips}
	if max_rigid_error>TOLERANCE: failures.append("Thigh/calf/boot distances changed: "+str(max_rigid_error))
	if max_length_error>TOLERANCE: failures.append("Rendered leg lengths changed: "+str(max_length_error))
	if flips>0: failures.append("Visible leg triangles flipped: "+str(flips))
	if nonfinite>0: failures.append("Nonfinite leg vertices: "+str(nonfinite))
	if ankle_flips>0 or ankle_degeneracies>0: failures.append("Ankle support fold/degeneracy: "+str(ankle_flips)+"/"+str(ankle_degeneracies))
	if ankle_boundary_misses>0: failures.append("Ankle support does not bridge cut edges: "+str(ankle_boundary_misses))
	if minimum_ankle_alpha<0.5: failures.append("Ankle support sampled transparent cloth: "+str(minimum_ankle_alpha))
	var foot_angle_ranges := {}
	for side in foot_relative_angles:
		var angles: Array = foot_relative_angles[side]
		var low := INF
		var high := -INF
		# Unwrap around first value so a branch cut cannot fake independent motion.
		var reference: float = angles[0]
		for angle in angles:
			var unwrapped := reference+wrapf(float(angle)-reference,-PI,PI)
			low = minf(low,unwrapped)
			high = maxf(high,unwrapped)
		foot_angle_ranges[side] = {"min":low,"max":high,"span":high-low}
		if high-low<0.01: failures.append("Foot does not articulate independently of calf: "+str(side))
	var report := {"passed":failures.is_empty(),"failures":failures,"poses":pose_count,"rigid_edge_checks":edge_checks,"maximum_rigid_distance_error":max_rigid_error,"maximum_shared_leg_length_error":max_length_error,"visible_triangle_flips":flips,"nonfinite_vertices":nonfinite,"clips":clips,"fold_examples":examples,"run_foot_rotation_relative_to_calf":foot_angle_ranges,"ankle_support_triangle_checks":ankle_triangle_checks,"ankle_support_flips":ankle_flips,"ankle_support_degeneracies":ankle_degeneracies,"ankle_support_boundary_checks":ankle_boundary_checks,"ankle_support_boundary_misses":ankle_boundary_misses,"minimum_ankle_support_alpha":minimum_ankle_alpha,"scope":"All evaluated source frames in both facings. All thigh/calf/foot and rigid knee support mesh edges preserve initial lengths; both legs use the same fixed thigh/calf lengths. Native alpha>=0.5 triangle-centroid fold check. Ankle support is a permitted local changing hull, tested using current triangulation for finite geometry, UV orientation, opacity and coverage of calf/foot cut edges. Foot must articulate independently in Run_New1. Does not assert robe occlusion or overall visual acceptance."}
	var file := FileAccess.open("res://output/ink_outfit_rigid_legs_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("RIGID LEGS REGRESSION ","PASS" if failures.is_empty() else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if failures.is_empty() else 1)
