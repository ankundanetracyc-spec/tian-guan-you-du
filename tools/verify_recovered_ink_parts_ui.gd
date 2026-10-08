extends SceneTree

const ASSET_ROOT := "res://mods/appearance/ink_recovered_parts_20261004/"
const RIG_ROOT := "res://mods/appearance/ink_recovered_parts_20261004/rig/"
const SLOTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const ORDER := [1, 6, 7, 3, 2, 4, 5, 8, 0]

func _initialize() -> void:
	call_deferred("verify")

func wait_for_ui() -> void:
	for frame in range(3):
		await process_frame
	await RenderingServer.frame_post_draw

func expected(sex: String, variants: Array, hidden: String = "", anchors: bool = false) -> Image:
	var result := Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
	result.fill(Color(0, 0, 0, 0))
	for i in ORDER:
		var part: String = SLOTS[i]
		if part == hidden:
			continue
		var source := Image.new()
		assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(ASSET_ROOT + sex + "/" + part + "/%02d.png" % (variants[i] + 1))) == OK)
		assert(source.get_size() == Vector2i(1024, 1536))
		source.convert(Image.FORMAT_RGBA8)
		result.blend_rect(source, Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	if anchors:
		var guide := Image.new()
		assert(guide.load_png_from_buffer(FileAccess.get_file_as_bytes(RIG_ROOT + sex + "_overlay.png")) == OK)
		result.blend_rect(guide, Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	return result

func choose(scene: Node, part: int, value: int) -> void:
	var choice: OptionButton = scene.find_child("Variant_" + SLOTS[part], true, false)
	var female: bool = scene.find_child("GenderChoice", true, false).selected == 1
	assert(choice.item_count == variant_count(female, part))
	choice.select(value)
	choice.emit_signal("item_selected", value)

func variant_count(female: bool, part: int) -> int:
	return 3 if part >= 7 else ((6 if female else 3) + (3 if part == 2 or part == 3 else 0))

func selections(scene: Node) -> Array:
	var values: Array = []
	for part in SLOTS:
		values.append(scene.find_child("Variant_" + part, true, false).selected)
	return values

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
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	var preview: TextureRect = scene.find_child("PortraitPreview", true, false)
	var anchor_check: CheckButton = scene.find_child("AnchorGuide", true, false)
	var remembered: Dictionary = {}
	var selection_checks := 0
	for sex in ["male", "female"]:
		var index := 1 if sex == "female" else 0
		gender.select(index)
		gender.emit_signal("item_selected", index)
		await wait_for_ui()
		assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
		for part in range(9):
			for value in range(variant_count(index == 1, part)):
				choose(scene, part, value)
				await process_frame
				assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data(), "Wrong variant composition: " + sex + SLOTS[part])
				selection_checks += 1
			choose(scene, part, 0)
		for value in range(3):
			for part in range(9):
				choose(scene, part, value % variant_count(index == 1, part))
			await wait_for_ui()
			assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
			assert(root.get_texture().get_image().save_png(ASSET_ROOT + "preview/" + sex + "_game_set_%d.png" % (value + 1)) == OK)
		for hair in [3,4]:
			for part in range(9):
				choose(scene, part, mini(hair, variant_count(index == 1, part) - 1) if part < 2 else (3 if index == 1 and part <= 6 else 0))
			await wait_for_ui()
			assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
			assert(root.get_texture().get_image().save_png(ASSET_ROOT + "preview/" + sex + "_game_ethereal_%d.png" % (hair + 1)) == OK)
		for part in range(9):
			choose(scene, part, part % 3)
		await wait_for_ui()
		var mixed: Array = selections(scene)
		for part in SLOTS:
			var check: CheckButton = scene.find_child("Part_" + part, true, false)
			check.button_pressed = false
			await process_frame
			assert(preview.texture.get_image().get_data() == expected(sex, mixed, part).get_data(), "Visibility failed: " + part)
			check.button_pressed = true
		anchor_check.button_pressed = true
		await wait_for_ui()
		assert(preview.texture.get_image().get_data() == expected(sex, mixed, "", true).get_data())
		anchor_check.button_pressed = false
		scene.find_child("RandomizeParts", true, false).emit_signal("pressed")
		await wait_for_ui()
		assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
		scene.find_child("ResetVariants", true, false).emit_signal("pressed")
		await wait_for_ui()
		assert(selections(scene) == [0,0,0,0,0,0,0,0,0])
		assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
		for part in range(9):
			choose(scene, part, (part + index) % 3)
		remembered[sex] = selections(scene)
		print("INK_RECOVERED_PARTS_SEX_PASS: ", sex, "; all available selections; new hair and facial parts; nine toggles; own anchors; random; reset")
	for sex in ["male", "female"]:
		var index := 1 if sex == "female" else 0
		gender.select(index)
		gender.emit_signal("item_selected", index)
		await wait_for_ui()
		assert(selections(scene) == remembered[sex], "Gender selections not preserved")
		assert(preview.texture.get_image().get_data() == expected(sex, remembered[sex]).get_data())
		assert(root.get_texture().get_image().save_png(ASSET_ROOT + "preview/" + sex + "_game_mixed.png") == OK)
	scene.find_child("ReferenceFemaleFace", true, false).emit_signal("pressed")
	await wait_for_ui()
	assert(gender.selected == 1)
	for part in range(2,7):
		assert(selections(scene)[part] == 1)
	assert(preview.texture.get_image().get_data() == expected("female", selections(scene)).get_data())
	var mod_catalog = root.get_node("AppearanceModCatalog")
	var portrait_catalog = root.get_node("PortraitCatalog")
	var runtime_count := 0
	for sex in ["male", "female"]:
		assert(not mod_catalog.allows_color_tint(sex, "skin"))
		assert(not mod_catalog.allows_color_tint(sex, "hair"))
		var runtime_slots := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "makeup" if sex == "female" else "beard"]
		for part in range(9):
			var entries = mod_catalog.get_parts(sex, runtime_slots[part])
			assert(entries.size() == variant_count(sex == "female", part))
			for item in entries:
				assert(item.package_id == "ink_recovered_parts_20261004")
				runtime_count += 1
		var data = load("res://csharp/resources/HumanData.cs").new()
		data.appearance_skin_tone = 4
		data.appearance_hair_color = 4
		var renderer = load("res://csharp/panel/PortraitRenderer.cs").new()
		root.add_child(renderer)
		var ctx = portrait_catalog.get_context(data, sex, 40, "", "")
		renderer.configure(data, ctx, {})
		assert(renderer.has_required_layers())
		for node_name in ["Face", "Hair Front", "Hair Back"]:
			assert(renderer.get_node(node_name).modulate == Color.WHITE)
		renderer.queue_free()
	assert(runtime_count == 87)
	var report := {"pass":true,"runtime_catalog_parts":runtime_count,"runtime_preserves_asset_colors":true,"selection_checks":selection_checks,"gender_selection_memory":true,"start_menu_entry":true,"mixed_visibility_checks":18,"anchors":true,"random_and_reset":true,"reference_female_face_button":true,"asset_revision":"recovered_nine_portraits_plus_three_brow_eye_pairs_each"}
	FileAccess.open(ASSET_ROOT + "game_verification.json", FileAccess.WRITE).store_string(JSON.stringify(report, "\t"))
	print("INK_RECOVERED_PARTS_ENTRY_PASS: ", selection_checks, " choices; start menu; independent gender memory; reference face preset")
	scene.queue_free()
	await wait_for_ui()
	quit(0)
