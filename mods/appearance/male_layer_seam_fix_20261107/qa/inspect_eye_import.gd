extends SceneTree

const OUTPUT := "res://mods/appearance/male_layer_seam_fix_20261107/qa/"
const EYE := "res://mods/appearance/ink_three_variants_20261004/male/eyes/04.png"

func _initialize() -> void:
	call_deferred("inspect")

func inspect() -> void:
	var raw := Image.new()
	if raw.load_png_from_buffer(FileAccess.get_file_as_bytes(EYE)) != OK:
		quit(2)
		return
	raw.convert(Image.FORMAT_RGBA8)
	var texture: Texture2D = ResourceLoader.load(EYE, "Texture2D", ResourceLoader.CACHE_MODE_IGNORE)
	var imported := texture.get_image()
	imported.convert(Image.FORMAT_RGBA8)
	imported.save_png(OUTPUT + "eye04_imported_texture.png")
	var renderer: Control = load("res://csharp/panel/PortraitRenderer.cs").new()
	renderer.size = Vector2(1024, 1536)
	root.add_child(renderer)
	var data: Resource = load("res://csharp/resources/HumanData.cs").new()
	data.set("gender", 0)
	data.set("appearance_skin_tone", 4)
	data.set("appearance_hair_color", 4)
	var catalog := root.get_node("PortraitCatalog")
	var context: Dictionary = catalog.call("get_context", data, "male", 40, "", "")
	renderer.call("configure", data, context, {"hair_front": "3", "hair_back": "3", "brow": "3", "eye": "3", "face": "0", "clothes": "0", "nose": "0", "mouth": "0", "beard": "0"})
	var composition: Control = renderer.find_child("PortraitComposition", true, false)
	var layers: Array[Dictionary] = []
	for child: TextureRect in composition.get_children():
		layers.append({"name": str(child.name), "z_index": child.z_index,
			"texture": child.texture.resource_path if child.texture != null else "",
			"position": [child.position.x, child.position.y], "scale": [child.scale.x, child.scale.y],
			"modulate": child.modulate.to_html(), "self_modulate": child.self_modulate.to_html(),
			"material": str(child.material), "use_parent_material": child.use_parent_material,
			"texture_filter": int(child.texture_filter)})
	var report := {"source": EYE, "raw_sha256": FileAccess.get_sha256(EYE),
		"imported_image": OUTPUT + "eye04_imported_texture.png", "layers": layers,
		"scope": "One ResourceLoader read and isolated formal renderer configuration only; no GUI, runtime suite or production writes.",
		"canvas_scale": [composition.scale.x, composition.scale.y],
		"canvas_position": [composition.position.x, composition.position.y],
		"renderer_modulate": renderer.modulate.to_html(), "renderer_material": str(renderer.material)}
	var output := FileAccess.open(OUTPUT + "engine_layer_properties.json", FileAccess.WRITE)
	output.store_string(JSON.stringify(report, "  "))
	output.close()
	renderer.queue_free()
	await process_frame
	print("Single eye ResourceLoader and layer inspection complete")
	quit(0)
