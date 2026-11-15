extends Node2D

## Separate four-way appearance prototype. No game state or combat rule writes.
const ART_FOLDER := "art/pawn_four_way_20261105"
const KEYS := ["front","right","back","left"]
const BODY_HEIGHT := 48.0
const HEAD_HEIGHT := 38.0
const HAND_DIAMETER := 14.0
const HAND_GAP := 4.0
const CLEAN_ALPHA := """shader_type canvas_item;
render_mode unshaded;
varying vec4 pawn_tint;
varying vec2 pawn_pixel;
uniform vec2 native_at = vec2(0.0);
uniform float native_scale = 1.0;
uniform float cap_side = 0.0;
uniform float cap_x = 0.0;
uniform float cap_bottom = -45.0;
uniform float front_side = 0.0;
uniform float ear_x = 0.0;
void vertex() {
 pawn_tint = COLOR;
 pawn_pixel = VERTEX * native_scale + native_at;
}
void fragment() {
 vec4 pixel = texture(TEXTURE,UV);
 pixel.a = smoothstep(0.78,0.94,pixel.a);
 if (abs(front_side)>0.5) {
  pixel.a *= smoothstep(-0.8,0.8,(pawn_pixel.x-ear_x)*front_side);
 }
 if (abs(cap_side)>0.5) {
  pixel.a *= 1.0-smoothstep(-0.4,0.4,(pawn_pixel.x-cap_x)*cap_side);
  pixel.a *= 1.0-smoothstep(cap_bottom-0.4,cap_bottom+0.4,pawn_pixel.y);
 }
 COLOR = pixel * pawn_tint;
}
"""
static var _shared_assets: Dictionary = {}
var visual: Node2D
var layers: Dictionary = {}
var assets: Dictionary = {}
var art: Dictionary = {}
var errors: Array[String] = []
var profile: Dictionary = {}
var supported := false
var loaded := false
var direction := "front"
var front_style := 0
var back_style := 0
var phase := 0.0
var clock := 0.0
var moving := false
var body_width := 42.0
var layer_visibility := {"hair_front":true,"hair_back":true}
var render_registration: Dictionary = {}

func _folder() -> String:
	return get_script().resource_path.get_base_dir()

func _asset_path(file: String) -> String:
	return _folder().path_join(ART_FOLDER).path_join(file)

func _bounds(image: Image, region: Rect2i) -> Rect2:
	var low := region.end
	var high := region.position-Vector2i.ONE
	for y in range(region.position.y,region.end.y):
		for x in range(region.position.x,region.end.x):
			if image.get_pixel(x,y).a<0.88: continue
			low.x = mini(low.x,x)
			low.y = mini(low.y,y)
			high.x = maxi(high.x,x)
			high.y = maxi(high.y,y)
	if high.x<low.x: return Rect2()
	return Rect2(Vector2(low-region.position),Vector2(high-low+Vector2i.ONE))

func _load_sheet(key: String, file: String) -> bool:
	var path := _asset_path(file)
	if _shared_assets.has(path):
		if _shared_assets[path].sha256==FileAccess.get_sha256(path):
			assets[key] = _shared_assets[path]
			return true
		_shared_assets.erase(path)
	var image := Image.load_from_file(path)
	if image==null:
		errors.append("Missing pawn art: "+path)
		return false
	var cell := Vector2i(image.get_width()/4,image.get_height()/4)
	var regions := []
	for row in range(3):
		var columns := []
		for col in range(4):
			var rect := Rect2i(Vector2i(col,row)*cell,cell)
			if key=="base":
				# The generated component rows have wide transparent gutters.
				# Exclude a neighbouring row's opaque hem from the bald-head crop.
				var top: int = [0,350,660][row]
				var height: int = [350,280,300][row]
				rect = Rect2i(col*cell.x,top,cell.x,height)
			columns.append({"region":rect,"bounds":_bounds(image,rect)})
		regions.append(columns)
	image.generate_mipmaps()
	var record := {"texture":ImageTexture.create_from_image(image),"size":image.get_size(),"cell":cell,"regions":regions,"path":path,"sha256":FileAccess.get_sha256(path)}
	_shared_assets[path] = record
	assets[key] = record
	return true

func build() -> bool:
	if loaded: return true
	var good := true
	for pair in [["base","base_parts.png"],["front","hair_front.png"],["back","hair_back.png"]]:
		if not _load_sheet(pair[0],pair[1]): good = false
	if not good: return false
	var registration_path := _asset_path("registration.json")
	if FileAccess.file_exists(registration_path):
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(registration_path))
		if parsed is Dictionary: art = parsed
	visual = Node2D.new()
	visual.name = "PawnLayers"
	add_child(visual)
	var shader := Shader.new()
	shader.code = CLEAN_ALPHA
	for name_value in ["HairBack","Body","Head","HairBackCap","HairFront","HandLeft","HandRight"]:
		var sprite := Sprite2D.new()
		sprite.name = name_value
		sprite.centered = false
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		var material := ShaderMaterial.new()
		material.shader = shader
		sprite.material = material
		visual.add_child(sprite)
		layers[name_value] = sprite
	loaded = true
	set_appearance({"gender_name":"male","appearance_clothes_style":5,"appearance_hair_style":0,"appearance_hair_back_style":0,"source_label":"unbound prototype art"})
	return true

