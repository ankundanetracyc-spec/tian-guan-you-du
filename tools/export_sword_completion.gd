extends "res://tools/export_locked_sword_actions.gd"

# Reuses the native straight-alpha renderer. No weapon is baked into a body.
const OUTPUT := "res://docs/美术规范/持剑受击与女修剑招_20261025/"

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--pack-root="): PACK=argument.trim_prefix("--pack-root=").trim_suffix("/")+"/"
		if argument.begins_with("--only="): only_pose=argument.trim_prefix("--only=")
	call_deferred("_export")

func _export() -> void:
	root.position=Vector2i(-4096,-4096)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	var source_layout: Dictionary=JSON.parse_string(FileAccess.get_file_as_string(PACK+"source/registration.json"))
	var icon := _texture("res://textures/ui/forged_swords_20261023_v1/slender_sword.png")
	var icon_grip := Vector2(.874,.512)*icon.get_size()
	var icon_axis := (Vector2(.024,.505)-Vector2(.874,.512))*icon.get_size()
	var registrations := {}
	if only_pose!="" and FileAccess.file_exists(PACK+"layer_layout.json"):
		registrations=JSON.parse_string(FileAccess.get_file_as_string(PACK+"layer_layout.json"))["poses"]
	for pose in source_layout["poses"]:
		if only_pose!="" and pose!=only_pose: continue
		var info: Dictionary=source_layout["poses"][pose].duplicate(true)
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/"+pose+".png"))
		_keep_character_component(source,Vector2i(info["seed"][0],info["seed"][1]))
		var grip := Vector2(info["grip"][0],info["grip"][1])
		var ground := Vector2(info["ground"][0],info["ground"][1])
		var art_scale: float=info["source_scale"]
		var shift := GROUND-ground*art_scale
		var angle: float=deg_to_rad(float(info["sword_angle_degrees"]))
		var rect: Array=info["hand_rect"]
		var skin := _skin_mask(source,Rect2i(rect[0],rect[1],rect[2],rect[3]))
		var mask_view := _view(source.get_size())
		for polygon in info["front"]: _mask_polygon(mask_view,_polygon(polygon))
		var mask := await _frame(mask_view)
		mask_view.queue_free()
		var front := source.duplicate()
		for y in range(source.get_height()):
			for x in range(source.get_width()):
				var c := source.get_pixel(x,y)
				c.a*=skin.get_pixel(x,y).a*mask.get_pixel(x,y).a
				front.set_pixel(x,y,c)
		var view := _view(Vector2i(2048,2048))
		var body := _sprite(view,ImageTexture.create_from_image(source))
		body.position=shift
		body.scale=Vector2.ONE*art_scale
		var canvas := await _frame(view)
		canvas.save_png(PACK+pose+"_body_back.png")
		body.texture=ImageTexture.create_from_image(front)
		var front_canvas := await _frame(view)
		var bounds := front_canvas.get_used_rect()
		for y in range(bounds.position.y,bounds.end.y):
			for x in range(bounds.position.x,bounds.end.x):
				var c := front_canvas.get_pixel(x,y)
				if c.a>0:
					var painted := canvas.get_pixel(x,y)
					c.r=painted.r
					c.g=painted.g
					c.b=painted.b
					front_canvas.set_pixel(x,y,c)
		front_canvas.save_png(PACK+pose+"_hand_front.png")
		var registered_grip := grip*art_scale+shift
		view.size=Vector2i(3072,2048)
		body.texture=ImageTexture.create_from_image(canvas)
		body.position=Vector2(512,0)
		body.scale=Vector2.ONE
		var sword := _sprite(view,icon)
		sword.offset=-icon_grip
		sword.position=registered_grip+Vector2(512,0)
		sword.rotation=angle-icon_axis.angle()
		sword.scale=Vector2.ONE*780/icon_axis.length()
		var fingers := _sprite(view,ImageTexture.create_from_image(front_canvas))
		fingers.position=Vector2(512,0)
		var assembled := await _frame(view)
		assembled.save_png(PACK+pose+"_assembled.png")
		assembled.get_region(Rect2i(Vector2i(registered_grip)+Vector2i(512,0)-Vector2i(140,140),Vector2i(280,280))).save_png(OUTPUT+source_layout["sex"]+"_"+pose+"_grip.png")
		info["grip_canvas"]=[registered_grip.x,registered_grip.y]
		info["canvas_translation"]=[shift.x,shift.y]
		info["weapon_hand"]="right"
		registrations[pose]=info
		print("EXPORTED: "+source_layout["sex"]+" / "+pose)
		view.queue_free()
		await process_frame
	FileAccess.open(PACK+"layer_layout.json",FileAccess.WRITE).store_string(JSON.stringify({"canvas":[2048,2048],"ground":[1024,1900],"reference_height":1375,"render_order":["body_back","independent sword","hand_front"],"grip_to_tip":780,"poses":registrations},"\t"))
	print("SWORD_COMPLETION_EXPORTED: "+source_layout["sex"])
	quit()
