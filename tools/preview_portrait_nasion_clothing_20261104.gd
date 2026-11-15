extends SceneTree

const OUTPUT := "res://mods/appearance/portrait_nasion_clothing_20261104_v4/installation/"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]


func _initialize() -> void:
	call_deferred("open_preview")


func show_gender(scene: Node, female: bool) -> void:
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	gender.select(1 if female else 0)
	gender.emit_signal("item_selected", 1 if female else 0)
	for part in PARTS:
		var choice: OptionButton = scene.find_child("Variant_" + part, true, false)
		choice.select(0)
		choice.emit_signal("item_selected", 0)


func capture_gender(scene: Node, female: bool) -> void:
	show_gender(scene, female)
	await process_frame
	var name := "female" if female else "male"
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	print("NASION_CLOTHING_PREVIEW_COMPOSITE_", name, ": ", view.texture.get_image().save_png(OUTPUT + "preview_" + name + "_composite.png"))
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		print("NASION_CLOTHING_PREVIEW_CAPTURE_", name, ": ", root.get_texture().get_image().save_png(OUTPUT + "preview_" + name + "_viewport.png"))


func open_preview() -> void:
	var scene: Control = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	var breathing: CheckButton = scene.find_child("BreathingPreview", true, false)
	breathing.button_pressed = false
	await capture_gender(scene, false)
	await capture_gender(scene, true)
	show_gender(scene, false)
	breathing.button_pressed = true
