extends SceneTree

func _initialize() -> void:
	call_deferred("_verify")

func _verify() -> void:
	var pawn_texture := load("res://textures/ui/world/player_pawn_marker_v6.tres") as Texture2D
	var pawn := pawn_texture.get_image()
	assert(pawn != null and not pawn.is_empty(), "Pawn missing")
	var used := pawn.get_used_rect()
	pawn = pawn.get_region(used)
	used = pawn.get_used_rect()
	assert(used.position.y == 0 and used.end.y == pawn.get_height(), "Pawn has vertical transparent padding")
	assert(pawn.has_mipmaps() == false)
	assert(pawn.get_pixel(0, 0).a < 0.01, "Pawn background is opaque")
	var map_canvas := Node2D.new()
	root.add_child(map_canvas)
	var terrain := TileMapLayer.new()
	var tiles := TileSet.new()
	tiles.tile_size = Vector2i(256, 256)
	terrain.tile_set = tiles
	map_canvas.add_child(terrain)
	var route := load("res://gdscripts/world/WorldMapFootprintRoute.gd").new() as Node2D
	map_canvas.add_child(route)
	var ink := Image.create(28, 58, false, Image.FORMAT_RGBA8)
	ink.fill(Color.WHITE)
	route.configure(terrain, ImageTexture.create_from_image(ink))
	var cells: Array[Vector2i] = [Vector2i.ZERO, Vector2i(1, 0), Vector2i(2, 0)]
	route.set_route(cells)
	var marker := Sprite2D.new()
	marker.texture = ImageTexture.create_from_image(pawn)
	var marker_height := 128.0
	marker.scale = Vector2.ONE * marker_height / pawn.get_height()
	marker.offset = Vector2(0, -pawn.get_height() * 0.5)
	marker.position = terrain.map_to_local(Vector2i.ZERO)
	map_canvas.add_child(marker)
	for zoom: float in [0.02, 0.25, 1.0, 2.5]:
		map_canvas.scale = Vector2.ONE * zoom
		map_canvas.position = Vector2(310, 230)
		route.refresh_view()
		await process_frame
		assert(not route.is_set_as_top_level(), "Route must inherit map scaling")
		assert(route.global_transform.get_scale().is_equal_approx(Vector2.ONE * zoom), "Footprint/map ratio changes with zoom")
		var print_screen_size := Vector2(28, 58) * route.global_transform.get_scale()
		assert((print_screen_size / (256.0 * zoom)).is_equal_approx(Vector2(28, 58) / 256.0), "Footprint/cell ratio is not fixed")
		var center := terrain.to_global(terrain.map_to_local(Vector2i.ZERO))
		var foot := marker.to_global(Vector2(0, pawn.get_height() * 0.5) + marker.offset)
		var head := marker.to_global(Vector2(0, -pawn.get_height() * 0.5) + marker.offset)
		assert(foot.is_equal_approx(center), "Character hem moved off cell center")
		assert(head.is_equal_approx(center - Vector2(0, 128 * zoom)), "Pawn head does not meet cell top edge")
		print("PASS zoom=", zoom, " footprint_screen=", print_screen_size, " footprint_map=28x58 pawn_height=", 128 * zoom)
	map_canvas.queue_free()
	await process_frame
	print("WORLD_MARKERS_PASS pawn=", pawn.get_size(), " used=", used)
	quit()
