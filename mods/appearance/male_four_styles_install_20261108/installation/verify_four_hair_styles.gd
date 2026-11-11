extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/male_four_styles_install_20261108/"
const OUTPUT := PACK + "installation/"
const PRESET := "user://开局设定预设.cfg"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "beard"]
const ORDER := [1, 6, 7, 4, 3, 2, 5, 8, 0]
const HAIR_PARTS := [0, 1]
const NEW_FRONT_NAMES := ["后收拢发", "露额拢发", "露耳后梳", "偏分鬓辫"]
const NEW_BACK_NAMES := ["后颈低髻", "长尾高束", "自然松披", "半束长披"]
const EXPECTED_STYLES := [0, 3, 4, 5, 6, 7, 8]
const EXPECTED_FILES := [1, 4, 5, 6, 7, 8, 9]
var config: Dictionary = {}
var baseline: Dictionary = {}
var old_config: Dictionary = {}
var checks: int = 0
var failures: Array[String] = []
var images: Dictionary = {}
var new_entries: Array[Dictionary] = []
var new_pairs: Array[Dictionary] = []
var imports: Array[Dictionary] = []
var male_combinations: int = 0
var female_combinations: int = 0
var collar_cases: int = 0
var beard_cases: int = 0
var editor_cases: int = 0
var creator_cases: int = 0
var preset_unchanged: bool = false
var source_snapshot_changes: Array[String] = []
var random_style_coverage: Dictionary = {}

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

func old_component(part: int) -> Dictionary:
	for entry: Dictionary in old_config.get("components",[]):
		if str(entry.get("gender"))=="male" and str(entry.get("part"))==CATALOG_PARTS[part]:
			return entry
	return {}

func setup_config_and_baseline() -> void:
	check(not baseline.is_empty(),"Frozen baseline exists")
	var retained_pngs: int=0
	for record: Dictionary in baseline.get("files",[]):
		var file: String=str(record.get("file",""))
		var path: String=file if file.begins_with("res://") else "res://"+file
		var unchanged: bool=FileAccess.get_sha256(path)==str(record.get("sha256",""))
		if file.ends_with(".png"):
			retained_pngs+=1
			check(unchanged,"Existing PNG unchanged: "+file)
		elif file.ends_with("appearance_parts.json"):
			old_config=json_file(PACK+str(record.get("backup","")))
		elif file.ends_with(".cs") and not unchanged:
			# Other conversations may legitimately edit UI code in this shared tree.
			source_snapshot_changes.append(file)
	check(retained_pngs==59,"All59 pre-existing PNGs protected")
	check(count_pngs(ACTIVE+"male")+count_pngs(ACTIVE+"female")==67,"Exactly eight PNGs added to current67")
	check(not old_config.is_empty(),"Previous component config loaded")
	for key: Variant in old_config:
		if str(key)!="components":
			check(config.get(key)==old_config[key],"Non-component package setting unchanged: "+str(key))
	for entry: Dictionary in old_config.get("components",[]):
		var id: String=str(entry.get("id",""))
		var current: Dictionary={}
		for candidate: Dictionary in config.get("components",[]):
			if str(candidate.get("id",""))==id:
				current=candidate
				break
		if id!="male_front_hair" and id!="male_back_hair":
			check(current==entry,"Non-hair component unchanged: "+id)
	for part in HAIR_PARTS:
		var entry: Dictionary=component(part)
		var previous: Dictionary=old_component(part)
		check(int(entry.get("variant_count",0))==7,"Seven independent "+CATALOG_PARTS[part]+" choices")
		check(same_integer_ids(entry.get("variant_style_ids",[]),EXPECTED_STYLES),"Saved style mapping0/3/4/5/6/7/8 for "+CATALOG_PARTS[part])
		check(same_integer_ids(entry.get("variant_indices",[]),EXPECTED_FILES),"Physical mapping01/04/05/06/07/08/09 for "+CATALOG_PARTS[part])
		check(entry.get("variant_aliases",{})==previous.get("variant_aliases",{}),"Old aliases retained for "+CATALOG_PARTS[part])
		check(entry.get("variant_names",[]).slice(0,3)==previous.get("variant_names",[]),"Old names retained for "+CATALOG_PARTS[part])
		for key: Variant in previous:
			if not ["variant_count","variant_names","variant_indices","variant_style_ids"].has(str(key)):
				check(entry.get(key)==previous[key],"Existing hair metadata retained: "+CATALOG_PARTS[part]+"/"+str(key))
	var total_options: int=0
	for entry: Dictionary in config.get("components",[]):
		total_options+=int(entry.get("variant_count",0))
	check(total_options==60,"Exactly60 current independent choices")
	for index in 4:
		var front: Dictionary={"part":0,"slot":"hair_front","name":NEW_FRONT_NAMES[index],"style":index+5,"choice_index":index+3,"path":path_for(component(0),index+3),"full_id":"male_front_hair_%02d"%(index+5)}
		var back: Dictionary={"part":1,"slot":"hair_back","name":NEW_BACK_NAMES[index],"style":index+5,"choice_index":index+3,"path":path_for(component(1),index+3),"full_id":"male_back_hair_%02d"%(index+5)}
		check(str(component(0).variant_names[index+3])==str(front.name),"New front name: "+str(front.name))
		check(str(component(1).variant_names[index+3])==str(back.name),"New back name: "+str(back.name))
		new_entries.append(front)
		new_entries.append(back)
		new_pairs.append({"style":index+5,"choice_index":index+3,"front":front,"back":back})

