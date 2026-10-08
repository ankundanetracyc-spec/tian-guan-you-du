extends SceneTree

const ASSET_ROOT := "res://mods/appearance/ink_portrait_pair_20261003_v4/"
const OUTPUT := ASSET_ROOT + "preview/"
const ORDER := ["hair_back", "face_shape", "clothing", "eyes", "eyebrows", "nose", "mouth", "face_accessory", "hair_front"]

func _initialize() -> void:
	call_deferred("verify")

func wait_for_ui() -> void:
	for frame in range(4):
		await process_frame
	await RenderingServer.frame_post_draw

func expected(sex: String, hidden: String = "") -> Image:
	var result := Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
	result.fill(Color(0, 0, 0, 0))
	for part in ORDER:
		if part == hidden:
			continue
		var source := Image.new()
		assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(ASSET_ROOT + sex + "/" + part + ".png")) == OK)
		assert(source.get_size() == Vector2i(1024, 1536))
		source.convert(Image.FORMAT_RGBA8)
		result.blend_rect(source, Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	return result

func verify() -> void:
	root.size = Vector2i(1672, 941)
	var start = load("res://tscn/scene/StartMenu.tscn").instantiate()
	root.add_child(start)
	current_scene = start
	await wait_for_ui()
	var entry = start.find_child("AppearanceCreatorTest", true, false)
	assert(entry is Button)
	entry.emit_signal("pressed")
	await wait_for_ui()
	var scene = current_scene
	assert(scene.scene_file_path == "res://tscn/scene/CharacterAppearanceTest.tscn")
	var gender = scene.find_child("GenderChoice", true, false)
	var preview = scene.find_child("PortraitPreview", true, false)
	assert(gender is OptionButton and preview is TextureRect)
	for sex in ["male", "female"]:
		var index := 1 if sex == "female" else 0
		gender.select(index)
		gender.emit_signal("item_selected", index)
		await wait_for_ui()
		var actual: Image = preview.texture.get_image()
		assert(actual.get_data() == expected(sex).get_data(), "C# compositor differs from selected nine-part package")
		assert(actual.save_png(OUTPUT + sex + "_godot_composite.png") == OK)
		assert(root.get_texture().get_image().save_png(OUTPUT + sex + "_game_test.png") == OK)
		for part in ORDER:
			var check = scene.find_child("Part_" + part, true, false)
			assert(check is CheckButton)
			check.button_pressed = false
			await process_frame
			assert(preview.texture.get_image().get_data() == expected(sex, part).get_data(), "Part toggle failed: " + part)
			if part in ["hair_front", "hair_back", "eyes"]:
				await wait_for_ui()
				assert(root.get_texture().get_image().save_png(OUTPUT + sex + "_game_without_" + part + ".png") == OK)
			check.button_pressed = true
			await process_frame
		assert(preview.texture.get_image().get_data() == actual.get_data(), "Restore failed")
		print("INK_PORTRAIT_V4_UI_PASS: ", sex, "; nine toggles; current C# composite matches selected assets")
	print("INK_PORTRAIT_V4_ENTRY_PASS: start menu -> nine-part test; both sexes verified")
	scene.queue_free()
	await wait_for_ui()
	quit(0)
