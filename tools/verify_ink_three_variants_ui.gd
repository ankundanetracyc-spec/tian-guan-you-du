extends SceneTree

const ASSET_ROOT := "res://mods/appearance/ink_three_variants_20261004/"
const RIG_ROOT := "res://mods/appearance/new_ink_hair_20261003_v1/rig/"
const SLOTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const ORDER := [1, 6, 7, 3, 2, 4, 5, 8, 0]

func component(female: bool, part: int) -> Dictionary:
	var config: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ASSET_ROOT + "appearance_parts.json"))
	for entry in config.components:
		if entry.gender == ("female" if female else "male") and ("/" + SLOTS[part] + "/") in entry.file:
			return entry
	assert(false, "Missing component")
	return {}

func file_index(sex: String, part: int, variant: int) -> int:
	var entry := component(sex == "female", part)
	return entry.variant_indices[variant] if entry.has("variant_indices") else variant + 1

func _initialize() -> void:
	call_deferred("verify")

func wait_for_ui() -> void:
	for frame in range(3):
		await process_frame
	if DisplayServer.get_name() != "headless": await RenderingServer.frame_post_draw

func capture_game_preview(relative_path: String) -> void:
	if DisplayServer.get_name() == "headless": return
	assert(root.get_texture().get_image().save_png(ASSET_ROOT + relative_path) == OK)

