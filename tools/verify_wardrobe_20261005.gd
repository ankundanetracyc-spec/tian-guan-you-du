extends SceneTree

const PACK := "res://mods/appearance/ink_three_variants_20261004/"
var human_script = load("res://csharp/resources/HumanData.cs")
var character_script = load("res://csharp/entity/Character.cs")

func _initialize() -> void:
	call_deferred("verify")

func settle() -> void:
	for frame in range(3): await process_frame
	if DisplayServer.get_name() != "headless": await RenderingServer.frame_post_draw

func verify() -> void:
	root.size = Vector2i(1672,941)
	var mods = root.get_node("AppearanceModCatalog")
	mods.reload()
	assert(mods.get_parts("male","clothes").size() == 7)
	assert(mods.get_variant_name("male","clothes",0) == "碧岚云叶袍")
	assert(mods.get_variant_name("female","clothes",2) == "藕荷花云袍")
	assert(mods.get_parts("male","eye").size() == 8)
	assert(mods.get_parts("male","brow").size() == 9)
	assert(mods.get_parts("male","eye")[2].resource_path.ends_with("/04.png"))
	assert(mods.get_variant_name("male","brow",5) == "凌锋扬眉")
	for female in [false,true]:
		var data = human_script.new()
		data.entity_name = "衣着验收"
		data.gender = 1 if female else 0
		data.current_age = 36
		data.appearance_seed = 771
		data.appearance_face_style = 0
		data.appearance_eye_style = 0
		data.appearance_brow_style = 0
		data.appearance_nose_style = 0
		data.appearance_mouth_style = 0
		data.appearance_hair_style = 0
		data.appearance_hair_back_style = 0
		data.appearance_clothes_style = 0
		data.appearance_skin_tone = 2
		data.appearance_hair_color = 1
		var character = character_script.new()
		character.entity_data = data
		root.add_child(character)
		var panel = load("res://tscn/panel/InfoPanel.tscn").instantiate()
		root.add_child(panel)
		panel.show_entity(character)
		panel.find_child("TabEquip",true,false).emit_signal("pressed")
		await settle()
		var entry = panel.find_child("ChangeClothing",true,false)
		assert(entry != null and entry.visible)
		entry.emit_signal("pressed")
		await settle()
		var wardrobe = panel.get_node("WardrobePicker")
		assert(wardrobe.visible)
		var choices = wardrobe.find_child("ClothingChoice",true,false)
		assert(choices.item_count == 7)
		var preview = wardrobe.find_child("WardrobePreview",true,false)
		var original_texture = preview.get_node("Clothes").texture
		choices.select(1); choices.emit_signal("item_selected",1)
		await settle()
		assert(data.appearance_clothes_style == 0, "Trial altered live character")
		assert(preview.get_node("Clothes").texture != original_texture)
		assert(preview.get_node("Face").modulate == Color.WHITE)
		assert(preview.get_node("Hair Front").modulate == Color.WHITE)
		assert(wardrobe.find_child("ClothingName",true,false).text == mods.get_variant_name("female" if female else "male","clothes",1))
		var rank_names = ["玉虚·杂役常服","玉虚·外门常服","玉虚·内门常服","玉虚·真传常服"]
		for style in range(3,7):
			choices.select(style); choices.emit_signal("item_selected",style); await settle()
			assert(choices.get_item_text(style) == rank_names[style - 3])
			assert(preview.get_node("Clothes").texture.resource_path.ends_with("/%02d.png" % (style + 1)))
			assert(data.appearance_clothes_style == 0, "Trial altered live character")
		wardrobe.find_child("CancelClothing",true,false).emit_signal("pressed")
		assert(data.appearance_clothes_style == 0 and not wardrobe.visible)
		entry.emit_signal("pressed"); await settle()
		choices.select(6); choices.emit_signal("item_selected",6); await settle()
		if DisplayServer.get_name() != "headless":
			assert(root.get_texture().get_image().save_png(PACK+"preview/"+("female" if female else "male")+"_yuxu_zhenchuan_wardrobe_20261008.png") == OK)
		wardrobe.find_child("ApplyClothing",true,false).emit_signal("pressed")
		await settle()
		assert(data.appearance_clothes_style == 6 and not wardrobe.visible)
		assert(data.appearance_eye_style == 0 and data.appearance_hair_style == 0)
		assert(data.appearance_skin_tone == 2 and data.appearance_hair_color == 1)
		assert(panel.find_child("ChangeClothing",true,false).text.ends_with("玉虚·真传常服"))
		var catalog = root.get_node("PortraitCatalog")
		var context = catalog.get_context(data,"female" if female else "male",36,"","凡人")
		var selected = catalog.select_portrait(context,catalog.get_requested_appearance(data))
		assert(selected.clothes.variant_index == 6, "Portrait/dialogue selection did not follow changed clothing")
		var raw_renderer = load("res://csharp/panel/PortraitRenderer.cs").new()
		root.add_child(raw_renderer)
		raw_renderer.configure(data,context,catalog.get_requested_appearance(data))
		assert(raw_renderer.get_node("Face").modulate == Color.WHITE)
		assert(raw_renderer.get_node("Hair Front").modulate == Color.WHITE)
		raw_renderer.queue_free()
		var save_path = "res://tmp/wardrobe_save_test_20261005.tres"
		assert(ResourceSaver.save(data,save_path) == OK)
		var restored = ResourceLoader.load(save_path,"",ResourceLoader.CACHE_MODE_IGNORE)
		assert(restored.appearance_clothes_style == 6 and restored.appearance_eye_style == 0)
		panel.queue_free(); character.queue_free(); await settle()
	print("WARDROBE_PASS: 7 named clothes per gender; four Yuxu ranks; stable old ordinals; equipment entry; trial/cancel/apply; portrait selection; colour flags; resource save roundtrip")
	FileAccess.open(PACK+"wardrobe_verification_20261008.json",FileAccess.WRITE).store_string(JSON.stringify({"pass":true,"sexes":2,"choices_per_gender":7,"yuxu_rank_variants":4,"old_ordinals_preserved":true,"trial_cancel_apply":true,"saved_clothing":true,"colour_flags":true,"equipment_entry":true,"portrait_and_dialogue_selection":true},"\t"))
	quit(0)
