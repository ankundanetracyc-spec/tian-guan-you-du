extends SceneTree

const PACK := "res://textures/characters/side_ink_sword_actions_20261024_v5/"
const REVIEW := "res://docs/美术规范/剑修动作重新生成_20261024/"
const POSES := ["cut", "parry", "nurture", "finish"]
const NAMES := ["斩剑 · 斜下斩", "格剑 · 斜架", "养锋 · 横剑敛息", "尽锋 · 单手直刺"]

func _initialize() -> void:
	call_deferred("_render")

func _render() -> void:
	root.mode = Window.MODE_WINDOWED
	root.position = Vector2i(-4096, -4096)
	root.size = Vector2i(1600, 1600)
	root.content_scale_size = root.size
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	var panel := Control.new()
	root.add_child(panel)
	var paper := ColorRect.new()
	paper.color = Color("24302f") if "--dark" in OS.get_cmdline_user_args() else Color("ddd5bd")
	paper.size = Vector2(root.size)
	panel.add_child(paper)
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	_label(panel, font, "剑修 · 右手四招", Vector2(44, 26), 34)
	_label(panel, font, "碧岚云叶袍  /  右向侧身 · 透明素材", Vector2(46, 78), 18)
	for i in range(4):
		var at := Vector2((i % 2) * 780 + 28, (i >> 1) * 730 + 126)
		_label(panel, font, NAMES[i], at + Vector2(16, 0), 25)
		var image_path: String = PACK + POSES[i] + "_assembled.png"
		if not FileAccess.file_exists(image_path):
			image_path = PACK + "source/" + POSES[i] + ".png"
		var picture := Sprite2D.new()
		picture.centered=false
		var review_image := Image.load_from_file(ProjectSettings.globalize_path(image_path))
		picture.texture = ImageTexture.create_from_image(review_image)
		var layout: Dictionary=JSON.parse_string(FileAccess.get_file_as_string(PACK+"layer_layout.json"))
		var grip: Array=layout["poses"][POSES[i]]["grip_canvas"]
		review_image.get_region(Rect2i(Vector2i(grip[0],grip[1])+Vector2i(512,0)-Vector2i(150,150),Vector2i(300,300))).save_png(REVIEW+POSES[i]+"_layered_grip.png")
		picture.position = at + Vector2(345,635)-Vector2(1536,1900)*.36
		picture.scale=Vector2.ONE*.36
		panel.add_child(picture)
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(REVIEW + ("four_actions_dark.png" if "--dark" in OS.get_cmdline_user_args() else "four_actions.png"))
	print("FRESH_SWORD_ACTION_REVIEW_RENDERED")
	quit()

func _label(parent: Node, font: Font, text: String, at: Vector2, size: int) -> void:
	var label := Label.new()
	label.text = text
	label.position = at
	label.add_theme_font_override("font", font)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", Color("e5dcc5") if "--dark" in OS.get_cmdline_user_args() else Color("30433e"))
	parent.add_child(label)
