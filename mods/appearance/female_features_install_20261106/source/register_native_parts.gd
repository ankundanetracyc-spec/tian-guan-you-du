extends SceneTree
const P := "res://mods/appearance/female_features_install_20261106/"
func _initialize() -> void:
	push_error("Historical v2 export retired after oversized-eye rejection. Do not overwrite the current approved-source extraction.")
	quit(1)
	return

func _historical_export_do_not_run() -> void:
	for part: String in ["eyebrows", "eyes"]:
		var index: String = "04" if part == "eyebrows" else "03"
		var source := Image.new()
		assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(P + "candidates/" + part + "/" + index + ".png")) == OK)
		source.convert(Image.FORMAT_RGBA8)
		var out := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
		var offset := Vector2i(0, 0) if part == "eyebrows" else Vector2i(2, 0)
		out.blit_rect(source, Rect2i(0, 0, 1024 - offset.x, 1536), offset)
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(P + "female/" + part))
		assert(out.save_png(P + "female/" + part + "/" + index + ".png") == OK)
	print("NATIVE_SHARED_CANVAS_REGISTRATION_READY")
	quit()

