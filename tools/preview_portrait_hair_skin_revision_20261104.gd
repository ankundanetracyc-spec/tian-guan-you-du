extends SceneTree

func _initialize() -> void:
	call_deferred("open_preview")

func open_preview() -> void:
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	for part in ["hair_front", "hair_back", "eyebrows", "eyes"]:
		var choice: OptionButton = scene.find_child("Variant_" + part, true, false)
		choice.select(1)
		choice.emit_signal("item_selected", 1)
	var clothing: OptionButton = scene.find_child("Variant_clothing", true, false)
	clothing.select(6)
	clothing.emit_signal("item_selected", 6)
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://mods/appearance/portrait_hair_skin_revision_20261104_v1/installation/game_style02_viewport.png")
