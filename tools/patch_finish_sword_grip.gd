extends SceneTree

const PACK := "res://textures/characters/side_ink_sword_actions_20261024_v5/"
const REVIEW := "res://docs/美术规范/剑修动作重新生成_20261024/"
const SCALE := 1790.0/1254.0

func _initialize() -> void:
	# This is a hand-only patch. The pose, cuff, garment and feet outside
	# this small region retain their original painted pixels.
	var base := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/finish_right_hand.png"))
	var edit := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/finish_grip_right_closeup.png"))
	edit.resize(300,300,Image.INTERPOLATE_LANCZOS)
	var mask := Image.create(300,300,false,Image.FORMAT_RGBA8)
	for y in range(88,192):
		for x in range(91,210):
			var c := edit.get_pixel(x,y)
			if c.a>.2 and c.r>.5 and c.r-c.g>.10 and c.g-c.b>.08 and c.r-c.g>(c.g-c.b)*.65:
				for dy in range(-2,3):
					for dx in range(-2,3):
						if dx*dx+dy*dy<=5: mask.set_pixel(x+dx,y+dy,Color.WHITE)
	var patch := Rect2i(825,500,94,80)
	var shift := Vector2(129,308.4130859375)
	var crop_origin := Vector2(1728,934)
	for y in range(patch.position.y,patch.end.y):
		for x in range(patch.position.x,patch.end.x):
			var local := Vector2(x,y)*SCALE+shift+Vector2(512,0)-crop_origin
			var p := Vector2i(local.round())
			var c := edit.get_pixelv(p)
			c.a*=mask.get_pixelv(p).a
			if p.x>=211: c=Color.TRANSPARENT
			# Keep the short cuff connection. All other dark handle pixels
			# are excluded, so the sword remains an independent texture.
			if x<830: c=edit.get_pixelv(p)
			base.set_pixel(x,y,c)
	base.save_png(PACK+"source/finish_right_grip_final.png")
	var detail := base.get_region(Rect2i(800,490,145,115))
	detail.resize(725,575,Image.INTERPOLATE_LANCZOS)
	detail.save_png(REVIEW+"finish_grip_corrected_source.png")
	print("FINISH_GRIP_PATCHED_WITH_CHARACTER_PRESERVED")
	quit()
