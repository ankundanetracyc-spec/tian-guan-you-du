extends Control

signal return_requested
signal cell_selected(cell: Vector2i)
signal player_cell_changed(cell: Vector2i)
signal player_travel_started
signal player_travel_finished(completed_steps: int)
signal node_enter_requested(node_id: String)
signal world_state_changed

const GRID_COLUMNS := 200
const GRID_ROWS := 200
const TILE_SIZE := 256
const APPROX_KILOMETERS_PER_CELL := 40.0
const INITIAL_FOCUS_CELL := Vector2i(38, 166)
const SETTINGS_PATH := "user://world_map_settings.cfg"
const WORLD_ANCHORS_PATH := "res://resources/world/bai_chuan/bai_chuan_grid_anchors.json"
const UI_FONT := preload("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf")
const PLAYER_MARKER_TEXTURE := preload("res://textures/ui/world/player_pawn_marker_v6.tres")
const DESTINATION_MARKER_TEXTURE := preload("res://textures/ui/world/world_map_destination_flag_ink_v1.png")
const ROUTE_FOOTPRINT_TEXTURE := preload("res://textures/ui/world/world_map_route_cloth_shoeprint_umber_v1.svg")
const ROUTE_FOOTPRINT_LAYER_SCRIPT := preload("res://gdscripts/world/WorldMapFootprintRoute.gd")
const PLACEHOLDER_MARKER_TEXTURE := preload("res://textures/ui/world/world_marker_circle_ink.svg")
const PRIMARY_MARKER_RING_TEXTURE := preload("res://textures/ui/world/world_marker_ring_ink.svg")
const MEMORY_MARKER_TEXTURE := preload("res://textures/ui/world/world_marker_empty_ink.svg")
const PLAYER_MARKER_DISPLAY_HEIGHT := TILE_SIZE / 2.0
const DESTINATION_MARKER_DISPLAY_HEIGHT := 100.0
const DESTINATION_MARKER_POLE_X_RATIO := 0.5
const DESTINATION_MARKER_MAX_CELL_RATIO := 0.42
const WORLD_OVERLAY_Z := 10
# Relative to WorldMapOverlay: terrain/decorations stay below fog; travel
# markers stay above fog. Screen controls use z=130 in the same canvas.
const FOG_OVERLAY_Z := 0
const ANCHOR_MARKER_Z := 3
const POPULATION_BADGE_Z := 4
const ROUTE_OVERLAY_Z := 5
const SELECTION_MARKER_Z := 6
const PLAYER_MARKER_Z := 7
const MIN_ZOOM := 0.02
const MAX_ZOOM := 2.5
const ZOOM_STEP := 1.2
const PAN_DRAG_THRESHOLD := 6.0
const MOVE_ANIMATION_SECONDS := 0.16
const WATER_TRAVEL_COST := 2.0
const FOREST_TRAVEL_COST := 1.5
const MOUNTAIN_TRAVEL_COST := 2.5
const UNKNOWN_FOG_COLOR := Color(0.16, 0.18, 0.18, 0.96)
const EXPLORED_FOG_COLOR := Color(0.23, 0.25, 0.24, 0.43)
const CLEAR_FOG_COLOR := Color(1.0, 1.0, 1.0, 0.0)

@export_range(MIN_ZOOM, MAX_ZOOM, 0.005) var initial_zoom := 1.0
@export_range(1, 20, 1) var vision_radius_cells := 5
var dun_speed := 100.0
var _can_fly := false
var _forest_cells: Dictionary = {}
var _mountain_cells: Dictionary = {}

@onready var _map_canvas: Node2D = $MapCanvas
@onready var _map_root: Node2D = $MapCanvas/BaiChuanDualGridTerrainV1
@onready var _terrain: TileMapLayer = $MapCanvas/BaiChuanDualGridTerrainV1/PlainGround
@onready var _terrain_logic: TileMapLayer = $MapCanvas/BaiChuanDualGridTerrainV1/TerrainLogic
@onready var _grid_lines: TileMapLayer = $MapCanvas/BaiChuanDualGridTerrainV1/LogicalGridLines
@onready var _grid_toggle: Button = $GridToggle
@onready var _zoom_out: Button = $ZoomOut
@onready var _zoom_reset: Button = $ZoomReset
@onready var _zoom_in: Button = $ZoomIn
@onready var _cell_info_panel: PanelContainer = $SelectedCellInfo
@onready var _cell_info_label: Label = $SelectedCellInfo/CellInfoLabel
@onready var _enter_node_button: Button = $EnterNodeButton

var _selected_cell := Vector2i(-1, -1)
var _player_cell := INITIAL_FOCUS_CELL
var _show_grid_lines := true
var _user_zoom := 1.0
var _map_pan := Vector2.ZERO
var _pan_active := false
var _pan_dragged := false
var _pan_button := MOUSE_BUTTON_NONE
var _pan_press_position := Vector2.ZERO
var _last_pan_position := Vector2.ZERO
var _world_overlay: Node2D
var _selected_cell_marker: Sprite2D
var _destination_marker_display_size := Vector2.ZERO
var _route_footprints: Node2D
var _player_marker: Sprite2D
var _player_marker_map_position := Vector2.ZERO
var _fog_sprite: Sprite2D
var _fog_image: Image
var _fog_texture: ImageTexture
var _explored_cells := PackedByteArray()
var _visible_cell_indices: Array[int] = []
var _pathfinder: AStarGrid2D
var _npc_pathfinder: AStarGrid2D
var _preview_path: Array[Vector2i] = []
var _moving := false
var _active_travel_path: Array[Vector2i] = []
var _active_travel_next_index := 0
var _travel_reveal_bonus_cells := 0
var _pending_resume_path: Array[Vector2i] = []
var _world_anchors: Array[Dictionary] = []
var _anchors_by_cell: Dictionary = {}
var _anchor_markers: Array[Node2D] = []
var _population_counts: Dictionary = {}
var _grid_population_badges: Dictionary = {}
var _world_markers_prepared := false

func _ready() -> void:
	# The persistent world HUD must render above the opaque map paper layer.
	var world_map_layer := get_parent() as CanvasLayer
	if world_map_layer != null:
		world_map_layer.layer = 15
	# Keep the paper as a backing layer only. Control/Node2D draw ordering can
	# otherwise make the full-screen paper cover the terrain in the live scene.
	var paper_background := get_node_or_null("PaperBackground") as ColorRect
	if paper_background != null:
		paper_background.z_index = -100
	_map_canvas.z_index = 0
	# The terrain scene is reusable, but must never inherit weather/UI z-order.
	_map_root.z_as_relative = true
	_map_root.z_index = 0
	_map_canvas.show()
	_map_root.show()
	_terrain.show()
	for art_layer_name in ["GreenWashStamps", "NatureDecorations"]:
		var art_layer := _map_root.get_node_or_null(art_layer_name)
		if art_layer != null:
			art_layer.show()
	_create_world_overlay()
	var sample_camera := _map_root.get_node_or_null("SampleCamera") as Camera2D
	if sample_camera != null:
		sample_camera.enabled = false
	var sample_controls := _map_root.get_node_or_null("SampleControls") as CanvasLayer
	if sample_controls != null:
		sample_controls.visible = false
	_map_root.set_process_unhandled_input(false)
	_create_selection_marker()
	_create_fog_layer()
	_create_route_layer()
	_load_world_anchors()
	_create_anchor_markers()
	_collect_slow_terrain_cells()
	_create_player_marker()
	_build_pathfinder()
	_reveal_around_player()
	_user_zoom = clampf(initial_zoom, _minimum_zoom_for_view(), MAX_ZOOM)
	mouse_filter = Control.MOUSE_FILTER_STOP
	_enter_node_button.set_anchors_preset(Control.PRESET_TOP_LEFT, true)
	$ReturnButton.pressed.connect(func() -> void: return_requested.emit())
	_enter_node_button.pressed.connect(_on_enter_node_pressed)
	_grid_toggle.pressed.connect(_toggle_grid_lines)
	_zoom_out.pressed.connect(func() -> void: _zoom_about_view_center(_user_zoom / ZOOM_STEP))
	_zoom_reset.pressed.connect(_reset_view)
	_zoom_in.pressed.connect(func() -> void: _zoom_about_view_center(_user_zoom * ZOOM_STEP))
	_apply_button_style($ReturnButton)
	_apply_button_style(_grid_toggle)
	_apply_button_style(_zoom_out)
	_apply_button_style(_zoom_reset)
	_apply_button_style(_zoom_in)
	_apply_button_style(_enter_node_button)
	_apply_info_panel_style()
	_load_grid_preference()
	_apply_grid_preference()
	_center_on_player()
	_update_zoom_label()

