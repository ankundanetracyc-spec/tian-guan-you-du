extends Node

const Adapter = preload("pawn_appearance_profile.gd")
const Avatar = preload("pawn_four_way_avatar.gd")
class StandaloneData extends Resource:
	@export var gender := 0
	@export var appearance_seed := 61105
	@export var appearance_clothes_style := 5
	@export var appearance_hair_style := 0
	@export var appearance_hair_back_style := 1
	@export var appearance_face_style := 0
	@export var appearance_ear_style := 0
	@export var appearance_eye_style := 0
	@export var appearance_brow_style := 0
	@export var appearance_nose_style := 0
	@export var appearance_mouth_style := 0
	@export var appearance_beard_style := 0
	@export var appearance_makeup_style := 0
class CharacterStub extends Node:
	var entity_data: Resource
	var current_age := 18
	var cultivation_realm := "云息"

var character: CharacterStub
var checks := 0
var failures: Array[String] = []
var adapter: RefCounted
var avatar: Node2D
var source: Resource
var signal_count := 0
var last_dto: Dictionary = {}
var last_layers: Array = []
var combinations := 0
var actual_csharp_human := false
var project_dir := ""
var output_dir := ""
var fixture_path := ""
var fixture_before := ""
var save_before: Dictionary = {}
var formal_before: Dictionary = {}

func get_player_character() -> Node:
	return character

func _ready() -> void:
	project_dir = ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().get_base_dir().get_base_dir()
	output_dir = project_dir.path_join("output/pawn_four_way_20261105")
	DirAccess.make_dir_recursive_absolute(output_dir)
	call_deferred("_run")

func _check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		push_error("PAWN PROFILE: " + label)

func _save_hashes(folder: String) -> Dictionary:
	var result := {}
	var directory := DirAccess.open(folder)
	if directory == null: return result
	for file in directory.get_files():
		var path := folder.path_join(file)
		if file.ends_with(".tres") or file.ends_with(".cfg"): result[path] = FileAccess.get_sha256(path)
	for child in directory.get_directories(): result.merge(_save_hashes(folder.path_join(child)))
	return result

func _formal_hashes() -> Dictionary:
	var result := {}
	var root := project_dir.path_join("mods/appearance/ink_three_variants_20261004")
	for relative in ["appearance_parts.json", "male/clothing/06.png", "male/clothing_back/06.png", "male/hair_front/01.png", "male/hair_front/02.png", "male/hair_front/03.png", "male/hair_back/01.png", "male/hair_back/02.png", "male/hair_back/03.png"]:
		var path := root.path_join(relative)
		result[relative] = FileAccess.get_sha256(path)
	return result

func _layer_paths(layers: Array) -> Dictionary:
	var result := {}
	for layer in layers: result[str(layer.part)] = str(layer.resource_path)
	return result

func _on_changed(dto: Dictionary) -> void:
	signal_count += 1
	last_dto = dto
	avatar.set_appearance(dto)
	last_layers = adapter.get_portrait_layers(dto)

func _region(name_value: String) -> Rect2:
	return (avatar.layers[name_value].texture as AtlasTexture).region

