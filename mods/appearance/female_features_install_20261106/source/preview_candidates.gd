extends SceneTree

const A := "res://mods/appearance/ink_three_variants_20261004/female/"
const P := "res://mods/appearance/female_features_install_20261106/"
const COLORS := {"light": Color("#e8e1d6"), "dark": Color("#20272f")}

func _initialize() -> void:
	call_deferred("_run")

func image_file(path: String) -> Image:
	var result := Image.new()
	assert(result.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK)
	result.convert(Image.FORMAT_RGBA8)
	return result

func compose(brow_path: String, eye_path: String, background: Color) -> Image:
	var out := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
	out.fill(background)
	var layers: Array[String] = [
		A + "hair_back/01.png", A + "face_shape/04.png", A + "clothing/01.png",
		A + "nose/01.png", eye_path, brow_path, A + "mouth/01.png",
		A + "face_accessory/01.png", A + "hair_front/01.png"
	]
	for path in layers:
		out.blend_rect(image_file(path), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	return out

func _run() -> void:
	var preview_v4 := OS.get_cmdline_user_args().has("--eye-v4")
	var new_eye_file: String = P + ("candidates/eyes/registered_v4_10point.png" if preview_v4 else "female/eyes/03.png")
	var suffix: String = "_v4" if preview_v4 else ""
	var variants: Array[Dictionary] = [
		{"brow": A + "eyebrows/01.png", "eye": A + "eyes/01.png", "name": "old_base"},
		{"brow": A + "eyebrows/02.png", "eye": A + "eyes/01.png", "name": "old_brow02"},
		{"brow": A + "eyebrows/03.png", "eye": A + "eyes/01.png", "name": "old_brow03"},
		{"brow": P + "female/eyebrows/04.png", "eye": A + "eyes/01.png", "name": "new_brow"},
		{"brow": A + "eyebrows/01.png", "eye": A + "eyes/02.png", "name": "old_eye02"},
		{"brow": A + "eyebrows/01.png", "eye": new_eye_file, "name": "new_eye"},
		{"brow": P + "female/eyebrows/04.png", "eye": new_eye_file, "name": "new_both"}
	]
	for theme: String in COLORS:
		var contact := Image.create_empty(1560, 1320, false, Image.FORMAT_RGBA8)
		contact.fill(COLORS[theme])
		for i in variants.size():
			var spec: Dictionary = variants[i]
			var out := compose(str(spec["brow"]), str(spec["eye"]), COLORS[theme])
			assert(out.save_png(P + "qa/" + str(spec["name"]) + "_" + theme + suffix + ".png") == OK)
			var crop: Image = out.get_region(Rect2i(390, 320, 260, 220))
			crop.resize(520, 440, Image.INTERPOLATE_LANCZOS)
			contact.blit_rect(crop, Rect2i(0, 0, 520, 440), Vector2i((i % 3) * 520, floori(float(i) / 3.0) * 440))
		assert(contact.save_png(P + "qa/contact_" + theme + suffix + ".png") == OK)
	print("CANDIDATE_PREVIEW_READY")
	quit()