func verify_import(item: Dictionary) -> void:
	var path: String=str(item.path)
	var texture: Texture2D=ResourceLoader.load(path,"Texture2D",ResourceLoader.CACHE_MODE_IGNORE)
	if not check(texture!=null,"Imported new "+str(item.name)):
		return
	var source: Image=load_image(path)
	var imported: Image=texture.get_image()
	imported.convert(Image.FORMAT_RGBA8)
	if not check(source.get_size()==Vector2i(1024,1536) and imported.get_size()==source.get_size(),"Shared canvas for "+str(item.name)):
		return
	check(not source.is_invisible(),"Visible hair in "+str(item.name))
	var actual: PackedByteArray=imported.get_data()
	var expected: PackedByteArray=source.get_data()
	var alpha_different: int=0
	var core_different: int=0
	var core_tested: int=0
	var edge_rgb_different: int=0
	for offset in range(3,expected.size(),4):
		if actual[offset]!=expected[offset]:
			alpha_different+=1
		var rgb_changed: bool=actual[offset-3]!=expected[offset-3] or actual[offset-2]!=expected[offset-2] or actual[offset-1]!=expected[offset-1]
		if expected[offset]>=230:
			core_tested+=1
			if rgb_changed:core_different+=1
		elif rgb_changed:
			edge_rgb_different+=1
	check(alpha_different==0 and core_different==0,"Imported alpha/core color preserved for "+str(item.name))
	imports.append({"path":path,"alpha_different_pixels":alpha_different,"core_tested_pixels":core_tested,"core_rgb_different_pixels":core_different,"low_alpha_rgb_different_pixels":edge_rgb_different,"note":"Existing Godot alpha-border processing may alter low-alpha RGB. No claim of complete importedRGBA identity."})

