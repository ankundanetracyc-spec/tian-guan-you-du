extends SceneTree

var pack_root := "res://textures/vfx/ink_response_sequences_20261015_v1/"
const SIDE := 768
var ANIMATIONS := {"hurt":24,"guard":30,"restore":48}

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument=="--include-dodge": ANIMATIONS["dodge"]=24
		if argument.begins_with("--pack-root="):
			pack_root = argument.trim_prefix("--pack-root=").trim_suffix("/") + "/"
	call_deferred("_bake")

func _bake() -> void:
	var viewport := SubViewport.new()
	viewport.size=Vector2i(SIDE,SIDE)
	viewport.transparent_bg=true
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var rect := ColorRect.new()
	rect.size=Vector2(SIDE,SIDE)
	var material := ShaderMaterial.new()
	material.shader=load(pack_root+"bake.gdshader")
	rect.material=material
	viewport.add_child(rect)
	var records: Array[Dictionary]=[]
	var mode := 0
	for kind in ANIMATIONS:
		var count: int=ANIMATIONS[kind]
		var source_kind: String="restore" if kind=="dodge" else kind
		var source:=Image.load_from_file(ProjectSettings.globalize_path(pack_root+"source/"+source_kind+".png"))
		assert(source!=null,"Missing generated source "+kind)
		material.set_shader_parameter("brush",ImageTexture.create_from_image(source))
		material.set_shader_parameter("mode",mode)
		var images: Array[Image]=[]
		for index in range(count):
			material.set_shader_parameter("progress",float(index)/float(count-1))
			await process_frame
			await RenderingServer.frame_post_draw
			var frame:=viewport.get_texture().get_image()
			frame.convert(Image.FORMAT_RGBA8)
			if index==count/2 and (frame.is_invisible() or frame.get_pixel(0,0).a>0.0):
				push_error("Baked effect is blank or has an opaque background: "+kind)
				quit(1)
				return
			var filename:="frames/%s_%02d.png" % [kind,index]
			assert(frame.save_png(pack_root+filename)==OK)
			images.append(frame)
			records.append({"animation":kind,"frame":index,"file":filename})
		for page in range(ceili(float(count)/16.0)):
			var tiles:=mini(16,count-page*16)
			var atlas:=Image.create(SIDE*4,SIDE*ceili(float(tiles)/4.0),false,Image.FORMAT_RGBA8)
			for tile in range(tiles):
				atlas.blit_rect(images[page*16+tile],Rect2i(0,0,SIDE,SIDE),Vector2i(tile%4,tile/4)*SIDE)
			assert(atlas.save_png(pack_root+"%s_atlas_%d.png" % [kind,page])==OK)
		mode+=1
	var manifest:=FileAccess.open(pack_root+"bake_manifest.json",FileAccess.WRITE)
	manifest.store_string(JSON.stringify({"frame_size":[SIDE,SIDE],"fps":30,"animations":ANIMATIONS,"frames":records,"source":"Generated colored brush textures; deterministic Godot reveal/spread/dissolve bake."},"\t"))
	_write_resource()
	print("INK_RESPONSE_BAKE_PASS: ",ANIMATIONS,"; RGBA frames=",records.size())
	quit()

func _write_resource() -> void:
	var resource_count:=1
	for count in ANIMATIONS.values(): resource_count+=int(count)+ceili(float(count)/16.0)
	var lines: Array[String]=['[gd_resource type="SpriteFrames" load_steps=%d format=3]' % resource_count,""]
	for kind in ANIMATIONS:
		for page in range(ceili(float(ANIMATIONS[kind])/16.0)):
			lines.append('[ext_resource type="Texture2D" path="%s%s_atlas_%d.png" id="%s%d"]' % [pack_root,kind,page,kind,page])
	for kind in ANIMATIONS:
		for index in range(ANIMATIONS[kind]):
			var tile:=index%16
			lines.append('\n[sub_resource type="AtlasTexture" id="%s_%d"]' % [kind,index])
			lines.append('atlas = ExtResource("%s%d")' % [kind,index/16])
			lines.append('region = Rect2(%d,%d,%d,%d)' % [tile%4*SIDE,tile/4*SIDE,SIDE,SIDE])
			lines.append('filter_clip = true')
	lines.append('\n[resource]\nanimations = [')
	for kind in ANIMATIONS:
		lines.append('{"frames": [')
		for index in range(ANIMATIONS[kind]):
			lines.append('{"duration":1.0,"texture":SubResource("%s_%d")},' % [kind,index])
		lines.append('],"loop":false,"name":&"%s","speed":30.0},' % kind)
	lines.append(']')
	var resource_file:=FileAccess.open(pack_root+"response_effects.tres",FileAccess.WRITE)
	resource_file.store_string("\n".join(lines))
