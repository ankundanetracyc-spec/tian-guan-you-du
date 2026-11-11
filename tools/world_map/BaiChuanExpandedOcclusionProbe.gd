extends Node
## Production-map pixel QA in an offscreen SubViewport. The only native window
## is hidden; this never starts Main or saves player progress.
const MapRuntime = preload("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
const NpcVisual = preload("res://gdscripts/world/WorldNpcTraveler.gd")
const VisualScale = preload("res://gdscripts/world/WorldMapVisualScale.gd")
const WorldScale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const OUT := "res://output/bai_chuan_expanded3x_20261106/"
const SIZE := Vector2i(1920, 1080)
var viewport: SubViewport
var map: Control
var npc: AnimatedSprite2D
var checks := 0
var failures: Array[String] = []
var samples: Array[Dictionary] = []
var calibration: Array[Dictionary] = []
var alpha_images: Dictionary = {}
var started := 0
var native_window_hidden := false

class TransformMap extends MapRuntime:
	func _ready() -> void: pass
	func _process(_delta: float) -> void: pass

func _ready() -> void:
	started = Time.get_ticks_msec()
	if not OS.get_environment("APPDATA").replace("\\", "/").contains("bai_chuan_expanded3x_20261106/"):
		push_error("Expanded occlusion QA requires an isolated APPDATA directory.")
		get_tree().quit(2)
		return
	if OS.get_cmdline_user_args().has("--npc-transform-only"):
		_npc_transform_only()
		return
	var window := get_window()
	window.mode = Window.MODE_WINDOWED
	window.size = Vector2i(64, 64)
	window.position = Vector2i(-16000, -16000)
	window.set_flag(Window.FLAG_NO_FOCUS, true)
	native_window_hidden = _hide_native_window()
	_check(native_window_hidden, "only this probe's native window is hidden")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	get_tree().create_timer(90.0).timeout.connect(func():
		_check(false, "offscreen rendering watchdog expired")
		_finish())
	viewport = SubViewport.new()
	viewport.name = "OffscreenProductionMapViewport"
	viewport.size = SIZE
	viewport.world_2d = World2D.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	map = MapRuntime.new()
	map.name = "ActualProductionFreeWorldMap"
	map.size = Vector2(SIZE)
	viewport.add_child(map)
	await _frames(8)
	_check(is_instance_valid(map.landscape) and map.landscape.active.size() > 0, "production map and native scenery load")
	if not is_instance_valid(map.landscape):
		_finish()
		return
	map.process_mode = Node.PROCESS_MODE_DISABLED
	map.player_avatar.update_motion(0.0, Vector2.ZERO)
	_hide_chrome()
	npc = NpcVisual.new()
	npc.name = "ActualNativeNpcSizeReference"
	map.world.add_child(npc)
	_check(npc.configure("qa_occlusion_native_size_reference"), "actual shared native NPC atlas loads")
	npc.scale = Vector2.ONE * VisualScale.NPC_WORLD_HEIGHT / npc.native_height
	npc.animation = "idle_right"
	npc.set_frame_and_progress(0, 0.0)
	npc.pause()
	npc.hide()
	_check(get_tree().get_nodes_in_group("main_scene").is_empty(), "Main is absent")
	var data: Dictionary = map.landscape.metadata
	_check(float(data.world_size) == 22118.4 and data.mountains.size() == 429 and data.trees.size() == 5509, "actual final expanded metadata is loaded")
	for band in range(3):
		for kind: String in ["mountain", "tree"]:
			var entry := _pick_entry(kind, band)
			if not _check(not entry.is_empty(), "dynamic scenery sample exists %s band %d" % [kind, band]): continue
			for level in [2, 1]:
				await _probe(entry, band, level)
	await _size_calibration()
	_check(get_tree().get_nodes_in_group("main_scene").is_empty(), "Main stayed absent")
	_finish()

func _hide_native_window() -> bool:
	if OS.get_name() != "Windows": return false
	# Godot intentionally refuses Window.hide on its main window. Hide this
	# process's exact native handle instead; never enumerate or touch other apps.
	var handle := DisplayServer.window_get_native_handle(DisplayServer.WINDOW_HANDLE)
	var code := """Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class OcclusionProbeWindow { [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c); [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h); [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p); }';
$occlusionWindowHandle = [IntPtr]__HANDLE__;
[uint32]$occlusionWindowOwner = 0;
[void][OcclusionProbeWindow]::GetWindowThreadProcessId($occlusionWindowHandle, [ref]$occlusionWindowOwner);
if ($occlusionWindowOwner -ne __OWNER__) { exit 3 };
[void][OcclusionProbeWindow]::ShowWindow($occlusionWindowHandle, 0);
if ([OcclusionProbeWindow]::IsWindowVisible($occlusionWindowHandle)) { exit 4 };
"""
	code = code.replace("__HANDLE__", str(handle)).replace("__OWNER__", str(OS.get_process_id()))
	var output: Array = []
	var encoded := Marshalls.raw_to_base64(code.to_utf16_buffer())
	var result := OS.execute("powershell.exe", ["-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-EncodedCommand", encoded], output, true, false)
	if result != 0: push_warning("Probe window hiding returned %s: %s" % [result, output])
	return result == 0

func _npc_transform_only() -> void:
	# Supplement the rendered 42/42 and 21/21 silhouettes without using a GPU:
	# exercise the actual NPC scaler and actual landscape depth update as the
	# production camera pans and changes zoom, preserving authored XY/logical Z.
	var subject := TransformMap.new()
	subject.add_to_group("free_world_map_presentation")
	subject.add_to_group("node_world_map_presentation")
	get_tree().root.add_child.call_deferred(subject)
	await get_tree().process_frame
	subject.add_child(subject.world)
	subject.world.add_child(subject.places)
	subject.player_mark = Sprite2D.new()
	subject.player_mark.texture = subject.PLAYER_PAWN_TEXTURE
	subject.world.add_child(subject.player_mark)
	subject.player_halo = subject.PlayerPositionHalo.new()
	subject.world.add_child(subject.player_halo)
	var reference := NpcVisual.new()
	subject.world.add_child(reference)
	_check(reference.configure("qa_camera_transform_npc"), "actual NPC config for headless camera fixture")
	reference.pause()
	reference.position = Vector2(11500, 18500)
	subject.player_mark.position = reference.position - Vector2(60, 0)
	subject.npc_views["qa_camera_transform_npc"] = reference
	var player_position := subject.player_mark.position
	var npc_position := reference.position
	var scenery = subject.LayeredLandscape.new()
	subject.world.add_child(scenery)
	scenery.map = subject
	scenery.metadata = {"max_prop_height": 427.2}
	scenery._elapsed = 0.0
	for zoom: float in [0.4, 0.8]:
		for shift: Vector2 in [Vector2(-500, -300), Vector2(80, -70)]:
			subject.world_zoom = zoom
			subject._world_zoom = zoom
			subject._zoom_level = 1 if zoom == 0.4 else 2
			subject.zoom_level = subject._zoom_level
			subject.camera_pos = npc_position + shift
			subject._update_camera()
			subject._update_player_mark_scale()
			subject._scale_npc(reference)
			scenery.update(0.0)
			_check(reference.position == npc_position and subject.player_mark.position == player_position, "production camera/scalers preserve actor and NPC XY")
			_check(reference.get_meta("logical_z") == npc_position.y and subject.player_mark.get_meta("logical_z") == player_position.y and reference.z_index <= 3880 and reference.z_index < 3900, "production depth conversion preserves NPC/player logical Z in the southern world")
			_check(absf(reference.native_height * reference.scale.y - 52.0) <= 0.001 and absf(reference.native_height * reference.get_global_transform().y.length() - 52.0 * zoom) <= 0.001, "production NPC scaler keeps fixed world and camera-projected height")
	var report := {"checks": checks, "failures": failures, "gpu_used": false, "main_launched": false, "npc_world_position": [npc_position.x, npc_position.y], "zooms": [0.4, 0.8]}
	var file := FileAccess.open(OUT + "npc_camera_transform_report.json", FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(report, "\t"))
	print("NPC_CAMERA_TRANSFORM_%s: %d checks, %d failures" % ["OK" if failures.is_empty() else "FAILED", checks, failures.size()])
	subject.free()
	get_tree().quit(0 if failures.is_empty() else 1)

func _hide_chrome() -> void:
	for child in map.get_children():
		if child is CanvasItem and child != map.world: child.hide()
	map.places.hide()
	map.player_halo.hide()
	map.route_line.hide()
	map.destination_mark.hide()
	map.ink.hide()
	map.population_badges.hide()
	map.landscape.overview.hide()

func _pick_entry(kind: String, band: int) -> Dictionary:
	var target_y := WorldScale.MAP_PIXELS * (float(band) + 0.5) / 3.0
	var best: Dictionary = {}
	var score := INF
	for entry: Dictionary in map.landscape.entries.values():
		if str(entry.kind) != kind: continue
		var y := float(entry.position[1])
		if int(y / (WorldScale.MAP_PIXELS / 3.0)) != band: continue
		var value := absf(y - target_y) + absf(float(entry.position[0]) - WorldScale.MAP_PIXELS * 0.5) * 0.03
		if value < score:
			score = value
			best = entry
	return best

func _set_camera(entry: Dictionary, level: int) -> Sprite2D:
	map._set_zoom_level(level, Vector2(SIZE) * 0.5)
	map.focus_world_point(Vector2(float(entry.position[0]), float(entry.position[1])) - Vector2(0, 25))
	map.landscape.refresh(true)
	map.landscape.update(0.0)
	_hide_chrome()
	for id: String in map.landscape.active:
		map.landscape.active[id].visible = id == str(entry.id)
	return map.landscape.active.get(str(entry.id)) as Sprite2D

func _alpha_at(sprite: Sprite2D, point: Vector2) -> float:
	var key := str(sprite.texture.get_instance_id())
	if not alpha_images.has(key): alpha_images[key] = sprite.texture.get_image()
	var source: Image = alpha_images[key]
	var local := sprite.to_local(map.world.to_global(point))
	var rect := sprite.get_rect()
	var uv := (local - rect.position) / rect.size
	if sprite.flip_h: uv.x = 1.0 - uv.x
	if sprite.flip_v: uv.y = 1.0 - uv.y
	if uv.x < 0.0 or uv.y < 0.0 or uv.x >= 1.0 or uv.y >= 1.0: return 0.0
	return source.get_pixel(clampi(floori(uv.x * source.get_width()), 0, source.get_width() - 1), clampi(floori(uv.y * source.get_height()), 0, source.get_height() - 1)).a

func _behind_point(sprite: Sprite2D, entry: Dictionary) -> Vector2:
	var foot := Vector2(float(entry.position[0]), float(entry.position[1]))
	var width := float(entry.bounds[2])
	var best := foot - Vector2(0, 24)
	var best_score := -1.0
	for dx: float in [0.0, -0.08, 0.08, -0.16, 0.16]:
		for dy: float in [16.0, 24.0, 32.0, 44.0]:
			var point := foot + Vector2(width * dx, -dy)
			var score := 0.0
			for sx: float in [-5.0, 0.0, 5.0]:
				for sy: float in [-44.0, -33.0, -22.0, -11.0]: score += _alpha_at(sprite, point + Vector2(sx, sy))
			if score > best_score:
				best_score = score
				best = point
	return best

func _opaque_core(sprite: Sprite2D, entry: Dictionary) -> Vector2:
	var foot := Vector2(float(entry.position[0]), float(entry.position[1]))
	var height := float(entry.bounds[3])
	for sy: float in [0.45, 0.6, 0.3, 0.75]:
		for sx: float in [0.0, -0.15, 0.15]:
			var point := foot + Vector2(float(entry.bounds[2]) * sx, -height * sy)
			if _alpha_at(sprite, point) > 0.98: return point
	return foot - Vector2(0, height * 0.5)

func _actor_region() -> Rect2i:
	var foot: Vector2 = map.player_mark.get_global_transform_with_canvas().origin
	var zoom := float(map.world_zoom)
	return Rect2i(Vector2i((foot + Vector2(-43, -64) * zoom).floor()), Vector2i((Vector2(86, 75) * zoom).ceil())).intersection(Rect2i(Vector2i.ZERO, SIZE))

func _triplet(point: Vector2, scenery: Sprite2D) -> Dictionary:
	map.player_mark.position = point
	map.player_halo.position = point
	map.landscape.update(0.0)
	var actor_depth: int = map.player_mark.z_index
	var prop_depth: int = scenery.z_index
	map.player_mark.hide()
	var background := await _screen()
	map.player_mark.show()
	var normal := await _screen()
	map.player_mark.z_as_relative = false
	map.player_mark.z_index = VisualScale.FLIGHT_DEPTH - 1
	var front := await _screen()
	var region := _actor_region()
	var visible := _changed(background, front, region)
	var normal_visible := _changed(background, normal, region)
	var front_difference := _changed(normal, front, region)
	map.depth_order.place(map.player_mark, point.y)
	return {"background": background, "normal": normal, "front": front,
		"visible": visible, "normal_visible": normal_visible, "difference": front_difference,
		"actor_depth": actor_depth, "prop_depth": prop_depth, "region": region}

func _probe(entry: Dictionary, band: int, level: int) -> void:
	var scenery := _set_camera(entry, level)
	if not _check(is_instance_valid(scenery), "actual scenery is resident " + str(entry.id)): return
	await _frames(3)
	var source_xyz := Vector3(float(entry.x), float(entry.y), float(entry.z))
	var prop_position := scenery.position
	var point := _behind_point(scenery, entry)
	var back: Dictionary = await _triplet(point, scenery)
	var minimum_visible := 70 if level == 2 else 18
	_check(back.actor_depth < back.prop_depth, "behind foot has lower real render depth %s / %d" % [entry.id, level])
	_check(back.visible >= minimum_visible, "native actor is measurable in forced-front reference %s / %d" % [entry.id, level])
	_check(back.normal_visible < back.visible * 0.68 and back.difference >= minimum_visible * 0.5, "real pixels occlude behind actor %s / %d (%s/%s)" % [entry.id, level, back.normal_visible, back.visible])
	var front_point := Vector2(point.x, float(entry.z) + 10.0)
	var front: Dictionary = await _triplet(front_point, scenery)
	_check(front.actor_depth > front.prop_depth, "front foot has higher real render depth %s / %d" % [entry.id, level])
	_check(front.visible >= minimum_visible and front.normal_visible >= front.visible * 0.95 and front.difference <= 2, "normal front pixels match forced-front reference %s / %d" % [entry.id, level])
	var core := _opaque_core(scenery, entry)
	var core_walkable: bool = map.navigation.is_walkable(core)
	if entry.kind == "mountain": _check(not core_walkable, "opaque mountain core is not walkable " + str(entry.id))
	var player_point: Vector2 = map.player_mark.position
	var player_logical: float = map.player_mark.get_meta("logical_z")
	map._set_zoom_level(1 if level == 2 else 2, Vector2(SIZE) * 0.5)
	map.focus_world_point(Vector2(float(entry.x), float(entry.y)))
	map.landscape.refresh(true)
	map.landscape.update(0.0)
	var changed_view: Node2D = map.landscape.active.get(str(entry.id))
	_check(is_instance_valid(changed_view) and changed_view.position == prop_position and Vector3(float(entry.x), float(entry.y), float(entry.z)) == source_xyz and is_equal_approx(float(changed_view.get_meta("logical_z")), source_xyz.z), "camera keeps prop authored XYZ unchanged " + str(entry.id))
	_check(map.player_mark.position == player_point and map.player_mark.get_meta("logical_z") == player_logical, "camera keeps actor XY/logical Z unchanged " + str(entry.id))
	var label: String = ["north", "middle", "south"][band] + "_" + str(entry.kind) + "_" + ("near" if level == 2 else "middle")
	# Representative files use unaltered full render-target pixels, cropped to
	# the tested prop's screen bounds for easier inspection; no resize/filtering.
	if level == 2 or band == 2:
		var zoom := 0.8 if level == 2 else 0.4
		var foot := Vector2(SIZE) * 0.5 + Vector2(0, 25.0 * zoom)
		var width := maxf(float(entry.bounds[2]) * zoom, 150.0)
		var height := maxf(float(entry.bounds[3]) * zoom, 90.0)
		var crop := Rect2i(Vector2i((foot - Vector2(width * 0.5 + 20.0, height + 20.0)).floor()), Vector2i(width + 40.0, height + 55.0)).intersection(Rect2i(Vector2i.ZERO, SIZE))
		for state: String in ["background", "normal", "front"]:
			(back[state] as Image).get_region(crop).save_png(OUT + label + "_behind_" + state + ".png")
		(front.normal as Image).get_region(crop).save_png(OUT + label + "_foot_front.png")
	var row := {"label": label, "id": entry.id, "kind": entry.kind, "xyz": [entry.x, entry.y, entry.z],
		"zoom": 0.8 if level == 2 else 0.4, "behind_point": [point.x, point.y], "front_point": [front_point.x, front_point.y],
		"behind_normal_pixels": back.normal_visible, "behind_forced_front_pixels": back.visible, "behind_pixel_difference": back.difference,
		"front_normal_pixels": front.normal_visible, "front_forced_front_pixels": front.visible, "front_pixel_difference": front.difference,
		"behind_actor_draw_z": back.actor_depth, "prop_draw_z": back.prop_depth, "front_actor_draw_z": front.actor_depth,
		"mountain_core_walkable": core_walkable if entry.kind == "mountain" else null,
		"actor_point_navigation_walkable": map.navigation.is_walkable(point), "render_only_teleport_fixture": true}
	samples.append(row)
	print("OCCLUSION_SAMPLE ", JSON.stringify(row))

func _size_calibration() -> void:
	var point := Vector2(WorldScale.MAP_PIXELS * 0.5, WorldScale.MAP_PIXELS * 0.5)
	for level in [2, 1]:
		map._set_zoom_level(level, Vector2(SIZE) * 0.5)
		map.focus_world_point(point)
		map.landscape.refresh(true)
		map.landscape.update(0.0)
		for view: Node2D in map.landscape.active.values(): view.hide()
		_hide_chrome()
		map.player_mark.position = point
		npc.position = point
		VisualScale.overlay(map.player_mark, VisualScale.FLIGHT_DEPTH - 1)
		VisualScale.overlay(npc, VisualScale.FLIGHT_DEPTH - 1)
		map.player_mark.hide()
		npc.hide()
		var background := await _screen()
		map.player_mark.show()
		var player := await _screen()
		map.player_mark.hide()
		npc.show()
		var other := await _screen()
		var region := _actor_region()
		var player_box := _difference_box(background, player, region)
		var npc_box := _difference_box(background, other, region)
		var player_world_height: float = map.player_mark.scale.y * map.player_avatar.scale.y * map.player_avatar.native_height
		var npc_world_height: float = npc.scale.y * npc.native_height
		_check(absf(player_world_height - 52.0) <= 0.001 and absf(npc_world_height - 52.0) <= 0.001, "actual player/NPC share 52 world height level " + str(level))
		_check(player_box.size.y > 10 and npc_box.size.y > 10 and absi(player_box.size.y - npc_box.size.y) <= 2, "rendered player/NPC silhouette heights agree level " + str(level))
		calibration.append({"level": level, "zoom": map.world_zoom, "player_world_height": player_world_height, "npc_world_height": npc_world_height,
			"player_pixel_height": player_box.size.y, "npc_pixel_height": npc_box.size.y})
		map.player_mark.position = point - Vector2(45, 0)
		npc.position = point + Vector2(45, 0)
		map.player_mark.show()
		var comparison := await _screen()
		var foot := Vector2(SIZE) * 0.5
		var crop := Rect2i(Vector2i(foot - Vector2(110, 80)), Vector2i(220, 100))
		comparison.get_region(crop).save_png(OUT + "player_npc_same_size_" + str(level) + ".png")
	npc.hide()

func _changed(a: Image, b: Image, rect: Rect2i) -> int:
	var count := 0
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			var av := a.get_pixel(x, y)
			var bv := b.get_pixel(x, y)
			if absf(av.r - bv.r) + absf(av.g - bv.g) + absf(av.b - bv.b) > 0.035: count += 1
	return count

func _difference_box(a: Image, b: Image, rect: Rect2i) -> Rect2i:
	var lo := rect.end
	var hi := rect.position
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			var av := a.get_pixel(x, y)
			var bv := b.get_pixel(x, y)
			if absf(av.r - bv.r) + absf(av.g - bv.g) + absf(av.b - bv.b) <= 0.035: continue
			lo = lo.min(Vector2i(x, y))
			hi = hi.max(Vector2i(x + 1, y + 1))
	return Rect2i(lo, (hi - lo).max(Vector2i.ZERO))

func _screen() -> Image:
	await _frames(2)
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func _frames(count: int) -> void:
	for i in range(count): await get_tree().process_frame

func _check(ok: bool, message: String) -> bool:
	checks += 1
	if ok: return true
	failures.append(message)
	push_error("EXPANDED_OCCLUSION_FAILED: " + message)
	return false

func _finish() -> void:
	var report := {"checks": checks, "failures": failures, "samples": samples, "size_calibration": calibration,
		"elapsed_msec": Time.get_ticks_msec() - started, "viewport": [SIZE.x, SIZE.y],
		"map_class": "BaiChuanFreeWorldRuntime", "main_launched": false, "native_window_hidden": native_window_hidden,
		"landscape_sha256": FileAccess.get_sha256(WorldScale.LANDSCAPE_PATH), "navigation_sha256": FileAccess.get_sha256(WorldScale.NAVIGATION_PATH)}
	var file := FileAccess.open(OUT + "pixel_occlusion_report.json", FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(report, "\t"))
	print("EXPANDED_OCCLUSION_%s: %d checks, %d failures" % ["OK" if failures.is_empty() else "FAILED", checks, failures.size()])
	get_tree().quit(0 if failures.is_empty() else 1)