func _place(name_value: String, sheet: String, row: int, col: int, anchor: Vector2, at: Vector2, scale_value: float, z: int) -> void:
	var record: Dictionary = assets[sheet]
	var atlas := AtlasTexture.new()
	atlas.atlas = record.texture
	atlas.region = record.regions[row][col].region
	atlas.filter_clip = true
	var sprite: Sprite2D = layers[name_value]
	sprite.texture = atlas
	sprite.offset = -anchor
	sprite.position = at
	sprite.scale = Vector2.ONE*scale_value
	sprite.z_index = z
	var material: ShaderMaterial = sprite.material
	material.set_shader_parameter("native_at",at)
	material.set_shader_parameter("native_scale",scale_value)
	render_registration[name_value] = {"sheet":sheet,"row":row,"column":col,"anchor":anchor,"at":at,"scale":scale_value,"z":z,"region":atlas.region}

func _vec(value: Variant, fallback: Vector2) -> Vector2:
	if value is Array and value.size()==2: return Vector2(float(value[0]),float(value[1]))
	return value if value is Vector2 else fallback

func _hair_registration(part: String, style: int, col: int) -> Dictionary:
	var cell: Vector2 = assets[part].cell
	var custom: Dictionary = art.get(part,{}).get(str(style),{}).get(KEYS[col],{})
	return {"anchor":_vec(custom.get("anchor"),Vector2(cell.x*0.5,cell.y*0.58)),"offset":_vec(custom.get("offset"),Vector2.ZERO),"scale":float(custom.get("scale",HEAD_HEIGHT/(cell.y*0.4)))}

func _render() -> void:
	if not loaded: return
	visual.visible = supported
	if not supported: queue_redraw();return
	var col := KEYS.find(direction)
	var body_bounds: Rect2 = assets.base.regions[0][col].bounds
	var head_bounds: Rect2 = assets.base.regions[1][col].bounds
	var hand_bounds: Rect2 = assets.base.regions[2][0].bounds
	var body_scale := BODY_HEIGHT/maxf(1.0,body_bounds.size.y)
	body_width = body_bounds.size.x*body_scale
	_place("Body","base",0,col,Vector2(body_bounds.get_center().x,body_bounds.end.y),Vector2.ZERO,body_scale,20)
	var head_at := Vector2(4.0 if direction=="right" else (-4.0 if direction=="left" else 0.0),-BODY_HEIGHT+3.0)
	var head_scale := HEAD_HEIGHT/maxf(1.0,head_bounds.size.y)
	_place("Head","base",1,col,Vector2(head_bounds.get_center().x,head_bounds.end.y),head_at,head_scale,30)
	var front := _hair_registration("front",front_style,col)
	var back := _hair_registration("back",back_style,col)
	_place("HairBack","back",back_style,col,back.anchor,head_at+back.offset,float(back.scale),45 if direction=="back" else 10)
	# In profile the rear scalp covers the rear half of the skull, while its
	# long section remains behind the robe. One shared rear sprite supplies
	# both passes, so the two independently selected hairstyles stay aligned.
	_place("HairBackCap","back",back_style,col,back.anchor,head_at+back.offset,float(back.scale),35)
	var cap_material: ShaderMaterial = layers.HairBackCap.material
	var side_sign := 1.0 if direction=="right" else -1.0
	cap_material.set_shader_parameter("cap_side",side_sign)
	# The approved profile head places the ear at the horizontal head anchor.
	# Rear scalp and front hair overlap slightly at this anatomical divide.
	cap_material.set_shader_parameter("cap_x",head_at.x+side_sign*1.0)
	cap_material.set_shader_parameter("cap_bottom",head_at.y+1.0)
	_place("HairFront","front",front_style,col,front.anchor,head_at+front.offset,float(front.scale),35 if direction=="back" else 40)
	var front_material: ShaderMaterial = layers.HairFront.material
	front_material.set_shader_parameter("front_side",side_sign if direction in ["left","right"] else 0.0)
	front_material.set_shader_parameter("ear_x",head_at.x)
	var hand_scale := HAND_DIAMETER/maxf(hand_bounds.size.x,hand_bounds.size.y)
	for item in _hand_placements():
		_place(item.name,"base",2,0,hand_bounds.get_center(),item.at,hand_scale,item.z)
	layers.HairFront.visible = layer_visibility.hair_front and direction!="back"
	layers.HairBack.visible = layer_visibility.hair_back
	layers.HairBackCap.visible = layer_visibility.hair_back and direction in ["left","right"]
	_apply_motion()
	queue_redraw()