func expected(sex: String, variants: Array, hidden: String = "", anchors: bool = false) -> Image:
	var result := Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
	result.fill(Color(0, 0, 0, 0))
	for i in ORDER:
		var part: String = SLOTS[i]
		if part == hidden:
			continue
		var source := Image.new()
		assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(ASSET_ROOT + sex + "/" + part + "/%02d.png" % file_index(sex, i, variants[i]))) == OK)
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
	return component(female, part).variant_count

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
	var style_expansion_mixes := 0
	var practical_hair_mixes := 0
	var dynamic_hair_counts: Dictionary = {}
	for sex in ["male", "female"]:
		var index := 1 if sex == "female" else 0
		gender.select(index)
		gender.emit_signal("item_selected", index)
		await wait_for_ui()
		var summary: Label = scene.find_child("AppearanceStatus", true, false)
		var counts := [variant_count(index == 1, 0), variant_count(index == 1, 1)]
		assert(summary.text.contains("前发 %d 款／后发 %d 款" % counts))
		dynamic_hair_counts[sex] = counts
		assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
		for part in range(9):
			for value in range(variant_count(index == 1, part)):
				choose(scene, part, value)
				assert(scene.find_child("Variant_" + SLOTS[part], true, false).get_item_text(value) == component(index == 1, part).variant_names[value])
				await process_frame
				assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data(), "Wrong variant composition: " + sex + SLOTS[part])
				selection_checks += 1
			choose(scene, part, 0)
		for value in range(3):
			for part in range(9):
				choose(scene, part, value % variant_count(index == 1, part))
			await wait_for_ui()
			assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
			capture_game_preview("preview/" + sex + "_game_set_%d.png" % (value + 1))
		if sex == "male":
			for brow in range(3,6):
				for eye in range(2,5):
					for part in range(9): choose(scene, part, 0)
					choose(scene,2,brow); choose(scene,3,eye)
					await wait_for_ui()
					assert(preview.texture.get_image().get_data() == expected(sex,selections(scene)).get_data())
					if eye == brow - 1:
						var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ASSET_ROOT + "portrait_manifest.json"))
						var prefix := "male_sclera_game_" if manifest.has("sclera_revision") else "male_soft_game_"
						if manifest.has("natural_eye_revision"):
							prefix = "male_natural_eye_game_"
						capture_game_preview("preview/" + prefix + "%d.png" % (eye-1))
			if variant_count(false,2) >= 9 and variant_count(false,3) >= 8:
				# New/new pairs, plus cross-combinations with both original styles.
				var pairs: Array = []
				for brow in range(6,9):
					for eye in range(5,8): pairs.append([brow,eye])
					for eye in [0,1]: pairs.append([brow,eye])
				for brow in [0,1]:
					for eye in range(5,8): pairs.append([brow,eye])
				for pair in pairs:
					for part in range(9): choose(scene,part,0)
					choose(scene,2,pair[0]); choose(scene,3,pair[1])
					await wait_for_ui()
					assert(preview.texture.get_image().get_data() == expected(sex,selections(scene)).get_data(), "New style cross-composition mismatch")
					style_expansion_mixes += 1
					if pair[0] >= 6 and pair[1] == pair[0]-1:
						capture_game_preview("preview/male_style_expansion_game_%d.png" % (pair[0]-5))
		if variant_count(index == 1,0) >= 7 and variant_count(index == 1,1) >= 8:
			var hair_pairs: Array = []
			for front in range(4,7):
				for back in range(5,8): hair_pairs.append([front,back])
				for back in [0,1,2,3,4]: hair_pairs.append([front,back])
			for front in [0,1,2,3]:
				for back in range(5,8): hair_pairs.append([front,back])
			for pair in hair_pairs:
				for part in range(9): choose(scene,part,0)
				choose(scene,0,pair[0]); choose(scene,1,pair[1])
				await wait_for_ui()
				assert(preview.texture.get_image().get_data() == expected(sex,selections(scene)).get_data(), "New hair cross-composition mismatch")
				practical_hair_mixes += 1
				if pair[1] == pair[0] + 1:
					capture_game_preview("preview/" + sex + "_practical_hair_game_%02d.png" % file_index(sex,0,pair[0]))
					var hair_manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ASSET_ROOT + "portrait_manifest.json"))
					if hair_manifest.has("hair_front_refinement"):
						capture_game_preview("preview/" + sex + "_hair_front_refined_game_%02d.png" % file_index(sex,0,pair[0]))
					if hair_manifest.has("short_sideburn_revision") and file_index(sex,0,pair[0]) == 8:
						capture_game_preview("preview/" + sex + "_short_sideburn_game_08.png")
					elif hair_manifest.has("straight_hair_redesign"):
						capture_game_preview("preview/" + sex + "_straight_hair_game_%02d.png" % file_index(sex,0,pair[0]))
		for hair in [3,4]:
			if sex == "male" and hair == 3:
				for eye_ordinal in [2,6,7]:
					for part in range(9):
						choose(scene, part, 0)
					choose(scene, 3, eye_ordinal)
					await wait_for_ui()
					assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
					var capture_manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ASSET_ROOT + "portrait_manifest.json"))
					var capture_prefix := "male_eye_width_game_" if capture_manifest.has("eye_width_revision") else "male_eye_diversity_game_"
					capture_game_preview("preview/" + capture_prefix + "%02d.png" % file_index(sex, 3, eye_ordinal))
			for part in range(9):
				choose(scene, part, mini(hair, variant_count(index == 1, part) - 1) if part < 2 else (3 if index == 1 and part <= 6 else 0))
			await wait_for_ui()
			assert(preview.texture.get_image().get_data() == expected(sex, selections(scene)).get_data())
			capture_game_preview("preview/" + sex + "_game_ethereal_%d.png" % (hair + 1))
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
		print("INK_THREE_VARIANTS_SEX_PASS: ", sex, "; all available selections; new hair and facial parts; nine toggles; own anchors; random; reset")
	for sex in ["male", "female"]:
		var index := 1 if sex == "female" else 0
		gender.select(index)
		gender.emit_signal("item_selected", index)
		await wait_for_ui()
		assert(selections(scene) == remembered[sex], "Gender selections not preserved")
		assert(preview.texture.get_image().get_data() == expected(sex, remembered[sex]).get_data())
		capture_game_preview("preview/" + sex + "_game_mixed.png")
	scene.find_child("ReferenceFemaleFace", true, false).emit_signal("pressed")
	await wait_for_ui()
	assert(gender.selected == 1)
	for part in range(2,7):
		assert(selections(scene)[part] == 3)
	assert(preview.texture.get_image().get_data() == expected("female", selections(scene)).get_data())
	var current_manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ASSET_ROOT + "portrait_manifest.json"))
	var revision := "masculine_sclera_refinement_20261005" if current_manifest.has("sclera_revision") else "masculine_soft_eyes_brows_20261005"
	if current_manifest.has("natural_eye_revision"):
		revision = "natural_eye_scale_20261005"
	if current_manifest.has("style_expansion"):
		revision = "style_expansion_20261006"
	if current_manifest.has("eye_diversity_revision"):
		revision = "eye_diversity_revision_20261006"
	if current_manifest.has("eye_width_revision"):
		revision = "eye_width_revision_20261006"
	if current_manifest.has("practical_hair_expansion"):
		revision = "practical_hair_expansion_20261006"
	if current_manifest.has("female_practical_hair_expansion"):
		revision = "female_practical_hair_expansion_20261006"
	if current_manifest.has("hair_front_refinement"):
		revision = "hair_front_refinement_20261006"
	if current_manifest.has("straight_hair_redesign"):
		revision = "straight_hair_redesign_20261006"
	if current_manifest.has("short_sideburn_revision"):
		revision = "short_sideburn_revision_20261006"
	if current_manifest.has("yuxu_clothing_integration"):
		revision = "yuxu_clothing_integration_20261008"
	var report := {"pass":true,"selection_checks":selection_checks,"names":true,"new_brow_eye_mixes":9,"gender_selection_memory":true,"start_menu_entry":true,"mixed_visibility_checks":18,"anchors":true,"random_and_reset":true,"reference_female_face_button":true,"asset_revision":revision}
	report["style_expansion_mixes"] = style_expansion_mixes
	report["practical_hair_mixes"] = practical_hair_mixes
	report["dynamic_hair_count_summary"] = {"pass":true,"counts":dynamic_hair_counts}
	FileAccess.open(ASSET_ROOT + "game_verification.json", FileAccess.WRITE).store_string(JSON.stringify(report, "\t"))
	print("INK_THREE_VARIANTS_ENTRY_PASS: ", selection_checks, " choices; start menu; independent gender memory; reference face preset")
	scene.queue_free()
	await wait_for_ui()
	quit(0)
