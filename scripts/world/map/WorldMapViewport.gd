extends Control

signal node_double_clicked(node_id: String)
signal node_enter_requested(node_id: String)
signal faction_clicked(faction_id: String)
signal return_requested

# Runtime inspection switch: hide terrain tiles while validating the faction
# hierarchy. Roads and authored markers remain visible and editable.
@export var show_terrain_background: bool = false

# The mainland's 180-degree presentation is baked into the exported visual
# layers and the current Bai Chuan node positions. Runtime keeps the canvas
# axis-aligned so editor and runtime use the same asset directly.
const MAP_ROTATION_RADIANS: float = 0.0
# Keep the first view close enough to read a region while still requiring a
# player to pan across the continent. The minimum is intentionally above the
# full-map fit so the whole landmass cannot be seen at once. At the detail end,
# the camera cap is calculated so the visible map area is about one of the
# planned twelve terrain tiles (roughly 1/12 of the full map).
const DEFAULT_ZOOM: float = 1.38
const MAX_VISIBLE_MAP_FRACTION: float = 1.0 / 12.0
const FIRST_LEVEL_MARKER_SCREEN_WIDTH: float = 88.0
const SECOND_LEVEL_MARKER_SCREEN_WIDTH: float = 56.0
const FIRST_LEVEL_PICK_PADDING: float = 8.0
const CIRCLE_MARKER_TEXTURE_PATH := "res://textures/ui/world/world_marker_circle_ink.svg"
const RING_MARKER_TEXTURE_PATH := "res://textures/ui/world/world_marker_ring_ink.svg"
const WORLD_MAP_FONT_PATH := "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"
const BAI_CHUAN_CATALOG_PATH := "res://resources/world/bai_chuan/bai_chuan_second_level_catalog.json"

var map_data: Object
var map_root: Node2D
var camera: Camera2D
var location_layout: Node2D
var roads_layer: Node2D
var second_level_layout: Node2D
var faction_layout: Node2D
var background: Sprite2D
var state_layers: Node2D
var travelers_layer: Node2D
var overlay_layer: CanvasLayer
var overlay: Control
var title_label: Label
var help_label: Label
var hover_label: Label
var legend_label: Label
var preview_label: Label
var reset_button: Button
var back_button: Button
var enter_button: Button
var view_button: Button
var info_panel: PanelContainer
var info_title: Label
var info_body: RichTextLabel
var enter_node_id: String = ""
var selected_id: String = ""
var selected_route_id: String = ""
var current_id: String = ""
var target_id: String = ""
var population_counts: Dictionary = {}
var player_counts: Dictionary = {}
var location_nodes: Dictionary = {}
var traveler_labels: Dictionary = {}
var traveler_specs: Dictionary = {}
var _faction_scope_directions: Dictionary = {}
var _second_level_catalog: Dictionary = {}
var map_scale: float = 1.0
var zoom_min: float = 1.28
# Fallback before map data and viewport size are available. Once the map is
# built, _zoom_cap_for_map() derives the cap from the actual map aspect ratio.
var zoom_max: float = 3.5
var zoom_step: float = 1.12
var panning: bool = false
var pan_button: int = MOUSE_BUTTON_MIDDLE
var _last_manual_click_ms: int = -10000
var _last_size := Vector2.ZERO
var _view_initialized: bool = false
var _circle_marker_texture: Texture2D
var _ring_marker_texture: Texture2D
var _world_map_font: Font

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_PASS
	visibility_changed.connect(func() -> void: panning = false)
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	map_root = $MapRoot
	state_layers = $MapRoot/BaiChuanMapLayers
	background = state_layers.get_node_or_null("LandBase") as Sprite2D
	# The road layer lives inside the unified BaiChuanWorldMap scene so the same
	# authored geometry is visible/editable in the editor and used at runtime.
	roads_layer = state_layers.get_node_or_null("Roads") as Node2D
	location_layout = $MapRoot/LocationNodes
	second_level_layout = state_layers.get_node_or_null("SecondLevelNodes") as Node2D
	faction_layout = $MapRoot/FactionNodes
	_load_bai_chuan_scope_catalog()
	travelers_layer = $MapRoot/Travelers
	_circle_marker_texture = load(CIRCLE_MARKER_TEXTURE_PATH) as Texture2D
	_ring_marker_texture = load(RING_MARKER_TEXTURE_PATH) as Texture2D
	_world_map_font = _resolve_world_map_font()
	_apply_world_map_font_to_tree(map_root)
	_apply_terrain_visibility()
	_audit_faction_node_links()
	_ensure_population_badges()
	_ensure_faction_rings()
	_apply_circle_marker_theme()
	camera = $MapCamera
	map_root.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	if background != null:
		background.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	_connect_faction_nodes()
	# NodeWorldMap lives in a CanvasLayer. Keep Camera2D as the transform state
	# and apply it to the map canvas explicitly to avoid competing main cameras.
	camera.position_smoothing_enabled = false
	camera.enabled = false
	_build_overlay()
	resized.connect(_on_resized)
	call_deferred("_rebuild_map")

func _connect_faction_nodes() -> void:
	if faction_layout == null:
		return
	for faction in faction_layout.get_children():
		var hit_area := faction.get_node_or_null("HitArea")
		if hit_area != null and hit_area.has_signal("faction_clicked"):
			hit_area.faction_clicked.connect(_on_faction_clicked)
		if hit_area != null and hit_area.has_signal("faction_double_clicked"):
			hit_area.faction_double_clicked.connect(_on_faction_double_clicked)

func _on_faction_clicked(faction_id: String) -> void:
	var core_id := _faction_core_id(faction_id)
	if map_data != null and map_data.get_node(core_id) != null:
		_select_map_node(core_id)
		return
	faction_clicked.emit(faction_id)

func _on_faction_double_clicked(faction_id: String) -> void:
	var core_id := _faction_core_id(faction_id)
	if map_data != null and map_data.get_node(core_id) != null:
		_select_map_node(core_id)
		node_double_clicked.emit(core_id)
		return
	faction_clicked.emit(faction_id)

func _faction_core_id(faction_id: String) -> String:
	return faction_id + "_core"

func _select_map_node(id: String) -> void:
	selected_id = id
	selected_route_id = ""
	if roads_layer != null and roads_layer.has_method("clear_selected_route"):
		roads_layer.call("clear_selected_route")
	_refresh_location_states()
	_refresh_node_preview()

