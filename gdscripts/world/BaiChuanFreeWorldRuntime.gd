extends "res://gdscripts/world/BaiChuanNodeMapRuntime.gd"
## Continuous travel, while retaining authored location and escort identifiers.

signal distance_traveled(kilometres: float)
signal visible_encounter_requested(event: Dictionary)
signal nearby_people_changed
signal world_npc_selected(npc_id: String)
signal location_context_changed(node_id: String)
signal player_boat_requested(artifact_id: String)

const Navigation = preload("res://gdscripts/world/BaiChuanFreeNavigation.gd")
const TerrainTravel = preload("res://gdscripts/world/BaiChuanTerrainTravel.gd")
const WorldScale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
const NpcTraveler = preload("res://gdscripts/world/WorldNpcTraveler.gd")
const SpiritBoatVisual = preload("res://gdscripts/world/WorldSpiritBoatVisual.gd")
const LayeredLandscape = preload("res://gdscripts/world/BaiChuanLayeredLandscape.gd")
const DepthOrder = preload("res://gdscripts/world/WorldDepthOrder.gd")
const WALK_PIXELS_PER_SECOND := 140.0
const ENTRANCE_REACH := 28.0
const CONTACT_REACH := 38.0
const HAZARD_REACH := 22.0
var navigation = Navigation.new()
var terrain_travel = TerrainTravel.new()
var waypoints: Array[Dictionary] = []
var _remaining_path_length := 0.0
var _remaining_path_cost := 0.0
var destination_node := ""
var movement_mode := "free"
var hazards: Array[Dictionary] = []
var nearby_people: Dictionary = {}
var npc_views: Dictionary = {}
var hazard_views: Dictionary = {}
var route_line: Line2D
var destination_mark: Sprite2D
var travel_controls: HBoxContainer
var travel_status: Label
var stop_button: Button
var _free_ready := false
var _resume_requested := false
var _people_timer := 0.0
var _save_timer := 0.0
var _nearby_signature := ""
var _nearby_location := ""
var _last_completed_steps := 0
var _target_npc := ""
var _target_refresh := 0.0
var _last_gate := ""
var _motion_blocked := false
var _map_hold_seconds := 0.0
var _clock_waiting := false
var _npc_visual_last_hours := -INF
var _clock_committed_km := 0.0
var _pending_checkpoint := ""
var scale_ruler: MapScaleRuler
var party_views: Dictionary = {}
var _party_trail: Array[Vector2] = []
var _party_last_player_position := Vector2.ZERO
var _party_has_moved := false
var flight_speed := 0.0
var flight_height := 0.0
var flight_driver_id := ""
var flight_boat: Node2D
var _flight_display_active := false
var player_boat_id := ""
var _owned_player_boats: Array[Dictionary] = []
var boat_button: Button
var boat_menu: PopupMenu
var _boat_prepare: Callable
var _boat_consume: Callable
var _boat_leave: Callable
var _boat_validate: Callable
var landscape: Node2D
var depth_order = DepthOrder.new()

class MapScaleRuler extends Control:
	var kilometres_per_screen_pixel := 1.0
	var preferred_kilometres := 0.0
	func _draw() -> void:
		var candidates := [10.0, 20.0, 50.0, 100.0, 200.0, 500.0, 1000.0, 2000.0]
		var kilometres := 10.0
		for candidate: float in candidates:
			if candidate / maxf(kilometres_per_screen_pixel, 0.0001) <= 155.0: kilometres = candidate
		if preferred_kilometres > 0.0: kilometres = preferred_kilometres
		var width := kilometres / maxf(kilometres_per_screen_pixel, 0.0001)
		var start := Vector2((180.0 - width) * 0.5, 25)
		var ink_color := Color("33473f")
		draw_style_box(_paper(), Rect2(0, 0, 180, 38))
		draw_line(start, start + Vector2(width, 0), ink_color, 1.3, true)
		draw_line(start - Vector2(0, 4), start + Vector2(0, 3), ink_color, 1.3, true)
		draw_line(start + Vector2(width, -4), start + Vector2(width, 3), ink_color, 1.3, true)
		var label := "%d 公里" % int(kilometres)
		var font := ThemeDB.fallback_font
		var text_width := font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1, 14).x
		draw_string(font, Vector2((180 - text_width) * 0.5, 16), label, HORIZONTAL_ALIGNMENT_LEFT, -1, 14, ink_color)
	func _paper() -> StyleBoxFlat:
		var paper := StyleBoxFlat.new()
		paper.bg_color = Color("e3e1d3", 0.82)
		paper.corner_radius_top_left = 3
		paper.corner_radius_bottom_right = 3
		return paper

func _add_transit_buttons() -> void:
	# Intermediate road geometry has no world marker or interaction in free travel.
	pass

func _far_label_limit() -> float:
	return MAX_FAR_LABEL_COMPENSATION * WorldScale.COORDINATE_SCALE

func _far_label_minimum() -> float:
	return FAR_LABEL_MIN_COMPENSATION * WorldScale.COORDINATE_SCALE

func _far_primary_icon_limit() -> float:
	return 1.0

func _far_city_icon_limit() -> float:
	return 1.0

func _ready() -> void:
	add_to_group("free_world_map_presentation")
	var paper := ColorRect.new()
	paper.name = "FreeWorldPaper"
	paper.color = Color("d7d6ca")
	paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(paper)
	paper.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	travel.graph.set("use_free_world", true)
	super._ready()
	if not navigation.initialize():
		push_error("百川自由行旅：通行数据加载失败")
		return
	if not terrain_travel.initialize():
		push_error("百川自由行旅：地形行速数据加载失败")
		return
	route_line = Line2D.new()
	route_line.name = "FreeTravelRoute"
	route_line.width = 1.8
	route_line.default_color = Color("56685b", 0.72)
	route_line.z_index = 15
	world.add_child(route_line)
	destination_mark = Sprite2D.new()
	destination_mark.name = "FreeTravelDestination"
	destination_mark.texture = load("res://textures/ui/world/world_map_destination_flag_v1.svg")
	destination_mark.scale = Vector2.ONE * 0.45
	destination_mark.z_index = 20
	destination_mark.hide()
	world.add_child(destination_mark)
	_build_travel_controls()
	scale_ruler = MapScaleRuler.new()
	scale_ruler.name = "WorldDistanceRuler"
	scale_ruler.mouse_filter = Control.MOUSE_FILTER_IGNORE
	scale_ruler.z_index = 118
	add_child(scale_ruler)
	_free_ready = true
	_refresh_zoom_visibility()
	_update_context()
	_refresh_people()
	landscape = LayeredLandscape.new()
	landscape.name = "LayeredLandscape"
	world.add_child(landscape)
	if not landscape.configure(self): push_error("百川地图：分层景物加载失败")
	var clock := get_node_or_null("/root/GameTimeManager")
	if clock != null and clock.has_signal("smooth_time_advanced"):
		clock.connect("smooth_time_advanced", _on_clock_settled)

func add_dynamic_dungeon_marker(runtime_id: String, display_name: String, seed_text: String, saved_position: Vector2, can_enter: bool) -> Vector2:
	if not _free_ready or runtime_id.is_empty() or data.is_empty() or not is_instance_valid(places):
		return Vector2(-1, -1)
	if place_views.has(runtime_id):
		var existing_position: Vector2 = travel.graph.positions.get(runtime_id, saved_position)
		return existing_position

	var rng := RandomNumberGenerator.new()
	var stable_seed := 0
	for byte in seed_text.to_utf8_buffer():
		stable_seed = int((stable_seed * 31 + int(byte)) & 0x7fffffffffffffff)
	rng.seed = maxi(1, stable_seed)
	var world_position := saved_position
	if world_position.x < 0.0 or world_position.y < 0.0:
		var candidates: Array[String] = []
		for node: Dictionary in data.get("nodes", []):
			if str(node.get("type", "")) not in ["wild", "village"]: continue
			var id := str(node.get("id", ""))
			if travel.graph.positions.has(id): candidates.append(id)
		if candidates.is_empty():
			for id in travel.graph.runtime_ids.values():
				if travel.graph.positions.has(str(id)): candidates.append(str(id))
		candidates.sort()
		if candidates.is_empty(): return Vector2(-1, -1)
		var margin := 48.0
		var best_clearance := -1.0
		var best_position := Vector2.ZERO
		for attempt in range(48):
			var anchor_id: String = candidates[rng.randi_range(0, candidates.size() - 1)]
			var anchor_position: Vector2 = travel.graph.positions[anchor_id]
			var angle := rng.randf_range(0.0, TAU)
			var distance := rng.randf_range(76.0, 122.0)
			var candidate_position := (anchor_position + Vector2(cos(angle), sin(angle)) * distance).clamp(Vector2.ONE * margin, Vector2.ONE * (get_world_size() - margin))
			var clearance := INF
			for id in travel.graph.runtime_ids.values():
				var candidate_id := str(id)
				if not travel.graph.positions.has(candidate_id): continue
				var occupied_position: Vector2 = travel.graph.positions[candidate_id]
				clearance = minf(clearance, occupied_position.distance_to(candidate_position))
			if clearance > best_clearance:
				best_clearance = clearance
				best_position = candidate_position
			if clearance >= 84.0:
				best_position = candidate_position
				break
		world_position = best_position

	var nearest_id := ""
	var nearest_distance := INF
	for id in travel.graph.runtime_ids.values():
		var candidate_id := str(id)
		if candidate_id == runtime_id or not travel.graph.positions.has(candidate_id): continue
		var candidate_position: Vector2 = travel.graph.positions[candidate_id]
		var distance := candidate_position.distance_squared_to(world_position)
		if distance < nearest_distance:
			nearest_distance = distance
			nearest_id = candidate_id
	if nearest_id.is_empty(): return Vector2(-1, -1)

	var graph = travel.graph
	graph.positions[runtime_id] = world_position
	graph.runtime_ids[runtime_id] = runtime_id
	graph.adjacency[runtime_id] = []
	var edge_id := nearest_id + "~" + runtime_id
	var points := PackedVector2Array([graph.positions[nearest_id], world_position])
	var kilometres := maxf(points[0].distance_to(points[1]) * graph.km_per_pixel, 0.01)
	graph.edge_points[edge_id] = points
	graph.edge_km[edge_id] = kilometres
	graph.adjacency[nearest_id].append({"to": runtime_id, "edge": edge_id, "km": kilometres})
	graph.adjacency[runtime_id].append({"to": nearest_id, "edge": edge_id, "km": kilometres})

	var node := {
		"id": runtime_id,
		"name": display_name if can_enter else display_name + " · 已探索",
		"type": "dungeon",
		"level": 3,
		"pos": [world_position.x * 100.0 / get_world_size(), world_position.y * 100.0 / get_world_size()],
		"enterable": can_enter,
	}
	data.nodes.append(node)
	_add_dynamic_dungeon_view(node, world_position, can_enter)
	_refresh_zoom_visibility()
	_refresh_actions()
	return world_position