func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED and is_node_ready():
		_layout_map()

func _process(_delta: float) -> void:
	if is_visible_in_tree() and _cell_info_panel.visible:
		_position_destination_info()

func _position_destination_info() -> void:
	if not _is_cell_in_bounds(_selected_cell) or _selected_cell == _player_cell:
		_cell_info_panel.hide()
		return
	var cell_center := _terrain.position + _terrain.map_to_local(_selected_cell)
	var destination := get_global_transform_with_canvas().affine_inverse() * (_map_canvas.get_global_transform_with_canvas() * cell_center)
	if not Rect2(Vector2.ZERO, size).has_point(destination):
		# Keep the hint attached to its destination; do not pin it to a screen edge.
		_cell_info_panel.modulate.a = 0.0
		return
	_cell_info_panel.modulate.a = 1.0
	_cell_info_panel.size = Vector2(minf(360.0, maxf(120.0, size.x - 24.0)), 0.0)
	var marker_height := _destination_marker_display_size.y
	if _selected_cell_marker != null:
		marker_height *= _selected_cell_marker.scale.y * _user_zoom
	var anchor: Dictionary = _anchors_by_cell.get(_selected_cell, {})
	var footprint: Array = anchor.get("footprint", [1, 1])
	var clearance := maxf(marker_height, float(footprint[1]) * TILE_SIZE * _user_zoom * 0.5 if not anchor.is_empty() else 0.0)
	var hint_position := destination - Vector2(_cell_info_panel.size.x * 0.5, clearance + _cell_info_panel.size.y + 10.0)
	hint_position.x = clampf(hint_position.x, 12.0, maxf(12.0, size.x - _cell_info_panel.size.x - 12.0))
	hint_position.y = maxf(12.0, hint_position.y)
	_cell_info_panel.position = hint_position

func _input(event: InputEvent) -> void:
	if not visible or not is_node_ready() or not event is InputEventMouseButton:
		return
	var mouse_event := event as InputEventMouseButton
	var local_event_position: Vector2 = get_global_transform_with_canvas().affine_inverse() * mouse_event.position
	if not mouse_event.pressed or not Rect2(Vector2.ZERO, size).has_point(local_event_position):
		return
	# _input precedes GUI scrolling: never steal the visible roster's wheel.
	for roster in get_tree().get_nodes_in_group("cell_character_list"):
		if roster.has_method("contains_roster_point") and roster.call("contains_roster_point", mouse_event.position):
			return
	match mouse_event.button_index:
		MOUSE_BUTTON_WHEEL_UP:
			_zoom_about_view_center(_user_zoom * ZOOM_STEP)
		MOUSE_BUTTON_WHEEL_DOWN:
			_zoom_about_view_center(_user_zoom / ZOOM_STEP)
		_:
			return
	get_viewport().set_input_as_handled()

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
			if _moving:
				accept_event()
				return
			var event_in_canvas: Vector2 = get_global_transform_with_canvas() * event.position
			var position_in_terrain: Vector2 = _terrain.get_global_transform_with_canvas().affine_inverse() * event_in_canvas
			var cell := _terrain.local_to_map(position_in_terrain)
			if cell.x >= 0 and cell.x < GRID_COLUMNS and cell.y >= 0 and cell.y < GRID_ROWS:
				_on_cell_clicked(cell)
				accept_event()
		elif event.button_index == MOUSE_BUTTON_RIGHT or event.button_index == MOUSE_BUTTON_MIDDLE:
			if event.pressed:
				if _moving:
					accept_event()
					return
				_pan_active = true
				_pan_dragged = false
				_pan_button = event.button_index
				_pan_press_position = event.position
				_last_pan_position = event.position
			elif _pan_active and _pan_button == event.button_index:
				var should_center_on_player := not _pan_dragged
				_pan_active = false
				_pan_dragged = false
				_pan_button = MOUSE_BUTTON_NONE
				if should_center_on_player:
					_center_on_player()
			accept_event()
			return
	elif event is InputEventMouseMotion:
		if _pan_active:
			if not _pan_dragged and event.position.distance_to(_pan_press_position) < PAN_DRAG_THRESHOLD:
				return
			_pan_dragged = true
			_map_pan += event.position - _last_pan_position
			_last_pan_position = event.position
			_layout_map()
			accept_event()

func _unhandled_key_input(event: InputEvent) -> void:
	if not visible or _moving or not event is InputEventKey:
		return
	var key_event := event as InputEventKey
	if not key_event.pressed:
		return
	var next_cell := _player_cell
	match key_event.keycode:
		KEY_W: next_cell.y -= 1
		KEY_A: next_cell.x -= 1
		KEY_S: next_cell.y += 1
		KEY_D: next_cell.x += 1
		_: return
	get_viewport().set_input_as_handled()
	if not _is_cell_in_bounds(next_cell) or not is_cell_traversable(next_cell):
		_cell_info_label.text = "前方地格不可通行。"
		_cell_info_panel.visible = true
		return
	_start_travel([_player_cell, next_cell])

func get_cell_terrain(cell: Vector2i) -> String:
	var tile_data := _terrain_logic.get_cell_tile_data(cell)
	var terrain_kind := "plain" if tile_data == null else str(tile_data.get_custom_data("terrain_kind"))
	if terrain_kind == "water":
		return "water"
	if terrain_kind == "blocked" or terrain_kind == "cliff":
		return "blocked"
	if terrain_kind == "mountain" or _mountain_cells.has(cell):
		return "mountain"
	if terrain_kind == "forest" or _forest_cells.has(cell):
		return "forest"
	return "plain"

func is_cell_traversable(cell: Vector2i) -> bool:
	if cell.x < 0 or cell.x >= GRID_COLUMNS or cell.y < 0 or cell.y >= GRID_ROWS:
		return false
	return get_cell_terrain(cell) != "blocked"

func get_cell_center(cell: Vector2i) -> Vector2:
	var point_in_canvas := _terrain.get_global_transform_with_canvas() * _terrain.map_to_local(cell)
	return get_global_transform_with_canvas().affine_inverse() * point_in_canvas

