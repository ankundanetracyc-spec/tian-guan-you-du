extends SceneTree

const ACTIVE := "res://mods/appearance/ink_three_variants_20261004/"
const PACK := "res://mods/appearance/male_beard_round2_install_20261108/"
const OUTPUT := PACK + "installation/"
const PRESET := "user://开局设定预设.cfg"
const PARTS := ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
const CATALOG_PARTS := ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "beard"]
const ORDER := [1, 6, 7, 4, 3, 2, 5, 8, 0]
const NEW_NAMES := ["八字短髭", "短颏须", "细髭尖颏", "薄络腮须"]
const EXPECTED_STYLES := [0, 3, 4, 5, 6, 7]
var config: Dictionary = {}
var baseline: Dictionary = {}
var old_beard: Dictionary = {}
var checks: int = 0
var failures: Array[String] = []
var images: Dictionary = {}
var new_beards: Array[Dictionary] = []
var imports: Array[Dictionary] = []
var combination_count: int = 0
var editor_cases: int = 0
var creator_cases: int = 0
var preset_unchanged: bool = false

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

func verify_baseline_and_config() -> void:
	check(not baseline.is_empty(), "Frozen baseline exists")
	var old_pngs: int = 0
	var before: Dictionary = {}
	for record: Dictionary in baseline.get("files", []):
		var file: String = str(record.get("file", ""))
		var path: String = file if file.begins_with("res://") else "res://" + file
		if file.ends_with(".png"):
			old_pngs += 1
			check(FileAccess.get_sha256(path) == str(record.get("sha256", "")), "Retained PNG unchanged: " + file)
		elif file.ends_with("appearance_parts.json"):
			before = json_file(PACK + str(record.get("backup", "")))
	check(old_pngs == 55, "All55 existing PNGs covered, including installed short beard")
	check(not before.is_empty(), "Frozen previous config exists")
	for entry: Dictionary in before.get("components", []):
		if str(entry.get("id")) == "male_beard":
			old_beard = entry
		else:
			var current: Dictionary = {}
			for candidate: Dictionary in config.get("components", []):
				if str(candidate.get("id")) == str(entry.get("id")):
					current = candidate
					break
			check(current == entry, "Unrelated component unchanged: " + str(entry.get("id")))
	var beard: Dictionary = component(8)
	check(beard.get("variant_aliases", {}) == old_beard.get("variant_aliases", {}), "All previous beard aliases retained")
	check(beard.get("variant_names", []).slice(0, 2) == old_beard.get("variant_names", []), "Existing no-beard and short-beard labels retained")
	check(beard.get("variant_indices", []).slice(0, 2) == old_beard.get("variant_indices", []), "Existing physical beard files retained")
	check(beard.get("variant_style_ids", []).slice(0, 2) == old_beard.get("variant_style_ids", []), "Existing saved beard styles retained")
	check(same_integer_ids(beard.get("variant_style_ids", []), EXPECTED_STYLES), "Six saved beard styles are0/3/4/5/6/7")
	check(same_integer_ids(beard.get("variant_indices", []), [1, 2, 3, 4, 5, 6]), "Six physical beard files are01..06")
	for index in 4:
		var item: Dictionary = {"name": NEW_NAMES[index], "choice_index": index + 2, "style": index + 4, "path": path_for(beard, index + 2), "full_id": "male_beard_%02d" % (index + 4)}
		check(str(beard.variant_names[index + 2]) == NEW_NAMES[index], "New beard label: " + NEW_NAMES[index])
		new_beards.append(item)

