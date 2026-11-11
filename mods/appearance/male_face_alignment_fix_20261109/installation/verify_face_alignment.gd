extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/male_face_alignment_fix_20261109/"
const OUTPUT := PACK+"installation/"
const PRESET := "user://开局设定预设.cfg"
const PARTS := ["hair_front","hair_back","eyebrows","eyes","nose","mouth","face_shape","clothing","face_accessory"]
const CATALOG_PARTS := ["hair_front","hair_back","brow","eye","nose","mouth","face","clothes","beard"]
const ORDER := [1,6,7,4,3,2,5,8,0]
const FEATURE_PARTS := [2,3,4,5]
const REPAIR_FILES := ["male/eyes/01.png","male/eyes/04.png","male/eyes/05.png","male/eyebrows/01.png","male/eyebrows/04.png","male/eyebrows/05.png","male/nose/01.png","male/mouth/01.png","male/face_accessory/02.png","male/face_accessory/03.png","male/face_accessory/04.png","male/face_accessory/05.png","male/face_accessory/06.png"]
const FEATURE_STYLES := [0,3,4]
var config: Dictionary={}
var baseline: Dictionary={}
var frozen_config: Dictionary={}
var checks: int=0
var failures: Array[String]=[]
var images: Dictionary={}
var frozen_paths: Dictionary={}
var repair_records: Array[Dictionary]=[]
var imports: Array[Dictionary]=[]
var male_combinations: int=0
var female_combinations: int=0
var bald_captures: int=0
var front_captures: int=0
var collar_cases: int=0
var beard_cases: int=0
var editor_cases: int=0
var creator_cases: int=0
var metadata_hash_records: int=0
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

func selections(scene: Node) -> Array[int]:
	var result: Array[int] = []
	for part in 9:
		result.append(ui_choice(scene,part).selected)
	return result

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

func capture_viewport(filename: String) -> void:
	if DisplayServer.get_name()!="headless":
		await process_frame
		await RenderingServer.frame_post_draw
		check(root.get_texture().get_image().save_png(OUTPUT+filename+"_viewport.png")==OK,"Save actual viewport: "+filename)

func button_by_text(parent: Node,text: String) -> Button:
	if parent is Button and str(parent.text)==text:
		return parent
	for child in parent.get_children():
		var found: Button=button_by_text(child,text)
		if found!=null:
			return found
	return null

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

func live_choice_row(parent: Node,title: String) -> Node:
	if parent.is_queued_for_deletion():
		return null
	var has_title: bool=false
	for child in parent.get_children():
		if child is Label and str(child.text)==title:
			has_title=true
	if has_title and parent.get_node_or_null("Previous") is Button and parent.get_node_or_null("Next") is Button:
		return parent
	for child in parent.get_children():
		var row: Node=live_choice_row(child,title)
		if row!=null:return row
	return null

func choice_label(row: Node,title: String) -> String:
	if row==null:return ""
	for child in row.get_children():
		if child is Label and str(child.text)!=title:return str(child.text)
	return ""

func choice_name(row: Node,title: String) -> String:
	# Current UI captions are '<configured name>  <index> / <count>'.
	# Strip only that numeric suffix; a name contained inside an older name
	# must not select the older style (半束长披 vs 木簪半束长披).
	var text: String=choice_label(row,title).strip_edges()
	var separator: int=text.rfind("  ")
	if separator<0:return text
	var suffix: PackedStringArray=text.substr(separator+2).split("/")
	if suffix.size()==2 and suffix[0].strip_edges().is_valid_int() and suffix[1].strip_edges().is_valid_int():
		return text.substr(0,separator).strip_edges()
	return text

func name_for_style(entry: Dictionary,style: int) -> String:
	var styles: Array=entry.get("variant_style_ids",[])
	var names: Array=entry.get("variant_names",[])
	for index in styles.size():
		if int(styles[index])==style and index<names.size():
			return str(names[index])
	return ""

