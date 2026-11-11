extends Node

var _test_scene: Node

func get_profile_location_for(actor: Node) -> String:
	if _test_scene != null and _test_scene.has_method("get_profile_location_for"):
		return str(_test_scene.call("get_profile_location_for", actor))
	return "位置未详"

func _ready() -> void:
	var scene_path := ""
	var target_width := 1280
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--native-paper-scene="):
			scene_path = argument.get_slice("=", 1)
		if argument.begins_with("--native-paper-width="):
			target_width = argument.get_slice("=", 1).to_int()
	var allowed := ["res://tools/QuietRoomPaperUiSmoke.tscn", "res://tscn/debug/RestPaperUiSmoke.tscn", "res://tools/CharacterPaperUiSmoke.tscn", "res://tools/AlchemyPaperUiSmoke.tscn", "res://tools/LetterPaperUiSmoke.tscn", "res://tools/NpcInteractionPaperUiSmoke.tscn", "res://tools/StoryDialoguePaperUiSmoke.tscn", "res://tools/TradePaperUiSmoke.tscn", "res://tscn/debug/TalismanCraftingNativeSmoke.tscn"]
	if not allowed.has(scene_path) or not [1280, 1920].has(target_width):
		push_error("Unsupported native paper capture scene or width")
		get_tree().quit(1)
		return
	get_window().mode = Window.MODE_WINDOWED
	get_window().borderless = true
	get_window().unfocusable = true
	get_window().content_scale_size = Vector2i.ZERO
	get_window().size = Vector2i(target_width, target_width * 9 / 16)
	var surface := SubViewport.new()
	surface.name = "NativePaperViewport"
	surface.size = Vector2i(target_width, target_width * 9 / 16)
	surface.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	surface.handle_input_locally = true
	add_child(surface)
	var display := TextureRect.new()
	display.texture = surface.get_texture()
	display.size = Vector2(surface.size)
	display.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(display)
	_test_scene = load(scene_path).instantiate()
	surface.add_child(_test_scene)
