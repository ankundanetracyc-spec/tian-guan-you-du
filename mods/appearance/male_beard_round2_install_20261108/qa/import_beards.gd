extends SceneTree

const PACK := "res://mods/appearance/male_beard_round2_install_20261108/"
const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const CANVAS := Vector2i(1024,1536)
const COMMON_QA_RECT := Rect2i(395,395,245,200)
const SPECS := {
	"01":{"name":"八字短髭","physical":3,"style":4,"roi":[450,448,134,43]},
	"02":{"name":"短颏须","physical":4,"style":5,"roi":[475,505,85,68]},
	"03":{"name":"细髭尖颏","physical":5,"style":6,"roi":[450,448,135,132]},
	"04":{"name":"薄络腮须","physical":6,"style":7,"roi":[417,418,195,139]}
}
# The same sampling locations are measured separately in every approved image.
# Generated-support pixels and dark scalp/nose pixels are excluded from the samples.
const DEFAULT_SAMPLE_BOXES := [
	[441,405,28,35],[568,405,28,35],[483,496,18,15],[532,496,18,15]
]
const DEFAULT_LIP_EXCLUSIONS := [[478,477,79,15]]

func path_in_pack(value: String) -> String:
	return value if value.begins_with("res://") or value.is_absolute_path() else PACK+value

func read_image(path: String) -> Image:
	var image := Image.new()
	assert(image.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK,"Cannot decode: "+path)
	image.convert(Image.FORMAT_RGBA8)
	return image

func rect_from_array(value: Array) -> Rect2i:
	assert(value.size() == 4,"Expected x,y,width,height")
	return Rect2i(int(value[0]),int(value[1]),int(value[2]),int(value[3]))

func luminance(color: Color) -> float:
	return color.r*0.2126+color.g*0.7152+color.b*0.0722

func median(values: Array[float]) -> float:
	assert(not values.is_empty(),"No eligible skin samples")
	values.sort()
	var middle: int = values.size() >> 1
	return values[middle] if values.size()%2 else (values[middle-1]+values[middle])*0.5

func supported_at(image: Image, x: int, y: int, radius: int) -> bool:
	for dy in range(-radius,radius+1):
		for dx in range(-radius,radius+1):
			var px: int = x+dx
			var py: int = y+dy
			if px>=0 and px<CANVAS.x and py>=0 and py<CANVAS.y and image.get_pixel(px,py).a>4.0/255.0:
				return true
	return false

func estimate_bias(approved: Image, clean: Image, support: Image, record: Dictionary) -> Dictionary:
	var red: Array[float] = []
	var green: Array[float] = []
	var blue: Array[float] = []
	var evidence: Array[Dictionary] = []
	var boxes: Array = record.get("sample_boxes",DEFAULT_SAMPLE_BOXES)
	for box: Array in boxes:
		var rect := rect_from_array(box).intersection(Rect2i(Vector2i.ZERO,CANVAS))
		var local_red: Array[float] = []
		var local_green: Array[float] = []
		var local_blue: Array[float] = []
		for y in range(rect.position.y,rect.end.y):
			for x in range(rect.position.x,rect.end.x):
				if supported_at(support,x,y,3):
					continue
				var c := approved.get_pixel(x,y)
				var b := clean.get_pixel(x,y)
				if luminance(c)<0.55 or luminance(b)<0.55:
					continue
				var delta := c-b
				local_red.append(delta.r*255.0)
				local_green.append(delta.g*255.0)
				local_blue.append(delta.b*255.0)
		if not local_red.is_empty():
			red.append_array(local_red)
			green.append_array(local_green)
			blue.append_array(local_blue)
			evidence.append({
				"box":box,"eligible_pixels":local_red.size(),
				"median_delta_rgb_255":[median(local_red),median(local_green),median(local_blue)]
			})
	assert(red.size()>=100,"Insufficient clean skin samples; adjust sample_boxes rather than guessing")
	var measured: Array = [median(red),median(green),median(blue)]
	var applied: Array = record.get("bias_override_rgb",measured)
	return {
		"measured_delta_rgb_255":measured,"applied_delta_rgb_255":applied,
		"override_used":record.has("bias_override_rgb"),"eligible_pixels":red.size(),
		"sample_evidence":evidence,
		"scope":"One constant measured per approved portrait; no spatial color paint or modification to the original skin."
	}

