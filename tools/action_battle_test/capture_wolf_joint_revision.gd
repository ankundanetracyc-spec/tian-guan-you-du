extends "capture_wolf_small_motion.gd"

class JointLabels extends Node2D:
	var actor: Node2D
	var offset := Vector2(24, 580)
	var font := SystemFont.new()

	func _ready() -> void:
		font.font_names = PackedStringArray(["Microsoft YaHei"])

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		if not is_instance_valid(actor):
			return
		for limb in ["near_hind", "near_front"]:
			var is_front: bool = "front" in limb
			var color := Color("28667a") if is_front else Color("925c29")
			var names: Array = actor._limb_bone_names(limb)
			var points: Array[Vector2] = []
			for key in names:
				var bone: Bone2D = actor.bones[key]
				points.append(offset + bone.get_global_transform_with_canvas().origin)
			var paw: Bone2D = actor.bones[limb + "_paw"]
			var toe: Vector2 = Vector2(actor.joints[limb][4]) - Vector2(actor.joints[limb][3])
			points.append(offset + paw.get_global_transform_with_canvas() * toe)
			var labels := ["肩", "肘", "腕", "掌垫", "趾端"] if is_front else ["髋", "膝", "跗", "掌垫", "趾端"]
			var label_offsets := [Vector2(45,-48),Vector2(52,-20),Vector2(72,-4),Vector2(62,20),Vector2(86,40)] if is_front else [Vector2(-100,-45),Vector2(-85,-20),Vector2(-90,0),Vector2(-77,24),Vector2(-40,45)]
			for index in range(5):
				if index < 4:
					draw_line(points[index], points[index + 1], color, 2.3, true)
				draw_circle(points[index], 3.5, color, true, -1, true)
				var label_offset: Vector2 = label_offsets[index]
				label_offset.x *= actor.current_facing
				var at: Vector2 = points[index] + label_offset
				draw_line(points[index], at + Vector2(5,-5), Color(color,0.65), 1.0, true)
				draw_string(font, at, labels[index], HORIZONTAL_ALIGNMENT_LEFT, -1, 17, color)


func _output_folder() -> String:
	return "forest_wolf_joint_revision_20261104"


func _detail_camera_offset() -> Vector2:
	return Vector2(0, -18)


func _make_scene() -> void:
	super._make_scene()
	detail_camera.zoom = Vector2.ONE * 4.2
	detail_wolf.debug_bones = false
	for label in viewport.find_children("*", "Label", true, false):
		if label.text.begins_with("林地凶狼"):
			label.text = "林地凶狼 · 关节结构修订"
		if label.text.begins_with("同一动作局部放大"):
			label.text = "关节放大核对 · 前肢蓝色 / 后肢棕色"
	var overlay := JointLabels.new()
	overlay.actor = detail_wolf
	viewport.add_child(overlay)
