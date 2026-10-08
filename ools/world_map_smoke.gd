extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var packed := load("res://tscn/world/TileWorldMapPresentation.tscn") as PackedScene
	if packed == null:
		push_error("world map scene failed to load")
		quit(1)
		return
	var map := packed.instantiate()
	root.add_child(map)
	await process_frame
	for cell in [Vector2i(38, 166), Vector2i(35, 167)]:
		print("WORLD_MAP_CELL_CHECK ", cell, " traversable=", map.call("is_cell_traversable", cell), " terrain=", map.call("get_cell_terrain", cell))
	quit()
