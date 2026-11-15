extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/male_face_single_side_mirror_20261109/"
const OUTPUT := PACK+"installation/"
const PRESET := "user://开局设定预设.cfg"
const PARTS := ["hair_front","hair_back","eyebrows","eyes","nose","mouth","face_shape","clothing","face_accessory"]
const CATALOG_PARTS := ["hair_front","hair_back","brow","eye","nose","mouth","face","clothes","beard"]
const ORDER := [1,6,7,4,3,2,5,8,0]
const MIRRORED := ["male/eyes/01.png","male/eyes/04.png","male/eyes/05.png","male/eyebrows/01.png","male/eyebrows/04.png","male/eyebrows/05.png"]
const STYLES := [0,3,4]
var config: Dictionary={}
var checks: int=0
var failures: Array[String]=[]
var images: Dictionary={}
var imports: Array[Dictionary]=[]
var mirror_geometry: Array[Dictionary]=[]
var records: Array[Dictionary]=[]
var captures: Array[String]=[]
var bald_cases: int=0
var whole_cases: int=0
var formal_cases: int=0
var preset_unchanged: bool=false

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

func same_integer_ids(actual: Array, expected: Array) -> bool:
	# JSON numbers can be floats while literal IDs are ints. Compare exact
	# numeric values per position, retaining length, order and type checks.
	if actual.size() != expected.size():
		return false
	for index in expected.size():
		var value: Variant = actual[index]
		if not (value is int or value is float):
			return false
		if float(value) != float(expected[index]):
			return false
	return true

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

func count_pngs(path: String) -> int:
	var directory: DirAccess=DirAccess.open(path)
	if directory==null:
		return 0
	var count: int=0
	for file in directory.get_files():
		if file.ends_with(".png"):
			count+=1
	for child in directory.get_directories():
		count+=count_pngs(path.path_join(child))
	return count


func verify_preservation() -> void:
	var baseline: Dictionary=json_file(PACK+"source/baseline.json")
	var old_pngs: int=0
	var retained: int=0
	var configs: int=0
	for row: Dictionary in baseline.get("files",[]):
		var file: String=str(row.get("file",""))
		var active: String=file if file.begins_with("res://") else "res://"+file
		var frozen: String=PACK+str(row.get("backup",""))
		check(FileAccess.file_exists(frozen) and FileAccess.get_sha256(frozen)==str(row.sha256),"Frozen baseline remains intact: "+file)
		var relative: String=active.trim_prefix(ACTIVE)
		if file.ends_with(".png"):
			old_pngs+=1
			if MIRRORED.has(relative):
				var candidate: String=PACK+relative
				if check(FileAccess.file_exists(candidate),"Mirror candidate exists: "+relative):
					var sha: String=FileAccess.get_sha256(candidate)
					check(FileAccess.get_sha256(active)==sha,"Actual installed mirror candidate: "+relative)
					records.append({"file":relative,"active":active,"candidate":candidate,"sha256":sha})
			else:
				retained+=1
				check(FileAccess.get_sha256(active)==str(row.sha256),"Other original PNG unchanged: "+relative)
		elif relative=="appearance_parts.json":
			configs+=1
			check(FileAccess.get_sha256(active)==str(row.sha256),"All option IDs/names/aliases and config bytes retained")
		elif relative=="portrait_manifest.json":
			configs+=1
			var before: Dictionary=json_file(frozen)
			var current: Dictionary=json_file(active)
			for key in ["canvas","sexes","slots","variants_per_slot","runtime_offset","rig"]:
				check(current.get(key)==before.get(key),"Manifest layout/rig retained: "+key)
	check(old_pngs==67 and retained==61 and configs==2 and records.size()==6,"Exactly6 selected mirror replacements and61 preserved PNGs; both configs frozen")
	check(count_pngs(ACTIVE+"male")+count_pngs(ACTIVE+"female")==67,"Still67 active PNGs")
	var choices: int=0
	for entry: Dictionary in config.get("components",[]):choices+=int(entry.get("variant_count",0))
	check(choices==60,"Still60 choices")
	for part in [2,3]:
		check(same_integer_ids(component(part).get("variant_style_ids",[]),STYLES),"Same0/3/4 saved IDs: "+CATALOG_PARTS[part])
		check(same_integer_ids(component(part).get("variant_indices",[]),[1,4,5]),"Same01/04/05 physical files: "+CATALOG_PARTS[part])

