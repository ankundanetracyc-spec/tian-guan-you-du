extends SceneTree
func _initialize():
	var runtime = load("res://guigu_rig_runtime.gd").new()
	root.add_child(runtime)
	var loaded = runtime.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art")
	runtime.position = Vector2(400, 500)
	print("loaded=", loaded, " summary=", runtime.get_runtime_summary())
	for polygon in runtime.mesh_nodes:
		var bounds = Rect2(polygon.polygon[0], Vector2.ZERO)
		for vertex in polygon.polygon:
			bounds = bounds.expand(vertex)
		print("mesh=", polygon.name, " visible=", polygon.visible, " z=", polygon.z_index, " bounds=", bounds, " n=", polygon.polygon.size(), " tri=", polygon.polygons.size(), " tex=", polygon.texture.get_size() if polygon.texture != null else "null", " skeleton=", polygon.skeleton, " bones=", polygon.get_bone_count())
	print("root_transform=", runtime.rig_root_node.transform, " runtime=", runtime.transform)
	quit()
