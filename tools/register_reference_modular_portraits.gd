extends SceneTree

# Import placement only. Generated artwork and native alpha remain in source_generated.
# Uniform scale keeps the drawing's aspect ratio. No skin warp or silhouette trimming.
const PACK = "res://mods/appearance/reference_face_sample_20260930/"

func _initialize():
	var config_file = FileAccess.open(PACK + "registration.json", FileAccess.READ)
	var config = JSON.parse_string(config_file.get_as_text())
	for entry in config:
		var source_path = entry.get("source", PACK + "source_generated/" + entry.sex + "/" + entry.part + "_0.png")
		var source = Image.load_from_file(ProjectSettings.globalize_path(source_path))
		assert(source != null and source.get_size() == Vector2i(1024, 1536))
		source.convert(Image.FORMAT_RGBA8)
		var result = Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
		result.fill(Color.TRANSPARENT)
		if entry.has("ear_roots"):
			var ear_scale = float(entry.scale)
			for side in range(2):
				var ear_canvas = Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
				ear_canvas.fill(Color.TRANSPARENT)
				# Split at the wholly transparent midline; no painted silhouette is cut.
				ear_canvas.blend_rect(source, Rect2i(side * 512, 0, 512, 1536), Vector2i(side * 512, 0))
				ear_canvas.resize(roundi(1024 * ear_scale), roundi(1536 * ear_scale), Image.INTERPOLATE_LANCZOS)
				var source_root = Vector2(float(entry.ear_roots[side][0]), float(entry.ear_roots[side][1]))
				var target_root = Vector2(float(entry.target_roots[side][0]), float(entry.target_roots[side][1]))
				result.blend_rect(ear_canvas, Rect2i(Vector2i.ZERO, ear_canvas.get_size()), Vector2i((target_root - source_root * ear_scale).round()))
		else:
			var uniform_scale = float(entry.scale)
			source.resize(roundi(1024 * uniform_scale), roundi(1536 * uniform_scale), Image.INTERPOLATE_LANCZOS)
			var source_anchor = Vector2(float(entry.source_anchor[0]), float(entry.source_anchor[1]))
			var target_anchor = Vector2(float(entry.target_anchor[0]), float(entry.target_anchor[1]))
			var origin = Vector2i((target_anchor - source_anchor * uniform_scale).round())
			result.blend_rect(source, Rect2i(Vector2i.ZERO, source.get_size()), origin)
		assert(result.save_png(PACK + "modular/" + entry.sex + "/" + entry.part + "_0.png") == OK)
		print("REFERENCE_PART_REGISTERED: " + entry.sex + "/" + entry.part)
	quit()
