extends Area2D

signal node_clicked(node_id: String)
signal node_double_clicked(node_id: String)
signal node_hovered(node_id: String)
signal node_unhovered(node_id: String)

var node_data: Object
var map_camera: Camera2D
var map_scale: float = 1.0
@export var scene_node_id: String = ""
var node_id: String = ""
var selected: bool = false
var current: bool = false
var target: bool = false
var unlocked: bool = true
var population: int = 0
var _last_click_ms: int = -10000
var _shape_initialized: bool = false

# Every map location uses the same plain ink circle. The hierarchy is
# communicated by screen-space size in WorldMapViewport; authored place art is
# intentionally not shown in the travel map layer.
const CIRCLE_MARKER_TEXTURE_PATH := "res://textures/ui/world/world_marker_circle_ink.svg"
const LOCATION_MARKER_SCREEN_WIDTH := 36.0
const SECOND_LEVEL_MARKER_SCREEN_WIDTH := 56.0
const WILDERNESS_MARKER_SCREEN_WIDTH := 26.0
const SECOND_LEVEL_LOCATION_IDS := {
	"white_stone_town": true,
	"east_woods": true,
	"fulan_temple": true,
	"lanpu_ferry": true,
	"changfeng_escort": true,
	"chunshui_prefecture": true,
	"guanshan_prefecture": true,
	"chilu_hall": true,
	"chishui_city": true,
	"youquan_cult": true,
	"yexing_society": true,
	"huigu_temple": true
}
# The player marker is a small map pin, not a full character portrait. Its
# local scale is deliberately fixed; _update_visuals() compensates only for
# map zoom so the pin keeps one readable screen size at every camera distance.
const PLAYER_MARKER_SCALE := 0.24
const TAG_FONT_SIZE := 17
@onready var visual: Node2D = $Visual
@onready var marker: Sprite2D = $Visual/Marker
@onready var ring: Sprite2D = $Visual/Ring
@onready var title: Label = $Visual/Title
@onready var population_badge: Label = $Visual/Population
@onready var player_marker: Sprite2D = $Visual/PlayerMarker
@onready var hit_shape: CollisionShape2D = $CollisionShape2D

func _ready() -> void:
	input_pickable = true
	population_badge.add_theme_color_override("font_color", Color("ffffff"))
	population_badge.add_theme_color_override("font_outline_color", Color(0.10, 0.08, 0.06, 0.96))
	population_badge.add_theme_constant_override("outline_size", 3)
	population_badge.add_theme_stylebox_override("normal", _badge_style())
	population_badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	title.add_theme_stylebox_override("normal", _tag_style())
	title.add_theme_color_override("font_color", Color("40392e"))
	title.add_theme_font_size_override("font_size", TAG_FONT_SIZE)
	title.add_theme_constant_override("line_spacing", -2)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	title.mouse_filter = Control.MOUSE_FILTER_IGNORE
	monitoring = true
	monitorable = true
	collision_layer = 1
	collision_mask = 1
	mouse_entered.connect(_on_mouse_entered)
	mouse_exited.connect(_on_mouse_exited)

func configure(data: Object, camera: Camera2D, base_scale: float) -> void:
	node_data = data
	map_camera = camera
	map_scale = maxf(base_scale, 0.001)
	node_id = str(data.node_id)
	unlocked = not bool(data.requires_unlock) or bool(data.unlocked)
	_set_tag_text(str(data.node_name))
	marker.texture = load(CIRCLE_MARKER_TEXTURE_PATH) as Texture2D
	marker.scale = Vector2.ONE * (_marker_screen_width() / 64.0)
	marker.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	ring.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	player_marker.texture = load("res://textures/ui/world/player_location_marker_mini_ink_v2.png")
	player_marker.scale = Vector2.ONE * PLAYER_MARKER_SCALE
	player_marker.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	_update_visuals()

func set_state(is_selected: bool, is_current: bool, is_target: bool, count: int) -> void:
	selected = is_selected
	current = is_current
	target = is_target
	population = maxi(0, count)
	_update_visuals()

func set_population(count: int) -> void:
	population = maxi(0, count)
	_update_visuals()

