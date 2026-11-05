extends SceneTree
var checks := 0
var failures := 0
func check(ok: bool,label: String) -> void:
	checks+=1
	if not ok:
		failures+=1
		push_error(label)
func _initialize() -> void:
	call_deferred("run")
func visible(state: Dictionary,viewport: Vector2) -> bool:
	var b:Rect2=state.target.bounds
	var pixels:=Rect2((b.position-state.center)*state.zoom+viewport*0.5,b.size*state.zoom)
	return state.target.safe_rect.grow(0.1).encloses(pixels)
func inside_map(state: Dictionary,viewport: Vector2,world: Vector2) -> bool:
	var size:Vector2=viewport/float(state.zoom)
	return Rect2(Vector2.ZERO,world).grow(0.1).encloses(Rect2(state.center-size*0.5,size))
func run() -> void:
	var framing=load("res://tools/action_battle_test/adaptive_combat_camera.gd").new()
	var world:=Vector2(3344,1882)
	var player:=Vector2(1500,1050)
	var player_record={"position":player,"head_height":130.0}
	var close=[player_record,{"position":player+Vector2(100,0),"head_height":85.0}]
	var far=[player_record,{"position":player+Vector2(1600,0),"head_height":85.0}]
	var medium=[]
	for point in [player,Vector2(1950,1040),Vector2(1050,850),Vector2(2050,850),Vector2(1100,1400),Vector2(2100,1400)]:
		medium.append({"position":point,"head_height":130.0})
	var extreme=[]
	for point in [Vector2(50,50),Vector2(3294,1832),Vector2(200,1600),Vector2(3000,100),Vector2(1600,800),Vector2(800,900)]:
		extreme.append({"position":point,"head_height":130.0})
	for viewport in [Vector2(1280,720),Vector2(1920,1080),Vector2(800,600),Vector2(640,360)]:
		var near:Dictionary=framing.solve(player,close,viewport,world)
		var spread:Dictionary=framing.solve(player,far,viewport,world)
		check(near.zoom>spread.zoom,"Spread controls zoom")
		check(spread.unit_count==2,"All remote participants included")
		var dead:Dictionary=framing.solve(player,[player_record,{"position":player+Vector2(1600,0),"alive":false}],viewport,world)
		check(dead.unit_count==1 and dead.zoom>spread.zoom,"Death excludes the dead unit")
		var normal:Dictionary=framing.update(player,medium,viewport,world,0.0,true)
		check(visible(normal,viewport),"Six ordinary participants all fit HUD-safe frame")
		check(inside_map(normal,viewport,world),"Normal view stays inside map")
		var independent:Dictionary=framing.solve(Vector2(50,50),medium,viewport,world)
		check(independent.center==normal.center,"No player center bias")
		var edge:Dictionary=framing.update(player,extreme,viewport,world,0.0,true)
		check(edge.target.unit_count==6,"Edge units all included in computation")
		check(edge.framing_saturated,"Map-vs-HUD extreme reports saturation")
		check(inside_map(edge,viewport,world),"Map containment wins over impossible margins")
		framing.update(player,close,viewport,world,0.0,true)
		var initial_zoom:float=framing.zoom
		var initial_center:Vector2=framing.center
		framing.update(player,medium,viewport,world,0.0)
		check(framing.zoom==initial_zoom and framing.center==initial_center,"Pause freezes easing")
		var expansion:Dictionary=framing.update(player,medium,viewport,world,1.0/60.0)
		check(visible(expansion,viewport),"Ordinary expansion keeps all units visible")
		check(inside_map(expansion,viewport,world),"Expansion stays inside map")
		var wider:Dictionary=framing.update(player,extreme,viewport*1.5,world,0.0)
		check(inside_map(wider,viewport*1.5,world),"Paused resize cannot expose map exterior")
		framing.update(player,medium,viewport,world,0.0,true)
		var far_zoom:float=framing.zoom
		var gathered:Dictionary=framing.update(player,close,viewport,world,1.0/60.0)
		check(gathered.zoom>far_zoom and gathered.zoom<initial_zoom,"Gathering zooms in gradually")
		framing.reset()
		var reset:Dictionary=framing.update(player,close,viewport,world,0.0)
		check(reset.zoom==initial_zoom and reset.center==initial_center,"Restart restores framing")
	print("SHARED_COMBAT_CAMERA_CHECKS ",checks," failures=",failures," map_priority=pass saturated_extremes=reported")
	quit(1 if failures>0 else 0)
