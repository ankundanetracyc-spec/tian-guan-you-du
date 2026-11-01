extends "res://scripts/world/bai_chuan/BaiChuanNineTileTest.gd"
## Formal world-map interaction over the Bai Chuan node graph.

signal return_requested
signal node_enter_requested(runtime_id: String)
signal world_state_changed
signal node_travel_started
signal node_travel_finished(node_id: String)
signal node_step_committed(node_id: String)

const Travel = preload("res://gdscripts/world/BaiChuanNodeTravel.gd")
const PLAYER_PAWN_TEXTURE: Texture2D = preload("res://textures/ui/world/player_pawn_marker_v6.tres")
const PLAYER_PAWN_HEIGHT := 64.0
var travel = Travel.new()
var dun_speed := 100.0
var selected_node := ""
var travel_busy := false
var travel_paused := false
var encounter_blocked := false
var last_travel_step: Dictionary = {}
var travel_button: Button
var enter_button: Button
var player_mark: Sprite2D
var population_badges: PopulationBadges

class PopulationBadges extends Node2D:
	var anchors: Dictionary = {}
	var counts: Dictionary = {}
	func _draw() -> void:
		var font := ThemeDB.fallback_font
		for id in counts:
			var count := int(counts[id])
			if count <= 0 or not anchors.has(id): continue
			var label := str(count)
			var width := maxf(18.0, font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1, 12).x + 8.0)
			var pos: Vector2 = anchors[id]
			var box := Rect2(pos, Vector2(width, 18.0))
			draw_rect(box.grow(1), Color("5d4d39"))
			draw_rect(box, Color("f4dfb1"))
			draw_string(font, pos + Vector2(4, 13), label, HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color("29231d"))

func _ready() -> void:
	add_to_group("node_world_map_presentation")
	if not travel.initialize("bc_place_001"):
		push_error("百川节点路网加载失败")
		return
	super._ready()
	_clean_preview_labels(self)
	player_mark = Sprite2D.new()
	player_mark.name = "PlayerNodeMark"
	player_mark.texture = PLAYER_PAWN_TEXTURE
	player_mark.centered = true
	player_mark.offset = Vector2(0, -PLAYER_PAWN_TEXTURE.get_height() * 0.5)
	player_mark.scale = Vector2.ONE * (PLAYER_PAWN_HEIGHT / PLAYER_PAWN_TEXTURE.get_height())
	player_mark.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	player_mark.z_index = 100
	player_mark.position = travel.graph.positions[travel.current_node]
	world.add_child(player_mark)
	_add_transit_buttons()
	_create_population_badges()
	var box: VBoxContainer = formal_info_box
	if box == null:
		var actions := _panel(self)
		actions.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
		actions.offset_left = -250
		actions.offset_top = 18
		actions.offset_right = -18
		actions.offset_bottom = 140
		box = VBoxContainer.new()
		actions.add_child(box)
	travel_button = _button(box,"前往所选地点",_travel_selected)
	enter_button = _button(box,"进入地点",_enter_current)
	_refresh_actions()

func _clean_preview_labels(node: Node) -> void:
	if node is Label:
		if node.text == "百川舆图 · 新版验收测试": node.text = "百川舆图"
		if node.text == "整图验收 · 本页不推进旅程": node.text = "选择地点查看路线"
	if node is Button and node.text == "返回首页": node.text = "返回"
	for child in node.get_children(): _clean_preview_labels(child)

func _process(delta: float) -> void:
	super._process(delta)
	if travel_busy and is_instance_valid(player_mark):
		camera_pos = player_mark.position-get_viewport_rect().size*0.5
		_update_camera()

func _select_place(node: Dictionary) -> void:
	super._select_place(node)
	selected_node = str(node.id)
	if is_instance_valid(travel_button):
		var preview: Dictionary = travel.preview(selected_node,dun_speed)
		if not preview.is_empty() and selected_node != travel.current_node:
			detail_label.text += "\n路程 %.1f 公里 · 预计 %.1f 小时" % [preview.kilometres,preview.hours]
		_refresh_actions()

func _add_transit_buttons() -> void:
	for node: Dictionary in data.get("transitNodes", []):
		var button := Button.new()
		button.name = "Transit_" + str(node.id)
		button.flat = true
		button.focus_mode = Control.FOCUS_NONE
		button.position = travel.graph.positions[node.id]-Vector2(8,8)
		button.size = Vector2(16,16)
		button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		for style in ["normal","hover","pressed","focus"]:
			button.add_theme_stylebox_override(style,StyleBoxEmpty.new())
		button.pressed.connect(_select_transit.bind(str(node.id)))
		places.add_child(button)

func _create_population_badges() -> void:
	population_badges = PopulationBadges.new()
	population_badges.name = "PopulationBadges"
	for row: Dictionary in presentation.placements:
		population_badges.anchors[str(row.id)] = Vector2(row.rect[0] + row.rect[2] - 4, row.rect[1] - 8)
	for node: Dictionary in data.get("transitNodes", []):
		population_badges.anchors[str(node.id)] = travel.graph.positions[node.id] + Vector2(5, -15)
	places.add_child(population_badges)

func set_population_counts(counts: Dictionary) -> void:
	if not is_instance_valid(population_badges): return
	population_badges.counts = counts.duplicate()
	population_badges.queue_redraw()

