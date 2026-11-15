extends SceneTree

# Registration reference only: copies the approved bald heads into a common
# four-direction canvas. Hair pixels are painted by imagegen, not this tool.
func _initialize() -> void:
	var base := Image.load_from_file("res://tools/action_battle_test/art/pawn_four_way_20261105/base_parts.png")
	var guide := Image.create(1536,768,false,Image.FORMAT_RGBA8)
	guide.fill(Color.TRANSPARENT)
	for col in range(4):
		var source := base.get_region(Rect2i(col*313,350,313,280))
		var low := Vector2i(313,280)
		var high := Vector2i(-1,-1)
		for y in range(280):
			for x in range(313):
				if source.get_pixel(x,y).a>0.88:
					low=Vector2i(mini(low.x,x),mini(low.y,y))
					high=Vector2i(maxi(high.x,x),maxi(high.y,y))
		var head := source.get_region(Rect2i(low,high-low+Vector2i.ONE))
		head.resize(roundi(head.get_width()*152.0/head.get_height()),152,Image.INTERPOLATE_LANCZOS)
		for row in range(2):
			guide.blit_rect(head,Rect2i(Vector2i.ZERO,head.get_size()),Vector2i(col*384+192-head.get_width()/2,row*384+250-152))
	guide.save_png("res://tools/action_battle_test/art/pawn_four_way_20261105/first_hair_head_scaffold.png")
	quit()