func set_player_present(present: bool) -> void:
	player_marker.visible = present

func _process(_delta: float) -> void:
	_update_visuals()

func _update_visuals() -> void:
	if map_camera == null:
		return
	var zoom := maxf(map_camera.zoom.x, 0.01)
	var inverse := 1.0 / (map_scale * zoom)
	visual.scale = Vector2.ONE * inverse
	# The 180-degree presentation is baked into the map textures and data
	# positions, so marker contents stay in their normal upright orientation.
	visual.rotation = 0.0
	# Keep the hit target close to the visible marker. The editor preview hides
	# the CollisionShape2D helper, so an oversized shape no longer reads as a
	# second map element.
	var desired_radius := _marker_screen_width() * 0.5
	if hit_shape.shape is CircleShape2D:
		if not _shape_initialized:
			hit_shape.shape = hit_shape.shape.duplicate()
			_shape_initialized = true
		var shape := hit_shape.shape as CircleShape2D
		shape.radius = desired_radius / (map_scale * zoom)
	ring.visible = selected or current or target
	ring.modulate = Color("f1c967") if current else Color("c99852") if target else Color("ded0a6")
	ring.scale = Vector2.ONE * (1.18 if current else 1.05 if target else 0.98)
	marker.modulate = Color(1, 1, 1, 1) if unlocked else Color(0.46, 0.46, 0.42, 0.55)
	# 地名是地图的基本信息，不随缩放和选中状态在竖牌与无标签之间切换。
	title.visible = unlocked
	title.add_theme_color_override("font_color", Color("1d1912"))
	# 每个已解锁地点都保留人数角标；空节点或无人地点显示 0，避免
	# 玩家无法区分“无人”与“没有人数标记”。
	population_badge.visible = unlocked
	population_badge.text = str(population)
	player_marker.visible = player_marker.visible and unlocked

func _set_tag_text(node_name: String) -> void:
	var value := node_name.strip_edges()
	title.text = value
	var tag_width := maxf(94.0, float(value.length()) * 21.0 + 22.0)
	title.position = Vector2(-tag_width * 0.5, 29.0)
	title.size = Vector2(tag_width, 34.0)

func _tag_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.91, 0.88, 0.79, 0.94)
	style.border_color = Color(0.38, 0.31, 0.2, 0.58)
	style.set_border_width_all(1)
	style.set_corner_radius_all(3)
	style.content_margin_left = 4.0
	style.content_margin_right = 4.0
	style.content_margin_top = 4.0
	style.content_margin_bottom = 4.0
	return style

func _badge_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.58, 0.16, 0.12, 0.96)
	style.border_color = Color(0.92, 0.76, 0.48, 0.95)
	style.set_border_width_all(1)
	style.set_corner_radius_all(14)
	return style

func get_screen_radius() -> float:
	return _marker_screen_width() * 0.5

func _marker_screen_width() -> float:
	var faction_id := str(node_data.faction_id) if node_data != null else ""
	var is_second_level := not faction_id.is_empty() or SECOND_LEVEL_LOCATION_IDS.has(node_id)
	if is_second_level:
		return SECOND_LEVEL_MARKER_SCREEN_WIDTH
	if node_data != null and int(node_data.node_type) == 7:
		return WILDERNESS_MARKER_SCREEN_WIDTH
	return LOCATION_MARKER_SCREEN_WIDTH

func _on_mouse_entered() -> void:
	node_hovered.emit(node_id)

func _on_mouse_exited() -> void:
	node_unhovered.emit(node_id)

func _input_event(_viewport: Node, event: InputEvent, _shape_idx: int) -> void:
	if event is not InputEventMouseButton:
		return
	var mouse := event as InputEventMouseButton
	if mouse.button_index != MOUSE_BUTTON_LEFT or not mouse.pressed:
		return
	get_viewport().set_input_as_handled()
	var now := Time.get_ticks_msec()
	var is_double := mouse.double_click or (now - _last_click_ms <= 330)
	_last_click_ms = now
	if is_double:
		node_double_clicked.emit(node_id)
	else:
		node_clicked.emit(node_id)
