extends Control

signal peak_selected(peak_id: String)
signal overview_closed
signal admission_requested
signal life_action_requested(action_id: String, peak_id: String)
signal exchange_purchase_requested(item_path: String, contribution_cost: int)

var _admission_root_name := "无灵根"
var _admission_rank := ""
var _admission_member := false
var _admission_current_rank := ""
var _has_other_affiliation := false

const LOCATION_NAMEPLATE_STYLE = preload("res://scripts/ui/WorldLocationNameplateStyle.gd")
const SCENE_ACTION_BUTTON = preload("res://tscn/panel/SceneActionButton.tscn")
const SCENE_ACTION_ROPE = preload("res://scripts/ui/SceneActionRope.gd")

const PEAK_LABELS := {
	"heguang": "和\n光\n峰",
	"guanfu": "观\n复\n峰",
	"zifu": "紫\n府\n峰",
	"xuanlan": "玄\n览\n峰",
	"tianwen": "天\n问\n峰",
	"tiangong": "天\n工\n峰",
	"zhizhong": "执\n中\n峰",
	"yaopu": "瑶\n圃\n峰"
}

@onready var close_button: Button = $CloseButton
@onready var peak_layer: Control = $PeakLayer
@onready var peak_interior: Control = $PeakInterior
@onready var overview_title: Label = $Title
@onready var overview_subtitle: Label = $Subtitle
@onready var overview_hint: Label = $Hint
var _current_peak_id := ""
var _gate_waiting := false
var _gate_actions: Control

func _ready() -> void:
	# 全屏总览本身不抢鼠标；只有峰位标签与场景内交互接收点击。
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_to_group("yuxu_sect_overview")
	for child in $PeakLayer.get_children():
		if child is Button:
			var peak_id := str(child.get_meta("peak_id", ""))
			child.pressed.connect(_on_peak_pressed.bind(peak_id))
			# 峰名直接复用大世界地点纸签，竖排文字保留深墨色。
			child.offset_left = -30.0
			child.offset_top = -68.0
			child.offset_right = 30.0
			child.offset_bottom = 68.0
			child.add_theme_stylebox_override("normal", _peak_label_style(false))
			child.add_theme_stylebox_override("hover", _peak_label_style(true))
			child.add_theme_stylebox_override("pressed", _peak_label_style(true))
			var icon := child.get_node_or_null("Icon") as Control
			if icon != null:
				icon.hide()
			var title := child.get_node_or_null("Title") as Label
			if title != null:
				title.position = Vector2(0.0, 7.0)
				title.size = Vector2(60.0, 122.0)
				title.text = str(PEAK_LABELS.get(peak_id, title.text))
				title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
				title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
				title.add_theme_font_size_override("font_size", 18)
				title.add_theme_color_override("font_color", Color("292217"))
				title.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0))
				title.add_theme_constant_override("outline_size", 0)
			var subtitle := child.get_node_or_null("Subtitle") as Control
			if subtitle != null:
				subtitle.hide()
	_refresh_peak_population_badges()
	close_button.pressed.connect(_on_close_requested)
	peak_interior.connect("back_requested", _on_peak_interior_back)
	peak_interior.connect("admission_requested", func() -> void: admission_requested.emit())
	peak_interior.connect("life_action_requested", func(action_id: String, peak_id: String) -> void: life_action_requested.emit(action_id, peak_id))
	peak_interior.connect("exchange_purchase_requested", func(item_path: String, contribution_cost: int) -> void: exchange_purchase_requested.emit(item_path, contribution_cost))
	_gate_actions = Control.new()
	_gate_actions.name = "GateActions"
	_gate_actions.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_gate_actions)
	_gate_actions.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var rope := Control.new()
	rope.name = "ActionRope"
	rope.set_script(SCENE_ACTION_ROPE)
	_gate_actions.add_child(rope)
	rope.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var welcome := SCENE_ACTION_BUTTON.instantiate() as Button
	welcome.name = "GateWelcome"
	welcome.text = "山门迎真"
	welcome.set_meta("action_id", "yuxu.welcome")
	_gate_actions.add_child(welcome)
	welcome.pressed.connect(func() -> void: admission_requested.emit())
	_gate_actions.hide()
	peak_interior.hide()

func open_overview() -> void:
	_gate_waiting = false
	_gate_actions.hide()
	_close_peak_interior()
	_refresh_peak_population_badges()
	_show_roster("")
	var parent_layer := get_parent()
	var world_map := parent_layer.get_node_or_null("NodeWorldMap")
	if world_map != null:
		world_map.hide()
	show()
	move_to_front()

func open_gate_welcome() -> void:
	open_overview()
	_gate_waiting = true
	peak_layer.hide()
	_hide_roster()
	_gate_actions.show()

