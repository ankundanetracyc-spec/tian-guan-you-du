extends SceneTree

## Run from either the main project or the isolated action-battle project.
## Exports a script-free editable native rest skeleton and its weighted skins.

func _initialize() -> void:
	call_deferred("_export_rest")


func _export_rest() -> void:
	var prefix := "res://tools/action_battle_test/" if FileAccess.file_exists("res://tools/action_battle_test/forest_wolf_rig.gd") else "res://"
	var folder := prefix + "art/forest_wolf_ingame_v3"
	var arguments := OS.get_cmdline_user_args()
	if not arguments.is_empty():
		folder = arguments[0]
	var rig = load(prefix + "forest_wolf_rig.gd").new()
	root.add_child(rig)
	if not rig.build(folder):
		push_error("Cannot export invalid wolf rig: " + str(rig.validation_errors))
		quit(1)
		return
	rig.name = "ForestWolfRest"
	rig.reset_pose()
	for bone in rig.bones.values():
		bone.apply_rest()
	rig.scale = Vector2.ONE * 0.55
	var expected_mesh_count: int = rig.meshes.size()
	# A lossless native texture keeps the scene portable between the full and
	# isolated projects without either project's PNG-import cache. Source PNG
	# remains untouched and the text scene does not embed raw pixel arrays.
	var texture_path := folder.path_join("source.png")
	var bitmap := Image.new()
	if bitmap.load(ProjectSettings.globalize_path(texture_path)) != OK:
		push_error("Cannot load wolf source.png for native rest-scene export.")
		quit(1)
		return
	bitmap.generate_mipmaps()
	var texture := PortableCompressedTexture2D.new()
	texture.set_keep_compressed_buffer(true)
	texture.create_from_image(bitmap, PortableCompressedTexture2D.COMPRESSION_MODE_LOSSLESS)
	var texture_resource_path := folder.path_join("wolf_atlas.res")
	var texture_result := ResourceSaver.save(texture, texture_resource_path)
	if texture_result != OK:
		push_error("Cannot save native wolf texture: " + str(texture_result))
		quit(1)
		return
	var imported: Texture2D = load(texture_resource_path)
	for mesh in rig.meshes:
		mesh.texture = imported
	# The debug overlay is a scripted convenience, not part of the native rig.
	var overlay: Node = rig.get_node_or_null("BoneOverlay")
	if overlay != null:
		overlay.free()
	for node in rig.find_children("*", "", true, false):
		node.owner = rig
	rig.set_script(null)
	var packed := PackedScene.new()
	var result := packed.pack(rig)
	if result == OK:
		result = ResourceSaver.save(packed, folder.path_join("forest_wolf_rest.tscn"), ResourceSaver.FLAG_RELATIVE_PATHS)
	if result == OK:
		# Godot's text-scene saver still writes res:// for texture resources.
		# A scene-relative reference resolves in either supported project root.
		var scene_path := folder.path_join("forest_wolf_rest.tscn")
		var scene_text := FileAccess.get_file_as_string(scene_path)
		scene_text = scene_text.replace("path=\"" + texture_resource_path + "\"", "path=\"wolf_atlas.res\"")
		var scene_file := FileAccess.open(scene_path, FileAccess.WRITE)
		scene_file.store_string(scene_text)
		scene_file.close()
	if result == OK:
		var reloaded: PackedScene = ResourceLoader.load(folder.path_join("forest_wolf_rest.tscn"), "PackedScene", ResourceLoader.CACHE_MODE_IGNORE)
		if reloaded == null:
			result = ERR_CANT_OPEN
		else:
			var instance := reloaded.instantiate()
			var native_only := instance.get_script() == null
			var skin_count := 0
			for node in instance.find_children("*", "", true, false):
				native_only = native_only and node.get_script() == null
				if node is Polygon2D and node.texture != null:
					skin_count += 1
					if node.texture.get_width() < 100 or node.get_node_or_null(node.skeleton) == null:
						result = ERR_INVALID_DATA
			if not native_only or skin_count != expected_mesh_count or not instance.scale.is_equal_approx(Vector2.ONE * 0.55):
				result = ERR_INVALID_DATA
			print("WOLF_REST_NATIVE_VERIFY scripts_free=", native_only, " skins=", skin_count, " scale=", instance.scale)
			instance.free()
	print("WOLF_REST_EXPORT result=", result)
	rig.queue_free()
	await process_frame
	quit(0 if result == OK else 1)