func _add_dynamic_dungeon_view(node: Dictionary, world_position: Vector2, can_enter: bool) -> void:
	var id := str(node.id)
	var marker := Control.new()
	marker.name = "DynamicDungeon_" + id.validate_node_name()
	marker.position = world_position.round()
	marker.mouse_filter = Control.MOUSE_FILTER_IGNORE
	places.add_child(marker)
	var icon := PanelContainer.new()
	icon.name = "NativeIcon"
	icon.position = Vector2(-19, -42)
	icon.size = Vector2(38, 38)
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var style := StyleBoxFlat.new()
	style.bg_color = Color("6c6043") if can_enter else Color("575950")
	style.border_color = Color("e4cf94") if can_enter else Color("a6a18d")
	style.set_border_width_all(2)
	style.set_corner_radius_all(19)
	icon.add_theme_stylebox_override("panel", style)
	var glyph := Label.new()
	glyph.text = "秘" if can_enter else "盡"
	glyph.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	glyph.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	glyph.mouse_filter = Control.MOUSE_FILTER_IGNORE
	glyph.add_theme_color_override("font_color", Color("f4ead0"))
	glyph.add_theme_font_size_override("font_size", 18)
	icon.add_child(glyph)
	marker.add_child(icon)

	var hit := Button.new()
	hit.name = "SelectIcon"
	hit.set_meta("formal_map_node_id", id)
	hit.flat = true
	hit.focus_mode = Control.FOCUS_NONE
	hit.position = Vector2(-23, -46)
	hit.size = Vector2(46, 46)
	hit.tooltip_text = str(node.name)
	hit.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	for state in ["normal", "hover", "pressed", "focus"]: hit.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	hit.mouse_entered.connect(_hover_place.bind(id))
	hit.mouse_exited.connect(_defer_hover_clear.bind(id))
	hit.pressed.connect(_select_place.bind(node))
	marker.add_child(hit)

	var title := Button.new()
	title.name = "SelectPlace"
	title.set_meta("formal_map_node_id", id)
	title.text = str(node.name)
	title.position = Vector2(18, -31)
	title.size = Vector2(132, 27)
	title.focus_mode = Control.FOCUS_NONE
	title.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	title.add_theme_font_override("font", load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"))
	title.add_theme_font_size_override("font_size", 15)
	title.add_theme_color_override("font_color", Color("292217"))
	title.add_theme_stylebox_override("normal", _nameplate_style("ead3a3", "876d45"))
	title.add_theme_stylebox_override("hover", _nameplate_style("f4dfb2", "a57036"))
	title.add_theme_stylebox_override("pressed", _nameplate_style("dcc18e", "765633"))
	title.mouse_entered.connect(_hover_place.bind(id))
	title.mouse_exited.connect(_defer_hover_clear.bind(id))
	title.pressed.connect(_select_place.bind(node))
	marker.add_child(title)
	place_views[id] = marker

func _build_travel_controls() -> void:
	var panel := PanelContainer.new()
	panel.name = "FreeTravelControls"
	panel.z_index = 120
	panel.add_theme_stylebox_override("panel", _location_paper())
	panel.custom_minimum_size.y = 44
	panel.set_anchors_preset(Control.PRESET_CENTER_TOP)
	panel.position = Vector2(get_viewport_rect().size.x * 0.5 - 235, 18)
	add_child(panel)
	travel_controls = HBoxContainer.new()
	travel_controls.add_theme_constant_override("separation", 12)
	panel.add_child(travel_controls)
	travel_status = Label.new()
	travel_status.add_theme_color_override("font_color", Color("303d38"))
	travel_status.add_theme_font_size_override("font_size", 16)
	travel_status.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	travel_status.custom_minimum_size.y = 32
	travel_controls.add_child(travel_status)
	stop_button = _button(travel_controls, "停下", stop_free_travel)
	stop_button.name = "StopFreeTravel"
	boat_button = _button(travel_controls, "驾舟", _on_boat_action)
	boat_button.name = "PlayerSpiritBoat"
	boat_button.hide()
	boat_menu = PopupMenu.new()
	boat_menu.name = "SpiritBoatSelection"
	add_child(boat_menu)
	boat_menu.id_pressed.connect(_choose_owned_boat)
	var focus := _button(travel_controls, "定位", focus_player_node)
	focus.name = "FocusFreeTraveler"
	for button: Button in [stop_button, boat_button, focus]:
		button.custom_minimum_size = Vector2(54, 32)
		button.add_theme_font_size_override("font_size", 16)
		for state in ["normal", "hover", "pressed", "disabled", "focus"]:
			button.add_theme_stylebox_override(state, _location_paper())
		button.add_theme_color_override("font_color", Color("303d38"))
		button.add_theme_color_override("font_hover_color", Color("6f3d1e"))
		button.add_theme_color_override("font_disabled_color", Color("8b9085"))
	_update_status()

func _update_status(message := "") -> void:
	if not is_instance_valid(travel_status): return
	var km := _remaining_length() * Navigation.KM_PER_PIXEL
	var terrain: Dictionary = terrain_travel.terrain_at(player_mark.position)
	var surface := "%s · 速度%d%%" % [str(terrain.name), roundi(float(terrain.multiplier) * 100.0)]
	if not message.is_empty(): travel_status.text = message
	elif movement_mode in ["flight", "player_flight"] and travel_busy:
		travel_status.text = "灵舟飞行 · 余 %.1f 公里 · %.1f 小时" % [km, _remaining_hours()]
	elif movement_mode == "player_flight":
		travel_status.text = "灵舟悬停 · 选择可落脚处" if flight_height > 0.0 else "已登舟 · 选择目的地"
	elif travel_busy:
		travel_status.text = "行旅中 · %s · 余 %.1f 公里 · %.1f 小时" % [surface, km, _remaining_hours()]
	else: travel_status.text = "%s · 空地点击前往 · 空格停下" % surface
	stop_button.disabled = not travel_busy or movement_mode in ["assigned", "flight"] or encounter_blocked
	if is_instance_valid(boat_button):
		boat_button.visible = movement_mode == "player_flight" or (_owned_player_boats.size() > 0 and movement_mode == "free")
		boat_button.text = "降落" if movement_mode == "player_flight" else "驾舟"
		boat_button.disabled = encounter_blocked or _clock_waiting or (movement_mode == "free" and travel_busy)
	var panel := travel_controls.get_parent() as Control
	panel.position.x = maxf(476.0, get_viewport_rect().size.x * 0.5 - panel.size.x * 0.5)
	panel.position.x = minf(panel.position.x, get_viewport_rect().size.x - panel.size.x - 16.0)
	if is_instance_valid(scale_ruler):
		scale_ruler.position = Vector2(get_viewport_rect().size.x * 0.5 - 90.0, get_viewport_rect().size.y - 148.0)
		scale_ruler.kilometres_per_screen_pixel = Navigation.KM_PER_PIXEL / maxf(world_zoom, 0.01)
		scale_ruler.preferred_kilometres = 40.0 if _zoom_level == 2 else (80.0 if _zoom_level == 1 else 0.0)
		scale_ruler.queue_redraw()

func _refresh_zoom_visibility() -> void:
	super._refresh_zoom_visibility()
	for marker: Button in transit_views.values(): marker.hide()
	_visible_transit_ids.clear()
	if is_instance_valid(ink):
		ink.paths_visible = false
		ink.marks_visible = false
		ink.queue_redraw()
	if is_instance_valid(population_badges):
		population_badges.visible_node_ids = _visible_place_ids.duplicate()
		population_badges.queue_redraw()
	for view: Node2D in npc_views.values(): _scale_npc(view)
	_update_status()
	if is_instance_valid(landscape): landscape.refresh(true)

func _input(event: InputEvent) -> void:
	if not is_visible_in_tree(): return
	if event is InputEventMouseButton and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN] and _is_pointer_over_map(event.position):
		_map_hold_seconds = 0.15
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_SPACE:
		if get_viewport().gui_get_focus_owner() is LineEdit or get_viewport().gui_get_focus_owner() is TextEdit: return
		stop_free_travel()
		get_viewport().set_input_as_handled()
		return
	super._input(event)

