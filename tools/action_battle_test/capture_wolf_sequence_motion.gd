extends "capture_wolf_small_motion.gd"

func _art_dir() -> String:
	return _local("art/forest_wolf_frames_v2")

func _wolf(parent: Node, at: Vector2, zoom: float, _debug: bool) -> Node2D:
	var holder := Node2D.new()
	holder.position = at
	holder.scale = Vector2.ONE * zoom
	parent.add_child(holder)
	var actor: Node2D = load(_local("forest_wolf_sequence.gd")).new()
	holder.add_child(actor)
	assert(actor.build(_art_dir()))
	wolves.append(actor)
	return actor

func _output_folder() -> String:
	return "forest_wolf_sequences_20261104/motion"

func _motion_capture_fps() -> float:
	return 60.0

func _motion_capture_count() -> int:
	return 240

func _make_scene() -> void:
	super._make_scene()
	for label in viewport.find_children("*","Label",true,false):
		if label.text.begins_with("林地凶狼"):
			label.text = "林地凶狼 · 整狼序列帧"
		if label.text.begins_with("同一动作局部放大"):
			label.text = "逐帧播放 · 走动16帧 / 小跑16帧"
