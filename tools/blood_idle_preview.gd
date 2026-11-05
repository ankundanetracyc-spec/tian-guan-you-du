extends SceneTree
var stage: Node
var actor: Node2D
var arm: Sprite2D
var material: ShaderMaterial
var clock := 0.0
var playing := false
var capture := false

func _initialize() -> void:
	capture = "--capture" in OS.get_cmdline_user_args()
	call_deferred("setup")

func setup() -> void:
	root.size = Vector2i(1440,810)
	if capture: root.position = Vector2i(-4096,-4096)
	stage = load("res://tools/QueueAvatarBattlePreview.tscn").instantiate()
	root.add_child(stage)
	current_scene = stage
	for i in range(45): await process_frame
	stage.get_node("BattleMgr").set_process(false)
	for doll in stage.find_children("BattlePaperDoll", "", true, false):
		if not doll.call("side_ink_faces_left"): actor = doll
	assert(actor != null)
	arm = Sprite2D.new()
	arm.name = "TranslucentBloodIdle"
	arm.texture = ImageTexture.create_from_image(Image.load_from_file("res://textures/vfx/blood_idle_20261103/shape.png"))
	material = ShaderMaterial.new()
	material.shader = load("res://shaders/blood_idle_translucent.gdshader")
	arm.material = material
	actor.add_child(arm)
	arm.z_index = -1
	arm.show_behind_parent = true
	arm.top_level = true
	arm.scale = Vector2.ONE * (350.0 / arm.texture.get_width())
	var ui := CanvasLayer.new()
	ui.layer = 125
	stage.add_child(ui)
	var label := Label.new()
	label.position = Vector2(420,122)
	label.text = "气血单臂 · 半透明待机 / 血气流动"
	label.add_theme_font_size_override("font_size",28)
	label.add_theme_color_override("font_color",Color("fff2df"))
	label.add_theme_color_override("font_outline_color",Color("392627"))
	label.add_theme_constant_override("outline_size",8)
	ui.add_child(label)
	playing = true
	if capture:
		for i in range(64):
			await process_frame
			await RenderingServer.frame_post_draw
			assert(root.get_texture().get_image().save_png("res://tmp/blood_idle_capture/%03d.png" % i) == OK)
		print("BLOOD_IDLE_CAPTURE_OK: 64 frames, 16fps, no attacks")
		quit()

func _process(delta: float) -> bool:
	if not playing or paused: return false
	if not is_instance_valid(actor):
		playing = false
		return false
	clock += delta
	material.set_shader_parameter("phase",clock)
	var tx: Transform2D = actor.call("side_ink_visual_transform")
	var chest: Vector2 = actor.call("side_ink_body")
	arm.global_position = tx * chest + Vector2(-135,-10 + sin(clock*1.4)*3)
	arm.rotation = sin(clock*.7)*.012
	return false
