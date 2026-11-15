extends SceneTree

const Room = preload("pawn_paper_vfx_room.gd")
var room: Node2D
var active:=false
var frame:=0
var folder:="res://output/pawn_paper_vfx_20261105"
var frames_per_kind:=96
var ending:=false
var records:Array=[]

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-dir="): folder=argument.trim_prefix("--capture-dir=")
	call_deferred("_setup")

func _setup() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(folder))
	root.mode=Window.MODE_WINDOWED
	root.position=Vector2i(-7000,-7000)
	root.unfocusable=true
	room=Room.new()
	room.scripted_capture=true
	root.add_child(room)
	room.set_process(false)
	room.set_process_unhandled_input(false)
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	await _overview()
	room.set_process(true)
	room.select_effect("fist")
	active=true

func _overview() -> void:
	var viewport:=SubViewport.new()
	viewport.size=Vector2i(1280,900)
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var stage:=Node2D.new()
	viewport.add_child(stage)
	var background:=ColorRect.new()
	background.size=Vector2(1280,900)
	background.color=Color("f0e5d2")
	stage.add_child(background)
	_grid_label(stage,"人物 × 水墨劲势",Vector2(38,24),30)
	_grid_label(stage,"与首套圆手人物同场  /  写意水墨 · 厚薄 · 劲势",Vector2(40,72),17)
	var banks:Array=[]
	for i in range(6):
		var cell:=Vector2(28+(i%3)*417,112+(i/3)*382)
		var panel:=Panel.new()
		panel.position=cell
		panel.size=Vector2(394,360)
		panel.add_theme_stylebox_override("panel",room._style())
		stage.add_child(panel)
		_grid_label(stage,Room.TITLES[i],cell+Vector2(20,14),23)
		var pawn:=Room.Avatar.new()
		stage.add_child(pawn)
		pawn.build()
		pawn.set_appearance(room.profile)
		pawn.set_direction(Vector2.RIGHT)
		pawn.scale=Vector2.ONE*2.30
		pawn.position=cell+Vector2(116,313)
		pawn.z_index=int(pawn.position.y)
		var bank:=Room.Vfx.new()
		stage.add_child(bank)
		bank.bind_actor(pawn)
		bank.play(Room.Vfx.KINDS[i],Vector2.RIGHT)
		bank.advance([0.16,0.16,0.20,0.16,0.46,0.48][i])
		banks.append(bank)
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	viewport.get_texture().get_image().save_png(folder.path_join("style_overview.png"))
	for bank in banks: bank.clear()
	viewport.queue_free()
	await process_frame

func _grid_label(stage: Node2D,text: String,at: Vector2,size: int) -> void:
	var label:=Label.new()
	label.text=text
	label.position=at
	label.add_theme_font_override("font",room.font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",Room.INK)
	stage.add_child(label)

func _process(_delta: float) -> bool:
	if not active: return false
	frame+=1
	var index:=frame/frames_per_kind
	if index<6:
		var local_frame:=frame%frames_per_kind
		if local_frame==0:
			room.select_effect(Room.Vfx.KINDS[index])
			room._set_aim(Vector2.RIGHT)
		if local_frame==15:
			root.get_texture().get_image().save_png(folder.path_join(Room.Vfx.KINDS[index]+"_room.png"))
			records.append({"kind":Room.Vfx.KINDS[index],"frame":frame,"diagnostics":room.bank.diagnostics()})
	else:
		var turn_frame:=frame-6*frames_per_kind
		if turn_frame<8*66:
			if turn_frame%66==0:
				room.select_effect("wave")
				room._set_aim(Vector2.from_angle((turn_frame/66)*PI/4.0))
				room.replay()
		else:
			room.paused=true
			FileAccess.open(folder.path_join("capture_record.json"),FileAccess.WRITE).store_string(JSON.stringify({
				"actual_room":true,"frames":frame,"fps":60,"audio":false,"formal_combat":false,
				"profile":room.profile,"samples":records,"eight_directions":true},"\t"))
			active=false
			print("PAWN_PAPER_VFX_CAPTURE PASS ",folder)
			quit()
	return false
