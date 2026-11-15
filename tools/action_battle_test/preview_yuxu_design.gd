extends SceneTree

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(1536, 1024)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var background := ColorRect.new()
	background.color = Color("e9e6df")
	background.size = Vector2(1536, 1024)
	viewport.add_child(background)
	var source := Image.new()
	assert(source.load(ProjectSettings.globalize_path("res://art/yuxu_unified_v14/source.png")) == OK)
	var preview := TextureRect.new()
	preview.texture = ImageTexture.create_from_image(source)
	preview.size = Vector2(1536, 1024)
	viewport.add_child(preview)
	await process_frame
	await RenderingServer.frame_post_draw
	assert(viewport.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://art/yuxu_unified_v14/design_preview.png")) == OK)
	quit()