func _formal_map_pointer_input(event: InputEvent) -> bool:
	# Keep the shared click/drag threshold, but give empty ground a destination.
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed and _map_press_active:
		var was_dragging := dragging
		var target := _map_press_target
		var ground_click := not was_dragging and not is_instance_valid(target) and _is_pointer_over_map(event.position)
		if is_instance_valid(target) and not was_dragging and target.is_visible_in_tree() and target.get_global_rect().has_point(event.position):
			if target.has_meta("free_npc_id") or target.has_meta("free_hazard_id"):
				_map_press_active = false
				_map_press_target = null
				dragging = false
				target.emit_signal("pressed")
				get_viewport().set_input_as_handled()
				return true
		var handled := super._formal_map_pointer_input(event)
		if ground_click and _free_ready:
			var point: Vector2 = world.get_global_transform_with_canvas().affine_inverse() * event.position
			navigate_to_world(point)
		return handled
	return super._formal_map_pointer_input(event)

func _can_record_formal_map_click(_node_id: String) -> bool:
	return not encounter_blocked and movement_mode not in ["assigned", "flight"]

func _on_formal_map_double_click(node_id: String) -> void:
	if encounter_blocked or movement_mode in ["assigned", "flight"]: return
	if node_id == _nearby_location and not travel_busy:
		_enter_current()
		return
	selected_node = node_id
	_travel_selected()

func _select_place(node: Dictionary) -> void:
	if not _free_ready:
		super._select_place(node)
		return
	selected_node = str(node.id)
	title_label.text = str(node.name)
	if movement_mode == "player_flight":
		var point: Vector2 = travel.graph.positions[selected_node]
		var km := player_mark.position.distance_to(point) * Navigation.KM_PER_PIXEL
		detail_label.text = "航程 %.1f 公里 · 预计 %.1f 小时" % [km, travel.graph.hours_for_km(km, flight_speed)] if navigation.is_walkable(point) else "此处无法降落"
		_refresh_actions()
		return
	var points := navigation.find_path(player_mark.position, travel.graph.positions[selected_node])
	var km: float = navigation.path_length(points) * Navigation.KM_PER_PIXEL
	var hours: float = travel.graph.hours_for_km(terrain_travel.path_cost_length(points) * Navigation.KM_PER_PIXEL, dun_speed)
	detail_label.text = "附近 · 可进入" if selected_node == _nearby_location else ("路程 %.1f 公里 · 预计 %.1f 小时" % [km, hours] if not points.is_empty() else "暂不可达")
	_refresh_actions()

func _travel_selected() -> void:
	if selected_node.is_empty() or not travel.graph.positions.has(selected_node): return
	navigate_to_world(travel.graph.positions[selected_node], selected_node)

func navigate_to_world(point: Vector2, node_id := "") -> bool:
	if not _free_ready or encounter_blocked or movement_mode in ["assigned", "flight"]: return false
	if movement_mode == "player_flight": return _navigate_player_boat(point, node_id)
	var points := navigation.find_path(player_mark.position, point)
	if points.size() < 2:
		_update_status("此处无法通行，请选择岸边或谷地" if not navigation.is_walkable(point) else "此处暂无通路")
		return false
	waypoints.clear()
	for i in range(1, points.size()): waypoints.append({"position": points[i], "node": ""})
	_rebuild_remaining_metrics()
	destination_node = node_id
	_target_npc = ""
	_resume_requested = true
	if not travel_busy:
		_last_completed_steps = 0
		travel_busy = true
		node_travel_started.emit()
	_update_route_line()
	_refresh_actions()
	_update_status()
	world_state_changed.emit()
	return true

func stop_free_travel() -> void:
	if movement_mode in ["assigned", "flight"] or encounter_blocked or _clock_waiting: return
	waypoints.clear()
	_target_npc = ""
	_resume_requested = false
	_finish_motion()

func _finish_motion() -> void:
	var was_moving := travel_busy
	travel_busy = false
	if waypoints.is_empty(): _rebuild_remaining_metrics()
	_update_context()
	_update_route_line()
	_refresh_actions()
	_update_status()
	world_state_changed.emit()
	if was_moving: node_travel_finished.emit(travel.current_node)
	nearby_people_changed.emit()

func _process(delta: float) -> void:
	if _free_ready and is_visible_in_tree():
		_map_hold_seconds = maxf(0.0, _map_hold_seconds - delta)
		var clock := get_node_or_null("/root/GameTimeManager")
		var time_paused := clock != null and bool(clock.get("is_paused"))
		if travel_busy and not encounter_blocked and not time_paused and not _motion_blocked and not _clock_waiting and not dragging and not _map_press_active and _map_hold_seconds <= 0.0:
			if movement_mode in ["flight", "player_flight"]:
				_advance_flight(minf(delta, 0.1))
			else:
				_advance_motion(minf(delta, 0.1) * WALK_PIXELS_PER_SECOND * dun_speed / 100.0)
		_people_timer += delta
		_save_timer += delta
		if _people_timer >= 0.25:
			_people_timer = 0.0
			_refresh_people()
			_update_status()
			_update_context()
		if _save_timer >= 0.3 and travel_busy:
			_save_timer = 0.0
			world_state_changed.emit()
		var hours: float = clock.call("get_precise_game_hours") if clock != null else 0.0
		var advancing := is_finite(_npc_visual_last_hours) and hours > _npc_visual_last_hours
		_npc_visual_last_hours = hours
		for view: Node2D in npc_views.values(): view.call("update_activity", advancing, time_paused, true)
	elif _free_ready:
		for view: Node2D in npc_views.values(): view.call("update_activity", false, false, false)
	_update_party_views()
	_update_flight_visual()
	super._process(delta)
	if is_instance_valid(landscape): landscape.update(delta)

## One-time opening placement. It is a coordinate, not a new map location.
func set_initial_position_near_location(runtime_id: String) -> bool:
	if not _free_ready or travel_busy: return false
	var node_id: String = travel.graph.node_for_runtime_id(runtime_id)
	if not travel.graph.positions.has(node_id): return false
	var gate: Vector2 = travel.graph.positions[node_id]
	for offset in [Vector2(-64, 0), Vector2(0, 64), Vector2(64, 0), Vector2(0, -64), Vector2(-48, 48), Vector2(48, 48), Vector2(-48, -48), Vector2(48, -48)]:
		var start: Vector2 = gate + offset
		if not navigation.segment_clear(start, gate): continue
		player_mark.position = start
		player_halo.position = start
		_last_avatar_position = start
		_party_trail.clear()
		_party_last_player_position = start
		_party_has_moved = false
		_update_context()
		_refresh_actions()
		focus_player_node()
		world_state_changed.emit()
		return true
	return false

func travel_to_runtime_location(runtime_id: String) -> bool:
	var node_id: String = travel.graph.node_for_runtime_id(runtime_id)
	if not travel.graph.positions.has(node_id): return false
	return navigate_to_world(travel.graph.positions[node_id], node_id)

func configure_player_boat_callbacks(prepare: Callable, consume: Callable, leave: Callable, validate: Callable) -> void:
	_boat_prepare = prepare
	_boat_consume = consume
	_boat_leave = leave
	_boat_validate = validate

func set_owned_player_boats(rows: Array) -> void:
	_owned_player_boats.clear()
	for row: Variant in rows:
		if row is Dictionary and not str(row.get("id", "")).is_empty():
			_owned_player_boats.append(row.duplicate(true))
	_update_status()

func _on_boat_action() -> void:
	if movement_mode == "player_flight":
		land_player_boat()
		return
	if movement_mode != "free" or travel_busy or _owned_player_boats.is_empty(): return
	if _owned_player_boats.size() == 1:
		player_boat_requested.emit(str(_owned_player_boats[0].id))
		return
	boat_menu.clear()
	for i in _owned_player_boats.size():
		var row: Dictionary = _owned_player_boats[i]
		boat_menu.add_item(str(row.get("name", "灵舟")) + " · 续航 %.1f 小时" % float(row.get("charge", 0.0)), i)
	boat_menu.position = Vector2i(boat_button.get_screen_position() + Vector2(0, boat_button.size.y))
	boat_menu.popup()

func _choose_owned_boat(index: int) -> void:
	if index >= 0 and index < _owned_player_boats.size(): player_boat_requested.emit(str(_owned_player_boats[index].id))

func board_player_spirit_boat(artifact_id: String, speed: float) -> bool:
	if not _free_ready or travel_busy or encounter_blocked or movement_mode != "free" or artifact_id.is_empty(): return false
	if not is_finite(speed) or speed <= 0.0 or not _boat_validate.is_valid() or not bool(_boat_validate.call(artifact_id, speed)): return false
	if not _ensure_flight_boat(""): return false
	player_boat_id = artifact_id
	flight_speed = speed
	flight_driver_id = ""
	flight_height = 0.0
	movement_mode = "player_flight"
	_sync_boat_passengers()
	_update_flight_visual()
	_update_context()
	_update_status()
	world_state_changed.emit()
	return true

