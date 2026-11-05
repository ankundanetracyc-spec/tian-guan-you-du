extends SceneTree

func _initialize() -> void:
	call_deferred("verify")

func verify() -> void:
	var base := "res://mods/appearance/ink_three_variants_20261004/"
	var report := "res://docs/美术规范/立绘全面复核_20261103/"
	var old: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(report + "appearance_parts.before.json"))
	var removed: Array = JSON.parse_string(FileAccess.get_file_as_string(report + "removed.json"))
	var rejected_names: Array = removed.map(func(e): return e.name)
	var mods := root.get_node("AppearanceModCatalog")
	var portraits := root.get_node("PortraitCatalog")
	mods.reload()
	var checks := 0
	for component in old.components:
		var entries: Array = mods.get_parts(component.gender, component.part)
		for index in component.variant_count:
			var name: String = component.variant_names[index]
			var previous_id := "%s_%02d" % [component.id, index]
			var found: Dictionary = {}
			for entry in entries:
				assert(not entry.display_name in rejected_names)
				if entry.id == previous_id: found = entry
			if name in rejected_names:
				assert(found.is_empty())
				for seed_value in [1, 42, 31415]:
					var context := {"gender": component.gender, "seed": seed_value, "age": 40}
					for request in [str(index), previous_id]:
						var fallback: Dictionary = portraits.choose_part(context, component.part, request)
						assert(not fallback.is_empty())
						assert(not fallback.display_name in rejected_names)
						assert(fallback == portraits.choose_part(context, component.part, request))
			else:
				assert(not found.is_empty())
				assert(found.variant_index == index)
				assert(found.display_name == name)
				var file_index: int = component.variant_indices[index] if component.has("variant_indices") else index + 1
				assert(found.resource_path == base + component.file.replace("{index}", "%02d" % file_index))
				assert(ResourceLoader.exists(found.resource_path))
			checks += 1
	print("PORTRAIT_RETIREMENT_PASS: ", checks, " original selections; surviving IDs preserved; retired IDs resolve deterministically")
	var scene = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	var selected_count := 0
	for sex_index in range(2):
		var gender: OptionButton = scene.find_child("GenderChoice", true, false)
		gender.select(sex_index)
		gender.emit_signal("item_selected", sex_index)
		for slot in ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]:
			var choice: OptionButton = scene.find_child("Variant_" + slot, true, false)
			for option in choice.item_count:
				assert(not choice.get_item_text(option) in rejected_names)
				choice.select(option)
				choice.emit_signal("item_selected", option)
				selected_count += 1
			choice.select(0)
			choice.emit_signal("item_selected", 0)
		scene.find_child("RandomizeParts", true, false).emit_signal("pressed")
		scene.find_child("ResetVariants", true, false).emit_signal("pressed")
	scene.find_child("ReferenceFemaleFace", true, false).emit_signal("pressed")
	assert(selected_count == 90)
	print("PORTRAIT_UI_PASS: ", selected_count, " options composed, random/reset/reference exercised")
	scene.queue_free()
	await process_frame
	quit()
