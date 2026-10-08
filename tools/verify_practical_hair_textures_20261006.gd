extends SceneTree

const PREFIX := "res://mods/appearance/ink_three_variants_20261004/"

func _initialize() -> void:
	var files: Array = []
	var sex := "female" if "--female" in OS.get_cmdline_user_args() else "male"
	var revision := "female_practical_hair_expansion_20261006" if sex == "female" else "practical_hair_expansion_20261006"
	for slot in ["hair_front", "hair_back"]:
		for number in [6,7,8]:
			var path: String = PREFIX + sex + "/" + slot + "/%02d.png" % number
			var texture := ResourceLoader.load(path) as Texture2D
			assert(texture != null, "Missing imported texture: " + path)
			var actual := texture.get_image()
			actual.convert(Image.FORMAT_RGBA8)
			var source := Image.new()
			assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK)
			source.convert(Image.FORMAT_RGBA8)
			source.fix_alpha_edges()
			assert(actual.get_size() == Vector2i(1024,1536))
			assert(actual.get_data() == source.get_data(), "Imported texture differs: " + path)
			files.append(path)
	var report := {"pass":true,"asset_revision":revision,"texture_checks":files.size(),"files":files,"alpha_and_visible_colour":true}
	FileAccess.open(PREFIX + "source/" + revision + "/runtime_texture_verification.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("PRACTICAL_HAIR_TEXTURE_PASS: ", files.size())
	quit(0)
