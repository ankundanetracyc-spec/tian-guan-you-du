extends SceneTree
const PACK := "res://mods/appearance/male_short_beard_install_20261108/"
const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const FINAL := PACK+"male/face_accessory/02.png"
func read_image(path: String) -> Image:
	var im := Image.new()
	assert(im.load_png_from_buffer(FileAccess.get_file_as_bytes(path))==OK)
	im.convert(Image.FORMAT_RGBA8)
	return im
func luminance(c: Color) -> float:
	return c.r*0.2126+c.g*0.7152+c.b*0.0722
func _initialize() -> void:
	var support := read_image(PACK+"raw/v4_generated_support_on_canvas.png")
	var approved := read_image(PACK+"reference/approved_short_beard_whole.png")
	var clean := read_image(PACK+"reference/approved_no_beard_whole.png")
	var final := Image.create_empty(1024,1536,false,Image.FORMAT_RGBA8)
	var pixels := 0
	var clamped_channels := 0
	var bias := Color(-2.5/255.0,-4.0/255.0,-5.0/255.0,0)
	for y in range(448,552):
		for x in range(448,582):
			# The approved generated support bounds extraction. Dilation does not itself create pigment.
			var supported := false
			for dy in range(-3,4):
				for dx in range(-3,4):
					if support.get_pixel(x+dx,y+dy).a>4.0/255.0:
						supported=true
						break
				if supported: break
			if not supported: continue
			# The lip opening stays on the existing independent mouth layer.
			if x>=478 and x<=556 and y>=477 and y<=491: continue
			var b := clean.get_pixel(x,y)+bias
			var c := approved.get_pixel(x,y)
			var difference := luminance(b)-luminance(c)
			if difference<=5.0/255.0: continue
			var alpha := clampf(difference/maxf(0.01,luminance(b)-80.0/255.0),0.0,1.0)
			if alpha<=4.0/255.0: continue
			var f := Color((c.r-(1.0-alpha)*b.r)/alpha,(c.g-(1.0-alpha)*b.g)/alpha,(c.b-(1.0-alpha)*b.b)/alpha,alpha)
			for v in [f.r,f.g,f.b]:
				if v<0 or v>1: clamped_channels+=1
			final.set_pixel(x,y,Color(clampf(f.r,0,1),clampf(f.g,0,1),clampf(f.b,0,1),alpha))
			pixels+=1
	assert(final.save_png(FINAL)==OK)
	for kind in ["light","dark","checker"]:
		var result:=Image.create_empty(1024,1536,false,Image.FORMAT_RGBA8)
		result.fill(Color("#253136") if kind=="dark" else Color("#e8e1d6"))
		if kind=="checker":
			for y in range(0,1536,24):
				for x in range(0,1024,24):
					if (x/24+y/24)%2:result.fill_rect(Rect2i(x,y,24,24),Color("#aaa9a4"))
		for part in ["hair_back","clothing_back","face_shape","clothing","nose","eyes","eyebrows","mouth","face_accessory","hair_front"]:
			var layer:=final if part=="face_accessory" else read_image(ACTIVE+"male/"+part+"/01.png")
			result.blend_rect(layer,Rect2i(0,0,1024,1536),Vector2i.ZERO)
		result.save_png(PACK+"qa/candidate_"+kind+".png")
		var close:=result.get_region(Rect2i(400,430,230,140))
		close.resize(920,560,Image.INTERPOLATE_NEAREST)
		close.save_png(PACK+"qa/beard_close_"+kind+".png")
	var report:={"method":"Foreground matting during Godot asset import, restricted to the image_gen beard support. No procedural hair drawing.","approved_rgb_source":PACK+"reference/approved_short_beard_whole.png","clean_skin_reference":"res://mods/appearance/male_style_alignment_20261106/preview/male_whole_01.png","generated_support":PACK+"raw/v4_generated_support_on_canvas.png","support_dilation_px":3,"global_skin_background_estimate_delta_rgb":[-2.5,-4,-5],"bias_measurement":"Frozen unbearded/bearded image skin sample medians near lower cheeks/chin; one constant for the whole beard, not local repaint.","minimum_darkening_255":5,"foreground_luminance_reference_255":80,"formula":"Within generated support, estimate a=(Y(B)-Y(C))/(Y(B)-80/255), recover F=(C-(1-a)B)/a. Transparent skin and lip opening excluded; only observed approved dark pigment retained.","selected_whole_canvas_coordinates_retained":true,"runtime_offset":[0,0],"pixels":pixels,"clamped_color_channels":clamped_channels,"final_sha256":FileAccess.get_sha256(FINAL),"no_existing_layer_modified":true,"limitations":"Estimated skin background gives source-faithful hair matting, not a claim of pixel-identical whole portrait or exact hair opacity."}
	var f:=FileAccess.open(PACK+"source/matting_report.json",FileAccess.WRITE)
	f.store_string(JSON.stringify(report,"  "))
	print(JSON.stringify(report))
	quit(0)

