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
const PLAYER_PAWN_SCREEN_HEIGHT := 32.0
const MAX_FAR_LABEL_COMPENSATION := 6.5
const MAX_FAR_CITY_ICON_COMPENSATION := 2.0
const FAR_PRIMARY_LABEL_SCREEN_SIZE := 15.0
const FAR_CITY_LABEL_SCREEN_SIZE := 14.0
const FAR_LABEL_MIN_COMPENSATION := 2.5
const FAR_LABEL_COLLISION_MARGIN_SCREEN := 3.0
const FAR_LABEL_MAX_SHIFT_SCREEN := 112.0
const FAR_CITY_STATIC_FONT_MIN_SCREEN_SIZE := 10.0
var travel = Travel.new()
var dun_speed := 100.0
var selected_node := ""
var travel_busy := false
var travel_paused := false
var encounter_blocked := false
var last_travel_step: Dictionary = {}
var travel_button: Button
var enter_button: Button
var current_location_label: Label
var player_mark: Sprite2D
var player_halo: PlayerPositionHalo
var population_badges: PopulationBadges
var far_city_hover_hint: PanelContainer
var far_city_hover_hint_label: Label
var transit_views: Dictionary = {}
var _visible_place_ids: Dictionary = {}
var _visible_transit_ids: Dictionary = {}
var _zoom_level := 0
var _world_zoom := 1.0
var _far_city_hover_fallback := false

class PopulationBadges extends Node2D:
	var anchors: Dictionary = {}
	var counts: Dictionary = {}
	var visible_node_ids: Dictionary = {}
	var font_scales: Dictionary = {}
	func _draw() -> void:
		var font := ThemeDB.fallback_font
		for id in counts:
			var count := int(counts[id])
			if count <= 0 or not anchors.has(id) or not visible_node_ids.has(id): continue
			var label := str(count)
			var scale := float(font_scales.get(id, 1.0))
			var font_size := maxi(1, roundi(12.0 * scale))
			var width := maxf(18.0 * scale, font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x + 8.0 * scale)
			var pos: Vector2 = anchors[id]
			var box := Rect2(pos, Vector2(width, 18.0 * scale))
			draw_rect(box.grow(1), Color("5d4d39"))
			draw_rect(box, Color("f4dfb1"))
			draw_string(font, pos + Vector2(4, 13) * scale, label, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size, Color("29231d"))

class PlayerPositionHalo extends Node2D:
	func _draw() -> void:
		draw_arc(Vector2.ZERO, 13.0, 0.0, TAU, 40, Color(0.24, 0.20, 0.15, 0.76), 1.2, true)
		draw_arc(Vector2.ZERO, 11.2, 0.0, TAU, 40, Color(0.76, 0.69, 0.55, 0.88), 1.0, true)

func _ready() -> void:
	add_to_group("node_world_map_presentation")
	if has_signal("map_zoom_changed"):
		connect("map_zoom_changed", _on_map_zoom_changed)
	if not travel.initialize("bc_place_001"):
		push_error("百川节点路网加载失败")
		return
	super._ready()
	_capture_marker_compensation_baselines()
	_clean_preview_labels(self)
	player_mark = Sprite2D.new()
	player_mark.name = "PlayerNodeMark"
	player_mark.texture = PLAYER_PAWN_TEXTURE
	player_mark.centered = true
	player_mark.offset = Vector2(0, -PLAYER_PAWN_TEXTURE.get_height() * 0.5)
	player_mark.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	player_mark.z_index = 100
	player_mark.position = travel.graph.positions[travel.current_node]
	world.add_child(player_mark)
	player_halo = PlayerPositionHalo.new()
	player_halo.name = "PlayerPositionHalo"
	player_halo.position = travel.graph.positions[travel.current_node]
	player_halo.z_index = 99
	world.add_child(player_halo)
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
	current_location_label = Label.new()
	current_location_label.name = "CurrentLocation"
	current_location_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	current_location_label.add_theme_font_size_override("font_size", 16)
	box.add_child(current_location_label)
	box.move_child(current_location_label, mini(1, box.get_child_count() - 1))
	_update_current_location()
	var action_buttons := HBoxContainer.new()
	action_buttons.name = "WorldMapActionButtons"
	action_buttons.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	action_buttons.add_theme_constant_override("separation", 6)
	box.add_child(action_buttons)
	travel_button = _button(action_buttons,"前往所选地点",_travel_selected)
	enter_button = _button(action_buttons,"进入地点",_enter_current)
	for button: Button in [travel_button, enter_button]:
		button.add_theme_font_size_override("font_size", 14)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_refresh_actions()
	_sync_initial_zoom_state()
	_create_far_city_hover_hint()
	_refresh_zoom_visibility()
	_update_player_mark_scale()
	focus_player_node()

