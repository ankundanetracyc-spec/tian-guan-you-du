extends SceneTree

func _initialize() -> void:
	_bake.call_deferred()

func _bake() -> void:
	for stem in ["c1_female_puppet_v2", "c1_male_puppet_v1", "c1_male_puppet_v2"]:
		var package: String = "res://textures/characters/" + stem + "/"
		var config = JSON.parse_string(FileAccess.get_file_as_string(package + "alignment.json"))
		var assembled := Image.create(1024, 1536, false, Image.FORMAT_RGBA8)
		var baked_layers: Dictionary = {}
		for part in config.layers:
			var viewport := SubViewport.new()
			viewport.size = Vector2i(1024, 1536)
			viewport.transparent_bg = true
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			root.add_child(viewport)
			for source in config.layers[part]:
				var image := Image.load_from_file(source[0])
				if image == null:
					push_error("Missing puppet art: " + str(source[0]))
					quit(1)
					return
				var sprite := Sprite2D.new()
				sprite.texture = ImageTexture.create_from_image(image)
				sprite.centered = false
				sprite.scale = Vector2(source[1][0], source[1][1])
				sprite.position = Vector2(source[2][0], source[2][1])
				viewport.add_child(sprite)
			await process_frame
			await RenderingServer.frame_post_draw
			var layer := viewport.get_texture().get_image()
			layer.save_png(ProjectSettings.globalize_path(package + "layers/" + part + ".png"))
			baked_layers[part] = layer
			viewport.queue_free()
			await process_frame
		for part in config.order:
			assembled.blend_rect(baked_layers[part], Rect2i(0,0,1024,1536), Vector2i.ZERO)
		assembled.save_png(ProjectSettings.globalize_path(package + "preview/static.png"))
		var preview := Image.create(1024,1536,false,Image.FORMAT_RGBA8)
		preview.fill(Color("e9dfd2"))
		preview.blend_rect(assembled, Rect2i(0,0,1024,1536), Vector2i.ZERO)
		preview.save_png(ProjectSettings.globalize_path(package + "preview/setup.png"))
		print("[colored-puppet] baked ", stem)
	quit()
