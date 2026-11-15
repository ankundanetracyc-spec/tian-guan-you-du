extends SceneTree

func _initialize() -> void:
	call_deferred("verify")

func verify() -> void:
	var scene = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	var toggle: CheckButton = scene.find_child("BreathingPreview", true, false)
	var view: TextureRect = scene.find_child("PortraitPreview", true, false)
	var material: ShaderMaterial = view.material
	assert(toggle.button_pressed)
	await create_timer(0.15).timeout
	assert(float(material.get_shader_parameter("breath")) > 0.0)
	toggle.button_pressed = false
	await process_frame
	assert(float(material.get_shader_parameter("breath")) == 0.0)
	toggle.button_pressed = true
	var anchor: CheckButton = scene.find_child("AnchorGuide", true, false)
	anchor.button_pressed = true
	await create_timer(0.1).timeout
	assert(float(material.get_shader_parameter("breath")) == 0.0)
	anchor.button_pressed = false
	var gender: OptionButton = scene.find_child("GenderChoice", true, false)
	gender.select(1)
	gender.emit_signal("item_selected", 1)
	await create_timer(0.1).timeout
	assert(float(material.get_shader_parameter("breath")) > 0.0)
	assert(view.texture != null)
	print("BREATHING_UI_PASS: animation, off/on, static anchors, female switching")
	scene.queue_free()
	await process_frame
	quit()
