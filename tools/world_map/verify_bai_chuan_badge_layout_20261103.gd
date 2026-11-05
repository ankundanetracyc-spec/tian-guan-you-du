extends SceneTree

var failures: Array[String] = []
var layouts := 0
var badges_checked := 0

func _initialize() -> void:
	_run.call_deferred()

func frames(count := 3) -> void:
	for i in count: await process_frame

func verify(map: Control, context: String) -> void:
	var badges = map.population_badges
	var to_badges: Transform2D = badges.get_global_transform_with_canvas().affine_inverse()
	var labels: Array[Rect2] = []
	var icons: Array[Rect2] = []
	if is_instance_valid(map.far_city_hover_hint) and map.far_city_hover_hint.visible:
		labels.append(to_badges * map.far_city_hover_hint.get_global_rect())
	for id: String in map._visible_place_ids:
		var marker: Control = map.place_views[id]
		var nameplate: Control = marker.get_node("SelectPlace")
		if nameplate.visible: labels.append(to_badges * nameplate.get_global_rect())
		icons.append(to_badges * marker.get_node("SelectIcon").get_global_rect())
	var placed: Array[Rect2] = []
	for id: String in badges.visible_node_ids:
		var count := int(badges.counts.get(id, 0))
		if count <= 0: continue
		var scale: float = badges.font_scales.get(id, 1.0)
		var font_size := maxi(1, roundi(12.0 * scale))
		var text_width := ThemeDB.fallback_font.get_string_size(str(count), HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
		var box := Rect2(badges.anchors[id], Vector2(maxf(18.0 * scale, text_width + 8.0 * scale), 18.0 * scale)).grow(1.0)
		for nameplate in labels:
			if box.intersects(nameplate): failures.append(context + " badge/name: " + id)
		for icon in icons:
			if box.intersects(icon): failures.append(context + " badge/icon: " + id)
		for prior in placed:
			if box.intersects(prior): failures.append(context + " badge/badge: " + id)
		if map._zoom_level == 0:
			var safe: Rect2 = to_badges * map.get_global_transform_with_canvas() * map.get_map_safe_rect()
			if not safe.encloses(box): failures.append(context + " outside HUD safe area: " + id)
		placed.append(box)
		badges_checked += 1
	layouts += 1

func _run() -> void:
	var map = load("res://gdscripts/world/BaiChuanNodeMapRuntime.gd").new()
	root.add_child(map)
	current_scene = map
	await frames()
	var counts := {}
	var ordered: Array = map.travel.graph.positions.keys()
	ordered.sort()
	for i in ordered.size(): counts[ordered[i]] = [1, 12, 54][i % 3]
	for resolution in [Vector2i(1920, 1080), Vector2i(1280, 720), Vector2i(1024, 720)]:
		root.size = resolution
		await frames()
		for level in [0, 1, 2]:
			map._set_zoom_level(level, Vector2(resolution) * 0.5)
			await frames()
			map.set_population_counts(counts)
			verify(map, str(resolution) + " level=" + str(level))
			if level == 0 and map._far_city_hover_fallback:
				for row: Dictionary in map.presentation.placements:
					if str(row.get("type", "")) != "city": continue
					map._show_far_city_hover_hint(str(row.id))
					await frames()
					verify(map, str(resolution) + " hovered city=" + str(row.id))
					map._clear_hover()
			var stable: Dictionary = map.population_badges.anchors.duplicate()
			map.set_population_counts(counts)
			if stable != map.population_badges.anchors: failures.append("Identical counts moved badges")
			var wide := {}
			for id: String in ordered: wide[id] = 12345
			map.set_population_counts(wide)
			verify(map, str(resolution) + " level=" + str(level) + " five digits")
			# Same totals in a different roster order must produce the same layout.
			stable = map.population_badges.anchors.duplicate()
			var reversed := {}
			for i in range(ordered.size()-1, -1, -1): reversed[ordered[i]] = 12345
			map.set_population_counts(reversed)
			if stable != map.population_badges.anchors: failures.append("Roster iteration order moved badges")
			map.set_population_counts(counts)
			map.focus_world_point(Vector2(3200, 600))
			await frames()
			verify(map, str(resolution) + " panned level=" + str(level))
	var result := {"layouts": layouts, "badgesChecked": badges_checked, "failures": failures}
	print("BAICHUAN_BADGE_LAYOUT " + JSON.stringify(result))
	map.queue_free()
	await frames()
	quit(0 if failures.is_empty() else 1)