func register_support(record: Dictionary, key: String) -> Dictionary:
	var source: String = path_in_pack(str(record.get("native","raw/"+key+"_native.png")))
	assert(FileAccess.file_exists(source),"Missing native extraction: "+source)
	var raw := read_image(source)
	var scale: float = float(record.get("scale",0.0))
	assert(scale>0.0 and scale<=8.0,"Supply a valid uniform scale from measured guides")
	var target := Vector2i(roundi(raw.get_width()*scale),roundi(raw.get_height()*scale))
	assert(target.x>0 and target.y>0 and target.x<=2048 and target.y<=3072,"Bad registered support dimensions")
	var offset := Vector2i(roundi(float(record.get("tx",0))),roundi(float(record.get("ty",0))))
	var clean := raw.duplicate() as Image
	clean.convert(Image.FORMAT_RGBAF)
	var removed: int = 0
	for y in raw.get_height():
		for x in raw.get_width():
			var color := raw.get_pixel(x,y)
			var alpha: int = roundi(color.a*255.0)
			if alpha<=4:
				if alpha>0:
					removed+=1
				clean.set_pixel(x,y,Color.TRANSPARENT)
			else:
				clean.set_pixel(x,y,Color(color.r*color.a,color.g*color.a,color.b*color.a,color.a))
	clean.resize(target.x,target.y,Image.INTERPOLATE_LANCZOS)
	for y in clean.get_height():
		for x in clean.get_width():
			var color := clean.get_pixel(x,y)
			if color.a<=4.0/255.0:
				clean.set_pixel(x,y,Color.TRANSPARENT)
			else:
				clean.set_pixel(x,y,Color(
					clampf(color.r/color.a,0.0,1.0),clampf(color.g/color.a,0.0,1.0),
					clampf(color.b/color.a,0.0,1.0),clampf(color.a,0.0,1.0)))
	clean.convert(Image.FORMAT_RGBA8)
	var canvas := Image.create_empty(CANVAS.x,CANVAS.y,false,Image.FORMAT_RGBA8)
	canvas.blit_rect(clean,Rect2i(Vector2i.ZERO,clean.get_size()),offset)
	var support_path: String = PACK+"raw/"+key+"_support_on_canvas.png"
	assert(canvas.save_png(support_path) == OK)
	return {
		"image":canvas,"report":{
			"native_source":source,"native_sha256":FileAccess.get_sha256(source),
			"native_size":[raw.get_width(),raw.get_height()],"uniform_scale":scale,
			"resized_size":[target.x,target.y],"integer_translation":[offset.x,offset.y],
			"removed_native_alpha1_to4_pixels":removed,"generated_support":support_path,
			"support_bbox":[canvas.get_used_rect().position.x,canvas.get_used_rect().position.y,
				canvas.get_used_rect().size.x,canvas.get_used_rect().size.y],
			"registration_guides":record.get("guides",[]),
			"method":"One whole-layer uniform Lanczos reduction in premultiplied alpha; integer placement. No local warp."
		}
	}

func in_exclusion(point: Vector2i, boxes: Array) -> bool:
	for box: Array in boxes:
		if rect_from_array(box).has_point(point):
			return true
	return false

