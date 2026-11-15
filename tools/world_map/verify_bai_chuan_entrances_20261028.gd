extends SceneTree

var failures: Array[String] = []
var checks := 0

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		push_error(label)

func move_mouse(point: Vector2) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	Input.parse_input_event(motion)

func _run() -> void:
	root.size = Vector2i(1920,1080)
	var scene = load("res://tscn/world/bai_chuan/BaiChuanNineTileTest.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for i in 5: await process_frame
	check(scene._whole_mode and scene._art_base == "res://textures/world/bai_chuan/whole_paint_v3/", "newly generated background active")
	check(scene.place_views.size() == 123, "123 independent named nodes")
	check(scene.ink.marks.size() == 321 and scene.ink.lines.size() == 498, "unchanged graph")
	check(not scene.ink.main_dashes.is_empty() and scene.ink.main_dashes.size()%2 == 0, "cached dashed route strokes")
	check(scene.ink.path_masks.size() == 246, "all node art and names protected")
	var route_intrusions := 0
	for strokes: PackedVector2Array in [scene.ink.main_dashes,scene.ink.spur_dashes]:
		for i in range(0,strokes.size(),2):
			for row: Dictionary in scene.presentation.placements:
				for box: Array in [row.rect,row.labelRect]:
					var rect := Rect2(box[0],box[1],box[2],box[3])
					if scene.ink._inside_interval(strokes[i],strokes[i+1],rect).x >= 0:
						route_intrusions += 1
	check(route_intrusions == 0, "no road strokes below node artwork or names")
	var sample: PackedVector2Array = scene.ink._dash_curve(PackedVector2Array([Vector2.ZERO,Vector2(6,0),Vector2(20,0)]))
	var drawn_length := 0.0
	for i in range(0,sample.size(),2): drawn_length += sample[i].distance_to(sample[i+1])
	check(is_equal_approx(drawn_length,13.0), "dash phase continues across curve vertices")
	var entrance_count := 0
	var tiers := {}
	for row: Dictionary in scene.presentation.placements:
		var marker: Control = scene.place_views[row.id]
		var icon: Sprite2D = marker.get_node("NativeIcon")
		var hit = marker.get_node("SelectIcon")
		var label: Button = marker.get_node("SelectPlace")
		check(hit.mouse_default_cursor_shape == Control.CURSOR_POINTING_HAND and label.mouse_default_cursor_shape == Control.CURSOR_POINTING_HAND, "cursors " + str(row.id))
		check(icon.scale == Vector2.ONE, "native icon scale " + str(row.id))
		if row.has("visualTier"):
			entrance_count += 1
			tiers[row.visualTier] = int(tiers.get(row.visualTier,0))+1
			check(icon.texture.get_width() == int(row.rect[2]) and icon.texture.get_height() == int(row.rect[3]), "native entrance dimensions " + str(row.id))
			check(hit.silhouette != null and not hit._has_point(Vector2(-1,-1)), "masked entrance " + str(row.id))
			var image := icon.texture.get_image()
			var transparent_found := false
			var opaque_found := false
			for y in image.get_height():
				for x in image.get_width():
					var alpha := image.get_pixel(x,y).a
					if not transparent_found and alpha < 0.01:
						check(not hit._has_point(Vector2(x,y)), "transparent hole rejects " + str(row.id))
						transparent_found = true
					if not opaque_found and alpha > 0.95:
						check(hit._has_point(Vector2(x,y)), "opaque entrance accepts " + str(row.id))
						opaque_found = true
					if opaque_found and transparent_found: break
				if opaque_found and transparent_found: break
			check(opaque_found and transparent_found, "real transparent asset " + str(row.id))
		check(marker.get_node("SelectIcon").is_connected("mouse_entered", scene._hover_place), "hover signal " + str(row.id))
	check(entrance_count == 48, "all 48 entrance replacements")
	check(tiers.get("sect_1") == 3 and tiers.get("sect_2") == 10 and tiers.get("city") == 16 and tiers.get("town") == 14 and tiers.get("market") == 5, "existing classification preserved")
	for node: Dictionary in scene.data.nodes:
		scene._focus_place(node.name)
		var marker: Control = scene.place_views[node.id]
		scene._hover_place(node.id)
		check(scene.hover_id == node.id and marker.get_node("NativeIcon").modulate != Color.WHITE, "hover highlights " + str(node.id))
		marker.get_node("SelectPlace").pressed.emit()
		check(scene.title_label.text == node.name, "label selects " + str(node.id))
		scene._clear_hover()
		check(scene.hover_id == "" and marker.get_node("NativeIcon").modulate == Color.WHITE, "hover restored " + str(node.id))
	scene._focus_place("玉虚仙宗")
	scene._hover_place("bc_place_001")
	scene._hover_place("bc_place_002")
	check(scene.place_views.bc_place_001.get_node("NativeIcon").modulate == Color.WHITE, "switch clears previous hover")
	scene._toggle_layer(false,"places")
	check(scene.hover_id == "", "hidden layer clears hover")
	scene._toggle_layer(true,"places")
	scene._focus_place("玉虚仙宗")
	# Test actual GUI event dispatch, not just the appearance helper.
	var gate: Control = scene.place_views.bc_place_001.get_node("SelectIcon")
	var point := gate.global_position + gate.size*0.5
	move_mouse(point)
	for i in 3: await process_frame
	check(scene.hover_id == "bc_place_001", "real mouse motion enters gate")
	move_mouse(Vector2(1900,1000))
	for i in 3: await process_frame
	check(scene.hover_id == "", "real mouse motion exits gate")
	if DisplayServer.get_name() != "headless":
		DirAccess.make_dir_recursive_absolute("res://output/bai_chuan_entrances/")
		for name in ["玉虚仙宗","玄都天府","无相圣宗","承天城","松风镇"]:
			scene._focus_place(name)
			await process_frame
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png("res://output/bai_chuan_entrances/"+name+".png")
	var result := {"checks":checks,"failures":failures,"display":DisplayServer.get_name(),"entrances":entrance_count,"totalNodes":123}
	var file := FileAccess.open("res://docs/美术规范/百川入口节点分层_20261028/交互验收.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(result,"  "))
	file.close()
	print("BAICHUAN_ENTRANCES_DONE "+JSON.stringify(result))
	current_scene = null
	scene.queue_free()
	for i in 3: await process_frame
	quit(0 if failures.is_empty() else 1)
