extends SceneTree

const ROOT := "res://mods/appearance/ink_portrait_pair_20261003_v3/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]

func _initialize() -> void:
	var failures: Array[String] = []
	for sex in ["male", "female"]:
		for part in PARTS:
			var path: String = ROOT + sex + "/" + part + ".png"
			var bytes := FileAccess.get_file_as_bytes(path)
			var img := Image.new()
			if bytes.is_empty() or img.load_png_from_buffer(bytes) != OK:
				failures.append("PNG load failed: " + path)
			elif img.get_width() != 1024 or img.get_height() != 1536:
				failures.append("Canvas mismatch: " + path)
	var rig_text := FileAccess.get_file_as_string(ROOT + "rig/portrait_rig_v3.json")
	var rig = JSON.parse_string(rig_text)
	if typeof(rig) != TYPE_DICTIONARY:
		failures.append("Rig missing or invalid canvas")
	else:
		var canvas: Array = rig.get("canvas", [])
		if canvas.size() != 2 or int(canvas[0]) != 1024 or int(canvas[1]) != 1536:
			failures.append("Rig missing or invalid canvas")
	if failures.is_empty():
		print("INK_PORTRAIT_V3_OK: 18 PNG layers decoded at 1024x1536; rig parsed")
		quit(0)
	else:
		for failure in failures:
			push_error(failure)
		quit(1)