func _build_overlay() -> void:
	overlay_layer = get_node_or_null("MapOverlayLayer")
	if overlay_layer == null:
		overlay_layer = CanvasLayer.new()
		overlay_layer.name = "MapOverlayLayer"
		overlay_layer.layer = 5
		add_child(overlay_layer)
	overlay = Control.new()
	overlay.name = "MapOverlay"
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	overlay_layer.add_child(overlay)
	title_label = _label("百川区域 · 行旅图", 24, Color("f1dfad"))
	help_label = _label("滚轮缩放　中键拖动　单击选中后查看　双击沿路启程", 15, Color("e4d5b4"))
	hover_label = _label("", 16, Color("f1dfad"))
	hover_label.visible = false
	legend_label = _label("百川区域行旅图　·　单击选中并查看　·　双击沿最短道路启程", 13, Color("cfc3a8"))
	preview_label = _label("", 14, Color("f6e3b2"))
	preview_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	preview_label.add_theme_stylebox_override("normal", _box(Color(0.06, 0.055, 0.04, 0.88), Color(0.57, 0.46, 0.28, 0.78), 6))
	preview_label.visible = false
	reset_button = _button("重置视野", 15)
	reset_button.pressed.connect(reset_view)
	back_button = _button("返回青溪里", 15)
	back_button.pressed.connect(func(): return_requested.emit())
	enter_button = _button("进入", 15)
	enter_button.pressed.connect(_on_enter_button_pressed)
	enter_button.visible = false
	view_button = _button("查看", 15)
	view_button.pressed.connect(_on_view_button_pressed)
	view_button.visible = false
	_build_info_panel()
	for child in [title_label, help_label, hover_label, legend_label, preview_label, reset_button, back_button, enter_button, view_button]:
		overlay.add_child(child)
	_on_resized()

func _build_info_panel() -> void:
	info_panel = PanelContainer.new()
	info_panel.name = "NodeInfoSidePanel"
	info_panel.add_theme_stylebox_override("panel", _box(Color("171711"), Color("ad9258"), 8))
	info_panel.visible = false
	info_panel.mouse_filter = Control.MOUSE_FILTER_STOP
	var margin := MarginContainer.new()
	for side in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 18 if side in ["left", "right"] else 16)
	info_panel.add_child(margin)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 12)
	margin.add_child(column)
	var header := HBoxContainer.new()
	column.add_child(header)
	info_title = _label("", 22, Color("f1dfad"))
	info_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(info_title)
	var close_button := _button("关闭", 14)
	close_button.pressed.connect(func(): info_panel.visible = false)
	header.add_child(close_button)
	column.add_child(HSeparator.new())
	info_body = RichTextLabel.new()
	info_body.bbcode_enabled = true
	info_body.fit_content = true
	info_body.scroll_active = true
	info_body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	info_body.add_theme_color_override("default_color", Color("e4d5b4"))
	info_body.add_theme_font_size_override("normal_font_size", 15)
	column.add_child(info_body)
	overlay.add_child(info_panel)

func _on_view_button_pressed() -> void:
	if selected_id.is_empty() or map_data == null: return
	var node = map_data.get_node(selected_id)
	if node == null: return
	info_title.text = str(node.node_name)
	info_body.clear()
	_append_info_line("[color=#c9aa69]" + _node_level_name(int(node.display_level), selected_id) + " · " + _node_type_name(int(node.node_type)) + "[/color]")
	if not str(node.description).strip_edges().is_empty(): _append_info_line(str(node.description))
	if not str(node.terrain).strip_edges().is_empty(): _append_info_line("[color=#c9aa69]地貌[/color]　" + str(node.terrain))
	var territory_id := str(node.territory_id).strip_edges()
	if not territory_id.is_empty() and map_data.has_method("get_territory"):
		var territory = map_data.call("get_territory", territory_id)
		if territory != null and not str(territory.territory_name).strip_edges().is_empty():
			_append_info_line("[color=#c9aa69]所属区域[/color]　" + str(territory.territory_name))
	if selected_id.ends_with("_core"):
		var faction_id := selected_id.trim_suffix("_core")
		var domains := _faction_subdomain_names(faction_id)
		if not domains.is_empty(): _append_info_line("[color=#c9aa69]下辖辖域[/color]　" + "、".join(domains))
		var scope_direction := str(_faction_scope_directions.get(faction_id, ""))
		if not scope_direction.is_empty(): _append_info_line("[color=#c9aa69]范围方向[/color]　" + scope_direction)
	else:
		var domain_anchor := _second_level_anchor_for_node_id(selected_id)
		if domain_anchor == null and not str(node.faction_id).is_empty():
			domain_anchor = _second_level_anchor_for_node_id(str(node.faction_id))
		if domain_anchor != null:
			var domain_label := domain_anchor.get_node_or_null("Label") as Label
			var parent_id := str(domain_anchor.get_meta("parent_faction_id", ""))
			if domain_label != null:
				_append_info_line("[color=#c9aa69]所属辖域[/color]　" + domain_label.text)
			var domain_type := str(domain_anchor.get_meta("territory_type", ""))
			if not domain_type.is_empty(): _append_info_line("[color=#c9aa69]辖域类型[/color]　" + domain_type)
			var domain_id := str(domain_anchor.get_meta("second_level_id", ""))
			var domain_data: Dictionary = _second_level_catalog.get(domain_id, {})
			var scope_type := str(domain_data.get("scope_type", ""))
			var scope_description := str(domain_data.get("scope_description", ""))
			if not scope_type.is_empty(): _append_info_line("[color=#c9aa69]管理范围[/color]　" + scope_type)
			if not scope_description.is_empty(): _append_info_line("[color=#c9aa69]主要职能[/color]　" + scope_description)
			if not parent_id.is_empty(): _append_info_line("[color=#c9aa69]上级势力[/color]　" + _faction_display_name(parent_id))
	_append_info_line("[color=#c9aa69]人口[/color]　" + str(maxi(0, int(node.population))))
	_append_info_line("[color=#c9aa69]危险度[/color]　" + str(maxi(0, int(node.danger_level))))
	if selected_id != current_id and map_data.has_method("get_shortest_travel_days"):
		var days := int(map_data.call("get_shortest_travel_days", current_id, selected_id))
		_append_info_line("[color=#c9aa69]通行时间[/color]　" + ("最短 " + str(days) + " 日" if days > 0 else "暂无可通行路线"))
	info_panel.visible = true

func _append_info_line(value: String) -> void:
	if not info_body.text.is_empty(): info_body.append_text("\n\n")
	info_body.append_text(value)

func _node_type_name(value: int) -> String:
	var names := ["城镇", "宗门", "资源地", "遗迹", "战事地点", "村落", "居所", "野外区域"]
	return names[value] if value >= 0 and value < names.size() else "地图节点"

func _node_level_name(value: int, node_id: String) -> String:
	if node_id.ends_with("_core"): return "一级势力"
	if value == 2: return "二级辖域"
	return "地点"

func _second_level_anchor_for_node_id(id: String) -> Node2D:
	if second_level_layout == null or id.is_empty(): return null
	for faction_group in second_level_layout.get_children():
		for anchor in faction_group.get_children():
			if str(anchor.get_meta("second_level_id", "")) == id or str(anchor.get_meta("mapped_location_id", "")) == id:
				return anchor as Node2D
	return null

