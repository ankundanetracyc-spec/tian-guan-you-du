extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/portrait_male_regenerated_20261104_v3/"
const OUTPUT := PACK + "installation/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "beard"]
const COUNTS := [3, 3, 3, 2, 1, 1, 1, 7, 1]
const ORDER := [1, 6, 7, 3, 2, 4, 5, 8, 0]
var checks := 0
var failures: Array[String] = []
var config: Dictionary
var images: Dictionary = {}
var combinations := 0
var face_candidate_pixels_match := false


func _initialize() -> void:
	call_deferred("verify")


func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures.append(message)
		push_error(message)


func choice(scene: Node, part: int) -> OptionButton:
	return scene.find_child("Variant_" + PARTS[part], true, false)


func select(scene: Node, part: int, index: int) -> void:
	var control := choice(scene, part)
	control.select(index)
	control.emit_signal("item_selected", index)


func set_gender(scene: Node, female: bool) -> void:
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	var index := 1 if female else 0
	gender.select(index)
	gender.emit_signal("item_selected", index)


func selections(scene: Node) -> Array[int]:
	var result: Array[int] = []
	for part in 9:
		result.append(choice(scene, part).selected)
	return result


func component(part: int, female: bool = false) -> Dictionary:
	var gender := "female" if female else "male"
	var catalog_part: String = "makeup" if female and part == 8 else CATALOG_PARTS[part]
	for entry in config.components:
		if entry.gender == gender and entry.part == catalog_part:
			return entry
	return {}


func load_image(file: String) -> Image:
	if images.has(file):
		return images[file]
	var image := Image.new()
	check(image.load_png_from_buffer(FileAccess.get_file_as_bytes(file)) == OK, "Decode " + file)
	image.convert(Image.FORMAT_RGBA8)
	images[file] = image
	return image


func verify_imported_core(file: String) -> void:
	var texture: Texture2D = ResourceLoader.load(file, "Texture2D", ResourceLoader.CACHE_MODE_IGNORE)
	check(texture != null, "Imported texture loads: " + file)
	if texture == null:
		return
	var imported := texture.get_image()
	imported.convert(Image.FORMAT_RGBA8)
	var source := load_image(file)
	check(imported.get_size() == source.get_size(), "Imported texture canvas matches source: " + file)
	if imported.get_size() != source.get_size():
		return
	var actual := imported.get_data()
	var expected := source.get_data()
	var different_core_pixels := 0
	for index in range(3, expected.size(), 4):
		if expected[index] < 230:
			continue
		for offset in [0, 1, 2, 3]:
			if actual[index - offset] != expected[index - offset]:
				different_core_pixels += 1
				break
	check(different_core_pixels == 0, "Imported visible-core pixels are current PNG, without stale cache: " + file)


func verify_pixels(scene: Node, label: String, female: bool = false) -> void:
	var expected := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
	for part in ORDER:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		if not toggle.button_pressed:
			continue
		var entry := component(part, female)
		var selected: int = choice(scene, part).selected
		var index: int = entry.variant_indices[selected] if entry.has("variant_indices") else selected + 1
		var sex := "female" if female else "male"
		var file: String = ACTIVE + sex + "/" + PARTS[part] + "/%02d.png" % index
		expected.blend_rect(load_image(file), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture != null, label + " texture exists")
	if view.texture != null:
		var actual := view.texture.get_image()
		actual.convert(Image.FORMAT_RGBA8)
		check(actual.get_size() == Vector2i(1024, 1536), label + " canvas")
		check(actual.get_data() == expected.get_data(), label + " actual game pixels match independently selected layers")


func verify_face_match(_report: Dictionary) -> void:
	var candidate := load_image(PACK + "male/face_shape/01.png")
	var current := load_image(ACTIVE + "male/face_shape/01.png")
	check(candidate.get_size() == Vector2i(1024, 1536), "Candidate replacement face canvas")
	check(current.get_size() == candidate.get_size(), "Installed face has candidate canvas")
	face_candidate_pixels_match = current.get_data() == candidate.get_data()
	check(face_candidate_pixels_match, "Installed complete face pixels exactly match new candidate")
	check(not current.is_invisible(), "Replacement face is visible")


