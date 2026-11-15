extends SceneTree
const Navigation = preload("res://gdscripts/world/BaiChuanFreeNavigation.gd")
const Graph = preload("res://gdscripts/world/BaiChuanNodeGraph.gd")
const NpcGraph = preload("res://gdscripts/world/BaiChuanNpcNodeGraph.gd")
const VisualScale = preload("res://gdscripts/world/WorldMapVisualScale.gd")
var checks := 0
var failures := 0
var report: Array[String] = []

func _initialize() -> void:
	var nav := Navigation.new()
	_check(nav.initialize(), "完整山体导航初始化")
	var graph := Graph.new()
	graph.use_free_world = true
	_check(graph.load_layout(), "123地点和原路线身份加载")
	_check(graph.positions.size() == 444 and graph.edge_points.size() == 575, "既有444路由锚点和575联系保持")
	for id: String in graph.positions:
		_check(nav.is_walkable(graph.positions[id]), "锚点在可通行地面 " + id)
	for edge: String in graph.edge_points:
		var path: PackedVector2Array = graph.edge_points[edge]
		_check(nav.validate_path(path), "NPC和押送路线绕山 " + edge)
		var subdivisions := true
		for i in range(1, path.size()):
			for j in 20:
				if not nav.segment_clear(path[i-1].lerp(path[i], float(j)/20.0), path[i-1].lerp(path[i], float(j+1)/20.0)):
					if subdivisions: print("SUBDIVISION_BLOCK ", edge, " a=", path[i-1], " b=", path[i], " sub=", j, " cells=",nav.cell(path[i-1].lerp(path[i],float(j)/20.0)), "->",nav.cell(path[i-1].lerp(path[i],float(j+1)/20.0)))
					subdivisions = false
		_check(subdivisions, "路线细分行走不会漏过角点 " + edge)
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://resources/world/bai_chuan/layered_landscape_20261106.json"))
	var image := Image.new()
	image.load_png_from_buffer(FileAccess.get_file_as_bytes(str(data.feature_data)))
	var blocked := true
	var surface_count := 0
	var river_overlap := 0
	for y in range(0, image.get_height(), 4):
		for x in range(0, image.get_width(), 4):
			var pixel := image.get_pixel(x, y)
			if pixel.g < 0.4: continue
			surface_count += 1
			if pixel.r > 0.5: river_overlap += 1
			if nav.is_walkable(Vector2(x+.5,y+.5) * float(data.source_scale)): blocked = false
	_check(surface_count > 100000 and blocked, "完整山面均禁止步行，旧路网没有穿山开口")
	_check(river_overlap == 0, "完整山体未压住河面")
	var trees_clear := true
	for row: Dictionary in data.trees:
		var rect := Rect2(row.bounds[0],row.bounds[1],row.bounds[2],row.bounds[3])
		var a := Vector2i((rect.position / float(data.source_scale)).floor())
		var b := Vector2i((rect.end / float(data.source_scale)).ceil())
		for y in range(maxi(0,a.y),mini(image.get_height(),b.y)):
			for x in range(maxi(0,a.x),mini(image.get_width(),b.x)):
				if image.get_pixel(x,y).g > 0.4: trees_clear = false
	_check(trees_clear, "所有树冠和树根均避开山体范围")
	_check(VisualScale.PLAYER_HEIGHT == VisualScale.NPC_HEIGHT and VisualScale.NPC_HEIGHT == VisualScale.COMPANION_HEIGHT, "全部人物共用40像素尺寸")
	var npc := NpcGraph.new()
	_check(npc.initialize(), "NPC仍使用当前真实路线与地形代价")
	var migrated := true
	for edge: String in graph.edge_points:
		var pair := edge.split("~")
		var a: Vector2i = npc.node_id_to_cell(pair[0])
		var b: Vector2i = npc.node_id_to_cell(pair[1])
		var hours: float = npc.upgrade_navigation_travel_progress(a,b,1.5,100.0)
		if not is_finite(hours) or hours < 0.0 or hours > npc.get_npc_edge_travel_hours(a,b,100.0) + 0.001: migrated = false
	_check(migrated, "575原行旅路段均可迁移至合法绕山路线")
	npc.free()
	report.append("CHECKS %d FAILURES %d" % [checks,failures])
	var file := FileAccess.open("res://output/bai_chuan_layered_20261106/navigation_runtime_report.txt",FileAccess.WRITE)
	file.store_string("\n".join(report)+"\n")
	print(report[-1])
	quit(0 if failures == 0 else 1)

func _check(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures += 1
	var line := ("PASS " if ok else "FAIL ") + message
	report.append(line)
	if not ok: print(line)
