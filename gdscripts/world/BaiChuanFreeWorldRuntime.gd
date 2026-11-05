extends "res://gdscripts/world/BaiChuanNodeMapRuntime.gd"
## Continuous travel, while retaining authored location and escort identifiers.

signal distance_traveled(kilometres: float)
signal visible_encounter_requested(event: Dictionary)
signal nearby_people_changed
signal world_npc_selected(npc_id: String)
signal location_context_changed(node_id: String)

const Navigation = preload("res://gdscripts/world/BaiChuanFreeNavigation.gd")
const TerrainTravel = preload("res://gdscripts/world/BaiChuanTerrainTravel.gd")
const WorldScale = preload("res://gdscripts/world/BaiChuanWorldScale.gd")
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
var _clock_committed_km := 0.0
var _pending_checkpoint := ""
var scale_ruler: MapScaleRuler

class MapScaleRuler extends Control:
	var kilometres_per_screen_pixel := 1.0
	func _draw() -> void:
		var candidates := [10.0, 20.0, 50.0, 100.0, 200.0, 500.0, 1000.0, 2000.0]
		var kilometres := 10.0
		for candidate: float in candidates:
			if candidate / maxf(kilometres_per_screen_pixel, 0.0001) <= 155.0: kilometres = candidate
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
	return 1.65 * WorldScale.COORDINATE_SCALE

func _far_city_icon_limit() -> float:
	return MAX_FAR_CITY_ICON_COMPENSATION * WorldScale.COORDINATE_SCALE

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
	var clock := get_node_or_null("/root/GameTimeManager")
	if clock != null and clock.has_signal("smooth_time_advanced"):
		clock.connect("smooth_time_advanced", _on_clock_settled)

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
	var focus := _button(travel_controls, "定位", focus_player_node)
	focus.name = "FocusFreeTraveler"
	for button: Button in [stop_button, focus]:
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
	elif travel_busy:
		travel_status.text = "行旅中 · %s · 余 %.1f 公里 · %.1f 小时" % [surface, km, _remaining_hours()]
	else: travel_status.text = "%s · 空地点击前往 · 空格停下" % surface
	stop_button.disabled = not travel_busy or movement_mode == "assigned" or encounter_blocked
	var panel := travel_controls.get_parent() as Control
	panel.position.x = maxf(476.0, get_viewport_rect().size.x * 0.5 - panel.size.x * 0.5)
	panel.position.x = minf(panel.position.x, get_viewport_rect().size.x - panel.size.x - 16.0)
	if is_instance_valid(scale_ruler):
		scale_ruler.position = Vector2(get_viewport_rect().size.x * 0.5 - 90.0, get_viewport_rect().size.y - 148.0)
		scale_ruler.kilometres_per_screen_pixel = Navigation.KM_PER_PIXEL / maxf(world_zoom, 0.01)
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
	return not encounter_blocked and movement_mode != "assigned"

func _on_formal_map_double_click(node_id: String) -> void:
	if encounter_blocked or movement_mode == "assigned": return
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
	var points := navigation.find_path(player_mark.position, travel.graph.positions[selected_node])
	var km: float = navigation.path_length(points) * Navigation.KM_PER_PIXEL
	var hours: float = travel.graph.hours_for_km(terrain_travel.path_cost_length(points) * Navigation.KM_PER_PIXEL, dun_speed)
	detail_label.text = "附近 · 可进入" if selected_node == _nearby_location else ("路程 %.1f 公里 · 预计 %.1f 小时" % [km, hours] if not points.is_empty() else "暂不可达")
	_refresh_actions()

func _travel_selected() -> void:
	if selected_node.is_empty() or not travel.graph.positions.has(selected_node): return
	navigate_to_world(travel.graph.positions[selected_node], selected_node)

func navigate_to_world(point: Vector2, node_id := "") -> bool:
	if not _free_ready or encounter_blocked or movement_mode == "assigned": return false
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
	if movement_mode == "assigned" or encounter_blocked: return
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
	super._process(delta)

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
		if distance <= best and navigation.segment_clear(player_mark.position, point):
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
	enter_button.disabled = not available
	enter_button.visible = available and _visible_place_ids.has(_nearby_location)
	if enter_button.visible: _position_location_control(enter_button, _nearby_location)
	formal_details_panel.visible = not selected_node.is_empty() and selected_node != _nearby_location and _selected_node_is_visible() and not travel_busy
	if formal_details_panel.visible: _position_location_control(formal_details_panel, selected_node)

