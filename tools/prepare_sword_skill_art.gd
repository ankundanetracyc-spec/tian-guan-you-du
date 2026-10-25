extends SceneTree

const PACK := "res://textures/characters/side_ink_sword_skills_20261023_v1/"

func _initialize() -> void:
	# All source poses use the same scale. Explicit baseline corrections only;
	# never derive scale or horizontal position from an individual pose's silhouette.
	var baseline_shifts := {"cut":0,"finish":-32,"parry":-6,"nurture":-8}
	for pose in baseline_shifts:
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/male_sword_"+pose+".png"))
		source.convert(Image.FORMAT_RGBA8)
		source.resize(2048,2048,Image.INTERPOLATE_LANCZOS)
		var registered := Image.create(2048,2048,false,Image.FORMAT_RGBA8)
		registered.blit_rect(source,Rect2i(0,0,2048,2048),Vector2i(0,baseline_shifts[pose]))
		registered.save_png(PACK+"male_sword_"+pose+".png")
	print("SWORD_SKILL_ART_REGISTERED")
	quit()
