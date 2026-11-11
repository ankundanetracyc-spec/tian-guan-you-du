extends SceneTree

var held_textures: Array[Texture2D] = []

func _initialize() -> void:
	var args: PackedStringArray = OS.get_cmdline_user_args()
	if args.size() != 1:
		push_error("Expected the workspace path")
		quit(1)
		return
	var workspace: String = args[0].replace("\\", "/")
	var pack: String = workspace.path_join("textures/ui/combat_board_20261107_v1")
	var manifest_value: Variant = JSON.parse_string(FileAccess.get_file_as_string(pack.path_join("skin_manifest.json")))
	if not manifest_value is Dictionary:
		push_error("Could not read the material manifest")
		quit(1)
		return
	var manifest: Dictionary = manifest_value
	var images: Dictionary = manifest["images"]
	for file_value: Variant in images.keys():
		var file: String = str(file_value)
		var image: Image = Image.load_from_file(pack.path_join(file))
		if image == null or image.is_empty():
			push_error("PNG did not decode: " + file)
			quit(1)
			return
		if image.get_pixel(0, 0).a != 0.0:
			push_error("PNG corner is not transparent: " + file)
			quit(1)
			return
		# Cache the unchanged PNG as its real project resource path. This isolates the
		# format check from the main game's editor/import state and C# autoloads.
		var texture: ImageTexture = ImageTexture.create_from_image(image)
		texture.take_over_path("res://textures/ui/combat_board_20261107_v1/" + file)
		held_textures.append(texture)
	var parts: Dictionary = manifest["parts"]
	var verified: Array[String] = []
	for key_value: Variant in parts.keys():
		var key: String = str(key_value)
		var part: Dictionary = parts[key]
		var resource: Resource = ResourceLoader.load(pack.path_join(str(part["atlas_resource"])))
		if not resource is AtlasTexture:
			push_error("Atlas resource failed: " + key)
			quit(1)
			return
		var atlas: AtlasTexture = resource as AtlasTexture
		var r: Array = part["rect"]
		var expected: Rect2 = Rect2(float(r[0]), float(r[1]), float(r[2]), float(r[3]))
		if atlas.region != expected or not atlas.filter_clip or not held_textures.has(atlas.atlas):
			push_error("Atlas region, clipping or source mismatch: " + key)
			quit(1)
			return
		if atlas.get_width() != int(r[2]) or atlas.get_height() != int(r[3]):
			push_error("Atlas dimensions mismatch: " + key)
			quit(1)
			return
		verified.append(key)
	var report: Dictionary = {
		"engine": Engine.get_version_info().get("string", ""),
		"png_count": images.size(),
		"atlas_count": verified.size(),
		"atlas_resources_loaded": verified,
		"png_corners_transparent": true,
		"sources_loaded_without_main_game": true,
		"passed": true
	}
	var output: FileAccess = FileAccess.open(pack.path_join("native_validation.json"), FileAccess.WRITE)
	output.store_string(JSON.stringify(report, "  ") + "\n")
	output.close()
	print(JSON.stringify(report))
	quit(0)