func _navigate_player_boat(point: Vector2, node_id: String) -> bool:
	if _clock_waiting or not point.is_finite() or not navigation.is_walkable(point):
		_update_status("请选择可通行的地面作为落点")
		return false
	var distance := player_mark.position.distance_to(point)
	if distance <= 0.01:
		_update_status("可在当前位置降落")
		return false
	var hours: float = travel.graph.hours_for_km(distance * Navigation.KM_PER_PIXEL, flight_speed)
	if not _boat_prepare.is_valid() or not bool(_boat_prepare.call(player_boat_id, hours)):
		_update_status("灵石不足，无法为这段航程充能")
		return false
	waypoints = [{"position": point, "node": ""}]
	destination_node = node_id
	_resume_requested = true
	_rebuild_remaining_metrics()
	if not travel_busy:
		travel_busy = true
		travel_paused = false
		_last_completed_steps = 0
		node_travel_started.emit()
	_update_route_line()
	_refresh_actions()
	_update_status()
	world_state_changed.emit()
	return true

func land_player_boat() -> bool:
	if movement_mode != "player_flight" or _clock_waiting: return false
	if travel_busy:
		_update_status("请先停下灵舟")
		return false
	if not navigation.is_walkable(player_mark.position):
		_update_status("此处无法落脚，请选择可通行的地面")
		return false
	if not _boat_leave.is_valid() or not bool(_boat_leave.call(player_boat_id)):
		_update_status("灵舟状态异常，暂无法降落")
		return false
	player_boat_id = ""
	flight_height = 0.0
	movement_mode = "free"
	_update_flight_visual()
	_update_context()
	_update_status()
	world_state_changed.emit()
	return true

func _sync_boat_passengers() -> void:
	if movement_mode not in ["flight", "player_flight"] or not is_instance_valid(flight_boat): return
	var ids := PackedStringArray()
	if movement_mode == "flight" and not flight_driver_id.is_empty(): ids.append(flight_driver_id)
	for id: String in party_views.keys():
		if not ids.has(id): ids.append(id)
	flight_boat.set_passenger_ids(ids)

## A scripted escort uses the vessel's own speed. The ground projection and
## positive height are separate; this route is not a terrain-navigation path.
func begin_spirit_boat_flight(destination_runtime_id: String, boat_speed: float, driver_id: String) -> bool:
	if not _free_ready or travel_busy or encounter_blocked or movement_mode != "free": return false
	var target_id: String = travel.graph.node_for_runtime_id(destination_runtime_id)
	if not travel.graph.positions.has(target_id) or not is_finite(boat_speed) or boat_speed <= 0.0 or driver_id.is_empty(): return false
	var target: Vector2 = travel.graph.positions[target_id]
	if player_mark.position.distance_to(target) <= 0.01 or not _ensure_flight_boat(driver_id): return false
	flight_speed = boat_speed
	flight_driver_id = driver_id
	flight_height = 0.0
	movement_mode = "flight"
	_sync_boat_passengers()
	waypoints = [{"position": target, "node": ""}]
	destination_node = target_id
	_target_npc = ""
	_resume_requested = true
	_rebuild_remaining_metrics()
	travel_busy = true
	travel_paused = false
	_last_completed_steps = 0
	_update_flight_visual()
	_update_route_line()
	_refresh_actions()
	_update_status()
	node_travel_started.emit()
	world_state_changed.emit()
	return true

func _ensure_flight_boat(driver_id: String) -> bool:
	if is_instance_valid(flight_boat):
		return flight_boat.set_passenger_ids(PackedStringArray([driver_id]) if not driver_id.is_empty() else PackedStringArray())
	var boat := SpiritBoatVisual.new()
	boat.name = "SpiritBoatTraveler"
	VisualScale.overlay(boat, VisualScale.FLIGHT_DEPTH)
	world.add_child(boat)
	if not boat.build(driver_id):
		boat.queue_free()
		return false
	flight_boat = boat
	return true

func _update_flight_visual() -> void:
	if not _free_ready or not is_instance_valid(player_mark): return
	var flying := movement_mode in ["flight", "player_flight"]
	if flying:
		_flight_display_active = true
		player_mark.hide()
		player_halo.hide()
	elif _flight_display_active:
		_flight_display_active = false
		player_mark.show()
		player_halo.show()
	if is_instance_valid(flight_boat):
		flight_boat.visible = flying
		if flying:
			flight_boat.set_map_zoom(world_zoom)
			flight_boat.set_flight_position(Vector3(player_mark.position.x, player_mark.position.y, flight_height))
			if not waypoints.is_empty():
				var direction: Vector2 = (waypoints[0].position as Vector2) - player_mark.position
				if absf(direction.x) > 0.001: flight_boat.set_facing(signf(direction.x))

func _advance_flight(delta: float) -> void:
	if waypoints.is_empty() or _clock_waiting: return
	var target: Vector2 = waypoints[0].position
	var previous := player_mark.position
	var remaining := previous.distance_to(target)
	if remaining <= 0.001:
		_commit_flight_step()
		return
	# Compress the visible speed range so a fast boat remains watchable while
	# still appearing faster than a slower one. Calendar time always uses the
	# vessel's actual speed and straight-line kilometres below.
	var visual_rate := clampf(0.3 * sqrt(flight_speed / 100.0), 0.3, 4.0)
	var amount := minf(remaining, delta * WALK_PIXELS_PER_SECOND * visual_rate)
	var next := previous.move_toward(target, amount)
	var kilometres := amount * Navigation.KM_PER_PIXEL
	var flight_hours: float = travel.graph.hours_for_km(kilometres, flight_speed)
	if movement_mode == "player_flight" and (not _boat_consume.is_valid() or not bool(_boat_consume.call(player_boat_id, flight_hours))):
		waypoints.clear()
		_resume_requested = false
		_finish_motion()
		_update_status("灵舟续航不足，已在空中停下")
		return
	player_mark.position = next
	player_halo.position = next
	flight_height = minf(move_toward(flight_height, 120.0, delta * 90.0), next.distance_to(target) * 0.75)
	_remaining_path_length = maxf(0.0, _remaining_path_length - amount)
	_remaining_path_cost = maxf(0.0, _remaining_path_cost - amount)
	var clock := get_node_or_null("/root/GameTimeManager")
	if clock != null and kilometres > 0.0:
		var hours := flight_hours
		var now: float = clock.call("get_precise_game_hours")
		if floori(now / 720.0) != floori((now + hours) / 720.0):
			_clock_waiting = true
			_clock_committed_km = kilometres
			_pending_checkpoint = ""
			clock.call("advance_time_smooth", hours, "world_map_travel")
			_update_route_line()
			return
		clock.call("advance_time", hours, "world_map_travel")
	_commit_flight_step()
	_update_route_line()

func _commit_flight_step() -> void:
	if waypoints.is_empty(): return
	if player_mark.position.distance_to(waypoints[0].position) > 0.001: return
	waypoints.clear()
	_resume_requested = false
	if movement_mode == "player_flight":
		_finish_motion()
		land_player_boat()
		return
	flight_height = 0.0
	movement_mode = "free"
	_update_flight_visual()
	_finish_motion()

## Members are real, saved NPCs; this method only manages their map presentation.
func set_player_travel_party(members: Array) -> void:
	var present := {}
	for row: Dictionary in members:
		var id := str(row.get("id", ""))
		if id.is_empty(): continue
		present[id] = true
		if is_instance_valid(party_views.get(id)): continue
		var view := NpcTraveler.new()
		view.name = "PartyTraveler_" + id.validate_node_name()
		view.z_index = 99
		world.add_child(view)
		if not view.configure(id):
			view.queue_free()
			continue
		var hit := Button.new()
		hit.name = "PartyMember"
		hit.flat = true
		hit.focus_mode = Control.FOCUS_NONE
		hit.size = VisualScale.AVATAR_HIT_WORLD_SIZE * float(view.native_height) / VisualScale.COMPANION_WORLD_HEIGHT
		hit.position = Vector2(-hit.size.x * 0.5, -hit.size.y)
		hit.set_meta("free_npc_id", id)
		hit.tooltip_text = str(row.get("name", "同行者"))
		hit.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		for state in ["normal", "hover", "pressed", "focus"]: hit.add_theme_stylebox_override(state, StyleBoxEmpty.new())
		hit.pressed.connect(func(): world_npc_selected.emit(id))
		VisualScale.overlay(hit, VisualScale.INPUT_DEPTH + 1)
		view.add_child(hit)
		party_views[id] = view
	for id: String in party_views.keys():
		if not present.has(id):
			party_views[id].queue_free()
			party_views.erase(id)
	_party_trail.clear()
	_party_last_player_position = player_mark.position
	_party_has_moved = false
	_update_party_views()
	_sync_boat_passengers()

func _update_party_views() -> void:
	if not _free_ready or not is_instance_valid(player_mark) or party_views.is_empty(): return
	var point := player_mark.position
	var motion := point - _party_last_player_position
	var spacing := VisualScale.PARTY_WORLD_SPACING
	if _party_trail.is_empty():
		_party_trail = [point, point - Vector2.RIGHT * spacing * party_views.size()]
	if motion.length_squared() > 0.0001:
		if not _party_has_moved:
			_party_trail = [_party_last_player_position, _party_last_player_position - motion.normalized() * spacing * party_views.size()]
			_party_has_moved = true
		_party_trail.push_front(point)
	_party_last_player_position = point
	var limit := spacing * (party_views.size() + 1) + 32.0
	var length := 0.0
	for i in range(1, _party_trail.size()):
		length += _party_trail[i - 1].distance_to(_party_trail[i])
		if length > limit:
			_party_trail.resize(i + 1)
			break
	var clock := get_node_or_null("/root/GameTimeManager")
	var paused := clock != null and bool(clock.get("is_paused"))
	var moving := motion.length_squared() > 0.0001 and travel_busy and not paused
	var index := 0
	for view: Node2D in party_views.values():
		view.visible = movement_mode not in ["flight", "player_flight"]
		index += 1
		var remaining := spacing * index
		var target: Vector2 = _party_trail[-1]
		for i in range(1, _party_trail.size()):
			var start: Vector2 = _party_trail[i - 1]
			var end: Vector2 = _party_trail[i]
			var segment := start.distance_to(end)
			if segment >= remaining and segment > 0.0001:
				target = start.lerp(end, remaining / segment)
				break
			remaining -= segment
		view.scale = Vector2.ONE * VisualScale.COMPANION_WORLD_HEIGHT / float(view.get("native_height"))
		view.call("sync_sample", target, moving)
		VisualScale.place_on_ground(view, target.y)
		view.call("update_activity", moving, paused, is_visible_in_tree())

