extends SceneTree

# Authoring-only registration. Runtime never fits individual pose bounds.
const ROOT := "res://textures/characters/side_ink_chop_sample_20261015_v2/"
const CANVAS_SIZE := Vector2i(2048, 2048)
const GROUND_ORIGIN := Vector2(1024.0, 1900.0)

func _initialize() -> void:
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(ROOT + "export_manifest.json"))
	if not parsed is Dictionary:
		push_error("Invalid side-ink export manifest")
		quit(1)
		return
	for entry in parsed["poses"]:
		var source := Image.load_from_file(ProjectSettings.globalize_path(ROOT + str(entry["source"])))
		if source == null or source.is_empty():
			push_error("Missing source: " + str(entry["source"]))
			quit(1)
			return
		source.convert(Image.FORMAT_RGBA8)
		var authoring_scale: float = float(entry["authoring_scale"])
		var source_origin := Vector2(float(entry["source_origin"][0]), float(entry["source_origin"][1]))
		if not is_equal_approx(authoring_scale, 1.0):
			source.resize(roundi(source.get_width() * authoring_scale), roundi(source.get_height() * authoring_scale), Image.INTERPOLATE_LANCZOS)
		var offset := Vector2i((GROUND_ORIGIN - source_origin * authoring_scale).round())
		var target := Image.create(CANVAS_SIZE.x, CANVAS_SIZE.y, false, Image.FORMAT_RGBA8)
		target.fill(Color.TRANSPARENT)
		target.blit_rect(source, Rect2i(Vector2i.ZERO, source.get_size()), offset)
		var error := target.save_png(ROOT + str(entry["output"]))
		if error != OK:
			push_error("Unable to export: " + str(entry["output"]))
			quit(1)
			return
		print("Exported ", entry["output"], " canvas=2048x2048 anchor=(1024,1900) offset=", offset)
	quit(0)