func _enter_current() -> void:
	if travel_busy or encounter_blocked or _nearby_location.is_empty(): return
	# Enter/exit preserves the exact nearby position; no free distance or snapping.
	travel.current_node = _nearby_location
	world_state_changed.emit()
	node_enter_requested.emit(get_current_location_runtime_id())

func _remaining_length() -> float:
	return _remaining_path_length

func _remaining_cost_length() -> float:
	return _remaining_path_cost

func _remaining_hours() -> float:
	return travel.graph.hours_for_km(_remaining_path_cost * Navigation.KM_PER_PIXEL, dun_speed)

func _rebuild_remaining_metrics() -> void:
	# Rebuild only after route changes or restoration. Long assigned routes must
	# not integrate thousands of unchanged segments for each frame or HUD tick.
	_remaining_path_length = 0.0
	_remaining_path_cost = 0.0
	var previous := player_mark.position if is_instance_valid(player_mark) else Vector2.ZERO
	for waypoint: Dictionary in waypoints:
		_remaining_path_length += previous.distance_to(waypoint.position)
		_remaining_path_cost += terrain_travel.segment_cost_length(previous, waypoint.position)
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
		var view: Sprite2D = npc_views.get(id)
		if not is_instance_valid(view):
			view = Sprite2D.new()
			view.name = "Traveler_" + id.validate_node_name()
			view.texture = PLAYER_PAWN_TEXTURE
			view.offset = Vector2(0, -PLAYER_PAWN_TEXTURE.get_height() * 0.5)
			view.modulate = Color("657261")
			view.z_index = 40
			world.add_child(view)
			var hit := Button.new()
			hit.name = "Approach"
			hit.flat = true
			hit.focus_mode = Control.FOCUS_NONE
			hit.set_meta("free_npc_id", id)
			hit.size = Vector2(26, 36) / (26.0 / PLAYER_PAWN_TEXTURE.get_height())
			hit.position = Vector2(-hit.size.x * 0.5, -hit.size.y)
			hit.tooltip_text = "%s · %s" % [row.name, row.realm]
			hit.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
			hit.pressed.connect(_approach_person.bind(id))
			for state in ["normal", "hover", "pressed", "focus"]: hit.add_theme_stylebox_override(state, StyleBoxEmpty.new())
			view.add_child(hit)
			npc_views[id] = view
		view.position = row.position
		_scale_npc(view)
	for id: String in npc_views.keys():
		if not present.has(id):
			npc_views[id].queue_free()
			npc_views.erase(id)
	var ids := get_nearby_person_ids()
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
	view.scale = Vector2.ONE * 26.0 / (PLAYER_PAWN_TEXTURE.get_height() * maxf(world_zoom, 0.01))
	view.visible = _zoom_level >= 1

func get_nearby_person_ids() -> PackedStringArray:
	var ids := PackedStringArray()
	var manager := get_node_or_null("/root/NpcWorldManager")
	if manager == null or not manager.has_method("get_free_world_map_people"): return ids
	var samples: Array = manager.call("get_free_world_map_people", player_mark.position, CONTACT_REACH)
	for row: Dictionary in samples:
		if navigation.segment_clear(player_mark.position, row.position): ids.append(str(row.id))
	return ids

func can_contact_map_npc(npc_id: String) -> bool:
	return npc_id in get_nearby_person_ids()

func _approach_person(npc_id: String) -> void:
	if encounter_blocked or movement_mode == "assigned" or not nearby_people.has(npc_id): return
	if can_contact_map_npc(npc_id):
		stop_free_travel()
		world_npc_selected.emit(npc_id)
	elif navigate_to_world(nearby_people[npc_id].position): _target_npc = npc_id

