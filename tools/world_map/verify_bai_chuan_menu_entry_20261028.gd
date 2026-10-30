extends SceneTree

const DOC := "res://docs/美术规范/百川整图与节点落位验收_20261028/"
var failures: Array[String] = []
var checks := 0

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures.append(label)
		push_error(label)

func frames(count: int) -> void:
	for i in count: await process_frame

func _run() -> void:
	var menu = load("res://tscn/scene/StartMenu.tscn").instantiate()
	root.add_child(menu)
	current_scene = menu
	await frames(5)
	var old_mode := root.content_scale_mode
	var old_size := root.content_scale_size
	var old_factor := root.content_scale_factor
	var demo: Button = menu.get_node("StartMenuMenu/DemoToggle")
	demo.emit_signal("pressed")
	await frames(3)
	var entry := menu.find_child("BaiChuanNineMapTest",true,false) as Button
	check(entry != null and entry.is_visible_in_tree(),"map entry available in preview menu")
	if entry == null:
		_finish()
		return
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(DOC+"实机_首页测试入口.png")
	entry.emit_signal("pressed")
	await frames(8)
	check(current_scene.scene_file_path == "res://tscn/world/bai_chuan/BaiChuanNineTileTest.tscn","real menu transition")
	var map = current_scene
	check(map.get("place_views").size() == 123,"all places loaded through menu")
	check(root.content_scale_mode == Window.CONTENT_SCALE_MODE_DISABLED,"native scale through menu")
	# Exercise the actual dropdown signal, not just its focus helper.
	var index := map.find_child("AllPlaceIndex",true,false) as OptionButton
	check(index != null and index.item_count == 124,"123-place dropdown")
	index.emit_signal("item_selected",2)
	await frames(2)
	check(map.title_label.text == str(map.data.nodes[1].name),"dropdown navigation")
	map._return_to_menu()
	await frames(8)
	check(current_scene.scene_file_path == "res://tscn/scene/StartMenu.tscn","return to menu")
	check(root.content_scale_mode == old_mode and root.content_scale_size == old_size and is_equal_approx(root.content_scale_factor,old_factor),"restore previous scale settings")
	_finish()

func _finish() -> void:
	var report := {"checks":checks,"failureCount":failures.size(),"failures":failures}
	var file := FileAccess.open(DOC+"入口验收.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	print("BAICHUAN_MENU_ENTRY_DONE "+JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)
