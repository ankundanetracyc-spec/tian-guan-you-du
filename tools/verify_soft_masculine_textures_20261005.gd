extends SceneTree

const PACK := "res://mods/appearance/ink_three_variants_20261004/"

func _initialize() -> void:
	var checked: Array[String] = []
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(PACK + "portrait_manifest.json"))
	var indices := [1,4,5,6,7,8,9] if manifest.has("style_expansion") else [4,5,6]
	for part in ["eyes", "eyebrows"]:
		for index in indices:
			var relative := "male/%s/%02d.png" % [part, index]
			var texture: Texture2D = ResourceLoader.load(PACK + relative, "Texture2D", ResourceLoader.CACHE_MODE_IGNORE)
			assert(texture != null, "Missing runtime texture: " + relative)
			var imported := texture.get_image()
			var source := Image.new()
			assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(PACK + relative)) == OK)
			imported.convert(Image.FORMAT_RGBA8)
			source.convert(Image.FORMAT_RGBA8)
			# Match the existing importer's alpha edge repair before comparing RGB.
			# This fills edge colours for filtered rendering without changing alpha.
			var options := ConfigFile.new()
			assert(options.load(PACK + relative + ".import") == OK)
			if options.get_value("params", "process/fix_alpha_border", false):
				source.fix_alpha_edges()
			assert(imported.get_size() == source.get_size())
			var actual := imported.get_data()
			var expected := source.get_data()
			for pixel in range(0, expected.size(), 4):
				assert(actual[pixel + 3] == expected[pixel + 3], "Stale alpha: " + relative)
				if expected[pixel + 3] > 0:
					assert(actual[pixel] == expected[pixel] and actual[pixel + 1] == expected[pixel + 1] and actual[pixel + 2] == expected[pixel + 2], "Stale colour: " + relative)
			checked.append(relative)
	var report := {"pass": true, "files": checked, "all_alpha_and_visible_rgb_match_import_processed_png": true, "existing_alpha_edge_import_setting_applied": true}
	var directory := "sclera_refinement_20261005" if manifest.has("sclera_revision") else "masculine_soft_revision_20261005"
	if manifest.has("natural_eye_revision"):
		directory = "natural_eye_scale_20261005"
	if manifest.has("style_expansion"):
		directory = "style_expansion_20261006"
	if manifest.has("eye_diversity_revision"):
		directory = "eye_diversity_revision_20261006"
	if manifest.has("eye_width_revision"):
		directory = "eye_width_revision_20261006"
	FileAccess.open(PACK + "source/" + directory + "/runtime_texture_verification.json", FileAccess.WRITE).store_string(JSON.stringify(report, "\t"))
	print("SOFT_MALE_RUNTIME_TEXTURES_PASS: ", checked.size(), " imported layers match current PNGs")
	quit(0)
