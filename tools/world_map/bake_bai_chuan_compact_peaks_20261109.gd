extends SceneTree
const ART := "res://textures/world/bai_chuan/compact_peaks_20261109/"

func _initialize() -> void:
	for stem in ["peaks_compact_native", "bridge_topdown_native"]:
		var image := Image.new()
		if image.load_png_from_buffer(FileAccess.get_file_as_bytes(ART+stem+".png"))!=OK:
			quit(1)
			return
		var original:=image.get_data()
		image.generate_mipmaps()
		if ResourceSaver.save(ImageTexture.create_from_image(image),ART+stem+".res",ResourceSaver.FLAG_COMPRESS)!=OK:
			quit(1)
			return
		var saved: Texture2D=load(ART+stem+".res")
		var restored:=saved.get_image()
		restored.clear_mipmaps()
		if restored.get_data()!=original:
			push_error("Compact terrain RGBA changed")
			quit(1)
			return
		print("COMPACT_NATIVE_RGBA_OK ",stem," ",restored.get_size())
	quit(0)