func get_player_cell() -> Vector2i:
	return _player_cell

func set_travel_reveal_bonus_cells(value: int) -> void:
	_travel_reveal_bonus_cells = clampi(value, 0, 3)

func is_traveling() -> bool:
	return _moving

func set_player_cell(cell: Vector2i) -> bool:
	if not _is_cell_in_bounds(cell) or not is_cell_traversable(cell) or _moving:
		return false
	_player_cell = cell
	_selected_cell = cell
	_player_marker_map_position = _terrain.position + _terrain.map_to_local(cell)
	_preview_path.clear()
	_update_route_points()
	_reveal_around_player()
	_update_selected_cell_marker()
	_map_pan = Vector2.ZERO
	_layout_map()
	_refresh_cell_info()
	player_cell_changed.emit(cell)
	world_state_changed.emit()
	return true

func get_persistent_state() -> Dictionary:
	return {
		"player_cell": _player_cell,
		"explored_cells": _explored_cells.duplicate(),
		"remaining_travel_path": get_remaining_travel_path(),
	}

func get_remaining_travel_path() -> Array[Vector2i]:
	if not _moving:
		return _pending_resume_path.duplicate()
	var result: Array[Vector2i] = [_player_cell]
	for index in range(_active_travel_next_index, _active_travel_path.size()):
		result.append(_active_travel_path[index])
	return result

func restore_persistent_state(cell: Vector2i, explored_cells: PackedByteArray, remaining_path: Array[Vector2i]) -> void:
	if not _is_cell_in_bounds(cell) or not is_cell_traversable(cell):
		push_warning("存档中的世界格坐标无效，沿用默认落点。")
		return
	_player_cell = cell
	_selected_cell = cell
	_player_marker_map_position = _terrain.position + _terrain.map_to_local(cell)
	if explored_cells.size() == GRID_COLUMNS * GRID_ROWS:
		_explored_cells = explored_cells.duplicate()
	else:
		_explored_cells = PackedByteArray()
		_explored_cells.resize(GRID_COLUMNS * GRID_ROWS)
	_pending_resume_path.clear()
	if remaining_path.size() > 1 and remaining_path[0] == cell:
		_pending_resume_path = remaining_path.duplicate()
	_build_pathfinder()
	_reveal_around_player()
	_preview_path.clear()
	_update_route_points()
	_update_selected_cell_marker()
	_map_pan = Vector2.ZERO
	_layout_map()
	_refresh_cell_info()
	world_state_changed.emit()

func resume_saved_travel() -> void:
	if _moving or _pending_resume_path.size() < 2:
		return
	var route := _pending_resume_path.duplicate()
	_pending_resume_path.clear()
	if route[0] != _player_cell:
		return
	_start_travel(route)

func set_dun_speed(value: float) -> void:
	dun_speed = maxf(0.01, value)
	if is_node_ready():
		_refresh_cell_info()

func set_can_fly(can_fly: bool) -> void:
	if _can_fly == can_fly:
		return
	_can_fly = can_fly
	_refresh_pathfinder_weights()
	if _is_cell_in_bounds(_selected_cell) and _is_cell_explored(_selected_cell) and _selected_cell != _player_cell:
		_preview_path = _find_shortest_path(_player_cell, _selected_cell)
		_update_route_points()
	_refresh_cell_info()

func get_location_node_id_at_cell(cell: Vector2i) -> String:
	var anchor: Dictionary = _anchors_by_cell.get(cell, {})
	return str(anchor.get("location_node_id", ""))

func get_location_anchor_cell(node_id: String) -> Vector2i:
	for anchor in _world_anchors:
		if str(anchor.get("location_node_id", "")) == node_id:
			var cell: Vector2i = anchor.get("cell", Vector2i(-1, -1))
			return cell
	return Vector2i(-1, -1)

func has_world_grid_anchor(node_id: String) -> bool:
	var cell := get_location_anchor_cell(node_id)
	return _is_cell_in_bounds(cell) and is_cell_traversable(cell)

func get_npc_grid_path(from_cell: Vector2i, to_cell: Vector2i) -> Array[Vector2i]:
	if _npc_pathfinder == null or not _is_cell_in_bounds(from_cell) or not _is_cell_in_bounds(to_cell):
		return []
	if not is_cell_traversable(from_cell) or not is_cell_traversable(to_cell):
		return []
	return _npc_pathfinder.get_id_path(from_cell, to_cell)

func get_npc_cell_travel_hours(cell: Vector2i, speed: float) -> float:
	if not _is_cell_in_bounds(cell):
		return INF
	return 2400.0 / maxf(0.01, speed) * _npc_terrain_travel_cost(cell)

func _layout_map() -> void:
	var focus_position := _terrain.position + _terrain.map_to_local(_player_cell)
	_map_canvas.position = _clamped_canvas_position(_view_center_local() + _map_pan - focus_position * _user_zoom, focus_position)
	_map_canvas.scale = Vector2.ONE * _user_zoom
	if not _moving:
		_player_marker_map_position = focus_position
	_update_player_marker_world_position()
	_update_screen_sized_markers()
	_update_route_points()
	_update_enter_node_button()

func _clamped_canvas_position(position_value: Vector2, focus_position: Vector2) -> Vector2:
	var result := position_value
	var map_size := Vector2(GRID_COLUMNS * TILE_SIZE, GRID_ROWS * TILE_SIZE) * _user_zoom
	var view_size := size
	var axes := ["x", "y"]
	for axis in axes:
		var map_extent := map_size.x if axis == "x" else map_size.y
		var view_extent := view_size.x if axis == "x" else view_size.y
		var map_start := result.x if axis == "x" else result.y
		if map_extent >= view_extent:
			map_start = clampf(map_start, view_extent - map_extent, 0.0)
		else:
			map_start = clampf(map_start, 0.0, view_extent - map_extent)
		if axis == "x":
			result.x = map_start
		else:
			result.y = map_start
	_map_pan = result - _view_center_local() + focus_position * _user_zoom
	return result

func _view_center_local() -> Vector2:
	var viewport_center := get_viewport_rect().size * 0.5
	return get_global_transform_with_canvas().affine_inverse() * viewport_center

func _update_player_marker_world_position() -> void:
	if _player_marker == null or not is_instance_valid(_player_marker):
		return
	_player_marker.position = _player_marker_map_position

func _set_player_marker_map_position(map_position: Vector2) -> void:
	_player_marker_map_position = map_position
	_update_player_marker_world_position()

func _zoom_about_view_center(target_zoom: float) -> void:
	var next_zoom := clampf(target_zoom, _minimum_zoom_for_view(), MAX_ZOOM)
	if is_equal_approx(next_zoom, _user_zoom):
		return
	var view_position := _view_center_local()
	var view_transform: Transform2D = get_global_transform_with_canvas()
	var anchor_in_canvas: Vector2 = view_transform * view_position
	var anchor_in_map: Vector2 = _map_canvas.get_global_transform_with_canvas().affine_inverse() * anchor_in_canvas
	_user_zoom = next_zoom
	_layout_map()
	var moved_anchor_in_canvas: Vector2 = _map_canvas.get_global_transform_with_canvas() * anchor_in_map
	var moved_anchor_in_view: Vector2 = view_transform.affine_inverse() * moved_anchor_in_canvas
	_map_pan += view_position - moved_anchor_in_view
	_layout_map()
	_update_zoom_label()

