extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/male_short_beard_install_20261108/"
const OUTPUT := PACK + "installation/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "beard"]
const ORDER := [1, 6, 7, 4, 3, 2, 5, 8, 0]
var config: Dictionary = {}
var baseline: Dictionary = {}
var failures: Array[String] = []
var checks: int = 0
var images: Dictionary = {}
var beard_file: String = ""
var beard_style: int = -1
var beard_full_id: String = ""
var old_beard: Dictionary = {}
var combinations: int = 0
var import_report: Dictionary = {}


func _initialize() -> void:
	call_deferred("verify")


func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures.append(message)
		push_error(message)


func json_file(path: String) -> Dictionary:
	var value: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	return value if value is Dictionary else {}


func component(part: int, female: bool = false) -> Dictionary:
	var gender: String = "female" if female else "male"
	var catalog_part: String = "makeup" if female and part == 8 else CATALOG_PARTS[part]
	for entry: Dictionary in config.get("components", []):
		if str(entry.get("gender")) == gender and str(entry.get("part")) == catalog_part:
			return entry
	return {}


func path_for(entry: Dictionary, index: int, rear: bool = false) -> String:
	var indices: Array = entry.get("variant_indices", [])
	var physical: int = int(indices[index]) if indices.size() > index else index + 1
	var template: String = str(entry.get("rear_file" if rear else "file", ""))
	return "" if template.is_empty() else ACTIVE + template.replace("{index}", "%02d" % physical)


func load_image(path: String) -> Image:
	if images.has(path):
		return images[path]
	var image: Image = Image.new()
	check(image.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK, "Decode PNG: " + path)
	image.convert(Image.FORMAT_RGBA8)
	images[path] = image
	return image


func ui_choice(scene: Node, part: int) -> OptionButton:
	return scene.find_child("Variant_" + PARTS[part], true, false)


func select(scene: Node, part: int, index: int) -> void:
	var control: OptionButton = ui_choice(scene, part)
	if control.selected == index:
		return
	control.select(index)
	control.emit_signal("item_selected", index)


func set_gender(scene: Node, female: bool) -> void:
	var control: OptionButton = scene.find_child("GenderChoice", true, false)
	var index: int = 1 if female else 0
	control.select(index)
	control.emit_signal("item_selected", index)


func selections(scene: Node) -> Array[int]:
	var result: Array[int] = []
	for part in 9:
		result.append(ui_choice(scene, part).selected)
	return result


