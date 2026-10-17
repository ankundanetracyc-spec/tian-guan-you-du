extends SceneTree

var pack_root := "res://textures/vfx/ink_chop_sequence_20261015_v1/"
const SIDE := 768
const FPS := 30.0

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--pack-root="):
			pack_root = argument.trim_prefix("--pack-root=").trim_suffix("/") + "/"
	call_deferred("_bake")

func _bake() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(SIDE, SIDE)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var rect := ColorRect.new()
	rect.size = Vector2(SIDE, SIDE)
	var material := ShaderMaterial.new()
	material.shader = load(pack_root + "bake.gdshader")
	rect.material = material
	viewport.add_child(rect)
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	var records: Array[Dictionary] = []
	for kind in ["slash", "impact"]:
		var count := 16 if kind == "slash" else 20
		var source := "sweep" if kind == "slash" else "impact"
		var image := Image.load_from_file(ProjectSettings.globalize_path(pack_root + "source/" + source + ".png"))
		material.set_shader_parameter("brush", ImageTexture.create_from_image(image))
		material.set_shader_parameter("impact", kind == "impact")
		frames.add_animation(kind)
		frames.set_animation_speed(kind, FPS)
		frames.set_animation_loop(kind, false)
		var images: Array[Image] = []
		for index in range(count):
			material.set_shader_parameter("progress", float(index) / float(count - 1))
			await process_frame
			await RenderingServer.frame_post_draw
			var output := viewport.get_texture().get_image()
			output.convert(Image.FORMAT_RGBA8)
			var filename := "frames/%s_%02d.png" % [kind, index]
			assert(output.save_png(pack_root + filename) == OK)
			images.append(output)
			records.append({"animation":kind,"frame":index,"file":filename,"size":[SIDE,SIDE]})
		for page in range(ceili(float(count) / 16.0)):
			var page_count := mini(16, count - page * 16)
			var atlas_image := Image.create(SIDE * 4, SIDE * ceili(float(page_count) / 4.0), false, Image.FORMAT_RGBA8)
			for tile in range(page_count):
				atlas_image.blit_rect(images[page * 16 + tile], Rect2i(0,0,SIDE,SIDE), Vector2i(tile % 4, tile / 4) * SIDE)
			var atlas_path := pack_root + "%s_atlas_%d.png" % [kind,page]
			assert(atlas_image.save_png(atlas_path) == OK)
			# External texture path is assigned through a text resource after import.
		var file := FileAccess.open(pack_root + "bake_manifest.json", FileAccess.WRITE)
		file.store_string(JSON.stringify({"frame_size":[SIDE,SIDE],"fps":FPS,"frames":records,"tool":"Godot SubViewport; generated organic ink textures; deterministic reveal, breakup and dissolve"}, "\t"))
	_write_resource()
	print("INK_SEQUENCE_BAKE_PASS: 16 slash + 20 impact RGBA frames, 3 atlas pages")
	quit()

func _write_resource() -> void:
	var lines: Array[String] = ['[gd_resource type="SpriteFrames" load_steps=40 format=3]', ""]
	for entry in [["slash",0],["impact",0],["impact",1]]:
		var id := "%s%d" % [entry[0],entry[1]]
		lines.append('[ext_resource type="Texture2D" path="%s%s_atlas_%d.png" id="%s"]' % [pack_root,entry[0],entry[1],id])
	for kind in ["slash","impact"]:
		var count := 16 if kind == "slash" else 20
		for index in range(count):
			var tile := index % 16
			lines.append('\n[sub_resource type="AtlasTexture" id="%s_%d"]' % [kind,index])
			lines.append('atlas = ExtResource("%s%d")' % [kind,index / 16])
			lines.append('region = Rect2(%d, %d, %d, %d)' % [tile % 4 * SIDE,tile / 4 * SIDE,SIDE,SIDE])
			lines.append('filter_clip = true')
	lines.append('\n[resource]\nanimations = [')
	for kind in ["slash","impact"]:
		var count := 16 if kind == "slash" else 20
		lines.append('{"frames": [')
		for index in range(count):
			lines.append('{"duration": 1.0, "texture": SubResource("%s_%d")},' % [kind,index])
		lines.append('], "loop": false, "name": &"%s", "speed": 30.0},' % kind)
	lines.append(']')
	var resource_file := FileAccess.open(pack_root + "chop_effects.tres", FileAccess.WRITE)
	resource_file.store_string("\n".join(lines))