func _should_follow_traveler() -> bool:
	var clock := get_node_or_null("/root/GameTimeManager")
	return travel_busy and not dragging and not _map_press_active and _map_hold_seconds <= 0.0 and not _clock_waiting and not (clock != null and bool(clock.get("is_paused")))

func _on_clock_settled() -> void:
	if not _clock_waiting: return
	_clock_waiting = false
	var distance := _clock_committed_km
	var checkpoint := _pending_checkpoint
	_clock_committed_km = 0.0
	_pending_checkpoint = ""
	if movement_mode in ["flight", "player_flight"]:
		_commit_flight_step()
	else:
		_commit_motion_step(distance, checkpoint)
	_update_route_line()
	world_state_changed.emit()

func _advance_motion(budget: float) -> void:
	if _clock_waiting: return
	var clock := get_node_or_null("/root/GameTimeManager")
	while budget > 0.0001 and travel_busy and not encounter_blocked and not waypoints.is_empty():
		var target: Vector2 = waypoints[0].position
		var previous := player_mark.position
		var distance := previous.distance_to(target)
		var amount := minf(distance, 8.0)
		var next := target if amount >= distance else previous.move_toward(target, amount)
		var cost: float = terrain_travel.segment_cost_length(previous, next)
		if cost > budget:
			amount = terrain_travel.distance_for_cost(previous, next, budget)
			next = previous.move_toward(next, amount)
			cost = terrain_travel.segment_cost_length(previous, next)
		amount = previous.distance_to(next)
		var reached := next == target
		if not reached and amount <= 0.0: break
		if not navigation.segment_clear(previous, next):
			waypoints.clear()
			_resume_requested = false
			_finish_motion()
			_update_status("前方通路有变，已停下")
			return
		player_mark.position = next
		player_halo.position = next
		# Budget is equivalent flat-ground distance. Time uses terrain cost;
		# encounters and mileage retain the actual distance.
		budget -= cost
		_remaining_path_length = maxf(0.0, _remaining_path_length - amount)
		_remaining_path_cost = maxf(0.0, _remaining_path_cost - cost)
		var km := amount * Navigation.KM_PER_PIXEL
		var checkpoint := ""
		if reached:
			checkpoint = str(waypoints[0].node)
			waypoints.pop_front()
		if amount > 0.0:
			if clock != null:
				var hours: float = travel.graph.hours_for_km(cost * Navigation.KM_PER_PIXEL, dun_speed)
				var now: float = clock.call("get_precise_game_hours")
				if floori(now / 720.0) != floori((now + hours) / 720.0):
					# The monthly simulation may yield. Commit its checkpoint and
					# any encounter only once the shared clock has fully settled.
					_clock_waiting = true
					_clock_committed_km = km
					_pending_checkpoint = checkpoint
					clock.call("advance_time_smooth", hours, "world_map_travel")
					break
				clock.call("advance_time", hours, "world_map_travel")
		_commit_motion_step(km, checkpoint)
	_update_route_line()

func _commit_motion_step(km: float, checkpoint: String) -> void:
	if not checkpoint.is_empty() and movement_mode == "assigned":
		var step: Dictionary = travel.next_step(dun_speed)
		if not step.is_empty() and checkpoint == str(step.to) and travel.commit_step(checkpoint):
			last_travel_step = step
			_last_completed_steps += 1
			node_step_committed.emit(checkpoint)
			if travel_paused:
				_finish_motion()
				return
	_update_context()
	if movement_mode == "free" and km > 0.0: distance_traveled.emit(km)
	if _check_hazard_contact(): return
	if not _target_npc.is_empty() and can_contact_map_npc(_target_npc):
		var npc_id := _target_npc
		waypoints.clear()
		_target_npc = ""
		_resume_requested = false
		_finish_motion()
		world_npc_selected.emit(npc_id)
		return
	if waypoints.is_empty() and travel_busy:
		_resume_requested = false
		movement_mode = "free"
		_finish_motion()

func _update_context() -> void:
	if not _free_ready: return
	var nearest := ""
	var best := INF
	for id: String in _all_place_ids():
		var distance: float = player_mark.position.distance_squared_to(travel.graph.positions[id])
		if distance < best:
			best = distance
			nearest = id
	if movement_mode != "assigned": travel.current_node = nearest
	var gate := ""
	best = ENTRANCE_REACH * ENTRANCE_REACH
	for id: String in _all_place_ids():
		var point: Vector2 = travel.graph.positions[id]
		var distance: float = player_mark.position.distance_squared_to(point)
		if movement_mode not in ["flight", "player_flight"] and distance <= best and navigation.segment_clear(player_mark.position, point):
			best = distance
			gate = id
	_nearby_location = gate
	if gate != _last_gate:
		if not gate.is_empty() and travel_busy: _last_completed_steps += 1
		_last_gate = gate
		location_context_changed.emit(travel.current_node)
		nearby_people_changed.emit()

func _all_place_ids() -> Array:
	return travel.graph.runtime_ids.values()

func get_current_location_runtime_id() -> String:
	return travel.graph.runtime_id_for_node(_nearby_location)

func get_world_position() -> Vector2:
	return player_mark.position

func get_last_completed_steps() -> int:
	return _last_completed_steps

func _layout_location_controls() -> void:
	if not _free_ready:
		super._layout_location_controls()
		return
	var available := not travel_busy and not encounter_blocked and not _nearby_location.is_empty()
	var enterable := _runtime_location_enterable(_nearby_location)
	enter_button.disabled = not available or not enterable
	enter_button.visible = available and enterable and _visible_place_ids.has(_nearby_location)
	if enter_button.visible: _position_location_control(enter_button, _nearby_location)
	formal_details_panel.visible = not selected_node.is_empty() and selected_node != _nearby_location and _selected_node_is_visible() and not travel_busy
	if formal_details_panel.visible: _position_location_control(formal_details_panel, selected_node)

func _enter_current() -> void:
	if travel_busy or encounter_blocked or _nearby_location.is_empty() or not _runtime_location_enterable(_nearby_location): return
	# Enter/exit preserves the exact nearby position; no free distance or snapping.
	travel.current_node = _nearby_location
	world_state_changed.emit()
	node_enter_requested.emit(get_current_location_runtime_id())

func _remaining_length() -> float:
	return _remaining_path_length

func _remaining_cost_length() -> float:
	return _remaining_path_cost

func _remaining_hours() -> float:
	return travel.graph.hours_for_km(_remaining_path_cost * Navigation.KM_PER_PIXEL, flight_speed if movement_mode in ["flight", "player_flight"] else dun_speed)

func _rebuild_remaining_metrics() -> void:
	# Rebuild only after route changes or restoration. Long assigned routes must
	# not integrate thousands of unchanged segments for each frame or HUD tick.
	_remaining_path_length = 0.0
	_remaining_path_cost = 0.0
	var previous := player_mark.position if is_instance_valid(player_mark) else Vector2.ZERO
	for waypoint: Dictionary in waypoints:
		_remaining_path_length += previous.distance_to(waypoint.position)
		_remaining_path_cost += previous.distance_to(waypoint.position) if movement_mode in ["flight", "player_flight"] else terrain_travel.segment_cost_length(previous, waypoint.position)
		previous = waypoint.position

func _update_route_line() -> void:
	if not is_instance_valid(route_line): return
	var points := PackedVector2Array([player_mark.position])
	for waypoint: Dictionary in waypoints: points.append(waypoint.position)
	route_line.points = points
	destination_mark.visible = points.size() > 1
	if destination_mark.visible: destination_mark.position = points[-1] - Vector2(0, 13)

