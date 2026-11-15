extends SceneTree
const P := "res://mods/appearance/female_features_install_20261106/qa/"
func _initialize() -> void:
	var specs: Array[Dictionary] = [
		{"file": "res://mods/appearance/female_aligned_features_20261106/preview/01_soft_arch_almond.png", "rect": Rect2i(420, 337, 200, 34), "name": "approved_brow_close.png", "scale": Vector2i(1200, 204)},
		{"file": "res://mods/appearance/female_aligned_features_20261106/preview/02_soft_straight_long_eyes.png", "rect": Rect2i(420, 364, 200, 42), "name": "approved_eyes_close.png", "scale": Vector2i(1200, 252)}
	]
	for spec: Dictionary in specs:
		var source := Image.new()
		assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(str(spec["file"]))) == OK)
		var out: Image = source.get_region(spec["rect"])
		out.resize(spec["scale"].x, spec["scale"].y, Image.INTERPOLATE_LANCZOS)
		assert(out.save_png(P + str(spec["name"])) == OK)
	print("APPROVED_FEATURE_REFERENCES_READY")
	quit()