func verify_catalog_and_formal_renderer() -> void:
	var catalog: Node=root.get_node("PortraitCatalog")
	var mods: Node=root.get_node("AppearanceModCatalog")
	var data: Resource=fixture_data()
	data.set("appearance_skin_tone",4)
	data.set("appearance_hair_color",4)
	var renderer: Control=load("res://csharp/panel/PortraitRenderer.cs").new()
	renderer.size=Vector2(1024,1536)
	root.add_child(renderer)
	var context: Dictionary=catalog.call("get_context",data,"male",25,"","凡人")
	for part in HAIR_PARTS:
		var entry: Dictionary=component(part)
		var slot: String=CATALOG_PARTS[part]
		var previous: Dictionary=old_component(part)
		check(mods.call("get_parts","male",slot).size()==7,"Formal pool has seven "+slot+" choices")
		for index in 7:
			var style: int=int(entry.variant_style_ids[index])
			var full_id: String=str(entry.id)+"_%02d"%style
			for requested: String in [str(style),full_id]:
				var selected: Dictionary=catalog.call("choose_part",context,slot,requested)
				check(str(selected.get("resource_path",""))==path_for(entry,index),"Numeric/fullID resolves "+slot+"/"+requested)
		for alias: Variant in previous.get("variant_aliases",{}):
			var selected: Dictionary=catalog.call("choose_part",context,slot,str(alias))
			check(int(selected.get("variant_index",-1))==int(previous.variant_aliases[alias]),"Old alias resolves "+slot+"/"+str(alias))
		var seen: Dictionary={}
		for seed in range(1,129):
			context["seed"]=seed
			var selected: Dictionary=catalog.call("choose_part",context,slot,"")
			var style: int=int(selected.get("variant_index",-1))
			check(EXPECTED_STYLES.has(style),"Random "+slot+" returns existing saved style")
			seen[style]=true
		random_style_coverage[slot]=seen.keys()
		for style in [5,6,7,8]:
			check(seen.has(style),"New "+slot+" style%d reachable by seeded random selection"%style)
	context["seed"]=314159
	for pair: Dictionary in new_pairs:
		data.set("appearance_hair_style",int(pair.style))
		data.set("appearance_hair_back_style",int(pair.style))
		for clothing in 7:
			data.set("appearance_clothes_style",clothing)
			var requested: Dictionary=catalog.call("get_requested_appearance",data)
			renderer.call("configure",data,context,requested)
			var composition: Node=renderer.find_child("PortraitComposition",true,false)
			if not check(composition!=null,"Formal composition exists"):
				continue
			for part in HAIR_PARTS:
				var slot: String=CATALOG_PARTS[part]
				var layer: TextureRect=composition.get_node(slot.capitalize())
				var item: Dictionary=pair.front if part==0 else pair.back
				check(layer.texture!=null and layer.texture.resource_path==str(item.path) and layer.visible,"Formal selected "+str(item.name))
				check(layer.position==Vector2.ZERO and layer.scale==Vector2.ONE and layer.modulate==Color.WHITE,"Registered unrecolored hair: "+str(item.name))
			var face: TextureRect=composition.get_node("Face")
			check(face.texture!=null and face.texture.resource_path==ACTIVE+"male/face_shape/01.png" and face.modulate==Color.WHITE,"Formal head/skin stays original")
			var front_clothes: TextureRect=composition.get_node("Clothes")
			var rear_clothes: TextureRect=composition.get_node("Rear Clothes")
			check(front_clothes.texture!=null and front_clothes.texture.resource_path==path_for(component(7),clothing),"Selected front clothing linked")
			check(rear_clothes.texture!=null and rear_clothes.texture.resource_path==path_for(component(7),clothing,true) and rear_clothes.visible,"Selected rear collar linked")
			check(rear_clothes.z_index<face.z_index and face.z_index<front_clothes.z_index,"Rear collar/head/front clothing layer order remains valid")
	renderer.call("clear_portrait")
	renderer.queue_free()

func reset_choices(scene: Node,female: bool=false) -> void:
	set_gender(scene,female)
	for part in 9:
		select(scene,part,0)

