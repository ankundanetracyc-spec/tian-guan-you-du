extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const OUTPUT := "res://mods/appearance/female_features_install_20261106/installation/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "makeup"]
const ORDER := [1, 6, 7, 4, 3, 2, 5, 8, 0]
var checks: int = 0
var failures: Array[String] = []
var config: Dictionary = {}
var report: Dictionary = {}
var images: Dictionary = {}
var combinations: int = 0

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
	var control: OptionButton = choice(scene, part)
	control.select(index)
	control.emit_signal("item_selected", index)

func selections(scene: Node) -> Array[int]:
	var result: Array[int] = []
	for part in 9:
		result.append(choice(scene, part).selected)
	return result

func catalog_part(part: int, gender: String = "female") -> String:
	return "beard" if gender == "male" and part == 8 else CATALOG_PARTS[part]

func component(part: int, gender: String = "female") -> Dictionary:
	for entry: Dictionary in config.components:
		if str(entry.gender) == gender and str(entry.part) == catalog_part(part, gender):
			return entry
	return {}

func file_for(entry: Dictionary, ui_index: int, field: String = "file") -> String:
	var index: int = int(entry.variant_indices[ui_index]) if entry.has("variant_indices") else ui_index + int(config.get("index_start", 0))
	var padding: int = int(entry.get("index_padding", config.get("index_padding", 0)))
	return ACTIVE + str(entry.get(field, "")).replace("{index}", str(index).pad_zeros(padding))

func load_image(file: String) -> Image:
	if images.has(file):
		return images[file]
	var image := Image.new()
	check(image.load_png_from_buffer(FileAccess.get_file_as_bytes(file)) == OK, "Decode " + file)
	image.convert(Image.FORMAT_RGBA8)
	images[file] = image
	return image

func blend_file(expected: Image, file: String) -> void:
	expected.blend_rect(load_image(file), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)

func verify_pixels(scene: Node, label: String, gender: String = "female") -> void:
	var expected := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
	for part: int in ORDER:
		if part == 6:
			var clothes_toggle: CheckButton = scene.find_child("Part_clothing", true, false)
			var clothes: Dictionary = component(7, gender)
			if clothes_toggle.button_pressed and str(clothes.get("rear_file", "")) != "":
				blend_file(expected, file_for(clothes, choice(scene, 7).selected, "rear_file"))
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		if toggle.button_pressed:
			blend_file(expected, file_for(component(part, gender), choice(scene, part).selected))
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture != null, label + " texture exists")
	if view.texture != null:
		var actual: Image = view.texture.get_image()
		actual.convert(Image.FORMAT_RGBA8)
		check(actual.get_size() == Vector2i(1024, 1536), label + " canvas")
		check(actual.get_data() == expected.get_data(), label + " exact layer composition")

func add_pair(pairs: Array[Vector2i], brow: int, eye: int) -> void:
	var pair := Vector2i(brow, eye)
	if not pairs.has(pair):
		pairs.append(pair)

func representative_pairs() -> Array[Vector2i]:
	var pairs: Array[Vector2i] = []
	var brow_count: int = int(component(2).variant_count)
	var eye_count: int = int(component(3).variant_count)
	if brow_count * eye_count <= 24:
		for brow in brow_count:
			for eye in eye_count:
				add_pair(pairs, brow, eye)
	else:
		add_pair(pairs, 0, 0)
		for brow in brow_count:
			add_pair(pairs, brow, brow % eye_count)
		for eye in eye_count:
			add_pair(pairs, eye % brow_count, eye)
		for record: Dictionary in report.records:
			if str(record.part) == "eyebrows":
				for eye in eye_count:
					add_pair(pairs, int(record.ui_index), eye)
			elif str(record.part) == "eyes":
				for brow in brow_count:
					add_pair(pairs, brow, int(record.ui_index))
	return pairs

