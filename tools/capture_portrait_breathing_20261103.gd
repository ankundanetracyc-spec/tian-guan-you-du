extends SceneTree

const OUT := "res://docs/美术规范/立绘呼吸预览_20261103/"
const PACK := "res://mods/appearance/ink_three_variants_20261004/"
var materials: Array[ShaderMaterial] = []

func _initialize() -> void:
	call_deferred("capture")

func portrait(sex: String) -> ImageTexture:
	var config: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(PACK + "appearance_parts.json"))
	var result := Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
	for slot in ["hair_back", "face_shape", "clothing", "eyes", "eyebrows", "nose", "mouth", "face_accessory", "hair_front"]:
		for component in config.components:
			if component.gender != sex or not ("/" + slot + "/") in component.file: continue
			var number: int = component.variant_indices[0] if component.has("variant_indices") else 1
			var source := Image.new()
			assert(source.load_png_from_buffer(FileAccess.get_file_as_bytes(PACK + component.file.replace("{index}", "%02d" % number))) == OK)
			source.convert(Image.FORMAT_RGBA8)
			result.blend_rect(source, Rect2i(0, 0, 1024, 1536), Vector2i.ZERO)
	return ImageTexture.create_from_image(result)

func capture() -> void:
	root.size = Vector2i(1000, 780)
	root.content_scale_size = Vector2i.ZERO
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	root.content_scale_factor = 1.0
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	DirAccess.make_dir_recursive_absolute(OUT + "frames")
	var stage := Control.new()
	root.add_child(stage)
	var paper := ColorRect.new()
	paper.color = Color("e8e4db")
	paper.size = Vector2(1000, 780)
	stage.add_child(paper)
	var font = load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf")
	var title := Label.new()
	title.text = "立绘呼吸预览 · 自然幅度"
	title.position = Vector2(325, 15)
	title.add_theme_font_override("font", font)
	title.add_theme_font_size_override("font_size", 26)
	title.add_theme_color_override("font_color", Color("35423d"))
	stage.add_child(title)
	for index in range(2):
		var view := TextureRect.new()
		view.texture = portrait("male" if index == 0 else "female")
		view.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		view.stretch_mode = TextureRect.STRETCH_SCALE
		view.size = Vector2(470, 705)
		view.position = Vector2(15 + index * 500, 60)
		var material := ShaderMaterial.new()
		material.shader = load("res://shaders/portrait_breathing.gdshader")
		view.material = material
		materials.append(material)
		stage.add_child(view)
	for frame in range(96):
		var amount := 0.5 - 0.5 * cos(TAU * frame / 96.0)
		for material in materials: material.set_shader_parameter("breath", amount)
		await process_frame
		await RenderingServer.frame_post_draw
		var screenshot := root.get_texture().get_image()
		assert(screenshot.save_png(OUT + "frames/%03d.png" % frame) == OK)
	print("BREATHING_CAPTURE_PASS: 96 frames, 4.8 seconds at 20 fps")
	materials.clear()
	stage.queue_free()
	await process_frame
	await process_frame
	quit()
