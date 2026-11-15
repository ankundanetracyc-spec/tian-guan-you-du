extends Control

signal subscene_selected(subscene_id: String)
signal district_selected
signal close_requested
signal location_action_requested(action_id: String)

const NAMEPLATE = preload("res://scripts/ui/WorldLocationNameplateStyle.gd")
const ACTION_TAG = preload("res://tscn/panel/SceneActionButton.tscn")
const ACTION_ROPE = preload("res://scripts/ui/SceneActionRope.gd")
const ACTION_GUIDE = preload("res://scripts/ui/SceneActionGuide.gd")
const CONTENT_PATH := "res://resources/world/bai_chuan/baishi_city_content_v1.json"
var _content: Dictionary = {}
var _available: Array[String] = []
var _visible_actions: Array[String] = []
var _backgrounds: Dictionary = {}
var _current_id := ""
var _district := ""
var _overview_places: Control
var _district_layer: Control
var _facility_layer: Control
var _facility_rope: Control
var _heading: Label
var _note: Label
var _back: Button
var _guidance: Control
var _guided_action := ""
var _guidance_text := ""

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(CONTENT_PATH))
	if not parsed is Dictionary:
		push_error("白石城场景配置无法读取")
		return
	_content = parsed
	_overview_places = _full_layer("OverviewPlaces")
	for place_tag: Dictionary in _content["overview"].get("place_tags", []):
		var place_id := str(place_tag["id"])
		if not _content["places"].has(place_id):
			continue
		var tag := _location_tag(str(_content["places"][place_id]["title"]))
		tag.name = "Place_" + place_id
		tag.set_meta("subscene_id", place_id)
		_overview_places.add_child(tag)
		_place_overview_tag(tag, _point(place_tag["at"]))
		tag.pressed.connect(open_space.bind(place_id))
	_district_layer = _full_layer("DistrictLabels")
	_facility_layer = _full_layer("FacilityLabels")
	_facility_rope = Control.new()
	_facility_rope.name = "ActionRope"
	_facility_rope.set_script(ACTION_ROPE)
	_facility_layer.add_child(_facility_rope)
	_facility_rope.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for district_id: String in _content["districts"]:
		var info: Dictionary = _content["districts"][district_id]
		var tag := _paper_tag(str(info["title"]))
		tag.name = "District_" + district_id
		_district_layer.add_child(tag)
		_place(tag, _point(info["at"]))
		tag.pressed.connect(open_district.bind(district_id))
	_heading = Label.new()
	_heading.name = "LocationTitle"
	_heading.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_heading.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_heading.add_theme_font_size_override("font_size", 28)
	_ink_label(_heading, 1)
	add_child(_heading)
	_heading.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	_heading.offset_left = -310
	_heading.offset_right = 310
	_heading.offset_top = 26
	_heading.offset_bottom = 74
	_note = Label.new()
	_note.name = "LocationDescription"
	_note.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_note.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_note.add_theme_font_size_override("font_size", 16)
	_ink_label(_note, 1)
	add_child(_note)
	_note.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	_guidance = Control.new()
	_guidance.set_script(ACTION_GUIDE)
	_guidance.name = "LectureGuidance"
	add_child(_guidance)
	_guidance.hide()
	_back = Button.new()
	_back.name = "ReturnButton"
	_back.add_theme_font_size_override("font_size", 18)
	_style_button(_back)
	add_child(_back)
	_back.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_back.offset_left = -242
	_back.offset_right = -40
	_back.offset_top = 28
	_back.offset_bottom = 76
	_back.pressed.connect(func() -> void: close_requested.emit())
	resized.connect(_layout_tags)
	open_overview()

func _full_layer(layer_name: String) -> Control:
	var layer := Control.new()
	layer.name = layer_name
	layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(layer)
	layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	return layer

func configure_spaces(ids: Array[String]) -> void:
	_available = ids

func configure_actions(ids: Array[String]) -> void:
	if _visible_actions == ids:
		return
	_visible_actions = ids
	if not _current_id.is_empty():
		_render_facilities()

func get_current_space_id() -> String:
	return _current_id