func _clean_preview_labels(node: Node) -> void:
	if node is Label:
		if node.text == "百川舆图 · 新版验收测试": node.text = "百川舆图"
		if node.text == "整图验收 · 本页不推进旅程": node.text = "选择地点查看路线"
	if node is Button and node.text == "返回首页": node.text = "返回"
	for child in node.get_children(): _clean_preview_labels(child)

func _capture_marker_compensation_baselines() -> void:
	for row: Dictionary in presentation.placements:
		var marker: Control = place_views.get(str(row.id))
		if not is_instance_valid(marker): continue
		var icon: Sprite2D = marker.get_node("NativeIcon")
		var icon_hit: Control = marker.get_node("SelectIcon")
		var nameplate: Control = marker.get_node("SelectPlace")
		var icon_size := Vector2(icon.texture.get_size()) if icon.texture != null else Vector2.ZERO
		_capture_control_compensation(icon, icon.position, icon_size)
		_capture_control_compensation(icon_hit, icon_hit.position, icon_hit.size)
		_capture_control_compensation(nameplate, nameplate.position, nameplate.size)

func _capture_control_compensation(control: Node, base_position: Vector2, base_size: Vector2) -> void:
	control.set_meta("zoom_base_position", base_position)
	control.set_meta("zoom_base_size", base_size)
	control.set_meta("zoom_base_scale", Vector2(control.get("scale")))

func _process(delta: float) -> void:
	super._process(delta)
	if travel_busy and _zoom_level > 0 and is_instance_valid(player_mark):
		focus_world_point(player_mark.position)

func _on_map_zoom_changed(zoom: float, level: int) -> void:
	_world_zoom = maxf(zoom, 0.01)
	_zoom_level = clampi(level, 0, 2)
	_refresh_zoom_visibility()
	_update_player_mark_scale()
	if _zoom_level == 0 and not travel_busy:
		focus_player_node()

func _sync_initial_zoom_state() -> void:
	var zoom_value: Variant = get("world_zoom")
	if zoom_value != null:
		_world_zoom = maxf(float(zoom_value), 0.01)
	var level_value: Variant = get("zoom_level")
	if level_value != null:
		_zoom_level = clampi(int(level_value), 0, 2)

func _update_player_mark_scale() -> void:
	if not is_instance_valid(player_mark): return
	var world_height := PLAYER_PAWN_SCREEN_HEIGHT / maxf(_world_zoom, 0.01)
	player_mark.scale = Vector2.ONE * (world_height / PLAYER_PAWN_TEXTURE.get_height())
	if is_instance_valid(player_halo):
		player_halo.scale = Vector2.ONE / maxf(_world_zoom, 0.01)

