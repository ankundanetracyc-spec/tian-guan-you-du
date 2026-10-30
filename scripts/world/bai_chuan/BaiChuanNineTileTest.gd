extends Control
## Isolated art/placement preview. No travel, calendar or save transactions.

const TILE_SIZE := 2048.0
const WORLD_SIZE := TILE_SIZE * 3.0
const ART_DIR := "res://textures/world/bai_chuan/acceptance_v1/tiles/"
const PRESENTATION_PATH := "res://textures/world/bai_chuan/acceptance_v1/presentation.json"
const ENTRANCE_PATH := "res://textures/world/bai_chuan/entrance_v1/nodes.json"
const WHOLE_ART_BASE := "res://textures/world/bai_chuan/whole_paint_v3/"
const JOINT_ART_BASE := "res://textures/world/bai_chuan/joint_paint_v4/"
const DATA_PATH := "res://docs/prototypes/百川宗门城镇资源规划数据_20261027.json"
const TILE_FILES := [
	"bc_11.png", "bc_12.png", "bc_13.png", "bc_21.png", "bc_22.png", "bc_23.png",
	"bc_31.png", "bc_32.png", "bc_33.png"
]
var world := Node2D.new()
var places := Node2D.new()
var ink: WorldInk
var data: Dictionary = {}
var presentation: Dictionary = {}
var place_views: Dictionary = {}
var icon_cache: Dictionary = {}
var camera_pos := Vector2.ZERO
var dragging := false
var title_label: Label
var detail_label: Label
var mini: Control
var mini_frame: ReferenceRect
var _old_mode: int
var _old_size: Vector2i
var _old_factor: float
var _scale_saved := false
var _textures: Array[Texture2D] = []
var _whole_mode := false
var _art_base := "res://textures/world/bai_chuan/acceptance_v1/"
var hover_id := ""
var hit_mask_cache: Dictionary = {}

class EntranceHitButton extends Button:
	var silhouette: BitMap
	func _has_point(point: Vector2) -> bool:
		if not Rect2(Vector2.ZERO, size).has_point(point):
			return false
		if silhouette == null:
			return true
		var pixel := Vector2i(point.floor())
		return silhouette.get_bitv(pixel)

class NaturalSiteMark extends Node2D:
	func _draw() -> void:
		draw_circle(Vector2.ZERO,4.0,Color("cfb98e"))
		draw_arc(Vector2.ZERO,4.0,0,TAU,20,Color("61583f"),1.1,true)