func verify_selected_mirror() -> void:
	var metadata: Dictionary=json_file(PACK+"source/selected_features.json")
	var seen: Dictionary={}
	for row: Dictionary in metadata.get("records",[]):
		var file: String=str(row.get("candidate",""))
		if not check(MIRRORED.has(file) and not seen.has(file),"Unique selected mirror file: "+file):continue
		seen[file]=true
		var source_path: String=PACK+str(row.get("source_ref",""))
		var side: String=str(row.get("side",""))
		if not check(side in ["left","right"] and float(row.get("axis_x",0))==513.0,"Explicit source side and fixed integer axis513: "+file):continue
		if not check(FileAccess.file_exists(source_path) and FileAccess.get_sha256(source_path)==str(row.get("source_sha256","")),"Frozen selected source SHA: "+file):continue
		check(FileAccess.get_sha256(PACK+file)==str(row.get("candidate_sha256","")),"Selection metadata binds exact mirror candidate: "+file)
		var source: Image=load_image(source_path)
		var candidate: Image=load_image(PACK+file)
		if not check(source.get_size()==Vector2i(1024,1536) and candidate.get_size()==source.get_size(),"Unscaled original source/candidate canvas: "+file):continue
		var raw: PackedByteArray=source.get_data()
		var actual: PackedByteArray=candidate.get_data()
		var domain_pixels: int=0
		var channel_differences: int=0
		var pixel_differences: int=0
		# Integer reflection aroundx513 mapsx to1026-x. The shared valid
		# canvas domain isx3..1023, including the fixed central column513.
		for y in 1536:
			for x in range(3,1024):
				var index: int=(y*1024+x)*4
				var reflected: int=(y*1024+1026-x)*4
				var changed: bool=false
				for channel in 4:
					if actual[index+channel]!=actual[reflected+channel]:
						channel_differences+=1
						changed=true
				if changed:pixel_differences+=1
				domain_pixels+=1
		var source_rect: Rect2i=Rect2i(0,0,513,1536) if side=="left" else Rect2i(514,0,510,1536)
		var source_retained: bool=source.get_region(source_rect).get_data()==candidate.get_region(source_rect).get_data()
		var center_retained: bool=source.get_region(Rect2i(513,0,1,1536)).get_data()==candidate.get_region(Rect2i(513,0,1,1536)).get_data()
		var source_unmapped_visible: int=0
		var candidate_unmapped_visible: int=0
		for y in 1536:
			for x in 3:
				var offset: int=(y*1024+x)*4+3
				if raw[offset]>0:source_unmapped_visible+=1
				if actual[offset]>0:candidate_unmapped_visible+=1
		check(channel_differences==0,"Strict row-by-row whole RGBA mirror onx3..1023: "+file)
		check(source_retained,"Complete chosen half RGBA unchanged; no resampling/repaint: "+file)
		check(center_retained,"Center column513 remains source RGBA without feather: "+file)
		check(source_unmapped_visible==0 and candidate_unmapped_visible==0,"Unmappable columns0..2 have Alpha0; no visible clipping: "+file)
		mirror_geometry.append({"file":file,"side":side,"axis_x":513,"mapping":"x_reflected=1026-x","valid_domain_x_inclusive":[3,1023],"pixels_compared":domain_pixels,"different_RGBA_channels":channel_differences,"different_RGBA_pixels":pixel_differences,"source_half_RGBA_unchanged":source_retained,"source_center_column_RGBA_unchanged":center_retained,"source_unmappable_visible_pixels":source_unmapped_visible,"candidate_unmappable_visible_pixels":candidate_unmapped_visible,"canvas":[1024,1536],"note":"Exact discrete mapping and source-pixel identity are geometric facts. Catchlight/sclera lighting are reflected with the selected eye and require normal whole-face review."})
	check(seen.size()==6 and mirror_geometry.size()==6,"All6 selected eyes/brows verified from their own frozen source sides")

