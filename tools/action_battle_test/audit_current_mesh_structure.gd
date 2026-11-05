extends "smoke_guigu_guard_run.gd"

func _vector_array(values) -> Array:
	var output := []
	for value in values: output.append([value.x,value.y])
	return output

func _snapshot(rig) -> Dictionary:
	var skin = rig.wanderer_skin
	var result := {"meshes":[],"bones":{}}
	for key in skin.bones:
		var bone: Bone2D = skin.bones[key]
		var point: Vector2 = rig.rig_root_node.to_local(bone.global_position)
		result.bones[key] = {"point":[point.x,point.y],"rotation":bone.rotation,"rest_origin":[bone.rest.origin.x,bone.rest.origin.y]}
	for record in skin.mesh_records:
		var mesh: Polygon2D = record.mesh
		var weights := []
		for row in record.weights: weights.append(Array(row))
		var transforms: Array[Transform2D] = []
		for index in range(record.keys.size()): transforms.append(rig.rig_root_node.global_transform.affine_inverse()*skin.bones[record.keys[index]].global_transform*record.rest[index].affine_inverse())
		var deformed := PackedVector2Array()
		for index in range(mesh.polygon.size()):
			var point := Vector2.ZERO
			for bone_index in range(record.keys.size()): point += (transforms[bone_index]*mesh.polygon[index])*float(record.weights[bone_index][index])
			deformed.append(point)
		var polygons := []
		for polygon in mesh.polygons: polygons.append(Array(polygon))
		var children := []
		for child in mesh.get_children():
			if child is Polygon2D:
				var triangles := []
				for polygon in child.polygons: triangles.append(Array(polygon))
				children.append({"name":child.name,"z_index":child.z_index,"triangles":triangles})
		result.meshes.append({"name":record.name,"keys":record.keys,"uv":_vector_array(mesh.uv),"vertices":_vector_array(mesh.polygon),"deformed":_vector_array(deformed),"weights":weights,"triangles":polygons,"children":children})
	return result

func _ready() -> void:
	var destination := "E:/stacklands/docs/美术规范/鬼谷原作网格与素材重构_20261104/证据图"
	DirAccess.make_dir_recursive_absolute(destination)
	var rig := _make_rig()
	var output := {"script_sha256":FileAccess.get_sha256("res://reference_walk_skin.gd"),"bind":_snapshot(rig),"running":[]}
	for frame in range(180):
		var clock := float(frame+1)/60.0
		rig.wanderer_skin.set_motion_context(1.0/60.0,Vector2(114,0),clock,1.0)
		rig.set_character_pose("Run_New1",clock,1.0)
		if frame in [150,160,170,179]:
			var snapshot := _snapshot(rig)
			snapshot.frame = frame
			snapshot.phase = rig.wanderer_skin.gait_phase
			output.running.append(snapshot)
	var file := FileAccess.open(destination.path_join("玉虚实际网格与权重.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(output))
	file.close()
	print("CURRENT MESH AUDIT COMPLETE")
	get_tree().quit()