func verify_config_preserved() -> void:
	var current: Array = config.components
	var before: Array = report.baseline_components
	check(current.size() == before.size(), "No new slots or component families")
	for old: Dictionary in before:
		var found: Dictionary = {}
		for entry: Dictionary in current:
			if str(entry.id) == str(old.id):
				found = entry
		check(not found.is_empty(), "Existing component retained " + str(old.id))
		if found.is_empty():
			continue
		var modified: bool = false
		for record: Dictionary in report.records:
			if str(old.gender) == "female" and str(record.catalog_part) == str(old.part):
				modified = true
		if not modified:
			check(found == old, "Unmodified component exact " + str(old.id))
			continue
		var n: int = int(old.variant_count)
		for field: String in ["variant_names", "variant_indices", "variant_style_ids"]:
			if old.has(field):
				check(found[field].slice(0, n) == old[field], "Existing prefix preserved " + str(old.id) + " " + field)
		for key in old:
			if not str(key) in ["variant_count", "variant_names", "variant_indices", "variant_style_ids"]:
				check(found.get(key) == old[key], "Existing metadata/aliases retained " + str(old.id) + " " + str(key))

func verify() -> void:
	if not FileAccess.file_exists(OUTPUT + "install_report.json"):
		push_error("Install report missing; run reviewed selection installer first")
		quit(2)
		return
	config = JSON.parse_string(FileAccess.get_file_as_string(ACTIVE + "appearance_parts.json"))
	report = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT + "install_report.json"))
	var mods: Node = root.get_node("AppearanceModCatalog")
	var catalog: Node = root.get_node("PortraitCatalog")
	mods.call("reload")
	verify_config_preserved()
	for gender: String in ["male", "female"]:
		for part in 9:
			var entries: Array = mods.call("get_parts", gender, catalog_part(part, gender))
			check(entries.size() == int(report.slot_counts[gender][part]), gender + " " + PARTS[part] + " catalog count")
	for record: Dictionary in report.records:
		for key: String in ["source_path", "candidate_path", "resource_path"]:
			check(FileAccess.get_sha256(str(record[key])) == str(record.sha256), "New file SHA " + str(record[key]))
		check(ResourceLoader.exists(str(record.resource_path)), "Imported feature " + str(record.resource_path))
		var image: Image = load_image(str(record.resource_path))
		check(image.get_size() == Vector2i(1024, 1536) and not image.is_invisible(), "Full-canvas visible new feature")
		var context: Dictionary = {"gender": "female", "age": 40, "seed": 42}
		var resolved: Dictionary = catalog.call("choose_part", context, str(record.catalog_part), str(int(record.style_id)))
		check(str(resolved.get("resource_path", "")) == str(record.resource_path), "New saved style resolves to new physical file")
	for file in report.baseline_sha256:
		check(FileAccess.get_sha256(str(file)) == str(report.baseline_sha256[file]), "Existing PNG unchanged " + str(file))
	for old: Dictionary in report.baseline_components:
		if str(old.gender) != "female":
			continue
		for alias in old.get("variant_aliases", {}):
			for random_seed in [1, 42, 31415]:
				var context: Dictionary = {"gender": "female", "age": 40, "seed": random_seed}
				var resolved: Dictionary = catalog.call("choose_part", context, str(old.part), str(alias))
				check(int(resolved.get("variant_index", -1)) == int(old.variant_aliases[alias]), "Retired alias preserved " + str(old.part) + ":" + str(alias))
		for style in old.get("variant_style_ids", []):
			var context: Dictionary = {"gender": "female", "age": 40, "seed": 42}
			var resolved: Dictionary = catalog.call("choose_part", context, str(old.part), str(int(style)))
			check(int(resolved.get("variant_index", -1)) == int(style), "Old saved style retained " + str(old.part) + ":" + str(style))
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	var gender_choice: OptionButton = scene.find_child("GenderChoice", true, false)
	var breathing: CheckButton = scene.find_child("BreathingPreview", true, false)
	breathing.button_pressed = false
	check(gender_choice.selected == 1, "Default female preview")
	for part in 9:
		check(choice(scene, part).item_count == int(report.slot_counts.female[part]), "Independent UI count " + PARTS[part])
	for record: Dictionary in report.records:
		var part: int = PARTS.find(str(record.part))
		var before: Array[int] = selections(scene)
		select(scene, part, int(record.ui_index))
		var after: Array[int] = selections(scene)
		check(after[part] == int(record.ui_index), "Select new option " + str(record.name))
		for other in 9:
			if other != part:
				check(after[other] == before[other], "New option preserves other slot " + PARTS[other])
		verify_pixels(scene, "New option " + str(record.name))
	var pairs: Array[Vector2i] = representative_pairs()
	for front in int(component(0).variant_count):
		for back in int(component(1).variant_count):
			for pair: Vector2i in pairs:
				select(scene, 0, front)
				select(scene, 1, back)
				select(scene, 2, pair.x)
				select(scene, 3, pair.y)
				verify_pixels(scene, "F%d B%d Brow%d Eye%d" % [front, back, pair.x, pair.y])
				combinations += 1
	for part in [2, 3, 4, 5]:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		toggle.button_pressed = false
		verify_pixels(scene, "Hide " + PARTS[part])
		toggle.button_pressed = true
		verify_pixels(scene, "Restore " + PARTS[part])
	seed(20261106)
	for iteration in 4:
		scene.find_child("RandomizeParts", true, false).emit_signal("pressed")
		verify_pixels(scene, "Random %d" % iteration)
	var preserved_female: Array[int] = selections(scene)
	gender_choice.select(0)
	gender_choice.emit_signal("item_selected", 0)
	var preserved_male: Array[int] = selections(scene)
	verify_pixels(scene, "Male preview", "male")
	for part in 9:
		check(choice(scene, part).item_count == int(report.slot_counts.male[part]), "Male UI preserved " + PARTS[part])
	gender_choice.select(1)
	gender_choice.emit_signal("item_selected", 1)
	check(selections(scene) == preserved_female, "Female selection memory preserved")
	gender_choice.select(0)
	gender_choice.emit_signal("item_selected", 0)
	check(selections(scene) == preserved_male, "Male selection memory preserved")
	scene.find_child("ReferenceFemaleFace", true, false).emit_signal("pressed")
	check(gender_choice.selected == 1 and selections(scene) == preserved_female, "View female keeps independent selections")
	scene.find_child("ResetVariants", true, false).emit_signal("pressed")
	check(selections(scene) == [0, 0, 0, 0, 0, 0, 0, 0, 0], "Reset uses existing first choices")
	verify_pixels(scene, "Reset original baseline")
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture.get_image().save_png(OUTPUT + "game_baseline_composite.png") == OK, "Save baseline actual composite")
	for record: Dictionary in report.records:
		select(scene, PARTS.find(str(record.part)), int(record.ui_index))
	verify_pixels(scene, "New selected features together")
	check(view.texture.get_image().save_png(OUTPUT + "game_new_features_composite.png") == OK, "Save new features actual composite")
	await process_frame
	var viewport_captured: bool = false
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		viewport_captured = root.get_texture().get_image().save_png(OUTPUT + "game_viewport.png") == OK
		check(viewport_captured, "Save actual game viewport")
	var result: Dictionary = {"pass": failures.is_empty(), "checks": checks, "failures": failures,
		"feature_counts": report.counts, "slot_counts": report.slot_counts,
		"hair_feature_combinations": combinations, "brow_eye_representative_pairs": pairs.size(),
		"baseline_pngs_preserved": report.baseline_sha256.size(), "new_features": report.records.size(),
		"aliases_preserved": true, "independent_controls_verified": true,
		"pixel_composites_verified": true, "selection_memory_verified": true,
		"viewport_captured": viewport_captured}
	var output_file: FileAccess = FileAccess.open(OUTPUT + "runtime_check.json", FileAccess.WRITE)
	output_file.store_string(JSON.stringify(result, "  "))
	output_file.close()
	print("FEMALE_FEATURE_EXPANSION: ", JSON.stringify(result))
	scene.queue_free()
	await process_frame
	quit(0 if failures.is_empty() else 1)