class WorldInk extends Node2D:
	var lines: Array[PackedVector2Array] = []
	var spurs: Array[bool] = []
	var marks: Array[Vector2] = []
	var junctions: Array[bool] = []
	var local_approaches: Array[PackedVector2Array] = []
	var paths_visible := true
	var marks_visible := true
	var main_dashes := PackedVector2Array()
	var spur_dashes := PackedVector2Array()
	var path_masks: Array[Rect2] = []
	var _mask_grid: Dictionary = {}
	const MASK_CELL := 256.0
	const DASH_LENGTH := 10.0
	const DASH_PERIOD := 17.0

	func rebuild_dashes(masks: Array[Rect2]) -> void:
		path_masks = masks
		_mask_grid.clear()
		for rect in masks:
			var lo := Vector2i((rect.position / MASK_CELL).floor())
			var hi := Vector2i((rect.end / MASK_CELL).floor())
			for y in range(lo.y, hi.y + 1):
				for x in range(lo.x, hi.x + 1):
					var cell := Vector2i(x,y)
					if not _mask_grid.has(cell): _mask_grid[cell] = []
					_mask_grid[cell].append(rect)
		main_dashes.clear()
		spur_dashes.clear()
		for i in lines.size():
			if spurs[i]: spur_dashes.append_array(_dash_curve(lines[i]))
			else: main_dashes.append_array(_dash_curve(lines[i]))
		for approach in local_approaches:
			spur_dashes.append_array(_dash_curve(approach))
		queue_redraw()

	func _dash_curve(points: PackedVector2Array) -> PackedVector2Array:
		var strokes := PackedVector2Array()
		var along := 0.0
		for i in range(1,points.size()):
			var a := points[i-1]
			var delta := points[i]-a
			var length := delta.length()
			if length < 0.001: continue
			var direction := delta/length
			var offset := 0.0
			while offset < length-0.001:
				var phase := fposmod(along,DASH_PERIOD)
				var drawing := phase < DASH_LENGTH
				var run := minf((DASH_LENGTH if drawing else DASH_PERIOD)-phase,length-offset)
				if run < 0.001:
					offset += 0.001
					along += 0.001
					continue
				if drawing:
					strokes.append_array(_unmasked_segment(a+direction*offset,a+direction*(offset+run)))
				offset += run
				along += run
		return strokes

	func _inside_interval(a: Vector2,b: Vector2,rect: Rect2) -> Vector2:
		var delta := b-a
		var lo := 0.0
		var hi := 1.0
		for axis in 2:
			if absf(delta[axis]) < 0.00001:
				if a[axis] < rect.position[axis] or a[axis] > rect.end[axis]: return Vector2(-1,-1)
				continue
			var enter := (rect.position[axis]-a[axis])/delta[axis]
			var leave := (rect.end[axis]-a[axis])/delta[axis]
			lo = maxf(lo,minf(enter,leave))
			hi = minf(hi,maxf(enter,leave))
			if hi <= lo: return Vector2(-1,-1)
		return Vector2(lo,hi)

	func _unmasked_segment(a: Vector2,b: Vector2) -> PackedVector2Array:
		var intervals: Array[Vector2] = [Vector2(0,1)]
		var lo := Vector2i((a.min(b)/MASK_CELL).floor())
		var hi := Vector2i((a.max(b)/MASK_CELL).floor())
		for y in range(lo.y,hi.y+1):
			for x in range(lo.x,hi.x+1):
				for rect: Rect2 in _mask_grid.get(Vector2i(x,y),[]):
					var blocked := _inside_interval(a,b,rect)
					if blocked.x < 0: continue
					var remaining: Array[Vector2] = []
					for span in intervals:
						if blocked.y <= span.x or blocked.x >= span.y:
							remaining.append(span)
						else:
							if span.x < blocked.x: remaining.append(Vector2(span.x,blocked.x))
							if span.y > blocked.y: remaining.append(Vector2(blocked.y,span.y))
					intervals = remaining
		var strokes := PackedVector2Array()
		for span in intervals:
			if span.y-span.x < 0.0001: continue
			strokes.append(a.lerp(b,span.x))
			strokes.append(a.lerp(b,span.y))
		return strokes

	func _draw() -> void:
		if paths_visible:
			if not main_dashes.is_empty(): draw_multiline(main_dashes,Color(0.36,0.30,0.22,0.77),1.8,true)
			if not spur_dashes.is_empty(): draw_multiline(spur_dashes,Color(0.40,0.34,0.25,0.65),1.3,true)
		if marks_visible:
			for i in marks.size():
				draw_circle(marks[i], 3.0 if junctions[i] else 2.5, Color(0.91, 0.86, 0.76, 0.85))
				draw_arc(marks[i], 3.0 if junctions[i] else 2.5, 0, TAU, 16, Color(0.40, 0.33, 0.24, 0.82), 1.0, true)

func _ready() -> void:
	_whole_mode = FileAccess.file_exists(WHOLE_ART_BASE+"READY.json") or OS.get_cmdline_user_args().has("--bai-chuan-whole-v2")
	if _whole_mode: _art_base = WHOLE_ART_BASE
	if FileAccess.file_exists(JOINT_ART_BASE+"READY.json") or OS.get_cmdline_user_args().has("--bai-chuan-joint-v4"):
		_art_base = JOINT_ART_BASE
		_whole_mode = true
	var win := get_window()
	_old_mode = win.content_scale_mode
	_old_size = win.content_scale_size
	_old_factor = win.content_scale_factor
	_scale_saved = true
	# A camera zoom of 1 alone is insufficient when canvas_items enlarges the window.
	win.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	win.content_scale_size = Vector2i.ZERO
	win.content_scale_factor = 1.0
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	data = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH)) as Dictionary
	presentation = JSON.parse_string(FileAccess.get_file_as_string(_art_base+"presentation.json")) as Dictionary
	_apply_entrance_art()
	if data.is_empty():
		push_error("百川地图测试：规划数据未能读取")
		_return_to_menu.call_deferred()
		return
	world.name = "FixedScaleWorld"
	add_child(world)
	for i in TILE_FILES.size():
		var texture := _image_texture(_art_base+"tiles/"+TILE_FILES[i])
		if texture == null:
			continue
		_textures.append(texture)
		var tile := Sprite2D.new()
		tile.name = "Background" + str(i / 3 + 1) + str(i % 3 + 1)
		tile.centered = false
		tile.texture = texture
		tile.position = Vector2(i % 3, floori(i / 3.0)) * TILE_SIZE
		world.add_child(tile)
	ink = WorldInk.new()
	ink.name = "RoutesAndTransit"
	world.add_child(ink)
	_build_routes()
	places.name = "Places"
	world.add_child(places)
	_build_places()
	_rebuild_path_art()
	_build_hud()
	get_viewport().size_changed.connect(_update_camera)
	_focus_place("玉虚仙宗")

