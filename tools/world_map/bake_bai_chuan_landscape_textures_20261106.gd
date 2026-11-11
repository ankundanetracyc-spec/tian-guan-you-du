extends SceneTree
## Lossless engine resources retain hidden RGB label bytes even where A=0.
const BASE := "res://textures/world/bai_chuan/layered_20261106/"

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(BASE + "runtime")
	for entry in [["feature_data.png", false], ["ground_tone_data.png", false], ["source/ground_native.png", true], ["source/trees_native.png", true], ["source/mountains_solid_a.png", true], ["source/mountains_solid_b.png", true]]:
		var source := BASE + str(entry[0])
		var image := Image.new()
		if image.load_png_from_buffer(FileAccess.get_file_as_bytes(source)) != OK:
			push_error("无法读取分层纹理 " + source)
			quit(1)
			return
		if bool(entry[1]): image.generate_mipmaps()
		var path := BASE + "runtime/" + source.get_file().get_basename() + ".res"
		var texture := ImageTexture.create_from_image(image)
		if ResourceSaver.save(texture, path, ResourceSaver.FLAG_COMPRESS) != OK:
			push_error("无法保存分层纹理 " + path)
			quit(1)
			return
		var reloaded: Texture2D = load(path)
		if reloaded.get_image().get_data() != image.get_data():
			push_error("分层纹理数据未无损保留 " + path)
			quit(1)
			return
		print("LOSSLESS_TEXTURE_OK " + path)
	quit()