func _run() -> void:
	save_before = _save_hashes(ProjectSettings.globalize_path("user://game_saves"))
	var preset := ProjectSettings.globalize_path("user://开局设定预设.cfg")
	if FileAccess.file_exists(preset): save_before[preset] = FileAccess.get_sha256(preset)
	formal_before = _formal_hashes()
	if ResourceLoader.exists("res://csharp/resources/HumanData.cs"):
		source = load("res://csharp/resources/HumanData.cs").new()
		actual_csharp_human = true
	else: source = StandaloneData.new()
	for field in Adapter.STYLE_FIELDS.values(): source.set(field, 0)
	source.set("gender", 0)
	source.set("appearance_seed", 61105)
	source.set("appearance_clothes_style", 5)
	source.set("appearance_hair_style", 0)
	source.set("appearance_hair_back_style", 1)
	character = CharacterStub.new()
	character.name = "ReadOnlyHumanDataFixture"
	character.entity_data = source
	add_child(character)
	var suffix := "root" if actual_csharp_human else "nested"
	fixture_path = output_dir.path_join("readonly_human_fixture_" + suffix + ".tres")
	_check(ResourceSaver.save(source, fixture_path) == OK, "isolated test resource snapshot is written only under output")
	fixture_before = FileAccess.get_sha256(fixture_path)
	adapter = Adapter.new()
	_check(adapter.build(self), "new demo adapter builds without production edits")
	var initial: Dictionary = adapter.resolve_current()
	_check(str(initial.source) == "runtime_player" and int(initial.capture_source.resource_instance_id) == source.get_instance_id(), "current runtime resource has priority and explicit source identity")
	_check(int(initial.appearance_clothes_style) == 5 and int(initial.appearance_hair_style) == 0 and int(initial.appearance_hair_back_style) == 1, "real independent saved fields survive catalog resolution")
	_check(str(initial.parts.clothes.id) == "male_clothes_05" and str(initial.parts.clothes.resource_path).ends_with("/male/clothing/06.png"), "saved clothes5 maps to real inner robe06PNG")
	var options: Dictionary = adapter.get_preview_options()
	_check(options.clothes.size() == 1 and int(options.clothes[0].value) == 5 and options.hair_front.size() == 3 and options.hair_back.size() == 3, "prototype exposes only clothes5 and all three independent front/back choices")
	var layers: Array = adapter.get_portrait_layers(initial)
	var paths := _layer_paths(layers)
	_check(str(paths.rear_clothes).ends_with("/male/clothing_back/06.png") and str(paths.hair_front).ends_with("/male/hair_front/01.png") and str(paths.hair_back).ends_with("/male/hair_back/02.png"), "portrait layers resolve exact front/back/collar source files")
	var ordered := []
	var geometry_ok := true
	var missing := []
	for layer in layers:
		ordered.append(str(layer.part))
		geometry_ok = geometry_ok and layer.position == Vector2.ZERO and layer.offset == Vector2.ZERO and layer.scale == Vector2.ONE and layer.canvas_size == Vector2(1024,1536)
		if bool(layer.visible) and not FileAccess.file_exists(ProjectSettings.globalize_path(str(layer.resource_path))): missing.append(str(layer.resource_path))
	_check(geometry_ok and missing.is_empty(), "all source portrait layers retain the common1024x1536 origin and exist")
	_check(ordered.find("hair_back") < ordered.find("rear_clothes") and ordered.find("rear_clothes") < ordered.find("face") and ordered.find("face") < ordered.find("clothes") and ordered.find("nose") < ordered.find("eye") and ordered.find("eye") < ordered.find("brow") and ordered[-1] == "hair_front", "portrait depth order includes rear collar and independent front/back hair")
	avatar = Avatar.new()
	add_child(avatar)
	_check(avatar.build() and avatar.errors.is_empty(), "actual new four-way renderer loads its three component atlases")
	avatar.set_appearance(initial)
	avatar.set_direction(Vector2.DOWN)
	adapter.profile_changed.connect(_on_changed)
	var old_front := _region("HairFront")
	var old_back := _region("HairBack")
	var old_body := _region("Body")
	var old_paths := _layer_paths(layers)
	source.set("appearance_hair_style", 2)
	source.emit_changed()
	_check(signal_count == 1 and avatar.front_style == 2 and avatar.back_style == 1 and _region("HairFront") != old_front and _region("HairBack") == old_back and _region("Body") == old_body, "real Resource.changed updates only the requested front atlas row")
	var front_paths := _layer_paths(last_layers)
	_check(front_paths.hair_front != old_paths.hair_front and front_paths.hair_back == old_paths.hair_back and front_paths.clothes == old_paths.clothes, "same live front change updates original portrait frontPNG only")
	old_front = _region("HairFront")
	old_back = _region("HairBack")
	source.set("appearance_hair_back_style", 2)
	source.emit_changed()
	_check(signal_count == 2 and _region("HairFront") == old_front and _region("HairBack") != old_back and avatar.back_style == 2, "real Resource.changed updates only the requested back atlas row")
	var back_paths := _layer_paths(last_layers)
	_check(back_paths.hair_front == front_paths.hair_front and back_paths.hair_back != front_paths.hair_back and back_paths.clothes == front_paths.clothes, "same live back change updates original portrait backPNG only")
	source.set("appearance_clothes_style", 0)
	source.emit_changed()
	_check(signal_count == 3 and not avatar.supported and not avatar.visual.visible and str(last_dto.parts.clothes.resource_path).ends_with("/male/clothing/01.png"), "live unsupported clothes retain their real portrait and hide the prototype instead of using inner robe")
	source.set("appearance_clothes_style", 5)
	source.emit_changed()
	_check(signal_count == 4 and avatar.supported and avatar.front_style == 2 and avatar.back_style == 2, "returning to supported clothes retains both independent live hair fields")
	var draft: Dictionary = adapter.with_field(last_dto, "appearance_hair_style", 0)
	_check(int(draft.appearance_hair_style) == 0 and int(draft.appearance_hair_back_style) == 2 and int(draft.appearance_clothes_style) == 5 and int(source.get("appearance_hair_style")) == 2 and str(draft.source) == "private_preview_draft", "single-field private draft preserves back/clothes and never edits the real resource")
	var private_resource: Resource = adapter.create_draft_resource()
	private_resource.set("appearance_hair_back_style", 0)
	private_resource.emit_changed()
	_check(private_resource.get_instance_id() != source.get_instance_id() and int(source.get("appearance_hair_back_style")) == 2 and signal_count == 4, "private Resource draft is detached from real HumanData.changed binding")
	var repeated: Dictionary = initial.duplicate(true)
	for index in range(30): repeated = adapter.with_field(repeated, "appearance_hair_style", index % 3)
	_check(str(repeated.capture_source.origin.kind) == "runtime_player" and not repeated.capture_source.origin.has("origin"), "repeated private edits retain one original capture source without nested history growth")
	for front in range(3):
		for back in range(3):
			var dto: Dictionary = adapter.with_field(adapter.with_field(initial, "appearance_hair_style", front), "appearance_hair_back_style", back)
			for column in range(4):
				avatar.set_appearance(dto)
				avatar.set_direction([Vector2.DOWN,Vector2.RIGHT,Vector2.UP,Vector2.LEFT][column])
				var front_region := _region("HairFront")
				var back_region := _region("HairBack")
				var good: bool = avatar.supported and avatar.errors.is_empty() and int(avatar.render_registration.HairFront.row) == front and int(avatar.render_registration.HairBack.row) == back and int(avatar.render_registration.HairFront.column) == column and int(avatar.render_registration.HairBack.column) == column
				good = good and front_region.size.x > 0 and back_region.size.y > 0 and avatar.layers.Head.texture != null and avatar.layers.Body.texture != null and avatar.layers.HairBack.visible
				good = good and avatar.layers.HairFront.visible == (column != 2)
				_check(good, "actual3x3 hair combination and four-way atlas regions: %d/%d direction%d" % [front,back,column])
				combinations += 1
	for change in [{"gender":"unknown"}, {"gender":1}, {"appearance_clothes_style":999}, {"appearance_hair_style":999}]:
		var unknown: Dictionary = initial.duplicate(true)
		unknown.merge(change, true)
		var dto: Dictionary = adapter.resolve_profile(unknown)
		avatar.set_appearance(dto)
		_check(not bool(dto.asset_support.supported) and not avatar.supported and not avatar.visual.visible, "unknown/unmade identity is unsupported without mappingfallback: " + str(change))
	_check(int(adapter.get_initial_profile().appearance_hair_style) == 0 and int(adapter.get_initial_profile().appearance_hair_back_style) == 1, "initial source snapshot remains unchanged after live updates and drafts")
	_check(FileAccess.get_sha256(fixture_path) == fixture_before, "original resource file SHA stays unchanged through all live signal and draft tests")
	var save_after := _save_hashes(ProjectSettings.globalize_path("user://game_saves"))
	if FileAccess.file_exists(preset): save_after[preset] = FileAccess.get_sha256(preset)
	_check(save_before == save_after and formal_before == _formal_hashes(), "formal user-save/preset and active portrait source SHA values are unchanged")
	adapter.unbind()
	_finish(suffix)

func _finish(suffix: String) -> void:
	var report := {"passed":failures.is_empty(), "checks":checks, "failures":failures, "actual_csharp_human_data":actual_csharp_human, "resource_script":source.get_script().resource_path if source != null else "", "live_changed_deliveries":signal_count, "actual_combinations_times_four_directions":combinations, "formal_save_files_checked":save_before.size(), "formal_portrait_files_checked":formal_before.size(), "source_fixture_sha256_unchanged":FileAccess.get_sha256(fixture_path) == fixture_before, "scope":"Engineering checks of independent live Resource fields, source portrait paths/layer order, actual new avatar AtlasTexture regions, four-direction combinations and unsupported identity hiding. Root uses real CSharp HumanData; standalone nested uses an explicitly labelled exported Resource fixture because it has no production CSharp autoloads. No artistic approval or side/back shape acceptance is inferred."}
	FileAccess.open(output_dir.path_join("profile_adapter_" + suffix + ".json"), FileAccess.WRITE).store_string(JSON.stringify(report, "\t"))
	print("PAWN PROFILE ", suffix, " ", "PASS" if failures.is_empty() else "FAIL", ": ", checks, " checks; ", JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
