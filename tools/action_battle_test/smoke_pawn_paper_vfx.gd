extends SceneTree

## Functional checks for the isolated art sample. GPU appearance is reviewed separately.
const Avatar = preload("pawn_four_way_avatar.gd")
const Adapter = preload("pawn_appearance_profile.gd")
const Vfx = preload("pawn_paper_vfx.gd")
const Room = preload("pawn_paper_vfx_room.gd")
var checks := 0
var failures: Array[String] = []
var stage: Node2D
var combinations: Array[Dictionary] = []
var source_before: Dictionary = {}
var save_before: Dictionary = {}
var image_before: Dictionary = {}

func _initialize() -> void:
	call_deferred("_run")

func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		push_error("PAWN PAPER VFX QA: " + label)

func _folder() -> String:
	return get_script().resource_path.get_base_dir()

func _source_snapshot(resource: Resource) -> Dictionary:
	var snapshot := {}
	if resource == null: return snapshot
	for property in resource.get_property_list():
		if int(property.usage) & PROPERTY_USAGE_STORAGE:
			snapshot[str(property.name)] = str(resource.get(str(property.name)))
	return snapshot

func _files_under(directory: String, snapshot: Dictionary) -> void:
	var dir := DirAccess.open(directory)
	if dir == null: return
	dir.list_dir_begin()
	var filename := dir.get_next()
	while not filename.is_empty():
		var path := directory.path_join(filename)
		if dir.current_is_dir():
			if not filename.begins_with("."): _files_under(path, snapshot)
		else:
			snapshot[path] = FileAccess.get_sha256(path)
		filename = dir.get_next()
	dir.list_dir_end()

func _save_snapshot() -> Dictionary:
	var snapshot := {}
	_files_under("user://game_saves", snapshot)
	var preset := "user://开局设定预设.cfg"
	if FileAccess.file_exists(preset): snapshot[preset] = FileAccess.get_sha256(preset)
	return snapshot

func _image_snapshot(profile: Dictionary) -> Dictionary:
	var snapshot := {}
	for filename in ["base_parts.png", "hair_front.png", "hair_back.png", "registration.json"]:
		var path := _folder().path_join("art/pawn_four_way_20261105").path_join(filename)
		snapshot[path] = FileAccess.get_sha256(path)
	for entry in profile.get("parts", {}).values():
		for field in ["resource_path", "rear_resource_path"]:
			var path := str(entry.get(field, ""))
			if not path.is_empty() and FileAccess.file_exists(path): snapshot[path] = FileAccess.get_sha256(path)
	return snapshot

func _live_children(node: Node) -> int:
	var count := 0
	for child in node.get_children():
		if not child.is_queued_for_deletion(): count += 1
	return count

func _audio_nodes(node: Node) -> int:
	var count := 1 if node is AudioStreamPlayer or node is AudioStreamPlayer2D or node is AudioStreamPlayer3D else 0
	for child in node.get_children(): count += _audio_nodes(child)
	return count

func _button(room: Node2D, text: String) -> Button:
	for child in room.ui.get_children():
		if child is Button and child.text == text: return child
	return null

func _layer_pair_matches(effect: Dictionary, at: Vector2, scale_value: Vector2) -> bool:
	for key in ["rear", "front"]:
		var layer: Node2D = effect[key]
		if not layer.global_position.is_equal_approx(at) or not layer.global_scale.is_equal_approx(scale_value): return false
	return true

