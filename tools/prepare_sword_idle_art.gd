extends SceneTree

const PACK := "res://textures/characters/side_ink_sword_idle_20261022_v1/"

func _initialize() -> void:
	var image := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/male_idle.png"))
	var original_size := image.get_size()
	image.convert(Image.FORMAT_RGBA8)
	# One production registration to the common character canvas. No runtime fitting.
	image.resize(2048,2048,Image.INTERPOLATE_LANCZOS)
	image.save_png(PACK+"male_idle.png")
	var bytes := image.get_data()
	var transparent := 0
	var partial := 0
	for index in range(3,bytes.size(),4):
		if bytes[index]==0: transparent+=1
		elif bytes[index]<255: partial+=1
	var manifest := {"canvas":[2048,2048],"format":"RGBA8","source_size":[original_size.x,original_size.y],"ground":[1024,1900],"reference_height":1375,"grip_canvas":[1466,994],"weapon_layer":"below_character_above_ground_shadow","alpha":{"transparent":transparent,"partial":partial},"scope":"One male sword idle; sword icons remain separate. No equipment rules or saves changed."}
	FileAccess.open(PACK+"asset_manifest.json",FileAccess.WRITE).store_string(JSON.stringify(manifest,"\t"))
	print("SWORD_IDLE_ART_REGISTERED")
	quit()
