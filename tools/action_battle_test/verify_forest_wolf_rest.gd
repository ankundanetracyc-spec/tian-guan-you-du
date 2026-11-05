extends SceneTree

func _initialize() -> void:
	call_deferred("_verify")


func _verify() -> void:
	var base: String = get_script().resource_path.get_base_dir()
	var resource_path: String = base.path_join("art/forest_wolf_ingame_v3/forest_wolf_rest.tscn")
	var packed: PackedScene = load(resource_path)
	if packed == null:
		push_error("WOLF_REST_VERIFY cannot load native scene: " + resource_path)
		quit(1)
		return
	var wolf := packed.instantiate()
	root.add_child(wolf)
	var bones := wolf.find_children("*", "Bone2D", true, false)
	var meshes: Array = []
	for node in wolf.find_children("*", "Polygon2D", true, false):
		if node.texture != null:
			meshes.append(node)
	var clean_scripts := wolf.get_script() == null
	for node in wolf.find_children("*", "", true, false):
		clean_scripts = clean_scripts and node.get_script() == null
	var texture_ok := true
	var bindings_ok := true
	for value in meshes:
		var mesh: Polygon2D = value
		texture_ok = texture_ok and mesh.texture != null and mesh.texture.get_width() > 1000 and mesh.texture.get_height() > 1000
		var skel: Skeleton2D = mesh.get_node_or_null(mesh.skeleton)
		bindings_ok = bindings_ok and skel != null and mesh.get_bone_count() > 0
		if skel != null:
			for index in range(mesh.get_bone_count()):
				bindings_ok = bindings_ok and skel.get_node_or_null(mesh.get_bone_path(index)) is Bone2D
	var valid: bool = clean_scripts and texture_ok and bindings_ok and bones.size() == 26 and meshes.size() == 7 and wolf.scale.is_equal_approx(Vector2.ONE * 0.55)
	print("WOLF_REST_VERIFY ", "PASS" if valid else "FAIL", " bones=", bones.size(), " meshes=", meshes.size(), " script_free=", clean_scripts, " texture=", texture_ok, " bindings=", bindings_ok, " path=", resource_path)
	wolf.queue_free()
	await process_frame
	quit(0 if valid else 1)
