extends RefCounted

## Read-only portrait identity bridge for the separate four-direction pawn demo.
## The bound HumanData is observed, never edited or saved by this adapter.
signal profile_changed(profile: Dictionary)

const ACTIVE_MANIFEST := "res://mods/appearance/ink_three_variants_20261004/appearance_parts.json"
const CANVAS := Vector2(1024, 1536)
const LAYER_ORDER := ["hair_back", "rear_clothes", "face", "ear", "clothes", "nose", "eye", "brow", "mouth", "beard", "makeup", "hair_front"]
const STYLE_FIELDS := {
	"face":"appearance_face_style", "ear":"appearance_ear_style",
	"eye":"appearance_eye_style", "brow":"appearance_brow_style",
	"nose":"appearance_nose_style", "mouth":"appearance_mouth_style",
	"beard":"appearance_beard_style", "makeup":"appearance_makeup_style",
	"clothes":"appearance_clothes_style", "hair_front":"appearance_hair_style",
	"hair_back":"appearance_hair_back_style"
}
const EDITABLE_FIELDS := ["appearance_clothes_style", "appearance_hair_style", "appearance_hair_back_style"]
var _host_ref: WeakRef
var _catalog: Node
var _mod_catalog: Node
var _game_start: Node
var _bound_data: Resource
var _source_kind := "unavailable"
var _source_character: WeakRef
var _initial: Dictionary = {}
var _current: Dictionary = {}
var _manifest: Dictionary = {}
var _manifest_root := ACTIVE_MANIFEST.get_base_dir()

func build(host: Node) -> bool:
	unbind()
	_host_ref = weakref(host)
	_catalog = host.get_node_or_null("/root/PortraitCatalog")
	_mod_catalog = host.get_node_or_null("/root/AppearanceModCatalog")
	_game_start = host.get_node_or_null("/root/GameStartProfile")
	var manifest_path := ACTIVE_MANIFEST
	if not FileAccess.file_exists(manifest_path):
		manifest_path = ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().path_join("../../mods/appearance/ink_three_variants_20261004/appearance_parts.json").simplify_path()
	if FileAccess.file_exists(manifest_path):
		_manifest_root = manifest_path.get_base_dir()
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))
		if parsed is Dictionary: _manifest = parsed
	return _catalog != null or not _manifest.is_empty()

func _host() -> Node:
	return _host_ref.get_ref() as Node if _host_ref != null else null

func _has_property(object: Object, field: String) -> bool:
	for property in object.get_property_list():
		if str(property.name) == field: return true
	return false

func _runtime_character() -> Node:
	var host := _host()
	if host == null or host.get_tree() == null: return null
	var scene := host.get_tree().current_scene
	if scene != null and scene.has_method("get_player_character"):
		var current: Variant = scene.call("get_player_character")
		if current is Node and _has_property(current, "entity_data") and current.get("entity_data") is Resource: return current
	for candidate in host.get_tree().root.get_children():
		if not candidate.has_method("get_player_character"): continue
		var current: Variant = candidate.call("get_player_character")
		if current is Node and _has_property(current, "entity_data") and current.get("entity_data") is Resource: return current
	return null

func _requested_source() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--profile-source="): return argument.trim_prefix("--profile-source=")
	return "current"

func _source_label(kind: String) -> String:
	match kind:
		"runtime_player": return "当前运行人物的真实外观"
		"game_start_preview": return "开局设定的只读立绘预览"
		"private_preview_draft": return "独立演示草稿，不写入人物或存档"
	return "未发现运行人物或开局外观来源"

func _capture(kind: String, resource: Resource = null, character: Node = null) -> Dictionary:
	return {"kind":kind, "label":_source_label(kind), "resource_instance_id":resource.get_instance_id() if resource != null else 0, "resource_path":resource.resource_path if resource != null else "", "character_path":str(character.get_path()) if character != null and character.is_inside_tree() else "", "requested_source":_requested_source(), "writes_source":false}