func write_qa(key: String, beard: Image, approved: Image) -> void:
	for kind: String in ["light","dark","checker"]:
		var background := Image.create_empty(CANVAS.x,CANVAS.y,false,Image.FORMAT_RGBA8)
		background.fill(Color("#253136") if kind=="dark" else Color("#e8e1d6"))
		if kind=="checker":
			for y in range(0,CANVAS.y,24):
				for x in range(0,CANVAS.x,24):
					if (x/24+y/24)%2:
						background.fill_rect(Rect2i(x,y,24,24),Color("#aaa9a4"))
		var isolated := background.duplicate() as Image
		isolated.blend_rect(beard,Rect2i(Vector2i.ZERO,CANVAS),Vector2i.ZERO)
		var isolated_close := isolated.get_region(COMMON_QA_RECT)
		isolated_close.resize(COMMON_QA_RECT.size.x*4,COMMON_QA_RECT.size.y*4,Image.INTERPOLATE_NEAREST)
		assert(isolated_close.save_png(PACK+"qa/"+key+"_isolated_"+kind+".png") == OK)
		var result := background.duplicate() as Image
		for part: String in ["hair_back","clothing_back","face_shape","clothing","nose","eyes","eyebrows","mouth","face_accessory","hair_front"]:
			var layer: Image = beard if part=="face_accessory" else read_image(ACTIVE+"male/"+part+"/01.png")
			result.blend_rect(layer,Rect2i(Vector2i.ZERO,CANVAS),Vector2i.ZERO)
		assert(result.save_png(PACK+"qa/"+key+"_candidate_"+kind+".png") == OK)
		var close := result.get_region(COMMON_QA_RECT)
		close.resize(COMMON_QA_RECT.size.x*4,COMMON_QA_RECT.size.y*4,Image.INTERPOLATE_NEAREST)
		assert(close.save_png(PACK+"qa/"+key+"_beard_close_"+kind+".png") == OK)
	var original_close := approved.get_region(COMMON_QA_RECT)
	original_close.resize(COMMON_QA_RECT.size.x*4,COMMON_QA_RECT.size.y*4,Image.INTERPOLATE_NEAREST)
	assert(original_close.save_png(PACK+"qa/"+key+"_approved_close.png") == OK)

func import_one(record: Dictionary) -> Dictionary:
	var key: String = str(record.get("key",str(record.get("id","")).substr(0,2)))
	assert(SPECS.has(key),"Unknown design key: "+key)
	var spec: Dictionary = SPECS[key]
	var approved_path: String = path_in_pack(str(record.get("approved","reference/"+key+"_approved.png")))
	var clean_path: String = path_in_pack(str(record.get("clean_reference","reference/approved_no_beard.png")))
	if not FileAccess.file_exists(clean_path):
		clean_path = PACK+"reference/project_male_master.png"
	var approved := read_image(approved_path)
	var clean := read_image(clean_path)
	assert(approved.get_size()==CANVAS and clean.get_size()==CANVAS,"Reference canvas changed")
	var registered := register_support(record,key)
	var support: Image = registered.image
	var roi: Rect2i = rect_from_array(record.get("allowed_roi",spec.roi)).intersection(Rect2i(Vector2i.ZERO,CANVAS))
	assert(support.get_used_rect().intersection(roi).has_area(),"Generated support misses the beard ROI")
	var bias_report := estimate_bias(approved,clean,support,record)
	var bias_array: Array = bias_report.applied_delta_rgb_255
	var bias := Color(float(bias_array[0])/255.0,float(bias_array[1])/255.0,float(bias_array[2])/255.0,0.0)
	var dilation: int = int(record.get("dilation_px",3))
	assert(dilation>=0 and dilation<=8,"Review an unexpectedly large support dilation")
	var exclusions: Array = record.get("lip_exclusions",DEFAULT_LIP_EXCLUSIONS)
	var minimum_darkening: float = float(record.get("minimum_darkening_255",5.0))
	var foreground_y: float = float(record.get("foreground_luminance_255",80.0))/255.0
	var final := Image.create_empty(CANVAS.x,CANVAS.y,false,Image.FORMAT_RGBA8)
	var accepted: int = 0
	var supported: int = 0
	var excluded: int = 0
	var clamped_channels: int = 0
	var max_reconstruction_error: float = 0.0
	var reconstruction_error_sum: float = 0.0
	for y in range(roi.position.y,roi.end.y):
		for x in range(roi.position.x,roi.end.x):
			if not supported_at(support,x,y,dilation):
				continue
			supported+=1
			if in_exclusion(Vector2i(x,y),exclusions):
				excluded+=1
				continue
			var b := clean.get_pixel(x,y)+bias
			var c := approved.get_pixel(x,y)
			var difference: float = luminance(b)-luminance(c)
			if difference<=minimum_darkening/255.0:
				continue
			var alpha: float = clampf(difference/maxf(0.01,luminance(b)-foreground_y),0.0,1.0)
			if alpha<=4.0/255.0:
				continue
			var unbounded := Color(
				(c.r-(1.0-alpha)*b.r)/alpha,(c.g-(1.0-alpha)*b.g)/alpha,
				(c.b-(1.0-alpha)*b.b)/alpha,alpha)
			for channel: float in [unbounded.r,unbounded.g,unbounded.b]:
				if channel<0.0 or channel>1.0:
					clamped_channels+=1
			var pigment := Color(clampf(unbounded.r,0.0,1.0),clampf(unbounded.g,0.0,1.0),clampf(unbounded.b,0.0,1.0),alpha)
			final.set_pixel(x,y,pigment)
			var reconstructed := Color(pigment.r*alpha+b.r*(1.0-alpha),pigment.g*alpha+b.g*(1.0-alpha),pigment.b*alpha+b.b*(1.0-alpha),1.0)
			var error: float = maxf(absf(reconstructed.r-c.r),maxf(absf(reconstructed.g-c.g),absf(reconstructed.b-c.b)))*255.0
			max_reconstruction_error=maxf(max_reconstruction_error,error)
			reconstruction_error_sum+=error
			accepted+=1
	assert(accepted>0,"No observed approved beard pigment after matting: "+key)
	var output: String = PACK+"male/face_accessory/%02d.png" % int(spec.physical)
	assert(final.save_png(output) == OK)
	write_qa(key,final,approved)
	var used := final.get_used_rect()
	return {
		"key":key,"name":spec.name,"physical_index":spec.physical,"style_id":spec.style,
		"full_id":"male_beard_%02d" % int(spec.style),"output":output,
		"output_sha256":FileAccess.get_sha256(output),"canvas":[CANVAS.x,CANVAS.y],
		"runtime_offset":[0,0],"approved":approved_path,"approved_sha256":FileAccess.get_sha256(approved_path),
		"clean_reference":clean_path,"clean_sha256":FileAccess.get_sha256(clean_path),
		"registration":registered.report,"background_estimate":bias_report,
		"allowed_roi":[roi.position.x,roi.position.y,roi.size.x,roi.size.y],"lip_exclusions":exclusions,
		"support_dilation_px":dilation,"minimum_darkening_255":minimum_darkening,
		"foreground_luminance_reference_255":foreground_y*255.0,"supported_roi_pixels":supported,
		"excluded_lip_pixels":excluded,"accepted_source_pigment_pixels":accepted,
		"final_alpha_bbox":[used.position.x,used.position.y,used.size.x,used.size.y],
		"clamped_foreground_channels":clamped_channels,
		"source_reconstruction_error_255":{"max_channel":max_reconstruction_error,"mean_max_channel":reconstruction_error_sum/accepted,
			"scope":"Recovered foreground against the estimated source background, before PNG quantization. Not a whole-game image equality claim."},
		"method":"Image_gen support gates observed approved-source dark pigment. a=(Y(B)-Y(C))/(Y(B)-Y(F));F=(C-(1-a)B)/a. No procedural beard drawing.",
		"selected_source_canvas_coordinates_retained":true,"no_local_warp":true,
		"no_active_file_written":true,
		"limitations":"Skin background and opacity are estimates; actual composite and edge QA must be viewed before copying to the active package."
	}

