extends SceneTree

const ART := "res://textures/vfx/queue_strikes_20261103_v1/"
var stage: Node
var attacker: Node2D
var effect: Sprite2D
var label: Label
var clock := 0.0
var previous_cycle := -1
var capture := false
var shots := 0
var ready_to_play := false
var textures: Array[Texture2D] = []

func _initialize() -> void:
	capture = "--capture" in OS.get_cmdline_user_args()
	call_deferred("setup")

func setup() -> void:
	root.size = Vector2i(1440, 810)
	if capture: root.position = Vector2i(-4096, -4096)
	for kind in ["punch", "chop"]:
		var img := Image.load_from_file(ART + kind + ".png")
		assert(img != null and img.get_width() == img.get_height() * 2)
		textures.append(ImageTexture.create_from_image(img))
	stage = load("res://tools/QueueAvatarBattlePreview.tscn").instantiate()
	root.add_child(stage)
	current_scene = stage
	for i in range(50): await process_frame
	stage.get_node("BattleMgr").set_process(false)
	for doll in stage.find_children("BattlePaperDoll", "", true, false):
		if not doll.call("side_ink_faces_left"): attacker = doll
	assert(attacker != null)
	effect = Sprite2D.new()
	effect.z_index = 100
	effect.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	stage.add_child(effect)
	var ui := CanvasLayer.new()
	ui.layer = 125
	stage.add_child(ui)
	label = Label.new()
	label.position = Vector2(480, 122)
	label.add_theme_font_size_override("font_size", 28)
	label.add_theme_color_override("font_color", Color("fff2d4"))
	label.add_theme_color_override("font_outline_color", Color("302d28"))
	label.add_theme_constant_override("outline_size", 8)
	ui.add_child(label)
	ready_to_play = true
	if capture:
		DirAccess.make_dir_recursive_absolute("res://tmp/queue_strikes_capture")
		for i in range(96):
			await process_frame
			await RenderingServer.frame_post_draw
			var frame := root.get_texture().get_image()
			assert(frame.save_png("res://tmp/queue_strikes_capture/%03d.png" % i) == OK)
		print("QUEUE_STRIKES_CAPTURE_OK: 96 rendered frames at 16 fps, left attacker only, visual-only")
		quit()

func _process(delta: float) -> bool:
	if not ready_to_play or paused: return false
	if not is_instance_valid(stage) or not is_instance_valid(attacker):
		ready_to_play = false
		return false
	clock += delta
	var cycle := int(clock / 3.0)
	var phase := fmod(clock, 3.0)
	var kind := cycle % 2
	label.text = ("拳击 · 无左右手特征" if kind == 0 else "手刀 · 向右切劲") + "   |   特效试演，不结算"
	if cycle != previous_cycle:
		previous_cycle = cycle
		attacker.call("play_attack", Vector2.RIGHT, true)
	var age := phase - 0.18
	effect.visible = age >= 0 and age < 0.85
	if effect.visible:
		# Fixed cell geometry; never normalize individual frames by their visible bounds.
		var tex := textures[kind]
		var cell := Vector2(tex.get_width() / 4.0, tex.get_height() / 2.0)
		var times := [0.045, 0.09, 0.15, 0.29, 0.40, 0.55, 0.70, 0.85]
		var idx := 0
		while idx < 7 and age >= times[idx]: idx += 1
		effect.texture = tex
		effect.region_enabled = true
		effect.region_rect = Rect2(Vector2(idx % 4, idx / 4) * cell + Vector2.ONE, cell - Vector2.ONE * 2)
		var transform: Transform2D = attacker.call("side_ink_visual_transform")
		var hand: Vector2 = attacker.call("side_ink_strike_contact")
		var origin := transform * hand
		effect.global_position = origin + Vector2(45 + minf(age / 0.15, 1.0) * 45, -12)
		effect.scale = Vector2.ONE * (340.0 / cell.x)
		effect.modulate.a = clampf((0.85 - age) / 0.20, 0, 1)
	return false