func _apply_marker_compensation() -> void:
	var screen_factor := 1.0 / maxf(_world_zoom, 0.01)
	_far_city_hover_fallback = _should_use_far_city_hover_fallback() if _zoom_level == 0 else false
	var far_label_layout: Dictionary = {}
	if _zoom_level == 0:
		far_label_layout = _resolve_far_label_layout(screen_factor, not _far_city_hover_fallback)
		var min_city_label_size := float(far_label_layout.get("_min_city_screen_font_size", INF))
		if not _far_city_hover_fallback and (bool(far_label_layout.get("_city_labels_unresolved", false)) or min_city_label_size < FAR_CITY_STATIC_FONT_MIN_SCREEN_SIZE):
			_far_city_hover_fallback = true
			far_label_layout = _resolve_far_label_layout(screen_factor, false)
	if is_instance_valid(far_city_hover_hint): far_city_hover_hint.visible = false
	if is_instance_valid(population_badges): population_badges.font_scales.clear()
	for row: Dictionary in presentation.placements:
		var marker: Control = place_views.get(str(row.id))
		if not is_instance_valid(marker): continue
		var is_primary := str(row.get("type", "")) == "sect" and int(row.get("level", 0)) == 1
		var is_city := str(row.get("type", "")) == "city"
		var icon_factor := 1.0
		if is_primary and _zoom_level == 0:
			icon_factor = minf(1.65, screen_factor)
		elif is_city and _zoom_level == 0:
			icon_factor = minf(MAX_FAR_CITY_ICON_COMPENSATION, screen_factor)
		var label_layout: Dictionary = far_label_layout.get(str(row.id), {})
		var label_factor := float(label_layout.get("factor", 1.0))
		var label_offset: Vector2 = label_layout.get("offset", Vector2.ZERO)
		var icon_rect := Rect2(float(row.rect[0]), float(row.rect[1]), float(row.rect[2]), float(row.rect[3]))
		if is_instance_valid(population_badges):
			var id := str(row.id)
			if (is_primary or is_city) and _zoom_level == 0:
				var scaled_size := icon_rect.size * icon_factor
				var scaled_position := icon_rect.get_center() - scaled_size * 0.5
				population_badges.anchors[id] = scaled_position + Vector2(scaled_size.x - 4.0 * icon_factor, -8.0 * icon_factor)
				population_badges.font_scales[id] = label_factor if label_layout.size() > 0 else minf(MAX_FAR_LABEL_COMPENSATION, screen_factor)
			else:
				population_badges.anchors[id] = icon_rect.position + Vector2(icon_rect.size.x - 4.0, -8.0)
				population_badges.font_scales[id] = 1.0
		var icon: Sprite2D = marker.get_node("NativeIcon")
		var icon_hit: Control = marker.get_node("SelectIcon")
		var nameplate: Control = marker.get_node("SelectPlace")
		_apply_control_compensation(icon, icon_factor)
		_apply_control_compensation(icon_hit, icon_factor)
		_apply_control_compensation(nameplate, label_factor, true, label_offset)
		if is_city:
			nameplate.visible = not (_zoom_level == 0 and _far_city_hover_fallback)