func choose_editor_name(editor: Node,title: String,name: String) -> bool:
	var row: Node=live_choice_row(editor,title)
	if row==null:return false
	var next: Button=row.get_node("Next")
	for attempt in 7:
		if choice_name(row,title)==name:return true
		next.emit_signal("pressed")
	return choice_name(row,title)==name

func find_component_selector(parent: Node,part: int) -> OptionButton:
	if parent==null or parent.is_queued_for_deletion():return null
	if parent is OptionButton:
		var names: Array=component(part).get("variant_names",[])
		var found: int=0
		for name: Variant in names:
			for index in parent.item_count:
				if str(parent.get_item_text(index))==str(name):
					found+=1
					break
		if found==names.size():return parent
	for child in parent.get_children():
		var result: OptionButton=find_component_selector(child,part)
		if result!=null:return result
	return null

func pick_selector_name(selector: OptionButton,name: String) -> bool:
	for index in selector.item_count:
		if selector.get_item_text(index)==name:
			selector.select(index)
			selector.emit_signal("item_selected",index)
			return true
	return false


func verify_baseline_and_install() -> void:
	var old_pngs: int=0
	var retained: int=0
	var expected_configs: int=0
	check(int(baseline.get("active_png_count",0))==67,"Frozen baseline states67 existing PNGs")
	for record: Dictionary in baseline.get("files",[]):
		var file: String=str(record.get("file",""))
		var active_path: String=file if file.begins_with("res://") else "res://"+file
		var frozen_path: String=PACK+str(record.get("backup",""))
		check(FileAccess.file_exists(frozen_path) and FileAccess.get_sha256(frozen_path)==str(record.get("sha256","")),"Frozen source intact: "+file)
		if file.ends_with(".png"):
			old_pngs+=1
			frozen_paths[active_path]=frozen_path
			var relative: String=active_path.trim_prefix(ACTIVE)
			if REPAIR_FILES.has(relative):
				var candidate: String=PACK+relative
				if check(FileAccess.file_exists(candidate),"Repair candidate exists: "+relative):
					var candidate_sha: String=FileAccess.get_sha256(candidate)
					check(FileAccess.get_sha256(active_path)==candidate_sha,"Installed bytes match exact repair candidate: "+relative)
					repair_records.append({"file":relative,"active":active_path,"candidate":candidate,"sha256":candidate_sha,"before_sha256":str(record.sha256),"same_bytes_as_before":candidate_sha==str(record.sha256)})
			else:
				retained+=1
				check(FileAccess.get_sha256(active_path)==str(record.sha256),"Unchanged retained PNG: "+relative)
		elif file.ends_with("appearance_parts.json"):
			expected_configs+=1
			check(FileAccess.get_sha256(active_path)==str(record.sha256),"appearance_parts.json byte-identical")
			frozen_config=json_file(frozen_path)
		elif file.ends_with("portrait_manifest.json"):
			expected_configs+=1
			var before: Dictionary=json_file(frozen_path)
			var current: Dictionary=json_file(active_path)
			for key in ["canvas","sexes","slots","variants_per_slot","runtime_offset","rig"]:
				check(current.get(key)==before.get(key),"Manifest non-geometry layout metadata retained: "+key)
	check(old_pngs==67 and expected_configs==2,"Baseline covers67 PNGs plus both configurations")
	check(retained==54 and repair_records.size()==13,"Exactly13 repaired files and54 byte-preserved PNGs, including original empty beard01")
	check(count_pngs(ACTIVE+"male")+count_pngs(ACTIVE+"female")==67,"Active67 PNG count unchanged")
	check(not frozen_config.is_empty() and config==frozen_config,"All original option data and aliases retained")
	var count: int=0
	for entry: Dictionary in config.get("components",[]):
		count+=int(entry.get("variant_count",0))
	check(count==60,"Active60 option count unchanged")
	check(int(component(0).get("variant_count",0))==7 and int(component(1).get("variant_count",0))==7,"All7front/7rear hair choices retained")
	check(int(component(8).get("variant_count",0))==6,"All6 beard choices retained")
	for part in [2,3]:
		check(same_integer_ids(component(part).get("variant_style_ids",[]),FEATURE_STYLES),"Original0/3/4 style IDs retained for "+CATALOG_PARTS[part])
		check(same_integer_ids(component(part).get("variant_indices",[]),[1,4,5]),"Original01/04/05 physical files retained for "+CATALOG_PARTS[part])
	for part in [4,5]:
		check(same_integer_ids(component(part).get("variant_style_ids",[]),[0]),"Original single style0 retained for "+CATALOG_PARTS[part])
		check(same_integer_ids(component(part).get("variant_indices",[]),[1]),"Original physical01 retained for "+CATALOG_PARTS[part])

