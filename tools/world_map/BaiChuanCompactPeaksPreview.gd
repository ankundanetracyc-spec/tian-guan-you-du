extends "res://tools/world_map/BaiChuanGroundDetailsPreview.gd"
## Actual production scenery in an isolated GPU viewport. The comparison only
## swaps scenery sprites in memory; neither authored data nor saves are edited.
const COMPACT_OUT := "res://output/bai_chuan_compact_peaks_20261109/"
var legacy_scene: Dictionary
var compact_comparisons: Array[Dictionary] = []
var xiyun_runtime_id := ""

func _ready() -> void:
	if not OS.get_environment("APPDATA").replace("\\", "/").contains("bai_chuan_compact_peaks_20261109/"):
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
	legacy_scene = JSON.parse_string(FileAccess.get_file_as_string("res://resources/world/bai_chuan/landscape_expanded3x_20261106.json"))
	_check(map.navigation.ready and map.terrain_travel.ready, "production navigation and terrain ready")
	_check(map.navigation.PROFILE_ID == "compact_peaks_20261109_v1", "new navigation source active")
	_check(map.place_views.size() == 123 and is_equal_approx(map.get_world_size(), 22118.4), "world scale and all places retained")
	_check(map.landscape.metadata.mountains.size() == 161 and map.landscape.metadata.trees.size() == 5490, "actual compact peaks and retained forest loaded")
	_check(map.landscape.metadata.bridges.size() == 9, "all nine bridges retained")
	_check(is_instance_valid(map.landscape.mountain_ground_details) and map.landscape.mountain_ground_details.total == 1029, "native contour ground details loaded")
	xiyun_runtime_id = map.travel.graph.runtime_id_for_node("bc_place_022")
	_check(not xiyun_runtime_id.is_empty() and map.set_initial_runtime_id(xiyun_runtime_id), "actual Xiyun city location resolved")
	map._set_zoom_level(2, Vector2(960, 540))
	_check(is_equal_approx(map.world_zoom, 0.8), "closest camera remains 0.8")
	_position_at(Vector2(12050, 16310))
	await _compare("xiyun")
	# One separate close crop shows the three independently sorted peaks and
	# their true lower alpha edge at the same scale as ordinary play.
	await _capture_detail("xiyun_peak_detail.png", Rect2(11350, 15980, 650, 500))
	for row: Dictionary in map.landscape.metadata.bridges:
		_position_at(Vector2(row.position[0], row.position[1]))
		var filename := str(row.id)
		if filename in ["bc_bridge_007", "bc_bridge_009"]:
			await _compare(filename)
		else:
			await _capture(filename + "_after.png")
		var bounds: Array = row.bounds
		await _capture_detail(filename + "_detail.png", Rect2(bounds[0], bounds[1], bounds[2], bounds[3]).grow(90.0))
		_check(map.landscape.active.has(filename) and map.landscape.active[filename].visible, "actual visible bridge " + filename)
		var drawn: Sprite2D = map.landscape.active[filename]
		_check(is_equal_approx(drawn.scale.x, drawn.scale.y) and drawn.scale.x < 1.0, "native uniform bridge scale " + filename)
		compact_comparisons.append({"name": filename, "camera": map.camera_pos, "rotation": drawn.rotation, "world_size": drawn.texture.get_size() * drawn.scale})
	_position_at(Vector2(5797.1138756, 14416.3521531))
	await _capture("white_stone_after.png")
	_check(map.landscape.grounding_patch.trees == 136, "natural White Stone forest retained")
	for level in [0, 1, 2]:
		var foot: Vector2 = map.player_mark.position
		map._set_zoom_level(level, Vector2(960, 540))
		map.focus_world_point(foot)
		map.landscape.refresh(true)
		map.landscape.update(0.0)
		_check(map.player_mark.position == foot, "camera preserves actor foot " + str(level))
		_check(absf(map.player_mark.scale.y * map.PLAYER_PAWN_TEXTURE.get_height() - 52.0) < 0.001, "same 52 world unit actor " + str(level))
		await _capture("scale_" + str(level) + ".png")
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
	var current: Array[Node2D] = []
	for id: String in map.landscape.active:
		if str(map.landscape.entries[id].kind) in ["mountain", "bridge"]:
			current.append(map.landscape.active[id])
			map.landscape.active[id].hide()
	map.landscape.mountain_ground_details.hide()
	var old_bucket := Node2D.new()
	map.landscape.add_child(old_bucket)
	var visible_rect := Rect2(map.camera_pos, Vector2(1920, 1080) / map.world_zoom).grow(100.0)
	for row: Dictionary in legacy_scene.mountains:
		var bounds: Array = row.bounds
		if not visible_rect.intersects(Rect2(bounds[0], bounds[1], bounds[2], bounds[3])): continue
		var view: Node2D = map.landscape._mountain(row)
		view.material = map.landscape.mountain_material
		view.position = Vector2(row.position[0], row.position[1])
		map.depth_order.place(view, float(row.z))
		old_bucket.add_child(view)
	var bridge_atlas: Texture2D = map.landscape._load_texture(str(legacy_scene.bridge_atlas), true)
	for row: Dictionary in legacy_scene.bridges:
		var bounds: Array = row.bounds
		if not visible_rect.intersects(Rect2(bounds[0], bounds[1], bounds[2], bounds[3])): continue
		var source: Dictionary = legacy_scene.bridge_catalog[str(row.asset)]
		var region: Array = source.region
		var atlas := AtlasTexture.new()
		atlas.atlas = bridge_atlas
		atlas.region = Rect2(region[0], region[1], region[2], region[3])
		atlas.filter_clip = true
		var view := Sprite2D.new()
		view.texture = atlas
		view.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		view.offset = (Vector2(0.5, 0.5) - Vector2(source.pivot[0], source.pivot[1])) * atlas.get_size()
		view.scale = Vector2(float(row.world_width), float(row.world_height)) / atlas.get_size()
		view.position = Vector2(row.position[0], row.position[1])
		view.rotation = float(row.rotation)
		view.z_as_relative = false
		view.z_index = int(row.z)
		old_bucket.add_child(view)
	await _capture(stem + "_before.png")
	old_bucket.hide()
	for view: Node2D in current: view.show()
	map.landscape.mountain_ground_details.show()
	await _capture(stem + "_after.png")
	compact_comparisons.append({"name": stem, "camera": map.camera_pos, "zoom": map.world_zoom, "summary": map.landscape.get_debug_summary()})
	old_bucket.queue_free()
	await _frames(1)

