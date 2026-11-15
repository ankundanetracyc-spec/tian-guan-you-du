extends SceneTree

func _initialize() -> void:
	var preview = load("res://scripts/world/bai_chuan/BaiChuanNineTileTest.gd").new()
	assert(is_equal_approx(preview.get_world_size(), 3762.0))
	assert(is_equal_approx(preview.get_tile_size(), 1254.0))
	assert(preview._background_files().size() == 9)
	preview.add_to_group("free_world_map_presentation")
	assert(is_equal_approx(preview.get_world_size(), 12288.0))
	assert(is_equal_approx(preview.get_tile_size(), 2457.6))
	var files: Array[String] = preview._background_files()
	assert(files.size() == 25 and files[0] == "bc_r0c0.png" and files[-1] == "bc_r4c4.png")
	preview.world.free()
	preview.places.free()
	preview.free()
	print("BAICHUAN_FREE_5X5_GRID_OK_LEGACY_3X3_UNCHANGED")
	quit()