func verify_import(record: Dictionary) -> void:
	var path: String=str(record.active)
	var texture: Texture2D=ResourceLoader.load(path,"Texture2D",ResourceLoader.CACHE_MODE_IGNORE)
	if not check(texture!=null,"Repair imports as Texture2D: "+str(record.file)):
		return
	var source: Image=load_image(path)
	var imported: Image=texture.get_image()
	imported.convert(Image.FORMAT_RGBA8)
	if not check(source.get_size()==Vector2i(1024,1536) and imported.get_size()==source.get_size(),"Repair/import shared canvas: "+str(record.file)):
		return
	check(not source.is_invisible(),"Repair has visible pixels: "+str(record.file))
	var actual: PackedByteArray=imported.get_data()
	var expected: PackedByteArray=source.get_data()
	var alpha_different: int=0
	var core_different: int=0
	var core_tested: int=0
	var edge_rgb_different: int=0
	for offset in range(3,expected.size(),4):
		if actual[offset]!=expected[offset]:alpha_different+=1
		var changed: bool=actual[offset-3]!=expected[offset-3] or actual[offset-2]!=expected[offset-2] or actual[offset-1]!=expected[offset-1]
		if expected[offset]>=230:
			core_tested+=1
			if changed:core_different+=1
		elif changed:edge_rgb_different+=1
	check(alpha_different==0 and core_different==0,"Imported alpha and opaque-core color retained: "+str(record.file))
	imports.append({"file":record.file,"alpha_different_pixels":alpha_different,"core_tested_pixels":core_tested,"core_rgb_different_pixels":core_different,"low_alpha_rgb_different_pixels":edge_rgb_different,"note":"Import-integrity check only. Low-alpha edge RGB can change under existing Godot fix_alpha_border. Does not establish anatomic balance or symmetry."})

func verify_metadata_hashes() -> void:
	# This optional existing report is provenance only; none of its geometric
	# quantities is used as a mirror/symmetry acceptance assertion.
	var path: String=PACK+"source/nose_mouth_centering.json"
	if not FileAccess.file_exists(path):return
	var metadata: Dictionary=json_file(path)
	for record: Dictionary in metadata.get("records",[]):
		var relative: String=str(record.get("target",""))
		if not REPAIR_FILES.has(relative):continue
		check(FileAccess.get_sha256(PACK+relative)==str(record.get("candidate_sha256","")),"Geometry metadata refers to actual candidate bytes: "+relative)
		check(FileAccess.get_sha256(PACK+str(record.get("source","")))==str(record.get("source_sha256","")),"Geometry metadata frozen source is intact: "+relative)
		metadata_hash_records+=1

func expected_feature_path(part: int,choice: int) -> String:
	return path_for(component(part),choice)

func check_formal_layers(composition: Node,brow: int,eye: int,label: String) -> void:
	for part in FEATURE_PARTS:
		var choice: int=brow if part==2 else eye if part==3 else 0
		var layer: TextureRect=composition.get_node(CATALOG_PARTS[part].capitalize())
		if not check(layer!=null and layer.texture!=null,label+": "+CATALOG_PARTS[part]+" layer exists"):
			continue
		check(layer.texture.resource_path==expected_feature_path(part,choice) and layer.visible,label+": selected "+CATALOG_PARTS[part]+" texture")
		check(layer.position==Vector2.ZERO and layer.scale==Vector2.ONE and is_zero_approx(layer.rotation),label+": no per-layer geometry change for "+CATALOG_PARTS[part])
		check(layer.size==Vector2(1024,1536) and layer.modulate==Color.WHITE,label+": unchanged canvas and color for "+CATALOG_PARTS[part])

