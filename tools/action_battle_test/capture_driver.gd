extends Node2D
func _ready():
	var battle = load("res://ActionBattleFeelTest.tscn").instantiate()
	add_child(battle)
	await get_tree().process_frame
	battle.set_process(false)
	battle.set_physics_process(false)
	battle.player = Vector2(400, 500)
	battle.enemy = Vector2(1000, 400)
	battle.hp = 100
	battle.enemy_hp = 100
	battle.facing = 1.0
	battle.update_skinned_player()
	for original in battle.skinned_player.mesh_nodes:
		var probe = Polygon2D.new()
		probe.polygon = original.polygon
		probe.polygons = original.polygons
		probe.uv = original.uv
		probe.texture = original.texture
		probe.position = original.position
		probe.color = Color.WHITE
		probe.z_index = 100
		original.get_parent().add_child(probe)
	await RenderingServer.frame_post_draw
	var image = get_viewport().get_texture().get_image()
	image.save_png("E:/tmp/action_battle_runtime_idle.png")
	get_tree().quit()
