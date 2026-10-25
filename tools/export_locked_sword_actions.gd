extends SceneTree

var PACK := "res://textures/characters/side_ink_sword_actions_20261024_v5/"
var only_pose := ""
const REVIEW := "res://docs/美术规范/剑修动作重新生成_20261024/"
const ART_SCALE := 1790.0 / 1254.0
const GROUND := Vector2(1024,1900)
var settings := {
	"cut": {"grip":[841,717],"tip":[1192,1007],"ground":[627,1192],"guard_distance":48.0,"hand_rect":[809,674,72,80],"front":[[813,716],[827,712],[839,716],[847,718],[857,716],[872,718],[876,747],[853,756],[828,744],[812,732]]},
	"parry": {"grip":[746,514],"tip":[1136,105],"ground":[627,1215],"guard_distance":52.0,"hand_rect":[697,477,88,70],"front":[[741,490],[777,484],[788,498],[779,514],[765,529],[745,546],[730,543],[729,531],[737,516]]},
	"nurture": {"grip":[559,680],"tip":[1094,680],"ground":[627,1215],"guard_distance":48.0,"hand_rect":[518,638,79,67],"front":[[526,669],[539,665],[553,668],[566,666],[581,667],[594,665],[599,703],[523,705]]},
	"finish": {"grip":[835,545],"tip":[1238,540],"ground":[627,1115],"guard_distance":38.0,"hand_rect":[807,509,59,62],"front":[[810,539],[828,535],[846,535],[857,533],[866,539],[868,568],[807,568]],"thumb":[[833,513],[845,514],[856,525],[864,541],[866,551],[858,555],[850,548],[845,535]]}
}

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--pack-root="): PACK=argument.trim_prefix("--pack-root=").trim_suffix("/")+"/"
		if argument.begins_with("--only="): only_pose=argument.trim_prefix("--only=")
	if FileAccess.file_exists(PACK+"registration_overrides.json"):
		var overrides: Dictionary=JSON.parse_string(FileAccess.get_file_as_string(PACK+"registration_overrides.json"))
		settings.merge(overrides,true)
	call_deferred("_export")

func _texture(path: String) -> Texture2D:
	return ImageTexture.create_from_image(Image.load_from_file(ProjectSettings.globalize_path(path)))

