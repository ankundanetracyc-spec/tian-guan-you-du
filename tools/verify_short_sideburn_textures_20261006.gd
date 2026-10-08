extends SceneTree
const PREFIX := "res://mods/appearance/ink_three_variants_20261004/"
func _initialize() -> void:
	var files: Array = []
	for relative in ["male/hair_front/08.png", "female/hair_front/08.png", "male/hair_back/08.png"]:
		var path: String = PREFIX + relative
		var texture := ResourceLoader.load(path) as Texture2D
		assert(texture != null)
		var actual := texture.get_image()
		actual.convert(Image.FORMAT_RGBA8)
		var source := Image.new()
		assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK)
		source.convert(Image.FORMAT_RGBA8)
		source.fix_alpha_edges()
		assert(actual.get_size() == Vector2i(1024,1536))
		assert(actual.get_data() == source.get_data(), "Imported texture differs: " + path)
		files.append(relative)
	var report := {"pass":true,"asset_revision":"short_sideburn_revision_20261006","texture_checks":files.size(),"files":files,"alpha_and_visible_colour":true}
	FileAccess.open(PREFIX + "source/short_sideburn_revision_20261006/runtime_texture_verification.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("SHORT_SIDEBURN_TEXTURE_PASS: ",files.size())
	quit(0)