func _refresh_people() -> void:
	var manager := get_node_or_null("/root/NpcWorldManager")
	if manager == null or not manager.has_method("get_free_world_map_people"): return
	var samples: Array = manager.call("get_free_world_map_people", player_mark.position, 900.0)
	var present := {}
	nearby_people.clear()
	for row: Dictionary in samples:
		var id := str(row.id)
		present[id] = true
		nearby_people[id] = row
		var view: AnimatedSprite2D = npc_views.get(id)
		if not is_instance_valid(view):
			view = NpcTraveler.new()
			view.name = "Traveler_" + id.validate_node_name()
			view.z_index = 40
			world.add_child(view)
			if not view.call("configure", id):
				view.queue_free()
				continue
			var hit := Button.new()
			hit.name = "Approach"
			hit.flat = true
			hit.focus_mode = Control.FOCUS_NONE
			hit.set_meta("free_npc_id", id)
			hit.size = VisualScale.AVATAR_HIT_WORLD_SIZE * float(view.get("native_height")) / VisualScale.NPC_WORLD_HEIGHT
			hit.position = Vector2(-hit.size.x * 0.5, -hit.size.y)
			hit.tooltip_text = "%s · %s" % [row.name, row.realm]
			hit.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
			hit.pressed.connect(_approach_person.bind(id))
			VisualScale.overlay(hit, VisualScale.INPUT_DEPTH + 1)
			for state in ["normal", "hover", "pressed", "focus"]: hit.add_theme_stylebox_override(state, StyleBoxEmpty.new())
			view.add_child(hit)
			npc_views[id] = view
		view.call("sync_sample", row.position, bool(row.traveling))
		_scale_npc(view)
	for id: String in npc_views.keys():
		if not present.has(id):
			npc_views[id].queue_free()
			npc_views.erase(id)
	var ids := _contact_ids_from_samples(samples)
	ids.sort()
	var signature := "|".join(ids) + ":" + _nearby_location
	if signature != _nearby_signature:
		_nearby_signature = signature
		nearby_people_changed.emit()
	if not _target_npc.is_empty():
		if not nearby_people.has(_target_npc):
			stop_free_travel()
			_update_status("对方已走远")
		elif travel_busy:
			var target_id := _target_npc
			var target_position: Vector2 = nearby_people[target_id].position
			if waypoints.is_empty() or (waypoints[-1].position as Vector2).distance_to(target_position) > 28.0:
				if navigate_to_world(target_position): _target_npc = target_id

func _scale_npc(view: Node2D) -> void:
	view.scale = Vector2.ONE * VisualScale.NPC_WORLD_HEIGHT / float(view.get("native_height"))
	VisualScale.place_on_ground(view, view.position.y)
	view.visible = _zoom_level >= 1

func get_nearby_person_ids() -> PackedStringArray:
	var ids := PackedStringArray()
	if movement_mode in ["flight", "player_flight"]: return ids
	var manager := get_node_or_null("/root/NpcWorldManager")
	if manager == null or not manager.has_method("get_free_world_map_people"): return ids
	var samples: Array = manager.call("get_free_world_map_people", player_mark.position, CONTACT_REACH)
	return _contact_ids_from_samples(samples)

func _contact_ids_from_samples(samples: Array) -> PackedStringArray:
	var ids := PackedStringArray()
	for row: Dictionary in samples:
		if player_mark.position.distance_squared_to(row.position) <= CONTACT_REACH * CONTACT_REACH and navigation.segment_clear(player_mark.position, row.position):
			ids.append(str(row.id))
	return ids

func set_population_counts(counts: Dictionary) -> void:
	if not is_instance_valid(population_badges): return
	var named_counts := {}
	for id: String in _all_place_ids():
		var count := int(counts.get(id, 0))
		if count > 0: named_counts[id] = count
	# Hidden compatibility waypoints have no population badge on the free map.
	if population_badges.counts == named_counts: return
	super.set_population_counts(named_counts)

func can_contact_map_npc(npc_id: String) -> bool:
	return movement_mode not in ["flight", "player_flight"] and npc_id in get_nearby_person_ids()

func _approach_person(npc_id: String) -> void:
	if encounter_blocked or movement_mode in ["assigned", "flight", "player_flight"] or not nearby_people.has(npc_id): return
	if can_contact_map_npc(npc_id):
		stop_free_travel()
		world_npc_selected.emit(npc_id)
	elif navigate_to_world(nearby_people[npc_id].position): _target_npc = npc_id

func spawn_visible_encounter(event: Dictionary) -> bool:
	if movement_mode in ["flight", "player_flight"] or waypoints.is_empty(): return false
	for existing: Dictionary in hazards:
		if str(existing.id) == str(event.id): return true
	var ahead := 150.0
	var previous := player_mark.position
	for waypoint: Dictionary in waypoints:
		var length := previous.distance_to(waypoint.position)
		if length >= ahead:
			var point := previous.move_toward(waypoint.position, ahead)
			for id: String in _all_place_ids():
				if point.distance_to(travel.graph.positions[id]) < ENTRANCE_REACH * 2.0: return false
			var row := event.duplicate(true)
			row.position = point
			hazards.append(row)
			_rebuild_hazards()
			world_state_changed.emit()
			return true
		ahead -= length
		previous = waypoint.position
	return false

func _rebuild_hazards() -> void:
	for view: Node in hazard_views.values(): view.queue_free()
	hazard_views.clear()
	for row: Dictionary in hazards:
		var hit := Button.new()
		hit.name = "Encounter_" + str(row.id).validate_node_name()
		hit.set_meta("free_hazard_id", str(row.id))
		hit.text = "兽" if row.kind == "wildlife" else "匪"
		hit.size = Vector2(32, 32)
		hit.position = (row.position as Vector2) - hit.size * 0.5
		hit.add_theme_font_size_override("font_size", 20)
		hit.add_theme_color_override("font_color", Color("873c2c"))
		hit.add_theme_stylebox_override("normal", _location_paper())
		hit.add_theme_stylebox_override("hover", _location_paper())
		hit.tooltip_text = "野兽踪迹 · 可绕行" if row.kind == "wildlife" else "劫匪出没 · 可绕行"
		hit.z_index = 45
		hit.pressed.connect(_approach_hazard.bind(str(row.id)))
		world.add_child(hit)
		hazard_views[str(row.id)] = hit

func _approach_hazard(id: String) -> void:
	for row: Dictionary in hazards:
		if str(row.id) == id: navigate_to_world(row.position)

func _check_hazard_contact() -> bool:
	if encounter_blocked or movement_mode in ["assigned", "flight", "player_flight"]: return false
	for row: Dictionary in hazards:
		if player_mark.position.distance_to(row.position) <= HAZARD_REACH and navigation.segment_clear(player_mark.position, row.position):
			encounter_blocked = true
			_finish_motion()
			visible_encounter_requested.emit(row.duplicate(true))
			return true
	return false

func resolve_visible_encounter(id: String) -> void:
	for i in range(hazards.size() - 1, -1, -1):
		if str(hazards[i].id) == id: hazards.remove_at(i)
	_rebuild_hazards()
	world_state_changed.emit()

func get_node_state() -> Dictionary:
	var state: Dictionary = travel.persistent_state()
	state.version = 2
	state.world_size = WorldScale.MAP_PIXELS
	state.world_km = WorldScale.WORLD_KM
	state.terrain_speed_profile = TerrainTravel.PROFILE_ID
	state.navigation_profile = Navigation.PROFILE_ID
	# Only the NPC manager can confirm that every saved NPC was migrated. In
	# particular an empty source profile must survive a failed batch migration.
	var npc_manager := get_node_or_null("/root/NpcWorldManager")
	state.npc_navigation_profile = npc_manager.call("get_npc_navigation_save_profile") if npc_manager != null and npc_manager.has_method("get_npc_navigation_save_profile") else ""
	if npc_manager != null and npc_manager.has_method("get_npc_navigation_save_world_size"):
		state.npc_world_size = npc_manager.call("get_npc_navigation_save_world_size")
		state.npc_terrain_speed_profile = npc_manager.call("get_npc_terrain_save_profile")
		state.npc_source_version = npc_manager.call("get_npc_navigation_save_version")
	state.world_position = [player_mark.position.x, player_mark.position.y]
	state.waypoints = []
	for waypoint: Dictionary in waypoints:
		var point: Vector2 = waypoint.position
		state.waypoints.append([point.x, point.y, str(waypoint.node)])
	if _clock_waiting and not _pending_checkpoint.is_empty():
		state.waypoints.push_front([player_mark.position.x, player_mark.position.y, _pending_checkpoint])
	state.destination_node = destination_node
	state.movement_mode = movement_mode
	if movement_mode in ["flight", "player_flight"]:
		state.flight_speed = flight_speed
		state.flight_height = flight_height
		state.flight_driver_id = flight_driver_id
		if movement_mode == "player_flight": state.player_boat_id = player_boat_id
	state.resume_movement = _resume_requested and not waypoints.is_empty()
	state.target_npc = _target_npc
	state.visible_encounters = []
	for hazard: Dictionary in hazards:
		var row := hazard.duplicate(true)
		var point: Vector2 = row.position
		row.position = [point.x, point.y]
		state.visible_encounters.append(row)
	return state

