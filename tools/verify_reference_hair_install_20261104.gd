extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const CANDIDATE := "res://mods/appearance/portrait_reference_hair_20261104_v1/"
const OUTPUT := CANDIDATE + "installation/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "makeup"]
const ORDER := [1, 6, 7, 3, 2, 4, 5, 8, 0]
var checks := 0
var failures: Array[String] = []
var image_cache: Dictionary = {}
var baseline_checks := 0
var viewport_captured := false
var config: Dictionary

func _initialize() -> void:
	call_deferred("verify")

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures.append(message)
		push_error(message)

func load_image(path: String) -> Image:
	if image_cache.has(path):
		return image_cache[path]
	var result := Image.new()
	var error := result.load_png_from_buffer(FileAccess.get_file_as_bytes(path))
	check(error == OK, "PNG decode: " + path)
	if error != OK:
		return null
	result.convert(Image.FORMAT_RGBA8)
	image_cache[path] = result
	return result

func choose(choice: OptionButton, index: int) -> void:
	choice.select(index)
	choice.emit_signal("item_selected", index)

func female_component(part: int) -> Dictionary:
	for entry in config.components:
		if entry.gender == "female" and ("/" + PARTS[part] + "/") in str(entry.file):
			return entry
	return {}

func expected_image(scene: Node) -> Image:
	var expected := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
	for part in ORDER:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		if not toggle.button_pressed:
			continue
		var choice: OptionButton = scene.find_child("Variant_" + PARTS[part], true, false)
		var component := female_component(part)
		var index: int = component.variant_indices[choice.selected] if component.has("variant_indices") else choice.selected + 1
		var file: String = ACTIVE + "female/" + PARTS[part] + "/%02d.png" % index
		var layer := load_image(file)
		if layer != null:
			expected.blend_rect(layer, Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	return expected

func check_composite(scene: Node, context: String) -> void:
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture != null, context + ": texture exists")
	if view.texture == null:
		return
	var actual := view.texture.get_image()
	actual.convert(Image.FORMAT_RGBA8)
	check(actual.get_size() == Vector2i(1024, 1536), context + ": canvas")
	check(actual.get_data() == expected_image(scene).get_data(), context + ": actual game pixels match selected layers")

func verify_baseline() -> void:
	var report_path := OUTPUT + "install_report.json"
	check(FileAccess.file_exists(report_path), "Installation report exists")
	if not FileAccess.file_exists(report_path):
		return
	var install_report = JSON.parse_string(FileAccess.get_file_as_string(report_path))
	check(install_report is Dictionary and install_report.has("baseline_sha256"), "Installation report contains baseline_sha256")
	if not install_report is Dictionary or not install_report.has("baseline_sha256"):
		return
	var baseline: Dictionary = install_report.baseline_sha256
	var males := 0
	var female_clothes := 0
	for key in baseline:
		var file := str(key)
		if not file.begins_with("res://"):
			file = "res://" + file.trim_prefix("./")
		if not (file.begins_with(ACTIVE + "male/") or file.begins_with(ACTIVE + "female/clothing/")):
			continue
		check(FileAccess.get_sha256(file) == str(baseline[key]), "Unchanged baseline: " + file)
		baseline_checks += 1
		if file.begins_with(ACTIVE + "male/"):
			males += 1
		else:
			female_clothes += 1
	check(males > 0 and female_clothes == 7, "Male and all seven female clothing baselines checked")