func set_appearance(value: Dictionary) -> void:
	profile = value.duplicate(true)
	var gender: String = str(profile.get("gender_name",str(profile.get("gender",""))))
	if gender=="0": gender = "male"
	front_style = int(profile.get("appearance_hair_style",-1))
	back_style = int(profile.get("appearance_hair_back_style",-1))
	supported = gender=="male" and int(profile.get("appearance_clothes_style",-1))==5 and front_style in [0,1,2] and back_style in [0,1,2]
	if profile.has("asset_support"): supported = supported and bool(profile.asset_support.get("supported",false))
	_render()

func set_direction(value: Vector2) -> void:
	if value.is_zero_approx(): return
	var next := ("right" if value.x>0.0 else "left") if absf(value.x)>absf(value.y) else ("front" if value.y>0.0 else "back")
	if next==direction: return
	direction = next
	_render()

func set_layer_visible(part: String, visible_value: bool) -> void:
	if not layer_visibility.has(part): return
	layer_visibility[part] = visible_value
	if loaded:
		layers.HairFront.visible = layer_visibility.hair_front and direction!="back"
		layers.HairBack.visible = layer_visibility.hair_back
		layers.HairBackCap.visible = layer_visibility.hair_back and direction in ["left","right"]

func advance(delta: float, velocity: Vector2) -> void:
	if not loaded: return
	var dt := clampf(delta,0.0,0.05)
	clock += dt
	moving = velocity.length_squared()>0.001
	if moving: phase += velocity.length()*dt/40.0*TAU
	_apply_motion()
	queue_redraw()

func _apply_motion() -> void:
	if not loaded: return
	visual.position.y = -absf(sin(phase))*0.55 if moving else 0.0
	visual.rotation = sin(phase)*0.015 if moving else 0.0
	for item in _hand_placements():
		layers[item.name].position = item.at+Vector2(0,sin(phase)*item.phase_sign*1.5 if moving else 0.0)

func _hand_placements() -> Array[Dictionary]:
	var waist_y := -BODY_HEIGHT*0.49
	if direction in ["left","right"]:
		var side := 1.0 if direction=="right" else -1.0
		return [
			{"name":"HandLeft","at":Vector2(side*body_width*0.38,waist_y-3.0),"z":12,"phase_sign":-1.0},
			{"name":"HandRight","at":Vector2(side*(body_width*0.5+HAND_DIAMETER*0.5+2.0),waist_y),"z":80,"phase_sign":1.0},
		]
	var hand_x := body_width*0.5+HAND_DIAMETER*0.5+HAND_GAP
	return [
		{"name":"HandLeft","at":Vector2(-hand_x,waist_y),"z":80,"phase_sign":-1.0},
		{"name":"HandRight","at":Vector2(hand_x,waist_y),"z":80,"phase_sign":1.0},
	]

func _draw() -> void:
	if not supported: return
	var points := PackedVector2Array()
	for index in range(40):
		var angle := float(index)*TAU/40.0
		points.append(Vector2(cos(angle)*(body_width*0.48),sin(angle)*2.0))
	draw_colored_polygon(points,Color(0.12,0.16,0.13,0.09))

func get_diagnostics() -> Dictionary:
	var ids := {}
	var parts: Dictionary = profile.get("parts",profile.get("resolved",{}).get("parts",{}))
	for part in ["clothes","hair_front","hair_back"]:
		ids[part] = {"id":str(parts.get(part,{}).get("id","")),"source_png":str(parts.get(part,{}).get("resource_path",""))}
	var sheets := {}
	for key in assets: sheets[key] = {"path":assets[key].path,"sha256":assets[key].sha256,"size":assets[key].size}
	return {"loaded":loaded,"supported":supported,"errors":errors.duplicate(),"direction":direction,"front_style":front_style,"back_style":back_style,"clothes_style":int(profile.get("appearance_clothes_style",-1)),"profile_key":profile.get("profile_key",""),"source_label":profile.get("source_label",""),"parts":ids,"layers":render_registration.duplicate(true),"atlas_sources":sheets,"show_front":layer_visibility.hair_front,"show_back":layer_visibility.hair_back,"body_height":BODY_HEIGHT,"body_width":body_width,"head_height":HEAD_HEIGHT,"hand_diameter":HAND_DIAMETER,"hand_body_gap":HAND_GAP,"moving":moving,"clock":clock,"phase":phase,"body_bob_y":visual.position.y if loaded else 0.0,"writes_game_state":false,"scope":"four-way separated portrait-driven front/rear hair, one robe, detached circle hands; isolated prototype"}