func _values(data: Resource) -> Dictionary:
	var values := {}
	for property in data.get_property_list():
		var field := str(property.name)
		if field.begins_with("appearance_") or field in ["gender", "faction_name", "current_age", "cultivation_realm", "entity_name", "unique_id", "portrait_clothing_item_path"]:
			values[field] = data.get(field)
	return values

func _bind(data: Resource, kind: String, character: Node = null) -> void:
	if _bound_data != data:
		unbind()
		_bound_data = data
		if _bound_data != null: _bound_data.changed.connect(_on_bound_changed)
	_source_kind = kind
	_source_character = weakref(character) if character != null else null

func unbind() -> void:
	if is_instance_valid(_bound_data) and _bound_data.changed.is_connected(_on_bound_changed):
		_bound_data.changed.disconnect(_on_bound_changed)
	_bound_data = null
	_source_character = null

func _on_bound_changed() -> void:
	var dto := _read_bound()
	_current = dto.duplicate(true)
	profile_changed.emit(dto)

func _read_bound() -> Dictionary:
	if _source_kind == "game_start_preview" and is_instance_valid(_game_start) and _game_start.has_method("create_player_appearance_preview"):
		var preview: Variant = _game_start.call("create_player_appearance_preview")
		if preview is Resource:
			var values := _values(preview)
			values.source = _source_kind
			values.capture_source = _capture(_source_kind, _bound_data)
			values.source_values = _values(_bound_data) if _bound_data != null else _values(preview)
			return resolve_profile(values)
	if _bound_data != null:
		var character: Node = _source_character.get_ref() as Node if _source_character != null else null
		var values := _values(_bound_data)
		if character != null:
			for pair in [["current_age", "current_age"], ["cultivation_realm", "cultivation_realm"]]:
				if _has_property(character, pair[1]): values[pair[0]] = character.get(pair[1])
		values.source = _source_kind
		values.capture_source = _capture(_source_kind, _bound_data, character)
		values.source_values = _values(_bound_data)
		return resolve_profile(values)
	return resolve_profile({"source":"unavailable", "capture_source":_capture("unavailable")})

func resolve_current() -> Dictionary:
	var character := _runtime_character()
	if character != null:
		_bind(character.get("entity_data") as Resource, "runtime_player", character)
	elif is_instance_valid(_game_start) and _game_start.has_method("create_player_appearance_preview"):
		var data: Resource = _game_start.get("player_appearance_data") as Resource if _has_property(_game_start, "player_appearance_data") else null
		if data == null: data = _game_start.call("create_player_appearance_preview") as Resource
		_bind(data, "game_start_preview")
	else:
		unbind()
		_source_kind = "unavailable"
	_current = _read_bound()
	if _initial.is_empty(): _initial = _current.duplicate(true)
	return _current.duplicate(true)

func get_initial_profile() -> Dictionary:
	if _initial.is_empty(): return resolve_current()
	return _initial.duplicate(true)

func get_bound_resource() -> Resource:
	return _bound_data

func _requested(values: Dictionary) -> Dictionary:
	var requested := {}
	for part in STYLE_FIELDS:
		var value := int(values.get(STYLE_FIELDS[part], -1))
		if part == "eye" and value < 0: value = int(values.get("appearance_eyebrow_style", -1))
		if value >= 0: requested[part] = str(value)
	# Same old-save handling as InfoPanel/BattlePortraitView: do not couple
	# an unspecified back hairstyle to the saved front hairstyle.
	if int(values.get("appearance_hair_back_style", -1)) < 0: requested.hair_back = ""
	return requested