func verify_catalog_and_renderer() -> void:
	var catalog: Node=root.get_node("PortraitCatalog")
	var data: Resource=fixture_data()
	data.set("appearance_skin_tone",4)
	data.set("appearance_hair_color",4)
	var context: Dictionary=catalog.call("get_context",data,"male",25,"","凡人")
	for part in FEATURE_PARTS:
		var entry: Dictionary=component(part)
		for choice in int(entry.get("variant_count",0)):
			var style: int=int(entry.variant_style_ids[choice])
			var full_id: String=str(entry.id)+"_%02d"%style
			for requested: String in [str(style),full_id]:
				var selected: Dictionary=catalog.call("choose_part",context,CATALOG_PARTS[part],requested)
				check(str(selected.get("resource_path",""))==path_for(entry,choice),"Original numeric/fullID selects correct repaired file: "+CATALOG_PARTS[part]+"/"+requested)
		for alias: Variant in entry.get("variant_aliases",{}):
			var selected: Dictionary=catalog.call("choose_part",context,CATALOG_PARTS[part],str(alias))
			check(int(selected.get("variant_index",-1))==int(entry.variant_aliases[alias]),"Existing feature alias retained: "+CATALOG_PARTS[part]+"/"+str(alias))
	var renderer: Control=load("res://csharp/panel/PortraitRenderer.cs").new()
	renderer.size=Vector2(1024,1536)
	root.add_child(renderer)
	for brow in 3:
		for eye in 3:
			for front in 7:
				data.set("appearance_brow_style",int(component(2).variant_style_ids[brow]))
				data.set("appearance_eye_style",int(component(3).variant_style_ids[eye]))
				data.set("appearance_hair_style",int(component(0).variant_style_ids[front]))
				var requested: Dictionary=catalog.call("get_requested_appearance",data)
				renderer.call("configure",data,context,requested)
				var composition: Node=renderer.find_child("PortraitComposition",true,false)
				if check(composition!=null,"Formal composition exists"):
					check_formal_layers(composition,brow,eye,"Formal brow%d/eye%d/front%d"%[brow,eye,front])
					var face: TextureRect=composition.get_node("Face")
					check(face.texture!=null and face.texture.resource_path==ACTIVE+"male/face_shape/01.png" and face.modulate==Color.WHITE,"Formal fixed face/skin preserved")
	renderer.call("clear_portrait")
	renderer.queue_free()

func reset_choices(scene: Node,female: bool=false) -> void:
	set_gender(scene,female)
	for part in 9:select(scene,part,0)

func frozen_before_image(scene: Node,female: bool=false) -> Image:
	var result: Image=Image.create_empty(1024,1536,false,Image.FORMAT_RGBA8)
	for part in ORDER:
		if part==6:
			var clothing_toggle: CheckButton=scene.find_child("Part_clothing",true,false)
			var rear: String=path_for(component(7,female),ui_choice(scene,7).selected,true)
			if clothing_toggle.button_pressed and not rear.is_empty():
				result.blend_rect(load_image(str(frozen_paths.get(rear,rear))),Rect2i(0,0,1024,1536),Vector2i.ZERO)
		var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
		if toggle.button_pressed:
			var active_path: String=path_for(component(part,female),ui_choice(scene,part).selected)
			result.blend_rect(load_image(str(frozen_paths.get(active_path,active_path))),Rect2i(0,0,1024,1536),Vector2i.ZERO)
	return result