func restore_node_state(state: Dictionary) -> bool:
	if travel_busy or _clock_waiting: return false
	# Validate before changing live state. Corrupt coordinates cannot teleport or
	# silently erase a route, an encounter or an assigned checkpoint.
	var version := int(state.get("version", 1))
	if version not in [1, 2] or not state.get("remaining_route", []) is Array: return false
	if state.get("remaining_route", []).size() > 20000: return false
	for id: Variant in state.get("remaining_route", []):
		if not id is String: return false
	if version == 1 and not str(state.get("navigation_profile", "")).is_empty(): return false
	if version == 1 and state.has("world_size") and absf(float(state.world_size) - WorldScale.LEGACY_MAP_PIXELS) >= 0.001: return false
	if state.has("world_km") and not is_equal_approx(float(state.world_km), WorldScale.WORLD_KM if version == 2 else WorldScale.LEGACY_WORLD_KM): return false
	var coordinate_scale := 1.0
	if version == 2:
		if not state.has("world_size") or not state.has("world_km"): return false
		var source_world_size := float(state.world_size)
		if not is_finite(source_world_size) or source_world_size <= 0.0: return false
		var source_profile := str(state.get("navigation_profile", ""))
		if absf(source_world_size - WorldScale.MAP_PIXELS) >= 0.001 or source_profile != Navigation.PROFILE_ID:
			if preload("res://gdscripts/world/WorldMapSourceRegistry.gd").resolve(source_profile, source_world_size).is_empty(): return false
			var upgraded := _upgrade_previous_navigation_state(state, WorldScale.MAP_PIXELS / source_world_size)
			if upgraded.is_empty(): return false
			return restore_node_state(upgraded)
	var candidate = Travel.new()
	# Graph/layout are immutable during this map session. Reuse them instead of
	# reparsing a megabyte of map data for every save restoration or location hop.
	candidate.graph = travel.graph
	if not candidate.restore_state(state): return false
	if int(state.get("version", 1)) < 2:
		var legacy_waypoints: Array[Dictionary] = []
		if candidate.next_index > 0:
			for i in range(candidate.next_index, candidate.route.size()):
				var points := _assigned_leg_points(candidate.route[i - 1], candidate.route[i])
				if points.size() < 2: return false
				for j in range(1, points.size()):
					legacy_waypoints.append({"position": points[j], "node": candidate.route[i] if j == points.size() - 1 else ""})
		travel = candidate
		player_mark.position = travel.graph.positions[travel.current_node]
		player_halo.position = player_mark.position
		_last_avatar_position = player_mark.position
		movement_mode = "free"
		waypoints = legacy_waypoints
		hazards.clear()
		_target_npc = ""
		flight_speed = 0.0
		flight_height = 0.0
		flight_driver_id = ""
		destination_node = travel.route[-1] if travel.next_index > 0 else ""
		if travel.next_index > 0:
			movement_mode = "assigned"
			if not bool(state.get("legacy_assigned_route", false)):
				# Ordinary old node trips become editable geometric paths. Only
				# a real escort retains its authored checkpoint restrictions.
				for waypoint: Dictionary in waypoints: waypoint.node = ""
				travel.route.clear()
				travel.next_index = 0
				movement_mode = "free"
		_resume_requested = bool(state.get("resume_movement", not waypoints.is_empty())) and not waypoints.is_empty()
	else:
		var raw_position = state.get("world_position")
		if not raw_position is Array or raw_position.size() != 2: return false
		if typeof(raw_position[0]) not in [TYPE_INT, TYPE_FLOAT] or typeof(raw_position[1]) not in [TYPE_INT, TYPE_FLOAT]: return false
		var point := Vector2(float(raw_position[0]), float(raw_position[1])) * coordinate_scale
		var mode := str(state.get("movement_mode", "free"))
		if mode not in ["free", "assigned", "flight", "player_flight"]: return false
		var saved_destination := str(state.get("destination_node", ""))
		if not saved_destination.is_empty() and not candidate.graph.positions.has(saved_destination): return false
		if not point.is_finite() or point.x < 0.0 or point.y < 0.0 or point.x > WorldScale.MAP_PIXELS or point.y > WorldScale.MAP_PIXELS: return false
		if mode not in ["flight", "player_flight"] and not navigation.is_walkable(point): return false
		var parsed: Array[Dictionary] = []
		var previous := point
		var raw_waypoints = state.get("waypoints", [])
		if not raw_waypoints is Array or raw_waypoints.size() > 20000: return false
		for row in raw_waypoints:
			if not row is Array or row.size() != 3: return false
			if typeof(row[0]) not in [TYPE_INT, TYPE_FLOAT] or typeof(row[1]) not in [TYPE_INT, TYPE_FLOAT] or not row[2] is String: return false
			var next := Vector2(float(row[0]), float(row[1])) * coordinate_scale
			if not next.is_finite() or next.x < 0.0 or next.y < 0.0 or next.x > WorldScale.MAP_PIXELS or next.y > WorldScale.MAP_PIXELS: return false
			if mode not in ["flight", "player_flight"] and not navigation.segment_clear(previous, next): return false
			if not str(row[2]).is_empty() and not travel.graph.positions.has(str(row[2])): return false
			parsed.append({"position": next, "node": str(row[2])})
			previous = next
		var parsed_hazards: Array[Dictionary] = []
		var raw_hazards = state.get("visible_encounters", [])
		if not raw_hazards is Array: return false
		var seen_hazards := {}
		for raw in raw_hazards:
			if not raw is Dictionary or not raw.get("position") is Array or raw.position.size() != 2: return false
			if typeof(raw.position[0]) not in [TYPE_INT, TYPE_FLOAT] or typeof(raw.position[1]) not in [TYPE_INT, TYPE_FLOAT]: return false
			if str(raw.get("id", "")).is_empty() or not str(raw.get("kind", "")) in ["wildlife", "bandit"]: return false
			if seen_hazards.has(str(raw.id)): return false
			seen_hazards[str(raw.id)] = true
			var row: Dictionary = raw.duplicate(true)
			row.position = Vector2(float(raw.position[0]), float(raw.position[1])) * coordinate_scale
			if not navigation.is_walkable(row.position): return false
			parsed_hazards.append(row)
		var checkpoints: Array[String] = []
		for waypoint: Dictionary in parsed:
			if str(waypoint.node).is_empty(): continue
			if (waypoint.position as Vector2).distance_to(candidate.graph.positions[str(waypoint.node)]) > 0.01: return false
			checkpoints.append(str(waypoint.node))
		if mode == "assigned":
			var expected: Array[String] = []
			if candidate.next_index > 0: expected.assign(candidate.route.slice(candidate.next_index))
			if checkpoints != expected: return false
		elif not checkpoints.is_empty() or candidate.next_index > 0: return false
		var parsed_flight_speed := 0.0
		var parsed_flight_height := 0.0
		var parsed_driver_id := ""
		var parsed_boat_id := ""
		if mode in ["flight", "player_flight"]:
			parsed_flight_speed = float(state.get("flight_speed", 0.0))
			parsed_flight_height = float(state.get("flight_height", -1.0))
			parsed_driver_id = str(state.get("flight_driver_id", ""))
			var dest := str(state.get("destination_node", ""))
			if not is_finite(parsed_flight_speed) or parsed_flight_speed <= 0.0 or not is_finite(parsed_flight_height) or parsed_flight_height < 0.0: return false
			if mode == "flight":
				if parsed_driver_id.is_empty() or parsed.size() != 1 or not candidate.graph.positions.has(dest): return false
				if (parsed[0].position as Vector2).distance_to(candidate.graph.positions[dest]) > 0.01: return false
			else:
				parsed_boat_id = str(state.get("player_boat_id", ""))
				if parsed_boat_id.is_empty() or parsed.size() > 1 or not _boat_validate.is_valid() or not bool(_boat_validate.call(parsed_boat_id, parsed_flight_speed)): return false
				if not parsed.is_empty() and not navigation.is_walkable(parsed[0].position): return false
				if not dest.is_empty() and not parsed.is_empty() and (parsed[0].position as Vector2).distance_to(candidate.graph.positions[dest]) > 0.01: return false
			if not _ensure_flight_boat(parsed_driver_id): return false
		travel = candidate
		player_mark.position = point
		player_halo.position = point
		_last_avatar_position = point
		waypoints = parsed
		hazards = parsed_hazards
		movement_mode = mode
		flight_speed = parsed_flight_speed
		flight_height = parsed_flight_height
		flight_driver_id = parsed_driver_id
		player_boat_id = parsed_boat_id
		destination_node = str(state.get("destination_node", ""))
		_target_npc = str(state.get("target_npc", ""))
		_resume_requested = bool(state.get("resume_movement", false)) and not waypoints.is_empty()
	_rebuild_remaining_metrics()
	_rebuild_hazards()
	_update_context()
	_refresh_actions()
	_update_route_line()
	focus_player_node()
	_update_status()
	_update_flight_visual()
	return true

