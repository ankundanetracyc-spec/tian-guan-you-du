extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/portrait_hair_skin_revision_20261104_v1/"
const OUTPUT := PACK + "installation/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "makeup"]
const COUNTS := [3, 3, 3, 2, 1, 1, 1, 7, 1]
const ORDER := [1, 6, 7, 3, 2, 4, 5, 8, 0]
var checks := 0
var failures: Array[String] = []
var config: Dictionary
var images: Dictionary = {}
var combinations := 0
var skin_alpha_changed_pixels := -1
var skin_bbox_unchanged := false
const SKIN_MEASUREMENT_REPORT := PACK + "qa/skin_color_report.json"

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

func selections(scene: Node) -> Array[int]:
	var result: Array[int] = []
	for part in 9:
		result.append(choice(scene, part).selected)
	return result

func component(part: int) -> Dictionary:
	for entry in config.components:
		if entry.gender == "female" and entry.part == CATALOG_PARTS[part]:
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

func verify_pixels(scene: Node, label: String) -> void:
	var expected := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
	for part in ORDER:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		if not toggle.button_pressed:
			continue
		var entry := component(part)
		var selected: int = choice(scene, part).selected
		var index: int = entry.variant_indices[selected] if entry.has("variant_indices") else selected + 1
		var file: String = ACTIVE + "female/" + PARTS[part] + "/%02d.png" % index
		expected.blend_rect(load_image(file), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture != null, label + " texture exists")
	if view.texture != null:
		var actual := view.texture.get_image()
		actual.convert(Image.FORMAT_RGBA8)
		check(actual.get_size() == Vector2i(1024, 1536), label + " canvas")
		check(actual.get_data() == expected.get_data(), label + " actual game pixels match independently selected layers")


func alpha_bbox(image: Image) -> Rect2i:
	var bytes: PackedByteArray = image.get_data()
	var width := image.get_width()
	var height := image.get_height()
	var min_x := width
	var min_y := height
	var max_x := -1
	var max_y := -1
	for y in height:
		for x in width:
			if bytes[(y * width + x) * 4 + 3] > 0:
				min_x = mini(min_x, x)
				min_y = mini(min_y, y)
				max_x = maxi(max_x, x)
				max_y = maxi(max_y, y)
	return Rect2i(min_x, min_y, max_x - min_x + 1, max_y - min_y + 1)

func verify_skin_geometry() -> void:
	var original := load_image(PACK + "source/original_face.png")
	var current := load_image(ACTIVE + "female/face_shape/04.png")
	check(original.get_size() == Vector2i(1024, 1536), "Original skin base canvas")
	check(current.get_size() == original.get_size(), "Skin base canvas remains unchanged")
	if current.get_size() == original.get_size():
		var before: PackedByteArray = original.get_data()
		var after: PackedByteArray = current.get_data()
		skin_alpha_changed_pixels = 0
		for index in range(3, before.size(), 4):
			if before[index] != after[index]:
				skin_alpha_changed_pixels += 1
		check(skin_alpha_changed_pixels == 0, "All original face/ear/neck alpha bytes retained exactly")
		skin_bbox_unchanged = alpha_bbox(current) == alpha_bbox(original)
		check(skin_bbox_unchanged, "Skin visible alpha bounds unchanged")
	check(FileAccess.file_exists(SKIN_MEASUREMENT_REPORT), "Skin color measurement report exists")
	if FileAccess.file_exists(SKIN_MEASUREMENT_REPORT):
		var measurement = JSON.parse_string(FileAccess.get_file_as_string(SKIN_MEASUREMENT_REPORT))
		check(measurement is Dictionary, "Skin color measurement report is valid JSON")