func _create_far_city_hover_hint() -> void:
	far_city_hover_hint = PanelContainer.new()
	far_city_hover_hint.name = "FarCityHoverHint"
	far_city_hover_hint.visible = false
	far_city_hover_hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	far_city_hover_hint.z_index = 120
	var frame := StyleBoxFlat.new()
	frame.bg_color = Color("f4dfb1")
	frame.border_color = Color("5d4d39")
	frame.border_width_left = 1
	frame.border_width_top = 1
	frame.border_width_right = 1
	frame.border_width_bottom = 1
	frame.content_margin_left = 8
	frame.content_margin_top = 5
	frame.content_margin_right = 8
	frame.content_margin_bottom = 5
	far_city_hover_hint.add_theme_stylebox_override("panel", frame)
	far_city_hover_hint_label = Label.new()
	far_city_hover_hint_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	far_city_hover_hint_label.add_theme_font_override("font", load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"))
	far_city_hover_hint_label.add_theme_font_size_override("font_size", 16)
	far_city_hover_hint_label.add_theme_color_override("font_color", Color("292217"))
	far_city_hover_hint.add_child(far_city_hover_hint_label)
	add_child(far_city_hover_hint)

func _show_far_city_hover_hint(node_id: String) -> void:
	if not is_instance_valid(far_city_hover_hint) or not place_views.has(node_id): return
	var marker: Control = place_views[node_id]
	var nameplate: Button = marker.get_node("SelectPlace")
	var city_name := nameplate.text.replace("\n", "")
	if city_name.is_empty(): return
	far_city_hover_hint_label.text = city_name
	far_city_hover_hint.size = far_city_hover_hint.get_combined_minimum_size()
	var icon_hit: Control = marker.get_node("SelectIcon")
	var icon_rect := icon_hit.get_global_rect()
	var safe_rect: Rect2 = call("get_map_safe_rect") if has_method("get_map_safe_rect") else Rect2(Vector2.ZERO, get_viewport_rect().size)
	var hint_size := far_city_hover_hint.size
	var candidates: Array[Vector2] = [
		Vector2(icon_rect.get_center().x - hint_size.x * 0.5, icon_rect.position.y - hint_size.y - 8.0),
		Vector2(icon_rect.end.x + 8.0, icon_rect.get_center().y - hint_size.y * 0.5),
		Vector2(icon_rect.get_center().x - hint_size.x * 0.5, icon_rect.end.y + 8.0),
		Vector2(icon_rect.position.x - hint_size.x - 8.0, icon_rect.get_center().y - hint_size.y * 0.5)
	]
	var screen_position := safe_rect.position + Vector2(8.0, 8.0)
	for candidate in candidates:
		if safe_rect.encloses(Rect2(candidate, hint_size)):
			screen_position = candidate
			break
	var local_position := get_global_transform_with_canvas().affine_inverse() * screen_position
	far_city_hover_hint.position = local_position
	far_city_hover_hint.visible = true

func _hover_place(node_id: String) -> void:
	super._hover_place(node_id)
	if _zoom_level == 0 and _far_city_hover_fallback:
		var row := _placement_for_id(node_id)
		if not row.is_empty() and str(row.get("type", "")) == "city":
			_show_far_city_hover_hint(node_id)

func _clear_hover() -> void:
	super._clear_hover()
	if is_instance_valid(far_city_hover_hint):
		far_city_hover_hint.visible = false

func _placement_for_id(node_id: String) -> Dictionary:
	for row: Dictionary in presentation.placements:
		if str(row.get("id", "")) == node_id:
			return row
	return {}
func _should_use_far_city_hover_fallback() -> bool:
	var screen_factor := 1.0 / maxf(_world_zoom, 0.01)
	var smallest_city_font := INF
	for row: Dictionary in presentation.placements:
		if str(row.get("type", "")) != "city": continue
		var font_size := maxf(1.0, float(row.get("labelSize", 14)))
		var visible_font_size := font_size * minf(MAX_FAR_LABEL_COMPENSATION, screen_factor) * _world_zoom
		smallest_city_font = minf(smallest_city_font, visible_font_size)
	return smallest_city_font < FAR_CITY_STATIC_FONT_MIN_SCREEN_SIZE

func _resolve_far_label_layout(screen_factor: float, include_city_labels := true) -> Dictionary:
	var label_rows: Array[Dictionary] = []
	var icon_obstacles: Array[Rect2] = []
	var icon_label_offsets: Dictionary = {}
	var margin := FAR_LABEL_COLLISION_MARGIN_SCREEN / maxf(_world_zoom, 0.01)
	var safe_rect: Rect2 = call("get_map_safe_rect") if has_method("get_map_safe_rect") else Rect2(Vector2.ZERO, get_viewport_rect().size)
	var safe_world_rect := Rect2(camera_pos + safe_rect.position / maxf(_world_zoom, 0.01), safe_rect.size / maxf(_world_zoom, 0.01))
	for row: Dictionary in presentation.placements:
		var id := str(row.id)
		if not _visible_place_ids.has(id): continue
		var kind := str(row.get("type", ""))
		var is_primary := kind == "sect" and int(row.get("level", 0)) == 1
		var is_city := kind == "city"
		var icon_rect := Rect2(float(row.rect[0]), float(row.rect[1]), float(row.rect[2]), float(row.rect[3]))
		var icon_factor := minf(1.65, screen_factor) if is_primary else (minf(MAX_FAR_CITY_ICON_COMPENSATION, screen_factor) if is_city else 1.0)
		var label_offset := Vector2.ZERO
		if is_primary or is_city:
			var label_rect := Rect2(float(row.labelRect[0]), float(row.labelRect[1]), float(row.labelRect[2]), float(row.labelRect[3]))
			var label_direction := label_rect.get_center() - icon_rect.get_center()
			var expansion := icon_rect.size * ((icon_factor - 1.0) * 0.5)
			if absf(label_direction.x) >= absf(label_direction.y):
				label_offset.x = signf(label_direction.x) * expansion.x
			else:
				label_offset.y = signf(label_direction.y) * expansion.y
		var scaled_icon_size := icon_rect.size * icon_factor
		icon_rect = Rect2(icon_rect.get_center() - scaled_icon_size * 0.5, scaled_icon_size)
		icon_obstacles.append(icon_rect.grow(margin))
		icon_label_offsets[id] = label_offset
		if is_primary or (is_city and include_city_labels):
			label_rows.append(row)
	if is_instance_valid(player_mark):
		var zoom := maxf(_world_zoom, 0.01)
		var sprite_scale := PLAYER_PAWN_SCREEN_HEIGHT / zoom / float(PLAYER_PAWN_TEXTURE.get_height())
		var pawn_size := Vector2(PLAYER_PAWN_TEXTURE.get_size()) * sprite_scale
		var pawn_rect := Rect2(player_mark.position + Vector2(-pawn_size.x * 0.5, -pawn_size.y), pawn_size)
		icon_obstacles.append(pawn_rect.grow(16.0 / zoom))

	var result: Dictionary = {}
	var placed_labels: Array[Rect2] = []
	var unresolved_city_labels := false
	var min_city_screen_font_size := INF
	var candidate_directions: Array[Vector2] = []
	for direction_index in range(16):
		var angle := TAU * float(direction_index) / 16.0
		candidate_directions.append(Vector2(cos(angle), sin(angle)))
	for row: Dictionary in label_rows:
		var id := str(row.id)
		var base_rect := Rect2(float(row.labelRect[0]), float(row.labelRect[1]), float(row.labelRect[2]), float(row.labelRect[3]))
		var icon_label_offset: Vector2 = icon_label_offsets.get(id, Vector2.ZERO)
		var base_label_position := base_rect.position + icon_label_offset
		var base_font_size := maxf(1.0, float(row.get("labelSize", 14)))
		var target_screen_size := FAR_PRIMARY_LABEL_SCREEN_SIZE if str(row.get("type", "")) == "sect" else FAR_CITY_LABEL_SCREEN_SIZE
		var target_factor := minf(MAX_FAR_LABEL_COMPENSATION, target_screen_size / (base_font_size * maxf(_world_zoom, 0.01)))
		var chosen_factor := FAR_LABEL_MIN_COMPENSATION
		var chosen_offset := Vector2.ZERO
		var found := false
		var factors := PackedFloat32Array([target_factor, maxf(FAR_LABEL_MIN_COMPENSATION, target_factor - 0.5), maxf(FAR_LABEL_MIN_COMPENSATION, target_factor - 1.0), FAR_LABEL_MIN_COMPENSATION])
		var max_shift_world := FAR_LABEL_MAX_SHIFT_SCREEN / maxf(_world_zoom, 0.01)
		for factor_value in factors:
			var factor := float(factor_value)
			if factor > target_factor or factor >= chosen_factor and found: continue
			var scaled_size := base_rect.size * factor
			var collision_margin := margin
			for radius_screen in range(0, int(FAR_LABEL_MAX_SHIFT_SCREEN) + 1, 8):
				var radius_world := float(radius_screen) / maxf(_world_zoom, 0.01)
				for direction_index in range(candidate_directions.size()):
					var offset := candidate_directions[direction_index] * radius_world
					if offset.length() > max_shift_world: continue
					var candidate := Rect2(base_label_position + offset, scaled_size)
					if not safe_world_rect.encloses(candidate): continue
					var blocked := false
					for obstacle in icon_obstacles:
						if candidate.intersects(obstacle):
							blocked = true
							break
					if blocked: continue
					for placed in placed_labels:
						if candidate.grow(collision_margin).intersects(placed):
							blocked = true
							break
					if blocked: continue
					chosen_factor = factor
					chosen_offset = offset
					found = true
					break
				if found: break
			if found: break
		if not found:
			chosen_factor = FAR_LABEL_MIN_COMPENSATION
			chosen_offset = Vector2.ZERO
			if str(row.get("type", "")) == "city": unresolved_city_labels = true
		var final_offset := icon_label_offset + chosen_offset
		var placed_rect := Rect2(base_rect.position + final_offset, base_rect.size * chosen_factor)
		placed_labels.append(placed_rect.grow(margin))
		var screen_font_size := base_font_size * chosen_factor * _world_zoom
		if str(row.get("type", "")) == "city": min_city_screen_font_size = minf(min_city_screen_font_size, screen_font_size)
		result[id] = {"factor": chosen_factor, "offset": final_offset, "screen_font_size": screen_font_size}
	result["_city_labels_unresolved"] = unresolved_city_labels
	result["_min_city_screen_font_size"] = min_city_screen_font_size
	return result

func _apply_control_compensation(control: Node, factor: float, expand_from_top_left := false, position_offset := Vector2.ZERO) -> void:
	var base_position: Vector2 = control.get_meta("zoom_base_position", Vector2(control.get("position")))
	var base_size: Vector2 = control.get_meta("zoom_base_size", Vector2.ZERO)
	var base_scale: Vector2 = control.get_meta("zoom_base_scale", Vector2(control.get("scale")))
	control.set("scale", base_scale * factor)
	var adjusted_position := base_position if expand_from_top_left else base_position + (base_size - base_size * factor) * 0.5
	adjusted_position += position_offset
	control.set("position", adjusted_position)

func _refresh_zoom_visibility() -> void:
	if data.is_empty() or not is_instance_valid(ink):
		return
	_visible_place_ids.clear()
	for node: Dictionary in data.get("nodes", []):
		if _place_visible_at_level(node, _zoom_level):
			_visible_place_ids[str(node.id)] = true
	for node_id: String in place_views:
		var marker: Control = place_views[node_id]
		marker.visible = _visible_place_ids.has(node_id)
	_visible_transit_ids.clear()
	var transit_points_visible := _zoom_level >= 2
	for node_id: String in transit_views:
		var marker: Button = transit_views[node_id]
		marker.visible = transit_points_visible
		if transit_points_visible:
			_visible_transit_ids[node_id] = true
	ink.paths_visible = transit_points_visible
	ink.marks_visible = transit_points_visible
	ink.queue_redraw()
	_apply_marker_compensation()
	if is_instance_valid(population_badges):
		population_badges.visible_node_ids = _visible_place_ids.duplicate()
		population_badges.visible_node_ids.merge(_visible_transit_ids, true)
		population_badges.queue_redraw()
	if not selected_node.is_empty() and selected_node != travel.current_node and not _selected_node_is_visible():
		selected_node = ""
		if is_instance_valid(title_label): title_label.text = "选择地点"
		if is_instance_valid(detail_label): detail_label.text = ""
		_refresh_actions()

func _place_visible_at_level(node: Dictionary, level: int) -> bool:
	var kind := str(node.get("type", "wild"))
	if kind == "sect":
		var sect_level := int(node.get("level", 2))
		return sect_level == 1 or (sect_level == 2 and level >= 1)
	if kind == "city":
		return level >= 0
	if kind in ["town", "market", "village", "dock"]:
		return level >= 1
	if kind == "wild":
		return level >= 2
	return level >= 2

func _selected_node_is_visible() -> bool:
	return _visible_place_ids.has(selected_node) or _visible_transit_ids.has(selected_node)

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
		button.set_meta("formal_map_node_id", str(node.id))
		button.flat = true
		button.focus_mode = Control.FOCUS_NONE
		button.position = travel.graph.positions[node.id]-Vector2(8,8)
		button.size = Vector2(16,16)
		button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		for style in ["normal","hover","pressed","focus"]:
			button.add_theme_stylebox_override(style,StyleBoxEmpty.new())
		button.pressed.connect(_select_transit.bind(str(node.id)))
		places.add_child(button)
		transit_views[str(node.id)] = button

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
	population_badges.visible_node_ids = _visible_place_ids.duplicate()
	population_badges.visible_node_ids.merge(_visible_transit_ids, true)
	population_badges.queue_redraw()

func _select_transit(id: String) -> void:
	selected_node = id
	title_label.text = "行旅途经点"
	var preview: Dictionary = travel.preview(id,dun_speed)
	detail_label.text = "当前位置" if id == travel.current_node else "路程 %.1f 公里 · 预计 %.1f 小时" % [preview.get("kilometres",0.0),preview.get("hours",0.0)]
	_refresh_actions()

func _can_record_formal_map_click(_node_id: String) -> bool:
	return not travel_busy and not encounter_blocked

func _on_formal_map_double_click(node_id: String) -> void:
	if travel_busy or encounter_blocked:
		return
	if not place_views.has(node_id) and not transit_views.has(node_id):
		return
	selected_node = node_id
	_refresh_actions()
	if node_id == travel.current_node:
		# A current transition point is only a route waypoint, never an enterable place.
		if not travel.graph.runtime_id_for_node(node_id).is_empty():
			_enter_current()
		return
	_travel_selected()

func _update_current_location() -> void:
	if not is_instance_valid(current_location_label): return
	var location_name := "行旅途经点"
	for node: Dictionary in data.get("nodes", []):
		if str(node.id) == travel.current_node:
			location_name = str(node.name)
			break
	current_location_label.text = "当前位置：" + location_name

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
	_clear_formal_map_click_sequence()
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
		var halo_tween := create_tween()
		for i in range(1,points.size()):
			var segment_duration := duration*points[i-1].distance_to(points[i])/maxf(length,0.001)
			tween.tween_property(player_mark,"position",points[i],segment_duration)
			halo_tween.tween_property(player_halo,"position",points[i],segment_duration)
		await tween.finished
		await halo_tween.finished
		if not travel.commit_step(step.to): break
		_update_current_location()
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
	if value:
		_clear_formal_map_click_sequence()
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
		if is_instance_valid(player_halo): player_halo.position = player_mark.position
		focus_player_node()
	_update_current_location()
	var found := false
	for node: Dictionary in data.get("nodes", []):
		if str(node.id) == travel.current_node:
			_select_place(node)
			found = true
			break
	if not found: _select_transit(travel.current_node)
	_refresh_actions()
	return true

func focus_player_node() -> void:
	if not is_instance_valid(player_mark): return
	var focus_point := player_mark.position
	var viewport_world_size := get_viewport_world_size()
	if _zoom_level == 0 and viewport_world_size.x >= WORLD_SIZE and viewport_world_size.y >= WORLD_SIZE:
		focus_point = Vector2.ONE * WORLD_SIZE * 0.5
	focus_world_point(focus_point)

func _return_to_menu() -> void:
	return_requested.emit()