func verify_import(row: Dictionary) -> void:
	var path: String=str(row.active)
	var texture: Texture2D=ResourceLoader.load(path,"Texture2D",ResourceLoader.CACHE_MODE_IGNORE)
	if not check(texture!=null,"Mirror feature imports: "+str(row.file)):return
	var raw: Image=load_image(path)
	var imported: Image=texture.get_image()
	imported.convert(Image.FORMAT_RGBA8)
	if not check(raw.get_size()==Vector2i(1024,1536) and imported.get_size()==raw.get_size(),"Original native canvas retained after import: "+str(row.file)):return
	var a: PackedByteArray=raw.get_data()
	var b: PackedByteArray=imported.get_data()
	var alpha_different: int=0
	var core_different: int=0
	var core_tested: int=0
	var edge_different: int=0
	for offset in range(3,a.size(),4):
		if a[offset]!=b[offset]:alpha_different+=1
		var changed: bool=a[offset-3]!=b[offset-3] or a[offset-2]!=b[offset-2] or a[offset-1]!=b[offset-1]
		if a[offset]>=230:
			core_tested+=1
			if changed:core_different+=1
		elif changed:edge_different+=1
	check(alpha_different==0 and core_different==0,"Imported Alpha and opaque-core RGB exact: "+str(row.file))
	imports.append({"file":row.file,"alpha_different_pixels":alpha_different,"core_tested_pixels":core_tested,"core_rgb_different_pixels":core_different,"low_alpha_rgb_different_pixels":edge_different,"note":"Existing fix_alpha_border may change low-alpha edge RGB. This check is import integrity, not an aesthetic or symmetry result."})

func capture_case(scene: Node,filename: String) -> void:
	verify_pixels(scene,filename)
	await capture_ui(scene,filename)
	captures.append(filename+"_composite.png")
	if DisplayServer.get_name()!="headless":captures.append(filename+"_viewport.png")
	if filename=="game_mirror_whole_04":
		var preview: TextureRect=scene.find_child("PortraitPreview",true,false)
		var face: Image=preview.texture.get_image().get_region(Rect2i(350,245,330,325))
		check(face.save_png(OUTPUT+"game_mirror_whole_04_face_1x.png")==OK,"Save actual mirrored04 face crop without resizing")
		captures.append("game_mirror_whole_04_face_1x.png")