func spawn_visible_encounter(event: Dictionary) -> bool:
	if waypoints.is_empty(): return false
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
	if encounter_blocked or movement_mode == "assigned": return false
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
	state.world_position = [player_mark.position.x, player_mark.position.y]
	state.waypoints = []
	for waypoint: Dictionary in waypoints:
		var point: Vector2 = waypoint.position
		state.waypoints.append([point.x, point.y, str(waypoint.node)])
	if _clock_waiting and not _pending_checkpoint.is_empty():
		state.waypoints.push_front([player_mark.position.x, player_mark.position.y, _pending_checkpoint])
	state.destination_node = destination_node
	state.movement_mode = movement_mode
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
	if state.has("world_km") and not is_equal_approx(float(state.world_km), WorldScale.WORLD_KM): return false
	var coordinate_scale := 1.0
	if state.has("world_size") and not is_equal_approx(float(state.world_size), WorldScale.MAP_PIXELS):
		# The previous 8000 km release used a larger display canvas. Only that
		# explicitly identified v2 scale can migrate; never guess from positions.
		if int(state.get("version", 1)) != 2 or not state.has("world_km") or not is_equal_approx(float(state.world_size), WorldScale.PREVIOUS_FREE_MAP_PIXELS): return false
		coordinate_scale = WorldScale.MAP_PIXELS / WorldScale.PREVIOUS_FREE_MAP_PIXELS
	var candidate = Travel.new()
	# Graph/layout are immutable during this map session. Reuse them instead of
	# reparsing a megabyte of map data for every save restoration or location hop.
	candidate.graph = travel.graph
	if not candidate.restore_state(state): return false
	if int(state.get("version", 1)) < 2:
		travel = candidate
		player_mark.position = travel.graph.positions[travel.current_node]
		player_halo.position = player_mark.position
		_last_avatar_position = player_mark.position
		movement_mode = "free"
		waypoints.clear()
		if travel.next_index > 0:
			movement_mode = "assigned"
			_build_assigned_waypoints()
			if not bool(state.get("legacy_assigned_route", false)):
				# Ordinary old node trips become editable geometric paths. Only
				# a real escort retains its authored checkpoint restrictions.
				for waypoint: Dictionary in waypoints: waypoint.node = ""
				travel.route.clear()
				travel.next_index = 0
				movement_mode = "free"
		_resume_requested = not waypoints.is_empty()
	else:
		var raw_position = state.get("world_position")
		if not raw_position is Array or raw_position.size() != 2: return false
		var point := Vector2(float(raw_position[0]), float(raw_position[1])) * coordinate_scale
		if not navigation.is_walkable(point): return false
		var parsed: Array[Dictionary] = []
		var previous := point
		var raw_waypoints = state.get("waypoints", [])
		if not raw_waypoints is Array or raw_waypoints.size() > 20000: return false
		for row in raw_waypoints:
			if not row is Array or row.size() != 3: return false
			var next := Vector2(float(row[0]), float(row[1])) * coordinate_scale
			if not navigation.segment_clear(previous, next): return false
			if not str(row[2]).is_empty() and not travel.graph.positions.has(str(row[2])): return false
			parsed.append({"position": next, "node": str(row[2])})
			previous = next
		var parsed_hazards: Array[Dictionary] = []
		var raw_hazards = state.get("visible_encounters", [])
		if not raw_hazards is Array: return false
		var seen_hazards := {}
		for raw in raw_hazards:
			if not raw is Dictionary or not raw.get("position") is Array or raw.position.size() != 2: return false
			if str(raw.get("id", "")).is_empty() or not str(raw.get("kind", "")) in ["wildlife", "bandit"]: return false
			if seen_hazards.has(str(raw.id)): return false
			seen_hazards[str(raw.id)] = true
			var row: Dictionary = raw.duplicate(true)
			row.position = Vector2(float(raw.position[0]), float(raw.position[1])) * coordinate_scale
			if not navigation.is_walkable(row.position): return false
			parsed_hazards.append(row)
		var mode := str(state.get("movement_mode", "free"))
		if not mode in ["free", "assigned"]: return false
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
		travel = candidate
		player_mark.position = point
		player_halo.position = point
		_last_avatar_position = point
		waypoints = parsed
		hazards = parsed_hazards
		movement_mode = mode
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
	return true

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
