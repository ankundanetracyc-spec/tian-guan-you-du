extends SceneTree
const PACK := "res://textures/characters/side_ink_sword_actions_20261024_v5/source/"
const REVIEW := "res://docs/美术规范/剑修动作重新生成_20261024/"
func _initialize() -> void:
	if "--cut-relaxed" in OS.get_cmdline_user_args():
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+"cut_relaxed.png"))
		var detail := source.get_region(Rect2i(710,600,190,200))
		detail.resize(570,600,Image.INTERPOLATE_LANCZOS)
		detail.save_png(REVIEW+"cut_relaxed_source_detail.png")
		quit()
		return
	if "--finish-right" in OS.get_cmdline_user_args():
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+"finish_right_hand.png"))
		var detail := source.get_region(Rect2i(800,490,125,105))
		detail.resize(625,525,Image.INTERPOLATE_LANCZOS)
		detail.save_png(REVIEW+"finish_right_hand_source_detail.png")
		quit()
		return
	for file in ["parry_before_hand_fix", "parry_hand_edit"]:
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+file+".png"))
		var detail := source.get_region(Rect2i(670,610,140,140))
		detail.resize(700,700,Image.INTERPOLATE_LANCZOS)
		detail.save_png(REVIEW+file+"_detail.png")
	var areas := {
		"cut": [Rect2i(770,650,120,110),Rect2i(425,585,110,105)],
		"parry": [Rect2i(680,450,130,125),Rect2i(675,640,120,100)],
		"nurture": [Rect2i(485,620,130,110),Rect2i(590,550,130,105)],
		"finish": [Rect2i(770,485,120,110),Rect2i(325,615,115,100)]
	}
	for pose in areas:
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+pose+".png"))
		for i in range(2):
			var region: Rect2i=areas[pose][i]
			var detail := source.get_region(region)
			detail.resize(region.size.x*5,region.size.y*5,Image.INTERPOLATE_LANCZOS)
			detail.save_png(REVIEW+pose+"_"+str(i)+"_detail.png")
	quit()
