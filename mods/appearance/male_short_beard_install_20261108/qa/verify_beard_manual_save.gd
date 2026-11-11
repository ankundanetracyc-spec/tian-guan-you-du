extends SceneTree

const OUTPUT := "res://mods/appearance/male_short_beard_install_20261108/installation/"
const BEARD := "res://mods/appearance/ink_three_variants_20261004/male/face_accessory/02.png"
const PRESET := "user://开局设定预设.cfg"
var checks: int = 0
var failures: Array[String] = []
var editor_age_cases: int = 0
var creator_verified: bool = false


func _initialize() -> void:
	call_deferred("verify")


func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures.append(message)
		push_error(message)


func button_by_text(parent: Node, text: String) -> Button:
	if parent is Button and str(parent.text) == text:
		return parent
	for child in parent.get_children():
		var result: Button = button_by_text(child, text)
		if result != null:
			return result
	return null


func beard_label(row: Node) -> String:
	if row == null:
		return ""
	for child in row.get_children():
		if child is Label and str(child.text) != "胡须":
			return str(child.text)
	return ""


func live_beard_row(parent: Node) -> Node:
	# Rebuilding queues old rows for deletion before adding new ones. Godot can
	# rename the new row while the old Choice_beard still owns that name, so
	# identify the live UI row by its title and controls instead of its name.
	if parent.is_queued_for_deletion():
		return null
	var has_title: bool = false
	for child in parent.get_children():
		if child is Label and str(child.text) == "胡须":
			has_title = true
	if has_title and parent.get_node_or_null("Previous") is Button and parent.get_node_or_null("Next") is Button:
		return parent
	for child in parent.get_children():
		var row: Node = live_beard_row(child)
		if row != null:
			return row
	return null


func fixture_data() -> Resource:
	var data: Resource = load("res://csharp/resources/HumanData.cs").new()
	data.set("gender", 0)
	data.set("appearance_seed", 314159)
	for field in ["appearance_face_style", "appearance_ear_style", "appearance_brow_style", "appearance_eye_style", "appearance_eyebrow_style", "appearance_nose_style", "appearance_mouth_style", "appearance_makeup_style", "appearance_hair_back_style", "appearance_hair_style", "appearance_clothes_style", "appearance_beard_style"]:
		data.set(field, 0)
	data.set("appearance_skin_tone", -1)
	data.set("appearance_hair_color", -1)
	return data


func verify_editor() -> void:
	var editor: Control = load("res://csharp/panel/AppearanceEditor.cs").new()
	root.add_child(editor)
	await process_frame
	var apply: Button = button_by_text(editor, "应用")
	var randomize: Button = button_by_text(editor, "随机一组")
	check(apply != null and randomize != null, "AppearanceEditor real apply/random controls exist")
	if apply == null or randomize == null:
		editor.queue_free()
		return
	for age in [16, 25]:
		var data: Resource = fixture_data()
		editor.call("open_for_data", data, age)
		await process_frame
		var row: Node = live_beard_row(editor)
		check(row != null and beard_label(row).contains("无须"), "Age%d editor opens at clean-shaven baseline" % age)
		if row == null:
			continue
		var next: Button = row.get_node("Next")
		check(not next.disabled, "Age%d editor allows manual beard selection" % age)
		next.emit_signal("pressed")
		check(beard_label(row).contains("短髭颌须"), "Age%d real next button selects short beard" % age)
		check(int(data.get("appearance_beard_style")) == 0, "Draft selection stays separate until Apply")
		var composition: Node = editor.find_child("PortraitComposition", true, false)
		var beard: TextureRect = composition.get_node("Beard") if composition != null else null
		check(beard != null and beard.texture != null and beard.texture.resource_path == BEARD, "Age%d editor real preview shows selected beard" % age)
		if age == 16 and DisplayServer.get_name() != "headless":
			await process_frame
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(OUTPUT+"editor_short_beard_viewport.png")
		apply.emit_signal("pressed")
		check(int(data.get("appearance_beard_style")) == 3 and not editor.visible, "Age%d Apply writes style3 to memory source and closes editor" % age)
		editor.call("open_for_data", data, age)
		await process_frame
		row = live_beard_row(editor)
		check(row != null and not row.is_queued_for_deletion(), "Age%d reopening locates live beard controls" % age)
		check(beard_label(row).contains("短髭颌须"), "Age%d reopening retains manually selected style3" % age)
		check(int(data.get("appearance_beard_style")) == 3, "Reopening does not normalize saved style3 away")
		randomize.emit_signal("pressed")
		check(beard_label(row).contains("无须"), "Age%d Random retains youthful no-beard policy" % age)
		apply.emit_signal("pressed")
		check(int(data.get("appearance_beard_style")) == 0, "Age%d randomized Apply stores no-beard style0" % age)
		editor_age_cases += 1
	for legacy in [1, 2]:
		var data: Resource = fixture_data()
		data.set("appearance_beard_style", legacy)
		editor.call("open_for_data", data, 25)
		await process_frame
		var row: Node = live_beard_row(editor)
		check(row != null and not row.is_queued_for_deletion(), "Legacy style%d locates live beard controls" % legacy)
		check(beard_label(row).contains("无须"), "Legacy beard style%d normalizes through existing alias to no-beard" % legacy)
		apply.emit_signal("pressed")
		check(int(data.get("appearance_beard_style")) == 0, "Legacy beard style%d Apply stores canonical style0" % legacy)
	editor.queue_free()
	await process_frame


