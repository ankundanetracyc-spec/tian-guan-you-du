extends SceneTree

const PACK := "res://mods/appearance/male_short_beard_install_20261108/"
const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const SOURCE := PACK + "raw/short_beard_extraction_v4.png"
const FINAL := PACK + "male/face_accessory/02.png"
const OFFSET := Vector2i(435, 446)
const SCALE := 0.10202511763206197

func image_at(path: String) -> Image:
	var im := Image.new()
	assert(im.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK)
	im.convert(Image.FORMAT_RGBA8)
	return im

func _initialize() -> void:
	var raw := image_at(SOURCE)
	assert(raw.get_size() == Vector2i(1608, 978))
	var clean := raw.duplicate() as Image
	clean.convert(Image.FORMAT_RGBAF)
	var removed := 0
	var kept := 0
	var maximum_removed_alpha := 0
	for y in raw.get_height():
		for x in raw.get_width():
			var color := raw.get_pixel(x, y)
			var alpha := int(round(color.a * 255.0))
			if alpha <= 4:
				if alpha > 0:
					removed += 1
					maximum_removed_alpha = maxi(maximum_removed_alpha, alpha)
				clean.set_pixel(x, y, Color.TRANSPARENT)
			else:
				kept += 1
				clean.set_pixel(x,y,Color(color.r*color.a,color.g*color.a,color.b*color.a,color.a))
	clean.resize(roundi(raw.get_width()*SCALE),roundi(raw.get_height()*SCALE),Image.INTERPOLATE_LANCZOS)
	for y in clean.get_height():
		for x in clean.get_width():
			var color := clean.get_pixel(x,y)
			if color.a <= 4.0/255.0:
				clean.set_pixel(x,y,Color.TRANSPARENT)
			else:
				clean.set_pixel(x,y,Color(clampf(color.r/color.a,0,1),clampf(color.g/color.a,0,1),clampf(color.b/color.a,0,1),clampf(color.a,0,1)))
	clean.convert(Image.FORMAT_RGBA8)
	var final := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
	final.blit_rect(clean, Rect2i(Vector2i.ZERO,clean.get_size()), OFFSET)
	assert(final.save_png(FINAL) == OK)
	assert(final.save_png(PACK+"raw/v4_generated_support_on_canvas.png") == OK)
	for kind in ["light", "dark", "checker"]:
		var result := Image.create_empty(1024, 1536, false, Image.FORMAT_RGBA8)
		result.fill(Color("#253136") if kind == "dark" else Color("#e8e1d6"))
		if kind == "checker":
			for y in range(0,1536,24):
				for x in range(0,1024,24):
					if (x/24 + y/24) % 2:
						result.fill_rect(Rect2i(x,y,24,24),Color("#aaa9a4"))
		for part in ["hair_back","clothing_back","face_shape","clothing","nose","eyes","eyebrows","mouth","face_accessory","hair_front"]:
			var layer := final if part == "face_accessory" else image_at(ACTIVE + "male/" + part + "/01.png")
			result.blend_rect(layer, Rect2i(0,0,1024,1536),Vector2i.ZERO)
		result.save_png(PACK + "qa/candidate_" + kind + ".png")
		var close := result.get_region(Rect2i(400,430,230,140))
		close.resize(920,560,Image.INTERPOLATE_NEAREST)
		close.save_png(PACK + "qa/beard_close_" + kind + ".png")
	var approved := image_at(PACK + "reference/approved_short_beard_whole.png")
	var source_close := approved.get_region(Rect2i(400,430,230,140))
	source_close.resize(920,560,Image.INTERPOLATE_NEAREST)
	source_close.save_png(PACK + "qa/approved_beard_close.png")
	var report := {
		"stage":"generated_support_before_source_matting",
		"generated_support_canvas":PACK+"raw/v4_generated_support_on_canvas.png",
		"source":SOURCE,"source_sha256":FileAccess.get_sha256(SOURCE),"final":FINAL,
		"final_sha256":FileAccess.get_sha256(FINAL),"canvas":[1024,1536],
		"pipeline":"Built-in image_gen extracts the approved close-up hair. Godot import removes alpha1..4 dust, performs one premultiplied-alpha uniform Lanczos reduction, then places the whole layer on the shared canvas.",
		"uniform_scale":SCALE,"resized_source":[clean.get_width(),clean.get_height()],
		"integer_translation":[OFFSET.x,OFFSET.y],"runtime_offset":[0,0],"alpha1_to4_removed":removed,
		"maximum_removed_alpha":maximum_removed_alpha,"native_supported_pixels_before_resampling":kept,
		"no_rgb_repaint":true,"no_local_warp":true,
		"alignment_basis":"Three manually read moustache-tail/chin guide pairs; uniform fit, approximate residuals2.27/2.10/2.98 canvas pixels before placement rounding. Native[[305,402],[1297,401],[803,936]] maps near active[[468,485],[566,485],[517,544]]. Final visual review required; no local warps or individual hair movement.",
		"limitations":"Image generation extracted/reconstructed beard strokes; not pixel-identical source matting. No existing facial feature pixels changed."
	}
	var f := FileAccess.open(PACK + "source/import_report.json",FileAccess.WRITE)
	f.store_string(JSON.stringify(report,"  "))
	print(JSON.stringify(report))
	quit(0)

