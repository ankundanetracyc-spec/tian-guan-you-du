extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/portrait_original_restore_20261105_v6/"
const OUTPUT := PACK + "installation/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "beard"]
const COUNTS := [3, 3, 3, 2, 1, 1, 1, 7, 1]
const ORDER := [1, 6, 7, 4, 3, 2, 5, 8, 0]
var checks := 0
var failures: Array[String] = []
var config: Dictionary
var images: Dictionary = {}
var combinations := {"male": 0, "female": 0}
var rear_pair_checks := 0
var renderer_pair_checks := 0


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
	var control: OptionButton = scene.find_child("GenderChoice", true, false)
	var index := 1 if female else 0
	control.select(index)
	control.emit_signal("item_selected", index)


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
	check(imported.get_size() == source.get_size(), "Imported texture canvas: " + file)
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
	if different_core_pixels > 0:
		imported.save_png(OUTPUT + "import_mismatch_" + file.get_base_dir().get_file() + "_" + file.get_file())
	check(different_core_pixels == 0, "Imported opaque core matches current PNG (%d mismatches): " % different_core_pixels + file)


func rear_file(scene: Node, female: bool) -> String:
	var clothing := component(7, female)
	var template := str(clothing.get("rear_file", ""))
	if template.is_empty():
		return ""
	var selected: int = choice(scene, 7).selected
	var index: int = clothing.variant_indices[selected] if clothing.has("variant_indices") else selected + 1
	return ACTIVE + template.replace("{index}", "%02d" % index)


