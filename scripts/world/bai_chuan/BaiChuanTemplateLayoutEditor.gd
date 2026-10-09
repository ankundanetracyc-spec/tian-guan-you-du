@tool
extends Node2D

## Editor-only authoring. The game only displays the saved Sprite2D children.
@export_category("地图模板生成")
@export var layout_seed: int = 20261009
@export_range(0.3, 2.0, 0.05) var layout_density: float = 1.0
## Leave empty to locate the local Python installation automatically.
@export_file("*.exe") var python_executable: String = ""
@export_tool_button("按种子生成地物", "Reload") var generate_button: Callable = _request_generation

var _generating := false

func _request_generation() -> void:
	if not Engine.is_editor_hint() or _generating:
		return
	_generate_layout.call_deferred()

func _generate_layout() -> void:
	if not Engine.is_editor_hint():
		return
	var scene_root: Node = get_tree().edited_scene_root
	if scene_root == null or scene_root.scene_file_path != "res://tscn/world/bai_chuan/dual_grid_v1/BaiChuanDualGridTerrainPrototype_v1.tscn":
		push_error("请先打开百川地图场景本身，再生成地物。")
		return
	_generating = true
	var executable := python_executable
	if executable.is_empty():
		var local_python := OS.get_environment("LOCALAPPDATA").path_join("Programs/Python/Python311/python.exe")
		executable = local_python if FileAccess.file_exists(local_python) else ("python" if OS.get_name() == "Windows" else "python3")
	var output_path := ProjectSettings.globalize_path("user://bai_chuan_editor_layout.json")
	var generator := ProjectSettings.globalize_path("res://tools/world_map/generate_bai_chuan_layout.py")
	var output: Array = []
	var exit_code := OS.execute(executable, PackedStringArray([generator, "--seed", str(layout_seed), "--density", str(layout_density), "--output", output_path]), output, true, false)
	if exit_code != 0:
		push_error("地物生成失败：%s" % str(output))
		_generating = false
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(output_path))
	if not parsed is Dictionary or not parsed.has("records"):
		push_error("没有读到有效的地物布局。")
		_generating = false
		return
	# Load every texture before replacing existing children.
	var textures: Dictionary = {}
	for record: Dictionary in parsed["records"]:
		var asset_path := str(record["asset"])
		if textures.has(asset_path):
			continue
		var texture := load(asset_path) as Texture2D
		if texture == null:
			push_error("无法读取素材：%s" % asset_path)
			_generating = false
			return
		textures[asset_path] = texture
	for child: Node in get_children():
		if child.name == "LandmarkProps":
			continue
		remove_child(child)
		child.queue_free()
	var parents: Dictionary = {"": self}
	for group: String in ["MountainRanges", "ForestPatches", "SmallScenery"]:
		_ensure_parent(group, parents, scene_root)
	var index := 0
	for record: Dictionary in parsed["records"]:
		var parent := _ensure_parent(str(record["parent"]), parents, scene_root)
		var sprite := Sprite2D.new()
		sprite.name = "Scenery_%05d" % index
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		sprite.texture = textures[str(record["asset"])]
		var point: Array = record["position"]
		var shift: Array = record["offset"]
		var footprint: Array = record["footprint"]
		sprite.position = Vector2(float(point[0]), float(point[1]))
		sprite.offset = Vector2(float(shift[0]), float(shift[1]))
		sprite.scale = Vector2.ONE * float(record["scale"])
		sprite.set_meta("asset_family", str(record["family"]))
		sprite.set_meta("cell_footprint", Rect2i(int(footprint[0]), int(footprint[1]), int(footprint[2]), int(footprint[3])))
		sprite.set_meta("generated_from_template", true)
		if record["role"] == "tree":
			sprite.set_meta("map_cell", Vector2i(int(footprint[0]), int(footprint[1])))
			sprite.set_meta("tree_count", 1)
			sprite.set_meta("upright", true)
		elif record["role"] == "mountain":
			sprite.set_meta("footprint_dimensions", Vector2i(int(footprint[2]), int(footprint[3])))
		parent.add_child(sprite)
		sprite.owner = scene_root
		index += 1
	set_meta("layout_seed", layout_seed)
	set_meta("placement_mode", "seeded_templates_baked_into_scene")
	set_meta("water_logic_cells", 0)
	if Engine.has_singleton("EditorInterface"):
		Engine.get_singleton("EditorInterface").call("mark_scene_as_unsaved")
	print("百川地物已生成，种子 %d，共 %d 件；保存场景后生效。" % [layout_seed, index])
	_generating = false

func _ensure_parent(path: String, parents: Dictionary, scene_root: Node) -> Node2D:
	if parents.has(path):
		return parents[path] as Node2D
	var current := ""
	var parent := self as Node2D
	for part: String in path.split("/"):
		var next_path := part if current.is_empty() else current + "/" + part
		if not parents.has(next_path):
			var group := Node2D.new()
			group.name = part
			group.y_sort_enabled = true
			group.z_index = 3 if current.is_empty() else 0
			parent.add_child(group)
			group.owner = scene_root
			parents[next_path] = group
		parent = parents[next_path] as Node2D
		current = next_path
	return parent
