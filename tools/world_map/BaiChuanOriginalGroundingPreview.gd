extends "res://tools/world_map/BaiChuanGroundDetailsPreview.gd"
## In-memory contact treatment A/B. Both sides use the original complete
## mountain geometry and the retained top-down bridges in production runtime.
const ORIGINAL_OUT := "res://output/bai_chuan_original_grounding_20261109/"
var grounding_comparisons: Array[Dictionary] = []

func _ready() -> void:
	if not OS.get_environment("APPDATA").replace("\\", "/").contains("bai_chuan_original_grounding_20261109/"):
		get_tree().quit(2)
		return
	get_window().size = Vector2i(64, 64)
	get_window().position = Vector2i(-16000, -16000)
	get_window().set_flag(Window.FLAG_NO_FOCUS, true)
	_check(_hide_native_window(), "only this preview window is hidden")
	viewport = SubViewport.new()
	viewport.size = Vector2i(1920, 1080)
	viewport.world_2d = World2D.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	map = MapRuntime.new()
	map.size = Vector2(1920, 1080)
	viewport.add_child(map)
	await _frames(6)
	map.set_process(false)
	_check(map.navigation.ready and map.terrain_travel.ready, "production navigation and terrain ready")
	_check(map.navigation.PROFILE_ID == "expanded3x_20261106_v1", "original complete mountain navigation restored")
	_check(map.landscape.metadata.mountains.size() == 429, "all original 429 mountains restored")
	_check(map.landscape.metadata.trees.size() == 5490 and map.landscape.grounding_patch.trees == 136, "existing natural forest retained")
	_check(map.place_views.size() == 123 and is_equal_approx(map.get_world_size(), 22118.4), "all places and world scale retained")
	_check(is_instance_valid(map.landscape.mountain_ground_details), "independent original mountain foot treatment loaded")
	_check(map.landscape.metadata.bridges.size() == 9 and map.landscape.metadata.bridge_catalog.has("wood_plan"), "all nine top-down bridges retained")
	map._set_zoom_level(2, Vector2(960, 540))
	for sample: Dictionary in [
		{"name": "xiyun", "point": Vector2(12050, 16310)},
		{"name": "white_stone_foothill", "point": Vector2(5000, 14350)},
		{"name": "river_mountains", "point": Vector2(16109.5563463, 12676.7818990)},
	]:
		_position_at(sample.point)
		await _compare(str(sample.name))
		if str(sample.name) == "xiyun":
			await _capture_detail("xiyun_foot_detail.png", Rect2(11200, 16260, 1400, 230))
	_position_at(Vector2(8003.6827530, 18955.2728359))
	await _capture("vertical_bridge_after.png")
	_position_at(Vector2(5797.1138756, 14416.3521531))
	await _capture("white_stone_forest_after.png")
	for level in [0, 1, 2]:
		var foot: Vector2 = map.player_mark.position
		map._set_zoom_level(level, Vector2(960, 540))
		map.focus_world_point(foot)
		map.landscape.refresh(true)
		map.landscape.update(0.0)
		_check(map.player_mark.position == foot, "camera preserves world foot " + str(level))
		_check(absf(map.player_mark.scale.y * map.PLAYER_PAWN_TEXTURE.get_height() - 52.0) < 0.001, "fixed 52 world unit actor " + str(level))
	_finish()

func _position_at(point: Vector2) -> void:
	var landing: Vector2 = map.navigation.nearest_walkable(point)
	map.player_mark.position = landing
	map.player_halo.position = landing
	map.focus_world_point(point)
	map._update_context()
	map._layout_location_controls()
	map.landscape.refresh(true)
	map.landscape.update(0.0)

func _compare(stem: String) -> void:
	var materials := {}
	for id: String in map.landscape.active:
		if str(map.landscape.entries[id].kind) != "mountain": continue
		materials[id] = map.landscape.active[id].material
		map.landscape.active[id].material = map.landscape.mountain_material
	map.landscape.mountain_ground_details.hide()
	await _capture(stem + "_before.png")
	for id: String in materials: map.landscape.active[id].material = materials[id]
	map.landscape.mountain_ground_details.show()
	await _capture(stem + "_after.png")
	_check(not materials.is_empty(), "complete original mountains visible " + stem)
	grounding_comparisons.append({"name": stem, "camera": map.camera_pos, "zoom": map.world_zoom, "visible_mountains": materials.size(), "summary": map.landscape.get_debug_summary()})

func _capture(filename: String) -> void:
	await _frames(3)
	await RenderingServer.frame_post_draw
	_check(viewport.get_texture().get_image().save_png(ORIGINAL_OUT + filename) == OK, "actual render " + filename)

func _capture_detail(filename: String, world_rect: Rect2) -> void:
	await _frames(2)
	await RenderingServer.frame_post_draw
	var screen_rect := Rect2((world_rect.position - map.camera_pos) * map.world_zoom, world_rect.size * map.world_zoom)
	var pixels := Rect2i(screen_rect).intersection(Rect2i(0, 0, 1920, 1080))
	_check(pixels.has_area(), "detail visible in real viewport " + filename)
	if pixels.has_area():
		_check(viewport.get_texture().get_image().get_region(pixels).save_png(ORIGINAL_OUT + filename) == OK, "actual detail " + filename)

func _finish() -> void:
	var file := FileAccess.open(ORIGINAL_OUT + "actual_map_visual_report.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks": checks, "failures": failures, "comparisons": grounding_comparisons, "world_size": 22118.4, "closest_zoom": 0.8}, "\t") + "\n")
	print("ORIGINAL_GROUNDING_VISUAL_CHECKS ", checks, " FAILURES ", failures.size())
	get_tree().quit(0 if failures.is_empty() else 1)