func _load_bai_chuan_scope_catalog() -> void:
	_faction_scope_directions.clear()
	_second_level_catalog.clear()
	if not FileAccess.file_exists(BAI_CHUAN_CATALOG_PATH): return
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(BAI_CHUAN_CATALOG_PATH))
	if not parsed is Dictionary: return
	for faction in parsed.get("factions", []):
		_faction_scope_directions[str(faction.get("id", ""))] = str(faction.get("scope_direction", ""))
	for domain in parsed.get("second_level", []):
		_second_level_catalog[str(domain.get("id", ""))] = domain

func _faction_display_name(id: String) -> String:
	var label := faction_layout.get_node_or_null(id + "/Label") as Label if faction_layout != null else null
	return label.text if label != null else id

func _faction_subdomain_names(id: String) -> Array[String]:
	var names: Array[String] = []
	var group := second_level_layout.get_node_or_null(id) if second_level_layout != null else null
	if group != null:
		for item in group.get_children():
			var label := item.get_node_or_null("Label") as Label
			if label != null and not label.text.strip_edges().is_empty(): names.append(label.text)
	return names

func _on_enter_button_pressed() -> void:
	if enter_node_id.is_empty() or map_data == null:
		return
	node_enter_requested.emit(enter_node_id)

func _label(text_value: String, size: int, color: Color) -> Label:
	var label := Label.new()
	label.text = text_value
	if _world_map_font != null:
		label.add_theme_font_override("font", _world_map_font)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label

func _button(text_value: String, size: int) -> Button:
	var button := Button.new()
	button.text = text_value
	if _world_map_font != null:
		button.add_theme_font_override("font", _world_map_font)
	button.add_theme_font_size_override("font_size", size)
	button.mouse_filter = Control.MOUSE_FILTER_STOP
	button.add_theme_stylebox_override("normal", _box(Color(0.12, 0.10, 0.07, 0.9), Color(0.57, 0.46, 0.28, 0.85), 6))
	button.add_theme_stylebox_override("hover", _box(Color(0.25, 0.20, 0.12, 0.94), Color(0.83, 0.66, 0.33, 0.95), 6))
	return button

func _resolve_world_map_font() -> Font:
	# Use the actual first-level faction label as the source of truth. This
	# keeps runtime-created text identical even if the scene theme changes.
	if faction_layout != null:
		var source_label := faction_layout.get_node_or_null("yuxu_xianzong/Label") as Label
		if source_label != null:
			var source_font := source_label.get_theme_font("font")
			if source_font != null:
				return source_font
	return load(WORLD_MAP_FONT_PATH) as Font

func _apply_world_map_font_to_tree(root: Node) -> void:
	if _world_map_font == null or root == null:
		return
	for child in root.get_children():
		if child is Label or child is Button:
			(child as Control).add_theme_font_override("font", _world_map_font)
			if child is Label:
				(child as Label).add_theme_color_override("font_outline_color", Color(0, 0, 0, 0))
				(child as Label).add_theme_constant_override("outline_size", 0)
		_apply_world_map_font_to_tree(child)