func verify_ui_and_renderer() -> void:
	var scene: Control=load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await process_frame
	var breathing: CheckButton=scene.find_child("BreathingPreview",true,false)
	breathing.button_pressed=false
	set_gender(scene,false)
	for part in 9:select(scene,part,0)
	check(ui_choice(scene,2).item_count==3 and ui_choice(scene,3).item_count==3,"All3 mirror brow and eye choices visible")
	for part in [0,1]:
		var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
		toggle.button_pressed=false
	for brow in 3:
		for eye in 3:
			select(scene,2,brow)
			select(scene,3,eye)
			await capture_case(scene,"game_mirror_bald_b%02d_e%02d"%[int(component(2).variant_indices[brow]),int(component(3).variant_indices[eye])])
			bald_cases+=1
	for part in [0,1]:
		var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
		toggle.button_pressed=true
	for choice in 3:
		for part in [0,1,2,3]:select(scene,part,choice)
		await capture_case(scene,"game_mirror_whole_%02d"%int(component(3).variant_indices[choice]))
		whole_cases+=1
	check(bald_cases==9 and whole_cases==3,"Nine actual bald mixes and3 complete matching mirror hairstyles captured")
	# Reuse the production renderer for a small direct layer/loading smoke check.
	# Existing editor/save workflows were verified previously; do not repeat them.
	scene.visible=false
	var background: ColorRect=ColorRect.new()
	background.color=Color(0.906,0.882,0.839)
	background.position=Vector2(48,40)
	background.size=Vector2(544,800)
	root.add_child(background)
	var renderer: Control=load("res://csharp/panel/PortraitRenderer.cs").new()
	renderer.position=Vector2(64,56)
	renderer.size=Vector2(512,768)
	root.add_child(renderer)
	var catalog: Node=root.get_node("PortraitCatalog")
	for choice in 3:
		var data: Resource=fixture_data()
		for field in ["appearance_hair_style","appearance_hair_back_style","appearance_brow_style","appearance_eye_style"]:data.set(field,int(STYLES[choice]))
		var context: Dictionary=catalog.call("get_context",data,"male",25,"","凡人")
		renderer.call("configure",data,context,catalog.call("get_requested_appearance",data))
		var composition: Node=renderer.find_child("PortraitComposition",true,false)
		if check(composition!=null,"Production renderer mirror portrait exists"):
			for part in [2,3]:
				var layer: TextureRect=composition.get_node(CATALOG_PARTS[part].capitalize())
				check(layer.texture!=null and layer.texture.resource_path==path_for(component(part),choice),"Renderer uses actual single-side-mirrored "+CATALOG_PARTS[part])
				check(layer.position==Vector2.ZERO and layer.scale==Vector2.ONE and is_zero_approx(layer.rotation),"Renderer adds no feature offset/rotation/scale")
				check(layer.size==Vector2(1024,1536) and layer.modulate==Color.WHITE,"Renderer retains original feature canvas/color")
		if DisplayServer.get_name()!="headless":
			await process_frame
			await RenderingServer.frame_post_draw
			var name: String="renderer_mirror_whole_%02d_viewport.png"%int(component(3).variant_indices[choice])
			check(root.get_texture().get_image().save_png(OUTPUT+name)==OK,"Save actual production renderer whole portrait")
			captures.append(name)
		formal_cases+=1
	renderer.queue_free()
	background.queue_free()
	scene.queue_free()
	await process_frame

func finish() -> void:
	var report: Dictionary={"schema":"male-single-side-mirror-runtime/v1","pass":failures.is_empty(),"checks":checks,"failures":failures,"selected_records":records,"mirrored_png_count":6,"mirror_geometry":mirror_geometry,"retained_pngs":61,"active_png_count":67,"active_choice_count":60,"bald_combinations_captured":bald_cases,"matching_whole_portraits_captured":whole_cases,"production_renderer_cases":formal_cases,"imported_textures":imports,"captures":captures,"user_preset_unchanged":preset_unchanged,"scope":"Six selected natural eye/brow layers mirrored by exact integer RGBA index mapping aboutx513. Existing IDs/config and other61 PNGs preserved. Actual9bald mixes/3matching whole portraits/04native face crop plus3formal renderer loading cases. No editor/save workflow rerun; no user data writes.","aesthetic_status":"Not determined by this fixture. Exact full-RGBA reflection is reported only as a geometric fact. It does not approve expression, catchlight direction or style. Actual normal-scale whole-face images require user review."}
	var file: FileAccess=FileAccess.open(OUTPUT+"runtime_check.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	print("MALE_FACE_SINGLE_SIDE_MIRROR_20261109: ",JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)

func verify() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	var preset_before: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	config=json_file(ACTIVE+"appearance_parts.json")
	verify_preservation()
	if records.size()!=6:
		finish()
		return
	verify_selected_mirror()
	for row: Dictionary in records:verify_import(row)
	root.get_node("AppearanceModCatalog").call("reload")
	await verify_ui_and_renderer()
	var preset_after: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	preset_unchanged=preset_before==preset_after
	check(preset_unchanged,"Existing user preset bytes remain unchanged")
	images.clear()
	finish()