func _image_texture(path: String) -> Texture2D:
	# Read original PNG bytes without import timing dependencies or resampling.
	var source := Image.new()
	var status := source.load_png_from_buffer(FileAccess.get_file_as_bytes(path))
	if status != OK or source.is_empty():
		push_error("百川测试素材缺失：" + path)
		return null
	return ImageTexture.create_from_image(source)

func _point(pos: Array) -> Vector2:
	return Vector2(float(pos[0]), float(pos[1])) * WORLD_SIZE / 100.0

func _apply_entrance_art() -> void:
	if bool(presentation.get("nodesIntegrated",false)):
		return
	if not FileAccess.file_exists(ENTRANCE_PATH):
		return
	var entrances: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ENTRANCE_PATH))
	presentation.catalog.merge(entrances.catalog, true)
	var by_id := {}
	for entry: Dictionary in entrances.placements:
		by_id[entry.id] = entry
	for row: Dictionary in presentation.placements:
		if not by_id.has(row.id):
			continue
		var entry: Dictionary = by_id[row.id]
		var dimensions: Array = entrances.catalog[entry.asset].size
		var anchor: Array = row.anchor
		var offset: Array = row.get("artOffset", [0,0])
		var pivot: Array = row.pivot
		row.asset = entry.asset
		row.drawIcon = true
		row.visualTier = entry.tier
		row.rect = [roundf(anchor[0]+offset[0]-dimensions[0]*pivot[0]), roundf(anchor[1]+offset[1]-dimensions[1]*pivot[1]), dimensions[0], dimensions[1]]

func _build_routes() -> void:
	for row: Dictionary in presentation.placements:
		if row.has("approach"):
			var approach := PackedVector2Array()
			for p: Array in row.approach: approach.append(Vector2(p[0],p[1]))
			ink.local_approaches.append(approach)
	var spur_edges := {}
	for route: Dictionary in data.get("routes", []):
		if route.get("isResourceSpur", false):
			for segment: Dictionary in route.get("movementSegments", []):
				spur_edges[segment.edge] = true
	var edges: Dictionary = data.get("roadCurveGeometry", {}).get("edges", {})
	var displayed: Dictionary = presentation.get("displayEdges",{})
	for key: String in edges:
		var points := PackedVector2Array()
		if displayed.has(key):
			for p: Array in displayed[key]: points.append(Vector2(p[0],p[1]))
		else:
			for p: Array in edges[key].points:
				points.append(_point(p))
		if points.size() >= 2:
			ink.lines.append(points)
			ink.spurs.append(spur_edges.has(key))
	for node: Dictionary in data.get("transitNodes", []):
		ink.marks.append(_point(node.pos))
		ink.junctions.append(node.get("isJunction", false))
	ink.queue_redraw()

