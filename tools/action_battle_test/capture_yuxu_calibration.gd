extends Node2D

class Ruler extends Node2D:
	var rig: Node2D
	func _draw() -> void:
		var font := ThemeDB.fallback_font
		var origin := Vector2(300, 550)
		var unit := 16.0 * 0.72 * 4.0
		for i in range(8):
			var y := origin.y - (7-i) * unit
			draw_line(Vector2(80, y), Vector2(510, y), Color(0.28, 0.38, 0.41, 0.45), 1.0)
			draw_string(font, Vector2(38, y+6), str(i) + " H", HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color("25464c"))
		draw_string(font, Vector2(65, 65), "YUXU / 7 HEADS / CROWN TO SOLE", HORIZONTAL_ALIGNMENT_LEFT, -1, 24, Color("25464c"))
		draw_string(font, Vector2(65, 92), "1 H = crown to chin; hair bun excluded", HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color("53666a"))
		var skin = rig.wanderer_skin
		var links := [["pelvis", "torso"], ["torso", "head"], ["torso", "near_upper"], ["near_upper", "near_forearm"], ["near_forearm", "near_hand"], ["pelvis", "near_thigh"], ["near_thigh", "near_calf"], ["near_calf", "near_foot"]]
		for link in links:
			draw_line(skin.bones[link[0]].global_position, skin.bones[link[1]].global_position, Color("bc6846"), 2.0)
		var labels := {"head": "neck -94", "near_upper": "shoulder -88", "torso": "waist -66", "near_forearm": "elbow -71", "near_hand": "wrist -57", "pelvis": "hip -56", "near_calf": "knee -29", "near_foot": "ankle -4"}
		for key in labels:
			var point: Vector2 = skin.bones[key].global_position
			draw_circle(point, 4.0, Color("bc6846"))
			var offset := Vector2(32, 4) if key not in ["near_forearm", "near_hand"] else Vector2(-155, 4)
			draw_string(font, point + offset, labels[key], HORIZONTAL_ALIGNMENT_LEFT, -1, 17, Color("72452e"))

func _ready() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(1000, 620)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	var bg := ColorRect.new()
	bg.color = Color("e9e6df")
	bg.size = viewport.size
	viewport.add_child(bg)
	var rigs: Array = []
	for x in [300.0, 730.0]:
		var rig = load("res://guigu_rig_runtime.gd").new()
		viewport.add_child(rig)
		assert(rig.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art", "yuxu_unified_v14"))
		rig.set_skin_mode("experiment")
		rig.wanderer_skin.pose_blending = false
		rig.set_character_pose("BindPose", 0.0, 1.0)
		rig.position = Vector2(x, 550)
		rig.scale *= 4.0
		rigs.append(rig)
	for mesh in rigs[1].wanderer_skin.meshes:
		mesh.modulate.a = 0.28
	var ruler := Ruler.new()
	ruler.rig = rigs[1]
	viewport.add_child(ruler)
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	assert(viewport.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://output/yuxu_unified_v14/seven_heads_calibration.png")) == OK)
	get_tree().quit()
