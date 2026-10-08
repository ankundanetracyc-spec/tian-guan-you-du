extends SceneTree


func _initialize() -> void:
	var root := "res://textures/characters/c1_female_shadow_v1/"
	var names := ["body_core", "head_hair", "arm_waist", "arm_raised", "lower_body", "lower_kneel", "lower_empty", "ribbons"]
	for part in names:
		var source := FileAccess.get_file_as_string(root + "source/" + part + ".svg")
		var image := Image.new()
		var error := image.load_svg_from_string(source)
		if error != OK:
			push_error("Could not rasterize " + part + ": " + str(error))
			quit(1)
			return
		error = image.save_png(ProjectSettings.globalize_path(root + "layers/" + part + ".png"))
		if error != OK:
			push_error("Could not save " + part + ": " + str(error))
			quit(1)
			return
	var silhouette := Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
	silhouette.fill(Color.TRANSPARENT)
	for part in ["lower_body", "body_core", "ribbons", "arm_waist", "arm_raised", "head_hair"]:
		var layer := Image.load_from_file(root + "layers/" + part + ".png")
		var position := Vector2i.ZERO
		if part == "ribbons":
			layer.resize(553, 369)
			position = Vector2i(23, 16)
		silhouette.blend_rect(layer, Rect2i(Vector2i.ZERO, layer.get_size()), position)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(root + "preview/"))
	silhouette.save_png(ProjectSettings.globalize_path(root + "preview/female_shadow_static.png"))
	var preview := Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
	preview.fill(Color("e9dfd2"))
	preview.blend_rect(silhouette, Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	preview.save_png(ProjectSettings.globalize_path(root + "preview/female_shadow_setup.png"))
	quit()