func configure_guidance(action_id: String, text: String) -> void:
	_guided_action = action_id
	_guidance_text = text
	_layout_guidance()

func open_overview() -> void:
	_current_id = ""
	_district = ""
	_set_background(str(_content["overview"]["background"]))
	_overview_places.show()
	_district_layer.hide()
	_facility_layer.hide()
	_guidance.hide()
	_heading.text = "白石城"
	_note.text = str(_content["overview"]["description"])
	_back.text = "返回百川地图"
	_layout_tags()
	show()
	subscene_selected.emit("")

func open_district(district_id: String) -> void:
	if not _content["districts"].has(district_id):
		return
	var entry: Dictionary = _content["districts"][district_id]
	var space_id := str(entry["space_id"])
	if not _available.is_empty() and not _available.has(space_id):
		return
	_district = district_id
	_current_id = space_id
	_overview_places.hide()
	_district_layer.hide()
	_set_background(str(entry["background"]))
	_heading.text = "白石城 · " + str(entry["title"])
	_note.text = str(entry["description"])
	_back.text = "返回白石城"
	_render_facilities()
	show()
	district_selected.emit()
	subscene_selected.emit(space_id)

func open_space(space_id: String) -> void:
	for district_id: String in _content["districts"]:
		if str(_content["districts"][district_id]["space_id"]) == space_id:
			open_district(district_id)
			return
	if not _content["places"].has(space_id):
		return
	if not _available.is_empty() and not _available.has(space_id):
		return
	var entry: Dictionary = _content["places"][space_id]
	_current_id = space_id
	_district = _district_for(space_id)
	_overview_places.hide()
	_district_layer.hide()
	_set_background(str(entry["background"]))
	_heading.text = "白石城 · " + str(entry["title"])
	_note.text = str(entry["description"])
	_back.text = "返回白石城"
	_render_facilities()
	show()
	subscene_selected.emit(space_id)

func handle_back() -> bool:
	if _current_id.is_empty():
		return false
	open_overview()
	return true

func show_courtyard_controls() -> void:
	open_space("lecture")

func _entry() -> Dictionary:
	if _content["places"].has(_current_id):
		return _content["places"][_current_id]
	if not _district.is_empty():
		return _content["districts"][_district]
	return {}

func _render_facilities() -> void:
	if not is_instance_valid(_facility_layer):
		return
	for child in _facility_layer.get_children():
		if child == _facility_rope:
			continue
		_facility_layer.remove_child(child)
		child.queue_free()
	if _current_id.is_empty():
		_facility_layer.hide()
		return
	var entry := _entry()
	for entrance: Dictionary in entry.get("entrances", []):
		var id := str(entrance["id"])
		if not _available.is_empty() and not _available.has(id):
			continue
		var tag := ACTION_TAG.instantiate() as Button
		tag.text = str(_content["places"][id]["title"])
		tag.name = "Facility_" + id
		tag.set_meta("subscene_id", id)
		_facility_layer.add_child(tag)
		tag.pressed.connect(open_space.bind(id))
	for action: Dictionary in entry.get("actions", []):
		var id := str(action["id"])
		if not _visible_actions.has(id):
			continue
		var tag := ACTION_TAG.instantiate() as Button
		tag.text = str(action["label"])
		tag.name = "Action_" + id.replace(".", "_")
		tag.set_meta("action_id", id)
		_facility_layer.add_child(tag)
		tag.pressed.connect(func() -> void: location_action_requested.emit(id))
	_facility_layer.show()
	_facility_rope.call("_refresh_tags")
	_layout_tags()

func _district_for(space_id: String) -> String:
	var parent := space_id
	for iteration in range(4):
		if _content["districts"].has(parent):
			return parent
		if not _content["places"].has(parent):
			break
		parent = str(_content["places"][parent]["parent"])
	return ""

func _set_background(path: String) -> void:
	if not _backgrounds.has(path):
		_backgrounds[path] = load(path)
	$Background.texture = _backgrounds[path]
	$Background.show()

func _point(values: Array) -> Vector2:
	return Vector2(float(values[0]), float(values[1]))

