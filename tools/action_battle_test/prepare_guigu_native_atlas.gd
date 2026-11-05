extends SceneTree

func _initialize() -> void:
	var folder: String = get_script().resource_path.get_base_dir().path_join("art/guigu_native_first_20261105")
	var layout: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(folder.path_join("atlas_layout.json")))
	var atlas := Image.create(layout.width,layout.height,false,Image.FORMAT_RGBA8)
	atlas.fill(Color.TRANSPARENT)
	for entry in layout.parts:
		var source := Image.load_from_file(folder.path_join(entry.source_file))
		assert(source.get_size()==Vector2i(entry.size[0],entry.size[1]))
		var rect: Array = entry.atlas_rect
		atlas.blit_rect(source,Rect2i(Vector2i.ZERO,source.get_size()),Vector2i(rect[0],rect[1]))
	assert(atlas.save_png(folder.path_join("source_atlas_120x180.png"))==OK)
	var preview := Image.create(layout.width,layout.height,false,Image.FORMAT_RGBA8)
	preview.fill(Color("78817b"))
	preview.blend_rect(atlas,Rect2i(Vector2i.ZERO,atlas.get_size()),Vector2i.ZERO)
	preview.resize(1200,1800,Image.INTERPOLATE_NEAREST)
	assert(preview.save_png(folder.path_join("source_atlas_reference_10x.png"))==OK)
	print("NATIVE SOURCE ATLAS PREPARED: six original canvases, same pixels")
	quit()
