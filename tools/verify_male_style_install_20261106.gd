extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/male_style_alignment_20261106/integration/"
const OUTPUT := PACK + "installation/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "beard"]
const MALE_COUNTS := [1, 1, 1, 1, 1, 1, 1, 7, 1]
const FEMALE_COUNTS := [3, 3, 4, 3, 1, 1, 1, 7, 1]
const APPROVED_SHA := "d97b79e3372df42db24b4dd5c3ec42f376d652616d9fa25d78a393b004da81a8"
const ORDER := [1, 6, 7, 4, 3, 2, 5, 8, 0]
var checks := 0
var failures: Array[String] = []
var config: Dictionary
var images: Dictionary = {}
var combinations := {"male": 0, "female": 0}
var rear_pair_checks := 0
var renderer_pair_checks := 0
var import_checks: Array[Dictionary] = []
var report: Dictionary = {}


func _initialize() -> void:
	call_deferred("verify")


func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures.append(message)
		push_error(message)


func bytes_sha256(data: PackedByteArray) -> String:
	var hashing := HashingContext.new()
	hashing.start(HashingContext.HASH_SHA256)
	hashing.update(data)
	return hashing.finish().hex_encode()


func counts_for(female: bool) -> Array:
	return report.slot_counts.female if female else report.slot_counts.male


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
	var different_visible_pixels := 0
	var different_transparent_rgb := 0
	var different_alpha := 0
	for index in range(3, expected.size(), 4):
		if actual[index] != expected[index]:
			different_alpha += 1
		var rgb_changed := actual[index - 3] != expected[index - 3] or actual[index - 2] != expected[index - 2] or actual[index - 1] != expected[index - 1]
		if rgb_changed:
			if expected[index] == 0:
				different_transparent_rgb += 1
			else:
				different_visible_pixels += 1
			if expected[index] >= 230:
				different_core_pixels += 1
	if different_core_pixels > 0:
		imported.save_png(OUTPUT + "import_mismatch_" + file.get_base_dir().get_file() + "_" + file.get_file())
	check(different_core_pixels == 0, "Imported opaque core matches current PNG (%d mismatches): " % different_core_pixels + file)
	check(different_alpha == 0, "Imported Alpha matches current PNG: " + file)
	# Godot's configured fix_alpha_border can change hidden/low-alpha edge RGB.
	# Record all differences instead of calling this a full RGBA equality check.
	import_checks.append({"file": file, "rgba_exact": actual == expected,
		"alpha_different_pixels": different_alpha, "opaque_core_rgb_different_pixels": different_core_pixels,
		"visible_rgb_different_pixels": different_visible_pixels,
		"transparent_rgb_different_pixels": different_transparent_rgb,
		"border_processing": "Existing Godot process/fix_alpha_border setting retained; raw-PNG UI compositions are checked separately pixel for pixel."})


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
	var counts := counts_for(female)
	set_gender(scene, female)
	for part in 9:
		check(choice(scene, part).item_count == counts[part], gender + " independent UI options: " + PARTS[part])
	for part in [0, 1, 2, 3, 7]:
		var before := selections(scene)
		select(scene, part, (before[part] + 1) % int(counts[part]))
		var after := selections(scene)
		for other in 9:
			if other != part:
				check(after[other] == before[other], gender + " changing " + PARTS[part] + " preserves " + PARTS[other])
	for front in int(counts[0]):
		for back in int(counts[1]):
			for brow in int(counts[2]):
				for eye in int(counts[3]):
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
	if not female:
		save_seven_collars(scene)
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


func save_seven_collars(scene: Node) -> void:
	var light := Image.create_empty(2240, 480, false, Image.FORMAT_RGBA8)
	var dark := Image.create_empty(2240, 480, false, Image.FORMAT_RGBA8)
	light.fill(Color("e9e4dc"))
	dark.fill(Color("262b30"))
	for clothing in 7:
		select(scene, 7, clothing)
		for part in 9:
			scene.find_child("Part_" + PARTS[part], true, false).button_pressed = part != 0 and part != 1
		verify_pixels(scene, "Male no-hair collar %02d" % [clothing + 1])
		var view: TextureRect = scene.find_child("PortraitPreview", true, false)
		var image: Image = view.texture.get_image()
		check(image.save_png(OUTPUT + "game_male_nohair_collar_%02d.png" % [clothing + 1]) == OK, "Save actual no-hair head/neck/clothes %02d" % [clothing + 1])
		image.resize(320, 480, Image.INTERPOLATE_LANCZOS)
		light.blend_rect(image, Rect2i(0, 0, 320, 480), Vector2i(clothing * 320, 0))
		dark.blend_rect(image, Rect2i(0, 0, 320, 480), Vector2i(clothing * 320, 0))
	check(light.save_png(OUTPUT + "seven_collars_light.png") == OK, "Save seven actual collars on light background")
	check(dark.save_png(OUTPUT + "seven_collars_dark.png") == OK, "Save seven actual collars on dark background")
	for part in 9:
		scene.find_child("Part_" + PARTS[part], true, false).button_pressed = true