func _initialize() -> void:
	for directory: String in ["raw","source","qa","male/face_accessory"]:
		assert(DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(PACK+directory)) == OK)
	var registration_path: String = PACK+"source/registration.json"
	assert(FileAccess.file_exists(registration_path),"Supply measured source/registration.json first")
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(registration_path))
	assert(parsed is Dictionary,"registration.json must be an object with records")
	var records: Array = parsed.get("records",[])
	assert(records.size()==4,"Expected four measured native beard registrations")
	var results: Array[Dictionary] = []
	var seen: Dictionary = {}
	for record: Dictionary in records:
		var key: String = str(record.get("key",str(record.get("id","")).substr(0,2)))
		assert(not seen.has(key),"Duplicate registration key: "+key)
		seen[key]=true
		results.append(import_one(record))
	var report: Dictionary = {
		"status":"four_candidate_layers_imported_for_visual_review",
		"source_mode":"built-in image_gen supports plus Godot source-foreground matting",
		"registration_file":registration_path,"records":results,
		"output_scope":"Only this install package; no active PNG, catalog, production code or user save writes.",
		"review":"Inspect each shallow/deep/checker composite and isolated layer before installation."
	}
	var file := FileAccess.open(PACK+"source/import_report.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	var summary: Array[Dictionary] = []
	for record: Dictionary in results:
		summary.append({"key":record.key,"sha256":record.output_sha256,"pixels":record.accepted_source_pigment_pixels})
	print(JSON.stringify({"imported":results.size(),"records":summary}))
	quit(0)

