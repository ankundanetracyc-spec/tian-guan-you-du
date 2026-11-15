extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")
@export var view_size := Vector2i(1200,800)
@export var model_zoom := 7.1
@export var foot_y := 705.0
@export var caption_y := 106.0
@export var title_font_size := 32
var mannequins: Array[Node2D] = []
var clock := 0.0
var running := true
var ready_to_show := false
var font := SystemFont.new()

func _caption(text: String, rect: Rect2, size: int) -> void:
	var label := Label.new()
	label.text = text
	label.position = rect.position
	label.size = rect.size
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",Color("424b48"))
	add_child(label)

func _ready() -> void:
	get_window().size = view_size
	get_window().content_scale_size = view_size
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	var background := ColorRect.new()
	background.size = Vector2(view_size)
	background.color = Color("dddcd1")
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)
	_caption("首套水墨长衣",Rect2(0,38,view_size.x,48),title_font_size)
	_caption("静站",Rect2(view_size.x*0.10,caption_y,view_size.x*0.30,32),21)
	_caption("持架跑动",Rect2(view_size.x*0.60,caption_y,view_size.x*0.30,32),21)
	var floor_line := Line2D.new()
	floor_line.points = PackedVector2Array([Vector2(view_size.x*0.104,foot_y),Vector2(view_size.x*0.917,foot_y)])
	floor_line.width = 1.0
	floor_line.default_color = Color(0.35,0.40,0.37,0.18)
	add_child(floor_line)
	for location in [Vector2(view_size.x*0.25,foot_y),Vector2(view_size.x*0.75,foot_y)]:
		var mannequin := OutfitRuntime.new()
		add_child(mannequin)
		mannequin.position = location
		mannequin.display_zoom = model_zoom
		if not mannequin.build():
			push_error("Independent outfit showcase could not build: "+JSON.stringify(mannequin.load_errors))
			get_tree().quit(1)
			return
		mannequins.append(mannequin)
	ready_to_show = true
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="):
			set_process(false)
			get_window().unfocusable = true
			get_window().position = Vector2i(-7000,-7000)
			await _capture(argument.trim_prefix("--capture-dir="))
			return

func _process(delta: float) -> void:
	if not ready_to_show: return
	_step(minf(delta,0.05))
	if clock>=5.0:
		clock = 0.0
		running = true
	elif clock>=3.0: running = false

func _step(delta: float) -> void:
	clock += delta
	for index in range(mannequins.size()):
		var mannequin: Node2D = mannequins[index]
		var moving: bool = index==1 and running
		mannequin.wanderer_skin.set_motion_context(delta,Vector2(114,0) if moving else Vector2.ZERO,clock,1.0)
		mannequin.set_character_pose("Run_New1" if moving else "Idle",clock,1.0)

func _snapshot(path: String) -> void:
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	assert(get_viewport().get_texture().get_image().save_png(path)==OK)

func _capture(destination: String) -> void:
	var output := ProjectSettings.globalize_path(destination)
	DirAccess.make_dir_recursive_absolute(output)
	var snapshots := []
	for frame in range(60):
		_step(1.0/60.0)
		if frame in [14,29,41,59]:
			var filename := "showcase_%03d.png"%frame
			await _snapshot(output.path_join(filename))
			snapshots.append({"file":filename,"clock":clock,"left_source_phase":mannequins[0].wanderer_skin.sampled_source_phase,"right_source_phase":mannequins[1].wanderer_skin.sampled_source_phase})
	clock = 0.0
	running = true
	var journal := []
	for frame in range(150):
		if frame==90: running = false
		_step(1.0/30.0)
		if frame%2==0: await _snapshot(output.path_join("run_stop_%03d.png"%frame))
		if frame in [89,90,94,104,119,149]: await _snapshot(output.path_join("transition_%03d.png"%frame))
		journal.append({"frame":frame,"clock":clock,"right_clip":mannequins[1].wanderer_skin.sampled_source_clip,"right_source_phase":mannequins[1].wanderer_skin.sampled_source_phase,"release_transition_proxy":mannequins[1].wanderer_skin.release_cloth_transition})
	var materials: Dictionary = mannequins[0].wanderer_skin.material_diagnostics.duplicate(true)
	for key in materials: materials[key].sha256 = FileAccess.get_sha256(materials[key].path)
	var record := {"scope":"independent outfit visual preview; default game entry unchanged","canvas":[view_size.x,view_size.y],"zoom":model_zoom,"nominal_full_source_envelope_pixels":mannequins[0].wanderer_skin.get_native_visual_height()*model_zoom,"source":mannequins[0].get_runtime_summary(),"materials":materials,"snapshots":snapshots,"run_stop_frames":journal,"capture_fps":15,"run_seconds":3,"stop_seconds":2,"motion_note":"Evaluated source bone/skin frames; no secondary-frame surrogate. Run-to-Idle hem release is a project transition proxy up to 0.58 s, not original DynamicBone simulation.","mannequin_note":"Neutral vector head without face features; hair is outside this outfit delivery."}
	var file := FileAccess.open(output.path_join("capture_record.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(record,"\t"))
	file.close()
	print("INK OUTFIT CLEAN SHOWCASE COMPLETE: ",output)
	get_tree().quit()
