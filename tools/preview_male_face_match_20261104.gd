extends SceneTree

const OUTPUT := "res://mods/appearance/portrait_male_face_match_20261104_v2/installation/"


func _initialize() -> void:
	call_deferred("open_preview")


func open_preview() -> void:
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	gender.select(0)
	gender.emit_signal("item_selected", 0)
	for part in ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]:
		var choice: OptionButton = scene.find_child("Variant_" + part, true, false)
		choice.select(0)
		choice.emit_signal("item_selected", 0)
	for part in ["hair_front", "hair_back", "eyebrows"]:
		var choice: OptionButton = scene.find_child("Variant_" + part, true, false)
		choice.select(2)
		choice.emit_signal("item_selected", 2)
	var eyes: OptionButton = scene.find_child("Variant_eyes", true, false)
	eyes.select(1)
	eyes.emit_signal("item_selected", 1)
	await process_frame
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		var capture := root.get_texture().get_image()
		var result := capture.save_png(OUTPUT + "game_male_viewport.png")
		print("MALE_FACE_MATCH_PREVIEW_CAPTURE: ", result)