func verify_import(item: Dictionary) -> void:
	var path: String = str(item.path)
	var texture: Texture2D = ResourceLoader.load(path, "Texture2D", ResourceLoader.CACHE_MODE_IGNORE)
	if not check(texture != null, "New beard imports: " + str(item.name)):
		return
	var source: Image = load_image(path)
	var imported: Image = texture.get_image()
	imported.convert(Image.FORMAT_RGBA8)
	if not check(source.get_size() == Vector2i(1024,1536) and imported.get_size() == source.get_size(), "Shared imported canvas: " + str(item.name)):
		return
	check(not source.is_invisible(), "Visible actual hair pigment: " + str(item.name))
	var actual: PackedByteArray = imported.get_data()
	var expected: PackedByteArray = source.get_data()
	var alpha_difference: int = 0
	var core_difference: int = 0
	var core_tested: int = 0
	var edge_rgb_difference: int = 0
	for offset in range(3, expected.size(), 4):
		if actual[offset] != expected[offset]:
			alpha_difference += 1
		var rgb_changed: bool = actual[offset-3] != expected[offset-3] or actual[offset-2] != expected[offset-2] or actual[offset-1] != expected[offset-1]
		if expected[offset] >= 230:
			core_tested += 1
			if rgb_changed:
				core_difference += 1
		elif rgb_changed:
			edge_rgb_difference += 1
	check(alpha_difference == 0 and core_difference == 0, "Imported alpha and opaque-core colors retained: " + str(item.name))
	imports.append({"name":item.name,"path":path,"alpha_different_pixels":alpha_difference,"core_tested_pixels":core_tested,"core_rgb_different_pixels":core_difference,"low_alpha_rgb_different_pixels":edge_rgb_difference,"note":"Existing Godot alpha-border processing may change low-alpha RGB; no full-RGBA identity claim."})

func verify_catalog_and_renderer() -> void:
	var catalog: Node = root.get_node("PortraitCatalog")
	check(root.get_node("AppearanceModCatalog").call("get_parts", "male", "beard").size() == 6, "Formal catalog has six beard choices")
	var data: Resource = fixture_data()
	var renderer: Control = load("res://csharp/panel/PortraitRenderer.cs").new()
	renderer.size = Vector2(1024,1536)
	root.add_child(renderer)
	for age in [16,25,40]:
		var context: Dictionary = catalog.call("get_context", data, "male", age, "", "")
		for item: Dictionary in new_beards:
			for requested: String in [str(item.style), str(item.full_id)]:
				var selected: Dictionary = catalog.call("choose_part", context, "beard", requested)
				check(str(selected.get("resource_path", "")) == str(item.path), "Explicit numeric/fullID resolves %s at age%d" % [str(item.name),age])
			data.set("appearance_beard_style", int(item.style))
			var saved: Dictionary = catalog.call("get_requested_appearance",data)
			check(str(saved.get("beard", "")) == str(item.style), "Saved requested style retained: " + str(item.name))
			renderer.call("configure",data,context,saved)
			var composition: Node = renderer.find_child("PortraitComposition",true,false)
			var layer: TextureRect = composition.get_node("Beard") if composition != null else null
			if check(layer != null and layer.texture != null, "Formal renderer creates beard layer: " + str(item.name)):
				check(layer.texture.resource_path == str(item.path) and layer.visible, "Formal renderer uses actual selected texture: " + str(item.name))
				check(layer.position == Vector2.ZERO and layer.scale == Vector2.ONE and layer.modulate == Color.WHITE, "Zero runtime offset and no hair recolor: " + str(item.name))
				var mouth: TextureRect = composition.get_node("Mouth")
				check(layer.z_index > mouth.z_index, "Beard above independent mouth: " + str(item.name))
		for old in [{"style":"0","path":ACTIVE+"male/face_accessory/01.png"},{"style":"3","path":ACTIVE+"male/face_accessory/02.png"}]:
			var selected: Dictionary = catalog.call("choose_part",context,"beard",str(old.style))
			check(str(selected.get("resource_path",""))==str(old.path), "Old saved style%s remains at age%d" % [str(old.style),age])
		for alias: Variant in old_beard.get("variant_aliases",{}):
			var selected: Dictionary = catalog.call("choose_part",context,"beard",str(alias))
			check(int(selected.get("variant_index",-1))==int(old_beard.variant_aliases[alias]), "Legacy alias%s preserved at age%d" % [str(alias),age])
		if age<35:
			var selected: Dictionary = catalog.call("choose_part",context,"beard","")
			check(int(selected.get("variant_index",-1))==0, "Unrequested youthful beard remains no-beard at age%d" % age)
	renderer.call("clear_portrait")
	renderer.queue_free()

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

