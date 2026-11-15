extends SceneTree

const DOC := "res://docs/美术规范/百川地图与节点验收_20261030/"
var failures: Array[String] = []
var checks := 0

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		push_error(label)

func frames(count: int) -> void:
	for i in count: await process_frame

func mouse(point: Vector2) -> void:
	if DisplayServer.get_name() != "headless": Input.warp_mouse(point)
	var event := InputEventMouseMotion.new()
	event.position = point
	event.global_position = point
	Input.parse_input_event(event)

func _run() -> void:
	root.size = Vector2i(1920,1080)
	var menu = load("res://tscn/scene/StartMenu.tscn").instantiate()
	root.add_child(menu)
	current_scene = menu
	await frames(5)
	var previous_mode := root.content_scale_mode
	var previous_size := root.content_scale_size
	var previous_factor := root.content_scale_factor
	menu.get_node("StartMenuMenu/DemoToggle").pressed.emit()
	await frames(3)
	var entry := menu.find_child("BaiChuanNineMapTest",true,false) as Button
	check(entry != null and entry.is_visible_in_tree(), "首页试演菜单存在可见地图入口")
	if entry == null:
		_finish()
		return
	entry.pressed.emit()
	await frames(8)
	var map = current_scene
	if DisplayServer.get_name() != "headless":
		root.mode = Window.MODE_WINDOWED
		root.size = Vector2i(1920,1080)
		root.position = Vector2i(80,80)
		await frames(5)
	check(map.scene_file_path == "res://tscn/world/bai_chuan/BaiChuanNineTileTest.tscn", "从真实首页进入测试场景")
	check(map._art_base == "res://textures/world/bai_chuan/acceptance_20261030/", "使用最新暖色验收资源")
	check(map.TILE_SIZE == 1254.0 and map.WORLD_SIZE == 3762.0, "原生1254九块与3762世界尺寸")
	check(map._textures.size() == 9, "九块纹理全部加载")
	for i in 9:
		var tile = map.world.get_child(i)
		check(tile.texture.get_size() == Vector2(1254,1254), "原生底图尺寸 " + str(i))
		check(tile.position == Vector2(i%3,floori(i/3.0))*1254.0 and tile.scale == Vector2.ONE, "九图位置与倍率 " + str(i))
	check(map.place_views.size() == 123, "123具名地点")
	check(map.icon_cache.size() == 9, "仅采用九种共用素材")
	var display_graph: Dictionary = map.display_layout.get("displayGraph",{}).get("edges",map.data.roadCurveGeometry.edges)
	check(map.ink.lines.size() == display_graph.size() and map.ink.marks.size() == 321, "全部显示连线与321空节点加载")
	check(map.ink.paths_visible and map.ink.marks_visible, "默认显示虚线路网与空节点")
	check(map.ink.MAIN_INK.r < 0.18 and map.ink.MAIN_INK.a > 0.9, "虚线加深为近黑墨色")
	if str(map.display_layout.get("version","")).begins_with("display_layout_v3"):
		var geometry: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(map._art_base+"display_layout_v3_independent_audit.json"))
		check(bool(geometry.get("strictGeometryPass",false)), "完整线路无非节点接触、误穿节点或越界")
		check(bool(geometry.get("visualClearancePass",false)), "非节点处墨线保留可见间距")
		check(geometry.get("layoutSha256","") == FileAccess.get_sha256(map._art_base+"display_layout_v3.json"), "几何验收对应本次实际加载的数据")
	check(root.content_scale_mode == Window.CONTENT_SCALE_MODE_DISABLED, "窗口不放大原像素")
	check(map._nameplate_texture.get_size() == Vector2(48,152), "去字纸纹名牌纹理加载")
	var positions := {}
	for id: String in map.display_layout.locations:
		var point: Array = map.display_layout.locations[id].drawPos
		positions[id] = Vector2(point[0],point[1])
	for id: String in map.display_layout.transitPositions:
		var point: Array = map.display_layout.transitPositions[id]
		positions[id] = Vector2(point[0],point[1])
	var adjacency := {}
	var edge_index := 0
	for key: String in display_graph:
		var edge: Dictionary = display_graph[key]
		var line: PackedVector2Array = map.ink.lines[edge_index]
		check(line[0].distance_to(positions[edge.from]) < 0.01 and line[-1].distance_to(positions[edge.to]) < 0.01, "虚线首尾连接对应显示节点 " + key)
		if not adjacency.has(edge.from): adjacency[edge.from] = []
		if not adjacency.has(edge.to): adjacency[edge.to] = []
		adjacency[edge.from].append(edge.to)
		adjacency[edge.to].append(edge.from)
		edge_index += 1
	var visited := {}
	var pending: Array = [map.data.nodes[0].id]
	while not pending.is_empty():
		var id: String = pending.pop_back()
		if visited.has(id): continue
		visited[id] = true
		pending.append_array(adjacency.get(id,[]))
	check(visited.size() == 444, "全部444既有节点真实连通")
	if map.display_layout.has("sourceEdgeWalks"):
		check(map.display_layout.sourceEdgeWalks.size() == map.data.roadCurveGeometry.edges.size(), "原498相邻联系均有显示路径")
		for source: String in map.data.roadCurveGeometry.edges:
			var original: Dictionary = map.data.roadCurveGeometry.edges[source]
			var walk: Array = map.display_layout.sourceEdgeWalks.get(source,[])
			var valid: bool = walk.size() >= 2 and walk[0] == original.from and walk[-1] == original.to
			for step in range(1,walk.size()):
				valid = valid and adjacency.get(walk[step-1],[]).has(walk[step])
			check(valid, "近节点接入后原联系仍可循真实边到达 " + source)
	for i in map.data.transitNodes.size():
		check(map.ink.marks[i] == positions[map.data.transitNodes[i].id], "过渡点与虚线共同移位 " + str(i))
	var built := 0
	var natural := 0
	var main_assets: Array[String] = []
	for row: Dictionary in map.presentation.placements:
		var marker: Control = map.place_views[row.id]
		var icon: Sprite2D = marker.get_node("NativeIcon")
		var hit = marker.get_node("SelectIcon")
		var nameplate: Button = marker.get_node("SelectPlace")
		check(nameplate.text.replace("\n", "") == row.name and nameplate.text.contains("\n"), "名称竖排且文字不变 " + row.id)
		check(not nameplate.flat and nameplate.get_theme_stylebox("normal") is StyleBoxTexture, "用户参考纸纹名牌底板可见 " + row.id)
		check(nameplate.size == Vector2(row.labelRect[2],row.labelRect[3]), "名牌占地与避让区域一致 " + row.id)
		check(marker.position == map._point(map.data.nodes.filter(func(n): return n.id == row.id)[0].pos).round(), "逻辑坐标保持 " + row.id)
		check(icon.scale == Vector2.ONE, "图标无额外缩放 " + row.id)
		if not row.drawIcon:
			check(marker.get_node("NaturalSiteMark").position+marker.position == positions[row.id], "野外标记与路端点一致 " + row.id)
		if row.drawIcon:
			built += 1
			check(icon.visible and icon.texture.get_size() == Vector2(row.rect[2],row.rect[3]), "透明贴图实际尺寸 " + row.id)
			check(hit.silhouette != null, "贴图轮廓命中 " + row.id)
			if row.type == "sect" and int(row.get("level",0)) == 1: main_assets.append(row.asset)
			map._focus_place(row.name)
			await frames(2)
			var pixels := icon.texture.get_image()
			var target := Vector2.ZERO
			var nearest := INF
			var transparent := false
			for y in pixels.get_height():
				for x in pixels.get_width():
					if pixels.get_pixel(x,y).a < 0.01 and not transparent:
						check(not hit._has_point(Vector2(x,y)), "透明区域不抢点击 " + row.id)
						transparent = true
					if pixels.get_pixel(x,y).a < 0.9: continue
					var distance := Vector2(x,y).distance_squared_to(Vector2(pixels.get_size())*0.5)
					if distance < nearest:
						nearest = distance
						target = Vector2(x,y)+Vector2(0.25,0.25)
			check(transparent and nearest < INF, "存在透明背景与不透明建筑 " + row.id)
			var point: Vector2 = hit.global_position+target
			mouse(point)
			await frames(3)
			check(map.hover_id == row.id, "真实鼠标悬停 " + row.id)
			map.title_label.text = "等待点击"
			for pressed in [true,false]:
				var click := InputEventMouseButton.new()
				click.position = point
				click.global_position = point
				click.button_index = MOUSE_BUTTON_LEFT
				click.pressed = pressed
				Input.parse_input_event(click)
				await process_frame
			check(map.title_label.text == row.name, "真实鼠标点击建筑 " + row.id)
		else:
			natural += 1
			check(icon.texture == null, "自然地点不加载额外贴图 " + row.id)
		marker.get_node("SelectPlace").pressed.emit()
		check(map.title_label.text == row.name, "名称选择 " + row.id)
	check(built == 53 and natural == 70, "53建筑与70自然地点")
	var label_overlaps := 0
	var building_overlaps := 0
	for i in map.presentation.placements.size():
		var row: Dictionary = map.presentation.placements[i]
		var label := Rect2(row.labelRect[0],row.labelRect[1],row.labelRect[2],row.labelRect[3])
		for j in map.presentation.placements.size():
			var other: Dictionary = map.presentation.placements[j]
			if other.drawIcon and label.intersects(Rect2(other.rect[0],other.rect[1],other.rect[2],other.rect[3])): building_overlaps += 1
			if j <= i: continue
			if label.intersects(Rect2(other.labelRect[0],other.labelRect[1],other.labelRect[2],other.labelRect[3])): label_overlaps += 1
	check(label_overlaps == 0, "竖排名牌之间无重叠")
	check(building_overlaps == 0, "竖排名牌不遮建筑")
	check(main_assets == ["yuxu","xuandu","wuxiang"], "三个一级势力各自独立")
	var index := map.find_child("AllPlaceIndex",true,false) as OptionButton
	check(index.item_count == 124, "123地点索引齐全")
	index.item_selected.emit(2)
	check(map.title_label.text == "玄都天府", "地点索引定位")
	map._toggle_layer(true,"paths")
	map._toggle_layer(true,"marks")
	check(map.ink.paths_visible and map.ink.marks_visible, "路线与空节点可打开")
	map._toggle_layer(false,"places")
	check(not map.places.visible and map.hover_id == "", "隐藏地点清除悬停")
	map._toggle_layer(true,"places")
	map._toggle_layer(true,"paths")
	map._toggle_layer(true,"marks")
	map._focus_place("玉虚仙宗")
	var before: Vector2 = map.camera_pos
	var wheel := InputEventMouseButton.new()
	wheel.button_index = MOUSE_BUTTON_WHEEL_UP
	wheel.pressed = true
	Input.parse_input_event(wheel)
	await frames(2)
	check(map.world.scale == Vector2.ONE and map.camera_pos == before, "真实滚轮保持固定倍率")
	var drag_start := InputEventMouseButton.new()
	drag_start.button_index = MOUSE_BUTTON_LEFT
	drag_start.pressed = true
	map._unhandled_input(drag_start)
	var drag_motion := InputEventMouseMotion.new()
	drag_motion.relative = Vector2(-100,0)
	map._unhandled_input(drag_motion)
	check(map.camera_pos.x > before.x, "拖动平移")
	drag_start.pressed = false
	map._input(drag_start)
	check(not map.dragging, "释放鼠标结束拖动")
	map.camera_pos = Vector2(99999,-99999)
	map._update_camera()
	check(map.camera_pos.y == 0 and map.camera_pos.x <= 3762-1920, "相机边界限制")
	if DisplayServer.get_name() != "headless":
		for name in ["玉虚仙宗","玄都天府","无相圣宗","朱明谷","葬日谷","疏影潭","天门峡"]:
			map._focus_place(name)
			mouse(Vector2(1880,700))
			await frames(3)
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(DOC+"游戏内_"+name+".png")
	map._return_to_menu()
	await frames(8)
	check(current_scene.scene_file_path == "res://tscn/scene/StartMenu.tscn", "返回首页")
	check(root.content_scale_mode == previous_mode and root.content_scale_size == previous_size and is_equal_approx(root.content_scale_factor,previous_factor), "返回后恢复窗口设置")
	_finish()

func _finish() -> void:
	var result := {"checks":checks,"failures":failures,"display":DisplayServer.get_name(),"gameTestOnly":true,"formalMapReplaced":false}
	var file := FileAccess.open(DOC+"游戏内验收_"+DisplayServer.get_name()+".json",FileAccess.WRITE)
	file.store_string(JSON.stringify(result,"  "))
	file.close()
	print("BAICHUAN_INGAME_DONE "+JSON.stringify(result))
	current_scene.queue_free()
	current_scene = null
	await frames(3)
	quit(0 if failures.is_empty() else 1)
