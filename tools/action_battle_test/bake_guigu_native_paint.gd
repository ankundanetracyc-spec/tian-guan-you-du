extends SceneTree

## Export only: sample the generated RGB master into the original canvases.
## Original source alpha bytes and transparent padding remain authoritative.
func _initialize() -> void:
	var folder: String = get_script().resource_path.get_base_dir().path_join("art/guigu_native_first_20261105")
	var layout: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(folder.path_join("atlas_layout.json")))
	var paint := Image.load_from_file(folder.path_join("paint_master.png"))
	var report := []
	for entry in layout.parts:
		if int(entry.sprite_id)==87679 and FileAccess.file_exists(folder.path_join("own_hair_registration.json")):
			report.append({"id":entry.sprite_id,"canvas":entry.size,"source_alpha_preserved":false,"exception":"user-selected own tied bun","registration":"own_hair_registration.json","output":folder.path_join("sprite_87679.png")})
			continue
		var source := Image.load_from_file(folder.path_join(entry.source_file))
		var output := Image.create(source.get_width(),source.get_height(),false,Image.FORMAT_RGBA8)
		var rect: Array = entry.atlas_rect
		for y in range(source.get_height()):
			for x in range(source.get_width()):
				var original := source.get_pixel(x,y)
				var point := Vector2((float(rect[0])+x+0.5)/float(layout.width),(float(rect[1])+y+0.5)/float(layout.height))
				var painted := paint.get_pixel(clampi(int(point.x*paint.get_width()),0,paint.get_width()-1),clampi(int(point.y*paint.get_height()),0,paint.get_height()-1))
				painted.a = original.a
				if original.a==0.0: painted = Color(0,0,0,0)
				output.set_pixel(x,y,painted)
		var output_path := folder.path_join("sprite_%d.png"%int(entry.sprite_id))
		assert(output.save_png(output_path)==OK)
		report.append({"id":entry.sprite_id,"canvas":entry.size,"source_alpha_preserved":true,"source":entry.source_file,"output":output_path})
	FileAccess.open(folder.path_join("native_export.json"),FileAccess.WRITE).store_string(JSON.stringify({"source_ppu":100,"original_body_mesh_uv_pivot_weights_unchanged":true,"hairstyle_exception":"own tied bun uses original21x56 canvas/head anchor with rigid full-canvas quad","paint_master":folder.path_join("paint_master.png"),"parts":report},"\t"))
	print("NATIVE PAINT EXPORT COMPLETE: body original alpha retained; own hairstyle exception registered")
	quit()
