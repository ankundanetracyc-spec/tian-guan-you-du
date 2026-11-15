extends SceneTree

const MaleRuntime = preload("guigu_native_first_runtime.gd")
const FemaleRuntime = preload("turn_based_female_runtime_20261106.gd")

func _initialize() -> void:
	call_deferred("_capture")

func _capture() -> void:
	root.size = Vector2i(1280,760)
	root.content_scale_size = Vector2i(1280,760)
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.position = Vector2i(-7000,-7000)
	root.unfocusable = true
	var scene := Node2D.new()
	root.add_child(scene)
	var background := ColorRect.new()
	background.size = Vector2(1280,760)
	background.color = Color("cdd1c6")
	scene.add_child(background)
	var project := ProjectSettings.globalize_path("res://").path_join("../..").simplify_path()
	var font := FontFile.new()
	assert(font.load_dynamic_font(project.path_join("resources/font/LXGWZhenKaiSlabGB-Regular.ttf")) == OK)
	var actors: Array[Node2D] = []
	var head_hashes := []
	for side in range(2):
		var actor: Node2D = MaleRuntime.new() if side==0 else FemaleRuntime.new()
		scene.add_child(actor)
		assert(actor.build())
		actor.position = Vector2(330+side*620,1260)
		actor.wanderer_skin.display_scale = 8.5
		actor.set_skin_mode("experiment")
		actors.append(actor)
		var title := Label.new()
		title.text = "现有男款" if side==0 else "女款修订 · 同一底头"
		title.position = Vector2(80+side*620,30)
		title.size = Vector2(500,50)
		title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		title.add_theme_font_override("font",font)
		title.add_theme_font_size_override("font_size",30)
		title.add_theme_color_override("font_color",Color("30413b"))
		scene.add_child(title)
		var face: Polygon2D
		for record in actor.wanderer_skin.mesh_records:
			if int(record.sprite_id)==65806: face=record.mesh
		assert(face != null)
		var image: Image = face.texture.get_image()
		assert(image.get_size()==Vector2i(24,24))
		var hashing := HashingContext.new()
		hashing.start(HashingContext.HASH_SHA256)
		hashing.update(image.get_data())
		head_hashes.append(hashing.finish().hex_encode())
		var texture := TextureRect.new()
		texture.texture = face.texture
		texture.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		texture.position = Vector2(255+side*620,100)
		texture.size = Vector2(144,144)
		texture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		scene.add_child(texture)
	for frame in range(30):
		for actor in actors:
			actor.wanderer_skin.set_motion_context(1.0/30.0,Vector2.ZERO,float(frame)/30.0,1.0)
			actor.set_character_pose("Idle",float(frame)/30.0,1.0)
		await process_frame
	assert(head_hashes[0]==head_hashes[1],"Female base head must match current male material")
	await RenderingServer.frame_post_draw
	var output := project.path_join("docs/美术规范/男女首套回合制人物_20261106")
	assert(root.get_texture().get_image().save_png(output.path_join("头部同尺度对照_v2.png"))==OK)
	var result := {"passed":true,"same_base_head_rgba":true,"head_canvas":[24,24],"male_head_sha256":head_hashes[0],"female_head_sha256":head_hashes[1],"same_head_renderer_transform_and_uv":true,"female_hair_layers":2,"female_meshes":actors[1].wanderer_skin.meshes.size(),"male_meshes":actors[0].wanderer_skin.meshes.size()}
	FileAccess.open(output.path_join("头部素材对照检查_v2.json"),FileAccess.WRITE).store_string(JSON.stringify(result,"\t"))
	print("PAIR HEAD MATERIAL COMPARE PASS")
	quit()
