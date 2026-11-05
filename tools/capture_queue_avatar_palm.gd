extends SceneTree

const OUTPUT := "res://docs/美术规范/行动队列局内形象_20261103/"
var frames_path := "res://tmp/queue_avatar_palm_capture/"
var actual_queue := false
var prefix := "queue_palm_"
var dolls: Array[Node] = []

func _initialize() -> void:
	if "--actual-queue" in OS.get_cmdline_user_args():
		actual_queue = true
		frames_path = "res://tmp/queue_avatar_palm_actual_capture/"
		prefix = "queue_palm_actual_"
	call_deferred("_capture")

func _capture() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1440, 810)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	var router := root.get_node_or_null("GameplayInputRouter")
	if router:
		router.set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	DirAccess.make_dir_recursive_absolute(frames_path)
	var scene := load("res://tools/QueueAvatarBattlePreview.tscn").instantiate() as Node
	root.add_child(scene)
	current_scene = scene
	for index in range(45):
		await process_frame
	dolls = scene.find_children("BattlePaperDoll", "", true, false)
	if dolls.size() != 2:
		push_error("Expected two actual battle actors")
		quit(1)
		return
	for doll in dolls:
		if not doll.call("uses_queue_avatar"):
			push_error("An actor is missing the queue avatar")
			quit(1)
			return
	for index in range(180):
		if index == 30 or index == 105:
			if not actual_queue:
				scene.call("demonstrate_palm")
			elif index == 30:
				var manager := scene.get_node("BattleMgr")
				var player: Node = manager.call("get_main_player_unit")
				var skills: Array = player.get_meta("combat_resolved_skills")
				for skill in skills:
					if skill.get("skill_name") == "应敌":
						manager.call("on_skill_selected", skill, 0)
						manager.call("confirm_player_queue")
						break
		await process_frame
		await RenderingServer.frame_post_draw
		var frame := root.get_texture().get_image()
		if frame.save_png(frames_path + "motion_%03d.png" % index) != OK:
			push_error("Cannot save runtime palm frame")
			quit(1)
			return
		if index in [0, 30, 32, 34, 36, 38, 39, 41, 44, 50, 60, 75]:
			frame.save_png(OUTPUT + prefix + "%03d.png" % index)
	print("QUEUE_PALM_CAPTURE_OK: 180 actual battle rendered frames, two mirrored actors, actual_queue=", actual_queue)
	quit()
