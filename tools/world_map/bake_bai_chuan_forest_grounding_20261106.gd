extends SceneTree
const ART := "res://textures/world/bai_chuan/forest_grounding_20261106/"

func _initialize() -> void:
	for filename in ["trees_natural_native", "transitions_native"]:
		var image := Image.new()
		if image.load_png_from_buffer(FileAccess.get_file_as_bytes(ART+filename+".png"))!=OK:
			quit(1)
			return
		var original := image.get_data()
		image.generate_mipmaps()
		var texture := ImageTexture.create_from_image(image)
		if ResourceSaver.save(texture,ART+filename+".res",ResourceSaver.FLAG_COMPRESS)!=OK:
			quit(1)
			return
		var saved: Texture2D = load(ART+filename+".res")
		var decoded := saved.get_image()
		decoded.clear_mipmaps()
		if decoded.get_data()!=original:
			push_error("Grounding native RGBA changed")
			quit(1)
			return
		print("GROUNDING_NATIVE_RGBA_OK ",filename," ",decoded.get_size())
	quit(0)
