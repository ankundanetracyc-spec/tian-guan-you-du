extends SceneTree

var pack:="res://textures/vfx/jingzhe_lightning_sequence_20261020_v2/"
const MODES:=[{"name":"charge","count":18,"size":Vector2i(512,512)},{"name":"bolt","count":36,"size":Vector2i(768,1024)},{"name":"impact","count":24,"size":Vector2i(768,768)}]

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--pack="): pack=argument.trim_prefix("--pack=").trim_suffix("/")+"/"
	call_deferred("_bake")

func _bake() -> void:
	DirAccess.make_dir_recursive_absolute(pack+"frames/")
	var viewport:=SubViewport.new()
	viewport.transparent_bg=true
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var rect:=ColorRect.new()
	var material:=ShaderMaterial.new()
	material.shader=load(pack+"bake.gdshader")
	rect.material=material
	viewport.add_child(rect)
	var records: Array[Dictionary]=[]
	for mode in range(MODES.size()):
		var definition: Dictionary=MODES[mode]
		var kind: String=definition["name"]
		var size: Vector2i=definition["size"]
		var count: int=definition["count"]
		viewport.size=size
		rect.size=Vector2(size)
		material.set_shader_parameter("mode",mode)
		var images: Array[Image]=[]
		for index in range(count):
			material.set_shader_parameter("clock",float(index)/30.0)
			await process_frame
			await RenderingServer.frame_post_draw
			var frame:=viewport.get_texture().get_image()
			frame.convert(Image.FORMAT_RGBA8)
			if index==count-1: frame.fill(Color.TRANSPARENT)
			var filename:="frames/%s_%02d.png"%[kind,index]
			frame.save_png(pack+filename)
			images.append(frame)
			records.append({"animation":kind,"frame":index,"file":filename})
		for page in range(ceili(count/16.0)):
			var tiles:=mini(16,count-page*16)
			var atlas:=Image.create(size.x*4,size.y*ceili(tiles/4.0),false,Image.FORMAT_RGBA8)
			for tile in range(tiles):
				atlas.blit_rect(images[page*16+tile],Rect2i(Vector2i.ZERO,size),Vector2i(tile%4,tile/4)*size)
			atlas.save_png(pack+"%s_atlas_%d.png"%[kind,page])
	_write_resource()
	var manifest:=FileAccess.open(pack+"bake_manifest.json",FileAccess.WRITE)
	manifest.store_string(JSON.stringify({"fps":30,"modes":MODES,"frames":records,"source_shader":pack+"bake.gdshader","provenance":"Code-native layered descending lightning. Source shader defines column width, interwoven icy cores, blue mantle, branches and ground remainder. No image generation in this VFX bake."},"\t"))
	print("JINGZHE_BAKE_COMPLETE: 78 transparent PNG frames; charge18/bolt36/impact24")
	quit()

func _write_resource() -> void:
	var steps:=1
	for definition in MODES: steps+=definition["count"]+ceili(definition["count"]/16.0)
	var lines: Array[String]=['[gd_resource type="SpriteFrames" load_steps=%d format=3]'%steps,""]
	for definition in MODES:
		for page in range(ceili(definition["count"]/16.0)):
			lines.append('[ext_resource type="Texture2D" path="%s%s_atlas_%d.png" id="%s%d"]'%[pack,definition["name"],page,definition["name"],page])
	for definition in MODES:
		var size: Vector2i=definition["size"]
		for index in range(definition["count"]):
			var tile:=index%16
			lines.append('\n[sub_resource type="AtlasTexture" id="%s_%d"]'%[definition["name"],index])
			lines.append('atlas = ExtResource("%s%d")'%[definition["name"],index/16])
			lines.append('region = Rect2(%d,%d,%d,%d)'%[tile%4*size.x,tile/4*size.y,size.x,size.y])
			lines.append('filter_clip = true')
	lines.append('\n[resource]\nanimations = [')
	for definition in MODES:
		lines.append('{"frames": [')
		for index in range(definition["count"]):
			lines.append('{"duration":1.0,"texture":SubResource("%s_%d")},'%[definition["name"],index])
		lines.append('],"loop":false,"name":&"%s","speed":30.0},'%definition["name"])
	lines.append(']')
	FileAccess.open(pack+"lightning_effects.tres",FileAccess.WRITE).store_string("\n".join(lines))
