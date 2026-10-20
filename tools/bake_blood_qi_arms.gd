extends SceneTree

const PACK := "res://textures/vfx/blood_qi_arms_sequence_20261020_v1/"
const SIZE := 768
const MODES := [{"name":"appear","count":18},{"name":"idle","count":24},{"name":"punch","count":24},{"name":"guard","count":24},{"name":"fade","count":18}]

func _initialize() -> void:
	call_deferred("_bake")

func _bake() -> void:
	DirAccess.make_dir_recursive_absolute(PACK+"frames/")
	var viewport := SubViewport.new()
	viewport.size=Vector2i(SIZE,SIZE)
	viewport.transparent_bg=true
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var rect := ColorRect.new()
	rect.size=Vector2(SIZE,SIZE)
	var material := ShaderMaterial.new()
	material.shader=load(PACK+"bake.gdshader")
	for pose in ["idle","punch","guard"]:
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+"art/"+pose+".png"))
		if source==null or source.is_empty():
			push_error("Missing authored blood arm pose: "+pose)
			quit(1)
			return
		material.set_shader_parameter(pose+"_art",ImageTexture.create_from_image(source))
	rect.material=material
	viewport.add_child(rect)
	var records: Array[Dictionary]=[]
	for mode in range(MODES.size()):
		var definition: Dictionary=MODES[mode]
		var animation: String=definition["name"]
		var count: int=definition["count"]
		material.set_shader_parameter("mode",mode)
		var images: Array[Image]=[]
		for index in range(count):
			var progress := float(index)/float(count if mode==1 else count-1)
			material.set_shader_parameter("progress",progress)
			await process_frame
			await RenderingServer.frame_post_draw
			var frame := viewport.get_texture().get_image()
			frame.convert(Image.FORMAT_RGBA8)
			if (mode==0 and index==0) or (mode==4 and index==count-1): frame.fill(Color.TRANSPARENT)
			var filename := "frames/%s_%02d.png"%[animation,index]
			frame.save_png(PACK+filename)
			images.append(frame)
			records.append({"animation":animation,"frame":index,"file":filename})
		for page in range(ceili(count/16.0)):
			var tiles := mini(16,count-page*16)
			var atlas := Image.create(SIZE*4,SIZE*ceili(tiles/4.0),false,Image.FORMAT_RGBA8)
			atlas.fill(Color.TRANSPARENT)
			for tile in range(tiles): atlas.blit_rect(images[page*16+tile],Rect2i(0,0,SIZE,SIZE),Vector2i(tile%4,tile/4)*SIZE)
			atlas.save_png(PACK+"%s_atlas_%d.png"%[animation,page])
	_write_resource()
	FileAccess.open(PACK+"bake_manifest.json",FileAccess.WRITE).store_string(JSON.stringify({"fps":30,"size":[SIZE,SIZE],"native_canvas":[2048,2048],"native_root":[1024,1900],"frames":records,"provenance":"Three image_gen arm keys, manually registered and baked through native pigment-current and dissolve shader. No combat rules."},"\t"))
	print("BLOOD_QI_ARMS_BAKE_COMPLETE: 108 transparent frames, appear/idle/punch/guard/fade")
	quit()

func _write_resource() -> void:
	var steps := 1
	for definition in MODES: steps+=definition["count"]+ceili(definition["count"]/16.0)
	var lines: Array[String]=['[gd_resource type="SpriteFrames" load_steps=%d format=3]'%steps,""]
	for definition in MODES:
		for page in range(ceili(definition["count"]/16.0)):
			lines.append('[ext_resource type="Texture2D" path="%s%s_atlas_%d.png" id="%s%d"]'%[PACK,definition["name"],page,definition["name"],page])
	for definition in MODES:
		for index in range(definition["count"]):
			var tile := index%16
			lines.append('\n[sub_resource type="AtlasTexture" id="%s_%d"]'%[definition["name"],index])
			lines.append('atlas = ExtResource("%s%d")'%[definition["name"],index/16])
			lines.append('region = Rect2(%d,%d,%d,%d)'%[tile%4*SIZE,tile/4*SIZE,SIZE,SIZE])
			lines.append('filter_clip = true')
	lines.append('\n[resource]\nanimations = [')
	for definition in MODES:
		lines.append('{"frames": [')
		for index in range(definition["count"]): lines.append('{"duration":1.0,"texture":SubResource("%s_%d")},'%[definition["name"],index])
		lines.append('],"loop":%s,"name":&"%s","speed":30.0},'%["true" if definition["name"]=="idle" else "false",definition["name"]])
	lines.append(']')
	FileAccess.open(PACK+"blood_arms.tres",FileAccess.WRITE).store_string("\n".join(lines))