func _box(fill: Color, border: Color, radius: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = fill
	style.border_color = border
	style.set_border_width_all(1)
	style.set_corner_radius_all(radius)
	style.content_margin_left = 8
	style.content_margin_right = 8
	style.content_margin_top = 4
	style.content_margin_bottom = 4
	return style

func set_map_data(value: Object) -> void:
	map_data = value
	_view_initialized = false
	if is_node_ready():
		_rebuild_map()

func _rebuild_map() -> void:
	if map_data == null or map_root == null or location_layout == null:
		return
	_apply_terrain_visibility()
	_audit_faction_node_links()
	location_nodes.clear()
	_apply_circle_marker_theme()
	var map_size: Vector2 = map_data.map_size
	var texture: Texture2D = map_data.background_texture
	# 百川地图由十二张带物理高清纹理的瓦片组成。它们已经在场景中
	# 固定好逻辑坐标，不能再按旧的单张底图规则移动或缩放。
	if not _uses_tiled_map():
		if background != null:
			background.texture = texture
		_position_visual_layers(map_size, texture)
		if background != null:
			background.scale = _background_visual_scale(texture)
	if roads_layer != null and roads_layer.has_method("set_map_data"):
		roads_layer.call("set_map_data", map_data)
	_update_map_scale()
	if not _view_initialized:
		camera.zoom = Vector2.ONE * DEFAULT_ZOOM
		_view_initialized = true
	# Location nodes are authored in the world-map scene. The map resource only
	# supplies their static data and current state; it never creates map nodes.
	for child in location_layout.get_children():
		if not child.has_method("configure") or not child.has_method("set_state"):
			continue
		var authored_id := str(child.get("scene_node_id"))
		if authored_id.is_empty():
			push_warning("WorldMapViewport: 场景节点缺少 scene_node_id：" + child.name)
			continue
		var data = map_data.get_node(authored_id)
		if data == null:
			child.visible = false
			push_warning("WorldMapViewport: 场景节点没有对应地图数据：" + authored_id)
			continue
		child.visible = true
		child.configure(data, camera, map_scale)
		if not child.node_clicked.is_connected(_on_location_clicked):
			child.node_clicked.connect(_on_location_clicked)
		if not child.node_double_clicked.is_connected(_on_location_double_clicked):
			child.node_double_clicked.connect(_on_location_double_clicked)
		if not child.node_hovered.is_connected(_on_location_hovered):
			child.node_hovered.connect(_on_location_hovered)
		if not child.node_unhovered.is_connected(_on_location_unhovered):
			child.node_unhovered.connect(_on_location_unhovered)
		location_nodes[authored_id] = child
	if roads_layer != null and roads_layer.has_method("set_node_positions"):
		var authored_positions := {}
		for authored_id in location_nodes:
			authored_positions[authored_id] = (location_nodes[authored_id] as Node2D).position
		# 一级势力节点由单独的场景层绘制，但它们仍是路网中的真实端点。
		# 这样在编辑器中拖动势力锚点时，通往本部的道路也会跟随移动。
		if faction_layout != null:
			for faction in faction_layout.get_children():
				if faction is Node2D:
					authored_positions[str(faction.name) + "_core"] = (faction as Node2D).position
		roads_layer.call("set_node_positions", authored_positions)
	_refresh_location_states()
	_clamp_camera()
	_refresh_overlay()
	_queue_refresh()

func _layout_position_for(data: Object) -> Vector2:
	var marker = location_nodes.get(str(data.node_id))
	if marker is Node2D:
		return (marker as Node2D).position
	var node_id := str(data.node_id)
	if node_id.ends_with("_core") and faction_layout != null:
		var faction_id := node_id.trim_suffix("_core")
		var faction := faction_layout.get_node_or_null(faction_id)
		if faction is Node2D:
			return (faction as Node2D).position
	return data.map_position

func _background_visual_scale(texture: Texture2D) -> Vector2:
	# Static map layers now share the same full presented canvas. Keep the ratio
	# fallback for older map resources so a legacy crop can still register with
	# the terrain layer without changing logical map coordinates.
	if map_data != null and str(map_data.map_name) == "百川区域":
		var terrain := state_layers.get_node_or_null("Terrain") as Sprite2D
		if terrain != null and terrain.texture != null and texture != null:
			var source_size := texture.get_size()
			var target_size := terrain.texture.get_size()
			if source_size.x > 0.0 and source_size.y > 0.0:
				return Vector2(target_size.x / source_size.x, target_size.y / source_size.y)
	return Vector2.ONE

func _uses_tiled_map() -> bool:
	return state_layers != null and state_layers.get_node_or_null("TileR0C0") != null

func _position_visual_layers(map_size: Vector2, texture: Texture2D) -> void:
	"""Keep cropped visual layers aligned with the logical map coordinates."""
	if state_layers == null or _uses_tiled_map():
		return
	var origin := map_size * 0.5
	var visual_origin := state_layers.get_node_or_null("VisualOrigin")
	# A full-canvas asset is already centered on the logical map. Cropped
	# assets use the origin exported by the shared layer scene instead.
	if visual_origin != null and texture != null and texture.get_size() != map_size:
		origin = visual_origin.position
	for child in state_layers.get_children():
		if child is Sprite2D:
			child.position = origin

func _update_map_scale() -> void:
	if map_data == null:
		return
	var map_size: Vector2 = map_data.map_size
	if size.x <= 1 or size.y <= 1:
		map_scale = 1.0
	else:
		map_scale = minf(size.x / map_size.x, size.y / map_size.y)
	map_scale = maxf(map_scale, 0.001)
	map_root.scale = Vector2.ONE * map_scale
	var extent := map_size * map_scale
	if camera != null:
		camera.position = Vector2(extent.x * 0.5, extent.y * 0.5)
		camera.limit_left = 0
		camera.limit_top = 0
		camera.limit_right = int(extent.x)
		camera.limit_bottom = int(extent.y)
	_apply_map_transform()

func _apply_map_transform() -> void:
	if map_root == null or camera == null:
		return
	var zoom := maxf(camera.zoom.x, 0.01)
	map_root.scale = Vector2.ONE * map_scale * zoom
	map_root.rotation = MAP_ROTATION_RADIANS
	map_root.position = size * 0.5 - (camera.position * zoom).rotated(MAP_ROTATION_RADIANS)

func _on_resized() -> void:
	if size == _last_size:
		return
	_last_size = size
	if map_data != null:
		_update_map_scale()
		_refresh_location_states()
		_clamp_camera()
	_layout_overlay()
	_refresh_node_preview()

func _layout_overlay() -> void:
	if overlay == null:
		return
	title_label.position = Vector2(28, 18)
	title_label.size = Vector2(440, 34)
	help_label.position = Vector2(28, maxf(0, size.y - 38))
	help_label.size = Vector2(570, 24)
	hover_label.position = Vector2(maxf(28, size.x - 430), 24)
	hover_label.size = Vector2(400, 26)
	legend_label.position = Vector2(maxf(28, size.x - 510), maxf(0, size.y - 38))
	legend_label.size = Vector2(480, 24)
	back_button.position = Vector2(maxf(0, size.x - 178), 18)
	back_button.size = Vector2(150, 38)
	reset_button.position = Vector2(maxf(0, size.x - 300), 18)
	reset_button.size = Vector2(108, 38)
	if info_panel != null:
		var panel_width := minf(380.0, maxf(300.0, size.x * 0.34))
		info_panel.position = Vector2(size.x - panel_width - 20.0, 70.0)
		info_panel.size = Vector2(panel_width, maxf(240.0, size.y - 140.0))
	_refresh_enter_button()

func _refresh_location_states() -> void:
	_refresh_second_level_visibility()
	_refresh_fixed_marker_scales()
	_refresh_population_badges()
	_refresh_faction_states()
	for id in location_nodes:
		var marker = location_nodes[id]
		var data = marker.node_data
		var visible := _node_visible(data)
		marker.visible = visible
		marker.map_scale = map_scale
		marker.set_state(id == selected_id, id == current_id, id == target_id, int(population_counts.get(id, 0)))
		marker.set_player_present(int(player_counts.get(id, 0)) > 0)
	_refresh_enter_button()

func _refresh_enter_button() -> void:
	if enter_button == null:
		return
	enter_node_id = ""
	enter_button.visible = false
	if map_data == null or current_id.is_empty():
		return
	var node = map_data.get_node(current_id)
	if node == null or (bool(node.requires_unlock) and not bool(node.unlocked)) or not bool(node.enterable):
		return
	# 没有内部划分的野外空节点仍可作为道路中继，但不显示“进入”。
	# 有子场景的药谷、宗门外院等野外地点仍然可以进入。
	if int(node.node_type) == 7 and node.subscenes.is_empty():
		return
	enter_node_id = current_id
	var screen_pos := _map_to_screen(_layout_position_for(node))
	enter_button.size = Vector2(86, 34)
	enter_button.position = Vector2(
		clampf(screen_pos.x - 43.0, 8.0, maxf(8.0, size.x - 94.0)),
		clampf(screen_pos.y - 66.0, 8.0, maxf(8.0, size.y - 42.0)))
	enter_button.visible = true

func _refresh_faction_states() -> void:
	if faction_layout == null:
		return
	for faction in faction_layout.get_children():
		if not faction is Node2D:
			continue
		var core_id := _faction_core_id(str(faction.name))
		var landmark := faction.get_node_or_null("Landmark") as Sprite2D
		if landmark == null:
			continue
		var is_current := current_id == core_id
		var is_target := target_id == core_id
		var is_selected := selected_id == core_id
		var ring := faction.get_node_or_null("SelectionRing") as Sprite2D
		if ring != null:
			ring.visible = is_selected or is_current or is_target
			ring.modulate = Color("f1c967") if is_current else Color("c99852") if is_target else Color("ded0a6")
			var ring_width := FIRST_LEVEL_MARKER_SCREEN_WIDTH * (1.52 if is_current else 1.45 if is_target else 1.38)
			ring.scale = Vector2.ONE * (ring_width / maxf(ring.texture.get_size().x, 1.0))
		landmark.modulate = Color("f5d477") if is_current else Color("e1b969") if is_target else Color("fffdf4")
		var label := faction.get_node_or_null("Label") as Label
		if label != null:
			_configure_faction_label(label)

func _ensure_faction_rings() -> void:
	if faction_layout == null or _ring_marker_texture == null:
		return
	for faction in faction_layout.get_children():
		if not faction is Node2D:
			continue
		var ring := faction.get_node_or_null("SelectionRing") as Sprite2D
		if ring == null:
			ring = Sprite2D.new()
			ring.name = "SelectionRing"
			ring.centered = true
			ring.z_index = -1
			faction.add_child(ring)
		ring.texture = _ring_marker_texture
		ring.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		ring.visible = false

func _configure_faction_label(label: Label) -> void:
	var text_width := maxf(94.0, float(label.text.length()) * 21.0 + 22.0)
	label.position = Vector2(-text_width * 0.5, 46.0)
	label.size = Vector2(text_width, 34.0)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 17)
	label.add_theme_color_override("font_color", Color("1d1912"))
	label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0))
	label.add_theme_constant_override("outline_size", 0)
	label.add_theme_stylebox_override("normal", _box(Color(0.91, 0.88, 0.79, 0.96), Color(0.38, 0.31, 0.20, 0.58), 3))