func _run() -> void:
	stage = Node2D.new()
	root.add_child(stage)
	var adapter := Adapter.new()
	_check(adapter.build(stage), "real appearance adapter builds")
	var profile: Dictionary = adapter.get_demo_sample_profile()
	var bound: Resource = adapter.get_bound_resource()
	source_before = _source_snapshot(bound)
	save_before = _save_snapshot()
	image_before = _image_snapshot(profile)
	_check(profile.gender == 0 and profile.appearance_clothes_style == 5 and profile.appearance_hair_style == 0 and profile.appearance_hair_back_style == 0, "sample is male clothes5 front0 rear0")
	_check(profile.source == "private_preview_draft" and bool(profile.asset_support.supported), "supported sample is an explicitly private preview")
	var actor := Avatar.new()
	stage.add_child(actor)
	_check(actor.build(), "real four-way avatar builds")
	actor.set_appearance(profile)
	actor.position = Vector2(260, 410)
	actor.scale = Vector2.ONE * 2.3
	actor.z_index = 410
	var bank := Vfx.new()
	stage.add_child(bank)
	bank.bind_actor(actor)
	_check(bank.follow_actor == actor and actor.layers.has("HandRight"), "bank binds real detached near-hand node")
	_check(not bool(bank.diagnostics().audio) and not bool(bank.diagnostics().applies_damage), "bank reports no audio and no damage settlement")
	for kind in Vfx.KINDS:
		for direction_index in range(8):
			var direction := Vector2.from_angle(direction_index * TAU / 8.0)
			actor.set_direction(direction)
			actor.advance(0.025, Vector2(80, 0))
			var expected_height: float = (actor.global_position.y - actor.layers.HandRight.global_position.y) / actor.global_scale.y
			_check(is_equal_approx(bank.actor_height(), expected_height), "real near-hand height " + str([kind, direction_index]))
			bank.play(kind, direction)
			_check(bank.effects.size() == 1 and _live_children(bank) == 2, "one effect owns exactly two depth layers " + str([kind, direction_index]))
			var effect: Dictionary = bank.effects[0]
			_check(Vector2(effect.direction).is_equal_approx(direction) and is_equal_approx(float(effect.height), expected_height), "continuous direction and raised plane captured " + str([kind, direction_index]))
			_check(effect.rear.z_index == actor.z_index + 5 and effect.front.z_index == actor.z_index + 90 and not effect.rear.z_as_relative and not effect.front.z_as_relative, "rear/front layers bracket avatar " + str([kind, direction_index]))
			_check(_layer_pair_matches(effect, actor.global_position, actor.global_scale), "layer anchors inherit actor position and scale " + str([kind, direction_index]))
			bank.advance(0.10)
			var age: float = effect.age
			bank.advance(0.25, true)
			_check(is_equal_approx(age, float(effect.age)), "pause freezes effect age " + str([kind, direction_index]))
			bank.advance(float(effect.duration) + 0.1)
			_check(bank.effects.is_empty(), "elapsed effect expires " + str([kind, direction_index]))
			await process_frame
			_check(bank.get_child_count() == 0, "expired layers are freed " + str([kind, direction_index]))
			combinations.append({"kind": kind, "angle_degrees": direction_index * 45})
	bank.play("unknown", Vector2.RIGHT)
	_check(bank.effects.is_empty(), "unknown effect creates no layers")
	bank.play("fist", Vector2.ZERO)
	_check(bank.effects.size() == 1 and Vector2(bank.effects[0].direction) == Vector2.RIGHT, "zero direction receives finite rightward fallback")
	bank.clear()
	await process_frame
	for i in range(80): bank.play(Vfx.KINDS[i % Vfx.KINDS.size()], Vector2.from_angle(i * 0.31))
	_check(bank.effects.size() == 18 and _live_children(bank) == 36, "80 rapid releases retain at most 18 effects and 36 live layers")
	await process_frame
	_check(bank.get_child_count() == 36, "overflow layers leave the scene after deferred disposal")
	bank.clear()
	await process_frame
	_check(bank.effects.is_empty() and bank.get_child_count() == 0, "clear disposes every effect and layer")
	for kind in ["fist", "wave"]:
		actor.set_direction(Vector2.RIGHT)
		bank.play(kind, Vector2(1, 1))
		var effect: Dictionary = bank.effects[0]
		var released_at: Vector2 = effect.front.global_position
		var released_scale: Vector2 = effect.front.global_scale
		var released_height: float = effect.height
		actor.position += Vector2(95, -35)
		actor.scale = Vector2.ONE * 2.7
		actor.set_direction(Vector2.UP)
		bank.advance(0.08)
		_check(_layer_pair_matches(effect, released_at, released_scale) and is_equal_approx(float(effect.height), released_height), "released effect remains fixed after actor move/scale/turn: " + kind)
		bank.clear()
		await process_frame
	for kind in ["qi", "guard"]:
		bank.play(kind, Vector2.LEFT)
		actor.position += Vector2(-45, 60)
		actor.scale = Vector2.ONE * 2.1
		actor.set_direction(Vector2.LEFT)
		actor.layers.HandRight.position.y -= 4.0
		bank.advance(0.08)
		var effect: Dictionary = bank.effects[0]
		var hand: Vector2 = (actor.layers.HandRight.global_position - actor.global_position) / actor.global_scale
		_check(_layer_pair_matches(effect, actor.global_position, actor.global_scale), "following effect stays at body center: " + kind)
		_check(is_equal_approx(float(effect.height), -hand.y) and Vector2(effect.hand).is_equal_approx(hand), "following effect reads actual moved near-hand, including explicit height offset: " + kind)
		bank.clear()
		await process_frame
	var room: Node2D
	var scene_path := "res://tscn/debug/PawnPaperVfxPreview.tscn"
	if ResourceLoader.exists(scene_path):
		room = (load(scene_path) as PackedScene).instantiate()
	else:
		room = Room.new()
	room.scripted_capture = true
	stage.add_child(room)
	room.set_process(false)
	_check(room.actor != null and room.target != null and room.small_actors.size() == 4 and room.small_banks.size() == 4, "actual room creates actor, private target and four-direction comparisons")
	_check(room.profile.source == "private_preview_draft" and room.profile.appearance_hair_style == 0 and room.profile.appearance_hair_back_style == 0, "room starts with explicit first-set private sample")
	_check(room.buttons.size() == 6, "room exposes all six real selection buttons")
	for i in range(room.buttons.size()):
		room.buttons[i].pressed.emit()
		_check(room.selected == Vfx.KINDS[i] and room.bank.effects.size() == 1 and room.bank.effects[0].kind == Vfx.KINDS[i], "actual selection button replays selected effect: " + Vfx.KINDS[i])
		for j in range(4):
			_check(room.small_banks[j].effects.size() == 1 and Vector2(room.small_banks[j].effects[0].direction).is_equal_approx(Vector2.from_angle(j * PI * 0.5)), "selection reaches fixed four-direction sample " + str([i, j]))
	var pause_button := _button(room, "暂停 / 继续")
	var replay_button := _button(room, "重播")
	var rotate_button := _button(room, "旋转方向")
	var return_button := _button(room, "返回")
	_check(pause_button != null and replay_button != null and rotate_button != null and return_button != null, "actual playback, rotation and return buttons exist")
	room._process(0.04)
	var room_age: float = room.bank.effects[0].age
	var room_clock: float = room.clock
	pause_button.pressed.emit()
	room._process(0.20)
	_check(room.paused and is_equal_approx(room_age, float(room.bank.effects[0].age)) and is_equal_approx(room_clock, room.clock), "actual pause button freezes both room and effect clocks")
	pause_button.pressed.emit()
	room._process(0.04)
	_check(not room.paused and float(room.bank.effects[0].age) > room_age, "actual continue button resumes effect age")
	replay_button.pressed.emit()
	_check(room.bank.effects.size() == 1 and is_zero_approx(float(room.bank.effects[0].age)), "actual replay button restarts effect")
	rotate_button.pressed.emit()
	_check(room.rotate_aim, "actual rotation button enables continuous aim")
	rotate_button.pressed.emit()
	_check(not room.rotate_aim, "actual rotation button disables continuous aim")
	var directions := [Vector2.RIGHT, Vector2.DOWN, Vector2.LEFT, Vector2.UP]
	var names := ["right", "front", "left", "back"]
	for i in range(4):
		room._set_aim(directions[i])
		replay_button.pressed.emit()
		_check(room.aim.is_equal_approx(directions[i]) and room.actor.direction == names[i] and Vector2(room.bank.effects[0].direction).is_equal_approx(directions[i]), "room cardinal aim turns real avatar and replays continuous direction: " + names[i])
	var arrow := InputEventKey.new()
	arrow.keycode = KEY_RIGHT
	arrow.pressed = true
	var before_aim: Vector2 = room.aim
	room._unhandled_input(arrow)
	_check(room.aim.is_equal_approx(before_aim.rotated(PI / 8)) and is_zero_approx(float(room.bank.effects[0].age)), "actual arrow input aims by 22.5 degrees and replays")
	return_button.pressed.emit()
	_check(is_instance_valid(room) and room.is_inside_tree(), "scripted capture suppresses return navigation during QA")
	_check(_audio_nodes(stage) == 0, "bank and actual room create no audio players")
	for item in [room.bank] + room.small_banks:
		_check(not bool(item.diagnostics().audio) and not bool(item.diagnostics().applies_damage), "room bank remains visual-only without HP settlement")
	_check(_source_snapshot(bound) == source_before, "bound real appearance data remains unchanged")
	_check(_image_snapshot(profile) == image_before, "pawn PNGs, registration and original portrait PNGs remain unchanged")
	_check(_save_snapshot() == save_before, "formal save files and appearance preset remain unchanged")
	adapter.unbind()
	stage.queue_free()
	await process_frame
	_finish()

func _finish() -> void:
	var report := {"passed": failures.is_empty(), "checks": checks, "failures": failures,
		"combinations": combinations, "project_root": ProjectSettings.globalize_path("res://"),
		"save_files_checked": save_before.size(), "image_files_checked": image_before.size(),
		"real_bound_resource": not source_before.is_empty(), "headless": DisplayServer.get_name() == "headless",
		"scope": "Real Avatar/Adapter first-set DTO, six effects at eight directions, release/follow anchors, hand height, depth order, bounded lifetime, actual room buttons/aim, unchanged saves/images/source and no audio or damage. GPU shader/visual appearance requires separate rendering review."}
	var output := _folder().path_join("output/pawn_paper_vfx_smoke.json")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--report-path="): output = argument.trim_prefix("--report-path=")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output).get_base_dir())
	FileAccess.open(output, FileAccess.WRITE).store_string(JSON.stringify(report, "\t"))
	print("PAWN PAPER VFX QA ", "PASS" if failures.is_empty() else "FAIL", ": ", checks, " checks; ", output)
	quit(0 if failures.is_empty() else 1)
