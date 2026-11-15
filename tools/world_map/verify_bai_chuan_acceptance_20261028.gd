extends SceneTree

const DOC := "res://docs/美术规范/百川整图与节点落位验收_20261028/"
const OUT := "res://output/bai_chuan_acceptance/"
var failures: Array[String] = []
var checks := 0

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool,label: String) -> void:
	checks+=1
	if not ok:
		failures.append(label)
		push_error("FAIL "+label)

func _run() -> void:
	var old_mode:=root.content_scale_mode
	var old_size:=root.content_scale_size
	var scene=load("res://tscn/world/bai_chuan/BaiChuanNineTileTest.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	for i in 6: await process_frame
	check(scene._textures.size()==9,"nine tiles")
	check(scene.WORLD_SIZE==6144,"world6144")
	check(scene.places.get_child_count()==123,"all123 places")
	check(scene.ink.lines.size()==498 and scene.ink.marks.size()==321,"unchanged graph")
	check(root.content_scale_mode==Window.CONTENT_SCALE_MODE_DISABLED,"physical pixel scale")
	for texture: Texture2D in scene._textures:
		check(texture.get_size()==Vector2(2048,2048),"2048 tile")
	for node: Dictionary in scene.data.nodes:
		scene._focus_place(node.name)
		check(scene.title_label.text==node.name,"focus "+str(node.name))
		var marker: Control=scene.place_views[node.id]
		var icon: Sprite2D=marker.get_node("NativeIcon")
		check(icon.scale==Vector2.ONE,"native icon "+str(node.name))
		check(marker.position.distance_to(scene._point(node.pos))<1,"logical anchor "+str(node.name))
		check(scene.camera_pos.x>=0 and scene.camera_pos.y>=0,"camera "+str(node.name))
	var before: Vector2=scene.camera_pos
	for direction in [MOUSE_BUTTON_WHEEL_UP,MOUSE_BUTTON_WHEEL_DOWN]:
		var event:=InputEventMouseButton.new()
		event.button_index=direction
		event.pressed=true
		Input.parse_input_event(event)
	var gesture:=InputEventMagnifyGesture.new()
	gesture.factor=2
	Input.parse_input_event(gesture)
	for i in 3: await process_frame
	check(scene.world.scale==Vector2.ONE and scene.camera_pos==before,"wheel and pinch locked")
	for layer in ["places","paths","marks"]: scene._toggle_layer(false,layer)
	check(not scene.places.visible and not scene.ink.paths_visible and not scene.ink.marks_visible,"independent layers")
	for layer in ["places","paths","marks"]: scene._toggle_layer(true,layer)
	if DisplayServer.get_name()!="headless":
		var shots: Array=["玉虚仙宗","玄都天府","无相圣宗","镇岳观","抱朴山","丹鼎宗","枯骨寺","蚀日宫","春水府","青萝城","雁回镇","澜浦渡","问津渡","藏真洞天","通济城"]
		for name: String in shots:
			scene._focus_place(name)
			await process_frame
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(DOC+"实机_"+name+".png")
		var view:=SubViewport.new()
		view.size=Vector2i(6144,6144)
		view.disable_3d=true
		view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
		root.add_child(view)
		var world: Node2D=scene.world.duplicate()
		# Runtime draw arrays are not exported Node properties; duplicate() alone
		# drops the road layer. Copy its live geometry explicitly for the export.
		var source_ink = scene.ink
		var copied_ink = world.get_node("RoutesAndTransit")
		copied_ink.lines = source_ink.lines.duplicate()
		copied_ink.spurs = source_ink.spurs.duplicate()
		copied_ink.marks = source_ink.marks.duplicate()
		copied_ink.junctions = source_ink.junctions.duplicate()
		copied_ink.local_approaches = source_ink.local_approaches.duplicate()
		check(copied_ink.lines.size()==498 and copied_ink.marks.size()==321,"export includes live roads and transit")
		copied_ink.queue_redraw()
		world.position=Vector2.ZERO
		view.add_child(world)
		for i in 3: await process_frame
		await RenderingServer.frame_post_draw
		view.get_texture().get_image().save_png(DOC+"完整地图_含节点与道路_6144.png")
		view.queue_free()
	scene.camera_pos=Vector2(999999,999999)
	scene._update_camera()
	check(scene.camera_pos.x<=maxf(0,6144-root.get_visible_rect().size.x) and scene.camera_pos.y<=maxf(0,6144-root.get_visible_rect().size.y),"camera bounds")
	scene.queue_free()
	await process_frame
	check(root.content_scale_mode==old_mode and root.content_scale_size==old_size,"restore window")
	var report:={"checks":checks,"failures":failures,"failureCount":failures.size(),"display":DisplayServer.get_name()}
	var file:=FileAccess.open(DOC+"实机验收.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	print("BAICHUAN_ACCEPTANCE_DONE "+JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)