func expected_image(scene: Node, female: bool) -> Image:
	var expected := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
	for part in ORDER:
		if part == 6:
			var clothes_visible: CheckButton = scene.find_child("Part_clothing", true, false)
			var rear := rear_file(scene, female)
			if clothes_visible.button_pressed and not rear.is_empty():
				expected.blend_rect(load_image(rear), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		if not toggle.button_pressed:
			continue
		var entry := component(part, female)
		var selected: int = choice(scene, part).selected
		var index: int = entry.variant_indices[selected] if entry.has("variant_indices") else selected + 1
		var gender := "female" if female else "male"
		var file: String = ACTIVE + gender + "/" + PARTS[part] + "/%02d.png" % index
		expected.blend_rect(load_image(file), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	var anchors: CheckButton = scene.find_child("AnchorGuide", true, false)
	if anchors.button_pressed:
		var gender := "female" if female else "male"
		var overlays: Dictionary = config.get("anchor_overlays", {})
		var overlay := str(overlays.get(gender, ""))
		if overlay.is_empty():
			overlay = "res://mods/appearance/new_ink_hair_20261003_v1/rig/" + gender + "_overlay.png"
		expected.blend_rect(load_image(overlay), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	return expected


func verify_pixels(scene: Node, label: String, female: bool = false) -> void:
	var expected := expected_image(scene, female)
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture != null, label + " texture exists")
	if view.texture != null:
		var actual := view.texture.get_image()
		actual.convert(Image.FORMAT_RGBA8)
		check(actual.get_size() == Vector2i(1024, 1536), label + " canvas")
		check(actual.get_data() == expected.get_data(), label + " game pixels equal selected independent layers")


func verify_anchor_toggle(scene: Node) -> void:
	set_gender(scene, false)
	var anchors: CheckButton = scene.find_child("AnchorGuide", true, false)
	check(not anchors.button_pressed, "Anchor overlay remains hidden by default")
	anchors.button_pressed = true
	verify_pixels(scene, "Male current wearer anchors overlay equals selected layers plus configured new overlay")
	anchors.button_pressed = false
	verify_pixels(scene, "Male anchor toggle off restores plain layers")


func verify_rear_pairing(scene: Node, female: bool, clothing: int) -> void:
	var gender := "female" if female else "male"
	var rear := rear_file(scene, female)
	if female:
		check(rear.is_empty(), "Female clothing retains original single layer %02d" % [clothing + 1])
	else:
		check(rear == ACTIVE + "male/clothing_back/%02d.png" % [clothing + 1], "Male rear file follows the same selected clothing index %02d" % [clothing + 1])
		check(ResourceLoader.exists(rear), "Bound rear layer exists for male clothing %02d" % [clothing + 1])
	for part in 9:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		toggle.button_pressed = part == 7
	verify_pixels(scene, "%s clothes alone %02d (rear stays when face hidden)" % [gender, clothing + 1], female)
	var clothing_toggle: CheckButton = scene.find_child("Part_clothing", true, false)
	clothing_toggle.button_pressed = false
	verify_pixels(scene, "%s hide bound clothes %02d" % [gender, clothing + 1], female)
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture.get_image().is_invisible(), "%s hides both front and rear with one clothing control %02d" % [gender, clothing + 1])
	for part in 9:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		toggle.button_pressed = true
	rear_pair_checks += 1


func verify_renderer_pairs() -> void:
	var renderer: Control = load("res://csharp/panel/PortraitRenderer.cs").new()
	root.add_child(renderer)
	var catalog := root.get_node("PortraitCatalog")
	var data: Resource = load("res://csharp/resources/HumanData.cs").new()
	for female in [false, true]:
		var gender := "female" if female else "male"
		data.set("gender", 1 if female else 0)
		var context: Dictionary = catalog.get_context(data, gender, 40, "", "")
		for clothing in 7:
			renderer.call("configure", data, context, {"clothes": str(clothing)})
			var composition: Node = renderer.find_child("PortraitComposition", true, false)
			var nose_layer: CanvasItem = composition.get_node("Nose")
			var eye_layer: CanvasItem = composition.get_node("Eye")
			var brow_layer: CanvasItem = composition.get_node("Brow")
			check(nose_layer.z_index < eye_layer.z_index and nose_layer.z_index < brow_layer.z_index, "Nose and nasion render below both eyes and eyebrows: " + gender)
			var rear: TextureRect
			var front: TextureRect
			var face: TextureRect
			for child in composition.get_children():
				var key := str(child.name).replace(" ", "").to_lower()
				if key == "rearclothes":
					rear = child
				elif key == "clothes":
					front = child
				elif key == "face":
					face = child
			check(rear != null and front != null and face != null, "Renderer has private rear/front/face layers")
			if rear == null or front == null or face == null:
				continue
			check(rear.get_index() < face.get_index() and face.get_index() < front.get_index(), "Renderer places rear fabric behind skin and front fabric in front")
			check(rear.position == front.position, "Renderer paired clothes share offset")
			if female:
				check(not rear.visible and rear.texture == null, "Renderer clears male rear when switching to female %02d" % [clothing + 1])
			else:
				check(rear.visible and front.visible and rear.texture != null, "Renderer shows male front/rear pair %02d" % [clothing + 1])
				if rear.texture != null:
					check(rear.texture.resource_path == ACTIVE + "male/clothing_back/%02d.png" % [clothing + 1], "Renderer loads rear from same clothing selection")
			renderer_pair_checks += 1
	renderer.call("clear_portrait")
	renderer.queue_free()


func verify_gender(scene: Node, female: bool) -> void:
	var gender := "female" if female else "male"
	set_gender(scene, female)
	for part in 9:
		check(choice(scene, part).item_count == COUNTS[part], gender + " independent UI options: " + PARTS[part])
	for part in [0, 1, 2, 3, 7]:
		var before := selections(scene)
		select(scene, part, (before[part] + 1) % COUNTS[part])
		var after := selections(scene)
		for other in 9:
			if other != part:
				check(after[other] == before[other], gender + " changing " + PARTS[part] + " preserves " + PARTS[other])
	for front in 3:
		for back in 3:
			for brow in 3:
				for eye in 2:
					select(scene, 0, front)
					select(scene, 1, back)
					select(scene, 2, brow)
					select(scene, 3, eye)
					verify_pixels(scene, "%s F%d B%d Brow%d Eye%d" % [gender, front, back, brow, eye], female)
					combinations[gender] += 1
	for clothing in 7:
		select(scene, 7, clothing)
		verify_pixels(scene, "%s clothing%02d" % [gender, clothing + 1], female)
		verify_rear_pairing(scene, female, clothing)
	for part in 9:
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		toggle.button_pressed = false
		verify_pixels(scene, gender + " hide " + PARTS[part], female)
		toggle.button_pressed = true
		verify_pixels(scene, gender + " restore " + PARTS[part], female)
	seed(20261104 + int(female))
	for iteration in 4:
		scene.find_child("RandomizeParts", true, false).emit_signal("pressed")
		verify_pixels(scene, "%s random%d" % [gender, iteration], female)
	scene.find_child("ResetVariants", true, false).emit_signal("pressed")
	check(selections(scene) == [0, 0, 0, 0, 0, 0, 0, 0, 0], gender + " reset returns each independent slot to first choice")
	verify_pixels(scene, gender + " reset", female)


func save_gender_capture(scene: Node, female: bool) -> bool:
	var gender := "female" if female else "male"
	set_gender(scene, female)
	for part in 9:
		select(scene, part, 0)
	await process_frame
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture.get_image().save_png(OUTPUT + "game_" + gender + "_composite.png") == OK, "Save " + gender + " actual composite")
	if DisplayServer.get_name() == "headless":
		return false
	await RenderingServer.frame_post_draw
	var captured := root.get_texture().get_image().save_png(OUTPUT + "game_" + gender + "_viewport.png") == OK
	check(captured, "Save " + gender + " actual viewport")
	return captured