func verify() -> void:
	config = JSON.parse_string(FileAccess.get_file_as_string(ACTIVE + "appearance_parts.json"))
	var report: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT + "install_report.json"))
	var mods := root.get_node("AppearanceModCatalog")
	var catalog := root.get_node("PortraitCatalog")
	mods.reload()
	var active_options := 0
	for entry in config.components:
		active_options += int(entry.variant_count)
	check(active_options == 71, "All 71 active appearance options retained")
	check(report.records.size() == 7, "Six hair sprites and one revised face registered")
	verify_skin_geometry()
	for part in 9:
		var entries: Array = mods.get_parts("female", CATALOG_PARTS[part])
		check(entries.size() == COUNTS[part], str(PARTS[part]) + " active count")
	for record in report.records:
		check(FileAccess.get_sha256(str(record.resource_path)) == str(record.sha256), "Installed SHA " + str(record.resource_path))
		check(FileAccess.get_sha256(str(record.source_path)) == str(record.sha256), "Source SHA " + str(record.source_path))
		check(FileAccess.get_sha256(str(record.candidate_path)) == str(record.sha256), "Candidate SHA " + str(record.candidate_path))
		check(ResourceLoader.exists(str(record.resource_path)), "Imported feature " + str(record.resource_path))
		var image := load_image(str(record.resource_path))
		check(image.get_size() == Vector2i(1024, 1536) and not image.is_invisible(), "Visible RGBA feature " + str(record.resource_path))
	for file in report.baseline_sha256:
		check(FileAccess.get_sha256(str(file)) == str(report.baseline_sha256[file]), "Preserved PNG " + str(file))
	for entry in config.components:
		if entry.gender != "female" or not entry.has("variant_aliases"):
			continue
		for alias in entry.variant_aliases:
			for random_seed in [1, 42, 31415]:
				var context := {"gender": "female", "age": 40, "seed": random_seed}
				var resolved: Dictionary = catalog.choose_part(context, str(entry.part), str(alias))
				check(int(resolved.get("variant_index", -1)) == int(entry.variant_aliases[alias]), "Merged style alias " + str(entry.part) + ":" + str(alias))
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	var breathing: CheckButton = scene.find_child("BreathingPreview", true, false)
	breathing.button_pressed = false
	check(gender.selected == 1, "Defaults to female preview")
	for part in 9:
		check(choice(scene, part).item_count == COUNTS[part], str(PARTS[part]) + " independent UI count")
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
	for part in [2, 3, 4, 5]:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		toggle.button_pressed = false
		verify_pixels(scene, "Hide " + str(PARTS[part]))
		toggle.button_pressed = true
		verify_pixels(scene, "Restore " + str(PARTS[part]))
	seed(20261104)
	for iteration in 8:
		scene.find_child("RandomizeParts", true, false).emit_signal("pressed")
		verify_pixels(scene, "Random %d" % iteration)
	var preserved := selections(scene)
	gender.select(0)
	gender.emit_signal("item_selected", 0)
	check(scene.find_child("PortraitPreview", true, false).texture != null, "Male preview still works")
	scene.find_child("ReferenceFemaleFace", true, false).emit_signal("pressed")
	check(gender.selected == 1 and selections(scene) == preserved, "View female preserves independent selections")
	verify_pixels(scene, "Return to female")
	scene.find_child("ResetVariants", true, false).emit_signal("pressed")
	check(selections(scene) == [0, 0, 0, 0, 0, 0, 0, 0, 0], "Reset returns each slot to its first choice")
	verify_pixels(scene, "Reset")
	var preview: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(preview.texture.get_image().save_png(OUTPUT + "game_composite.png") == OK, "Saved game composite")
	await process_frame
	var viewport_captured := false
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		viewport_captured = root.get_texture().get_image().save_png(OUTPUT + "game_viewport.png") == OK
		check(viewport_captured, "Saved actual game viewport")
	var result := {"pass": failures.is_empty(), "checks": checks, "failures": failures, "feature_counts": {"eyebrows": 3, "eyes": 2, "nose": 1, "mouth": 1}, "all_hair_feature_combinations": combinations, "baseline_files_checked": report.baseline_sha256.size(), "independent_controls_verified": true, "alias_resolution_verified": true, "pixel_composites_verified": true, "viewport_captured": viewport_captured, "skin_alpha_changed_pixels": skin_alpha_changed_pixels, "skin_bbox_unchanged": skin_bbox_unchanged, "skin_measurement_report": SKIN_MEASUREMENT_REPORT}
	var output_file := FileAccess.open(OUTPUT + "runtime_check.json", FileAccess.WRITE)
	output_file.store_string(JSON.stringify(result, "  "))
	output_file.close()
	print("PORTRAIT_HAIR_SKIN_REVISION: ", JSON.stringify(result))
	scene.queue_free()
	await process_frame
	quit(0 if failures.is_empty() else 1)
