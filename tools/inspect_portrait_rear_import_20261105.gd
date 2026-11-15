extends SceneTree
func _initialize() -> void:
	var file := "res://mods/appearance/ink_three_variants_20261004/male/clothing_back/01.png"
	var tex: Texture2D = load(file)
	var img := tex.get_image()
	img.save_png("res://mods/appearance/portrait_male_faithful_restore_20261104_v5/installation/rear01_imported.png")
	quit()
