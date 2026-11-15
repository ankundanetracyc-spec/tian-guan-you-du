extends SceneTree

const OUTPUT := "res://docs/美术规范/林地凶狼扑咬特效_20261024/"
const FRAMES := "res://tmp/forest_wolf_effect_motion/"
const SKILLS := "res://resources/activeskills/妖兽/林地凶狼/"

func _initialize() -> void:
	call_deferred("_capture")

func _capture() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1440, 810)
	root.position = Vector2i(-4096, -4096)
	root.gui_disable_input = true
	root.get_node("GameplayInputRouter").set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	DirAccess.make_dir_recursive_absolute(FRAMES)
	var preview: Node = load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
	root.add_child(preview)
	current_scene = preview
	await create_timer(0.35, false).timeout
	var ordinal := 0
	for item in [["撕咬", 0, "bite"], ["掠影扑杀", 0, "pounce"], ["撕咬", 1, "bite_left"]]:
		preview.call("PreviewSkill", SKILLS + item[0] + ".tres", item[1])
		var actor: Node2D = preview.find_child("出手", true, false)
		var used_textures := {}
		for frame in range(54):
			await process_frame
			await RenderingServer.frame_post_draw
			var pose: Sprite2D = actor.get_node("BattlePaperDoll/Portrait")
			used_textures[pose.texture.resource_path] = true
			if frame == 20:
				var effect: Node = preview.get_node_or_null("ForestWolfIncoming")
				if effect != null:
					var material: ShaderMaterial = effect.get_node("InkSurface").material
					var held: float = material.get_shader_parameter("progress")
					paused = true
					await create_timer(.2, true).timeout
					assert(material.get_shader_parameter("progress") == held, "Wolf effect advanced while paused")
					paused = false
					print("WOLF_PAUSE_PASSED ", item[2])
			if frame % 2 == 0:
				var output := root.get_texture().get_image()
				var stage: Control = preview.find_child("Stage", true, false)
				# The logical stage is scaled by the project's canvas stretch.
				var rect := stage.get_global_rect()
				var logical := root.get_visible_rect().size
				var factor := Vector2(output.get_width(), output.get_height()) / logical
				var region := Rect2i(rect.position * factor, rect.size * factor)
				var cropped := output.get_region(region)
				cropped.resize(960, roundi(960.0 * region.size.y / region.size.x), Image.INTERPOLATE_LANCZOS)
				cropped.save_png(FRAMES + "%03d.png" % ordinal)
				ordinal += 1
			if frame == 20:
				root.get_texture().get_image().save_png(OUTPUT + item[2] + "_game.png")
			if frame == 12:
				root.get_texture().get_image().save_png(OUTPUT + item[2] + "_pounce.png")
		assert(used_textures.size() == 2 and used_textures.has("res://textures/bestiary/static/forest_wolf.png") and used_textures.has("res://textures/bestiary/static/forest_wolf_pounce.png"), "Attack used more than idle and attack images")
		print("WOLF_TWO_POSES_PASSED ", item[2])
		await create_timer(.2, false).timeout
		var doll: Node2D = preview.find_child("出手", true, false).get_node("BattlePaperDoll")
		var portrait: Sprite2D = doll.get_node("Portrait")
		var planted: Vector2 = portrait.position
		for tick in range(12):
			await process_frame
			assert(portrait.position == planted, "Static wolf portrait moved during idle")
		print("WOLF_STATIC_IDLE_PASSED ", item[2])
	print("FOREST_WOLF_CAPTURE_COMPLETE frames=", ordinal)
	preview.queue_free()
	await process_frame
	await process_frame
	quit()