func capture_with_frozen_reference(scene: Node,filename: String) -> void:
	check(frozen_before_image(scene).save_png(OUTPUT+"frozen_raw_before_"+filename+".png")==OK,"Save frozen raw-before diagnostic: "+filename)
	await capture_ui(scene,filename)

func verify_ui() -> void:
	var scene: Control=load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await process_frame
	var breathing: CheckButton=scene.find_child("BreathingPreview",true,false)
	breathing.button_pressed=false
	reset_choices(scene)
	check(ui_choice(scene,0).item_count==7 and ui_choice(scene,1).item_count==7,"All7front/7rear choices visible")
	check(ui_choice(scene,2).item_count==3 and ui_choice(scene,3).item_count==3,"Original three brow/eye options visible")
	for front in 7:
		for back in 7:
			for brow in 3:
				for eye in 3:
					select(scene,0,front)
					select(scene,1,back)
					select(scene,2,brow)
					select(scene,3,eye)
					verify_pixels(scene,"Male front%d/back%d/brow%d/eye%d"%[front,back,brow,eye])
					male_combinations+=1
	check(male_combinations==441,"All441 current male exact-layer UI combinations")
	reset_choices(scene)
	for part in FEATURE_PARTS:
		for choice in int(component(part).get("variant_count",0)):
			select(scene,part,choice)
			var selection: Array[int]=selections(scene)
			var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
			toggle.button_pressed=false
			verify_pixels(scene,"Independently hide repaired "+PARTS[part]+" choice%d"%choice)
			toggle.button_pressed=true
			check(selections(scene)==selection,"Repaired feature visibility retains choices")
			verify_pixels(scene,"Re-show repaired "+PARTS[part]+" choice%d"%choice)
		select(scene,part,0)
	# These saved current UI outputs are for visual review, not mirror assertions.
	reset_choices(scene)
	for part in [0,1]:
		var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
		toggle.button_pressed=false
	for brow in 3:
		for eye in 3:
			select(scene,2,brow)
			select(scene,3,eye)
			verify_pixels(scene,"Bald brow%d/eye%d"%[brow,eye])
			await capture_with_frozen_reference(scene,"game_bald_b%02d_e%02d"%[int(component(2).variant_style_ids[brow]),int(component(3).variant_style_ids[eye])])
			bald_captures+=1
	for part in [0,1]:
		var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
		toggle.button_pressed=true
	reset_choices(scene)
	for front in 7:
		select(scene,0,front)
		verify_pixels(scene,"Repaired baseline with front choice%d"%front)
		await capture_with_frozen_reference(scene,"game_front_%02d"%int(component(0).variant_style_ids[front]))
		front_captures+=1
	check(bald_captures==9 and front_captures==7,"Actual9 bald combinations and7front overlays captured")
	select(scene,0,0)
	for brow in 3:
		for eye in 3:
			select(scene,2,brow)
			select(scene,3,eye)
			for clothing in 7:
				select(scene,7,clothing)
				verify_pixels(scene,"Repaired brow%d/eye%d with collar%d"%[brow,eye,clothing])
				collar_cases+=1
			select(scene,7,0)
			for beard in 6:
				select(scene,8,beard)
				verify_pixels(scene,"Repaired brow%d/eye%d with beard%d"%[brow,eye,beard])
				beard_cases+=1
			select(scene,8,0)
	check(collar_cases==63 and beard_cases==54,"All9 brow/eye pairs checked with7collars and6beards")
	await capture_ui(scene,"game_repaired_feature_final")
	var male_saved: Array[int]=selections(scene)
	reset_choices(scene,true)
	for front in 3:
		for back in 3:
			for brow in 4:
				for eye in 3:
					select(scene,0,front)
					select(scene,1,back)
					select(scene,2,brow)
					select(scene,3,eye)
					verify_pixels(scene,"Preserved female front%d/back%d/brow%d/eye%d"%[front,back,brow,eye],true)
					female_combinations+=1
	check(female_combinations==108,"All108 original female exact-layer UI combinations preserved")
	var female_saved: Array[int]=selections(scene)
	await capture_ui(scene,"game_female_preserved")
	set_gender(scene,false)
	check(selections(scene)==male_saved,"Male corrected-feature selections survive gender switch")
	set_gender(scene,true)
	check(selections(scene)==female_saved,"Female selections survive gender switch")
	set_gender(scene,false)
	scene.find_child("ResetVariants",true,false).emit_signal("pressed")
	check(selections(scene)==[0,0,0,0,0,0,0,0,0],"Restore-current retains original selection indices")
	verify_pixels(scene,"Reset corrected baseline")
	scene.queue_free()
	await process_frame