func verify() -> void:
	if not FileAccess.file_exists(OUTPUT + "install_report.json"):
		push_error("Install the visually reviewed v5 candidate before runtime verification.")
		quit(1)
		return
	config = JSON.parse_string(FileAccess.get_file_as_string(ACTIVE + "appearance_parts.json"))
	var report: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT + "install_report.json"))
	var mods := root.get_node("AppearanceModCatalog")
	mods.reload()
	var active_options := 0
	for entry in config.components:
		active_options += int(entry.variant_count)
	check(active_options == 44, "44 independent selectable options retained")
	check(report.records.size() == 28, "14 original-pixel male head components and 14 unchanged clothing layers installed")
	check(report.baseline_sha256.size() == 23, "23 registered textures retained, all female assets and empty male accessory")
	check(report.female_baseline_sha256.size() == 22, "All 22 female textures retained")
	check(FileAccess.get_sha256(ACTIVE + "appearance_parts.json") == str(report.config_sha256), "Exact installed configuration SHA")
	var before_config := FileAccess.get_file_as_string(ACTIVE + "appearance_parts.json").replace(str(report.anchor_overlay_resource_path), str(report.config_previous_overlay))
	check(before_config.sha256_text() == str(report.config_before_sha256), "Only male anchor overlay path changed in selection configuration")
	check(FileAccess.get_sha256(ACTIVE + "portrait_manifest.json") == str(report.manifest_after_sha256), "Installed provenance SHA")
	check(FileAccess.get_sha256(str(report.anchor_overlay_resource_path)) == str(report.anchor_overlay_sha256), "Current face and neck overlay SHA")
	for part in 9:
		for female in [false, true]:
			var gender := "female" if female else "male"
			var catalog_part: String = "makeup" if female and part == 8 else CATALOG_PARTS[part]
			check(mods.get_parts(gender, catalog_part).size() == COUNTS[part], gender + " independent count " + PARTS[part])
	for record in report.records:
		check(str(record.gender) == "male" and str(record.part) in ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "clothing_back"], "Only authorized male head and compatible garment layer replaced")
		check(FileAccess.get_sha256(str(record.resource_path)) == str(record.sha256), "Installed SHA " + str(record.resource_path))
		check(FileAccess.get_sha256(str(record.candidate_path)) == str(record.sha256), "Candidate SHA " + str(record.candidate_path))
		check(FileAccess.get_sha256(str(record.before_resource_path)) == str(record.before_sha256), "Frozen prior file retained")
		check(ResourceLoader.exists(str(record.resource_path)), "Imported resource exists")
		verify_imported_core(str(record.resource_path))
		var image := load_image(str(record.resource_path))
		check(image.get_size() == Vector2i(1024, 1536) and not image.is_invisible(), "Visible common full-canvas PNG")
		check(image.get_data() == load_image(str(record.candidate_path)).get_data(), "Installed pixels equal registered candidate")
	for file in report.baseline_sha256:
		check(FileAccess.get_sha256(str(file)) == str(report.baseline_sha256[file]), "Preserved registered PNG " + str(file))
	for file in report.female_baseline_sha256:
		check(FileAccess.get_sha256(str(file)) == str(report.female_baseline_sha256[file]), "Female unchanged " + str(file))
	verify_renderer_pairs()
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	scene.find_child("BreathingPreview", true, false).button_pressed = false
	verify_gender(scene, false)
	verify_gender(scene, true)
	verify_anchor_toggle(scene)
	set_gender(scene, false)
	select(scene, 0, 1)
	select(scene, 1, 2)
	select(scene, 2, 1)
	select(scene, 3, 1)
	select(scene, 7, 6)
	var male_saved := selections(scene)
	set_gender(scene, true)
	select(scene, 0, 2)
	select(scene, 1, 1)
	var female_saved := selections(scene)
	set_gender(scene, false)
	check(selections(scene) == male_saved, "Male independent choices persist across gender switch")
	verify_pixels(scene, "Male restored choices")
	set_gender(scene, true)
	check(selections(scene) == female_saved, "Female independent choices persist across gender switch")
	verify_pixels(scene, "Female restored choices", true)
	var male_captured: bool = await save_gender_capture(scene, false)
	var female_captured: bool = await save_gender_capture(scene, true)
	var result := {"pass": failures.is_empty(), "checks": checks, "failures": failures, "active_option_count": active_options, "physical_png_count": 51, "installed_pngs_checked": report.records.size(), "baseline_files_checked": report.baseline_sha256.size(), "female_preserved": report.female_baseline_sha256.size(), "rear_pair_visibility_checks": rear_pair_checks, "renderer_pair_checks": renderer_pair_checks, "male_hair_feature_combinations": combinations.male, "female_hair_feature_combinations": combinations.female, "pixel_composites_verified": true, "male_viewport_captured": male_captured, "female_viewport_captured": female_captured, "visual_acceptance": "Installation and independent mixing verified. Artistic likeness remains subject to the user's judgement; test counts are not proof of likeness."}
	var output_file := FileAccess.open(OUTPUT + "runtime_check.json", FileAccess.WRITE)
	output_file.store_string(JSON.stringify(result, "  "))
	output_file.close()
	print("PORTRAIT_FAITHFUL_RESTORE_20261105: ", JSON.stringify(result))
	scene.queue_free()
	await process_frame
	images.clear()
	quit(0 if failures.is_empty() else 1)