func expected_image(scene: Node, female: bool) -> Image:
	var result: Image = Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
	for part in ORDER:
		if part == 6:
			var clothing_toggle: CheckButton = scene.find_child("Part_clothing", true, false)
			var rear: String = path_for(component(7, female), ui_choice(scene, 7).selected, true)
			if clothing_toggle.button_pressed and not rear.is_empty():
				result.blend_rect(load_image(rear), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
		var toggle: CheckButton = scene.find_child("Part_" + PARTS[part], true, false)
		if toggle.button_pressed:
			result.blend_rect(load_image(path_for(component(part, female), ui_choice(scene, part).selected)), Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	return result


func verify_pixels(scene: Node, label: String, female: bool = false) -> void:
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture != null, label + ": preview exists")
	if view.texture == null:
		return
	var actual: Image = view.texture.get_image()
	actual.convert(Image.FORMAT_RGBA8)
	check(actual.get_size() == Vector2i(1024, 1536) and actual.get_data() == expected_image(scene, female).get_data(), label + ": UI matches selected independent layers")


func verify_frozen_pngs() -> void:
	check(not baseline.is_empty(), "Frozen baseline exists")
	var png_count: int = 0
	for record: Dictionary in baseline.get("files", []):
		var file: String = str(record.get("file", ""))
		var path: String = file if file.begins_with("res://") else "res://" + file
		if file.ends_with(".png"):
			png_count += 1
			check(FileAccess.get_sha256(path) == str(record.get("sha256", "")), "Old PNG unchanged: " + file)
		elif file.ends_with("appearance_parts.json"):
			var backup: String = str(record.get("backup", ""))
			var old_config: Dictionary = json_file(PACK + backup)
			for entry: Dictionary in old_config.get("components", []):
				if str(entry.get("id", "")) == "male_beard":
					old_beard = entry
	check(png_count == 54, "All54 old PNGs covered by baseline")
	check(not old_beard.is_empty(), "Frozen old beard metadata loaded")


func verify_import() -> void:
	var texture: Texture2D = ResourceLoader.load(beard_file, "Texture2D", ResourceLoader.CACHE_MODE_IGNORE)
	check(texture != null, "New beard imports as Texture2D")
	if texture == null:
		return
	var imported: Image = texture.get_image()
	imported.convert(Image.FORMAT_RGBA8)
	var source: Image = load_image(beard_file)
	check(source.get_size() == Vector2i(1024, 1536) and not source.is_invisible(), "New beard is visible full-canvas PNG")
	check(imported.get_size() == source.get_size(), "Imported beard canvas retained")
	if imported.get_size() != source.get_size():
		return
	var actual: PackedByteArray = imported.get_data()
	var expected: PackedByteArray = source.get_data()
	var alpha_differences: int = 0
	var core_differences: int = 0
	var edge_rgb_differences: int = 0
	for index in range(3, expected.size(), 4):
		if actual[index] != expected[index]:
			alpha_differences += 1
		var changed: bool = actual[index - 3] != expected[index - 3] or actual[index - 2] != expected[index - 2] or actual[index - 1] != expected[index - 1]
		if changed and expected[index] >= 230:
			core_differences += 1
		elif changed:
			edge_rgb_differences += 1
	check(alpha_differences == 0 and core_differences == 0, "Import retains all alpha and opaque-core RGB")
	import_report = {"alpha_different_pixels": alpha_differences, "opaque_core_rgb_different_pixels": core_differences, "low_alpha_rgb_different_pixels": edge_rgb_differences, "note": "Existing Godot alpha-border processing can alter low-alpha RGB; not claimed full RGBA identity."}


func verify_catalog_and_renderer() -> void:
	var catalog: Node = root.get_node("PortraitCatalog")
	var data: Resource = load("res://csharp/resources/HumanData.cs").new()
	data.set("gender", 0)
	data.set("appearance_beard_style", beard_style)
	var renderer: Control = load("res://csharp/panel/PortraitRenderer.cs").new()
	renderer.size = Vector2(1024, 1536)
	root.add_child(renderer)
	for age in [16, 25, 40]:
		var context: Dictionary = catalog.call("get_context", data, "male", age, "", "")
		for requested: String in [str(beard_style), beard_full_id]:
			var selected: Dictionary = catalog.call("choose_part", context, "beard", requested)
			check(str(selected.get("resource_path", "")) == beard_file, "Explicit short beard resolves at age%d: %s" % [age, requested])
		var saved: Dictionary = catalog.call("get_requested_appearance", data)
		check(str(saved.get("beard", "")) == str(beard_style), "Saved beard style retained at age%d" % age)
		renderer.call("configure", data, context, saved)
		var composition: Node = renderer.find_child("PortraitComposition", true, false)
		var layer: TextureRect = composition.get_node("Beard")
		check(layer.texture != null and layer.texture.resource_path == beard_file and layer.visible, "Formal renderer shows short beard at age%d" % age)
		check(layer.position == Vector2.ZERO and layer.scale == Vector2.ONE and layer.modulate == Color.WHITE, "Formal beard uses registered coordinates and unmodified color")
		var mouth: TextureRect = composition.get_node("Mouth")
		check(layer.z_index > mouth.z_index, "Beard renders above mouth")
		var clean: Dictionary = catalog.call("choose_part", context, "beard", "0")
		check(str(clean.get("resource_path", "")) == ACTIVE + "male/face_accessory/01.png", "Old saved style0 remains no-beard at age%d" % age)
		for alias: Variant in old_beard.get("variant_aliases", {}):
			var old_style: int = int(old_beard.variant_aliases[alias])
			var resolved: Dictionary = catalog.call("choose_part", context, "beard", str(alias))
			check(int(resolved.get("variant_index", -1)) == old_style, "Existing saved alias remains compatible at age%d: %s" % [age, str(alias)])
		if age < 35:
			var random_beard: Dictionary = catalog.call("choose_part", context, "beard", "")
			check(int(random_beard.get("variant_index", -1)) == 0, "Unrequested youth random selection retains no-beard default at age%d" % age)
	renderer.call("clear_portrait")
	renderer.queue_free()


func capture(scene: Node, filename: String) -> void:
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	check(view.texture.get_image().save_png(OUTPUT + filename + "_composite.png") == OK, "Save actual UI composite: " + filename)
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		check(root.get_texture().get_image().save_png(OUTPUT + filename + "_viewport.png") == OK, "Save viewport: " + filename)


func verify_ui(scene: Node) -> void:
	var breathing: CheckButton = scene.find_child("BreathingPreview", true, false)
	breathing.button_pressed = false
	set_gender(scene, false)
	check(ui_choice(scene, 8).item_count == 2, "Male face-accessory UI has no-beard and short beard")
	check(ui_choice(scene, 8).get_item_text(0) == "无须", "No-beard remains first UI choice")
	for part in 9:
		select(scene, part, 0)
	verify_pixels(scene, "Retained clean-shaven baseline")
	await capture(scene, "game_male_no_beard")
	var clean: Image = scene.find_child("PortraitPreview", true, false).texture.get_image()
	select(scene, 8, 1)
	var saved: Array[int] = selections(scene)
	check(saved.slice(0, 8) == [0, 0, 0, 0, 0, 0, 0, 0], "Beard choice preserves all other selections")
	verify_pixels(scene, "Selected short beard")
	await capture(scene, "game_male_short_beard")
	var bearded: Image = scene.find_child("PortraitPreview", true, false).texture.get_image()
	check(bearded.get_data() != clean.get_data(), "Selected short beard visibly changes portrait")
	var toggle: CheckButton = scene.find_child("Part_face_accessory", true, false)
	toggle.button_pressed = false
	verify_pixels(scene, "Independent beard visibility off")
	check(scene.find_child("PortraitPreview", true, false).texture.get_image().get_data() == clean.get_data(), "Hiding beard restores exact original composition")
	toggle.button_pressed = true
	check(selections(scene) == saved, "Visibility toggle retains short-beard selection")
	for front in int(component(0).variant_count):
		for back in int(component(1).variant_count):
			for clothing in int(component(7).variant_count):
				select(scene, 0, front)
				select(scene, 1, back)
				select(scene, 7, clothing)
				check(ui_choice(scene, 8).selected == 1, "Hair/clothes preserve beard choice")
				verify_pixels(scene, "Short beard front%d/back%d/clothes%d" % [front, back, clothing])
				combinations += 1
	check(combinations == 63, "All3 front ×3 back ×7 clothes beard combinations checked")
	var male_saved: Array[int] = selections(scene)
	set_gender(scene, true)
	check(ui_choice(scene, 8).item_count == 1, "Female face-accessory choices unchanged")
	verify_pixels(scene, "Unchanged female baseline", true)
	select(scene, 0, 2)
	select(scene, 1, 1)
	select(scene, 2, 3)
	select(scene, 3, 2)
	var female_saved: Array[int] = selections(scene)
	verify_pixels(scene, "Existing female mixed choice", true)
	await capture(scene, "game_female_preserved")
	set_gender(scene, false)
	check(selections(scene) == male_saved, "Male beard/hair/clothes choice survives gender switch")
	verify_pixels(scene, "Restored male short-beard combination")
	set_gender(scene, true)
	check(selections(scene) == female_saved, "Female selections survive gender switch")
	set_gender(scene, false)
	scene.find_child("ResetVariants", true, false).emit_signal("pressed")
	check(ui_choice(scene, 8).selected == 0, "Restore-current button returns to no-beard baseline")
	verify_pixels(scene, "Restore no-beard button")
	select(scene, 8, 1)
	await capture(scene, "game_male_final")


func finish() -> void:
	var result: Dictionary = {"pass": failures.is_empty(), "checks": checks, "failures": failures, "beard_file": beard_file, "beard_style": beard_style, "beard_full_id": beard_full_id, "male_hair_clothing_combinations": combinations, "imported_texture": import_report, "scope": "One short-beard addition;63 hair/clothing combinations, UI independence, actual PNG composition, saved/explicit styles at16/25/40, old PNGs and female preservation. No blanket rerun of historic portrait suites."}
	var output: FileAccess = FileAccess.open(OUTPUT + "runtime_check.json", FileAccess.WRITE)
	output.store_string(JSON.stringify(result, "  "))
	output.close()
	print("MALE_SHORT_BEARD_20261108: ", JSON.stringify(result))
	quit(0 if failures.is_empty() else 1)


func verify() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	config = json_file(ACTIVE + "appearance_parts.json")
	baseline = json_file(PACK + "source/baseline.json")
	var entry: Dictionary = component(8)
	check(int(entry.get("variant_count", 0)) == 2, "Exactly one optional beard appended")
	if int(entry.get("variant_count", 0)) != 2:
		finish()
		return
	beard_style = int(entry.variant_style_ids[1])
	beard_full_id = str(entry.id) + "_%02d" % beard_style
	beard_file = path_for(entry, 1)
	verify_frozen_pngs()
	root.get_node("AppearanceModCatalog").call("reload")
	check(root.get_node("AppearanceModCatalog").call("get_parts", "male", "beard").size() == 2, "Formal catalog sees both beard choices")
	verify_import()
	verify_catalog_and_renderer()
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	await verify_ui(scene)
	scene.queue_free()
	await process_frame
	images.clear()
	finish()
