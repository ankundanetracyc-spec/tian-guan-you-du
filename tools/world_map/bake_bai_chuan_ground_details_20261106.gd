extends SceneTree
## Keep every generated base pixel and its alpha unchanged in an exportable
## native resource; mip levels are solely for stable zoomed sampling.
const ART := "res://textures/world/bai_chuan/ground_details_20261106/"

func _initialize() -> void:
	var image := Image.new()
	if image.load_png_from_buffer(FileAccess.get_file_as_bytes(ART + "ground_details_native.png")) != OK:
		push_error("Ground detail source could not load")
		quit(1)
		return
	var source := image.get_data()
	image.generate_mipmaps()
	var texture := ImageTexture.create_from_image(image)
	if ResourceSaver.save(texture, ART + "ground_details_native.res", ResourceSaver.FLAG_COMPRESS) != OK:
		quit(1)
		return
	var saved: Texture2D = load(ART + "ground_details_native.res")
	var restored := saved.get_image()
	restored.clear_mipmaps()
	if restored.get_data() != source:
		push_error("Ground detail base pixels changed")
		quit(1)
		return
	print("GROUND_DETAILS_NATIVE_LOSSLESS_OK ", restored.get_size())
	quit(0)