func verify_ui() -> void:
	var scene: Control=load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await process_frame
	var breathing: CheckButton=scene.find_child("BreathingPreview",true,false)
	breathing.button_pressed=false
	reset_choices(scene)
	check(ui_choice(scene,0).item_count==7 and ui_choice(scene,1).item_count==7,"Test UI exposes7front/7back choices")
	check(ui_choice(scene,2).item_count==3 and ui_choice(scene,3).item_count==3,"Existing male brow/eye choices unchanged")
	check(ui_choice(scene,8).item_count==6,"All six beard choices retained")
	verify_pixels(scene,"Original male baseline")
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
	check(male_combinations==441,"All441 male hair/brow/eye compositions checked")
	for pair: Dictionary in new_pairs:
		reset_choices(scene)
		var before: Array[int]=selections(scene)
		select(scene,0,int(pair.choice_index))
		check(ui_choice(scene,1).selected==0 and selections(scene).slice(2,9)==before.slice(2,9),"Front choice does not force rear or facial changes")
		select(scene,1,int(pair.choice_index))
		verify_pixels(scene,"Selected four-style pair%d"%int(pair.style))
		await capture_ui(scene,"game_hair_%02d"%int(pair.style))
		var selected: Array[int]=selections(scene)
		for part in HAIR_PARTS:
			var toggle: CheckButton=scene.find_child("Part_"+PARTS[part],true,false)
			toggle.button_pressed=false
			verify_pixels(scene,"Independent hidden "+PARTS[part]+" style%d"%int(pair.style))
			toggle.button_pressed=true
			check(selections(scene)==selected,"Visibility preserves selected hair pair")
		var clothing_toggle: CheckButton=scene.find_child("Part_clothing",true,false)
		clothing_toggle.button_pressed=false
		verify_pixels(scene,"Both clothing sublayers hidden under new hair")
		clothing_toggle.button_pressed=true
		for clothing in 7:
			select(scene,7,clothing)
			select(scene,0,int(pair.choice_index))
			select(scene,1,0)
			verify_pixels(scene,"New front with retained rear/collar style%d/clothes%d"%[int(pair.style),clothing])
			collar_cases+=1
			select(scene,0,0)
			select(scene,1,int(pair.choice_index))
			verify_pixels(scene,"New rear with retained front/collar style%d/clothes%d"%[int(pair.style),clothing])
			collar_cases+=1
		select(scene,0,int(pair.choice_index))
		select(scene,1,int(pair.choice_index))
		select(scene,7,0)
		for beard in 6:
			select(scene,8,beard)
			verify_pixels(scene,"New hair pair%d with beard choice%d"%[int(pair.style),beard])
			beard_cases+=1
	check(collar_cases==56,"Each new front and new rear checked independently against all7collars")
	check(beard_cases==24,"Each of four new hair pairs checked with all6beards")
	var male_saved: Array[int]=selections(scene)
	reset_choices(scene,true)
	check(ui_choice(scene,0).item_count==3 and ui_choice(scene,1).item_count==3 and ui_choice(scene,2).item_count==4 and ui_choice(scene,3).item_count==3,"Female option counts retained")
	for front in 3:
		for back in 3:
			for brow in 4:
				for eye in 3:
					select(scene,0,front)
					select(scene,1,back)
					select(scene,2,brow)
					select(scene,3,eye)
					verify_pixels(scene,"Female front%d/back%d/brow%d/eye%d"%[front,back,brow,eye],true)
					female_combinations+=1
	check(female_combinations==108,"All108 existing female hair/brow/eye compositions retained")
	var female_saved: Array[int]=selections(scene)
	await capture_ui(scene,"game_female_preserved")
	set_gender(scene,false)
	check(selections(scene)==male_saved,"Male new selections survive gender switch")
	verify_pixels(scene,"Restored male selections")
	set_gender(scene,true)
	check(selections(scene)==female_saved,"Female selections survive gender switch")
	set_gender(scene,false)
	for iteration in 8:
		var randomize: Button=button_by_text(scene,"随机组合")
		if not check(randomize!=null,"Actual test-UI random button exists"):
			break
		randomize.emit_signal("pressed")
		for part in 9:
			check(ui_choice(scene,part).selected>=0 and ui_choice(scene,part).selected<ui_choice(scene,part).item_count,"Random test-UI choice within configured range")
		verify_pixels(scene,"Actual randomized combination%d"%iteration)
	scene.find_child("ResetVariants",true,false).emit_signal("pressed")
	check(selections(scene)==[0,0,0,0,0,0,0,0,0],"Restore-current returns original all-zero choice indices")
	verify_pixels(scene,"Restored original male baseline")
	scene.queue_free()
	await process_frame

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