func _upgrade_previous_navigation_state(state: Dictionary, coordinate_scale: float) -> Dictionary:
	# Source coordinates are validated before scaling, against that release's
	# own grid. Opaque node/escort indices and flight altitude never scale.
	const Sources = preload("res://gdscripts/world/WorldMapSourceRegistry.gd")
	var source_size := float(state.get("world_size", 0.0))
	var source_profile := str(state.get("navigation_profile", ""))
	var source := Sources.resolve(source_profile, source_size)
	if source.is_empty() or not is_finite(coordinate_scale) or absf(coordinate_scale - WorldScale.MAP_PIXELS / source_size) > 0.000001: return {}
	# Identical authored routes and terrain preserve a journey exactly while
	# its points and segments remain legal in the current navigation release.
	# A restored mountain may close a former gap; that journey uses the normal
	# nearest-land and path migration after its original source is validated.
	var shared_geometry := absf(source_size - WorldScale.MAP_PIXELS) < 0.001 and str(source.layout_path) == WorldScale.LAYOUT_PATH and str(state.get("terrain_speed_profile", "")) == TerrainTravel.PROFILE_ID
	var preserve_geometry := shared_geometry
	var old_graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd").new()
	old_graph.use_free_world = true
	if not old_graph.load_layout(str(source.layout_path)): return {}
	var candidate = Travel.new()
	candidate.graph = travel.graph
	if not state.get("remaining_route", []) is Array or not candidate.restore_state(state): return {}
	var upgraded := state.duplicate(true)
	var raw: Variant = state.get("world_position")
	var mode := str(state.get("movement_mode", "free"))
	if not raw is Array or raw.size() != 2 or mode not in ["free", "assigned", "flight", "player_flight"]: return {}
	if typeof(raw[0]) not in [TYPE_INT, TYPE_FLOAT] or typeof(raw[1]) not in [TYPE_INT, TYPE_FLOAT]: return {}
	var old_point := Vector2(float(raw[0]), float(raw[1]))
	if not Sources.contains(old_point, source_size): return {}
	if mode not in ["flight", "player_flight"] and not Sources.is_walkable(old_point, source_profile, source_size): return {}
	var point := old_point if shared_geometry else old_point * coordinate_scale
	if mode not in ["flight", "player_flight"]:
		if not navigation.is_walkable(point):
			preserve_geometry = false
			point = navigation.nearest_walkable(point)
	if not Sources.contains(point, WorldScale.MAP_PIXELS): return {}
	upgraded.world_position = raw.duplicate() if shared_geometry and point == old_point else [point.x, point.y]
	var rows: Variant = state.get("waypoints", [])
	if not rows is Array or rows.size() > 20000: return {}
	var old_points: Array[Vector2] = []
	var checkpoints: Array[String] = []
	var previous_old := old_point
	for row in rows:
		if not row is Array or row.size() != 3: return {}
		if typeof(row[0]) not in [TYPE_INT, TYPE_FLOAT] or typeof(row[1]) not in [TYPE_INT, TYPE_FLOAT] or not row[2] is String: return {}
		var old_target := Vector2(float(row[0]), float(row[1]))
		if not Sources.contains(old_target, source_size): return {}
		if mode not in ["flight", "player_flight"] and not Sources.segment_clear(previous_old, old_target, source_profile, source_size): return {}
		if preserve_geometry and mode not in ["flight", "player_flight"] and not navigation.segment_clear(previous_old, old_target): preserve_geometry = false
		previous_old = old_target
		old_points.append(old_target * coordinate_scale)
		var checkpoint := str(row[2])
		if not checkpoint.is_empty():
			if not old_graph.positions.has(checkpoint) or not travel.graph.positions.has(checkpoint): return {}
			if old_target.distance_to(old_graph.positions[checkpoint]) > 0.02: return {}
			checkpoints.append(checkpoint)
	var expected: Array[String] = []
	if candidate.next_index > 0: expected.assign(candidate.route.slice(candidate.next_index))
	if mode == "assigned":
		if checkpoints != expected: return {}
	elif not checkpoints.is_empty() or candidate.next_index > 0: return {}
	var parsed: Array = []
	var destination := str(state.get("destination_node", ""))
	if not destination.is_empty() and (not old_graph.positions.has(destination) or not travel.graph.positions.has(destination)): return {}
	if mode == "flight":
		if rows.size() != 1 or destination.is_empty(): return {}
		var old_destination := Vector2(float(rows[0][0]), float(rows[0][1]))
		if old_destination.distance_to(old_graph.positions[destination]) > 0.02: return {}
		var target: Vector2 = travel.graph.positions[destination]
		parsed.append([target.x, target.y, ""])
	elif mode == "player_flight":
		if rows.size() > 1: return {}
		if not rows.is_empty():
			if preserve_geometry and not navigation.is_walkable(old_points[0]): preserve_geometry = false
			var target: Vector2 = travel.graph.positions[destination] if not destination.is_empty() else navigation.nearest_walkable(old_points[0])
			if not Sources.contains(target, WorldScale.MAP_PIXELS) or not navigation.is_walkable(target): return {}
			parsed.append([target.x, target.y, ""])
	elif not preserve_geometry and not rows.is_empty():
		var targets: Array[Dictionary] = []
		if mode == "assigned":
			for id: String in checkpoints: targets.append({"point": travel.graph.positions[id], "id": id})
		else:
			var target: Vector2 = travel.graph.positions[destination] if not destination.is_empty() else navigation.nearest_walkable(old_points[-1])
			if not Sources.contains(target, WorldScale.MAP_PIXELS): return {}
			targets.append({"point": target, "id": ""})
		var previous := point
		for target: Dictionary in targets:
			var path: PackedVector2Array = navigation.find_path(previous, target.point)
			if path.is_empty(): return {}
			for i in range(1, path.size()): parsed.append([path[i].x, path[i].y, target.id if i == path.size() - 1 else ""])
			if path.size() == 1 and not str(target.id).is_empty(): parsed.append([previous.x, previous.y, target.id])
			previous = target.point
	if preserve_geometry: parsed = rows.duplicate(true)
	upgraded.waypoints = parsed
	var hazards_raw: Variant = state.get("visible_encounters", [])
	if not hazards_raw is Array: return {}
	var seen_hazards := {}
	for hazard in upgraded.get("visible_encounters", []):
		if not hazard is Dictionary or not hazard.get("position") is Array or hazard.position.size() != 2: return {}
		if typeof(hazard.position[0]) not in [TYPE_INT, TYPE_FLOAT] or typeof(hazard.position[1]) not in [TYPE_INT, TYPE_FLOAT]: return {}
		var id := str(hazard.get("id", ""))
		if id.is_empty() or seen_hazards.has(id) or str(hazard.get("kind", "")) not in ["wildlife", "bandit"]: return {}
		seen_hazards[id] = true
		var target := Vector2(float(hazard.position[0]), float(hazard.position[1]))
		if not Sources.is_walkable(target, source_profile, source_size): return {}
		var mapped_target := target if shared_geometry else target * coordinate_scale
		if not navigation.is_walkable(mapped_target): mapped_target = navigation.nearest_walkable(mapped_target)
		if not Sources.contains(mapped_target, WorldScale.MAP_PIXELS): return {}
		if not shared_geometry or mapped_target != target: hazard.position = [mapped_target.x, mapped_target.y]
	upgraded.world_size = WorldScale.MAP_PIXELS
	upgraded.world_km = WorldScale.WORLD_KM
	upgraded.navigation_profile = Navigation.PROFILE_ID
	return upgraded

func resume_saved_travel() -> void:
	if travel_busy or encounter_blocked or not _resume_requested or waypoints.is_empty(): return
	travel_busy = true
	travel_paused = false
	_last_completed_steps = 0
	node_travel_started.emit()
	_refresh_actions()
	_update_status()

func _build_assigned_waypoints() -> void:
	waypoints.clear()
	var remaining := get_remaining_travel_path()
	var previous := player_mark.position
	for i in range(1, remaining.size()):
		# Exact reviewed road curves are sampled onto traversable cells. At narrow
		# crossings their grid corridor is shared by free travel and NPCs.
		var connector := navigation.find_path(previous, travel.graph.positions[remaining[i - 1]])
		if i == 1 and connector.is_empty() and previous.distance_to(travel.graph.positions[remaining[i - 1]]) > 0.01:
			waypoints.clear()
			_rebuild_remaining_metrics()
			return
		if i == 1 and connector.size() > 1:
			for j in range(1, connector.size()): waypoints.append({"position": connector[j], "node": ""})
		var path := _assigned_leg_points(remaining[i - 1], remaining[i])
		if path.size() < 2:
			waypoints.clear()
			_rebuild_remaining_metrics()
			return
		for j in range(1, path.size()): waypoints.append({"position": path[j], "node": remaining[i] if j == path.size() - 1 else ""})
		previous = path[-1]
	_rebuild_remaining_metrics()

func _assigned_leg_points(from: String, to: String) -> PackedVector2Array:
	# Prefer the authored polyline where fully legal; navigation provides a
	# conservative discrete crossing route when the original curve hugs a bank.
	var points: PackedVector2Array = travel.graph.points_between(from, to)
	if navigation.validate_path(points): return points
	return navigation.find_path(travel.graph.positions[from], travel.graph.positions[to])

func get_path_travel_hours(path: Array) -> float:
	if not _free_ready: return super.get_path_travel_hours(path)
	if path.size() < 2: return 0.0
	if not travel.graph.positions.has(str(path[0])): return INF
	var connector := navigation.find_path(player_mark.position, travel.graph.positions[str(path[0])])
	if connector.is_empty() and player_mark.position.distance_to(travel.graph.positions[str(path[0])]) > 0.01: return INF
	var cost: float = terrain_travel.path_cost_length(connector)
	for i in range(1, path.size()):
		var from := str(path[i - 1])
		var to := str(path[i])
		if not travel.graph.positions.has(from) or not travel.graph.positions.has(to): return INF
		var points := _assigned_leg_points(from, to)
		if points.size() < 2: return INF
		cost += terrain_travel.path_cost_length(points)
	return travel.graph.hours_for_km(cost * Navigation.KM_PER_PIXEL, dun_speed)

func start_assigned_route(path: Array, destination_runtime_id: String) -> bool:
	if travel_busy or encounter_blocked: return false
	var ids: Array[String] = []
	ids.assign(path)
	if ids.is_empty() or ids[-1] != travel.graph.node_for_runtime_id(destination_runtime_id) or not travel.begin(ids): return false
	movement_mode = "assigned"
	destination_node = ids[-1]
	_build_assigned_waypoints()
	if waypoints.is_empty():
		movement_mode = "free"
		travel.route.clear()
		travel.next_index = 0
		return false
	_resume_requested = true
	resume_saved_travel()
	_update_route_line()
	return true

func pause_travel_at_node() -> void:
	travel_paused = true

func interrupt_assigned_route() -> void:
	waypoints.clear()
	travel.route.clear()
	travel.next_index = 0
	movement_mode = "free"
	_resume_requested = false
	_finish_motion()

func set_encounter_blocked(value: bool) -> void:
	encounter_blocked = value
	if value:
		_clear_formal_map_click_sequence()
		if travel_busy: _finish_motion()
	_refresh_actions()
