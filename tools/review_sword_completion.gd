extends SceneTree

const OUTPUT := "res://docs/美术规范/持剑受击与女修剑招_20261025/"
const FEMALE := "res://textures/characters/side_ink_sword_female_20261025_v1/"
const MALE := "res://textures/characters/side_ink_sword_hurt_20261025_v1/"

func _initialize() -> void:
	call_deferred("_render")

func _render() -> void:
	root.position=Vector2i(-4096,-4096)
	root.size=Vector2i(2400,1120)
	root.content_scale_size=root.size
	root.content_scale_mode=Window.CONTENT_SCALE_MODE_DISABLED
	var font := SystemFont.new()
	font.font_names=PackedStringArray(["Microsoft YaHei"])
	for dark in [false,true]:
		var panel := Control.new()
		root.add_child(panel)
		var back := ColorRect.new()
		back.color=Color("24302f") if dark else Color("ddd5bd")
		back.size=Vector2(root.size)
		panel.add_child(back)
		var specs := [["女修 · 持剑待机",FEMALE,"idle",false],["女修 · 斩剑",FEMALE,"cut",false],["女修 · 格剑",FEMALE,"parry",false],["女修 · 养锋",FEMALE,"nurture",false],["女修 · 尽锋",FEMALE,"finish",false],["女修 · 持剑受击",FEMALE,"hurt",false],["男修 · 持剑受击",MALE,"hurt",false],["女修 · 受击镜像",FEMALE,"hurt",true]]
		for i in range(specs.size()):
			var spec: Array=specs[i]
			var at := Vector2((i%4)*600,(i>>2)*560)
			var label := Label.new()
			label.text=spec[0]
			label.position=at+Vector2(30,22)
			label.add_theme_font_override("font",font)
			label.add_theme_font_size_override("font_size",26)
			label.add_theme_color_override("font_color",Color("e5dcc5") if dark else Color("30433e"))
			panel.add_child(label)
			var view := SubViewport.new()
			view.size=Vector2i(600,500)
			view.transparent_bg=true
			view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
			panel.add_child(view)
			var picture := Sprite2D.new()
			picture.texture=ImageTexture.create_from_image(Image.load_from_file(ProjectSettings.globalize_path(spec[1]+spec[2]+"_assembled.png")))
			picture.centered=false
			picture.offset=-Vector2(1536,1900)
			picture.position=Vector2(420 if spec[2]=="hurt" else 190 if spec[2]=="finish" else 240,470)
			picture.scale=Vector2(-.27 if spec[3] else .27,.27)
			if spec[3]: picture.position.x=180
			view.add_child(picture)
			var surface := TextureRect.new()
			surface.texture=view.get_texture()
			surface.position=at+Vector2(0,60)
			surface.size=Vector2(600,500)
			panel.add_child(surface)
		await process_frame
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(OUTPUT+("review_dark.png" if dark else "review_light.png"))
		panel.queue_free()
		await process_frame
	print("SWORD_COMPLETION_REVIEW_RENDERED")
	quit()