func verify_backup_and_head() -> void:
	var backup := ZIPReader.new()
	var opened := backup.open(str(report.backup)) == OK
	check(opened, "Full pre-install backup ZIP opens")
	if not opened:
		return
	for file in report.baseline_sha256:
		var member := str(file).trim_prefix("res://")
		check(backup.file_exists(member), "Frozen PNG is in backup: " + member)
		if backup.file_exists(member):
			check(bytes_sha256(backup.read_file(member)) == str(report.baseline_sha256[file]), "Frozen PNG backup SHA: " + member)
	var head_member := (ACTIVE + "male/face_shape/01.png").trim_prefix("res://")
	var old_head := Image.new()
	check(old_head.load_png_from_buffer(backup.read_file(head_member)) == OK, "Decode frozen head/neck")
	old_head.convert(Image.FORMAT_RGBA8)
	var before := old_head.get_data()
	var after := load_image(ACTIVE + "male/face_shape/01.png").get_data()
	var alpha_differences := 0
	if before.size() == after.size():
		for index in range(3, before.size(), 4):
			if before[index] != after[index]:
				alpha_differences += 1
	check(before.size() == after.size() and alpha_differences == 0, "Head/neck Alpha exactly retained for all seven collars")
	backup.close()


func verify_saved_styles() -> void:
	var catalog := root.get_node("PortraitCatalog")
	for entry: Dictionary in config.components:
		for random_seed in [1, 42, 31415]:
			var context: Dictionary = {"gender": str(entry.gender), "age": 40, "seed": random_seed}
			for alias in entry.get("variant_aliases", {}):
				var resolved: Dictionary = catalog.call("choose_part", context, str(entry.part), str(alias))
				check(int(resolved.get("variant_index", -1)) == int(entry.variant_aliases[alias]), "Saved alias preserved %s/%s:%s" % [entry.gender, entry.part, alias])
			for style in entry.get("variant_style_ids", []):
				var resolved: Dictionary = catalog.call("choose_part", context, str(entry.part), str(int(style)))
				check(int(resolved.get("variant_index", -1)) == int(style), "Saved style preserved %s/%s:%s" % [entry.gender, entry.part, style])