func _build_places() -> void:
	var placement_by_id := {}
	for row: Dictionary in presentation.placements:
		placement_by_id[row.id] = row
	for node: Dictionary in data.get("nodes", []):
		var row: Dictionary = placement_by_id[node.id]
		var marker := Control.new()
		marker.name = str(node.id)
		marker.position = _point(node.pos).round()
		marker.mouse_filter = Control.MOUSE_FILTER_IGNORE
		places.add_child(marker)
		var key: String = row.asset
		var draw_icon := bool(row.get("drawIcon",true))
		if draw_icon and not icon_cache.has(key):
			icon_cache[key] = _image_texture(presentation.catalog[key].file)
		var image_view := Sprite2D.new()
		image_view.name = "NativeIcon"
		if draw_icon: image_view.texture = icon_cache[key]
		image_view.visible = draw_icon and not bool(row.get("hoverOnly",false))
		image_view.set_meta("hover_only",bool(row.get("hoverOnly",false)))
		image_view.centered = false
		image_view.position = Vector2(row.rect[0],row.rect[1])-marker.position
		marker.add_child(image_view)
		if not draw_icon:
			var natural_mark := NaturalSiteMark.new()
			natural_mark.name = "NaturalSiteMark"
			marker.add_child(natural_mark)
		var hit := EntranceHitButton.new()
		hit.name = "SelectIcon"
		hit.flat = true
		hit.focus_mode = Control.FOCUS_NONE
		hit.position = image_view.position
		hit.size = Vector2(row.rect[2],row.rect[3])
		hit.tooltip_text = str(node.name)
		hit.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		if draw_icon:
			if not hit_mask_cache.has(key):
				var mask := BitMap.new()
				mask.create_from_image_alpha(image_view.texture.get_image(), 0.12)
				hit_mask_cache[key] = mask
			hit.silhouette = hit_mask_cache[key]
		hit.mouse_entered.connect(_hover_place.bind(str(node.id)))
		hit.mouse_exited.connect(_defer_hover_clear.bind(str(node.id)))
		hit.add_theme_stylebox_override("normal",StyleBoxEmpty.new())
		hit.add_theme_stylebox_override("hover",StyleBoxEmpty.new())
		hit.add_theme_stylebox_override("pressed",StyleBoxEmpty.new())
		hit.pressed.connect(_select_place.bind(node))
		marker.add_child(hit)
		var button := Button.new()
		button.name = "SelectPlace"
		button.text = str(node.name)
		button.flat = true
		button.position = Vector2(row.labelRect[0],row.labelRect[1])-marker.position
		button.size = Vector2(row.labelRect[2],row.labelRect[3])
		button.focus_mode = Control.FOCUS_NONE
		button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		button.mouse_entered.connect(_hover_place.bind(str(node.id)))
		button.mouse_exited.connect(_defer_hover_clear.bind(str(node.id)))
		button.add_theme_font_override("font",load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"))
		button.add_theme_font_size_override("font_size", int(row.labelSize))
		button.add_theme_color_override("font_color", Color("594d3d"))
		button.add_theme_color_override("font_hover_color", Color("8c5637"))
		button.add_theme_color_override("font_outline_color", Color("d9c59e"))
		button.add_theme_constant_override("outline_size", 2)
		button.pressed.connect(_select_place.bind(node))
		marker.add_child(button)
		place_views[node.id] = marker

func _hover_place(id: String) -> void:
	if hover_id == id:
		return
	_clear_hover()
	hover_id = id
	var marker: Control = place_views[id]
	if bool(marker.get_node("NativeIcon").get_meta("hover_only",false)):
		marker.get_node("NativeIcon").show()
	marker.get_node("NativeIcon").modulate = Color(1.20, 1.14, 1.04, 1.0)
	marker.get_node("SelectPlace").add_theme_color_override("font_color", Color("955a2d"))
	var natural := marker.get_node_or_null("NaturalSiteMark")
	if natural != null:
		natural.modulate = Color(1.3, 1.15, 0.9)

func _defer_hover_clear(id: String) -> void:
	_check_hover_exit.call_deferred(id)

func _check_hover_exit(id: String) -> void:
	if hover_id != id or not place_views.has(id):
		return
	var marker: Control = place_views[id]
	for name in ["SelectIcon", "SelectPlace"]:
		var hit: Control = marker.get_node(name)
		var point := hit.get_local_mouse_position()
		var contains := hit._has_point(point) if hit is EntranceHitButton else Rect2(Vector2.ZERO, hit.size).has_point(point)
		if hit.is_visible_in_tree() and contains:
			return
	_clear_hover()

func _clear_hover() -> void:
	if place_views.has(hover_id):
		var marker: Control = place_views[hover_id]
		marker.get_node("NativeIcon").modulate = Color.WHITE
		if bool(marker.get_node("NativeIcon").get_meta("hover_only",false)):
			marker.get_node("NativeIcon").hide()
		marker.get_node("SelectPlace").add_theme_color_override("font_color", Color("594d3d"))
		var natural := marker.get_node_or_null("NaturalSiteMark")
		if natural != null:
			natural.modulate = Color.WHITE
	hover_id = ""

func _rebuild_path_art() -> void:
	var masks: Array[Rect2] = []
	for row: Dictionary in presentation.placements:
		var rect: Array = row.rect
		var label: Array = row.labelRect
		# Co-painted buildings remain in the background when information is hidden.
		if places.visible or bool(row.get("hoverOnly",false)):
			masks.append(Rect2(rect[0],rect[1],rect[2],rect[3]).grow(6.0))
		if places.visible:
			masks.append(Rect2(label[0],label[1],label[2],label[3]).grow(3.0))
	ink.rebuild_dashes(masks)

func _panel(parent: Node) -> PanelContainer:
	var panel := PanelContainer.new()
	var paper := StyleBoxFlat.new()
	paper.bg_color = Color(0.94, 0.90, 0.81, 0.96)
	paper.border_color = Color("ada087")
	paper.set_border_width_all(1)
	paper.content_margin_left = 14
	paper.content_margin_right = 14
	paper.content_margin_top = 10
	paper.content_margin_bottom = 10
	panel.add_theme_stylebox_override("panel", paper)
	var palette := Theme.new()
	for kind in ["Label", "Button", "OptionButton", "CheckButton"]:
		palette.set_color("font_color", kind, Color("554b3c"))
		palette.set_color("font_hover_color", kind, Color("6f573d"))
		palette.set_color("font_pressed_color", kind, Color("554b3c"))
	for kind in ["Button", "OptionButton"]:
		for state in ["normal", "hover", "pressed", "focus"]:
			var style := StyleBoxFlat.new()
			style.bg_color = Color("dfd0af") if state == "normal" else Color("eee1c3")
			style.border_color = Color("b5a385")
			style.set_border_width_all(1)
			style.content_margin_left = 7
			style.content_margin_right = 7
			style.content_margin_top = 4
			style.content_margin_bottom = 4
			palette.set_stylebox(state,kind,style)
	panel.theme = palette
	parent.add_child(panel)
	return panel

func _button(parent: Node, text: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.focus_mode = Control.FOCUS_NONE
	button.add_theme_font_size_override("font_size", 17)
	button.pressed.connect(action)
	parent.add_child(button)
	return button

func _build_hud() -> void:
	var top := _panel(self)
	top.position = Vector2(18, 18)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	top.add_child(box)
	var heading := Label.new()
	heading.text = "百川舆图 · 地图试览" if _whole_mode else "百川舆图 · 九图测试"
	heading.add_theme_font_size_override("font_size", 26)
	box.add_child(heading)
	var nav := HBoxContainer.new()
	box.add_child(nav)
	for location in ["玉虚仙宗", "玄都天府", "无相圣宗", "春水府"]:
		_button(nav, location, _focus_place.bind(location))
	var jump := OptionButton.new()
	jump.name = "AllPlaceIndex"
	jump.add_item("地点索引（123处）")
	for node: Dictionary in data.nodes:
		jump.add_item(str(node.name))
	jump.item_selected.connect(func(index: int):
		if index > 0: _focus_place(str(data.nodes[index-1].name))
	)
	nav.add_child(jump)
	_button(nav, "返回首页", _return_to_menu)
	var toggles := HBoxContainer.new()
	box.add_child(toggles)
	for entry in [["道路", "paths"], ["空节点", "marks"], ["地点信息", "places"]]:
		var toggle := CheckButton.new()
		toggle.text = entry[0]
		toggle.button_pressed = true
		toggle.focus_mode = Control.FOCUS_NONE
		toggle.toggled.connect(_toggle_layer.bind(entry[1]))
		toggles.add_child(toggle)
	var note := Label.new()
	note.text = "倍率锁定 1∶1 · 拖动或方向键浏览 · 滚轮不缩放"
	note.add_theme_font_size_override("font_size", 14)
	box.add_child(note)
	var details := _panel(self)
	details.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	details.offset_left = -310
	details.offset_top = -192
	details.offset_right = -18
	details.offset_bottom = -18
	var info := VBoxContainer.new()
	details.add_child(info)
	title_label = Label.new()
	title_label.add_theme_font_size_override("font_size", 23)
	info.add_child(title_label)
	detail_label = Label.new()
	detail_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	detail_label.custom_minimum_size.x = 255
	detail_label.add_theme_font_size_override("font_size", 15)
	info.add_child(detail_label)
	var tip := Label.new()
	tip.text = "整图验收 · 本页不推进旅程"
	tip.add_theme_font_size_override("font_size", 13)
	info.add_child(tip)
	var mini_panel := _panel(self)
	mini_panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	mini_panel.offset_left = 18
	mini_panel.offset_top = -246
	mini_panel.offset_right = 230
	mini_panel.offset_bottom = -18
	mini = Control.new()
	mini.custom_minimum_size = Vector2(180, 180)
	mini.size = Vector2(180, 180)
	mini.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	mini.gui_input.connect(_mini_input)
	mini_panel.add_child(mini)
	var atlas_view := TextureRect.new()
	atlas_view.texture = _image_texture(_art_base+"overview.png")
	atlas_view.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	atlas_view.size = Vector2(180, 180)
	atlas_view.mouse_filter = Control.MOUSE_FILTER_IGNORE
	mini.add_child(atlas_view)
	mini_frame = ReferenceRect.new()
	mini_frame.editor_only = false
	mini_frame.border_color = Color("786044")
	mini_frame.border_width = 2
	mini_frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	mini.add_child(mini_frame)

func _toggle_layer(on: bool, layer: String) -> void:
	if layer == "places":
		if not on:
			_clear_hover()
		places.visible = on
		_rebuild_path_art()
	elif layer == "paths":
		ink.paths_visible = on
	else:
		ink.marks_visible = on
	ink.queue_redraw()

func _select_place(node: Dictionary) -> void:
	title_label.text = str(node.name)
	var types := {"sect": "宗门", "city": "城邑", "town": "镇", "market": "坊市", "village": "村落", "dock": "渡口", "wild": "野外"}
	detail_label.text = types.get(node.get("type", "wild"), "野外")
	if node.get("resourceSite", false):
		detail_label.text += " · 资源产地"
	if node.has("owner"):
		detail_label.text += "\n归属：" + str(node.owner)

func _focus_place(place_name: String) -> void:
	_clear_hover()
	for node: Dictionary in data.get("nodes", []):
		if node.name == place_name:
			var center := _point(node.pos)
			if bool(presentation.get("nodesIntegrated",false)):
				for row: Dictionary in presentation.placements:
					if row.id == node.id and bool(row.get("hoverOnly",false)):
						center = Vector2(row.rect[0]+row.rect[2]*0.5,row.rect[1]+row.rect[3]*0.5)
						break
			camera_pos = center - get_viewport_rect().size * 0.5
			_update_camera()
			_select_place(node)
			return

func _update_camera() -> void:
	var viewport_size := get_viewport_rect().size
	var limits := (Vector2.ONE * WORLD_SIZE - viewport_size).max(Vector2.ZERO)
	camera_pos = camera_pos.clamp(Vector2.ZERO, limits).round()
	world.position = -camera_pos
	world.scale = Vector2.ONE
	if is_instance_valid(mini_frame):
		mini_frame.position = camera_pos / WORLD_SIZE * 180.0
		mini_frame.size = viewport_size.min(Vector2.ONE * WORLD_SIZE) / WORLD_SIZE * 180.0

func _mini_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		camera_pos = event.position.clamp(Vector2.ZERO, Vector2.ONE * 180.0) / 180.0 * WORLD_SIZE - get_viewport_rect().size * 0.5
		_update_camera()
		mini.accept_event()

func _input(event: InputEvent) -> void:
	if event is InputEventMagnifyGesture or event is InputEventPanGesture:
		get_viewport().set_input_as_handled()
	elif event is InputEventMouseButton and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN, MOUSE_BUTTON_WHEEL_LEFT, MOUSE_BUTTON_WHEEL_RIGHT]:
		get_viewport().set_input_as_handled()
	elif event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		get_viewport().set_input_as_handled()
		_return_to_menu()
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
		dragging = false

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		dragging = event.pressed
		get_viewport().set_input_as_handled()
	elif event is InputEventMouseMotion and dragging:
		camera_pos -= event.relative
		_update_camera()
		get_viewport().set_input_as_handled()

func _process(delta: float) -> void:
	# Input has no zoom command; also guard against accidental transform changes.
	world.scale = Vector2.ONE
	var axis := Vector2(float(Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_LEFT)), float(Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_UP)))
	if axis != Vector2.ZERO:
		camera_pos += axis.normalized() * 650.0 * delta
		_update_camera()

func _return_to_menu() -> void:
	get_tree().change_scene_to_file("res://tscn/scene/StartMenu.tscn")

func _exit_tree() -> void:
	if _scale_saved and is_instance_valid(get_window()):
		get_window().content_scale_mode = _old_mode
		get_window().content_scale_size = _old_size
		get_window().content_scale_factor = _old_factor