func _reset_view() -> void:
	_user_zoom = clampf(initial_zoom, _minimum_zoom_for_view(), MAX_ZOOM)
	_center_on_player()
	_update_zoom_label()

func _center_on_player() -> void:
	_map_pan = Vector2.ZERO
	_layout_map()

func _update_zoom_label() -> void:
	_zoom_reset.text = "%d%%" % roundi(_user_zoom * 100.0)

func _minimum_zoom_for_view() -> float:
	if size.x <= 0.0 or size.y <= 0.0:
		return MIN_ZOOM
	var fit_zoom := minf(size.x / float(GRID_COLUMNS * TILE_SIZE), size.y / float(GRID_ROWS * TILE_SIZE))
	return minf(MIN_ZOOM, fit_zoom)

func _refresh_cell_info() -> void:
	_update_enter_node_button()
	if not _is_cell_in_bounds(_selected_cell) or _selected_cell == _player_cell:
		_cell_info_panel.hide()
		return
	if not _is_cell_explored(_selected_cell):
		_cell_info_label.text = "未探索区域　｜　使用 WASD 逐格探路"
		_cell_info_panel.visible = true
		return
	var terrain := get_cell_terrain(_selected_cell)
	var terrain_names := {"plain": "平原", "water": "水域", "forest": "森林", "mountain": "山地", "blocked": "阻隔地形"}
	var terrain_name: String = terrain_names.get(terrain, "平原")
	var passability := "可通行" if is_cell_traversable(_selected_cell) else "不可通行"
	var anchor_name := _get_anchor_name(_selected_cell)
	var cell_summary := "%s · %s" % [terrain_name if anchor_name.is_empty() else anchor_name, passability]
	_cell_info_label.tooltip_text = "第 %d 列 · 第 %d 行 · %s" % [_selected_cell.x + 1, _selected_cell.y + 1, terrain_name]
	if _selected_cell == _player_cell:
		_cell_info_label.text = cell_summary + "\n当前位置"
	elif not _preview_path.is_empty():
		var hours := _path_travel_hours(_preview_path)
		var route_cells := maxi(0, _preview_path.size() - 1)
		var route_distance_km := float(route_cells) * APPROX_KILOMETERS_PER_CELL
		_cell_info_label.text = cell_summary + "\n%d 格 · 约 %s 公里 · 预计 %s\n再次点击启程" % [
			route_cells,
			_format_number(route_distance_km),
			_format_travel_time(hours)
		]
	else:
		_cell_info_label.text = cell_summary + "\n单击查看最短用时路线与预计时间"
	_cell_info_panel.visible = true
	_position_destination_info()

func _is_cell_in_bounds(cell: Vector2i) -> bool:
	return cell.x >= 0 and cell.x < GRID_COLUMNS and cell.y >= 0 and cell.y < GRID_ROWS

func _cell_index(cell: Vector2i) -> int:
	return cell.y * GRID_COLUMNS + cell.x

func _is_cell_explored(cell: Vector2i) -> bool:
	return _is_cell_in_bounds(cell) and _explored_cells.size() == GRID_COLUMNS * GRID_ROWS and _explored_cells[_cell_index(cell)] != 0

func _on_cell_clicked(cell: Vector2i) -> void:
	if cell == _player_cell:
		_selected_cell = cell
		_preview_path.clear()
		_update_route_points()
		_update_selected_cell_marker()
		_refresh_cell_info()
		cell_selected.emit(cell)
		return
	_enter_node_button.visible = false
	if not _is_cell_explored(cell):
		_selected_cell = cell
		_preview_path.clear()
		_update_route_points()
		_update_selected_cell_marker()
		_refresh_cell_info()
		return
	if not is_cell_traversable(cell):
		_selected_cell = cell
		_preview_path.clear()
		_update_route_points()
		_update_selected_cell_marker()
		_cell_info_label.text = "第 %d 列 · 第 %d 行　｜　%s · 不可通行" % [cell.x + 1, cell.y + 1, "水域" if get_cell_terrain(cell) == "water" else "地形"]
		_cell_info_panel.visible = true
		return
	if cell == _selected_cell and not _preview_path.is_empty():
		_start_travel(_preview_path.duplicate())
		return
	_selected_cell = cell
	_update_selected_cell_marker()
	_preview_path = _find_shortest_path(_player_cell, cell)
	_update_route_points()
	if _preview_path.is_empty():
		_cell_info_label.text = "该地格暂时没有已知可通行路线。"
		_cell_info_panel.visible = true
		return
	_refresh_cell_info()
	cell_selected.emit(cell)

func _build_pathfinder() -> void:
	_explored_cells.resize(GRID_COLUMNS * GRID_ROWS)
	_pathfinder = AStarGrid2D.new()
	_pathfinder.region = Rect2i(Vector2i.ZERO, Vector2i(GRID_COLUMNS, GRID_ROWS))
	_pathfinder.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	_pathfinder.default_compute_heuristic = AStarGrid2D.HEURISTIC_MANHATTAN
	_pathfinder.default_estimate_heuristic = AStarGrid2D.HEURISTIC_MANHATTAN
	_pathfinder.update()
	_npc_pathfinder = AStarGrid2D.new()
	_npc_pathfinder.region = Rect2i(Vector2i.ZERO, Vector2i(GRID_COLUMNS, GRID_ROWS))
	_npc_pathfinder.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	_npc_pathfinder.default_compute_heuristic = AStarGrid2D.HEURISTIC_MANHATTAN
	_npc_pathfinder.default_estimate_heuristic = AStarGrid2D.HEURISTIC_MANHATTAN
	_npc_pathfinder.update()
	for y in range(GRID_ROWS):
		for x in range(GRID_COLUMNS):
			var cell := Vector2i(x, y)
			# Previously discovered cells remain traversable after loading or leaving current sight.
			var explored := _explored_cells[_cell_index(cell)] != 0
			_pathfinder.set_point_solid(cell, not explored or not is_cell_traversable(cell))
			_pathfinder.set_point_weight_scale(cell, _terrain_travel_cost(cell))
			_npc_pathfinder.set_point_solid(cell, not is_cell_traversable(cell))
			_npc_pathfinder.set_point_weight_scale(cell, _npc_terrain_travel_cost(cell))
	_fog_image = Image.create(GRID_COLUMNS, GRID_ROWS, false, Image.FORMAT_RGBA8)
	_fog_image.fill(UNKNOWN_FOG_COLOR)
	_fog_texture = ImageTexture.create_from_image(_fog_image)

func _create_world_overlay() -> void:
	_world_overlay = Node2D.new()
	_world_overlay.name = "WorldMapOverlay"
	_world_overlay.z_as_relative = false
	_world_overlay.z_index = WORLD_OVERLAY_Z
	_map_canvas.add_child(_world_overlay)

func _create_fog_layer() -> void:
	_fog_sprite = Sprite2D.new()
	_fog_sprite.name = "ExplorationFog"
	_fog_sprite.centered = false
	_fog_sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_fog_sprite.z_index = FOG_OVERLAY_Z
	_fog_sprite.texture = _fog_texture
	_fog_sprite.scale = Vector2(TILE_SIZE, TILE_SIZE)
	_fog_sprite.position = _terrain.position + _terrain.map_to_local(Vector2i.ZERO) - Vector2.ONE * TILE_SIZE * 0.5
	_world_overlay.add_child(_fog_sprite)