func verify_editor() -> void:
	var editor: Control=load("res://csharp/panel/AppearanceEditor.cs").new()
	root.add_child(editor)
	await process_frame
	var apply: Button=button_by_text(editor,"应用")
	var randomize: Button=button_by_text(editor,"随机一组")
	if not check(apply!=null and randomize!=null,"Real editor apply/random controls exist"):
		editor.queue_free()
		return
	for pair: Dictionary in new_pairs:
		var data: Resource=fixture_data()
		editor.call("open_for_data",data,25)
		await process_frame
		check(choose_editor_name(editor,"前发",str(pair.front.name)),"Actual editor selects "+str(pair.front.name))
		var back_row: Node=live_choice_row(editor,"后发")
		check(choice_name(back_row,"后发")==str(component(1).variant_names[0]),"Front manual selection leaves old rear hair untouched")
		check(choose_editor_name(editor,"后发",str(pair.back.name)),"Actual editor independently selects "+str(pair.back.name))
		check(int(data.get("appearance_hair_style"))==0 and int(data.get("appearance_hair_back_style"))==0,"Editor draft does not alter source before Apply")
		var composition: Node=editor.find_child("PortraitComposition",true,false)
		for part in HAIR_PARTS:
			var layer: TextureRect=composition.get_node(CATALOG_PARTS[part].capitalize()) if composition!=null else null
			var item: Dictionary=pair.front if part==0 else pair.back
			check(layer!=null and layer.texture!=null and layer.texture.resource_path==str(item.path),"Editor real preview shows "+str(item.name))
		await capture_viewport("editor_hair_%02d"%int(pair.style))
		apply.emit_signal("pressed")
		check(int(data.get("appearance_hair_style"))==int(pair.style) and int(data.get("appearance_hair_back_style"))==int(pair.style),"Apply stores both independent new hair styles")
		check(int(data.get("appearance_brow_style"))==0 and int(data.get("appearance_eye_style"))==0 and int(data.get("appearance_face_style"))==0 and int(data.get("appearance_nose_style"))==0 and int(data.get("appearance_mouth_style"))==0,"Apply preserves reused facial styles")
		editor.call("open_for_data",data,25)
		await process_frame
		check(choice_name(live_choice_row(editor,"前发"),"前发")==str(pair.front.name) and choice_name(live_choice_row(editor,"后发"),"后发")==str(pair.back.name),"Reopening retains both new hair choices")
		randomize.emit_signal("pressed")
		apply.emit_signal("pressed")
		check(EXPECTED_STYLES.has(int(data.get("appearance_hair_style"))) and EXPECTED_STYLES.has(int(data.get("appearance_hair_back_style"))),"Actual editor random stores valid front/rear saved styles")
		check(int(data.get("appearance_beard_style"))==0,"Editor youthful random beard policy retained")
		editor_cases+=1
	for part in HAIR_PARTS:
		var field: String="appearance_hair_style" if part==0 else "appearance_hair_back_style"
		var title: String="前发" if part==0 else "后发"
		for legacy in [1,2]:
			var data: Resource=fixture_data()
			data.set(field,legacy)
			editor.call("open_for_data",data,25)
			await process_frame
			check(choice_name(live_choice_row(editor,title),title)==str(component(part).variant_names[0]),"Editor old "+title+" alias%d retains original look"%legacy)
			apply.emit_signal("pressed")
			check(int(data.get(field))==0,"Editor old "+title+" alias%d Apply stores canonical0"%legacy)
	for retained in [[3,4],[4,3]]:
		var data: Resource=fixture_data()
		data.set("appearance_hair_style",int(retained[0]))
		data.set("appearance_hair_back_style",int(retained[1]))
		editor.call("open_for_data",data,25)
		await process_frame
		check(choice_name(live_choice_row(editor,"前发"),"前发")==name_for_style(component(0),int(retained[0])) and choice_name(live_choice_row(editor,"后发"),"后发")==name_for_style(component(1),int(retained[1])),"Existing valid male styles3/4 retain their labels")
		apply.emit_signal("pressed")
		check(int(data.get("appearance_hair_style"))==int(retained[0]) and int(data.get("appearance_hair_back_style"))==int(retained[1]),"Existing valid male styles3/4 Apply unchanged")
	for part in HAIR_PARTS:
		var field: String="appearance_hair_style" if part==0 else "appearance_hair_back_style"
		var title: String="前发" if part==0 else "后发"
		for valid in [1,2]:
			var data: Resource=fixture_data()
			data.set("gender",1)
			data.set(field,valid)
			editor.call("open_for_data",data,25)
			await process_frame
			var expected: String=name_for_style(component(part,true),valid)
			check(not expected.is_empty() and choice_name(live_choice_row(editor,title),title)==expected,"Female valid "+title+" style%d takes precedence over male retired-alias semantics"%valid)
			apply.emit_signal("pressed")
			check(int(data.get(field))==valid,"Female valid "+title+" style%d Apply unchanged"%valid)
	editor.queue_free()
	await process_frame

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
		var front_selector: OptionButton=find_component_selector(overlay,0)
		var back_selector: OptionButton=find_component_selector(overlay,1)
		if check(front_selector!=null and back_selector!=null,"Actual creator has independent front/rear selectors"):
			check(front_selector.item_count==8 and back_selector.item_count==8,"Creator has random plus7 choices per hair slot")
			for pair: Dictionary in new_pairs:
				check(pick_selector_name(front_selector,str(pair.front.name)),"Creator picks "+str(pair.front.name))
				check(pick_selector_name(back_selector,str(pair.back.name)),"Creator picks "+str(pair.back.name))
				var preview_data: Resource=profile.call("create_player_appearance_preview")
				check(int(preview_data.get("appearance_hair_style"))==int(pair.style) and int(preview_data.get("appearance_hair_back_style"))==int(pair.style),"Creator profile stores independent explicit new styles")
				var portrait: Node=menu.find_child("StartPortraitPreview",true,false)
				var composition: Node=portrait.find_child("PortraitComposition",true,false) if portrait!=null else null
				for part in HAIR_PARTS:
					var layer: TextureRect=composition.get_node(CATALOG_PARTS[part].capitalize()) if composition!=null else null
					var item: Dictionary=pair.front if part==0 else pair.back
					check(layer!=null and layer.texture!=null and layer.texture.resource_path==str(item.path),"Actual creator preview shows "+str(item.name))
				await capture_viewport("creator_hair_%02d"%int(pair.style))
				creator_cases+=1
			front_selector.select(0)
			front_selector.emit_signal("item_selected",0)
			back_selector.select(0)
			back_selector.emit_signal("item_selected",0)
			var preview_data: Resource=profile.call("create_player_appearance_preview")
			check(int(preview_data.get("appearance_hair_style"))==-1 and int(preview_data.get("appearance_hair_back_style"))==-1,"Creator random stores unrequested hair styles")
			var catalog: Node=root.get_node("PortraitCatalog")
			var context: Dictionary=catalog.call("get_context",preview_data,"male",18,"","凡人")
			var requested: Dictionary=catalog.call("get_requested_appearance",preview_data)
			requested["hair_back"]=""
			var selected: Dictionary=catalog.call("select_portrait",context,requested)
			for slot in ["hair_front","hair_back"]:
				var entry: Dictionary=selected.get(slot,{})
				check(EXPECTED_STYLES.has(int(entry.get("variant_index",-1))),"Creator youthful random returns configured "+slot+" style")
	menu.queue_free()
	await process_frame
	root.remove_child(profile)
	profile.queue_free()
	root.add_child(original)
	await process_frame