func _entries(gender: String, part: String) -> Array:
	if is_instance_valid(_mod_catalog): return _mod_catalog.call("get_parts", gender, part)
	var result := []
	for component in _manifest.get("components", []):
		if str(component.gender) != gender or str(component.part) != part: continue
		for index in range(int(component.get("variant_count", 1))):
			var entry: Dictionary = component.duplicate(true)
			var styles: Array = component.get("variant_style_ids", [])
			var files: Array = component.get("variant_indices", [])
			var style := int(styles[index]) if index < styles.size() else index
			var file := int(files[index]) if index < files.size() else index + int(_manifest.get("index_start", 0))
			var formatted := str(file).pad_zeros(int(_manifest.get("index_padding", 0)))
			entry.variant_index = style
			entry.id = str(component.id) + "_%02d" % style
			entry.package_id = str(_manifest.get("package_id", ""))
			entry.resource_path = _manifest_root.path_join(str(component.file).replace("{index}", formatted))
			var rear := str(component.get("rear_file", ""))
			if not rear.is_empty(): entry.rear_resource_path = _manifest_root.path_join(rear.replace("{index}", formatted))
			var names: Array = component.get("variant_names", [])
			entry.display_name = str(names[index]) if index < names.size() else str(style)
			entry.canvas = _manifest.get("canvas", {"width":1024,"height":1536})
			entry.allow_skin_tone_tint = bool(_manifest.get("allow_skin_tone_tint", false))
			entry.allow_hair_color_tint = bool(_manifest.get("allow_hair_color_tint", false))
			result.append(entry)
	return result

func resolve_profile(profile: Variant) -> Dictionary:
	var values: Dictionary = _values(profile) if profile is Resource else (profile.duplicate(true) if profile is Dictionary else {})
	var source := str(values.get("source", "private_preview_draft"))
	if not values.has("source_values"): values.source_values = values.duplicate(true)
	var raw_gender: Variant = values.get("gender", 0)
	var gender_id := -1
	if raw_gender is String:
		if str(raw_gender) in ["male", "0"]: gender_id = 0
		elif str(raw_gender) in ["female", "1"]: gender_id = 1
	elif int(raw_gender) in [0, 1]: gender_id = int(raw_gender)
	var gender := "male" if gender_id == 0 else ("female" if gender_id == 1 else "unknown")
	var age := int(values.get("current_age", 18))
	var realm := str(values.get("cultivation_realm", "凡人"))
	var context := {"gender":gender, "age":age, "realm":realm, "faction":str(values.get("faction_name", "")), "seed":maxi(1, int(values.get("appearance_seed", 0)))}
	var requested := _requested(values)
	var selected := {}
	var unresolved := []
	if is_instance_valid(_catalog):
		context = _catalog.call("get_context", null, gender, age, str(values.get("faction_name", "")), realm)
		context.seed = int(values.get("appearance_seed", 0)) if int(values.get("appearance_seed", 0)) != 0 else 1
		selected = _catalog.call("select_portrait", context, requested)
	else:
		# Standalone demo fallback resolves explicit IDs only. Reimplementing
		# the catalog's weighted automatic selection would misstate identity.
		for part in STYLE_FIELDS:
			if not requested.has(part) or str(requested[part]).is_empty():
				unresolved.append(part)
				continue
			for entry in _entries(gender, part):
				if str(entry.id) == str(requested[part]) or str(entry.variant_index) == str(requested[part]):
					selected[part] = entry
					break
	values.source = source
	if not values.has("capture_source"): values.capture_source = _capture(source)
	values.gender = gender_id
	values.gender_id = gender_id
	values.gender_name = gender
	values.source_label = str(values.get("source_note", _source_label(source)))
	values.canvas_size = CANVAS
	values.requested_values = requested
	values.resolved = {"parts":selected, "context":context, "catalog_available":is_instance_valid(_catalog), "unresolved_automatic_parts":unresolved}
	values.parts = selected
	for part in STYLE_FIELDS:
		if selected.has(part): values[STYLE_FIELDS[part]] = int(selected[part].variant_index)
	var missing := []
	if gender != "male": missing.append("当前首版未制作女修局内素材")
	for part in ["clothes", "hair_front", "hair_back"]:
		var expected := [5] if part == "clothes" else [0, 1, 2]
		var explicit := str(requested.get(part, ""))
		if explicit.is_valid_int() and int(explicit) >= 0 and selected.has(part) and int(selected[part].get("variant_index", -1)) != int(explicit):
			missing.append(part + ":未制作请求编号" + explicit)
		if not selected.has(part) or int(selected[part].get("variant_index", -1)) not in expected: missing.append(part)
		if selected.has(part) and str(selected[part].get("package_id", "")) != "ink_three_variants_20261004": missing.append(part + ":package")
	values.asset_support = {"supported":missing.is_empty(), "missing":missing, "message":"" if missing.is_empty() else "未制对应局内素材：" + "、".join(missing), "scope":"首版身份覆盖范围；实际四向PNG完整性由独立renderer检查", "sample_mapping":false}
	values.profile_key = str([gender_id, context.seed, values.get("appearance_clothes_style", -1), values.get("appearance_hair_style", -1), values.get("appearance_hair_back_style", -1), requested])
	return values

