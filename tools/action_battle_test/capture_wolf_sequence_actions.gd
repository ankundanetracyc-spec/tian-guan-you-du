extends "capture_forest_wolf_rig.gd"

var sheet_display: TextureRect
var count_label: Label

func _art_dir() -> String:
	return _local("art/forest_wolf_frames_v2")

func _wolf(parent: Node, at: Vector2, zoom: float, _debug: bool) -> Node2D:
	var holder := Node2D.new()
	holder.position = at
	holder.scale = Vector2.ONE * (0.55 if wolves.is_empty() else zoom)
	parent.add_child(holder)
	var actor: Node2D = load(_local("forest_wolf_sequence.gd")).new()
	holder.add_child(actor)
	assert(actor.build(_art_dir()))
	wolves.append(actor)
	return actor

func _capture_output_folder() -> String:
	return "forest_wolf_sequences_20261104/actions"

func _capture_source_name() -> String:
	return "forest_wolf_frames_v2"

func _context(frame: int) -> Dictionary:
	var context: Dictionary = super._context(frame)
	context.label = str(context.label).replace("完整骨骼镜像","整狼帧镜像").replace("连续骨骼动作","整图倒地")
	return context

func _make_scene() -> void:
	super._make_scene()
	wolves[2].visible = false
	sheet_display = TextureRect.new()
	sheet_display.position = Vector2(753,466)
	sheet_display.size = Vector2(640,350)
	sheet_display.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	sheet_display.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	viewport.add_child(sheet_display)
	count_label = _label(viewport,"",Vector2(754,426),21)
	for label in viewport.find_children("*","Label",true,false):
		if label.text.begins_with("林地凶狼"):
			label.text = "林地凶狼 · 序列帧与帧表"
		elif label.text.begins_with("局部放大"):
			label.text = "动作放大 · 完整身体逐帧绘制"
		elif label.text.begins_with("同一动作"):
			label.text = ""

func _sync_capture_actors() -> void:
	var lead: Node2D = wolves[0]
	for actor in wolves.slice(1):
		actor.current_facing = lead.current_facing
		actor._show_frame(lead.current_clip,lead.sprite.frame)
	var clip: Dictionary = lead.clips[lead.current_clip]
	sheet_display.texture = lead.textures[_art_dir().path_join(str(clip.sheet))]
	var names := {"idle":"待机","walk":"走动","trot":"小跑","windup":"蓄势","lunge":"扑咬","recover":"收势","hurt":"受击","death":"倒地"}
	count_label.text = "原始帧表 · %s%d帧 · 当前第%d帧" % [str(names.get(lead.current_clip,lead.current_clip)),int(clip.frame_count),lead.sprite.frame+1]