func _ensure_population_badges() -> void:
	if faction_layout != null:
		for faction in faction_layout.get_children():
			if faction is Node2D:
				_create_population_badge(faction as Node2D, true)
	if second_level_layout != null:
		for faction in second_level_layout.get_children():
			if not faction is Node2D:
				continue
			for node in faction.get_children():
				if node is Node2D:
					_create_population_badge(node as Node2D, false)

func _create_population_badge(root: Node2D, is_faction: bool) -> Label:
	var badge := root.get_node_or_null("Population") as Label
	if badge == null:
		badge = Label.new()
		badge.name = "Population"
		root.add_child(badge)
	badge.position = Vector2(40, -58) if is_faction else Vector2(25, -40)
	badge.size = Vector2(48, 24)
	badge.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if _world_map_font != null:
		badge.add_theme_font_override("font", _world_map_font)
	badge.add_theme_font_size_override("font_size", 13 if is_faction else 11)
	badge.add_theme_color_override("font_color", Color("ffffff"))
	badge.add_theme_color_override("font_outline_color", Color(0.10, 0.08, 0.06, 0.96))
	badge.add_theme_constant_override("outline_size", 3)
	badge.add_theme_stylebox_override("normal", _box(Color(0.58, 0.16, 0.12, 0.96), Color(0.92, 0.76, 0.48, 0.95), 14))
	badge.add_theme_constant_override("content_margin_left", 4)
	badge.add_theme_constant_override("content_margin_right", 4)
	return badge

func _refresh_population_badges() -> void:
	if faction_layout != null:
		for faction in faction_layout.get_children():
			if not faction is Node2D:
				continue
			var badge := _create_population_badge(faction as Node2D, true)
			var count := 0
			var group := second_level_layout.get_node_or_null(str(faction.name)) if second_level_layout != null else null
			if group != null:
				for node in group.get_children():
					var mapped_id := str(node.get_meta("mapped_location_id", ""))
					if not mapped_id.is_empty():
						count += int(population_counts.get(mapped_id, 0))
			badge.text = str(count)
			badge.visible = true
	if second_level_layout != null:
		for faction in second_level_layout.get_children():
			if not faction is Node2D:
				continue
			for node in faction.get_children():
				if not node is Node2D:
					continue
				var badge := _create_population_badge(node as Node2D, false)
				var mapped_id := str(node.get_meta("mapped_location_id", ""))
				badge.text = str(population_counts.get(mapped_id, 0)) if not mapped_id.is_empty() else "0"
				badge.visible = true

func _refresh_second_level_visibility() -> void:
	if second_level_layout == null:
		return
	# 二级势力只在中近景出现，避免远景把百川地图压成一层文字；
	# 编辑器预览场景不经过这里，因此仍能完整检查和拖动全部节点。
	var zoom := camera.zoom.x if camera != null else 0.0
	# 底图隐藏时进入布局检查模式，强制显示全部二级节点，便于确认
	# 每个一级势力都有完整的辖域挂接；恢复底图后再回到正常门槛。
	var layer_visible := not show_terrain_background or zoom >= 1.80
	second_level_layout.visible = layer_visible
	for faction in second_level_layout.get_children():
		if not faction is Node2D:
			continue
		for node in faction.get_children():
			if not node is Node2D:
				continue
			# 已经挂到真实地点的二级势力只保留地点层的唯一圆形节点。
			# 政治层继续保留在场景里用于编辑坐标，但运行时隐藏，避免
			# 同一位置出现两枚印记和两个人数角标。
			if node.has_meta("mapped_location_id") and not str(node.get_meta("mapped_location_id", "")).is_empty():
				node.visible = false
				var mapped_badge := node.get_node_or_null("Population") as Label
				if mapped_badge != null:
					mapped_badge.visible = false
			else:
				node.visible = layer_visible

func _apply_terrain_visibility() -> void:
	if state_layers == null:
		return
	for child in state_layers.get_children():
		if not child is Sprite2D:
			continue
		var node_name := str(child.name)
		if node_name.begins_with("Tile") or node_name == "LandBase" or node_name == "Terrain":
			(child as CanvasItem).visible = show_terrain_background

func _refresh_fixed_marker_scales() -> void:
	if camera == null:
		return
	var inverse := 1.0 / (maxf(map_scale, 0.001) * maxf(camera.zoom.x, 0.01))
	_apply_circle_marker_theme()
	if faction_layout != null:
		for faction in faction_layout.get_children():
			if faction is Node2D:
				_normalize_marker_root(faction as Node2D, "Landmark", FIRST_LEVEL_MARKER_SCREEN_WIDTH, inverse)
	if second_level_layout != null:
		for faction in second_level_layout.get_children():
			if not faction is Node2D:
				continue
			for node in faction.get_children():
				if node is Node2D:
					_normalize_marker_root(node as Node2D, "Marker", SECOND_LEVEL_MARKER_SCREEN_WIDTH, inverse)

func _normalize_marker_root(root: Node2D, sprite_name: String, target_width: float, inverse: float) -> void:
	var sprite := root.get_node_or_null(sprite_name) as Sprite2D
	if sprite == null or sprite.texture == null:
		return
	if not root.has_meta("fixed_marker_base_scale"):
		root.set_meta("fixed_marker_base_scale", root.scale.x)
	var base_root_scale := float(root.get_meta("fixed_marker_base_scale", 1.0))
	var texture_width := maxf(sprite.texture.get_size().x, 1.0)
	# All political markers share one circle texture. Normalize its local width,
	# then cancel map scale/zoom on the parent so the screen size stays stable.
	sprite.scale = Vector2.ONE * (target_width / texture_width)
	root.scale = Vector2.ONE * base_root_scale * inverse

func _apply_circle_marker_theme() -> void:
	if _circle_marker_texture == null:
		return
	if faction_layout != null:
		for faction in faction_layout.get_children():
			if not faction is Node2D:
				continue
			var landmark := faction.get_node_or_null("Landmark") as Sprite2D
			if landmark != null:
				landmark.texture = _circle_marker_texture
				landmark.modulate = Color(1, 1, 1, 1)
				landmark.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	if second_level_layout != null:
		for faction in second_level_layout.get_children():
			if not faction is Node2D:
				continue
			for node in faction.get_children():
				if not node is Node2D:
					continue
				var marker := node.get_node_or_null("Marker") as Sprite2D
				if marker != null:
					marker.texture = _circle_marker_texture
					marker.modulate = Color(1, 1, 1, 1)
					marker.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS

func _audit_faction_node_links() -> void:
	if faction_layout == null or second_level_layout == null:
		return
	var missing: Array = []
	var seen_ids: Dictionary = {}
	var mapped_ids: Dictionary = {}
	var faction_count := 0
	var linked_count := 0
	for faction in faction_layout.get_children():
		if not faction is Node2D:
			continue
		faction_count += 1
		var faction_id := str(faction.name)
		var group := second_level_layout.get_node_or_null(faction_id)
		if group == null:
			missing.append(faction_id + "(缺少二级节点组)")
			continue
		var local_count := 0
		for node in group.get_children():
			if not node is Node2D:
				continue
			var second_level_id := str(node.get_meta("second_level_id", ""))
			var parent_id := str(node.get_meta("parent_faction_id", ""))
			if second_level_id.is_empty() or parent_id != faction_id:
				missing.append(faction_id + "/" + str(node.name) + "(归属元数据错误)")
				continue
			if seen_ids.has(second_level_id):
				missing.append(second_level_id + "(重复挂接)")
				continue
			seen_ids[second_level_id] = true
			var mapped_location_id := str(node.get_meta("mapped_location_id", ""))
			if mapped_location_id.is_empty():
				missing.append(second_level_id + "(缺少地图地点挂接)")
			elif mapped_ids.has(mapped_location_id):
				missing.append(mapped_location_id + "(地点被多个二级势力占用)")
			else:
				mapped_ids[mapped_location_id] = second_level_id
				if not _has_authored_location_id(mapped_location_id):
					missing.append(second_level_id + "(地图地点不存在:" + mapped_location_id + ")")
			local_count += 1
			linked_count += 1
		if local_count == 0:
			missing.append(faction_id + "(没有二级节点)")
	if missing.is_empty():
		print("bai_chuan_faction_links_ok factions=%d second_level=%d" % [faction_count, linked_count])
	else:
		push_warning("WorldMapViewport: 一级势力节点挂接检查失败：" + ", ".join(missing))

func _has_authored_location_id(location_id: String) -> bool:
	if location_layout == null or location_id.is_empty():
		return false
	for location in location_layout.get_children():
		if str(location.get("scene_node_id")) == location_id:
			return true
	return false

func _node_visible(data: Object) -> bool:
	if bool(data.requires_unlock) and not bool(data.unlocked):
		return false
	var zoom := camera.zoom.x if camera != null else 1.0
	var level := int(data.display_level)
	var threshold := 0.58
	if level == 2:
		threshold = 0.7
	elif level == 3:
		threshold = 0.78
	elif level >= 4:
		threshold = 1.2
	if str(data.node_id) == current_id or str(data.node_id) == target_id or str(data.node_id) == selected_id:
		return true
	return zoom >= maxf(float(data.min_zoom), threshold) and (float(data.max_zoom) < 0 or zoom <= float(data.max_zoom))

func _on_location_clicked(id: String) -> void:
	selected_id = id
	selected_route_id = ""
	if roads_layer != null and roads_layer.has_method("clear_selected_route"):
		roads_layer.call("clear_selected_route")
	_refresh_location_states()
	_refresh_node_preview()

func _on_location_double_clicked(id: String) -> void:
	selected_id = id
	selected_route_id = ""
	if roads_layer != null and roads_layer.has_method("clear_selected_route"):
		roads_layer.call("clear_selected_route")
	_refresh_location_states()
	_refresh_node_preview()
	node_double_clicked.emit(id)

func _on_location_hovered(id: String) -> void:
	var marker = location_nodes.get(id)
	if marker == null:
		return
	hover_label.text = "地点：" + str(marker.node_data.node_name)
	hover_label.visible = true

func _on_location_unhovered(id: String) -> void:
	if hover_label.text == "地点：" + str(location_nodes.get(id).node_data.node_name if location_nodes.get(id) != null else ""):
		hover_label.visible = false

func _gui_input(event: InputEvent) -> void:
	# Hidden compatibility maps must not intercept another page's wheel.
	# GUI dispatch also gives overlaid lists and panels priority over this map.
	if not is_visible_in_tree():
		panning = false
		return
	if event is InputEventMouseButton:
		var mouse := event as InputEventMouseButton
		if mouse.button_index == MOUSE_BUTTON_WHEEL_UP and mouse.pressed:
			_apply_zoom(zoom_step, mouse.position)
			get_viewport().set_input_as_handled()
		elif mouse.button_index == MOUSE_BUTTON_WHEEL_DOWN and mouse.pressed:
			_apply_zoom(1.0 / zoom_step, mouse.position)
			get_viewport().set_input_as_handled()
		elif mouse.button_index == MOUSE_BUTTON_MIDDLE:
			panning = mouse.pressed
			pan_button = mouse.button_index
			get_viewport().set_input_as_handled()
	elif event is InputEventMouseMotion and panning:
		var motion := event as InputEventMouseMotion
		camera.position -= motion.relative.rotated(-MAP_ROTATION_RADIANS) / maxf(camera.zoom.x, 0.01)
		_clamp_camera()
		_apply_map_transform()
		_queue_refresh()
		get_viewport().set_input_as_handled()

func _apply_zoom(factor: float, screen_position: Vector2) -> void:
	if camera == null:
		return
	var old_zoom := camera.zoom.x
	var new_zoom := clampf(old_zoom * factor, zoom_min, _zoom_cap_for_map())
	if is_equal_approx(old_zoom, new_zoom):
		return
	var before := camera.position + (screen_position - size * 0.5).rotated(-MAP_ROTATION_RADIANS) / old_zoom
	camera.zoom = Vector2.ONE * new_zoom
	camera.position = before - (screen_position - size * 0.5).rotated(-MAP_ROTATION_RADIANS) / new_zoom
	_clamp_camera()
	_apply_map_transform()
	_refresh_location_states()
	_refresh_node_preview()
	_queue_refresh()

func _zoom_cap_for_map() -> float:
	if map_data == null or size.x <= 1.0 or size.y <= 1.0 or map_scale <= 0.0:
		return zoom_max
	var map_size: Vector2 = map_data.map_size
	var map_area_in_view_units := maxf(map_scale * map_scale * map_size.x * map_size.y, 0.001)
	var baseline_visible_fraction := (size.x * size.y) / map_area_in_view_units
	# visible_fraction = baseline_visible_fraction / zoom^2
	# Solving for the requested 1/12 detail window gives the cap below.
	return maxf(zoom_min, sqrt(baseline_visible_fraction / MAX_VISIBLE_MAP_FRACTION))

