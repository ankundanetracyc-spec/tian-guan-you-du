extends SceneTree

const PACK := "res://mods/appearance/male_beard_round2_install_20261108/"
const REVIEW := "res://mods/appearance/male_beard_round2_review_20261108/"
const DESIGNS := [
	{"key":"01","stem":"01_fine_moustache","name":"八字短髭","physical":3,"style":4,"crop":[435,435,160,135]},
	{"key":"02","stem":"02_short_chin_only","name":"短颏须","physical":4,"style":5,"crop":[445,490,145,105]},
	{"key":"03","stem":"03_separate_moustache_goatee","name":"细髭尖颏","physical":5,"style":6,"crop":[435,435,165,155]},
	{"key":"04","stem":"04_light_connected_beard","name":"薄络腮须","physical":6,"style":7,"crop":[400,400,230,185]}
]

func read_image(path: String) -> Image:
	var image := Image.new()
	assert(image.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK, "Cannot decode: "+path)
	image.convert(Image.FORMAT_RGBA8)
	return image

func copy_source(source: String, destination: String) -> void:
	var file := FileAccess.open(destination, FileAccess.WRITE)
	assert(file != null, "Cannot freeze: "+destination)
	file.store_buffer(FileAccess.get_file_as_bytes(source))
	file.close()

func _initialize() -> void:
	for folder in ["reference","source","raw","qa","male/face_accessory"]:
		assert(DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(PACK+folder)) == OK)
	copy_source(REVIEW+"reference/project_male_master.png",PACK+"reference/project_male_master.png")
	var records: Array[Dictionary] = []
	for spec: Dictionary in DESIGNS:
		var source: String = REVIEW+"preview/"+str(spec.stem)+".png"
		var frozen: String = PACK+"reference/"+str(spec.key)+"_approved.png"
		copy_source(source,frozen)
		var approved := read_image(frozen)
		assert(approved.get_size() == Vector2i(1024,1536),"Unexpected approved canvas")
		var box: Array = spec.crop
		var rect := Rect2i(int(box[0]),int(box[1]),int(box[2]),int(box[3]))
		var cropped := approved.get_region(rect)
		cropped.resize(rect.size.x*4,rect.size.y*4,Image.INTERPOLATE_NEAREST)
		var output: String = PACK+"reference/"+str(spec.key)+"_beard_close.png"
		assert(cropped.save_png(output) == OK)
		records.append({
			"key":spec.key,"name":spec.name,"physical_index":spec.physical,"style_id":spec.style,
			"approved":frozen,"approved_sha256":FileAccess.get_sha256(frozen),
			"crop_rect_original_pixels":box,"qa_zoom":4,"reference_crop":output,
			"crop_size":[cropped.get_width(),cropped.get_height()]
		})
	var report := {
		"purpose":"Source crops for built-in image_gen beard support and visual QA only. Nearest-neighbor enlargement; no creative pixels generated.",
		"clean_reference":PACK+"reference/project_male_master.png",
		"clean_reference_sha256":FileAccess.get_sha256(PACK+"reference/project_male_master.png"),
		"records":records
	}
	var file := FileAccess.open(PACK+"source/crop_manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	print(JSON.stringify({"cropped":records.size(),"reference_dir":PACK+"reference/"}))
	quit(0)

