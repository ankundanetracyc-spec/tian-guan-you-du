extends Node

# 只打开正式原生组件，使用上一轮隔离存档的副本；不运行验收、不保存或自动退出。
var _actor: Node
var _mode := "character"
var _capture_path := ""
var _show_controls := false
var _run_battle := false
var _record_path := ""
var _record_until := 0
var _record_next := 0
var _record_times: Array[int] = []
var _hold_after_record := false

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	var data_path := ""
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--viewer-mode="):
			_mode = argument.trim_prefix("--viewer-mode=")
		if argument.begins_with("--viewer-data="):
			data_path = argument.trim_prefix("--viewer-data=")
		if argument.begins_with("--viewer-capture="):
			_capture_path = argument.trim_prefix("--viewer-capture=")
		if argument == "--viewer-controls":
			_show_controls = true
		if argument == "--viewer-combat":
			_run_battle = true
		if argument.begins_with("--viewer-record="):
			_record_path = argument.trim_prefix("--viewer-record=")
		if argument == "--viewer-hold":
			_hold_after_record = true
	if data_path.is_empty() or not ResourceLoader.exists(data_path):
		push_error("人物原生查看入口需要隔离人物存档。")
		get_tree().quit(1)
		return
	var save: Resource = ResourceLoader.load(data_path, "", ResourceLoader.CACHE_MODE_IGNORE_DEEP)
	var data: Resource = save.get("player_data").duplicate(true)
	get_window().mode = Window.MODE_WINDOWED
	get_window().content_scale_size = Vector2i.ZERO
	get_window().size = Vector2i(1280, 720)
	get_window().min_size = Vector2i(960, 540)
	get_window().title = "人物战斗 · 正式实装" if _mode == "character" else "局内配招 · 正式实装"
	if _run_battle:
		get_window().title = "战斗过程 · 左下招式盘"
	get_window().position = Vector2i(20, 45) if _mode == "character" else Vector2i(610, 250)
	get_node("/root/GameTimeManager").call("pause_time")
	_actor = load("res://csharp/entity/Character.cs").new()
	_actor.name = "ViewerCharacter"
	_actor.process_mode = Node.PROCESS_MODE_DISABLED
	add_child(_actor)
	_actor.call("load_from_data", data)
	_actor.set("entity_type", 0) # WorldEntity.EntityType.CHARACTER；与正式人物入口一致。
	_actor.hide()
	if _mode == "character":
		await _show_character()
	else:
		await _show_battle()
	await get_tree().process_frame
	await get_tree().process_frame
	get_window().grab_focus()
	if not _capture_path.is_empty():
		for _frame in range(12):
			await get_tree().process_frame
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(_capture_path.get_base_dir()))
		get_viewport().get_texture().get_image().save_png(_capture_path)
	print("NATIVE_COMBAT_VIEWER_READY mode=" + _mode)

func _show_character() -> void:
	var panel: Control = load("res://tscn/panel/InfoPanel.tscn").instantiate()
	add_child(panel)
	panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.call("show_entity", _actor)
	await get_tree().process_frame
	panel.find_child("CharacterTab2", true, false).emit_signal("pressed")

func _show_battle() -> void:
	var scene := Node2D.new()
	scene.name = "NativeBattleView"
	add_child(scene)
	var deploy := Node2D.new()
	deploy.name = "DeployLayer"
	scene.add_child(deploy)
	var ui := CanvasLayer.new()
	ui.name = "UILayer"
	scene.add_child(ui)
	ui.add_child(load("res://tscn/panel/CombatSkillBar.tscn").instantiate())
	var manager: Node = load("res://csharp/battle/BattleManager.cs").new()
	manager.name = "BattleMgr"
	manager.set("deploy_layer", NodePath("../DeployLayer"))
	manager.set("isolated_trial_battle", true)
	scene.add_child(manager)
	await get_tree().process_frame
	var player: Resource = _actor.call("get_snapshot_data")
	player.set("faction", 1)
	player.set("uid", "native_viewer_player")
	var foe: Resource = player.duplicate(true)
	foe.set("faction", 2)
	foe.set("uid", "native_viewer_foe")
	foe.set("entity_name", "对练者")
	var character: PackedScene = load("res://tscn/battle/BattleTestCharacter.tscn")
	manager.call("setup_team_battle", [character], [character], [player], [foe])
	await get_tree().process_frame
	if _show_controls:
		var hud: Node = scene.find_child("BattleBoardHud", true, false)
		hud.call("CloseEditor")
		await get_tree().process_frame
		for child in hud.get_children():
			if child is Button and (child.name == "BoardModeToggle" or child.text.begins_with("倍速") or child.text in ["暂停", "继续"]):
				print("BATTLE_BUTTON_LAYOUT ", child.text, " position=", child.position, " size=", child.size, " font=", child.get_theme_font_size("font_size"))
	if _run_battle:
		if not _record_path.is_empty():
			DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(_record_path))
			_record_until = Time.get_ticks_msec() + 10000
		scene.find_child("BeginCombat", true, false).emit_signal("pressed")

func _process(_delta: float) -> void:
	if _record_until == 0:
		return
	var now := Time.get_ticks_msec()
	if now >= _record_until:
		_record_until = 0
		var report := FileAccess.open(_record_path.path_join("frames.json"), FileAccess.WRITE)
		report.store_string(JSON.stringify({"timestamps_ms": _record_times, "source": "native_viewport"}))
		print("NATIVE_COMBAT_RECORDING_READY frames=", _record_times.size())
		if _hold_after_record:
			var manager := find_child("BattleMgr", true, false)
			if manager != null and manager.call("is_board_battle") and not manager.call("is_board_editing") and not manager.call("is_battle_paused"):
				manager.call("toggle_battle_pause")
		return
	if now < _record_next:
		return
	_record_next = now + 67
	get_viewport().get_texture().get_image().save_jpg(_record_path.path_join("%04d.jpg" % _record_times.size()), 0.9)
	_record_times.append(now)

func get_profile_location_for(_character: Node) -> String:
	return "独立查看"
