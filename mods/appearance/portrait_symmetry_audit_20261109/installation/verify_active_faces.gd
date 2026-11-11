extends SceneTree
const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/portrait_symmetry_audit_20261109/"
const OUTPUT := PACK+"installation/"
const PRESET := "user://开局设定预设.cfg"
const PARTS := ["hair_front","hair_back","eyebrows","eyes","nose","mouth","face_shape","clothing","face_accessory"]
const CATALOG_PARTS := ["hair_front","hair_back","brow","eye","nose","mouth","face","clothes","beard"]
const ORDER := [1,6,7,4,3,2,5,8,0]
var config: Dictionary={}
var images: Dictionary={}
var checks: int=0
var failures: Array[String]=[]
var pack_infos: Array[Dictionary]=[]
var resolutions: Array[Dictionary]=[]
var cases: Array[Dictionary]=[]
var male_geometry: Array=[]
var before_hashes: Dictionary={}
var after_hashes: Dictionary={}
var preset_unchanged: bool=false
var female_bald_count: int=0
var female_hair_count: int=0
var male_count: int=0

func _initialize() -> void:
	call_deferred("verify")

func check(ok: bool, label: String) -> bool:
	checks += 1
	if not ok:
		failures.append(label)
		push_error(label)
	return ok

func json_file(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		check(false, "Required JSON exists: " + path)
		return {}
	var value: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	return value if value is Dictionary else {}

func component(part: int, female: bool = false) -> Dictionary:
	var gender: String = "female" if female else "male"
	var slot: String = "makeup" if female and part == 8 else CATALOG_PARTS[part]
	for entry: Dictionary in config.get("components", []):
		if str(entry.get("gender")) == gender and str(entry.get("part")) == slot:
			return entry
	return {}

func path_for(entry: Dictionary, index: int, rear: bool = false) -> String:
	var indices: Array = entry.get("variant_indices", [])
	var physical: int = int(indices[index]) if indices.size() > index else index + 1
	var template: String = str(entry.get("rear_file" if rear else "file", ""))
	return "" if template.is_empty() else ACTIVE + template.replace("{index}", "%02d" % physical)

func load_image(path: String) -> Image:
	if images.has(path):
		return images[path]
	var result: Image = Image.new()
	check(result.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK, "Decode PNG: " + path)
	result.convert(Image.FORMAT_RGBA8)
	images[path] = result
	return result

func fixture_data() -> Resource:
	var data: Resource = load("res://csharp/resources/HumanData.cs").new()
	data.set("gender", 0)
	data.set("appearance_seed", 314159)
	for field in ["appearance_face_style", "appearance_ear_style", "appearance_brow_style", "appearance_eye_style", "appearance_eyebrow_style", "appearance_nose_style", "appearance_mouth_style", "appearance_makeup_style", "appearance_hair_back_style", "appearance_hair_style", "appearance_clothes_style", "appearance_beard_style"]:
		data.set(field, 0)
	data.set("appearance_skin_tone", -1)
	data.set("appearance_hair_color", -1)
	return data

func ui_choice(scene: Node, part: int) -> OptionButton:
	return scene.find_child("Variant_"+PARTS[part],true,false)

func select(scene: Node,part: int,index: int) -> void:
	var control: OptionButton = ui_choice(scene,part)
	if control.selected != index:
		control.select(index)
		control.emit_signal("item_selected",index)

func set_gender(scene: Node,female: bool) -> void:
	var control: OptionButton = scene.find_child("GenderChoice",true,false)
	var index: int = 1 if female else 0
	control.select(index)
	control.emit_signal("item_selected",index)

func expected_image(scene: Node,female: bool=false) -> Image:
	var result: Image = Image.create_empty(1024,1536,false,Image.FORMAT_RGBA8)
	for part in ORDER:
		if part==6:
			var toggle: CheckButton=scene.find_child("Part_clothing",true,false)
			var rear: String=path_for(component(7,female),ui_choice(scene,7).selected,true)
			if toggle.button_pressed and not rear.is_empty():
				result.blend_rect(load_image(rear),Rect2i(0,0,1024,1536),Vector2i.ZERO)
		var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
		if toggle.button_pressed:
			result.blend_rect(load_image(path_for(component(part,female),ui_choice(scene,part).selected)),Rect2i(0,0,1024,1536),Vector2i.ZERO)
	return result

func verify_pixels(scene: Node,label: String,female: bool=false) -> void:
	var preview: TextureRect=scene.find_child("PortraitPreview",true,false)
	if not check(preview.texture != null,label+": preview exists"):
		return
	var actual: Image=preview.texture.get_image()
	actual.convert(Image.FORMAT_RGBA8)
	check(actual.get_size()==Vector2i(1024,1536) and actual.get_data()==expected_image(scene,female).get_data(),label+": exact selected independent layers")

func capture_ui(scene: Node,filename: String) -> void:
	var preview: TextureRect=scene.find_child("PortraitPreview",true,false)
	check(preview.texture.get_image().save_png(OUTPUT+filename+"_composite.png")==OK,"Save actual composite: "+filename)
	if DisplayServer.get_name()!="headless":
		await process_frame
		await RenderingServer.frame_post_draw
		check(root.get_texture().get_image().save_png(OUTPUT+filename+"_viewport.png")==OK,"Save actual UI: "+filename)


func scan_configs(path: String,result: Array[String]) -> void:
	var directory: DirAccess=DirAccess.open(path)
	if directory==null:return
	if directory.file_exists("appearance_parts.json"):result.append(path.path_join("appearance_parts.json"))
	for child in directory.get_directories():scan_configs(path.path_join(child),result)

func png_hashes(path: String,result: Dictionary) -> void:
	var directory: DirAccess=DirAccess.open(path)
	if directory==null:return
	for file in directory.get_files():
		if file.ends_with(".png"):result[path.path_join(file)]=FileAccess.get_sha256(path.path_join(file))
	for child in directory.get_directories():png_hashes(path.path_join(child),result)

func snapshot_inputs() -> Dictionary:
	var result: Dictionary={}
	png_hashes(ACTIVE+"male",result)
	png_hashes(ACTIVE+"female",result)
	var configs: Array[String]=[]
	scan_configs("res://mods/appearance",configs)
	for path in configs:result[path]=FileAccess.get_sha256(path)
	result[ACTIVE+"portrait_manifest.json"]=FileAccess.get_sha256(ACTIVE+"portrait_manifest.json")
	return result

func audit_catalog() -> void:
	var configs: Array[String]=[]
	scan_configs("res://mods/appearance",configs)
	var enabled: Array[String]=[]
	for path in configs:
		var value: Dictionary=json_file(path)
		var on: bool=bool(value.get("enabled",false))
		if on:enabled.append(path)
		pack_infos.append({"config":path,"sha256":FileAccess.get_sha256(path),"enabled":on,"package_id":str(value.get("package_id","")),"rig_id":str(value.get("rig_id",""))})
	check(enabled.size()==1 and enabled[0]==ACTIVE+"appearance_parts.json","Only the current installed appearance pack is enabled")
	var mod_catalog: Node=root.get_node("AppearanceModCatalog")
	var catalog: Node=root.get_node("PortraitCatalog")
	mod_catalog.call("reload")
	for female in [false,true]:
		var sex: String="female" if female else "male"
		var data: Resource=fixture_data()
		data.set("gender",1 if female else 0)
		var context: Dictionary=catalog.call("get_context",data,sex,25,"","凡人")
		for part in [2,3]:
			var entry: Dictionary=component(part,female)
			var loaded: Array=mod_catalog.call("get_parts",sex,CATALOG_PARTS[part])
			check(loaded.size()==int(entry.variant_count),"Live catalog has exact current "+sex+" "+CATALOG_PARTS[part]+" count")
			for current: Dictionary in loaded:
				check(str(current.get("package_path",""))+"/"==ACTIVE and str(current.get("resource_path","")).begins_with(ACTIVE+sex+"/"),"Live facial catalog entry belongs to current pack/gender")
			for choice in int(entry.variant_count):
				var style: int=int(entry.variant_style_ids[choice])
				for requested: String in [str(style),str(entry.id)+"_%02d"%style]:
					var selected: Dictionary=catalog.call("choose_part",context,CATALOG_PARTS[part],requested)
					var expected: String=path_for(entry,choice)
					check(str(selected.get("resource_path",""))==expected,"Explicit numeric/full savedID selects current facial texture")
					resolutions.append({"sex":sex,"part":CATALOG_PARTS[part],"requested":requested,"resolved_style":int(selected.get("variant_index",-1)),"resource_path":str(selected.get("resource_path","")),"name":str(selected.get("display_name",""))})
			for requested: Variant in entry.get("variant_aliases",{}):
				var selected: Dictionary=catalog.call("choose_part",context,CATALOG_PARTS[part],str(requested))
				var expected_style: int=int(entry.variant_aliases[requested])
				check(int(selected.get("variant_index",-1))==expected_style and str(selected.get("resource_path","")).begins_with(ACTIVE+sex+"/"),"Existing facial alias resolves to its configured retained style")
				resolutions.append({"sex":sex,"part":CATALOG_PARTS[part],"requested":str(requested),"kind":"retired_alias","resolved_style":int(selected.get("variant_index",-1)),"resource_path":str(selected.get("resource_path","")),"name":str(selected.get("display_name",""))})
	male_geometry=json_file(OUTPUT+"current_male_geometry.json").get("records",[])
	check(male_geometry.size()==6,"Independent read-only current male mirror proof covers6PNG")
	for row: Dictionary in male_geometry:
		check(FileAccess.get_sha256(str(row.resource_path))==str(row.current_sha256) and str(row.current_sha256)==str(row.selected_sha256),"Male current PNG SHA equals the installed selected mirror")
		check(int(row.mirror_different_RGBA_channels)==0 and int(row.axis_x)==513,"Actual male fullRGBA reflection remains exact about513; geometry only")

func selected_case(scene: Node,female: bool,label: String) -> Dictionary:
	var result: Dictionary={"label":label,"sex":"female" if female else "male","composite":OUTPUT+label+"_composite.png","viewport":OUTPUT+label+"_viewport.png","face_crop":OUTPUT+label+"_face_1x.png"}
	for part in [0,1,2,3]:
		var entry: Dictionary=component(part,female)
		var choice: int=ui_choice(scene,part).selected
		result[PARTS[part]]={"ui_index":choice,"physical_index":int(entry.variant_indices[choice]),"style_id":int(entry.variant_style_ids[choice]),"name":str(entry.variant_names[choice]),"resource_path":path_for(entry,choice)}
	return result

func capture_case(scene: Node,female: bool,label: String) -> Image:
	verify_pixels(scene,label,female)
	await capture_ui(scene,label)
	var preview: TextureRect=scene.find_child("PortraitPreview",true,false)
	var face: Image=preview.texture.get_image().get_region(Rect2i(350,245,330,325))
	check(face.save_png(OUTPUT+label+"_face_1x.png")==OK,"Actual native-size face crop saved")
	cases.append(selected_case(scene,female,label))
	return face

func set_hair_visible(scene: Node,on: bool) -> void:
	for part in [0,1]:
		var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
		toggle.button_pressed=on

func audit_actual_faces() -> void:
	var scene: Control=load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await process_frame
	var breathing: CheckButton=scene.find_child("BreathingPreview",true,false)
	breathing.button_pressed=false
	set_gender(scene,true)
	for part in 9:select(scene,part,0)
	check(ui_choice(scene,2).item_count==4 and ui_choice(scene,3).item_count==3,"Female actual UI has4 brows and3 eyes")
	var bald_contact: Image=Image.create_empty(1050,1400,false,Image.FORMAT_RGBA8)
	bald_contact.fill(Color(0.906,0.882,0.839))
	set_hair_visible(scene,false)
	for brow in 4:
		for eye in 3:
			select(scene,2,brow)
			select(scene,3,eye)
			var label: String="female_bald_b%02d_e%02d"%[int(component(2,true).variant_indices[brow]),int(component(3,true).variant_indices[eye])]
			var face: Image=await capture_case(scene,true,label)
			bald_contact.blit_rect(face,Rect2i(0,0,330,325),Vector2i(eye*350+10,brow*350+12))
			female_bald_count+=1
	check(bald_contact.save_png(OUTPUT+"female_actual_bald_4brow_3eye_contact_1x.png")==OK,"Actual female12case contact saved without face rescaling")
	set_hair_visible(scene,true)
	select(scene,2,0)
	var hair_contact: Image=Image.create_empty(1050,1050,false,Image.FORMAT_RGBA8)
	hair_contact.fill(Color(0.906,0.882,0.839))
	for hair in 3:
		select(scene,0,hair)
		select(scene,1,hair)
		for eye in 3:
			select(scene,3,eye)
			var label: String="female_hair_%02d_eye_%02d"%[int(component(0,true).variant_indices[hair]),int(component(3,true).variant_indices[eye])]
			var face: Image=await capture_case(scene,true,label)
			hair_contact.blit_rect(face,Rect2i(0,0,330,325),Vector2i(eye*350+10,hair*350+12))
			female_hair_count+=1
	check(hair_contact.save_png(OUTPUT+"female_actual_3hair_3eye_contact_1x.png")==OK,"Actual female hair/eye contact saved without face rescaling")
	set_gender(scene,false)
	for part in 9:select(scene,part,0)
	for choice in 3:
		for part in [0,1,2,3]:select(scene,part,choice)
		await capture_case(scene,false,"male_current_b%02d_e%02d"%[int(component(2).variant_indices[choice]),int(component(3).variant_indices[choice])])
		male_count+=1
	check(female_bald_count==12 and female_hair_count==9 and male_count==3,"Actual female12bald/9hair cases and3current male cases captured")
	scene.queue_free()
	await process_frame

func finish() -> void:
	var report: Dictionary={"schema":"active-portrait-read-only-audit/v1","pass":failures.is_empty(),"checks":checks,"failures":failures,"pack_configs":pack_infos,"facial_resolution_cases":resolutions,"male_actual_RGBA_mirror_facts":male_geometry,"female_bald_cases":female_bald_count,"female_hair_eye_cases":female_hair_count,"male_sample_cases":male_count,"actual_cases":cases,"input_hashes_before":before_hashes,"input_hashes_after":after_hashes,"all_input_PNG_config_hashes_unchanged":before_hashes==after_hashes,"user_preset_unchanged":preset_unchanged,"contacts":{"female_bald":{"file":OUTPUT+"female_actual_bald_4brow_3eye_contact_1x.png","rows":"brow physical01/02/03/04","columns":"eye physical01/02/03","native_face_pixels":[330,325]},"female_hair":{"file":OUTPUT+"female_actual_3hair_3eye_contact_1x.png","rows":"front+back matching physical01/02/03; fixed brow01","columns":"eye physical01/02/03","native_face_pixels":[330,325]}},"read_only_scope":"Only this audit package receives scripts, measured facts, actual screenshots and reports. Active PNG/config and user preset hashes checked before/after. No editor apply, creation, game-save or profile-save methods invoked.","aesthetic_status":"Female original painted shape/lighting and hair occlusion require actual visual review. Technical exact selected-layer loading is not an aesthetic approval. Male strict reflection is reported only as a current geometric fact."}
	var file: FileAccess=FileAccess.open(OUTPUT+"runtime_check.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	print("PORTRAIT_SYMMETRY_READ_ONLY_AUDIT_20261109: ",JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)

func verify() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	var preset_before: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	config=json_file(ACTIVE+"appearance_parts.json")
	before_hashes=snapshot_inputs()
	audit_catalog()
	await audit_actual_faces()
	after_hashes=snapshot_inputs()
	check(before_hashes==after_hashes,"Every current active PNG and discovered config/manifest byte hash unchanged")
	var preset_after: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	preset_unchanged=preset_before==preset_after
	check(preset_unchanged,"Existing user preset bytes unchanged")
	images.clear()
	finish()
