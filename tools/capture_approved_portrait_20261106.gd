extends SceneTree

const OUTPUT := "res://mods/appearance/portrait_original_restore_20261105_v6/whole_male_first_20261105/integration/installation/"

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	gender.select(0)
	gender.emit_signal("item_selected", 0)
	scene.find_child("BreathingPreview", true, false).button_pressed = false
	await process_frame
	await RenderingServer.frame_post_draw
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	var error := view.texture.get_image().save_png(OUTPUT + "game_male_composite.png")
	if error != OK:
		push_error("Failed to save actual male composite")
		quit(1)
		return
	error = root.get_texture().get_image().save_png(OUTPUT + "game_male_viewport_latest_20261106.png")
	if error != OK:
		push_error("Failed to save actual male viewport")
		quit(1)
		return
	print("APPROVED_MALE_CURRENT_VIEWPORT_SAVED")
	scene.queue_free()
	await process_frame
	quit(0)