func _select_transit(id: String) -> void:
	selected_node = id
	title_label.text = "行旅途经点"
	var preview: Dictionary = travel.preview(id,dun_speed)
	detail_label.text = "当前位置" if id == travel.current_node else "路程 %.1f 公里 · 预计 %.1f 小时" % [preview.get("kilometres",0.0),preview.get("hours",0.0)]
	_refresh_actions()

func _refresh_actions() -> void:
	if not is_instance_valid(travel_button): return
	travel_button.disabled = travel_busy or encounter_blocked or selected_node.is_empty() or selected_node == travel.current_node
	enter_button.disabled = travel_busy or encounter_blocked or selected_node != travel.current_node or travel.graph.runtime_id_for_node(travel.current_node).is_empty()

func _travel_selected() -> void:
	if travel_busy or encounter_blocked or selected_node.is_empty(): return
	var preview: Dictionary = travel.preview(selected_node,dun_speed)
	if preview.is_empty(): return
	var path: Array[String] = []
	path.assign(preview.path)
	if not travel.begin(path): return
	_run_travel()

func resume_saved_travel() -> void:
	if travel_busy or encounter_blocked or travel.next_step(dun_speed).is_empty(): return
	_run_travel()

func _run_travel() -> void:
	travel_busy = true
	travel_paused = false
	_refresh_actions()
	node_travel_started.emit()
	while true:
		var step: Dictionary = travel.next_step(dun_speed)
		if step.is_empty(): break
		var points: PackedVector2Array = travel.graph.points_between(step.from,step.to)
		if points.size() >= 2 and absf(points[-1].x - points[0].x) > 0.01:
			player_mark.flip_h = points[-1].x < points[0].x
		var tween := create_tween()
		var duration := 0.42
		var length := 0.0
		for i in range(1,points.size()): length += points[i-1].distance_to(points[i])
		for i in range(1,points.size()):
			tween.tween_property(player_mark,"position",points[i],duration*points[i-1].distance_to(points[i])/maxf(length,0.001))
		await tween.finished
		if not travel.commit_step(step.to): break
		var time_manager := get_node_or_null("/root/GameTimeManager")
		if time_manager != null and time_manager.has_method("advance_time_smooth"):
			time_manager.call("advance_time_smooth",float(step.hours),"world_map_travel")
			await time_manager.smooth_time_advanced
		elif time_manager != null and time_manager.has_method("advance_time"):
			time_manager.call("advance_time",float(step.hours),"world_map_travel")
		world_state_changed.emit()
		last_travel_step = step
		node_step_committed.emit(travel.current_node)
		if travel_paused: break
	travel_busy = false
	_refresh_actions()
	node_travel_finished.emit(travel.current_node)

func pause_travel_at_node() -> void:
	travel_paused = true

func set_encounter_blocked(value: bool) -> void:
	encounter_blocked = value
	_refresh_actions()

func get_last_travel_step() -> Dictionary:
	return last_travel_step.duplicate()

func _enter_current() -> void:
	if travel_busy or encounter_blocked or selected_node != travel.current_node: return
	var runtime_id: String = travel.graph.runtime_id_for_node(travel.current_node)
	if not runtime_id.is_empty(): node_enter_requested.emit(runtime_id)

func set_dun_speed(value: float) -> void:
	dun_speed = maxf(value,0.01)

func get_node_state() -> Dictionary:
	return travel.persistent_state()

func get_current_node() -> String:
	return travel.current_node

func get_current_neighbours() -> Array[String]:
	var result: Array[String] = []
	for link: Dictionary in travel.graph.adjacency[travel.current_node]: result.append(link.to)
	return result

func get_runtime_id(node_id: String) -> String:
	return travel.graph.runtime_id_for_node(node_id)

func set_initial_runtime_id(runtime_id: String) -> bool:
	var node_id: String = travel.graph.node_for_runtime_id(runtime_id)
	if node_id.is_empty(): return false
	return restore_node_state({"current_node": node_id, "remaining_route": []})

func get_assigned_route_to_node(runtime_id: String) -> Array[String]:
	var destination: String = travel.graph.node_for_runtime_id(runtime_id)
	return travel.graph.shortest_path(travel.current_node, destination)

func get_path_travel_hours(path: Array) -> float:
	var ids: Array[String] = []
	ids.assign(path)
	return travel.graph.hours_for_km(travel.graph.path_km(ids), dun_speed)

func start_assigned_route(path: Array, destination_runtime_id: String) -> bool:
	if travel_busy or encounter_blocked: return false
	var ids: Array[String] = []
	ids.assign(path)
	if ids.is_empty() or ids[-1] != travel.graph.node_for_runtime_id(destination_runtime_id): return false
	if not travel.begin(ids): return false
	_run_travel()
	return true

func interrupt_assigned_route() -> void:
	travel.route.clear()
	travel.next_index = 0
	world_state_changed.emit()

func is_traveling() -> bool:
	return travel_busy

func get_remaining_travel_path() -> Array[String]:
	if travel.next_index <= 0: return [travel.current_node]
	var remaining: Array[String] = []
	remaining.assign(travel.route.slice(travel.next_index - 1))
	return remaining

func restore_node_state(state: Dictionary) -> bool:
	if travel_busy or not travel.restore_state(state): return false
	if is_instance_valid(player_mark):
		player_mark.position = travel.graph.positions[travel.current_node]
		focus_player_node()
	_refresh_actions()
	return true

func focus_player_node() -> void:
	if not is_instance_valid(player_mark): return
	camera_pos = player_mark.position - get_viewport_rect().size * 0.5
	_update_camera()

func _return_to_menu() -> void:
	return_requested.emit()
