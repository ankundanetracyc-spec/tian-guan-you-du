extends Node

const Room = preload("pawn_four_way_room.gd")
var checks := 0
var failures: Array[String] = []
var combinations := []
var room: Node2D

func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		push_error("PAWN ROOM QA: "+label)

func _source_values(resource: Resource) -> Dictionary:
	var result := {}
	if resource==null: return result
	for property in resource.get_property_list():
		var name_value := str(property.name)
		if name_value.begins_with("appearance_") or name_value=="gender": result[name_value] = resource.get(name_value)
	return result

func _select(part: String, value: int) -> void:
	var picker: OptionButton = room.option_buttons[part]
	for index in range(picker.item_count):
		if int(picker.get_item_metadata(index))==value:
			picker.item_selected.emit(index)
			return
	_check(false,"option exists: "+str([part,value]))

func _ready() -> void:
	room = Room.new()
	add_child(room)
	room.set_process(false)
	_check(room.renderer_ready and room.actor!=null and room.comparisons.size()==4,"actual room loads renderer and four fixed comparisons")
	_check(room.adapter!=null and room.load_errors.is_empty(),"actual profile adapter builds without missing bridge")
	if room.adapter==null or room.actor==null:
		_finish()
		return
	var bound: Resource = room.adapter.get_bound_resource()
	var original_source := _source_values(bound)
	var initial_profile: Dictionary = room.draft.duplicate(true)
	_check((room.option_buttons.hair_front as OptionButton).item_count==3 and (room.option_buttons.hair_back as OptionButton).item_count==3,"three independent front and three rear options exposed")
	room.sample_button.pressed.emit()
	_check(room.draft.gender==0 and room.draft.appearance_clothes_style==5 and bool(room.draft.asset_support.supported),"explicit sample button selects supported male5 private draft")
	_check(str(room.draft.source)=="private_preview_draft" and str(room.draft.source_label).contains("未替换当前人物"),"sample is labelled as independent instead of silently replacing current identity")
	for front in range(3):
		for back in range(3):
			var previous_back: int = room.draft.appearance_hair_back_style
			_select("hair_front",front)
			_check(room.draft.appearance_hair_style==front and room.draft.appearance_hair_back_style==previous_back,"front control preserves actual selected rear field: "+str([front,back]))
			_select("hair_back",back)
			_check(room.draft.appearance_hair_style==front and room.draft.appearance_hair_back_style==back,"rear control preserves actual selected front field: "+str([front,back]))
			var all_avatars: Array = room.comparisons.duplicate()
			all_avatars.append(room.actor)
			for avatar in all_avatars:
				var data: Dictionary = avatar.get_diagnostics()
				_check(data.supported and data.front_style==front and data.back_style==back and data.profile_key==room.draft.profile_key,"main/four-direction avatar reads the same resolved DTO: "+str([front,back,avatar.name]))
				var front_region: Rect2 = (avatar.layers.HairFront.texture as AtlasTexture).region
				var back_region: Rect2 = (avatar.layers.HairBack.texture as AtlasTexture).region
				_check(is_equal_approx(front_region.position.y,float(front)*avatar.assets.front.cell.y) and is_equal_approx(back_region.position.y,float(back)*avatar.assets.back.cell.y),"real front/rear atlas row changes independently with the same profile: "+str([front,back,avatar.name]))
				for part in ["clothes","hair_front","hair_back"]:
					_check(str(data.parts[part].source_png)==str(room.draft.parts[part].resource_path),"avatar source identity matches portrait DTO: "+part+str([front,back,avatar.name]))
			var expected_count := 0
			var required_parts := ["hair_back","rear_clothes","face","clothes","nose","eye","brow","mouth","hair_front"]
			for layer in room.portrait_layers:
				if bool(layer.get("visible",true)) and not str(layer.get("resource_path","")).is_empty():
					expected_count += 1
					required_parts.erase(str(layer.part))
			_check(room.portrait_missing.is_empty() and room.portrait_nodes.size()==expected_count and required_parts.is_empty(),"all original visible layers plus rear collar load exactly as supplied by adapter: "+str([front,back]))
			for node in room.portrait_nodes:
				var part := str(node.get_meta("portrait_part"))
				var matching := {}
				for layer in room.portrait_layers:
					if str(layer.part)==part: matching = layer;break
				_check(not matching.is_empty() and node.texture==room.portrait_textures[str(matching.resource_path)],"portrait node uses actual adapter layer resource: "+part+str([front,back]))
				_check(node.texture.get_size()==Vector2(1024,1536) and not node.centered and node.position==Vector2.ZERO and node.offset==Vector2.ZERO and node.scale==Vector2.ONE,"original layer shares full untouched portrait canvas: "+part+str([front,back]))
			combinations.append({"front":front,"back":back,"profile_key":room.draft.profile_key,"portrait_layers":room.portrait_nodes.size(),"source":room.draft.capture_source})
	for index in range(4):
		room.direction_buttons[index].pressed.emit()
		_check(room.direction==room.DIRECTIONS[index] and str(room.actor.get_diagnostics().direction)==["front","back","left","right"][index],"direction button updates actual main avatar: "+str(index))
		_check(str(room.comparisons[index].get_diagnostics().direction)==["front","back","left","right"][index],"comparison remains on its assigned direction: "+str(index))
		var card: Node2D = room.comparisons[index]
		var expected_column: int = [0,2,3,1][index]
		_check(is_equal_approx((card.layers.Body.texture as AtlasTexture).region.position.x,float(expected_column)*card.assets.base.cell.x) and is_equal_approx((card.layers.Head.texture as AtlasTexture).region.position.x,float(expected_column)*card.assets.base.cell.x),"side/rear body and head use their actual column rather than the front column: "+str(index))
	_check(not room.comparisons[1].layers.HairFront.visible,"rear comparison does not expose front hair after room reapplies debug visibility")
	room.front_check.button_pressed = false
	_check(not room.actor.layer_visibility.hair_front and room.actor.layer_visibility.hair_back,"front visibility control stays independent")
	for node in room.portrait_nodes:
		if str(node.get_meta("portrait_part"))=="hair_front": _check(not node.visible,"real portrait front hides with the same debug switch")
	room.front_check.button_pressed = true
	room.back_check.button_pressed = false
	_check(room.actor.layer_visibility.hair_front and not room.actor.layer_visibility.hair_back,"rear visibility control stays independent")
	for node in room.portrait_nodes:
		if str(node.get_meta("portrait_part"))=="hair_back": _check(not node.visible,"real portrait rear hides with the same debug switch")
	room.set_debug_layers(true,true)
	room.actor.position = Vector2(414,496)
	var before_position: Vector2 = room.actor.position
	room.advance_preview(1.0/30.0,Vector2(170,0))
	_check(room.actor.position.x>before_position.x and room.actor.get_diagnostics().moving,"actual preview advances movement and render motion")
	var before_clock: float = room.actor.get_diagnostics().clock
	var paused_position: Vector2 = room.actor.position
	room.pause_button.pressed.emit()
	room._process(0.25)
	_check(room.paused and is_equal_approx(before_clock,room.actor.get_diagnostics().clock) and room.actor.position==paused_position,"pause button freezes room movement and avatar clock")
	room.pause_button.pressed.emit()
	var unsupported: Dictionary = room.adapter.resolve_profile({"gender":1,"appearance_clothes_style":5,"appearance_hair_style":0,"appearance_hair_back_style":0,"source":"private_preview_draft"})
	room.set_preview_profile(unsupported)
	_check(room.draft.gender==1 and not room.actor.get_diagnostics().supported and room.renderer_message.visible,"unsupported identity stays visible as a notice without automatic sample replacement")
	room.sample_button.pressed.emit()
	_check(room.draft.gender==0 and room.actor.get_diagnostics().supported,"explicit sample button restores only the preview draft")
	room.read_button.pressed.emit()
	_check(room.draft.profile_key==initial_profile.profile_key,"read-current button restores actual current source identity")
	room.reset_button.pressed.emit()
	_check(room.draft.profile_key==room.initial_profile.profile_key,"initial appearance button restores session initial DTO")
	_check(_source_values(bound)==original_source,"all controls and nine combinations leave actual bound HumanData unchanged")
	_finish()

func _finish() -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"combinations":combinations,"project_root":ProjectSettings.globalize_path("res://"),"scope":"Real room controls, same DTO for main/four directions/original portrait, independent front/rear fields and visibility, movement/pause, explicit unsupported/sample/read/reset, original HumanData unchanged. Functional QA only; shape/colour/side-view art is not approved."}
	var directory: String = get_script().resource_path.get_base_dir().path_join("output")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(directory))
	var output := directory.path_join("pawn_four_way_room_smoke.json")
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--report-path="): output = argument.trim_prefix("--report-path=")
	FileAccess.open(output,FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("PAWN FOUR WAY ROOM QA ","PASS" if failures.is_empty() else "FAIL",": ",JSON.stringify(report))
	get_tree().quit(0 if failures.is_empty() else 1)
