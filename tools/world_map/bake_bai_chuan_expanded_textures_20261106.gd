extends SceneTree
## Preserve generated RGBA and technical distance bytes in native resources.
const DATA := "res://resources/world/bai_chuan/landscape_expanded3x_20261106.json"

func _initialize() -> void:
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(DATA))
	var base: String = data.runtime_texture_base
	DirAccess.make_dir_recursive_absolute(base)
	var entries: Dictionary = {}
	for key in ["ground_texture", "water_texture", "tree_atlas", "bridge_atlas"]: entries[str(data[key])] = true
	for key in ["ground_tone_data", "water_distance"]: entries[str(data[key])] = false
	for bank: Dictionary in data.mountain_sources.values(): entries[str(bank.path)] = true
	var nodes: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://textures/world/bai_chuan/expanded3x_20261106/nodes/node_catalog.json"))
	for bank: Dictionary in nodes.atlases.values(): entries[str(bank.path)] = true
	var checks := 0
	for source: String in entries:
		var image := Image.new()
		if image.load_png_from_buffer(FileAccess.get_file_as_bytes(source)) != OK:
			push_error("扩图纹理加载失败 " + source)
			quit(1)
			return
		if bool(entries[source]): image.generate_mipmaps()
		var path := base + source.get_file().get_basename() + ".res"
		var texture := ImageTexture.create_from_image(image)
		if ResourceSaver.save(texture, path, ResourceSaver.FLAG_COMPRESS) != OK:
			push_error("扩图纹理保存失败 " + path)
			quit(1)
			return
		var reloaded: Texture2D = load(path)
		if reloaded.get_image().get_data() != image.get_data():
			push_error("扩图纹理未无损保留 " + path)
			quit(1)
			return
		checks += 1
		print("EXPANDED_LOSSLESS_TEXTURE_OK ", path)
	print("EXPANDED_TEXTURES_OK ",checks)
	quit()
