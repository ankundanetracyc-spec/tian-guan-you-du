extends SceneTree
const PACK := "res://docs/美术规范/剑修基础动作设计_20261024/"
func _initialize() -> void:
	call_deferred("_render")
func _render() -> void:
	root.mode=Window.MODE_WINDOWED
	root.position=Vector2i(-4096,-4096)
	root.size=Vector2i(1600,1500)
	root.content_scale_size=Vector2i(1600,1500)
	root.content_scale_mode=Window.CONTENT_SCALE_MODE_DISABLED
	var panel := Control.new()
	root.add_child(panel)
	var paper := ColorRect.new()
	paper.color=Color("ddd5bd")
	paper.size=Vector2(1600,1500)
	panel.add_child(paper)
	var font := SystemFont.new()
	font.font_names=PackedStringArray(["Microsoft YaHei"])
	_label(panel,font,"剑修 · 四招基础动作设计",Vector2(50,28),34)
	_label(panel,font,"男修 · 碧岚云叶袍  /  带剑关键姿势参考",Vector2(50,78),18)
	var poses := ["cut","parry","nurture","finish"]
	var names := ["斩剑 · 单手斜斩","格剑 · 提剑斜架","养锋 · 横剑敛息","尽锋 · 双手直刺"]
	for index in range(4):
		var pos := Vector2((index%2)*780+30,(index>>1)*680+124)
		_label(panel,font,names[index],pos+Vector2(20,0),25)
		var picture := TextureRect.new()
		picture.texture=ImageTexture.create_from_image(Image.load_from_file(ProjectSettings.globalize_path(PACK+poses[index]+".png")))
		picture.expand_mode=TextureRect.EXPAND_IGNORE_SIZE
		picture.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		picture.position=pos+Vector2(0,40)
		picture.size=Vector2(750,600)
		panel.add_child(picture)
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(PACK+"four_actions_design.png")
	print("BASIC_SWORD_DESIGN_RENDERED")
	quit()
func _label(parent: Node,font: Font,text: String,at: Vector2,size: int) -> void:
	var label := Label.new()
	label.text=text
	label.position=at
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",Color("30433e"))
	parent.add_child(label)
