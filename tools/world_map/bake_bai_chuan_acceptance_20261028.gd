extends SceneTree

const BASE := "res://textures/world/bai_chuan/acceptance_v1/"
const DOC := "res://docs/美术规范/百川整图与节点落位验收_20261028/"

class GroundInk extends Node2D:
	var layout: Dictionary
	func points(a: Array) -> PackedVector2Array:
		var ps := PackedVector2Array()
		for p: Array in a: ps.append(Vector2(float(p[0]),float(p[1])))
		return ps
	func smooth_loop(a: Array) -> PackedVector2Array:
		var ps := points(a)
		var out := PackedVector2Array()
		for i in ps.size():
			var n := ps.size()
			for j in 12:
				var t := j / 12.0
				var p0 := ps[(i-1+n)%n]
				var p1 := ps[i]
				var p2 := ps[(i+1)%n]
				var p3 := ps[(i+2)%n]
				out.append(0.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
		return out
	func _draw() -> void:
		for lake: Dictionary in layout.lakes:
			var ps := smooth_loop(lake.points)
			draw_colored_polygon(ps, Color("a1aca3"))
			var edge := ps.duplicate()
			edge.append(ps[0])
			draw_polyline(edge,Color(0.43,0.43,0.35,0.58),2.0,true)
		for water: Dictionary in layout.waters:
			var ps := points(water.points)
			draw_polyline(ps,Color(0.45,0.43,0.35,0.45),float(water.width)+4.0,true)
		for water: Dictionary in layout.waters:
			var ps := points(water.points)
			draw_polyline(ps,Color("a1aca3"),float(water.width),true)
			draw_circle(ps[0],float(water.width)/2.0,Color("a1aca3"))
			draw_circle(ps[-1],float(water.width)/2.0,Color("a1aca3"))

class BridgeInk extends Node2D:
	var bridges: Array
	func _draw() -> void:
		for b: Dictionary in bridges:
			var p := Vector2(b.pos[0],b.pos[1])
			var length := float(b.length)
			draw_set_transform(p,float(b.angle))
			var rect := Rect2(-length/2.0,-7.0,length,14.0)
			draw_rect(rect,Color("c4b28a"))
			draw_rect(rect,Color("655a43"),false,1.1,true)
			for x in range(-int(length/2)+5,int(length/2),7):
				draw_line(Vector2(x,-5),Vector2(x,5),Color(0.40,0.35,0.26,0.65),.9,true)
			draw_line(Vector2(-length/2,-9),Vector2(length/2,-9),Color("78684d"),1.2,true)
			draw_line(Vector2(-length/2,9),Vector2(length/2,9),Color("78684d"),1.2,true)
			draw_set_transform(Vector2.ZERO)

class ShelfInk extends Node2D:
	var sites: Array
	func _draw() -> void:
		for p: Dictionary in sites:
			if not p.has("shelf"): continue
			var r: Array = p.rect
			var x := float(r[0]); var y := float(r[1]); var w := float(r[2]); var h := float(r[3])
			var anchor := Vector2(p.anchor[0],p.anchor[1])
			draw_polyline(PackedVector2Array([Vector2(x-17,y+h*.56),Vector2(x-32,y+h*.84),Vector2(x-21,anchor.y+17)]),Color(0.39,0.35,0.25,0.40),1.1,true)
			draw_polyline(PackedVector2Array([Vector2(x+w+14,y+h*.58),Vector2(x+w+31,y+h*.86),Vector2(x+w+19,anchor.y+19)]),Color(0.39,0.35,0.25,0.40),1.1,true)
			for i in 4:
				var yy := anchor.y+3+i*3.0
				draw_line(Vector2(anchor.x-5,yy),Vector2(anchor.x+7,yy),Color(0.44,0.39,0.29,0.5),.8,true)

func _initialize() -> void:
	_run.call_deferred()

func _texture(path: String) -> Texture2D:
	var im := Image.new()
	var error := im.load_png_from_buffer(FileAccess.get_file_as_bytes(path))
	assert(error == OK,path)
	return ImageTexture.create_from_image(im)

func _stamp(parent: Node, item: Dictionary, catalog: Dictionary, cache: Dictionary) -> void:
	var key: String = item.asset
	if not cache.has(key): cache[key]=_texture(catalog[key].file)
	var sprite := Sprite2D.new()
	sprite.texture=cache[key]
	sprite.centered=false
	sprite.position=Vector2(item.pos[0],item.pos[1])
	sprite.flip_h=item.get("flip",false)
	sprite.modulate.a=float(item.get("alpha",1))
	parent.add_child(sprite)

func _run() -> void:
	var layout: Dictionary=JSON.parse_string(FileAccess.get_file_as_string(BASE+"presentation.json"))
	var size := int(layout.worldSize)
	var view := SubViewport.new()
	view.size=Vector2i(size,size)
	view.disable_3d=true
	view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	var world := Node2D.new()
	view.add_child(world)
	var ground := ColorRect.new()
	ground.size=Vector2(size,size)
	var material := ShaderMaterial.new()
	material.shader=load("res://scripts/world/bai_chuan/BaiChuanGroundPaper.gdshader")
	material.set_shader_parameter("paper",_texture(BASE+"source/ground.png"))
	ground.material=material
	world.add_child(ground)
	var water := GroundInk.new()
	water.layout=layout
	var water_material := ShaderMaterial.new()
	water_material.shader=load("res://scripts/world/bai_chuan/BaiChuanWaterInk.gdshader")
	water_material.set_shader_parameter("paper",_texture(BASE+"source/ground.png"))
	water.material=water_material
	var cache := {}
	for item: Dictionary in layout.terrain: _stamp(world,item,layout.catalog,cache)
	var shelves := ShelfInk.new()
	shelves.sites=layout.placements
	world.add_child(shelves)
	for item: Dictionary in layout.forests: _stamp(world,item,layout.catalog,cache)
	# Water cuts through slopes at the bank; a wide shoulder must not hide a
	# continuous river. All landmark buildings are drawn later at runtime.
	world.add_child(water)
	var bridges := BridgeInk.new()
	bridges.bridges=layout.bridges
	world.add_child(bridges)
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	var image := view.get_texture().get_image()
	assert(image.get_size()==Vector2i(size,size))
	image.save_png(DOC+"百川地表母图_6144.png")
	for row in 3:
		for col in 3:
			var tile:=image.get_region(Rect2i(col*2048,row*2048,2048,2048))
			tile.save_png(BASE+"tiles/bc_%d%d.png" % [row+1,col+1])
	image.resize(1024,1024,Image.INTERPOLATE_LANCZOS)
	image.save_png(BASE+"overview.png")
	print("BAICHUAN_BAKE_DONE 6144 / 9 x 2048 native raster composition")
	quit()
