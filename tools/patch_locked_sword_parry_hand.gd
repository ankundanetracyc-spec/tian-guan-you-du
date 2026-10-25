extends SceneTree
const PACK := "res://textures/characters/side_ink_sword_actions_20261024_v5/source/"
const REVIEW := "res://docs/美术规范/剑修动作重新生成_20261024/"
func _initialize() -> void:
	# The generated edit is locally registered at the cuff. Only this bounded
	# hand patch is adopted; the character elsewhere stays pixel-identical.
	var base := Image.load_from_file(ProjectSettings.globalize_path(PACK+"parry_before_hand_fix.png"))
	var edit := Image.load_from_file(ProjectSettings.globalize_path(PACK+"parry_hand_edit.png"))
	var shift := Vector2i(4,-19)
	var patch := Rect2i(684,647,103,77)
	for y in range(patch.position.y,patch.end.y):
		for x in range(patch.position.x,patch.end.x):
			var edge := minf(minf(x-patch.position.x,patch.end.x-1-x),minf(y-patch.position.y,patch.end.y-1-y))
			var blend := smoothstep(0.0,5.0,edge)
			base.set_pixel(x,y,base.get_pixel(x,y).lerp(edit.get_pixel(x+shift.x,y+shift.y),blend))
	base.save_png(PACK+"parry.png")
	var detail := base.get_region(Rect2i(665,615,145,145))
	detail.resize(725,725,Image.INTERPOLATE_LANCZOS)
	detail.save_png(REVIEW+"parry_hand_corrected.png")
	print("PARRY_HAND_PATCHED_WITH_CHARACTER_PRESERVED")
	quit()
