extends "ink_outfit_fitting_room.gd"

const ApprovedRuntime = preload("approved_outfit_fitting_runtime.gd")
var approved_description: Label
var sword_mode := "off"
var sword_buttons: Dictionary = {}

func _init() -> void:
	zoom = 5.0

func _ready() -> void:
	super._ready()
	get_window().size = Vector2i(1000,830)
	get_window().content_scale_size = Vector2i(1000,830)
	for child in get_children():
		if child is ColorRect: child.size.y = 830
	for button in hand_buttons.values(): button.position.y = 707
	var held := _button(self,"9  持剑",758,func(): _select_sword("hold"))
	held.position.x = 286
	sword_buttons.hold = held
	var swing := _button(self,"0  挥剑",758,func(): _select_sword("swing"))
	swing.position.x = 496
	sword_buttons.swing = swing
	_label(self,"持剑后按 2 查看跑动",Vector2(711,770),16)
	if OS.get_cmdline_user_args().has("--preview-sword"):
		_select_sword("hold")
		if OS.get_cmdline_user_args().has("--preview-run"): _select_pose("Run_New1")

func _reload_outfit() -> void:
	if fitting_rig!=null:
		remove_child(fitting_rig)
		fitting_rig.queue_free()
	fitting_rig = ApprovedRuntime.new()
	add_child(fitting_rig)
	fitting_rig.position = Vector2(650,600)
	var built: bool = fitting_rig.build()
	availability_label.text = "常服 · 鬼谷参考跑姿与衣摆" if built else "试穿载入失败"
	_select_pose("Idle")
	_select_hand(hand_preview)
	if approved_description==null:
		approved_description = _label(self,"旧版常服 · 整体衣摆与惯性",Vector2(286,112),18)
	get_window().title = "玉虚常服 · 旧版素材整体试穿"
	if not built: push_error(str(fitting_rig.load_errors))

func _select_hand(style: String) -> void:
	hand_preview = style
	sword_mode = "off"
	if fitting_rig!=null and fitting_rig.loaded:
		fitting_rig.position.x = 650
		var skin = fitting_rig.wanderer_skin
		if skin.approved_sword!=null: skin.approved_sword.set_mode("off")
		if skin.approved_hands!=null: skin.approved_hands.set_pose_override(style)
	for key in hand_buttons: hand_buttons[key].disabled = key==style
	for button in sword_buttons.values(): button.disabled = false
	if paused: _pose_step(0.00001)

func _select_sword(mode: String) -> void:
	if fitting_rig==null or not fitting_rig.loaded: return
	sword_mode = mode
	fitting_rig.position.x = 590
	fitting_rig.wanderer_skin.approved_sword.set_mode(mode)
	_select_pose("AttackIdle" if mode=="swing" else "Idle")
	for button in hand_buttons.values(): button.disabled = false
	for key in sword_buttons: sword_buttons[key].disabled = key==mode
	if paused: _pose_step(0.00001)

func _unhandled_key_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.physical_keycode==KEY_9:
			_select_sword("hold")
			return
		if event.physical_keycode==KEY_0:
			_select_sword("swing")
			return
	super._unhandled_key_input(event)

func _capture_fitting(destination: String) -> void:
	if fitting_rig==null or not fitting_rig.loaded:
		get_tree().quit(1)
		return
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	get_window().size = Vector2i(1000,830)
	get_window().content_scale_size = Vector2i(1000,830)
	for child in get_children():
		if child is ColorRect: child.size.y = 830
	for button in hand_buttons.values(): button.position.y = 707
	var snapshots := []
	for direction in [1.0,-1.0]:
		facing = direction
		for clip in ["Idle","Run_New1","BackstepWalk","AttackIdle","Step1"]:
			_select_pose(clip)
			for frame in range(72):
				_pose_step(1.0/60.0)
				if frame in [29,47,71]:
					var file := "%s_%s_%03d.png"%[clip,"right" if facing>0.0 else "left",frame]
					await _snapshot(output.path_join(file))
					snapshots.append({"file":file,"clip":clip,"facing":facing,"diagnostics":fitting_rig.get_runtime_summary()})
	facing = 1.0
	for frame in range(180):
		if frame==0: _select_pose("Run_New1")
		if frame==90: _select_pose("Idle")
		_pose_step(1.0/30.0)
		if frame%2==0: await _snapshot(output.path_join("run_stop_%03d.png"%frame))
	for style in ["open","fist","sword_near","grip_near"]:
		_select_hand(style)
		_select_pose("Idle")
		for frame in range(36): _pose_step(1.0/60.0)
		await _snapshot(output.path_join("hand_%s.png"%style))
	for direction in [1.0,-1.0]:
		facing = direction
		for mode in ["hold","swing"]:
			_select_sword(mode)
			for clip in (["Idle","Run_New1"] if mode=="hold" else ["AttackIdle"]):
				_select_pose(clip)
				for frame in range(72):
					_pose_step(1.0/60.0)
					if frame in [11,23,35,47,59,71]:
						var name := "sword_%s_%s_%s_%03d.png"%[mode,clip,"right" if facing>0.0 else "left",frame]
						await _snapshot(output.path_join(name))
	_select_hand("auto")
	var record := FileAccess.open(output.path_join("capture_record.json"),FileAccess.WRITE)
	record.store_string(JSON.stringify({"runtime":fitting_rig.get_runtime_summary(),"snapshots":snapshots,"run_stop_frames":180,"frame_rate":30},"\t"))
	record.close()
	print("APPROVED OUTFIT NATIVE CAPTURE COMPLETE: ",output)
	get_tree().quit()
