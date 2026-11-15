extends SceneTree
func _initialize() -> void:
	var source := Image.load_from_file("E:/stacklands/textures/characters/side_ink_sword_idle_right_20261024_v3/source/idle_no_sword.png")
	var detail := source.get_region(Rect2i(510,585,155,155))
	detail.resize(620,620,Image.INTERPOLATE_LANCZOS)
	detail.save_png("E:/stacklands/tmp/right_sword_idle_grip.png")
	quit()