func set_admission_context(root_name: String, rank: String, is_member: bool, current_rank: String, has_other_affiliation: bool) -> void:
	_admission_root_name = root_name if not root_name.is_empty() else "无灵根"
	_admission_rank = rank
	_admission_member = is_member
	_admission_current_rank = current_rank
	_has_other_affiliation = has_other_affiliation
	peak_interior.call("set_admission_context", _admission_root_name, _admission_rank, _admission_member, _admission_current_rank, _has_other_affiliation)

func refresh_admission_detail() -> void:
	if _current_peak_id == "guanfu":
		peak_interior.call("refresh_admission_detail")

func set_life_context(is_member: bool, current_rank: String, monthly_allowance: int, unclaimed_allowance: int) -> void:
	peak_interior.call("set_life_context", is_member, current_rank, monthly_allowance, unclaimed_allowance)

func resolve_exchange_purchase(success: bool, current_balance: int, message: String) -> void:
	peak_interior.call("resolve_exchange_purchase", success, current_balance, message)

func handle_back() -> bool:
	if not is_visible_in_tree():
		return false
	if peak_interior.visible:
		return bool(peak_interior.call("handle_back"))
	return false

func close_overview() -> void:
	_gate_waiting = false
	_gate_actions.hide()
	_close_peak_interior()
	var roster := get_tree().get_first_node_in_group("cell_character_list")
	if roster != null and roster.has_method("hide_world_npcs"):
		roster.call("hide_world_npcs")
	hide()
	var world_map := get_parent().get_node_or_null("NodeWorldMap")
	if world_map != null:
		world_map.show()
	overview_closed.emit()

func _on_peak_pressed(peak_id: String) -> void:
	if _gate_waiting or not PEAK_LABELS.has(peak_id):
		return
	_current_peak_id = peak_id
	_refresh_peak_population_badges()
	peak_layer.hide()
	overview_title.hide()
	overview_subtitle.hide()
	overview_hint.hide()
	close_button.hide()
	peak_interior.call("open_peak", peak_id)
	_hide_roster()
	peak_selected.emit(peak_id)

func _show_roster(subscene_id: String) -> void:
	var roster := get_tree().get_first_node_in_group("cell_character_list")
	if roster == null:
		return
	if roster.has_method("show_world_context"):
		roster.call("show_world_context", "yuxu_xianzong_core", subscene_id, "interior:yuxu_xianzong:" + subscene_id)
	elif roster.has_method("show_static_rows"):
		# 兼容尚未重新载入 C# 面板的旧场景；正式运行会走上面的运行时名单。
		roster.call("show_static_rows", [], "interior:yuxu_xianzong:" + subscene_id)

func _on_close_requested() -> void:
	close_overview()

func _close_peak_interior() -> void:
	peak_interior.hide()
	_current_peak_id = ""
	peak_layer.show()
	overview_title.show()
	overview_subtitle.show()
	overview_hint.show()
	close_button.text = "返回百川地图"

func _on_peak_interior_back() -> void:
	_close_peak_interior()
	close_button.show()
	_show_roster("")

func _on_peak_roster_requested(peak_id: String) -> void:
	_show_roster(peak_id)

func _hide_roster() -> void:
	var roster := get_tree().get_first_node_in_group("cell_character_list")
	if roster != null and roster.has_method("hide_world_npcs"):
		roster.call("hide_world_npcs")

func _peak_label_style(hovered: bool) -> StyleBoxTexture:
	return LOCATION_NAMEPLATE_STYLE.create(hovered)

func _refresh_peak_population_badges() -> void:
	var manager := get_node_or_null("/root/NpcWorldManager")
	for child in peak_layer.get_children():
		if not child is Button:
			continue
		var peak_id := str(child.get_meta("peak_id", ""))
		var count := 0
		if manager != null and manager.has_method("get_subscene_roster") and not peak_id.is_empty():
			var roster: Variant = manager.call("get_subscene_roster", "yuxu_xianzong_core", peak_id)
			if roster is Array:
				count = roster.size()
		var badge := child.get_node_or_null("Population") as Label
		if badge == null:
			badge = Label.new()
			badge.name = "Population"
			badge.position = Vector2(39.0, -10.0)
			badge.size = Vector2(26.0, 26.0)
			badge.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			badge.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
			badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
			badge.add_theme_font_size_override("font_size", 13)
			badge.add_theme_color_override("font_color", Color("29231d"))
			badge.add_theme_constant_override("outline_size", 0)
			var badge_style := StyleBoxFlat.new()
			badge_style.bg_color = Color("f4dfb1")
			badge_style.border_color = Color("5d4d39")
			badge_style.set_border_width_all(1)
			badge.add_theme_stylebox_override("normal", badge_style)
			child.add_child(badge)
		badge.text = str(count)
		badge.show()
