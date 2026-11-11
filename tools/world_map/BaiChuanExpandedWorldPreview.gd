extends Control
const MapRuntime = preload("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
const BoatVisual = preload("res://gdscripts/world/WorldSpiritBoatVisual.gd")
const VisualScale = preload("res://gdscripts/world/WorldMapVisualScale.gd")
const OUT := "res://output/bai_chuan_expanded3x_20261106/"
var map: Control
var boat: Node2D
var checks := 0
var failures := 0
var lines: Array[String] = []

func _ready() -> void:
	if not OS.get_environment("APPDATA").replace("\\","/").contains("bai_chuan_expanded3x_20261106/"):
		push_error("扩图验收必须隔离存档")
		get_tree().quit(2)
		return
	get_window().size = Vector2i(1920,1080)
	map = MapRuntime.new()
	map.name = "ActualExpandedMap"
	map.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(map)
	await _frames(5)
	_check(map.navigation.ready and map.terrain_travel.ready,"细导航与独立地形网格均就绪")
	_check(map.place_views.size()==123 and map.travel.graph.positions.size()==444,"123地点及444兼容锚点保持")
	_check(is_equal_approx(map.get_world_size(),22118.4),"边长3倍且实际8000公里")
	_check(map.landscape.metadata.has("water_distance"),"纹理与河湖海使用独立清晰地表")
	_check(map.set_initial_runtime_id("white_stone_town"),"白石入口可用")
	var white_stone: Vector2 = map.player_mark.position
	map._set_zoom_level(2,get_viewport_rect().size*.5)
	map.focus_world_point(white_stone)
	await _frames(5)
	_check(is_equal_approx(map.world_zoom,.8),"最近档0.8")
	var old_span := 100.0*.96/(8000.0/7372.8)
	var new_span: float = 40.0*map.world_zoom/map.Navigation.KM_PER_PIXEL
	_check(absf(old_span-new_span)<.001,"原100公里同屏跨度现为40公里")
	_check(map.scale_ruler.preferred_kilometres==40.,"最近档显示40公里比例尺")
	_check(absf(map.player_mark.scale.y*map.PLAYER_PAWN_TEXTURE.get_height()-52.)<.001,"主角固定52世界单位")
	map.set_player_travel_party([{"id":"qa_expanded_companion","name":"同行者"}])
	boat = BoatVisual.new()
	map.world.add_child(boat)
	_check(boat.build(),"原生灵舟与同尺寸舟上人物")
	boat.set_map_zoom(map.world_zoom)
	boat.set_flight_position(Vector3(white_stone.x+260.,white_stone.y+100.,0))
	VisualScale.overlay(boat,VisualScale.FLIGHT_DEPTH)
	var pawn: Vector2 = map.navigation.nearest_walkable(white_stone+Vector2(-90,80))
	if pawn.x>=0:
		map.player_mark.position=pawn
		map.player_halo.position=pawn
	map.focus_world_point(white_stone+Vector2(0,-90))
	await _frames(7)
	await _capture("expanded_white_stone_near.png")
	_check(boat.scale==Vector2.ONE and absf(boat.player_avatar.native_height*boat.player_avatar.scale.y-52.)<.001,"船和所有人物保持世界比例")
	boat.hide()
	map.set_player_travel_party([])
	for id in ["yuxu_xianzong_core","white_stone_town"]:
		_check(map.set_initial_runtime_id(id),"地点身份保持 "+id)
		map.focus_world_point(map.player_mark.position)
		await _frames(7)
		await _capture("expanded_"+id+"_near.png")
	var icon_sizes := {}
	for id: String in map.place_views:
		var icon: Sprite2D = map.place_views[id].get_node("NativeIcon")
		if icon.texture!=null: icon_sizes[id]=icon.scale
	for level in [0,1,2]:
		map._set_zoom_level(level,get_viewport_rect().size*.5)
		map.focus_player_node()
		await _frames(5)
		var sizes_ok := true
		for id: String in icon_sizes:
			if not (map.place_views[id].get_node("NativeIcon") as Sprite2D).scale.is_equal_approx(icon_sizes[id]): sizes_ok=false
		_check(sizes_ok,"缩放不改变建筑世界尺寸 "+str(level))
		_check(absf(map.player_mark.scale.y*map.PLAYER_PAWN_TEXTURE.get_height()-52.)<.001,"缩放不改变人物世界尺寸 "+str(level))
		await _capture("expanded_zoom_%d.png"%level)
	map._set_zoom_level(2,get_viewport_rect().size*.5)
	var metadata: Dictionary = map.landscape.metadata
	var explicit := true
	for group: Array in [metadata.mountains,metadata.trees,metadata.bridges]:
		for entry: Dictionary in group:
			if not entry.has_all(["x","y","z"]):explicit=false
	_check(explicit,"所有山树与桥面明确X/Y/Z")
	var bridge: Dictionary = metadata.bridges[0]
	map.focus_world_point(Vector2(bridge.x,bridge.y))
	await _frames(7)
	await _capture("expanded_native_bridge.png")
	for fraction in [.1,.5,.9]:
		var point := Vector2(11000,22118.4*fraction)
		map.focus_world_point(point)
		await _frames(4)
		var rank_a: int = map.depth_order.render_depth(point.y)
		var rank_b: int = map.depth_order.render_depth(point.y+3.)
		_check(rank_a<rank_b,"北中南均保持严格前后层级 "+str(fraction))
	lines.append("SCENERY "+JSON.stringify(map.landscape.get_debug_summary()))
	lines.append("CHECKS %d FAILURES %d"%[checks,failures])
	var file:=FileAccess.open(OUT+"expanded_visual_report.txt",FileAccess.WRITE)
	file.store_string("\n".join(lines)+"\n")
	print(lines[-1])
	get_tree().quit(0 if failures==0 else 1)

func _frames(count:int)->void:
	for i in count:await get_tree().process_frame
func _capture(name:String)->void:
	await _frames(2)
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(OUT+name)
func _check(ok:bool,message:String)->void:
	checks+=1
	if not ok:failures+=1
	var line:=("PASS "if ok else"FAIL ")+message
	lines.append(line)
	print(line)
