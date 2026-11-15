extends SceneTree

func _initialize() -> void:
	call_deferred("_export")

func _export() -> void:
	var base: String=get_script().resource_path.get_base_dir()
	var folder: String=base.path_join("art/forest_wolf_frames_v2")
	var actor: Node2D=load(base.path_join("forest_wolf_sequence.gd")).new()
	root.add_child(actor)
	assert(actor.build(folder))
	var native := {}
	var paths: Array[String] = []
	for source in actor.textures:
		var texture: Texture2D=actor.textures[source]
		var portable:=PortableCompressedTexture2D.new()
		portable.set_keep_compressed_buffer(true)
		portable.create_from_image(texture.get_image(),PortableCompressedTexture2D.COMPRESSION_MODE_LOSSLESS)
		var path: String=folder.path_join(str(source).get_file().get_basename()+"_texture.res")
		assert(ResourceSaver.save(portable,path)==OK)
		native[texture.get_instance_id()]=load(path)
		paths.append(path)
	for name in actor.frames.get_animation_names():
		for index in range(actor.frames.get_frame_count(name)):
			var atlas: AtlasTexture=actor.frames.get_frame_texture(name,index)
			if native.has(atlas.atlas.get_instance_id()):
				atlas.atlas=native[atlas.atlas.get_instance_id()]
	var path: String=folder.path_join("forest_wolf_sequences.tres")
	assert(ResourceSaver.save(actor.frames,path,ResourceSaver.FLAG_RELATIVE_PATHS)==OK)
	var content:=FileAccess.get_file_as_string(path)
	for resource_path in paths:
		content=content.replace("path=\""+resource_path+"\"","path=\""+resource_path.get_file()+"\"")
	var file:=FileAccess.open(path,FileAccess.WRITE)
	file.store_string(content)
	file.close()
	var reloaded: SpriteFrames=ResourceLoader.load(path,"SpriteFrames",ResourceLoader.CACHE_MODE_IGNORE)
	var total:=0
	for name in reloaded.get_animation_names(): total+=reloaded.get_frame_count(name)
	assert(total==86)
	print("WOLF SPRITEFRAMES EXPORT PASS animations=",reloaded.get_animation_names().size()," source_cels=",total)
	actor.queue_free()
	await process_frame
	quit()
