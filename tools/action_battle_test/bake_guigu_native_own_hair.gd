extends SceneTree

func _initialize() -> void:
	var folder: String = get_script().resource_path.get_base_dir().path_join("art/guigu_native_first_20261105")
	var master := Image.load_from_file(folder.path_join("own_hair_master.png"))
	var crop := master.get_region(Rect2i(136,90,499,530))
	crop.resize(21,22,Image.INTERPOLATE_LANCZOS)
	var native := Image.create(21,56,false,Image.FORMAT_RGBA8)
	native.fill(Color(0,0,0,0))
	native.blit_rect(crop,Rect2i(0,0,21,22),Vector2i.ZERO)
	var target := folder.path_join("sprite_87679.png")
	assert(native.save_png(target)==OK)
	var used := native.get_used_rect()
	var metadata := {"user_instruction":"Hairstyle follows our own approved plain bun","style_reference":"../yuxu_fullbody_v15/cutout.png","generation":"built-in image_gen, isolated own hairstyle master","master":"own_hair_master.png","master_crop":[136,90,499,530],"native_canvas":[21,56],"native_visible_target":[0,0,21,22],"native_alpha_bbox":[used.position.x,used.position.y,used.size.x,used.size.y],"png_sha256":FileAccess.get_sha256(target),"source_neck_anchor_px":[12.796864086,20.105417728],"source_pivot":[0.5,0.5],"pixels_per_unit":100,"mesh_adaptation":"full original canvas quad, rigid original renderer/head attachment; source long ponytail alpha/triangles are an explicit hairstyle exception","physics":"original cloth unchanged; original hair chain retained for reference, compact tied bun follows head rigidly"}
	FileAccess.open(folder.path_join("own_hair_registration.json"),FileAccess.WRITE).store_string(JSON.stringify(metadata,"\t"))
	print("OWN BUN NATIVE EXPORT COMPLETE: 21x56, original head attachment")
	quit()