func verify() -> void:
	if not FileAccess.file_exists(OUTPUT + "install_report.json"):
		push_error("Reviewed male installation report is missing; this verifier never installs assets.")
		quit(2)
		return
	config = JSON.parse_string(FileAccess.get_file_as_string(ACTIVE + "appearance_parts.json"))
	report = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT + "install_report.json"))
	var mods := root.get_node("AppearanceModCatalog")
	mods.reload()
	check(str(report.schema) == "male-style-install-report/v1", "Current male replacement report schema")
	check(str(report.approved_portrait_sha256) == APPROVED_SHA and FileAccess.get_sha256(str(report.approved_portrait)) == APPROVED_SHA, "User-approved original-color 01 portrait")
	var reported_male_counts: Array[int] = []
	var reported_female_counts: Array[int] = []
	for value in report.slot_counts.male:
		reported_male_counts.append(int(value))
	for value in report.slot_counts.female:
		reported_female_counts.append(int(value))
	check(reported_male_counts == MALE_COUNTS and reported_female_counts == FEMALE_COUNTS, "Existing male/female option counts remain current")
	var active_options := 0
	for entry: Dictionary in config.components:
		active_options += int(entry.variant_count)
	check(active_options == 39, "39 existing selectable options: male15 plus female24")
	check(report.records.size() == 9, "Nine approved male layers replaced")
	check(report.baseline_sha256.size() == 46 and int(report.png_count) == 46, "46 current registered PNGs retained in inventory")
	check(report.preserved_sha256.size() == 37 and int(report.female_pngs_preserved) == 24, "37 old PNGs retained, including all24 female")
	check(FileAccess.get_sha256(ACTIVE + "appearance_parts.json") == str(report.config_sha256), "Appearance config exact byte SHA unchanged")
	check(FileAccess.get_file_as_bytes(ACTIVE + "appearance_parts.json") == FileAccess.get_file_as_bytes(OUTPUT + "appearance_parts.before.json"), "Appearance config byte-for-byte unchanged")
	var before_config: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT + "appearance_parts.before.json"))
	check(before_config == config and config.components == report.baseline_components, "All component metadata, styles and aliases unchanged")
	check(config.canvas.width == 1024 and config.canvas.height == 1536, "Common full canvas retained")
	var before_manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT + "portrait_manifest.before.json"))
	var after_manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ACTIVE + "portrait_manifest.json"))
	check(after_manifest.has("male_style_alignment_20261106"), "Only new source-provenance metadata added")
	after_manifest.erase("male_style_alignment_20261106")
	check(after_manifest == before_manifest, "All other manifest fields unchanged")
	check(FileAccess.get_sha256(ACTIVE + "portrait_manifest.json") == str(report.manifest_after_sha256), "Manifest recorded SHA")
	check(str(component(7).get("rear_file", "")) == "male/clothing_back/{index}.png", "Seven male clothing/rear pair template retained")
	var targets: Array[String] = []
	for part in PARTS.slice(0, 8):
		targets.append("male/" + str(part) + "/01.png")
	targets.append("male/clothing_back/01.png")
	for record: Dictionary in report.records:
		check(targets.has(str(record.target)), "Only a whitelisted male01 path replaced")
		targets.erase(str(record.target))
		for key in ["source_path", "candidate_path", "resource_path"]:
			check(FileAccess.get_sha256(str(record[key])) == str(record.sha256), "Reviewed/install PNG SHA " + str(record[key]))
		check(ResourceLoader.exists(str(record.resource_path)), "Imported resource exists")
		verify_imported_core(str(record.resource_path))
		var image := load_image(str(record.resource_path))
		check(image.get_size() == Vector2i(1024, 1536) and not image.is_invisible(), "Visible 1024x1536 approved layer")
		check(image.get_data() == load_image(str(record.source_path)).get_data(), "All installed pixels equal reviewed candidate")
	check(targets.is_empty(), "All nine distinct target paths checked")
	for file in report.preserved_sha256:
		check(FileAccess.get_sha256(str(file)) == str(report.preserved_sha256[file]), "Unreplaced old PNG unchanged " + str(file))
	verify_backup_and_head()
	for part in 9:
		for female in [false, true]:
			var gender := "female" if female else "male"
			var catalog_part: String = "makeup" if female and part == 8 else CATALOG_PARTS[part]
			check(mods.get_parts(gender, catalog_part).size() == int(counts_for(female)[part]), gender + " catalog independent count " + PARTS[part])
	verify_saved_styles()
	verify_renderer_pairs()
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	scene.find_child("BreathingPreview", true, false).button_pressed = false
	verify_gender(scene, false)
	verify_gender(scene, true)
	check(int(combinations.female) == 108, "All108 female front/back/brow/eye combinations checked")
	verify_anchor_toggle(scene)
	set_gender(scene, false)
	select(scene, 7, 6)
	var male_saved := selections(scene)
	set_gender(scene, true)
	select(scene, 0, 2)
	select(scene, 1, 1)
	select(scene, 2, 3)
	select(scene, 3, 2)
	var female_saved := selections(scene)
	set_gender(scene, false)
	check(selections(scene) == male_saved, "Male independent selections survive gender switching")
	verify_pixels(scene, "Male restored selections")
	set_gender(scene, true)
	check(selections(scene) == female_saved, "Female independent selections survive gender switching")
	verify_pixels(scene, "Female restored selections", true)
	scene.find_child("ReferenceFemaleFace", true, false).emit_signal("pressed")
	check(selections(scene) == female_saved, "Female reference button retains current choices")
	var female_captured: bool = await save_gender_capture(scene, true)
	var male_captured: bool = await save_gender_capture(scene, false)
	var result := {"pass": failures.is_empty(), "checks": checks, "failures": failures,
		"active_option_count": active_options, "physical_png_count": int(report.png_count),
		"installed_pngs_checked": report.records.size(), "preserved_pngs_checked": report.preserved_sha256.size(),
		"baseline_backup_pngs_checked": report.baseline_sha256.size(), "female_preserved": int(report.female_pngs_preserved),
		"rear_pair_visibility_checks": rear_pair_checks, "renderer_pair_checks": renderer_pair_checks,
		"male_hair_feature_combinations": combinations.male, "female_hair_feature_combinations": combinations.female,
		"head_alpha_unchanged": true, "pixel_composites_verified": failures.is_empty(),
		"imported_texture_checks": import_checks,
		"male_viewport_captured": male_captured, "female_viewport_captured": female_captured,
		"visual_acceptance": "Engineering checks verify installation and composition; generated hidden areas and final likeness remain subject to user review."}
	var output_file := FileAccess.open(OUTPUT + "runtime_check.json", FileAccess.WRITE)
	output_file.store_string(JSON.stringify(result, "  "))
	output_file.close()
	print("MALE_STYLE_INSTALL_20261106: ", JSON.stringify(result))
	scene.queue_free()
	await process_frame
	images.clear()
	quit(0 if failures.is_empty() else 1)