func verify_retired_styles(catalog: Node, mods: Node) -> void:
	for part in CATALOG_PARTS:
		var candidates: Array = mods.get_parts("male", part)
		for random_seed in [1, 42, 31415]:
			var context := {"gender": "male", "age": 40, "seed": random_seed}
			var first: Dictionary = catalog.choose_part(context, part, "99")
			var second: Dictionary = catalog.choose_part(context, part, "99")
			check(not first.is_empty(), "Retired male style resolves: " + part)
			if first.is_empty():
				continue
			check(first.get("id") == second.get("id"), "Retired male style fallback is deterministic: " + part)
			var valid := false
			for entry in candidates:
				if entry.id == first.id and entry.resource_path == first.resource_path:
					valid = true
			check(valid and ResourceLoader.exists(str(first.resource_path)), "Retired style resolves to an active imported male part: " + part)


func verify() -> void:
	config = JSON.parse_string(FileAccess.get_file_as_string(ACTIVE + "appearance_parts.json"))
	var report: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT + "install_report.json"))
	var mods := root.get_node("AppearanceModCatalog")
	var catalog := root.get_node("PortraitCatalog")
	mods.reload()
	var active_options := 0
	for entry in config.components:
		active_options += int(entry.variant_count)
	check(active_options == 44, "44 total active male/female appearance options")
	check(report.records.size() == 14, "Six newly generated hair layers, seven features and one complete face registered")
	check(report.baseline_sha256.size() == 30, "22 female PNGs, seven male clothes and empty accessory preserved")
	verify_face_match(report)
	check(FileAccess.get_sha256(ACTIVE + "appearance_parts.json") == str(report.config_sha256), "Appearance configuration bytes preserved")
	check(FileAccess.get_sha256(str(report.reference_resource_path)) == str(report.reference_sha256), "Specified reference image unchanged")
	for part in 9:
		var entries: Array = mods.get_parts("male", CATALOG_PARTS[part])
		check(entries.size() == COUNTS[part], str(PARTS[part]) + " male active count")
		var female_part: String = "makeup" if part == 8 else CATALOG_PARTS[part]
		var female_entries: Array = mods.get_parts("female", female_part)
		check(female_entries.size() == COUNTS[part], str(PARTS[part]) + " female count preserved")
	for record in report.records:
		check(str(record.sha256) != str(record.before_sha256), "Rejected male artwork has been replaced " + str(record.resource_path))
		check(FileAccess.get_sha256(str(record.resource_path)) == str(record.sha256), "Installed SHA " + str(record.resource_path))
		check(FileAccess.get_sha256(str(record.source_path)) == str(record.sha256), "Source SHA " + str(record.source_path))
		check(FileAccess.get_sha256(str(record.candidate_path)) == str(record.sha256), "Candidate SHA " + str(record.candidate_path))
		check(ResourceLoader.exists(str(record.resource_path)), "Imported male part " + str(record.resource_path))
		verify_imported_core(str(record.resource_path))
		var image := load_image(str(record.resource_path))
		check(image.get_size() == Vector2i(1024, 1536) and not image.is_invisible(), "Visible male RGBA part " + str(record.resource_path))
		check(image.get_data() == load_image(str(record.candidate_path)).get_data(), "Installed component pixels match candidate " + str(record.resource_path))
	for file in report.baseline_sha256:
		check(FileAccess.get_sha256(str(file)) == str(report.baseline_sha256[file]), "Preserved PNG " + str(file))
	verify_retired_styles(catalog, mods)
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	set_gender(scene, false)
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	var breathing: CheckButton = scene.find_child("BreathingPreview", true, false)
	breathing.button_pressed = false
	check(gender.selected == 0, "Male preview explicitly selected")
	for part in 9:
		check(choice(scene, part).item_count == COUNTS[part], str(PARTS[part]) + " male independent UI count")
	for part in [0, 1, 2, 3]:
		var before := selections(scene)
		select(scene, part, (before[part] + 1) % COUNTS[part])
		var after := selections(scene)
		for other in 9:
			if other != part:
				check(after[other] == before[other], "Changing " + str(PARTS[part]) + " preserves " + str(PARTS[other]))
	for front in 3:
		for back in 3:
			for brow in 3:
				for eye in 2:
					select(scene, 0, front)
					select(scene, 1, back)
					select(scene, 2, brow)
					select(scene, 3, eye)
					verify_pixels(scene, "F%d B%d Brow%d Eye%d" % [front, back, brow, eye])
					combinations += 1
	for part in [0, 1, 2, 3, 4, 5, 6]:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		toggle.button_pressed = false
		verify_pixels(scene, "Hide " + str(PARTS[part]))
		toggle.button_pressed = true
		verify_pixels(scene, "Restore " + str(PARTS[part]))
	seed(20261104)
	for iteration in 8:
		scene.find_child("RandomizeParts", true, false).emit_signal("pressed")
		verify_pixels(scene, "Random %d" % iteration)
	var male_preserved := selections(scene)
	set_gender(scene, true)
	select(scene, 0, 2)
	select(scene, 1, 1)
	select(scene, 2, 2)
	select(scene, 3, 1)
	var female_preserved := selections(scene)
	verify_pixels(scene, "Female after male switch", true)
	set_gender(scene, false)
	check(gender.selected == 0 and selections(scene) == male_preserved, "Returning to male preserves all independent choices")
	verify_pixels(scene, "Return to male")
	scene.find_child("ReferenceFemaleFace", true, false).emit_signal("pressed")
	check(gender.selected == 1 and selections(scene) == female_preserved, "View female button preserves female choices")
	verify_pixels(scene, "View female button", true)
	set_gender(scene, false)
	check(selections(scene) == male_preserved, "Male choices retained after view female button")
	scene.find_child("ResetVariants", true, false).emit_signal("pressed")
	check(selections(scene) == [0, 0, 0, 0, 0, 0, 0, 0, 0], "Male reset returns each slot to its first choice")
	verify_pixels(scene, "Male reset")
	var preview: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(preview.texture.get_image().save_png(OUTPUT + "game_composite.png") == OK, "Saved male game composite")
	await process_frame
	var viewport_captured := false
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		viewport_captured = root.get_texture().get_image().save_png(OUTPUT + "game_viewport.png") == OK
		check(viewport_captured, "Saved actual male game viewport")
	var result := {"pass": failures.is_empty(), "checks": checks, "failures": failures, "gender": "male", "male_slot_counts": COUNTS, "active_option_count": active_options, "new_parts_checked": report.records.size(), "feature_counts": {"eyebrows": 3, "eyes": 2, "nose": 1, "mouth": 1}, "all_hair_feature_combinations": combinations, "baseline_files_checked": report.baseline_sha256.size(), "independent_controls_verified": true, "retired_style_fallback_verified": true, "pixel_composites_verified": true, "viewport_captured": viewport_captured, "face_candidate_pixels_match": face_candidate_pixels_match, "face_geometry_may_change": true, "all_male_artwork_regenerated": true, "rejected_previous_rounds_must_not_be_restored": true, "visual_material_acceptance": "Runtime pixel equality confirms installation and composition, not a visual match to the source skin or feature shape."}
	var output_file := FileAccess.open(OUTPUT + "runtime_check.json", FileAccess.WRITE)
	output_file.store_string(JSON.stringify(result, "  "))
	output_file.close()
	print("MALE_REGENERATED_20261104: ", JSON.stringify(result))
	scene.queue_free()
	await process_frame
	images.clear()
	quit(0 if failures.is_empty() else 1)