func finish() -> void:
	var report: Dictionary={"pass":failures.is_empty(),"checks":checks,"failures":failures,"new_pairs":new_pairs,"new_files":new_entries,"retained_pngs":59,"active_pngs":67,"active_options":60,"male_hair_feature_combinations":male_combinations,"female_hair_feature_combinations":female_combinations,"independent_hair_collar_cases":collar_cases,"new_hair_beard_cases":beard_cases,"editor_manual_cases":editor_cases,"creator_actual_dropdown_cases":creator_cases,"user_preset_unchanged":preset_unchanged,"source_snapshot_changes":source_snapshot_changes,"intentional_source_change":"AppearanceEditor existing alias lookup also covers front/rear hair; shared-thread source differences are recorded separately.","random_style_coverage":random_style_coverage,"imported_textures":imports,"scope":"Eight hair PNG additions plus editor front/rear-hair alias normalization. Exact441male/108female UI composites;new front/rear independence,7collars and6beards;formal render/old numeric IDs/full IDs/retired aliases;real editor Apply/reopen and creator selectors;seeded random new-style reachability. Memory-only profile/data fixtures;no game or preset save invoked. Source snapshot differences are reported separately for shared-thread edits."}
	var file: FileAccess=FileAccess.open(OUTPUT+"runtime_check.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	print("MALE_FOUR_HAIR_STYLES_20261108: ",JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)

func verify() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	var preset_before: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	config=json_file(ACTIVE+"appearance_parts.json")
	baseline=json_file(PACK+"source/baseline.json")
	if not check(int(component(0).get("variant_count",0))==7 and int(component(1).get("variant_count",0))==7,"Expected7male front/7male rear hair choices"):
		finish()
		return
	setup_config_and_baseline()
	root.get_node("AppearanceModCatalog").call("reload")
	for item: Dictionary in new_entries:
		verify_import(item)
	verify_catalog_and_formal_renderer()
	await verify_ui()
	await verify_editor()
	await verify_creator()
	var preset_after: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	preset_unchanged=preset_before==preset_after
	check(preset_unchanged,"User preset SHA unchanged")
	check(editor_cases==4 and creator_cases==4,"All four new hair pairs exercised through actual manual workflows")
	images.clear()
	finish()

