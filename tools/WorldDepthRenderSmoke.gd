extends SceneTree

func _initialize() -> void:
	call_deferred("verify_render")

func solid_texture(color: Color) -> Texture2D:
	var image := Image.create(1, 1, false, Image.FORMAT_RGBA8)
	image.fill(color)
	return ImageTexture.create_from_image(image)

func figure(texture: Texture2D, feet: Vector2, dimensions: Vector2) -> Sprite2D:
	var sprite := Sprite2D.new()
	sprite.centered = false
	sprite.texture = texture
	sprite.position = feet
	sprite.scale = dimensions
	sprite.offset = Vector2(-0.5, -1.0)
	return sprite

func verify_render() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(160, 160)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var depth := Node2D.new()
	depth.y_sort_enabled = true
	depth.z_index = 6
	viewport.add_child(depth)
	var branch := Node2D.new()
	branch.y_sort_enabled = true
	depth.add_child(branch)
	var green := solid_texture(Color.GREEN)
	branch.add_child(figure(green, Vector2(80, 90), Vector2(60, 80)))
	var player := figure(solid_texture(Color.RED), Vector2(70, 80), Vector2(20, 40))
	depth.add_child(player)
	var fog_image := Image.create(2, 2, false, Image.FORMAT_RGBA8)
	fog_image.fill(Color.TRANSPARENT)
	fog_image.set_pixel(1, 0, Color.BLUE)
	fog_image.set_pixel(1, 1, Color.BLUE)
	var fog_texture := ImageTexture.create_from_image(fog_image)
	var fog := Sprite2D.new()
	fog.centered = false
	fog.texture = fog_texture
	fog.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	fog.scale = Vector2(80, 80)
	fog.z_index = 10
	viewport.add_child(fog)
	var memory := figure(green, Vector2(80, 90), Vector2(60, 80))
	memory.z_index = 13
	var material := ShaderMaterial.new()
	material.shader = load("res://resources/shaders/world_known_illustration_fog.gdshader")
	material.set_shader_parameter("world_fog", fog_texture)
	material.set_shader_parameter("map_size", Vector2(160, 160))
	memory.material = material
	viewport.add_child(memory)
	await process_frame
	await RenderingServer.frame_post_draw
	var behind := viewport.get_texture().get_image()
	if behind.get_pixel(70, 60).g < 0.9:
		fail("角色在地物后侧时没有被遮住")
		return
	player.position.y = 110
	await process_frame
	await RenderingServer.frame_post_draw
	var front := viewport.get_texture().get_image()
	if front.get_pixel(70, 80).r < 0.9:
		fail("角色在前侧时仍被地物或上层已知插画覆盖")
		return
	if front.get_pixel(90, 60).g < 0.9 or front.get_pixel(130, 60).b < 0.9:
		fail("迷雾中的已知插画显示或普通迷雾被改变")
		return
	print("WORLD_DEPTH_RENDER PASS: nested ground Y-sort behind/front, clear-region player preserved, known artwork restored only through fog")
	viewport.queue_free()
	finish_success.call_deferred()

func finish_success() -> void:
	# CursorSetup installs a process-wide 48px ImageTexture; release it while the
	# renderer is alive so the isolated check also shuts down without leaked RIDs.
	Input.set_custom_mouse_cursor(null)
	await process_frame
	await RenderingServer.frame_post_draw
	quit()

func fail(message: String) -> void:
	push_error(message)
	quit(1)