func verify_test_ui() -> void:
	var scene: Control=load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await process_frame
	var breathing: CheckButton=scene.find_child("BreathingPreview",true,false)
	breathing.button_pressed=false
	set_gender(scene,false)
	check(ui_choice(scene,8).item_count==6,"Test UI exposes six beard choices")
	for part in 9:
		select(scene,part,0)
	verify_pixels(scene,"Original clean-shaven composition")
	var clean: Image=scene.find_child("PortraitPreview",true,false).texture.get_image()
	select(scene,8,1)
	verify_pixels(scene,"Retained installed short-beard composition")
	for item: Dictionary in new_beards:
		for part in 8:
			select(scene,part,0)
		select(scene,8,int(item.choice_index))
		check(ui_choice(scene,8).get_item_text(int(item.choice_index))==str(item.name),"UI label and beard mapping: "+str(item.name))
		check(selections(scene).slice(0,8)==[0,0,0,0,0,0,0,0],"Selecting "+str(item.name)+" leaves other choices alone")
		verify_pixels(scene,"Selected "+str(item.name))
		var selected_before_hide: Array[int]=selections(scene)
		var toggle: CheckButton=scene.find_child("Part_face_accessory",true,false)
		toggle.button_pressed=false
		verify_pixels(scene,"Hide "+str(item.name))
		check(scene.find_child("PortraitPreview",true,false).texture.get_image().get_data()==clean.get_data(),"Hiding "+str(item.name)+" restores exact no-beard composition")
		toggle.button_pressed=true
		check(selections(scene)==selected_before_hide,"Visibility retains "+str(item.name)+" choice")
		check(scene.find_child("PortraitPreview",true,false).texture.get_image().get_data()!=clean.get_data(),"Selected "+str(item.name)+" visibly changes portrait")
		await capture_ui(scene,"game_beard_%02d" % int(item.style))
		for front in int(component(0).variant_count):
			for back in int(component(1).variant_count):
				for clothing in int(component(7).variant_count):
					select(scene,0,front)
					select(scene,1,back)
					select(scene,7,clothing)
					check(ui_choice(scene,8).selected==int(item.choice_index),"Hair/clothes retain beard style%d" % int(item.style))
					verify_pixels(scene,"style%d front%d/back%d/clothes%d" % [int(item.style),front,back,clothing])
					combination_count+=1
	check(combination_count==252,"Four beards ×3front ×3back ×7clothes combinations checked")
	var male_saved: Array[int]=selections(scene)
	set_gender(scene,true)
	check(ui_choice(scene,8).item_count==1,"Female makeup choices unchanged")
	verify_pixels(scene,"Unchanged female baseline",true)
	select(scene,0,2)
	select(scene,1,1)
	select(scene,2,3)
	select(scene,3,2)
	var female_saved: Array[int]=selections(scene)
	verify_pixels(scene,"Existing female mixed choices",true)
	await capture_ui(scene,"game_female_preserved")
	set_gender(scene,false)
	check(selections(scene)==male_saved,"All male choices survive gender switch")
	verify_pixels(scene,"Male restored after gender switch")
	set_gender(scene,true)
	check(selections(scene)==female_saved,"Female choices survive gender switch")
	set_gender(scene,false)
	scene.find_child("ResetVariants",true,false).emit_signal("pressed")
	check(ui_choice(scene,8).selected==0,"Restore-current still chooses original no-beard")
	verify_pixels(scene,"Restore clean-shaven baseline")
	scene.queue_free()
	await process_frame

func button_by_text(parent: Node,text: String) -> Button:
	if parent is Button and str(parent.text)==text:
		return parent
	for child in parent.get_children():
		var found: Button=button_by_text(child,text)
		if found!=null:
			return found
	return null