func _clamp_camera() -> void:
	if camera == null or map_data == null:
		return
	var extent: Vector2 = map_data.map_size * map_scale
	var half := size * 0.5 / maxf(camera.zoom.x, 0.01)
	var min_center := half
	var max_center := extent - half
	var center := extent * 0.5
	if max_center.x < min_center.x:
		camera.position.x = center.x
	else:
		camera.position.x = clampf(camera.position.x, min_center.x, max_center.x)
	if max_center.y < min_center.y:
		camera.position.y = center.y
	else:
		camera.position.y = clampf(camera.position.y, min_center.y, max_center.y)
	_apply_map_transform()

func reset_view() -> void:
	if map_data == null:
		return
	_update_map_scale()
	camera.zoom = Vector2.ONE * DEFAULT_ZOOM
	_view_initialized = true
	camera.position = map_data.map_size * map_scale * 0.5
	_apply_map_transform()
	selected_id = ""
	selected_route_id = ""
	target_id = ""
	if roads_layer != null and roads_layer.has_method("clear_selected_route"):
		roads_layer.call("clear_selected_route")
	_refresh_location_states()
	_refresh_node_preview()

func select_node(id: String) -> void:
	selected_id = id
	selected_route_id = ""
	if roads_layer != null and roads_layer.has_method("clear_selected_route"):
		roads_layer.call("clear_selected_route")
	_refresh_location_states()
	_refresh_node_preview()

func show_node_preview(id: String) -> void:
	select_node(id)

func set_player_location(id: String) -> void:
	current_id = id
	_refresh_location_states()
	_refresh_overlay()
	_refresh_node_preview()

func set_target_node(id: String) -> void:
	target_id = id
	_refresh_location_states()
	_refresh_overlay()

func clear_target_node() -> void:
	target_id = ""
	_refresh_location_states()
	_refresh_overlay()

func set_population_counts(values: Dictionary) -> void:
	population_counts = values.duplicate(true)
	_refresh_location_states()

func set_player_counts(values: Dictionary) -> void:
	player_counts = values.duplicate(true)
	_refresh_location_states()

func set_return_button_text(text_value: String) -> void:
	back_button.text = text_value

func set_player_travelers(values: Array) -> void:
	for label in traveler_labels.values():
		label.queue_free()
	traveler_labels.clear()
	traveler_specs.clear()
	for entry in values:
		var path_value = entry.get("path", [])
		if path_value is Array and path_value.size() >= 2:
			_add_traveler_path(str(entry.get("label", "我方队伍")), path_value, float(entry.get("progress", 0.0)))
		else:
			_add_traveler(str(entry.get("label", "我方队伍")), str(entry.get("from", "")), str(entry.get("to", "")), float(entry.get("progress", 0.0)))

func _add_traveler(label_text: String, from_id: String, to_id: String, progress: float) -> void:
	if map_data == null:
		return
	var from = map_data.get_node(from_id)
	var to = map_data.get_node(to_id)
	if from == null or to == null:
		return
	if _route_points_between(from_id, to_id).size() < 2:
		return
	var label := _label("● " + label_text, 12, Color("e8c56f"))
	label.name = "Traveler_" + label_text
	label.position = _traveler_map_position(from_id, to_id, progress) - Vector2(30, 10)
	travelers_layer.add_child(label)
	traveler_labels[label_text] = label
	traveler_specs[label_text] = {"from": from_id, "to": to_id, "progress": progress}

func _add_traveler_path(label_text: String, node_ids: Array, progress: float) -> void:
	var path_points := _path_points_between(node_ids)
	if path_points.size() < 2:
		return
	var label := _label("● " + label_text, 12, Color("e8c56f"))
	label.name = "Traveler_" + label_text
	label.position = _point_on_polyline(path_points, clampf(progress, 0.0, 1.0)) - Vector2(30, 10)
	travelers_layer.add_child(label)
	traveler_labels[label_text] = label
	traveler_specs[label_text] = {"path_points": path_points, "progress": progress}

func add_traveler(label_text: String, from_id: String, to_id: String, progress: float = 0.0) -> void:
	_add_traveler(label_text, from_id, to_id, progress)

func update_traveler(label_text: String, progress: float) -> void:
	if not traveler_labels.has(label_text) or map_data == null or not traveler_specs.has(label_text):
		return
	var spec: Dictionary = traveler_specs[label_text]
	if spec.has("path_points"):
		spec["progress"] = progress
		traveler_labels[label_text].position = _point_on_polyline(spec["path_points"], clampf(progress, 0.0, 1.0)) - Vector2(30, 10)
		return
	var from = map_data.get_node(str(spec.get("from", "")))
	var to = map_data.get_node(str(spec.get("to", "")))
	if from == null or to == null:
		return
	spec["progress"] = progress
	traveler_labels[label_text].position = _traveler_map_position(str(spec.get("from", "")), str(spec.get("to", "")), progress) - Vector2(30, 10)

func animate_traveler(label_text: String, from_id: String, to_id: String, duration: float = 1.0) -> void:
	"""让地图上的队伍标记沿道路折线移动，而不是在节点间走直线。"""
	if _route_points_between(from_id, to_id).size() < 2:
		return
	_add_traveler(label_text, from_id, to_id, 0.0)
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_method(func(value: float): update_traveler(label_text, value), 0.0, 1.0, maxf(0.1, duration))
	tween.finished.connect(func(): remove_traveler(label_text))

func animate_traveler_path(label_text: String, node_ids: Array, duration: float = 1.0) -> void:
	"""让队伍沿最短路径的多段道路连续移动。"""
	var path_points := _path_points_between(node_ids)
	if path_points.size() < 2:
		return
	_add_traveler_path(label_text, node_ids, 0.0)
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_method(func(value: float): update_traveler(label_text, value), 0.0, 1.0, maxf(0.1, duration))
	tween.finished.connect(func(): remove_traveler(label_text))

func remove_traveler(label_text: String) -> void:
	if traveler_labels.has(label_text):
		traveler_labels[label_text].queue_free()
		traveler_labels.erase(label_text)
		traveler_specs.erase(label_text)

func _route_points_between(from_id: String, to_id: String) -> PackedVector2Array:
	if roads_layer != null and roads_layer.has_method("points_between"):
		return roads_layer.call("points_between", from_id, to_id)
	return PackedVector2Array()

func _path_points_between(node_ids: Array) -> PackedVector2Array:
	var points := PackedVector2Array()
	if node_ids.size() < 2:
		return points
	for index in range(node_ids.size() - 1):
		var segment := _route_points_between(str(node_ids[index]), str(node_ids[index + 1]))
		if segment.size() < 2:
			return PackedVector2Array()
		for point_index in range(segment.size()):
			if not points.is_empty() and point_index == 0:
				continue
			points.append(segment[point_index])
	return points

func _traveler_map_position(from_id: String, to_id: String, progress: float) -> Vector2:
	var points := _route_points_between(from_id, to_id)
	if points.size() < 2:
		return Vector2.ZERO
	return _point_on_polyline(points, clampf(progress, 0.0, 1.0))

