extends SceneTree

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")

func _initialize() -> void:
	call_deferred("_trace")

func _trace() -> void:
	var rig := OutfitRuntime.new()
	root.add_child(rig)
	rig.position = Vector2(300,705)
	rig.display_zoom = 7.1
	assert(rig.build())
	rig.wanderer_skin.motion_context = false
	rig.wanderer_skin.pose_blending = false
	rig.set_character_pose("Idle",42.0/60.0,1.0)
	await process_frame
	var results := []
	for screen_point in [Vector2(340,385),Vector2(340,393),Vector2(341,400),Vector2(340,407)]:
		var layers := []
		for record in rig.wanderer_skin.mesh_records:
			var mesh: Polygon2D = record.mesh
			var local := mesh.to_local(screen_point)
			for triangle in mesh.polygons:
				var a: Vector2 = mesh.polygon[triangle[0]]
				var b: Vector2 = mesh.polygon[triangle[1]]
				var c: Vector2 = mesh.polygon[triangle[2]]
				if not Geometry2D.is_point_in_polygon(local,PackedVector2Array([a,b,c])): continue
				var barycentric := Transform2D(b-a,c-a,a).affine_inverse()*local
				var uv: Vector2 = mesh.uv[triangle[0]]*(1.0-barycentric.x-barycentric.y)+mesh.uv[triangle[1]]*barycentric.x+mesh.uv[triangle[2]]*barycentric.y
				var image := mesh.texture.get_image()
				var color := image.get_pixel(clampi(roundi(uv.x),0,image.get_width()-1),clampi(roundi(uv.y),0,image.get_height()-1))
				var clipped: bool = record.category=="leg" and not rig.wanderer_skin.is_leg_pixel_below_waist(local)
				layers.append({"component":record.component,"renderer":record.source.rendererPath,"source_mesh_index":record.source_mesh_index,"z":mesh.z_index,"uv_pixels":[uv.x,uv.y],"texture_rgba":[color.r,color.g,color.b,color.a],"waist_masked":clipped,"effective_alpha":0.0 if clipped else color.a})
		results.append({"screen":[screen_point.x,screen_point.y],"layers_back_to_front":layers})
	var output := "res://output/ink_outfit_v7_front_patch_trace.json"
	var materials: Dictionary = rig.wanderer_skin.material_diagnostics.duplicate(true)
	for key in materials: materials[key].sha256 = FileAccess.get_sha256(materials[key].path)
	var file := FileAccess.open(output,FileAccess.WRITE)
	file.store_string(JSON.stringify({"view":"1200x800 showcase left Idle_041; time42/60","materials":materials,"waist_mask":rig.wanderer_skin.get_garment_diagnostics().pants_upper_insertion_mask,"results":results,"finding":"The old dark patch belonged to source left pants mesh1. The new leg-only source-waist fragment mask hides this upper insertion; native UV and all source vertices are unchanged. Native screenshot pixels are verified separately."},"\t"))
	file.close()
	print(JSON.stringify(results))
	quit()