func live_beard_row(parent: Node) -> Node:
	if parent.is_queued_for_deletion():
		return null
	var title: bool=false
	for child in parent.get_children():
		if child is Label and str(child.text)=="胡须":
			title=true
	if title and parent.get_node_or_null("Previous") is Button and parent.get_node_or_null("Next") is Button:
		return parent
	for child in parent.get_children():
		var found: Node=live_beard_row(child)
		if found!=null:
			return found
	return null

func beard_label(row: Node) -> String:
	if row==null:
		return ""
	for child in row.get_children():
		if child is Label and str(child.text)!="胡须":
			return str(child.text)
	return ""

func verify_editor() -> void:
	var editor: Control=load("res://csharp/panel/AppearanceEditor.cs").new()
	root.add_child(editor)
	await process_frame
	var apply: Button=button_by_text(editor,"应用")
	var randomize: Button=button_by_text(editor,"随机一组")
	if not check(apply!=null and randomize!=null,"Real editor apply/random controls exist"):
		editor.queue_free()
		return
	for item: Dictionary in new_beards:
		var data: Resource=fixture_data()
		editor.call("open_for_data",data,16)
		await process_frame
		var row: Node=live_beard_row(editor)
		if not check(row!=null,"Real live beard row exists for "+str(item.name)):
			continue
		check(beard_label(row).contains("无须"),"Editor initially no-beard for "+str(item.name))
		var next: Button=row.get_node("Next")
		for step in 6:
			if beard_label(row).contains(str(item.name)):
				break
			next.emit_signal("pressed")
		check(beard_label(row).contains(str(item.name)),"Actual next button selects "+str(item.name))
		check(int(data.get("appearance_beard_style"))==0,"Draft stays separate before Apply")
		var composition: Node=editor.find_child("PortraitComposition",true,false)
		var layer: TextureRect=composition.get_node("Beard") if composition!=null else null
		check(layer!=null and layer.texture!=null and layer.texture.resource_path==str(item.path),"Editor actual preview shows "+str(item.name))
		await capture_viewport("editor_beard_%02d" % int(item.style))
		apply.emit_signal("pressed")
		check(int(data.get("appearance_beard_style"))==int(item.style) and not editor.visible,"Apply stores style%d" % int(item.style))
		editor.call("open_for_data",data,16)
		await process_frame
		row=live_beard_row(editor)
		check(row!=null and beard_label(row).contains(str(item.name)),"Reopening retains "+str(item.name))
		check(int(data.get("appearance_beard_style"))==int(item.style),"Reopening preserves saved style%d" % int(item.style))
		randomize.emit_signal("pressed")
		check(beard_label(row).contains("无须"),"Young randomized editor remains no-beard")
		apply.emit_signal("pressed")
		check(int(data.get("appearance_beard_style"))==0,"Young random Apply stores no-beard")
		editor_cases+=1
	for legacy in [1,2]:
		var data: Resource=fixture_data()
		data.set("appearance_beard_style",legacy)
		editor.call("open_for_data",data,25)
		await process_frame
		var row: Node=live_beard_row(editor)
		check(row!=null and beard_label(row).contains("无须"),"Old alias%d normalizes to no-beard in editor" % legacy)
		apply.emit_signal("pressed")
		check(int(data.get("appearance_beard_style"))==0,"Old alias%d Apply retains canonical0" % legacy)
	editor.queue_free()
	await process_frame

func beard_selector(parent: Node) -> OptionButton:
	if parent==null or parent.is_queued_for_deletion():
		return null
	if parent is OptionButton:
		var found: int=0
		for item in parent.item_count:
			if NEW_NAMES.has(str(parent.get_item_text(item))):
				found+=1
		if found==4:
			return parent
	for child in parent.get_children():
		var result: OptionButton=beard_selector(child)
		if result!=null:
			return result
	return null