func _create_route_layer() -> void:
	_route_footprints = ROUTE_FOOTPRINT_LAYER_SCRIPT.new() as Node2D
	_route_footprints.name = "RouteFootprints"
	_route_footprints.z_index = ROUTE_OVERLAY_Z
	_world_overlay.add_child(_route_footprints)
	_route_footprints.call("configure", _terrain, ROUTE_FOOTPRINT_TEXTURE)
	_route_footprints.call("refresh_view")

func _create_selection_marker() -> void:
	var marker_image := DESTINATION_MARKER_TEXTURE.get_image()
	var marker_bounds := marker_image.get_used_rect()
	if marker_bounds.size.x > 0 and marker_bounds.size.y > 0:
		marker_image = marker_image.get_region(marker_bounds)
	var resize_scale := DESTINATION_MARKER_DISPLAY_HEIGHT / float(marker_image.get_height())
	var marker_width := maxi(1, roundi(float(marker_image.get_width()) * resize_scale))
	var marker_height := int(DESTINATION_MARKER_DISPLAY_HEIGHT)
	marker_image.resize(marker_width, marker_height, Image.INTERPOLATE_LANCZOS)
	_destination_marker_display_size = Vector2(float(marker_width), float(marker_height))
	_selected_cell_marker = Sprite2D.new()
	_selected_cell_marker.name = "SelectedCellMarker"
	_selected_cell_marker.centered = false
	_selected_cell_marker.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	_selected_cell_marker.texture = ImageTexture.create_from_image(marker_image)
	_selected_cell_marker.z_index = SELECTION_MARKER_Z
	_selected_cell_marker.visible = false
	_world_overlay.add_child(_selected_cell_marker)

func _create_player_marker() -> void:
	var marker_image := PLAYER_MARKER_TEXTURE.get_image()
	var marker_bounds := marker_image.get_used_rect()
	if marker_bounds.size.x > 0 and marker_bounds.size.y > 0:
		marker_image = marker_image.get_region(marker_bounds)
	var marker_height := roundi(PLAYER_MARKER_DISPLAY_HEIGHT)
	var marker_width := maxi(1, roundi(float(marker_image.get_width()) * PLAYER_MARKER_DISPLAY_HEIGHT / float(marker_image.get_height())))
	marker_image.resize(marker_width, marker_height, Image.INTERPOLATE_LANCZOS)
	_player_marker = Sprite2D.new()
	_player_marker.name = "PlayerCellMarker"
	_player_marker.centered = true
	_player_marker.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	_player_marker.texture = ImageTexture.create_from_image(marker_image)
	_player_marker.offset = Vector2(0.0, -float(marker_height) * 0.5)
	_player_marker.z_index = PLAYER_MARKER_Z
	_player_marker_map_position = _terrain.position + _terrain.map_to_local(_player_cell)
	_world_overlay.add_child(_player_marker)
	_update_player_marker_world_position()

func _reveal_around_player() -> void:
	var reveal_radius := clampi(vision_radius_cells + _travel_reveal_bonus_cells, 1, 20)
	for index in _visible_cell_indices:
		var old_cell := Vector2i(index % GRID_COLUMNS, index / GRID_COLUMNS)
		# Every cell seen before remains known after it leaves the current view.
		_fog_image.set_pixel(old_cell.x, old_cell.y, EXPLORED_FOG_COLOR)
		if _pathfinder != null and is_cell_traversable(old_cell):
			_pathfinder.set_point_solid(old_cell, false)
	_visible_cell_indices.clear()
	for dy in range(-reveal_radius, reveal_radius + 1):
		for dx in range(-reveal_radius, reveal_radius + 1):
			if dx * dx + dy * dy > reveal_radius * reveal_radius:
				continue
			var cell := _player_cell + Vector2i(dx, dy)
			if not _is_cell_in_bounds(cell):
				continue
			var index := _cell_index(cell)
			_explored_cells[index] = 1
			_visible_cell_indices.append(index)
			_fog_image.set_pixel(cell.x, cell.y, CLEAR_FOG_COLOR)
			if _pathfinder != null:
				_pathfinder.set_point_solid(cell, not is_cell_traversable(cell))
	_fog_texture.update(_fog_image)
	if _fog_sprite != null:
		_fog_sprite.texture = _fog_texture
	_refresh_anchor_visibility()

func _find_shortest_path(from_cell: Vector2i, to_cell: Vector2i) -> Array[Vector2i]:
	if not _is_cell_in_bounds(from_cell) or not _is_cell_in_bounds(to_cell) or not _is_cell_explored(to_cell):
		return []
	if not is_cell_traversable(from_cell) or not is_cell_traversable(to_cell):
		return []
	return _pathfinder.get_id_path(from_cell, to_cell)

func _path_travel_hours(path: Array[Vector2i]) -> float:
	var hours := 0.0
	for index in range(1, path.size()):
		hours += _base_hours_per_cell() * _terrain_travel_cost(path[index])
	return hours

func _base_hours_per_cell() -> float:
	return 2400.0 / maxf(0.01, dun_speed)

func _terrain_travel_cost(cell: Vector2i) -> float:
	if _can_fly:
		return 1.0
	return _npc_terrain_travel_cost(cell)

func _npc_terrain_travel_cost(cell: Vector2i) -> float:
	match get_cell_terrain(cell):
		"water": return WATER_TRAVEL_COST
		"forest": return FOREST_TRAVEL_COST
		"mountain": return MOUNTAIN_TRAVEL_COST
		_: return 1.0

func _refresh_pathfinder_weights() -> void:
	if _pathfinder == null:
		return
	for y in range(GRID_ROWS):
		for x in range(GRID_COLUMNS):
			var cell := Vector2i(x, y)
			_pathfinder.set_point_weight_scale(cell, _terrain_travel_cost(cell))

func _collect_slow_terrain_cells() -> void:
	_forest_cells.clear()
	_mountain_cells.clear()
	var forest_root := _map_root.get_node_or_null("NatureDecorations/ForestPatches")
	if forest_root != null:
		for forest_node in forest_root.find_children("*", "Node2D", true, false):
			if not forest_node.has_meta("map_cell"):
				continue
			var cell: Vector2i = forest_node.get_meta("map_cell")
			if _is_cell_in_bounds(cell):
				_forest_cells[cell] = true
	var mountain_root := _map_root.get_node_or_null("NatureDecorations/MountainRanges")
	if mountain_root != null:
		for mountain in mountain_root.find_children("*", "Sprite2D", true, false):
			if not mountain.has_meta("footprint_dimensions"):
				continue
			var footprint: Vector2i = mountain.get_meta("footprint_dimensions")
			var center := _terrain.local_to_map(_terrain.to_local(mountain.to_global(Vector2.ZERO)))
			var start := center - Vector2i(floori(float(footprint.x) * 0.5), floori(float(footprint.y) * 0.5))
			for y in range(maxi(1, footprint.y)):
				for x in range(maxi(1, footprint.x)):
					var cell := start + Vector2i(x, y)
					if _is_cell_in_bounds(cell):
						_mountain_cells[cell] = true

func _format_travel_time(hours: float) -> String:
	if hours < 24.0:
		return "%s 小时" % _format_number(hours)
	var days := floorf(hours / 24.0)
	var remaining_hours := hours - days * 24.0
	if is_zero_approx(remaining_hours):
		return "%s 天" % _format_number(days)
	return "%s 天 %s 小时" % [_format_number(days), _format_number(remaining_hours)]

