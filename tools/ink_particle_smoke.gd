extends SceneTree
## Repeatable visual-only pool checks and actual-render world-anchor regression.
## -- --capture-dir=E:/stacklands/output/ink_particle_anchor_20261104
## -- --logic-only skips image capture and works with a headless renderer.

const Layer = preload("res://tools/action_battle_test/ink_particle_layer.gd")
const VIEW_SIZE := Vector2i(960, 540)
var capture_dir := "res://output/ink_particle_anchor_20261104"
var logic_only := false
var checks := 0
var failures := 0


class Backdrop extends Node2D:
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, Vector2(VIEW_SIZE)), Color("eee5d5"))
		for x in [190.0, 480.0, 770.0]:
			draw_circle(Vector2(x, 280), 2.0, Color("b4a392"))


func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		push_error(label)


func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			capture_dir = argument.trim_prefix("--capture-dir=").replace("\\", "/")
		elif argument == "--logic-only":
			logic_only = true
	call_deferred("run")


func run() -> void:
	root.size = VIEW_SIZE
	root.content_scale_size = VIEW_SIZE
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	root.title = "水墨粒子世界锚点核验"
	_run_logic_checks()
	print("INK_PARTICLE_LOGIC_RESULT checks=%d failures=%d" % [checks, failures])
	if not logic_only:
		await _run_render_checks()
	print("INK_PARTICLE_SMOKE_%s checks=%d failures=%d" % ["OK" if failures == 0 else "FAIL", checks, failures])
	quit(0 if failures == 0 else 1)


func _run_logic_checks() -> void:
	var layer = Layer.new()
	layer.max_particles = 12
	root.add_child(layer)
	check(layer.get_child_count() == 0, "pool must not create nodes")
	check(layer.burst(Vector2(20, 30), Vector2(0.3, -0.8), "fleck", {"count": 30}) == 12, "burst is bounded")
	check(layer.active_count() == 12 and layer._pool.size() == 12, "capacity enforced")
	var original = layer._pool.duplicate(true)
	layer.clear_visuals()
	check(layer.active_count() == 0, "clear removes live samples")
	layer.burst(Vector2(20, 30), Vector2(0.3, -0.8), "fleck", {"count": 30})
	check(original == layer._pool, "same fixed seed repeats exactly")
	layer.position = Vector2(200, -150)
	layer.rotation = 1.5
	check(original == layer._pool, "moving emitter does not mutate emitted world state")
	layer.advance(0.0)
	check(original == layer._pool, "zero clock increment freezes particles")
	layer.advance(0.25)
	check(layer.active_count() == 12, "explicit clock advances live samples")
	check(layer._pool[0].origin == original[0].origin, "emission origins remain fixed")
	layer.advance(3.0)
	check(layer.active_count() == 0, "all particles expire")
	layer.max_particles = 6
	layer.trail_emit(Vector2(1, 2), Vector2.DOWN, "ground", {"count": 20})
	check(layer._pool.size() == 6 and layer.active_count() == 6, "pool resizing stays bounded")
	layer.stop()
	check(layer.burst(Vector2.ZERO) == 0, "stop prevents new emission")
	layer.advance(3.0)
	check(layer.active_count() == 0, "stop allows old samples to finish")
	check(layer.play(Vector2.ZERO, Vector2.RIGHT, "mist") == 4, "play resumes after stop")
	layer.stop(true)
	check(layer.active_count() == 0, "stop clear cancels samples")
	check(layer.get_child_count() == 0, "no particle nodes leak")
	layer.free()


