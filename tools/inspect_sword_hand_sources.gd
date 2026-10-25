extends SceneTree
func _initialize() -> void:
	var skin := Image.load_from_file(ProjectSettings.globalize_path("res://textures/characters/side_ink_sword_idles_20261023_v2/source/male_held_idle.png"))
	var seal := Image.load_from_file(ProjectSettings.globalize_path("res://docs/美术规范/剑修基础动作设计_20261024/nurture.png"))
	var fist := skin.get_region(Rect2i(490,610,140,110))
	fist.resize(700,550,Image.INTERPOLATE_NEAREST)
	fist.save_png("res://docs/美术规范/剑动作手部分层修正_20261024/fist_source_detail.png")
	var fingers := seal.get_region(Rect2i(635,370,125,140))
	fingers.resize(500,560,Image.INTERPOLATE_NEAREST)
	fingers.save_png("res://docs/美术规范/剑动作手部分层修正_20261024/seal_source_detail.png")
	quit()
