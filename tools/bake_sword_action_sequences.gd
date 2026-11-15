extends SceneTree
const PACK := "res://textures/vfx/sword_action_sequences_20261024_v1/"
const SIDE := 768
const COUNTS := {"cut":24,"parry":18,"nurture":36,"finish":24,"contact":18}
func _initialize() -> void:
	call_deferred("_bake")
func _bake() -> void:
	var view := SubViewport.new()
	view.size=Vector2i(SIDE,SIDE)
	view.transparent_bg=true
	view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	var rect := ColorRect.new()
	rect.size=Vector2(SIDE,SIDE)
	var material := ShaderMaterial.new()
	material.shader=load(PACK+"bake.gdshader")
	rect.material=material
	view.add_child(rect)
	var records := []
	var mode := 0
	for animation in COUNTS:
		material.set_shader_parameter("kind",mode)
		var images: Array[Image]=[]
		for index in range(COUNTS[animation]):
			material.set_shader_parameter("progress",float(index)/float(COUNTS[animation]-1))
			await process_frame
			await RenderingServer.frame_post_draw
			var output := view.get_texture().get_image()
			output.convert(Image.FORMAT_RGBA8)
			# Store straight-alpha PNGs. Runtime playback uses alpha blending.
			var area := output.get_used_rect()
			for y in range(area.position.y,area.end.y):
				for x in range(area.position.x,area.end.x):
					var color := output.get_pixel(x,y)
					if color.a>.002 and color.a<.999:
						color.r=minf(color.r/color.a,1)
						color.g=minf(color.g/color.a,1)
						color.b=minf(color.b/color.a,1)
						output.set_pixel(x,y,color)
			output.save_png(PACK+"frames/%s_%02d.png"%[animation,index])
			images.append(output)
		for page in range(ceili(float(images.size())/16)):
			var tiles := mini(16,images.size()-page*16)
			var atlas := Image.create(SIDE*4,SIDE*ceili(float(tiles)/4),false,Image.FORMAT_RGBA8)
			for tile in range(tiles):
				atlas.blit_rect(images[page*16+tile],Rect2i(0,0,SIDE,SIDE),Vector2i(tile%4,tile/4)*SIDE)
			atlas.save_png(PACK+"%s_atlas_%d.png"%[animation,page])
		records.append({"name":animation,"frames":images.size(),"fps":30,"frame_size":[SIDE,SIDE]})
		mode+=1
	FileAccess.open(PACK+"manifest.json",FileAccess.WRITE).store_string(JSON.stringify({"animations":records,"palette":"celadon / ivory / restrained ochre","blend":"straight alpha; no additive glow","tool":"Godot native shader bake; all motion and staggered dissolution stored in PNG sequence"},"\t"))
	print("SWORD_SEQUENCE_BAKE_COMPLETE ",records)
	quit()
