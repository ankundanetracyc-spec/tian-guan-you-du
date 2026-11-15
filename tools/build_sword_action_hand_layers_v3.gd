extends SceneTree
const PACK := "res://textures/characters/sword_action_hands_20261024_v3/"
const REVIEW := "res://docs/美术规范/剑动作手部分层修正_20261024/"
const SKIN := "res://textures/characters/side_ink_sword_idles_20261023_v2/source/male_held_idle.png"
const SEAL := "res://docs/美术规范/剑修基础动作设计_20261024/nurture.png"
const RIGHT_WRIST := Vector2(822,710)
const RIGHT_GRIP := Vector2(839,735)
const FIST_WRIST := Vector2(525,652)
const SEAL_WRIST := Vector2(719,482)
const SCALE := 2048.0/1254.0
var skin: Texture2D
var seal: Texture2D
var contours: Dictionary
var poses := {
	"cut":{"right":[714,642],"angle":25.0,"left":[418,620],"left_axis":51.0,"left_pose":"fist","ground_shift":-40},
	"finish":{"right":[750,548],"angle":0.0,"left":[386,657],"left_axis":52.0,"left_pose":"fist","ground_shift":97},
	"nurture":{"right":[556,504],"angle":0.0,"left":[720,484],"left_axis":-114.0,"left_pose":"seal","ground_shift":-66},
	"parry":{"right":[751,450],"angle":-43.0,"left":[805,563],"left_axis":-110.0,"left_pose":"seal","ground_shift":-89}
}
func _initialize() -> void:
	call_deferred("_build")
func _texture(path: String) -> Texture2D:
	return ImageTexture.create_from_image(Image.load_from_file(ProjectSettings.globalize_path(path)))
func _build() -> void:
	skin=_texture(SKIN)
	seal=_texture(SEAL)
	contours={
		"right":[[818,696],[832,697],[846,704],[857,713],[869,729],[873,745],[863,761],[850,770],[838,767],[827,763],[817,754],[810,743],[811,732],[820,718]],
		"fist":[[520,644],[530,638],[552,639],[577,649],[593,662],[596,674],[588,686],[577,693],[562,699],[551,695],[541,690],[533,683],[523,674],[518,660]],
		"seal":[[707,485],[720,488],[731,479],[735,464],[730,449],[717,436],[701,414],[688,397],[680,392],[671,393],[666,400],[669,412],[678,428],[670,435],[668,444],[674,459],[685,474]],
		"fingers":[[817.51,734.33],[819.12,731.12],[822.86,728.98],[827.15,729.52],[829.29,732.19],[832.50,732.19],[836.24,733.26],[837.31,735.40],[840.52,734.87],[843.74,736.47],[845.34,739.15],[848.02,740.22],[851.23,743.43],[851.76,751.46],[853.37,756.81],[854.44,762.69],[850.69,765.91],[846.41,766.98],[842.67,764.83],[839.99,762.16],[835.17,761.09],[831.43,761.62],[828.22,759.48],[827.15,757.88],[821.79,756.81],[818.58,754.13],[816.44,750.39],[816.44,744.50]],
		"thumb":[[857.65,720.95],[862.47,725.77],[865.68,731.66],[866.21,738.61],[863.54,746.64],[859.79,752.53],[857.11,754.67],[854.44,753.60],[852.30,750.39],[852.83,744.50],[853.37,737.54],[854.97,732.73]]
	}
	if "--canon-only" in OS.get_cmdline_user_args():
		await _canon_review()
		quit()
		return
	var subtraction := Shader.new()
	subtraction.code="shader_type canvas_item; uniform sampler2D front; void fragment(){vec4 b=texture(TEXTURE,UV);float a=texture(front,UV).a;COLOR=vec4(b.rgb,clamp((b.a-a)/max(1.0-a,0.0001),0.0,1.0));}"
	var icon := _texture("res://textures/ui/forged_swords_20261023_v1/slender_sword.png")
	var icon_grip := Vector2(.874,.512)*icon.get_size()
	var icon_axis := (Vector2(.024,.505)-Vector2(.874,.512))*icon.get_size()
	for pose in poses:
		var info: Dictionary=poses[pose]
		var view := SubViewport.new()
		view.size=Vector2i(2048,2048)
		view.transparent_bg=true
		view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
		root.add_child(view)
		var content := Node2D.new()
		content.scale=Vector2.ONE*SCALE
		content.position=Vector2(0,info["ground_shift"])
		view.add_child(content)
		var body := Sprite2D.new()
		body.centered=false
		body.texture=_texture(PACK+"source/"+pose+"_nohands.png")
		content.add_child(body)
		_add_hands(content,info,false)
		var all := await _frame(view)
		for child in content.get_children(): child.queue_free()
		await process_frame
		_add_hands(content,info,true)
		var front := await _frame(view)
		front.save_png(PACK+pose+"_hand_front.png")
		content.queue_free()
		await process_frame
		var back_sprite := Sprite2D.new()
		back_sprite.centered=false
		back_sprite.texture=ImageTexture.create_from_image(all)
		var material := ShaderMaterial.new()
		material.shader=subtraction
		material.set_shader_parameter("front",ImageTexture.create_from_image(front))
		back_sprite.material=material
		view.add_child(back_sprite)
		var back := await _frame(view)
		back.save_png(PACK+pose+"_body_back.png")
		back_sprite.material=null
		back_sprite.texture=ImageTexture.create_from_image(back)
		var angle := deg_to_rad(float(info["angle"]))
		var raw_grip := Vector2(info["right"][0],info["right"][1])+(RIGHT_GRIP-RIGHT_WRIST).rotated(angle-deg_to_rad(45))
		var grip := raw_grip*SCALE+Vector2(0,info["ground_shift"])
		info["grip_canvas"]=[grip.x,grip.y]
		var sword := Sprite2D.new()
		sword.centered=false
		sword.texture=icon
		sword.offset=-icon_grip
		sword.position=grip
		sword.rotation=angle-icon_axis.angle()
		sword.scale=Vector2.ONE*780.0/icon_axis.length()
		view.add_child(sword)
		var front_sprite := Sprite2D.new()
		front_sprite.centered=false
		front_sprite.texture=ImageTexture.create_from_image(front)
		view.add_child(front_sprite)
		var assembled := await _frame(view)
		assembled.save_png(PACK+pose+"_assembled.png")
		var region := Rect2i(Vector2i(grip)-Vector2i(170,170),Vector2i(340,340))
		assembled.get_region(region).save_png(REVIEW+pose+"_grip_detail.png")
		view.queue_free()
		await process_frame
	FileAccess.open(PACK+"layer_layout.json",FileAccess.WRITE).store_string(JSON.stringify({"canvas":[2048,2048],"ground":[1024,1900],"reference_height":1375,"character_layers":["body_back","hand_front"],"render_order":["body_back","independent sword","hand_front"],"hand_source":SKIN,"left_hand_chirality":"Reflection about the forearm axis, with wrist fixed; never mirrored whole character.","poses":poses,"contours":contours},"\t"))
	await _canon_review()
	print("SWORD_ACTION_HAND_LAYERS_V3_READY")
	quit()
