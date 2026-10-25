extends SceneTree
func _initialize() -> void:
	for pose in ["parry","nurture"]:
		var source := Image.load_from_file("E:/stacklands/textures/characters/side_ink_sword_actions_20261024_v6/source/"+pose+".png")
		var region := Rect2i(770,365,125,140) if pose=="parry" else Rect2i(730,630,140,120)
		var detail := source.get_region(region)
		detail.resize(region.size.x*4,region.size.y*4,Image.INTERPOLATE_LANCZOS)
		detail.save_png("E:/stacklands/tmp/"+pose+"_consistent_hand.png")
	quit()
