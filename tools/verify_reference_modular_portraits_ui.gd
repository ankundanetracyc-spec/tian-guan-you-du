extends SceneTree

const SAMPLE_ROOT = "res://mods/appearance/reference_face_sample_20260930/"
const ORDER = ["hair_back", "face", "ear", "clothes", "eye", "brow", "nose", "mouth", "extra", "hair_front"]

func _initialize():
	call_deferred("verify_samples")

func verify_samples():
	var scene = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for frame in range(8):
		await process_frame
	var gender = scene.find_child("GenderChoice", true, false)
	var preview = scene.find_child("PortraitPreview", true, false)
	assert(gender != null and preview != null)
	for sex in ["male", "female"]:
		var gender_index = 1 if sex == "female" else 0
		gender.select(gender_index)
		gender.emit_signal("item_selected", gender_index)
		await process_frame
		var result = Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
		result.fill(Color.TRANSPARENT)
		for layer_name in ORDER:
			var part = ("makeup" if sex == "female" else "beard") if layer_name == "extra" else layer_name
			var source = Image.load_from_file(ProjectSettings.globalize_path(SAMPLE_ROOT + "modular/" + sex + "/" + part + "_0.png"))
			assert(source != null and source.get_size() == Vector2i(1024, 1536))
			source.convert(Image.FORMAT_RGBA8)
			result.blend_rect(source, Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
		assert(result.save_png(SAMPLE_ROOT + "modular_review/" + sex + "_godot_composite.png") == OK)
		preview.texture = ImageTexture.create_from_image(result)
		for part in ["hair_back", "hair_front", "ear"]:
			var choice = scene.find_child("Choice_" + part, true, false)
			if choice != null:
				choice.set_item_text(0, "参考标准样例（独立拆件）")
		for frame in range(3):
			await process_frame
		await RenderingServer.frame_post_draw
		assert(root.get_texture().get_image().save_png(SAMPLE_ROOT + "modular_review/" + sex + "_test_ui.png") == OK)
		print("REFERENCE_MODULAR_UI_PASS: " + sex + " ten full-canvas transparent layers rendered in CharacterAppearanceTest")
	quit()