func beard_selector(parent: Node) -> OptionButton:
	if parent is OptionButton:
		for index in parent.item_count:
			if str(parent.get_item_text(index)) == "短髭颌须":
				return parent
	for child in parent.get_children():
		var result: OptionButton = beard_selector(child)
		if result != null:
			return result
	return null


func verify_creator() -> void:
	# Replace only this isolated process's in-memory autoload; never invoke preset/game save.
	var original: Node = root.get_node("GameStartProfile")
	root.remove_child(original)
	var profile: Node = load("res://csharp/autoload/GameStartProfile.cs").new()
	profile.name = "GameStartProfile"
	root.add_child(profile)
	profile.call("capture_player_appearance", fixture_data())
	profile.call("roll_fated_aptitude")
	var menu: Control = load("res://tscn/scene/StartMenu.tscn").instantiate()
	root.add_child(menu)
	await process_frame
	var new_game: Button = menu.find_child("NewSave", true, false)
	check(new_game != null, "StartMenu real NewSave button exists")
	if new_game != null:
		new_game.emit_signal("pressed")
		await process_frame
		var overlay: Node = menu.find_child("NewGameSetup", true, false)
		var selector: OptionButton = beard_selector(overlay) if overlay != null else null
		check(selector != null, "Creator real dropdown includes short beard")
		if selector != null:
			var index: int = -1
			for item in selector.item_count:
				if selector.get_item_text(item) == "短髭颌须":
					index = item
			selector.select(index)
			selector.emit_signal("item_selected", index)
			var preview_data: Resource = profile.call("create_player_appearance_preview")
			check(int(preview_data.get("appearance_beard_style")) == 3, "Creator selection writes style3 into isolated profile")
			var portrait: Node = menu.find_child("StartPortraitPreview", true, false)
			var composition: Node = portrait.find_child("PortraitComposition", true, false) if portrait != null else null
			var beard: TextureRect = composition.get_node("Beard") if composition != null else null
			check(beard != null and beard.texture != null and beard.texture.resource_path == BEARD, "Age18 creator preview shows manually chosen short beard")
			if DisplayServer.get_name() != "headless":
				await process_frame
				await RenderingServer.frame_post_draw
				root.get_texture().get_image().save_png(OUTPUT+"creator_short_beard_viewport.png")
			var catalog: Node = root.get_node("PortraitCatalog")
			var context: Dictionary = catalog.call("get_context", preview_data, "male", 18, "", "凡人")
			selector.select(0)
			selector.emit_signal("item_selected", 0)
			preview_data = profile.call("create_player_appearance_preview")
			check(int(preview_data.get("appearance_beard_style")) == -1, "Creator random option stores unrequested style-1")
			var requested: Dictionary = catalog.call("get_requested_appearance", preview_data)
			var selected: Dictionary = catalog.call("select_portrait", context, requested)
			var random_beard: Dictionary = selected.get("beard", {})
			check(int(random_beard.get("variant_index", -1)) == 0, "Creator random youth still resolves to no-beard")
			creator_verified = true
	menu.queue_free()
	await process_frame
	root.remove_child(profile)
	profile.queue_free()
	root.add_child(original)
	await process_frame


func verify() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	var preset_before: String = FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	root.get_node("AppearanceModCatalog").call("reload")
	await verify_editor()
	await verify_creator()
	var preset_after: String = FileAccess.get_sha256(PRESET) if FileAccess.file_exists(PRESET) else "absent"
	check(preset_before == preset_after, "User preset file unchanged")
	var result: Dictionary = {"pass": failures.is_empty(), "checks": checks, "failures": failures, "editor_age_cases": editor_age_cases, "creator_real_dropdown_verified": creator_verified, "user_preset_unchanged": preset_before == preset_after, "scope": "Real editor controls apply/reopen using temporary HumanData at16/25;legacy1/2 aliases;random youth default. Real StartMenu dropdown uses a temporary in-memory GameStartProfile. No game journey or preset save invoked."}
	var output: FileAccess = FileAccess.open(OUTPUT + "manual_save_check.json", FileAccess.WRITE)
	output.store_string(JSON.stringify(result, "  "))
	output.close()
	print("MALE_BEARD_MANUAL_SAVE_20261108: ", JSON.stringify(result))
	quit(0 if failures.is_empty() else 1)