func verify() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	config = JSON.parse_string(FileAccess.get_file_as_string(ACTIVE + "appearance_parts.json"))
	var mods := root.get_node("AppearanceModCatalog")
	var catalog := root.get_node("PortraitCatalog")
	mods.reload()
	for part in [0, 1]:
		var entries: Array = mods.get_parts("female", CATALOG_PARTS[part])
		check(entries.size() == 3, CATALOG_PARTS[part] + ": exactly three active choices")
		for index in 3:
			var file: String = "female/" + PARTS[part] + "/%02d.png" % (index + 1)
			check(FileAccess.get_sha256(ACTIVE + file) == FileAccess.get_sha256(CANDIDATE + file), file + ": candidate SHA256 matches active PNG")
			check(ResourceLoader.exists(ACTIVE + file), file + ": imported resource exists")
			var image := load_image(ACTIVE + file)
			check(image != null and image.get_size() == Vector2i(1024, 1536), file + ": canvas")
			check(image != null and not image.is_invisible(), file + ": nonempty hair")
		for entry in entries:
			check(entry.variant_index in [0, 1, 2], PARTS[part] + ": saved style ID retained")
			check(str(entry.resource_path).begins_with(ACTIVE), PARTS[part] + ": no duplicate candidate package")
	for part in [2, 3, 4, 5, 8]:
		var entries: Array = mods.get_parts("female", CATALOG_PARTS[part])
		check(entries.size() == 1, PARTS[part] + ": exactly one blank choice")
		if entries.size() == 1:
			check(entries[0].variant_index == 0, PARTS[part] + ": blank saved style ID zero")
			check(str(entries[0].resource_path) == ACTIVE + "female/" + PARTS[part] + "/01.png", PARTS[part] + ": blank file01")
			var image := load_image(entries[0].resource_path)
			check(image != null and image.get_size() == Vector2i(1024, 1536), PARTS[part] + ": blank canvas")
			check(image != null and image.is_invisible(), PARTS[part] + ": every alpha pixel is zero")
		var png_files: Array[String] = []
		for file in DirAccess.get_files_at(ACTIVE + "female/" + PARTS[part]):
			if file.ends_with(".png"):
				png_files.append(file)
		check(png_files == ["01.png"], PARTS[part] + ": retired PNGs absent from active directory")
		for requested in ["0", "1", "2", "3", "99", "female_eyes_03"]:
			var context := {"gender": "female", "seed": 31415, "age": 40}
			var selected: Dictionary = catalog.choose_part(context, CATALOG_PARTS[part], requested)
			check(entries.has(selected), PARTS[part] + ": retired selection resolves to blank")
	var faces: Array = mods.get_parts("female", "face")
	check(faces.size() == 1 and str(faces[0].resource_path) == ACTIVE + "female/face_shape/04.png", "Fixed face04 remains the sole female face")
	check(FileAccess.get_sha256(ACTIVE + "female/face_shape/04.png") == FileAccess.get_sha256(CANDIDATE + "female/face_shape.png"), "Fixed face PNG matches delivered base exactly")
	verify_baseline()
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	var breathing: CheckButton = scene.find_child("BreathingPreview", true, false)
	breathing.button_pressed = false
	check(gender.selected == 1, "Preview opens on the new female hair by default")
	choose(gender, 1)
	for part in 9:
		var choice: OptionButton = scene.find_child("Variant_" + PARTS[part], true, false)
		var expected_count: int = [3, 3, 1, 1, 1, 1, 1, 7, 1][part]
		check(choice.item_count == expected_count, PARTS[part] + ": UI choices match active catalog")
	var front: OptionButton = scene.find_child("Variant_hair_front", true, false)
	var back: OptionButton = scene.find_child("Variant_hair_back", true, false)
	for f in 3:
		for b in 3:
			choose(front, f)
			choose(back, b)
			check_composite(scene, "Front%d/back%d" % [f + 1, b + 1])
	for part in 9:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		toggle.button_pressed = false
		check_composite(scene, "Hide " + PARTS[part])
		toggle.button_pressed = true
		check_composite(scene, "Restore " + PARTS[part])
	seed(20261104)
	for iteration in 8:
		scene.find_child("RandomizeParts", true, false).emit_signal("pressed")
		for part in [2, 3, 4, 5, 8]:
			var choice: OptionButton = scene.find_child("Variant_" + PARTS[part], true, false)
			check(choice.selected == 0, "Random never restores retired " + PARTS[part])
		check_composite(scene, "Random%d" % iteration)
	scene.find_child("ResetVariants", true, false).emit_signal("pressed")
	check(front.selected == 0 and back.selected == 0, "Reset returns both hair choices to first new set")
	check_composite(scene, "Reset")
	choose(gender, 0)
	check(scene.find_child("PortraitPreview", true, false).texture != null, "Male preview still composes")
	choose(gender, 1)
	check_composite(scene, "Return to female")
	choose(gender, 0)
	scene.find_child("ReferenceFemaleFace", true, false).emit_signal("pressed")
	check(gender.selected == 1, "Reference female button returns to female")
	check_composite(scene, "Reference female button keeps blank face")
	scene.find_child("ResetVariants", true, false).emit_signal("pressed")
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture.get_image().save_png(OUTPUT + "game_composite.png") == OK, "Game composite PNG saved")
	await process_frame
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		var viewport_image := root.get_texture().get_image()
		viewport_captured = viewport_image != null and not viewport_image.is_empty()
		check(viewport_captured, "Actual viewport available")
		if viewport_captured:
			check(viewport_image.save_png(OUTPUT + "game_viewport.png") == OK, "Actual viewport PNG saved")
	var report := {"pass": failures.is_empty(), "checks": checks, "failures": failures, "hair_assets": 6, "blank_female_slots": 5, "hair_combinations": 9, "real_preview_scene": true, "baseline_files_checked": baseline_checks, "viewport_captured": viewport_captured, "display_server": DisplayServer.get_name(), "pixel_composites_verified": true}
	var output_file := FileAccess.open(OUTPUT + "runtime_check.json", FileAccess.WRITE)
	output_file.store_string(JSON.stringify(report, "  "))
	output_file.close()
	print("REFERENCE_HAIR_INSTALL: ", JSON.stringify(report))
	scene.queue_free()
	await process_frame
	quit(0 if failures.is_empty() else 1)