func verify_editor() -> void:
	var editor: Control=load("res://csharp/panel/AppearanceEditor.cs").new()
	root.add_child(editor)
	await process_frame
	var apply: Button=button_by_text(editor,"应用")
	if not check(apply!=null,"Actual editor Apply exists"):
		editor.queue_free()
		return
	for brow in 3:
		for eye in 3:
			var data: Resource=fixture_data()
			editor.call("open_for_data",data,25)
			await process_frame
			var brow_name: String=str(component(2).variant_names[brow])
			var eye_name: String=str(component(3).variant_names[eye])
			check(choose_editor_name(editor,"眉型",brow_name),"Actual editor selects "+brow_name)
			check(choose_editor_name(editor,"眉眼组合",eye_name),"Actual editor selects "+eye_name)
			check(int(data.get("appearance_brow_style"))==0 and int(data.get("appearance_eye_style"))==0,"Draft remains separate until Apply")
			var composition: Node=editor.find_child("PortraitComposition",true,false)
			if check(composition!=null,"Actual editor portrait exists"):
				check_formal_layers(composition,brow,eye,"Editor brow%d/eye%d"%[brow,eye])
			for item in [{"part":4,"title":"鼻型"},{"part":5,"title":"口型"}]:
				var row: Node=live_choice_row(editor,str(item.title))
				check(row!=null and choice_name(row,str(item.title))==str(component(int(item.part)).variant_names[0]),"Original single-choice nose/mouth labels retained")
			if brow==eye:
				await capture_viewport("editor_feature_%02d"%int(component(2).variant_style_ids[brow]))
			apply.emit_signal("pressed")
			check(int(data.get("appearance_brow_style"))==int(component(2).variant_style_ids[brow]) and int(data.get("appearance_eye_style"))==int(component(3).variant_style_ids[eye]),"Apply preserves original0/3/4 saved feature styles")
			check(int(data.get("appearance_nose_style"))==0 and int(data.get("appearance_mouth_style"))==0 and int(data.get("appearance_face_style"))==0,"Apply keeps nose/mouth/face on original style0")
			editor.call("open_for_data",data,25)
			await process_frame
			check(choice_name(live_choice_row(editor,"眉型"),"眉型")==brow_name and choice_name(live_choice_row(editor,"眉眼组合"),"眉眼组合")==eye_name,"Reopening retains exact independent brow/eye names")
			editor_cases+=1
	editor.queue_free()
	await process_frame

