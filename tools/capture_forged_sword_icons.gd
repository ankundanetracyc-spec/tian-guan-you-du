extends SceneTree

const OUTPUT := "res://docs/美术规范/炼器剑图标重绘_20261023/"
const ASSETS := "res://textures/ui/forged_swords_20261023_v1/"

func _initialize() -> void:
	call_deferred("_capture")

func _capture() -> void:
	root.mode = Window.MODE_WINDOWED
	root.position = Vector2i(-4096, -4096)
	root.size = Vector2i(1440, 900)
	root.content_scale_size = Vector2i(1440, 900)
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	var canvas := Control.new()
	root.add_child(canvas)
	var background := ColorRect.new()
	background.color = Color("ddd5bd")
	background.size = Vector2(1440, 900)
	canvas.add_child(background)
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei", "SimHei"])
	_label(canvas,font,"炼器 · 水墨剑形",Vector2(64,32),32,Color("293c39"))
	_label(canvas,font,"修长、宽展、厚脊  /  与人物共用墨线和概括明暗",Vector2(64,82),18,Color("6d6857"))
	var names := ["修长", "宽展", "厚脊"]
	var files := ["slender_sword.png", "broad_sword.png", "spined_sword.png"]
	for index in range(3):
		var y := 148 + index * 240
		_label(canvas,font,names[index],Vector2(64,y+40),22,Color("293c39"))
		var texture := load(ASSETS+files[index]) as Texture2D
		var sword := TextureRect.new()
		sword.texture = texture
		sword.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		sword.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		sword.position = Vector2(180,y-80)
		sword.size = Vector2(1190,390)
		canvas.add_child(sword)
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"sword_icons.png")
	background.color = Color("263633")
	for child in canvas.get_children():
		if child is Label:
			child.add_theme_color_override("font_color",Color("e4dcc9"))
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+"sword_icons_dark.png")
	print("SWORD_ICON_CAPTURE_COMPLETE")
	quit()

func _label(parent: Control, font: Font, text: String, pos: Vector2, size: int, color: Color) -> void:
	var label := Label.new()
	label.text = text
	label.position = pos
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",color)
	parent.add_child(label)
