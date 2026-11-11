extends SceneTree

const MaleRuntime = preload("guigu_native_first_runtime.gd")

func _solid_bounds(image: Image, region: Rect2i) -> Rect2:
	var low := region.end
	var high := region.position
	for y in range(region.position.y,region.end.y):
		for x in range(region.position.x,region.end.x):
			if image.get_pixel(x,y).a<0.5: continue
			low=low.min(Vector2i(x,y))
			high=high.max(Vector2i(x+1,y+1))
	return Rect2(Vector2(low),Vector2(high-low))

func _initialize() -> void:
	call_deferred("_bake")

func _sample(image: Image, point: Vector2) -> Color:
	var x := clampf(point.x-0.5,0,image.get_width()-1)
	var y := clampf(point.y-0.5,0,image.get_height()-1)
	var a := Vector2i(floori(x),floori(y))
	var b := Vector2i(mini(a.x+1,image.get_width()-1),mini(a.y+1,image.get_height()-1))
	return image.get_pixel(a.x,a.y).lerp(image.get_pixel(b.x,a.y),x-a.x).lerp(image.get_pixel(a.x,b.y).lerp(image.get_pixel(b.x,b.y),x-a.x),y-a.y)

func _sample_area(image: Image, center: Vector2, footprint: Vector2) -> Color:
	# Average premultiplied colour so thin hair does not become black single-pixel noise.
	var sum := Vector3.ZERO
	var alpha := 0.0
	for sy in range(4):
		for sx in range(4):
			var offset := (Vector2((sx+0.5)/4.0,(sy+0.5)/4.0)-Vector2.ONE*0.5)*footprint
			var sample := _sample(image,center+offset)
			sum += Vector3(sample.r,sample.g,sample.b)*sample.a
			alpha += sample.a
	if alpha<0.0001: return Color(0,0,0,0)
	return Color(sum.x/alpha,sum.y/alpha,sum.z/alpha,alpha/16.0)

func _bake() -> void:
	var actor := MaleRuntime.new()
	root.add_child(actor)
	assert(actor.build())
	var skin: RefCounted = actor.wanderer_skin
	var project := ProjectSettings.globalize_path("res://").path_join("../..").simplify_path()
	var output := project.path_join("textures/characters/turn_based_pair_20261106/female_native_v3")
	assert(DirAccess.make_dir_recursive_absolute(output)==OK)
	var atlas := Image.load_from_file(project.path_join("textures/characters/turn_based_pair_20261106/female_atlas_master_v1.png"))
	var regions := {85649:Rect2i(60,30,390,850),79543:Rect2i(650,30,160,410),84807:Rect2i(640,560,210,390),87557:Rect2i(90,910,240,590)}
	var report := []
	var seen := {}
	for record in skin.mesh_records:
		var id := int(record.sprite_id)
		if seen.has(id) or id==87679: continue
		seen[id] = true
		var original: Image = record.paint_texture.get_image()
		var src := _solid_bounds(original,Rect2i(Vector2i.ZERO,original.get_size()))
		var dst := _solid_bounds(atlas,regions[id]) if id!=65806 else Rect2()
		var image := Image.create(original.get_width(),original.get_height(),false,Image.FORMAT_RGBA8)
		for y in range(image.get_height()):
			for x in range(image.get_width()):
				var color := original.get_pixel(x,y)
				if id!=65806 and color.a>0.0:
					var within := ((Vector2(x+0.5,y+0.5)-src.position)/src.size).clamp(Vector2.ZERO,Vector2.ONE)
					var painted := _sample_area(atlas,dst.position+within*dst.size,dst.size/src.size)
					var mixed := color.lerp(painted,smoothstep(0.12,0.78,painted.a))
					mixed.a = color.a
					color = mixed
				image.set_pixel(x,y,color if color.a>0.0 else Color(0,0,0,0))
		assert(image.save_png(output.path_join("sprite_%d.png"%id))==OK)
		report.append({"id":id,"canvas":[image.get_width(),image.get_height()],"original_alpha_preserved":true,"shared_base_head":id==65806})
	var hair_image := Image.load_from_file(project.path_join("textures/characters/turn_based_pair_20261106/female_hair_layers_v2.png"))
	var hair_source: Dictionary
	var head_source: Dictionary
	for record in skin.mesh_records:
		if int(record.sprite_id)==87679: hair_source=record.source
		if int(record.sprite_id)==65806: head_source=record.source
	var head_inverse: Transform3D = skin.world_transforms[head_source.rendererPath].affine_inverse()
	var hair_transform: Transform3D = skin.world_transforms[hair_source.rendererPath]
	var hair_uv: Array = hair_source.uvConvention.sourceUvTransform
	var head_uv: Array = head_source.uvConvention.sourceUvTransform
	var face := Image.load_from_file(project.path_join("tools/action_battle_test/art/guigu_native_first_20261105/sprite_65806.png"))
	for back in [false,true]:
		var logical := Rect2(1.34,-2.15,14.4,50.6) if back else Rect2(1.74,-0.12,14.76,25.2)
		var tile := Rect2(440,320,400,920) if back else Rect2(20,340,410,700)
		var image := Image.create(21,56,false,Image.FORMAT_RGBA8)
		image.fill(Color(0,0,0,0))
		for y in range(56):
			for x in range(21):
				var pixel := Vector2(x+0.5,y+0.5)
				var local := Vector3((pixel.x-float(hair_uv[1]))/float(hair_uv[0]),(56.0-pixel.y-float(hair_uv[3]))/float(hair_uv[2]),0)
				var head := head_inverse*(hair_transform*local)
				var head_pixel := Vector2(head.x*float(head_uv[0])+float(head_uv[1]),24.0-(head.y*float(head_uv[2])+float(head_uv[3])))
				# Keep the existing side-profile forehead, nose and chin readable.
				if not back and head_pixel.x>=12.0 and head_pixel.y>=7.5 and head_pixel.y<19.5:
					var face_pixel := Vector2i(floori(head_pixel.x),floori(head_pixel.y))
					if Rect2i(0,0,24,24).has_point(face_pixel) and face.get_pixelv(face_pixel).a>0.1: continue
				var within := (head_pixel-logical.position)/logical.size
				if within.x<0.0 or within.x>1.0 or within.y<0.0 or within.y>1.0: continue
				image.set_pixel(x,y,_sample_area(hair_image,tile.position+within*tile.size,tile.size/logical.size))
		var name := "hair_back.png" if back else "hair_front.png"
		assert(image.save_png(output.path_join(name))==OK)
		var bounds := image.get_used_rect()
		report.append({"part":name,"canvas":[21,56],"visible_xywh":[bounds.position.x,bounds.position.y,bounds.size.x,bounds.size.y],"anchor":"same original hair renderer UV and head/neck transform as male"})
	FileAccess.open(output.path_join("native_export.json"),FileAccess.WRITE).store_string(JSON.stringify({"parts":report,"source_head_canvas":[24,24],"source_hair_canvas":[21,56],"scale":"one native texture pixel per source pixel; body/arm/leg canvases unchanged","filter":"4x4 premultiplied area sampling","front_hair_occlusion":"preserve existing side-profile face silhouette","back_hair_registration":"lowered three source-head pixels to keep the half-up knot inside the original canvas"},"\t"))
	print("FEMALE NATIVE CANVASES EXPORTED: ",report)
	quit()
