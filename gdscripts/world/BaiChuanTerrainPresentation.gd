extends RefCounted
## Display-only grading of the unchanged native map tiles. No gameplay state.

const BASE := "res://textures/world/bai_chuan/scenery_ink_20261104/presentation/"
const CONFIG_PATH := BASE + "presentation_own_landscape.json"
const TERRAIN_PATH := BASE + "terrain_presentation_data.png"
const REGIONAL_PATH := BASE + "regional_presentation_data.png"
const WATER_GEOMETRY_PATH := BASE + "water_geometry_lock_data.png"
const TERRAIN_SHADER = preload("res://shaders/world/BaiChuanInkTerrain.gdshader")
static var _terrain: ImageTexture
static var _regional: ImageTexture
static var _water_geometry: ImageTexture
static var _parameters: Dictionary = {}
static var _loaded := false


static func configure_tile(tile: Sprite2D, tile_origin: Vector2, tile_size: float, map_size: float) -> void:
	if not is_instance_valid(tile) or tile_size <= 0.0 or map_size <= 0.0:
		return
	_load_shared_data()
	if _terrain == null or _regional == null:
		return
	var material := ShaderMaterial.new()
	material.shader = TERRAIN_SHADER
	material.set_shader_parameter("terrain_data", _terrain)
	material.set_shader_parameter("regional_data", _regional)
	material.set_shader_parameter("tile_origin", tile_origin)
	material.set_shader_parameter("tile_size", tile_size)
	material.set_shader_parameter("map_size", map_size)
	for key: String in _parameters:
		var value: Variant = _parameters[key]
		if value is String and str(value).begins_with("#"):
			value = Color(str(value))
		material.set_shader_parameter(key, value)
	if _water_geometry != null:
		material.set_shader_parameter("water_geometry_data", _water_geometry)
	else:
		material.set_shader_parameter("water_geometry_lock", 0.0)
	tile.material = material
	# The regional material owns tile colour; retain no legacy per-tile wash.
	tile.modulate = Color.WHITE
	tile.self_modulate = Color.WHITE
	tile.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	tile.set_meta("bai_chuan_terrain_presentation", true)
	tile.set_meta("presentation_world_origin", tile_origin)


static func _load_shared_data() -> void:
	if _loaded:
		return
	_loaded = true
	# Import processing can alter RGB under alpha because A is also mask data.
	# Read RGBA bytes directly, avoiding alpha-border repair and gamma conversion.
	_terrain = _data_texture(TERRAIN_PATH)
	_regional = _data_texture(REGIONAL_PATH)
	if FileAccess.file_exists(WATER_GEOMETRY_PATH):
		_water_geometry = _data_texture(WATER_GEOMETRY_PATH)
	if FileAccess.file_exists(CONFIG_PATH):
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(CONFIG_PATH))
		if parsed is Dictionary:
			_parameters = parsed.get("shader_parameters", {})


static func _data_texture(path: String) -> ImageTexture:
	var image := Image.load_from_file(path)
	if image == null or image.is_empty():
		push_error("百川地形展示蒙版无法读取：" + path)
		return null
	image.convert(Image.FORMAT_RGBA8)
	return ImageTexture.create_from_image(image)