func _frame(view: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return view.get_texture().get_image()
func _piece(parent: Node,texture: Texture2D,contour: Array,from: Vector2,at: Vector2,angle: float,reflect: bool=false,axis: float=0) -> void:
	var uv := PackedVector2Array()
	var polygon := PackedVector2Array()
	for coords in contour:
		var point := Vector2(coords[0],coords[1])
		uv.append(point)
		var delta := (point-from).rotated(-axis)
		if reflect: delta.y=-delta.y
		polygon.append(delta.rotated(angle)+at)
	var piece := Polygon2D.new()
	piece.texture=texture
	piece.uv=uv
	piece.polygon=polygon
	piece.antialiased=true
	parent.add_child(piece)
func _add_hands(parent: Node,info: Dictionary,front_only: bool) -> void:
	var right := Vector2(info["right"][0],info["right"][1])
	var angle := deg_to_rad(float(info["angle"])-45)
	if front_only:
		_piece(parent,skin,contours["fingers"],RIGHT_WRIST,right,angle)
		_piece(parent,skin,contours["thumb"],RIGHT_WRIST,right,angle)
	else:
		_piece(parent,skin,contours["right"],RIGHT_WRIST,right,angle)
		var left := Vector2(info["left"][0],info["left"][1])
		if info["left_pose"]=="fist": _piece(parent,skin,contours["fist"],FIST_WRIST,left,deg_to_rad(info["left_axis"]),true,deg_to_rad(30))
		else: _piece(parent,seal,contours["seal"],SEAL_WRIST,left,deg_to_rad(info["left_axis"]),true,deg_to_rad(-121.5))
func _canon_review() -> void:
	root.mode=Window.MODE_WINDOWED
	root.position=Vector2i(-4096,-4096)
	root.size=Vector2i(1500,650)
	root.content_scale_size=Vector2i(1500,650)
	root.content_scale_mode=Window.CONTENT_SCALE_MODE_DISABLED
	var scene := Node2D.new()
	root.add_child(scene)
	var background := Polygon2D.new()
	background.polygon=PackedVector2Array([Vector2.ZERO,Vector2(1500,0),Vector2(1500,650),Vector2(0,650)])
	background.color=Color("ddd5bd")
	scene.add_child(background)
	var specs := [["右手 · 握柄",Vector2(90,260),"right"],["左手 · 收拳",Vector2(650,260),"fist"],["左手 · 剑诀",Vector2(1190,440),"seal"]]
	for spec in specs:
		var label := Label.new()
		label.text=spec[0]
		label.position=Vector2(spec[1].x-40,60)
		label.add_theme_font_size_override("font_size",28)
		label.add_theme_color_override("font_color",Color("30433e"))
		scene.add_child(label)
		var hand := Node2D.new()
		hand.position=spec[1]
		hand.scale=Vector2.ONE*4
		scene.add_child(hand)
		match spec[2]:
			"right": _piece(hand,skin,contours["right"],RIGHT_WRIST,Vector2.ZERO,0)
			"fist": _piece(hand,skin,contours["fist"],FIST_WRIST,Vector2.ZERO,0,true,deg_to_rad(30))
			"seal": _piece(hand,seal,contours["seal"],SEAL_WRIST,Vector2.ZERO,deg_to_rad(-90),true,deg_to_rad(-121.5))
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(REVIEW+"canonical_hands.png")