func verify_creator() -> void:
	# This temporary in-memory autoload is confined to this isolated test process.
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
		var selector: OptionButton=beard_selector(overlay)
		if check(selector!=null,"Actual creation dropdown includes all four approved beards"):
			check(selector.item_count==7,"Creation dropdown has random plus six beard choices")
			for item: Dictionary in new_beards:
				var selected_index: int=-1
				for candidate in selector.item_count:
					if selector.get_item_text(candidate)==str(item.name):
						selected_index=candidate
						break
				if not check(selected_index>=0,"Creator option exists: "+str(item.name)):
					continue
				selector.select(selected_index)
				selector.emit_signal("item_selected",selected_index)
				var preview_data: Resource=profile.call("create_player_appearance_preview")
				check(int(preview_data.get("appearance_beard_style"))==int(item.style),"Creator selection stores style%d" % int(item.style))
				var portrait: Node=menu.find_child("StartPortraitPreview",true,false)
				var composition: Node=portrait.find_child("PortraitComposition",true,false) if portrait!=null else null
				var layer: TextureRect=composition.get_node("Beard") if composition!=null else null
				check(layer!=null and layer.texture!=null and layer.texture.resource_path==str(item.path),"Creator actual age18 preview shows "+str(item.name))
				await capture_viewport("creator_beard_%02d" % int(item.style))
				creator_cases+=1
			selector.select(0)
			selector.emit_signal("item_selected",0)
			var preview_data: Resource=profile.call("create_player_appearance_preview")
			check(int(preview_data.get("appearance_beard_style"))==-1,"Creator random option stores unrequested-1")
			var catalog: Node=root.get_node("PortraitCatalog")
			var context: Dictionary=catalog.call("get_context",preview_data,"male",18,"","凡人")
			var requested: Dictionary=catalog.call("get_requested_appearance",preview_data)
			var selected: Dictionary=catalog.call("select_portrait",context,requested)
			var random_beard: Dictionary=selected.get("beard",{})
			check(int(random_beard.get("variant_index",-1))==0,"Creator unrequested youth resolves to no-beard")
	menu.queue_free()
	await process_frame
	root.remove_child(profile)
	profile.queue_free()
	root.add_child(original)
	await process_frame

func finish() -> void:
	var report: Dictionary={"pass":failures.is_empty(),"checks":checks,"failures":failures,"new_beards":new_beards,"retained_pngs":55,"all_beard_choices":6,"hair_clothing_combinations":combination_count,"editor_manual_cases":editor_cases,"creator_actual_dropdown_cases":creator_cases,"user_preset_unchanged":preset_unchanged,"imported_textures":imports,"scope":"Four beard additions; existing55PNG and old ID preservation;4x63 exact UI compositions, independent visibility, female preservation;formal render at16/25/40;real editor Apply/reopen and actual creator dropdown. Temporary in-memory fixtures only; no journey or preset saves."}
	var out: FileAccess=FileAccess.open(OUTPUT+"runtime_check.json",FileAccess.WRITE)
	out.store_string(JSON.stringify(report,"  "))
	out.close()
	print("MALE_BEARD_ROUND2_20261108: ",JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)

func verify() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	var preset_before: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	config=json_file(ACTIVE+"appearance_parts.json")
	baseline=json_file(PACK+"source/baseline.json")
	if not check(int(component(8).get("variant_count",0))==6,"Exactly six male beard choices after four additions"):
		finish()
		return
	verify_baseline_and_config()
	root.get_node("AppearanceModCatalog").call("reload")
	for item: Dictionary in new_beards:
		verify_import(item)
	verify_catalog_and_renderer()
	await verify_test_ui()
	await verify_editor()
	await verify_creator()
	var preset_after: String=FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	preset_unchanged=preset_before==preset_after
	check(preset_unchanged,"User preset SHA unchanged")
	check(editor_cases==4 and creator_cases==4,"All four new beards exercised through both real manual-choice flows")
	images.clear()
	finish()