func _format_number(value: float) -> String:
	if is_equal_approx(value, roundf(value)):
		return str(roundi(value))
	var formatted := "%.2f" % value
	if formatted.ends_with("0"):
		formatted = formatted.trim_suffix("0")
	return formatted.trim_suffix(".")

func _start_travel(path: Array[Vector2i]) -> void:
	if _moving or path.size() < 2:
		return
	_moving = true
	player_travel_started.emit()
	_active_travel_path = path.duplicate()
	_active_travel_next_index = 1
	var completed_steps := 0
	world_state_changed.emit()
	_pan_active = false
	_pan_button = MOUSE_BUTTON_NONE
	_map_pan = Vector2.ZERO
	_cell_info_label.text = "正在循已知路线行进……"
	_cell_info_panel.visible = true
	for index in range(1, path.size()):
		var next_cell := path[index]
		if not is_cell_traversable(next_cell):
			break
		var next_position := _terrain.position + _terrain.map_to_local(next_cell)
		if next_cell.x != _player_cell.x:
			_player_marker.flip_h = next_cell.x < _player_cell.x
		var next_canvas_position := _clamped_canvas_position(_view_center_local() - next_position * _user_zoom, next_position)
		var tween := create_tween().set_parallel(true)
		tween.tween_method(_set_player_marker_map_position, _player_marker_map_position, next_position, MOVE_ANIMATION_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tween.tween_property(_map_canvas, "position", next_canvas_position, MOVE_ANIMATION_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		await tween.finished
		_player_cell = next_cell
		_player_marker_map_position = next_position
		_active_travel_next_index = index + 1
		await _advance_game_time(_base_hours_per_cell() * _terrain_travel_cost(next_cell))
		completed_steps += 1
		_reveal_around_player()
		player_cell_changed.emit(_player_cell)
		world_state_changed.emit()
		_map_pan = Vector2.ZERO
		_layout_map()
		if index + 1 < path.size():
			_preview_path = path.slice(index)
			_update_route_points()
	_selected_cell = _player_cell
	_preview_path.clear()
	_update_route_points()
	_update_selected_cell_marker()
	_moving = false
	_layout_map()
	_active_travel_path.clear()
	_active_travel_next_index = 0
	player_travel_finished.emit(completed_steps)
	_refresh_cell_info()
	world_state_changed.emit()

func _advance_game_time(hours: float) -> void:
	var time_manager := get_node_or_null("/root/GameTimeManager")
	if time_manager != null and time_manager.has_method("advance_time_smooth"):
		time_manager.call("advance_time_smooth", hours, "world_map_travel")
		await time_manager.smooth_time_advanced
	elif time_manager != null and time_manager.has_method("advance_time"):
		time_manager.call("advance_time", hours, "world_map_travel")

func _update_route_points() -> void:
	if _route_footprints == null:
		return
	_route_footprints.call("set_route", _preview_path)

func _update_selected_cell_marker() -> void:
	if _selected_cell_marker == null:
		return
	_selected_cell_marker.visible = _is_cell_in_bounds(_selected_cell) and _selected_cell != _player_cell
	if not _selected_cell_marker.visible:
		return
	var max_scale_by_cell := TILE_SIZE * DESTINATION_MARKER_MAX_CELL_RATIO / maxf(_destination_marker_display_size.x, _destination_marker_display_size.y)
	var marker_scale := minf(1.0 / maxf(_user_zoom, 0.01), max_scale_by_cell)
	var cell_center := _terrain.position + _terrain.map_to_local(_selected_cell)
	var pole_anchor := Vector2(_destination_marker_display_size.x * DESTINATION_MARKER_POLE_X_RATIO, _destination_marker_display_size.y)
	_selected_cell_marker.position = cell_center - pole_anchor * marker_scale
	_selected_cell_marker.scale = Vector2.ONE * marker_scale

func _update_screen_sized_markers() -> void:
	if _user_zoom <= 0.0:
		return
	if _route_footprints != null:
		_route_footprints.call("refresh_view")
	_update_selected_cell_marker()
	for marker in _anchor_markers:
		if is_instance_valid(marker):
			marker.scale = Vector2.ONE / _user_zoom
			var illustration := marker.get_node_or_null("FactionIllustration") as Sprite2D
			if illustration != null:
				illustration.scale = Vector2.ONE * float(illustration.get_meta("world_marker_base_scale", 1.0)) * _user_zoom
			var label := marker.get_node_or_null("Name") as Label
			if label != null:
				label.visible = marker.visible
				if illustration != null:
					label.position.y = float(marker.get_meta("world_marker_label_half_height", 0.0)) * _user_zoom + 8.0
			_position_population_badge(marker)

func prepare_world_markers() -> void:
	if _world_markers_prepared:
		return
	_world_markers_prepared = true
	for index in range(_anchor_markers.size()):
		_ensure_anchor_illustration(index)
	_refresh_anchor_visibility()
	_layout_map()

func set_population_counts(counts: Dictionary) -> void:
	_population_counts = counts.duplicate(true)
	for marker in _anchor_markers:
		if not is_instance_valid(marker):
			continue
		var badge := marker.get_node_or_null("PopulationBadge") as PanelContainer
		var label := marker.get_node_or_null("PopulationBadge/Count") as Label
		if badge == null or label == null:
			continue
		var location_node_id := str(marker.get_meta("world_location_node_id", ""))
		var count := int(_population_counts.get(location_node_id, 0)) if not location_node_id.is_empty() else 0
		label.text = str(count)
		badge.visible = count > 0

func set_grid_population_counts(counts: Dictionary) -> void:
	var active_cells: Dictionary = {}
	for cell_variant in counts:
		if not cell_variant is Vector2i:
			continue
		var cell: Vector2i = cell_variant
		var count := int(counts[cell_variant])
		if count <= 0 or not _is_cell_in_bounds(cell) or not get_location_node_id_at_cell(cell).is_empty():
			continue
		active_cells[cell] = count
		var badge := _grid_population_badges.get(cell) as PanelContainer
		if badge == null or not is_instance_valid(badge):
			badge = _create_grid_population_badge(cell)
			_grid_population_badges[cell] = badge
		var label := badge.get_node_or_null("Count") as Label
		if label != null:
			label.text = str(count)
		badge.visible = true
	for cell_variant in _grid_population_badges.keys():
		if active_cells.has(cell_variant):
			continue
		var badge := _grid_population_badges[cell_variant] as PanelContainer
		if is_instance_valid(badge):
			badge.queue_free()
		_grid_population_badges.erase(cell_variant)

func _create_grid_population_badge(cell: Vector2i) -> PanelContainer:
	var badge := PanelContainer.new()
	badge.name = "GridPopulationBadge_%d_%d" % [cell.x, cell.y]
	badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	badge.z_index = POPULATION_BADGE_Z
	badge.size = Vector2(28.0, 27.0)
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.18, 0.16, 0.13, 0.94)
	style.border_color = Color("c9ad75")
	style.set_border_width_all(2)
	style.set_corner_radius_all(9)
	badge.add_theme_stylebox_override("panel", style)
	var label := Label.new()
	label.name = "Count"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.custom_minimum_size = Vector2(24.0, 23.0)
	label.add_theme_font_override("font", UI_FONT)
	label.add_theme_font_size_override("font_size", 14)
	label.add_theme_color_override("font_color", Color("f5ead1"))
	badge.add_child(label)
	badge.position = _terrain.position + _terrain.map_to_local(cell) + Vector2(TILE_SIZE * 0.34, -TILE_SIZE * 0.34) - badge.size * 0.5
	_world_overlay.add_child(badge)
	return badge

func _get_anchor_name(cell: Vector2i) -> String:
	var anchor: Dictionary = _anchors_by_cell.get(cell, {})
	return str(anchor.get("name", ""))

func _load_world_anchors() -> void:
	var file := FileAccess.open(WORLD_ANCHORS_PATH, FileAccess.READ)
	if file == null:
		push_error("无法读取百川世界格网地点配置：%s" % WORLD_ANCHORS_PATH)
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		push_error("百川世界格网地点配置格式无效。")
		return
	var raw_anchors: Variant = (parsed as Dictionary).get("anchors", [])
	if not raw_anchors is Array:
		push_error("百川世界格网地点配置缺少 anchors 列表。")
		return
	for raw_anchor in raw_anchors:
		if not raw_anchor is Dictionary:
			continue
		var anchor: Dictionary = (raw_anchor as Dictionary).duplicate(true)
		var origin: Array = anchor.get("grid_origin", [0, 0])
		var footprint: Array = anchor.get("footprint", [1, 1])
		if origin.size() < 2 or footprint.size() < 2:
			continue
		var origin_cell := Vector2i(int(origin[0]), int(origin[1]))
		var footprint_size := Vector2(float(footprint[0]), float(footprint[1]))
		var cell := origin_cell + Vector2i(floori(footprint_size.x / 2.0), floori(footprint_size.y / 2.0))
		if not _is_cell_in_bounds(cell):
			push_warning("忽略超出百川格网的地点锚点：%s" % str(anchor.get("id", "")))
			continue
		anchor["cell"] = cell
		anchor["marker_position"] = _terrain.position + _terrain.map_to_local(origin_cell) + Vector2((footprint_size.x - 1.0) * TILE_SIZE * 0.5, (footprint_size.y - 1.0) * TILE_SIZE * 0.5)
		_world_anchors.append(anchor)
		for y in range(origin_cell.y, origin_cell.y + int(footprint_size.y)):
			for x in range(origin_cell.x, origin_cell.x + int(footprint_size.x)):
				var footprint_cell := Vector2i(x, y)
				if not _is_cell_in_bounds(footprint_cell):
					continue
				if _anchors_by_cell.has(footprint_cell):
					var existing: Dictionary = _anchors_by_cell[footprint_cell]
					push_warning("世界节点占地重叠：%s 与 %s（格 %s）" % [str(existing.get("id", "")), str(anchor.get("id", "")), str(footprint_cell)])
					continue
				_anchors_by_cell[footprint_cell] = anchor

func _create_anchor_markers() -> void:
	for anchor in _world_anchors:
		var cell: Vector2i = anchor["cell"]
		var level := int(anchor.get("level", 2))
		var marker := Node2D.new()
		marker.name = "WorldAnchor_%s" % str(anchor.get("id", "Node"))
		marker.position = anchor.get("marker_position", _terrain.position + _terrain.map_to_local(cell))
		marker.z_index = ANCHOR_MARKER_Z
		var color := _anchor_color(str(anchor.get("faction_id", "")), level)
		var placeholder := Sprite2D.new()
		placeholder.name = "PlaceholderToken"
		placeholder.centered = true
		placeholder.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		placeholder.texture = MEMORY_MARKER_TEXTURE if level == 0 else PLACEHOLDER_MARKER_TEXTURE
		placeholder.modulate = color
		if level == 1:
			var ring := Sprite2D.new()
			ring.name = "PrimaryPlaceholderRing"
			ring.centered = true
			ring.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
			ring.texture = PRIMARY_MARKER_RING_TEXTURE
			ring.modulate = color
			marker.add_child(ring)
		marker.add_child(placeholder)
		var marker_size := 14.0 if level == 0 else 32.0 if level == 2 else 42.0
		var label := Label.new()
		label.name = "Name"
		label.text = str(anchor.get("name", "地点"))
		label.position = Vector2(-96.0, marker_size + 8.0)
		label.size = Vector2(192.0, 22.0)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		label.add_theme_font_override("font", UI_FONT)
		label.add_theme_font_size_override("font_size", 15 if level == 1 else 13)
		label.add_theme_color_override("font_color", Color("302d28"))
		label.add_theme_color_override("font_outline_color", Color(0.95, 0.93, 0.88, 0.95))
		label.add_theme_constant_override("outline_size", 4)
		marker.add_child(label)
		_world_overlay.add_child(marker)
		_anchor_markers.append(marker)
		marker.set_meta("world_location_node_id", str(anchor.get("location_node_id", "")))
		_create_population_badge(marker, anchor)
	_refresh_anchor_visibility()

func _anchor_color(faction_id: String, level: int) -> Color:
	var colors := {
		"xuandu_tianfu": Color("858b78"),
		"taiwei_daogong": Color("7d9296"),
		"yuxu_xianzong": Color("75918c"),
		"wuxiang_shengzong": Color("8d7974"),
	}
	var base: Color = colors.get(faction_id, Color("a2957a"))
	return base if level == 1 else base.lightened(0.18)

func _refresh_anchor_visibility() -> void:
	for index in range(_anchor_markers.size()):
		var marker := _anchor_markers[index]
		# Strategic nodes and authored place labels are map knowledge, not fog-revealed discoveries.
		marker.visible = true
		if _world_markers_prepared:
			_ensure_anchor_illustration(index)
		var label := marker.get_node_or_null("Name") as Label
		if label != null:
			label.visible = true
			if marker.has_meta("world_marker_label_half_height"):
				label.position.y = float(marker.get_meta("world_marker_label_half_height")) * _user_zoom + 8.0
		_position_population_badge(marker)

func _create_population_badge(marker: Node2D, anchor: Dictionary) -> void:
	var level := int(anchor.get("level", 2))
	var footprint: Array = anchor.get("footprint", [1, 1])
	var badge_size := Vector2(25.0, 24.0)
	var font_size := 13
	if level == 1:
		badge_size = Vector2(42.0, 38.0)
		font_size = 19
	elif int(footprint[0]) > 1 or int(footprint[1]) > 1:
		badge_size = Vector2(34.0, 31.0)
		font_size = 16
	var badge := PanelContainer.new()
	badge.name = "PopulationBadge"
	badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	badge.z_index = 20
	badge.size = badge_size
	badge.set_meta("world_population_badge_size", badge_size)
	badge.set_meta("world_population_footprint", Vector2(float(footprint[0]), float(footprint[1])))
	var badge_style := StyleBoxFlat.new()
	badge_style.bg_color = Color(0.18, 0.16, 0.13, 0.94)
	badge_style.border_color = Color("c9ad75")
	badge_style.set_border_width_all(2)
	badge_style.set_corner_radius_all(10)
	badge.add_theme_stylebox_override("panel", badge_style)
	var count_label := Label.new()
	count_label.name = "Count"
	count_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	count_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	count_label.custom_minimum_size = badge_size - Vector2(4.0, 4.0)
	count_label.add_theme_font_override("font", UI_FONT)
	count_label.add_theme_font_size_override("font_size", font_size)
	count_label.add_theme_color_override("font_color", Color("f5ead1"))
	badge.add_child(count_label)
	marker.add_child(badge)
	var location_node_id := str(anchor.get("location_node_id", ""))
	var count := int(_population_counts.get(location_node_id, 0)) if not location_node_id.is_empty() else 0
	count_label.text = str(count)
	badge.visible = count > 0
	_position_population_badge(marker)

func _position_population_badge(marker: Node2D) -> void:
	var badge := marker.get_node_or_null("PopulationBadge") as Control
	if badge == null or not badge.has_meta("world_population_footprint"):
		return
	var footprint: Vector2 = badge.get_meta("world_population_footprint")
	var badge_size: Vector2 = badge.get_meta("world_population_badge_size", badge.size)
	var top_right_corner := Vector2(footprint.x * TILE_SIZE * 0.5, -footprint.y * TILE_SIZE * 0.5) * _user_zoom
	badge.position = top_right_corner - badge_size * 0.5

func _ensure_anchor_illustration(index: int) -> void:
	if index < 0 or index >= _anchor_markers.size():
		return
	var marker := _anchor_markers[index]
	if marker.has_node("FactionIllustration") or bool(marker.get_meta("world_marker_texture_attempted", false)):
		return
	var anchor: Dictionary = _world_anchors[index]
	var texture_path := str(anchor.get("marker_texture", ""))
	if texture_path.is_empty():
		return
	marker.set_meta("world_marker_texture_attempted", true)
	var texture := ResourceLoader.load(texture_path) as Texture2D
	if texture == null:
		push_warning("节点插画无法读取，保留占位标记：%s (%s)" % [str(anchor.get("id", "")), texture_path])
		return
	var footprint: Array = anchor.get("footprint", [1, 1])
	var footprint_pixels := Vector2(float(footprint[0]), float(footprint[1])) * TILE_SIZE
	var texture_pixels := Vector2(texture.get_width(), texture.get_height())
	var fit_scale := minf(footprint_pixels.x / texture_pixels.x, footprint_pixels.y / texture_pixels.y)
	var illustration := Sprite2D.new()
	illustration.name = "FactionIllustration"
	illustration.centered = true
	illustration.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	illustration.texture = texture
	illustration.scale = Vector2.ONE * fit_scale * _user_zoom
	illustration.set_meta("world_marker_base_scale", fit_scale)
	marker.set_meta("world_marker_label_half_height", texture_pixels.y * fit_scale * 0.5)
	marker.add_child(illustration)
	for placeholder_name in ["PlaceholderToken", "PrimaryPlaceholderRing"]:
		var placeholder := marker.get_node_or_null(placeholder_name)
		if placeholder != null:
			marker.remove_child(placeholder)
			placeholder.queue_free()
	var label := marker.get_node_or_null("Name") as Label
	if label != null:
		label.position.y = texture_pixels.y * fit_scale * _user_zoom * 0.5 + 8.0

func _on_enter_node_pressed() -> void:
	if _moving or _selected_cell != _player_cell:
		return
	var anchor: Dictionary = _anchors_by_cell.get(_player_cell, {})
	if bool(anchor.get("enterable", false)):
		var node_id := str(anchor.get("location_node_id", ""))
		if not node_id.is_empty():
			node_enter_requested.emit(node_id)

func _update_enter_node_button() -> void:
	if _enter_node_button == null:
		return
	var anchor: Dictionary = _anchors_by_cell.get(_player_cell, {})
	var can_enter := _selected_cell == _player_cell and not _moving and bool(anchor.get("enterable", false)) and not str(anchor.get("location_node_id", "")).is_empty()
	_enter_node_button.visible = can_enter
	if can_enter:
		_enter_node_button.text = "进入 · %s" % str(anchor.get("name", "此地"))
		_position_enter_node_button(anchor)

func _position_enter_node_button(anchor: Dictionary) -> void:
	var fallback_cell: Vector2i = anchor.get("cell", _player_cell)
	var anchor_center: Vector2 = anchor.get("marker_position", _terrain.position + _terrain.map_to_local(fallback_cell))
	var center_on_view := get_global_transform_with_canvas().affine_inverse() * (_map_canvas.get_global_transform_with_canvas() * anchor_center)
	var footprint: Array = anchor.get("footprint", [1, 1])
	var button_size := _enter_node_button.size
	var position_above_node := center_on_view - Vector2(button_size.x * 0.5, float(footprint[1]) * TILE_SIZE * _user_zoom * 0.5 + button_size.y + 12.0)
	position_above_node.x = clampf(position_above_node.x, 12.0, maxf(12.0, size.x - button_size.x - 12.0))
	position_above_node.y = clampf(position_above_node.y, 12.0, maxf(12.0, size.y - button_size.y - 12.0))
	_enter_node_button.position = position_above_node

func _apply_button_style(button: Button) -> void:
	button.focus_mode = Control.FOCUS_NONE
	button.add_theme_font_override("font", UI_FONT)
	button.add_theme_font_size_override("font_size", 18)
	button.add_theme_color_override("font_color", Color("34332d"))
	var normal := StyleBoxFlat.new()
	normal.bg_color = Color(0.94, 0.92, 0.86, 0.94)
	normal.border_color = Color(0.20, 0.21, 0.19, 0.55)
	normal.set_border_width_all(1)
	normal.set_corner_radius_all(5)
	normal.content_margin_left = 12
	normal.content_margin_right = 12
	button.add_theme_stylebox_override("normal", normal)
	var hover := normal.duplicate() as StyleBoxFlat
	hover.bg_color = Color(0.99, 0.97, 0.91, 0.98)
	button.add_theme_stylebox_override("hover", hover)
	button.add_theme_stylebox_override("pressed", hover)

func _apply_info_panel_style() -> void:
	_cell_info_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	_cell_info_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_cell_info_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_cell_info_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var panel_style := StyleBoxFlat.new()
	panel_style.bg_color = Color(0.94, 0.92, 0.86, 0.96)
	panel_style.border_color = Color(0.20, 0.21, 0.19, 0.55)
	panel_style.set_border_width_all(1)
	panel_style.set_corner_radius_all(2)
	panel_style.content_margin_left = 14
	panel_style.content_margin_right = 14
	panel_style.content_margin_top = 7
	panel_style.content_margin_bottom = 7
	_cell_info_panel.add_theme_stylebox_override("panel", panel_style)
	_cell_info_label.add_theme_font_override("font", UI_FONT)
	_cell_info_label.add_theme_font_size_override("font_size", 16)
	_cell_info_label.add_theme_color_override("font_color", Color("34332d"))
	var help_label := get_node_or_null("HelpLabel") as Label
	if help_label != null:
		help_label.add_theme_font_override("font", UI_FONT)
		help_label.add_theme_font_size_override("font_size", 16)
		help_label.add_theme_color_override("font_color", Color("34332d"))

func _toggle_grid_lines() -> void:
	_show_grid_lines = not _show_grid_lines
	_apply_grid_preference()
	var config := ConfigFile.new()
	config.set_value("world_map", "grid_lines", _show_grid_lines)
	config.save(SETTINGS_PATH)

func _load_grid_preference() -> void:
	var config := ConfigFile.new()
	if config.load(SETTINGS_PATH) == OK:
		_show_grid_lines = bool(config.get_value("world_map", "grid_lines", true))

func _apply_grid_preference() -> void:
	_grid_lines.visible = _show_grid_lines
	_grid_toggle.text = "格线：显示" if _show_grid_lines else "格线：隐藏"