func _point_on_polyline(points: PackedVector2Array, progress: float) -> Vector2:
	var total_length := 0.0
	for index in range(points.size() - 1):
		total_length += points[index].distance_to(points[index + 1])
	if total_length <= 0.001:
		return points[0]
	var remaining := total_length * progress
	for index in range(points.size() - 1):
		var segment := points[index].distance_to(points[index + 1])
		if remaining <= segment:
			return points[index].lerp(points[index + 1], remaining / maxf(segment, 0.001))
		remaining -= segment
	return points[points.size() - 1]

func select_route(id: String) -> void:
	selected_route_id = id
	selected_id = ""
	if roads_layer != null and roads_layer.has_method("set_selected_route"):
		roads_layer.call("set_selected_route", id)
	_refresh_location_states()
	_refresh_node_preview()

func select_territory(_id: String) -> void:
	return

func refresh_map() -> void:
	_rebuild_map()

func _unhandled_input(event: InputEvent) -> void:
	if not is_visible_in_tree() or not event is InputEventMouseButton:
		return
	var mouse := event as InputEventMouseButton
	if mouse.button_index != MOUSE_BUTTON_LEFT or not mouse.pressed:
		return
	# 普通地点由 LocationNodes 的 Area2D 处理；一级势力是独立的场景锚点，
	# 不能只依赖它们的碰撞回调。这里用同一套屏幕坐标拾取作兜底，保证
	# 点击一级势力大圆节点时仍然走统一的选中、耗时预览和双击出发流程。
	var id := _pick_location(mouse.position)
	if id.is_empty():
		id = _pick_faction(mouse.position)
	if id == "":
		var route_id := _pick_route(mouse.position)
		if not route_id.is_empty():
			select_route(route_id)
			get_viewport().set_input_as_handled()
			return
		selected_id = ""
		selected_route_id = ""
		if roads_layer != null and roads_layer.has_method("clear_selected_route"):
			roads_layer.call("clear_selected_route")
		_refresh_location_states()
		_refresh_node_preview()
		return
	var now := Time.get_ticks_msec()
	var is_double := mouse.double_click or (now - _last_manual_click_ms <= 330 and id == selected_id)
	_last_manual_click_ms = now
	if is_double:
		_on_location_double_clicked(id)
	else:
		_on_location_clicked(id)
	get_viewport().set_input_as_handled()

func _pick_location(screen_position: Vector2) -> String:
	if map_data == null or map_scale <= 0.0 or camera == null:
		return ""
	var zoom := maxf(camera.zoom.x, 0.01)
	var scaled_position := camera.position + (screen_position - size * 0.5).rotated(-MAP_ROTATION_RADIANS) / zoom
	var map_position := scaled_position / map_scale
	var nearest_id := ""
	var nearest_distance := INF
	for id in location_nodes:
		var marker = location_nodes[id]
		if not marker.visible or marker.node_data == null:
			continue
		var data = marker.node_data
		var distance := map_position.distance_to(_layout_position_for(data))
		var screen_radius: float = float(marker.get_screen_radius()) if marker.has_method("get_screen_radius") else (13.0 if int(data.node_type) == 7 else 18.0)
		var radius: float = screen_radius / (map_scale * zoom)
		if distance <= radius and distance < nearest_distance:
			nearest_distance = distance
			nearest_id = str(data.node_id)
	return nearest_id

func _pick_faction(screen_position: Vector2) -> String:
	if faction_layout == null or map_data == null or map_scale <= 0.0 or camera == null:
		return ""
	var map_position := _screen_to_map(screen_position)
	var zoom := maxf(camera.zoom.x, 0.01)
	var screen_radius := FIRST_LEVEL_MARKER_SCREEN_WIDTH * 0.5 + FIRST_LEVEL_PICK_PADDING
	var logical_radius := screen_radius / (map_scale * zoom)
	var nearest_id := ""
	var nearest_distance := INF
	for faction in faction_layout.get_children():
		if not faction is Node2D or not (faction as Node2D).visible:
			continue
		var faction_id := str(faction.name)
		var core_id := _faction_core_id(faction_id)
		if map_data.get_node(core_id) == null:
			continue
		var distance := map_position.distance_to((faction as Node2D).position)
		if distance <= logical_radius and distance < nearest_distance:
			nearest_distance = distance
			nearest_id = core_id
	return nearest_id

func _pick_route(screen_position: Vector2) -> String:
	if roads_layer == null or not roads_layer.has_method("route_at") or camera == null or map_scale <= 0.0:
		return ""
	var map_position := _screen_to_map(screen_position)
	var zoom := maxf(camera.zoom.x, 0.01)
	var tolerance := 22.0 / (map_scale * zoom)
	return str(roads_layer.call("route_at", map_position, tolerance))

func _refresh_overlay() -> void:
	# 地图顶部不再显示当前位置或远行目标，避免与节点和其他界面元素重叠。
	return

func _refresh_node_preview() -> void:
	if selected_id == "" or map_data == null:
		preview_label.visible = false
		view_button.visible = false
		return
	var node = map_data.get_node(selected_id)
	if node == null:
		preview_label.visible = false
		view_button.visible = false
		return
	var marker = location_nodes.get(selected_id)
	if marker != null and not marker.visible:
		preview_label.visible = false
		view_button.visible = false
		return
	var days := 0
	if selected_id != current_id and map_data.has_method("get_shortest_travel_days"):
		days = int(map_data.call("get_shortest_travel_days", current_id, selected_id))
	preview_label.text = "当前位置" if selected_id == current_id else "最短需 " + str(days) + " 日" if days > 0 else "暂无可通行路线"
	var point := _map_to_screen(_layout_position_for(node))
	preview_label.size = Vector2(160, 28)
	preview_label.position = Vector2(clampf(point.x - 80, 8, maxf(8, size.x - 168)), maxf(8, point.y - 70))
	preview_label.visible = true
	view_button.size = Vector2(78, 32)
	view_button.position = Vector2(clampf(point.x - 39, 8, maxf(8, size.x - 86)), maxf(8, point.y - 108))
	view_button.visible = true

func _map_to_screen(map_position: Vector2) -> Vector2:
	var parent_position := map_position * map_scale
	return size * 0.5 + ((parent_position - camera.position) * camera.zoom.x).rotated(MAP_ROTATION_RADIANS)

func _screen_to_map(screen_position: Vector2) -> Vector2:
	if camera == null or map_scale <= 0.0:
		return Vector2.ZERO
	var zoom := maxf(camera.zoom.x, 0.01)
	var parent_position := camera.position + (screen_position - size * 0.5).rotated(-MAP_ROTATION_RADIANS) / zoom
	return parent_position / map_scale

func _queue_refresh() -> void:
	_refresh_location_states()
	_refresh_node_preview()