func _paper_tag(title: String) -> Button:
	var tag := Button.new()
	tag.text = "\n".join(title.split("", false))
	tag.add_theme_font_size_override("font_size", 18)
	tag.add_theme_constant_override("outline_size", 0)
	_style_button(tag)
	return tag

func _location_tag(title: String) -> Button:
	var tag := Button.new()
	tag.text = title
	tag.add_theme_font_size_override("font_size", 19)
	tag.custom_minimum_size = Vector2(164, 52)
	_style_button(tag)
	return tag

func _style_button(button: Button) -> void:
	button.add_theme_stylebox_override("normal", NAMEPLATE.create(false))
	button.add_theme_stylebox_override("hover", NAMEPLATE.create(true))
	button.add_theme_stylebox_override("pressed", NAMEPLATE.create(true))
	for state: String in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		button.add_theme_color_override(state, Color("292217"))

func _ink_label(label: Label, outline: int) -> void:
	label.add_theme_color_override("font_color", Color("292217"))
	label.add_theme_color_override("font_outline_color", Color("e9dec5"))
	label.add_theme_constant_override("outline_size", outline)

func _place(tag: Control, at: Vector2) -> void:
	tag.set_meta("scene_anchor", at)
	var safe_margin := minf(400.0, size.x * 0.32)
	var x := clampf(at.x * size.x, safe_margin + 30.0, size.x - safe_margin - 30.0)
	tag.anchor_left = x / maxf(1.0, size.x)
	tag.anchor_right = tag.anchor_left
	var y := clampf(at.y * size.y, 200.0, maxf(200.0, size.y - 224.0))
	tag.anchor_top = y / maxf(1.0, size.y)
	tag.anchor_bottom = tag.anchor_top
	tag.offset_left = -30.0
	tag.offset_right = 30.0
	tag.offset_top = -74.0
	tag.offset_bottom = 74.0

func _place_overview_tag(tag: Control, at: Vector2) -> void:
	tag.set_meta("scene_anchor", at)
	var x := clampf(at.x * size.x, 102.0, size.x - 102.0)
	var y := clampf(at.y * size.y, 184.0, maxf(184.0, size.y - 48.0))
	tag.anchor_left = x / maxf(1.0, size.x)
	tag.anchor_right = tag.anchor_left
	tag.anchor_top = y / maxf(1.0, size.y)
	tag.anchor_bottom = tag.anchor_top
	tag.offset_left = -82.0
	tag.offset_right = 82.0
	tag.offset_top = -26.0
	tag.offset_bottom = 26.0

func _layout_tags() -> void:
	if is_instance_valid(_overview_places):
		for tag: Control in _overview_places.get_children():
			_place_overview_tag(tag, tag.get_meta("scene_anchor"))
	for layer: Control in [_district_layer]:
		if not is_instance_valid(layer):
			continue
		for tag: Control in layer.get_children():
			_place(tag, tag.get_meta("scene_anchor"))
	if is_instance_valid(_facility_rope):
		_facility_rope.call_deferred("_layout_tags")
	call_deferred("_layout_guidance")
	if is_instance_valid(_note):
		var width := clampf(size.x - 800.0, 280.0, 720.0)
		_note.offset_left = -width * 0.5
		_note.offset_right = width * 0.5
		_note.offset_top = 103
		_note.offset_bottom = 150

func _layout_guidance() -> void:
	if not is_instance_valid(_guidance) or not is_inside_tree():
		return
	_guidance.hide()
	if _guidance_text.is_empty():
		return
	var target: Control = null
	if _current_id.is_empty() and _guided_action.begins_with("place:"):
		var place_id := _guided_action.trim_prefix("place:")
		for tag in _overview_places.get_children():
			if tag is Button and str(tag.get_meta("subscene_id", "")) == place_id:
				target = tag
				break
	elif _current_id == "lecture":
		for tag in _facility_layer.get_children():
			if tag is Button and str(tag.get_meta("action_id", "")) == _guided_action:
				target = tag
				break
	if target != null:
		_guidance.call("set_instruction", _guidance_text)
		_guidance.size = Vector2(410, 84)
		_guidance.position = Vector2(target.position.x + target.size.x * 0.5 - 205, maxf(155.0, target.position.y - 128))
		_guidance.show()