func verify_creator() -> void:
	var original: Node=root.get_node("GameStartProfile")
	root.remove_child(original)
	var profile: Node=load("res://csharp/autoload/GameStartProfile.cs").new()
	profile.name="GameStartProfile"
	root.add_child(profile)
	profile.call("capture_player_appearance",fixture_data())
	profile.call("roll_fated_aptitude")
	var menu: Control=load("res://tscn/scene/StartMenu.tscn").instantiate()
	root.add_child(menu)
	await process_frame
	var new_game: Button=menu.find_child("NewSave",true,false)
	if check(new_game!=null,"Actual NewSave button exists"):
		new_game.emit_signal("pressed")
		await process_frame
		var overlay: Node=menu.find_child("NewGameSetup",true,false)
		var brow_selector: OptionButton=find_component_selector(overlay,2)
		var eye_selector: OptionButton=find_component_selector(overlay,3)
		if check(brow_selector!=null and eye_selector!=null,"Actual creator brow/eye dropdowns exist"):
			check(brow_selector.item_count==4 and eye_selector.item_count==4,"Creation keeps random plus3 original options")
			for brow in 3:
				for eye in 3:
					check(pick_selector_name(brow_selector,str(component(2).variant_names[brow])),"Creator selects exact original brow name")
					check(pick_selector_name(eye_selector,str(component(3).variant_names[eye])),"Creator selects exact original eye name")
					var preview_data: Resource=profile.call("create_player_appearance_preview")
					check(int(preview_data.get("appearance_brow_style"))==int(component(2).variant_style_ids[brow]) and int(preview_data.get("appearance_eye_style"))==int(component(3).variant_style_ids[eye]),"Creator profile stores original0/3/4 feature IDs")
					var portrait: Node=menu.find_child("StartPortraitPreview",true,false)
					var composition: Node=portrait.find_child("PortraitComposition",true,false) if portrait!=null else null
					if check(composition!=null,"Actual creation portrait exists"):
						check_formal_layers(composition,brow,eye,"Creator brow%d/eye%d"%[brow,eye])
					if brow==eye:
						await capture_viewport("creator_feature_%02d"%int(component(2).variant_style_ids[brow]))
					creator_cases+=1
	menu.queue_free()
	await process_frame
	root.remove_child(profile)
	profile.queue_free()
	root.add_child(original)
	await process_frame

func finish() -> void:
	var report: Dictionary={"pass":failures.is_empty(),"checks":checks,"failures":failures,"repair_records":repair_records,"active_pngs":67,"active_options":60,"retained_pngs":54,"appearance_parts_byte_identical":FileAccess.get_sha256(ACTIVE+"appearance_parts.json")==FileAccess.get_sha256(PACK+"source/frozen/appearance_parts.snapshot.json"),"male_hair_feature_combinations":male_combinations,"female_hair_feature_combinations":female_combinations,"bald_combinations_captured":bald_captures,"front_overlay_captures":front_captures,"collar_cases":collar_cases,"beard_cases":beard_cases,"editor_manual_cases":editor_cases,"creator_actual_dropdown_cases":creator_cases,"user_preset_unchanged":preset_unchanged,"geometry_source_hash_records_checked":metadata_hash_records,"imported_textures":imports,"scope":"Thirteen existing PNG repairs only: eight facial features plus five nonempty beards; empty beard01 is preserved. No added IDs/options or runtime offsets. Exact441male/108female selected-layer compositions,9bald+7front current-game captures and separately named frozen raw-before diagnostic references,7collars/6beards,formal renderer geometry/IDs and original0/3/4 actual manual workflows. Temporary in-memory fixtures only; no game or preset save.","visual_scope":"Passing import hashes, alpha/core checks or raw composition equality is not evidence that eyes or brows are anatomically balanced. Symmetry, eye opening and aesthetic review belong to root/pipeline/visual review; no mirror-image acceptance assertion exists here."}
	var output: FileAccess=FileAccess.open(OUTPUT+"runtime_check.json",FileAccess.WRITE)
	output.store_string(JSON.stringify(report,"  "))
	output.close()
	print("MALE_FACE_ALIGNMENT_FIX_20261109: ",JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)

func verify() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	var preset_before: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	config=json_file(ACTIVE+"appearance_parts.json")
	baseline=json_file(PACK+"source/baseline.json")
	verify_baseline_and_install()
	if repair_records.size()!=13:
		finish()
		return
	root.get_node("AppearanceModCatalog").call("reload")
	for record: Dictionary in repair_records:verify_import(record)
	verify_metadata_hashes()
	verify_catalog_and_renderer()
	await verify_ui()
	await verify_editor()
	await verify_creator()
	var preset_after: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	preset_unchanged=preset_before==preset_after
	check(preset_unchanged,"User preset SHA unchanged")
	check(editor_cases==9 and creator_cases==9,"All9 independent original0/3/4 pairs exercised through real manual choices")
	images.clear()
	finish()