func _run_render_checks() -> void:
	var directory_error := DirAccess.make_dir_recursive_absolute(capture_dir)
	check(directory_error == OK, "capture directory can be created")
	if directory_error != OK:
		return
	var stage := Node2D.new()
	root.add_child(stage)
	current_scene = stage
	stage.add_child(Backdrop.new())
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei", "Noto Sans CJK SC"])
	_add_label(stage, font, "水墨粒子 · 世界锚点", Vector2(28, 22), 25)
	_add_label(stage, font, "贴地墨点", Vector2(142, 390), 19)
	_add_label(stage, font, "碎墨", Vector2(460, 390), 19)
	_add_label(stage, font, "淡雾", Vector2(750, 390), 19)
	var parent := Node2D.new()
	stage.add_child(parent)
	var layer = Layer.new()
	layer.max_particles = 48
	layer.auto_advance = false
	parent.add_child(layer)
	var empty: Image = await _capture("empty.png")
	if empty == null:
		return
	var profiles := ["ground", "fleck", "mist"]
	var emitted := 0
	for index in range(profiles.size()):
		emitted += layer.burst(Vector2(190 + index * 290, 280), Vector2(0.7, -0.2), profiles[index],
			{"count": 8, "size": 28.0 if index < 2 else 55.0})
	layer.advance(0.25)
	var frozen_clock: float = layer.clock
	var active_before: int = layer.active_count()
	var frozen_pool = layer._pool.duplicate(true)
	check(emitted == 24 and active_before == 24, "all three profiles produce live ink")
	var image_a: Image = await _capture("anchor_a.png")
	if image_a == null:
		return
	for index in range(profiles.size()):
		var area := Rect2i(75 + index * 290, 170, 230, 180)
		check(_different_pixels(empty, image_a, area) > 0, "rendered profile has ink: " + profiles[index])
	parent.position = Vector2(173, -95)
	parent.rotation = 0.73
	parent.scale = Vector2(1.70, 0.61)
	# No advance() call: retain precisely the same particle clock and world state.
	var image_b: Image = await _capture("anchor_b_parent_transformed.png")
	if image_b == null:
		return
	check(is_equal_approx(layer.clock, frozen_clock), "parent transformation does not advance clock")
	check(layer.active_count() == active_before, "parent transformation preserves active count")
	check(layer._pool == frozen_pool, "parent transformation preserves all world sample data")
	var comparison := _compare_images(image_a, image_b)
	check(comparison.max_channel_difference <= 2, "rendered ink moved with parent: " + str(comparison))
	print("INK_PARTICLE_WORLD_ANCHOR_RENDER %s active_before=%d active_after=%d" % [comparison, active_before, layer.active_count()])
	layer.stop(true)
	check(layer.active_count() == 0, "stop(true) clears all rendered profiles")
	check(layer.get_child_count() == 0, "render path creates no particle nodes")
	var cleared: Image = await _capture("cleared.png")
	if cleared == null:
		return
	var clear_comparison := _compare_images(empty, cleared)
	check(clear_comparison.max_channel_difference <= 2, "stop(true) leaves rendered residue: " + str(clear_comparison))
	print("INK_PARTICLE_RENDER_CLEAR %s" % clear_comparison)


func _add_label(stage: Node2D, font: Font, text_value: String, at: Vector2, size: int) -> void:
	var label := Label.new()
	label.position = at
	label.text = text_value
	label.add_theme_font_override("font", font)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", Color("66443d"))
	stage.add_child(label)


func _capture(filename: String) -> Image:
	for frame in range(3):
		await process_frame
	await RenderingServer.frame_post_draw
	var shot := root.get_texture().get_image()
	check(shot != null and not shot.is_empty(), "real rendering frame is available: " + filename)
	if shot == null or shot.is_empty():
		return null
	shot.convert(Image.FORMAT_RGBA8)
	check(shot.save_png(capture_dir.path_join(filename)) == OK, "rendered PNG saved: " + filename)
	return shot


func _different_pixels(before: Image, after: Image, area: Rect2i) -> int:
	var count := 0
	for y in range(area.position.y, area.end.y):
		for x in range(area.position.x, area.end.x):
			if before.get_pixel(x, y) != after.get_pixel(x, y):
				count += 1
	return count


func _compare_images(before: Image, after: Image) -> Dictionary:
	var data_a := before.get_data()
	var data_b := after.get_data()
	var maximum := 0
	var differing_pixels := 0
	var bad_min := Vector2i(before.get_width(), before.get_height())
	var bad_max := Vector2i(-1, -1)
	for offset in range(0, data_a.size(), 4):
		var pixel_max := 0
		for channel in range(4):
			pixel_max = maxi(pixel_max, absi(int(data_a[offset + channel]) - int(data_b[offset + channel])))
		maximum = maxi(maximum, pixel_max)
		if pixel_max > 0:
			differing_pixels += 1
		if pixel_max > 2:
			var index := offset / 4
			var point := Vector2i(index % before.get_width(), index / before.get_width())
			bad_min = Vector2i(mini(bad_min.x, point.x), mini(bad_min.y, point.y))
			bad_max = Vector2i(maxi(bad_max.x, point.x), maxi(bad_max.y, point.y))
	return {"max_channel_difference": maximum, "differing_pixels": differing_pixels,
		"pixels_over_tolerance_bbox": [bad_min, bad_max] if bad_max.x >= 0 else []}
