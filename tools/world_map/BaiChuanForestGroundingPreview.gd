extends "res://tools/world_map/BaiChuanGroundDetailsPreview.gd"
## Production renderer A/B: the baseline restores only the removed original
## tree sprites and original mountain material inside this isolated viewport.
const FOREST_OUT := "res://output/bai_chuan_forest_grounding_20261106/"
var patch: Dictionary
var legacy: Dictionary
var comparisons: Array[Dictionary] = []

func _ready() -> void:
	if not OS.get_environment("APPDATA").replace("\\", "/").contains("bai_chuan_forest_grounding_20261106/"):
		get_tree().quit(2)
		return
	get_window().size=Vector2i(64,64)
	get_window().position=Vector2i(-16000,-16000)
	get_window().set_flag(Window.FLAG_NO_FOCUS,true)
	_check(_hide_native_window(),"only this preview window is hidden")
	viewport=SubViewport.new()
	viewport.size=Vector2i(1920,1080)
	viewport.world_2d=World2D.new()
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	map=MapRuntime.new()
	map.size=Vector2(1920,1080)
	viewport.add_child(map)
	await _frames(6)
	map.set_process(false)
	patch=JSON.parse_string(FileAccess.get_file_as_string("res://resources/world/bai_chuan/forest_grounding_sample_20261106.json"))
	legacy=JSON.parse_string(FileAccess.get_file_as_string("res://resources/world/bai_chuan/landscape_expanded3x_20261106.json"))
	_check(bool(map.landscape.grounding_patch.get("active",false)),"actual production sample patch active")
	_check(is_instance_valid(map.landscape.grounding_details),"forest floor and mountain foot renderer active")
	_check(map.navigation.ready and map.terrain_travel.ready,"original navigation and terrain ready")
	_check(map.landscape.tree_textures.size()==10,"six natural trees and four original trees available")
	_check(map.landscape.metadata.trees.size()==legacy.trees.size()-patch.removed_tree_ids.size()+patch.trees.size(),"only sample tree membership replaced")
	_check(map.place_views.size()==123 and is_equal_approx(map.get_world_size(),22118.4),"places and world scale preserved")
	var saved_layout: Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://resources/world/bai_chuan/free_layout_expanded3x_20261106.json"))
	var buildings:=0
	for id: String in map.place_views:
		var marker: Control=map.place_views[id]
		var icon: Sprite2D=marker.get_node("NativeIcon")
		if icon.texture==null:continue
		var authored: Array=saved_layout.locations[id].rect
		var source_size:=Vector2(float(authored[2])-float(authored[0]),float(authored[3])-float(authored[1]))
		var drawn_size:=icon.texture.get_size()*icon.scale.abs()
		var hit: Control=marker.get_node("SelectIcon")
		_check(drawn_size.is_equal_approx(source_size*.85),"building art reduced15percent "+id)
		_check(hit.size.is_equal_approx(drawn_size) and hit.position.is_equal_approx(icon.position),"visible art and click bounds match "+id)
		var anchor:=Vector2(float(saved_layout.locations[id].drawPos[0]),float(saved_layout.locations[id].drawPos[1]))
		var original_offset:=Vector2(float(authored[0]),float(authored[1]))-anchor
		var actual_world_top_left:=marker.position+icon.position
		_check(actual_world_top_left.is_equal_approx(anchor+original_offset*.85),"building foot anchor unchanged "+id)
		buildings+=1
	_check(buildings==53,"all53building materials scaled; natural places retained")
	map._set_zoom_level(2,Vector2(960,540))
	for sample: Dictionary in [{"name":"white_stone","point":Vector2(5797.1138756,14416.3521531)}, {"name":"forest_and_foothill","point":Vector2(5000,14350)}, {"name":"forest_edge","point":Vector2(4800,13550)}]:
		var landing: Vector2=map.navigation.nearest_walkable(sample.point)
		map.player_mark.position=landing
		map.player_halo.position=landing
		map.focus_world_point(sample.point)
		map._update_context()
		map._layout_location_controls()
		map.landscape.refresh(true)
		map.landscape.update(0.0)
		var tree_views: Array=[]
		var materials := {}
		var new_ids := {}
		for tree: Dictionary in patch.trees:new_ids[str(tree.id)]=true
		for id: String in map.landscape.active:
			var view=map.landscape.active[id]
			if new_ids.has(id):
				tree_views.append(view)
				view.hide()
			if str(id) in patch.grounded_mountain_ids:
				materials[id]=view.material
				view.material=map.landscape.mountain_material
		var originals:=Node2D.new()
		map.landscape.add_child(originals)
		for tree: Dictionary in legacy.trees:
			if str(tree.id) not in patch.removed_tree_ids:continue
			var view: Node2D=map.landscape._tree(tree)
			view.position=Vector2(tree.position[0],tree.position[1])
			map.depth_order.place(view,float(tree.z))
			originals.add_child(view)
		map.landscape.grounding_details.hide()
		await _capture(str(sample.name)+"_before.png")
		originals.hide()
		for view: Node2D in tree_views:view.show()
		for id: String in materials:map.landscape.active[id].material=materials[id]
		map.landscape.grounding_details.show()
		await _capture(str(sample.name)+"_after.png")
		var summary: Dictionary=map.landscape.get_debug_summary()
		_check(tree_views.size()>0,"new tree silhouettes visible "+str(sample.name))
		_check(int(summary.grounding_details.visible_instances)>0,"connected ground and foot aprons visible "+str(sample.name))
		comparisons.append({"sample":sample.name,"camera":map.camera_pos,"player":landing,"visible_natural_trees":tree_views.size(),"summary":summary})
		originals.queue_free()
		await _frames(1)
	for level in [0,1,2]:
		var foot: Vector2=map.player_mark.position
		map._set_zoom_level(level,Vector2(960,540))
		map.focus_world_point(foot)
		map.landscape.refresh(true)
		map.landscape.update(0.0)
		_check(map.player_mark.position==foot,"camera preserves world foot "+str(level))
		_check(absf(map.player_mark.scale.y*map.PLAYER_PAWN_TEXTURE.get_height()-52.)<.001,"same52world actor "+str(level))
		_check(map.landscape.grounding_details.draw_calls==0 if level==0 else map.landscape.grounding_details.draw_calls>0,"sample ground detail LOD "+str(level))
	_finish()

func _capture(filename: String) -> void:
	await _frames(3)
	await RenderingServer.frame_post_draw
	_check(viewport.get_texture().get_image().save_png(FOREST_OUT+filename)==OK,"actual sample render "+filename)

func _finish() -> void:
	var file:=FileAccess.open(FOREST_OUT+"actual_map_visual_report.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"comparisons":comparisons,"patch":map.landscape.grounding_patch if is_instance_valid(map) else {}},"\t")+"\n")
	print("FOREST_GROUNDING_VISUAL_CHECKS ",checks," FAILURES ",failures.size())
	get_tree().quit(0 if failures.is_empty() else 1)
