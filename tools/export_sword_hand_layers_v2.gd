extends SceneTree
const PACK := "res://textures/characters/side_ink_sword_actions_20261024_v2/"
var split_shader: Shader
func _initialize() -> void:
	call_deferred("_export")
func _export() -> void:
	split_shader=Shader.new()
	split_shader.code="shader_type canvas_item; uniform sampler2D hand_layer; void fragment(){ vec4 body=texture(TEXTURE,UV); float hand=texture(hand_layer,UV).a; COLOR=vec4(body.rgb,clamp((body.a-hand)/max(1.0-hand,0.0001),0.0,1.0)); }"
	var file := FileAccess.open(PACK+"layer_layout.json",FileAccess.READ)
	var layouts: Dictionary=JSON.parse_string(file.get_as_text())
	for pose in layouts:
		var layout: Dictionary=layouts[pose]
		var original := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/"+pose+"_body.png"))
		var source_size := original.get_size()
		original.convert(Image.FORMAT_RGBA8)
		original.resize(2048,2048,Image.INTERPOLATE_LANCZOS)
		var texture := ImageTexture.create_from_image(original)
		var view := SubViewport.new()
		view.size=Vector2i(2048,2048)
		view.transparent_bg=true
		view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
		root.add_child(view)
		for contour in layout["front_contours"]:
			var polygon := Polygon2D.new()
			var points := PackedVector2Array()
			for point in contour:
				points.append(Vector2(point[0],point[1])*Vector2(2048.0/source_size.x,2048.0/source_size.y))
			polygon.polygon=points
			polygon.uv=points
			polygon.texture=texture
			polygon.antialiased=true
			view.add_child(polygon)
		await process_frame
		await RenderingServer.frame_post_draw
		var front := view.get_texture().get_image()
		front.save_png(PACK+pose+"_hand_front.png")
		for child in view.get_children():
			child.queue_free()
		await process_frame
		var body := Sprite2D.new()
		body.centered=false
		body.texture=texture
		var material := ShaderMaterial.new()
		material.shader=split_shader
		material.set_shader_parameter("hand_layer",ImageTexture.create_from_image(front))
		body.material=material
		view.add_child(body)
		await process_frame
		await RenderingServer.frame_post_draw
		view.get_texture().get_image().save_png(PACK+pose+"_body_back.png")
		view.queue_free()
		await process_frame
	print("SWORD_HAND_LAYERS_EXPORTED")
	quit()