func _capture(filename: String) -> void:
	await _frames(3)
	await RenderingServer.frame_post_draw
	_check(viewport.get_texture().get_image().save_png(COMPACT_OUT + filename) == OK, "actual render " + filename)

func _capture_detail(filename: String, world_rect: Rect2) -> void:
	await _frames(2)
	await RenderingServer.frame_post_draw
	var screen_rect := Rect2((world_rect.position - map.camera_pos) * map.world_zoom, world_rect.size * map.world_zoom)
	var pixels := Rect2i(screen_rect).intersection(Rect2i(0, 0, 1920, 1080))
	_check(pixels.size.x > 0 and pixels.size.y > 0, "detail visible in real viewport " + filename)
	if pixels.has_area():
		_check(viewport.get_texture().get_image().get_region(pixels).save_png(COMPACT_OUT + filename) == OK, "actual detail " + filename)

func _finish() -> void:
	var file := FileAccess.open(COMPACT_OUT + "actual_map_visual_report.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks": checks, "failures": failures, "xiyun_runtime_id": xiyun_runtime_id, "comparisons": compact_comparisons, "world_size": 22118.4, "closest_zoom": 0.8}, "\t") + "\n")
	print("COMPACT_PEAKS_VISUAL_CHECKS ", checks, " FAILURES ", failures.size())
	get_tree().quit(0 if failures.is_empty() else 1)