func _frame(view: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	var result := view.get_texture().get_image()
	# Viewport RGBA is premultiplied; PNG layers are composed as straight
	# alpha. Restore edge RGB before saving or using it as another texture.
	var used := result.get_used_rect()
	for y in range(used.position.y,used.end.y):
		for x in range(used.position.x,used.end.x):
			var c := result.get_pixel(x,y)
			if c.a>0.001 and c.a<0.999:
				var linear := c.srgb_to_linear()
				linear.r=minf(linear.r/c.a,1)
				linear.g=minf(linear.g/c.a,1)
				linear.b=minf(linear.b/c.a,1)
				var straight := linear.linear_to_srgb()
				straight.a=c.a
				result.set_pixel(x,y,straight)
	return result

func _view(size: Vector2i) -> SubViewport:
	var result := SubViewport.new()
	result.size=size
	result.transparent_bg=true
	result.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(result)
	return result

func _sprite(parent: Node,texture: Texture2D) -> Sprite2D:
	var result := Sprite2D.new()
	result.centered=false
	result.texture=texture
	parent.add_child(result)
	return result

func _polygon(points: Array) -> PackedVector2Array:
	var result := PackedVector2Array()
	for p in points: result.append(Vector2(p[0],p[1]))
	return result

func _quad(start: Vector2,end: Vector2,half_width: float) -> PackedVector2Array:
	var normal := (end-start).normalized().orthogonal()*half_width
	return PackedVector2Array([start-normal,start+normal,end+normal,end-normal])

func _mask_polygon(parent: Node,points: PackedVector2Array) -> void:
	var p := Polygon2D.new()
	p.polygon=points
	p.color=Color.WHITE
	p.antialiased=true
	parent.add_child(p)

func _skin_mask(source: Image,area: Rect2i) -> Image:
	var mask := Image.create(source.get_width(),source.get_height(),false,Image.FORMAT_RGBA8)
	for y in range(area.position.y,area.end.y):
		for x in range(area.position.x,area.end.x):
			var c := source.get_pixel(x,y)
			if c.a>.3 and c.r>.55 and c.g>.3 and c.r-c.g>.105 and c.g-c.b>.07 and c.r-c.g>(c.g-c.b)*.8:
				for dy in range(-2,3):
					for dx in range(-2,3):
						if dx*dx+dy*dy<=5:
							mask.set_pixel(x+dx,y+dy,Color.WHITE)
	return mask

func _keep_character_component(source: Image,seed: Vector2i) -> void:
	var width := source.get_width()
	var height := source.get_height()
	var connected := PackedByteArray()
	connected.resize(width*height)
	var queue := PackedInt32Array([seed.y*width+seed.x])
	connected[queue[0]]=1
	var head := 0
	while head<queue.size():
		var index := queue[head]
		head+=1
		var x := index%width
		var y: int=index/width
		for dy in range(-1,2):
			for dx in range(-1,2):
				var nx := x+dx
				var ny := y+dy
				if nx<0 or nx>=width or ny<0 or ny>=height: continue
				var next := ny*width+nx
				if connected[next]==0 and source.get_pixel(nx,ny).a>.32:
					connected[next]=1
					queue.append(next)
	var retained := connected.duplicate()
	for index in queue:
		var x := index%width
		var y: int=index/width
		for dy in range(-2,3):
			for dx in range(-2,3):
				var nx := x+dx
				var ny := y+dy
				if nx>=0 and nx<width and ny>=0 and ny<height: retained[ny*width+nx]=1
	for y in range(height):
		for x in range(width):
			if retained[y*width+x]==0 or source.get_pixel(x,y).a<.12:
				source.set_pixel(x,y,Color.TRANSPARENT)

func _export() -> void:
	var icon := _texture("res://textures/ui/forged_swords_20261023_v1/slender_sword.png")
	var icon_grip := Vector2(.874,.512)*icon.get_size()
	var icon_axis := (Vector2(.024,.505)-Vector2(.874,.512))*icon.get_size()
	var exported_poses := {}
	if only_pose!="" and FileAccess.file_exists(PACK+"layer_layout.json"):
		var previous_layout: Dictionary=JSON.parse_string(FileAccess.get_file_as_string(PACK+"layer_layout.json"))
		exported_poses=previous_layout.get("poses",{}).duplicate(true)
	for pose in settings:
		if only_pose!="" and pose!=only_pose: continue
		var info: Dictionary=settings[pose]
		if not FileAccess.file_exists(PACK+"source/"+pose+"_no_sword.png"):
			push_error("Missing swordless source: "+pose)
			quit(1)
			return
		var original := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/"+str(info.get("source_image",pose+".png"))))
		var original_texture := ImageTexture.create_from_image(original)
		var clean := _texture(PACK+"source/"+pose+"_no_sword.png")
		var grip := Vector2(info["grip"][0],info["grip"][1])
		var tip := Vector2(info["tip"][0],info["tip"][1])
		var axis := (tip-grip).normalized()
		var guard: Vector2=grip+axis*float(info["guard_distance"])
		var mask_view := _view(Vector2i(original.get_size()))
		_mask_polygon(mask_view,_quad(grip-axis*67,guard+axis*8,float(info.get("hilt_half_width",20))))
		_mask_polygon(mask_view,_quad(guard-axis*float(info.get("guard_half_depth",11)),guard+axis*float(info.get("guard_half_depth",12)),float(info.get("guard_half_width",39))))
		_mask_polygon(mask_view,_quad(guard+axis*5,tip+axis*8,19))
		var weapon_mask := await _frame(mask_view)
		var removal_mask: Image=weapon_mask
		if info.get("already_swordless",false):
			removal_mask=weapon_mask.duplicate()
			removal_mask.fill(Color.TRANSPARENT)
		mask_view.queue_free()
		var raw_view := _view(Vector2i(original.get_size()))
		var combined := _sprite(raw_view,original_texture)
		var shader := Shader.new()
		shader.code="shader_type canvas_item; uniform sampler2D clean; uniform sampler2D removal; uniform float clean_scale=1.0; uniform vec2 clean_offset=vec2(0.0); void fragment(){vec4 original=texture(TEXTURE,UV); vec2 cuv=(UV-clean_offset)/clean_scale; vec4 replacement=texture(clean,cuv); if(replacement.r-replacement.g>.105 && replacement.g-replacement.b>.07 && replacement.r-replacement.g>(replacement.g-replacement.b)*.8){replacement=vec4(0.0);} if(any(lessThan(cuv,vec2(0.0)))||any(greaterThan(cuv,vec2(1.0)))){replacement=vec4(0.0);} float m=texture(removal,UV).a; COLOR=mix(original,replacement,m);}"
		var material := ShaderMaterial.new()
		material.shader=shader
		material.set_shader_parameter("clean",clean)
		material.set_shader_parameter("removal",ImageTexture.create_from_image(removal_mask))
		var cleanup_registration := {"cut":[.818,85,176],"parry":[1,0,0],"nurture":[.833,78,179],"finish":[.821,45,151]}
		var reg: Array=info.get("cleanup_registration",cleanup_registration.get(pose,[1,0,0]))
		material.set_shader_parameter("clean_scale",reg[0])
		material.set_shader_parameter("clean_offset",Vector2(reg[1],reg[2])/Vector2(original.get_size()))
		combined.material=material
		var rect_values: Array=info["hand_rect"]
		var hand_mask := _skin_mask(original,Rect2i(rect_values[0],rect_values[1],rect_values[2],rect_values[3]))
		var restore := _sprite(raw_view,original_texture)
		var restore_shader := Shader.new()
		restore_shader.code="shader_type canvas_item; uniform sampler2D hand; void fragment(){vec4 source=texture(TEXTURE,UV); source.a*=texture(hand,UV).a; COLOR=source;}"
		var restore_material := ShaderMaterial.new()
		restore_material.shader=restore_shader
		restore_material.set_shader_parameter("hand",ImageTexture.create_from_image(hand_mask))
		restore.material=restore_material
		var full_body := await _frame(raw_view)
		if pose=="cut" and not info.get("already_swordless",false):
			# A detached ring left by the weapon removal is not part of the
			# hand or cuff. Remove it before the character silhouette cleanup.
			for y in range(700,718):
				for x in range(801,817):
					if Vector2((x-809.0)/8.0,(y-709.0)/9.0).length()<1:
						full_body.set_pixel(x,y,Color.TRANSPARENT)
		_keep_character_component(full_body,Vector2i(600,620))
		raw_view.queue_free()
		var front_mask_view := _view(Vector2i(original.get_size()))
		_mask_polygon(front_mask_view,_polygon(info["front"]))
		if info.has("thumb"): _mask_polygon(front_mask_view,_polygon(info["thumb"]))
		var front_region := await _frame(front_mask_view)
		front_mask_view.queue_free()
		var front: Image=original.duplicate()
		for y in range(original.get_height()):
			for x in range(original.get_width()):
				var c := original.get_pixel(x,y)
				c.a*=hand_mask.get_pixel(x,y).a*maxf(front_region.get_pixel(x,y).a,weapon_mask.get_pixel(x,y).a)
				front.set_pixel(x,y,c)
		# Opaque overlap keeps the hand continuous under filtering. The near
		# fingers overdraw matching pixels; the hand is not cut into two holes.
		var back: Image=full_body.duplicate()
		var ground := Vector2(info["ground"][0],info["ground"][1])
		var pose_scale: float=float(info.get("source_scale",ART_SCALE))
		var shift := GROUND-ground*pose_scale
		var body_view := _view(Vector2i(2048,2048))
		var body_sprite := _sprite(body_view,ImageTexture.create_from_image(back))
		body_sprite.position=shift
		body_sprite.scale=Vector2.ONE*pose_scale
		var body_canvas := await _frame(body_view)
		body_canvas.save_png(PACK+pose+"_body_back.png")
		body_sprite.texture=ImageTexture.create_from_image(front)
		var front_canvas := await _frame(body_view)
		# At the internal split, both layers sample the same painted hand.
		# Copy the back layer's straight RGB to eliminate a filtered mask seam.
		var hand_bounds := front_canvas.get_used_rect()
		for y in range(hand_bounds.position.y,hand_bounds.end.y):
			for x in range(hand_bounds.position.x,hand_bounds.end.x):
				var c := front_canvas.get_pixel(x,y)
				if c.a>0:
					var painted := body_canvas.get_pixel(x,y)
					c.r=painted.r
					c.g=painted.g
					c.b=painted.b
					front_canvas.set_pixel(x,y,c)
		front_canvas.save_png(PACK+pose+"_hand_front.png")
		body_view.size=Vector2i(3072,2048)
		body_sprite.texture=ImageTexture.create_from_image(body_canvas)
		body_sprite.position=Vector2(512,0)
		body_sprite.scale=Vector2.ONE
		var sword := _sprite(body_view,icon)
		sword.offset=-icon_grip
		sword.position=grip*pose_scale+shift+Vector2(512,0)
		sword.rotation=axis.angle()-icon_axis.angle()
		sword.scale=Vector2.ONE*780/icon_axis.length()
		var near_fingers := _sprite(body_view,ImageTexture.create_from_image(front_canvas))
		near_fingers.position=Vector2(512,0)
		var assembled := await _frame(body_view)
		assembled.save_png(PACK+pose+"_assembled.png")
		var canvas_grip := grip*pose_scale+shift
		assembled.get_region(Rect2i(Vector2i(canvas_grip)+Vector2i(512,0)-Vector2i(150,150),Vector2i(300,300))).save_png(REVIEW+pose+"_layered_grip.png")
		info["grip_canvas"]=[canvas_grip.x,canvas_grip.y]
		info["weapon_hand"]=info.get("weapon_hand","right; confirmed by user, the old idle hand is being corrected")
		info["sword_angle_degrees"]=rad_to_deg(axis.angle())
		info["canvas_translation"]=[shift.x,shift.y]
		exported_poses[pose]=info
		body_view.queue_free()
		await process_frame
	var layout_scale: float=float(settings[only_pose].get("source_scale",ART_SCALE)) if only_pose!="" else ART_SCALE
	FileAccess.open(PACK+"layer_layout.json",FileAccess.WRITE).store_string(JSON.stringify({"canvas":[2048,2048],"ground":[1024,1900],"reference_height":1375,"source_scale":layout_scale,"render_order":["body_back","independent sword","hand_front"],"grip_to_tip":780,"poses":exported_poses},"\t"))
	print("LOCKED_CHARACTER_FOUR_ACTION_LAYERS_EXPORTED")
	quit()