func get_preview_options(gender: String = "male") -> Dictionary:
	var result := {"clothes":[], "hair_front":[], "hair_back":[]}
	for part in result:
		for entry in _entries(gender, part):
			var value := int(entry.get("variant_index", -1))
			if (part == "clothes" and value != 5) or (part != "clothes" and value not in [0,1,2]): continue
			var option: Dictionary = entry.duplicate(true)
			option.value = value
			option.label = str(entry.get("display_name", entry.get("id", "")))
			result[part].append(option)
	return result

func get_demo_sample_profile() -> Dictionary:
	if _current.is_empty(): resolve_current()
	var sample := _current.duplicate(true)
	var origin := _first_origin(sample.get("capture_source", {}))
	for field in STYLE_FIELDS.values(): sample[field] = 0
	sample.gender = 0
	sample.appearance_clothes_style = 5
	sample.appearance_hair_style = 0
	sample.appearance_hair_back_style = 0
	sample.source = "private_preview_draft"
	sample.source_note = "首版男修内门样例（独立草稿，未替换当前人物）"
	sample.capture_source = _capture("private_preview_draft")
	sample.capture_source.origin = origin
	return resolve_profile(sample)

func get_portrait_layers(profile: Variant) -> Array:
	var dto := resolve_profile(profile)
	var parts: Dictionary = dto.resolved.parts
	var result := []
	for index in range(LAYER_ORDER.size()):
		var part: String = LAYER_ORDER[index]
		var entry: Dictionary = parts.get("clothes" if part == "rear_clothes" else part, {})
		var path := str(entry.get("rear_resource_path" if part == "rear_clothes" else "resource_path", ""))
		result.append({"part":part, "id":str(entry.get("id", "")), "resource_path":path, "visible":not path.is_empty(), "position":Vector2.ZERO, "offset":Vector2.ZERO, "scale":Vector2.ONE, "alpha":1.0, "modulate":Color.WHITE, "canvas_size":CANVAS, "z_index":index})
	return result

func create_draft_resource() -> Resource:
	if _bound_data == null: resolve_current()
	if _bound_data == null: return null
	var draft := _bound_data.duplicate(true)
	draft.set_meta("pawn_demo_private_draft", true)
	if _source_kind == "game_start_preview" and _current.has("gender") and _has_property(draft, "gender"): draft.set("gender", _current.gender)
	return draft

func with_field(profile: Variant, field: String, value: int) -> Dictionary:
	if field not in EDITABLE_FIELDS: return resolve_profile(profile)
	if profile is Resource:
		var draft: Resource = profile.duplicate(true)
		draft.set_meta("pawn_demo_private_draft", true)
		draft.set(field, value)
		var values := _values(draft)
		values.source = "private_preview_draft"
		values.capture_source = _capture("private_preview_draft", draft)
		return resolve_profile(values)
	var values: Dictionary = profile.duplicate(true) if profile is Dictionary else {}
	values[field] = value
	values.source = "private_preview_draft"
	var origin := _first_origin(values.get("capture_source", {}))
	values.capture_source = _capture("private_preview_draft")
	values.capture_source.origin = origin
	return resolve_profile(values)

func _first_origin(capture: Dictionary) -> Dictionary:
	var origin := capture
	for depth in range(64):
		if str(origin.get("kind", "")) != "private_preview_draft" or not origin.get("origin") is Dictionary: break
		origin = origin.origin
	return origin.duplicate(true)
