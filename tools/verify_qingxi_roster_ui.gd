extends SceneTree

# 使用实际青溪里入口验证头像和名单布局；非无头模式同时输出预览。

func _initialize() -> void:
	call_deferred("verify")

func settle() -> void:
	for frame in range(6):
		await process_frame
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw

func verify() -> void:
	root.size = Vector2i(1920, 1080)
	var scene := Control.new()
	root.add_child(scene)
	current_scene = scene
	var roster: Control = load("res://tscn/panel/CellCharacterList.tscn").instantiate()
	scene.add_child(roster)
	var estate: Control = load("res://tscn/panel/bamboo/EstateOverview.tscn").instantiate()
	scene.add_child(estate)
	for location in ["li_residence", "village", "farmland", "back_hill"]:
		estate.call("open_location_view", location)
		await settle()
		assert(roster.size.y < 300, "Short roster wastes a tall empty panel")
		var panel: Control = roster.get_node("ListPanel")
		var header: Control = panel.get_node("RosterHeader")
		assert(header.position.x >= 18 and header.position.y >= 18)
		assert(header.get_rect().end.x <= panel.size.x - 18)
		assert(not panel.has_node("RosterPaperFrame"), "Double frame returned")
		assert(roster.position == Vector2(22, 132), "Roster no longer aligns with HUD")
		var scroll: ScrollContainer = panel.get_node("CharacterScroll")
		assert(scroll.position.x >= 14 and scroll.get_rect().end.x <= panel.size.x - 14)
		var rows: VBoxContainer = scroll.get_node("CharacterVBox")
		var expected := 2 if location == "village" else (0 if location == "back_hill" else 1)
		assert(rows.get_child_count() == expected)
		for row in rows.get_children():
			var portrait = row.find_child("AvatarCrop", true, false).get_child(0)
			assert(portrait.call("has_required_layers"), "Missing Qingxi portrait")
			assert(row.size.x <= scroll.size.x, "Row overflow")
		if location == "li_residence":
			rows.get_child(0).emit_signal("pressed")
		if location == "village" and DisplayServer.get_name() != "headless":
			DirAccess.make_dir_recursive_absolute("res://tmp/roster_ui")
			assert(root.get_texture().get_image().save_png("res://tmp/roster_ui/qingxi_roster.png") == OK)
	estate.hide()
	var npc_script = load("res://csharp/world/WorldNpcData.cs")
	var npcs := Array([], TYPE_OBJECT, &"Resource", npc_script)
	for index in range(8):
		var npc = npc_script.new()
		npc.npc_id = "roster_layout_test_%d" % index
		npc.npc_name = "测试人物%d" % (index + 1)
		npc.formal_identity = "外门弟子"
		npc.cultivation_realm = "云息三层"
		npcs.append(npc)
		if index < 6:
			continue
		roster.call("show_world_npcs", npcs, "world:roster-test")
		await settle()
		var scroll: ScrollContainer = roster.get_node("ListPanel/CharacterScroll")
		var rows: VBoxContainer = scroll.get_node("CharacterVBox")
		assert(roster.size.y > 500 and roster.size.y < 700)
		if index == 6:
			assert(rows.get_child(6).get_rect().end.y <= scroll.size.y, "Seventh row clipped")
		else:
			assert(rows.size.y > scroll.size.y, "Eighth row should scroll")
		if DisplayServer.get_name() != "headless":
			assert(root.get_texture().get_image().save_png("res://tmp/roster_ui/roster_%d.png" % (index + 1)) == OK)
	print("QINGXI_ROSTER_OK: single frame, HUD aligned, short roster fits content, seven visible rows, eighth scrolls, Qingxi portraits")
	quit()
