extends SceneTree
const PACK := "res://textures/characters/side_ink_sword_actions_fresh_20261024_v4/source/"
const REVIEW := "res://docs/美术规范/剑修动作重新生成_20261024/"
func _initialize() -> void:
	for pose in ["cut", "parry", "nurture", "finish"]:
		if not FileAccess.file_exists(PACK+pose+".png"): continue
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+pose+".png"))
		var region: Rect2i
		match pose:
			"cut": region = Rect2i(600, 600, 310, 230)
			"parry": region = Rect2i(580, 370, 240, 300)
			"nurture": region = Rect2i(400, 390, 450, 240)
			_: region = Rect2i(520, 350, 370, 250)
		var detail := source.get_region(region)
		detail.resize(region.size.x*3,region.size.y*3,Image.INTERPOLATE_LANCZOS)
		detail.save_png(REVIEW+pose+"_source_hands.png")
	quit()
