extends SceneTree

const PACK := "res://mods/appearance/ink_three_variants_20261004/"

func count_for(sex: String, slot: String) -> int:
	var config: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(PACK + "appearance_parts.json"))
	for entry in config.components:
		if entry.gender == sex and entry.part == slot:
			return entry.variant_count
	assert(false, "Missing component")
	return 0

func _initialize() -> void:
	call_deferred("verify")

func settle() -> void:
	for i in range(3): await process_frame
	await RenderingServer.frame_post_draw

func verify() -> void:
	root.size = Vector2i(1672,941)
	var start = load("res://tscn/scene/StartMenu.tscn").instantiate()
	root.add_child(start); current_scene = start
	await settle()
	start.find_child("AppearanceCreatorTest",true,false).emit_signal("pressed")
	await settle()
	var scene = current_scene
	var gender: OptionButton = scene.find_child("GenderChoice",true,false)
	var summary: Label = scene.find_child("AppearanceStatus",true,false)
	var counts: Dictionary = {}
	for sex in ["male", "female"]:
		var index := 1 if sex == "female" else 0
		gender.select(index); gender.emit_signal("item_selected",index)
		await settle()
		var front_count := count_for(sex,"hair_front")
		var back_count := count_for(sex,"hair_back")
		counts[sex] = [front_count,back_count]
		assert(summary.text.contains("前发 %d 款／后发 %d 款" % [front_count,back_count]))
		for i in range(3):
			var front: OptionButton = scene.find_child("Variant_hair_front",true,false)
			var back: OptionButton = scene.find_child("Variant_hair_back",true,false)
			front.select(front_count-3+i); front.emit_signal("item_selected",front_count-3+i)
			back.select(back_count-3+i); back.emit_signal("item_selected",back_count-3+i)
			await settle()
			assert(root.get_texture().get_image().save_png(PACK + "preview/" + sex + "_practical_hair_game_%02d.png" % (i+6)) == OK)
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(PACK + "portrait_manifest.json"))
	var revision := "female_practical_hair_expansion_20261006" if manifest.has("female_practical_hair_expansion") else "practical_hair_expansion_20261006"
	if manifest.has("hair_front_refinement"):
		revision = "hair_front_refinement_20261006"
	if manifest.has("straight_hair_redesign"):
		revision = "straight_hair_redesign_20261006"
	var report := {"pass":true,"start_menu_entry":true,"counts":counts,"gender_switch_updates_counts":true,"fresh_game_captures":6}
	FileAccess.open(PACK + "source/" + revision + "/ui_summary_verification.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("PRACTICAL_HAIR_SUMMARY_PASS: ", counts)
	scene.queue_free(); await settle(); quit(0)
