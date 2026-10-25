extends SceneTree
const OUTPUT := "res://docs/美术规范/林地妖狈技能与动作_20261025/"
const FRAMES := "res://tmp/forest_bei_action_frames/"
const SKILLS := "res://resources/activeskills/妖兽/林地妖狈/"
func _initialize() -> void:
	call_deferred("_capture")
func _capture() -> void:
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1440,810)
	root.position = Vector2i(-4096,-4096)
	root.gui_disable_input = true
	root.get_node("GameplayInputRouter").set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	DirAccess.make_dir_recursive_absolute(FRAMES)
	var preview: Node = load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
	root.add_child(preview)
	current_scene = preview
	await create_timer(.35,false).timeout
	var ordinal := 0
	for item in [["狈啸",0,"cry",54],["唤猎",0,"call",36],["受击",0,"hurt",36],["狈啸",1,"cry_left",54]]:
		if item[0] == "受击": preview.call("PreviewForestBeiHurt",item[1])
		else: preview.call("PreviewSkill",SKILLS+item[0]+".tres",item[1])
		var actor: Node = preview.find_child("出手",true,false)
		var portrait: Sprite2D = actor.get_node("BattlePaperDoll/Portrait")
		var textures := {}
		for frame in range(item[3]):
			await process_frame
			await RenderingServer.frame_post_draw
			textures[portrait.texture.resource_path] = true
			if frame == 5:
				var effects := get_nodes_in_group("battle_action_visuals")
				for effect in effects:
					if str(effect.name).begins_with("ForestBei"):
						var material: ShaderMaterial = effect.get_node("InkSurface").material
						var held: float = material.get_shader_parameter("progress")
						paused = true
						await create_timer(.2,true).timeout
						assert(material.get_shader_parameter("progress") == held,"Bei FX advanced while paused")
						paused = false
						print("BEI_PAUSE_PASSED ",item[2])
						break
			if frame % 2 == 0:
				var output := root.get_texture().get_image()
				var stage: Control = preview.find_child("Stage",true,false)
				var rect := stage.get_global_rect()
				var factor := Vector2(output.get_width(),output.get_height())/root.get_visible_rect().size
				var region := Rect2i(rect.position*factor,rect.size*factor)
				var image := output.get_region(region)
				image.resize(960,roundi(960.0*region.size.y/region.size.x),Image.INTERPOLATE_LANCZOS)
				image.save_png(FRAMES+"%03d.png"%ordinal)
				ordinal += 1
			if frame == (3 if item[2] == "hurt" else 6):
				root.get_texture().get_image().save_png(OUTPUT+item[2]+"_game.png")
		var expected := "forest_bei_hurt_v1.png" if item[2] == "hurt" else "forest_bei_attack_v1.png"
		assert(textures.size()==2 and textures.has("res://textures/bestiary/static/forest_bei_v1.png") and textures.has("res://textures/bestiary/static/"+expected),"Bei used unexpected poses")
		var planted: Vector2 = portrait.position
		for tick in range(12):
			await process_frame
			assert(portrait.position==planted,"Bei idle portrait moved")
		print("BEI_POSES_AND_IDLE_PASSED ",item[2])
	print("FOREST_BEI_CAPTURE_COMPLETE frames=",ordinal)
	preview.queue_free()
	await process_frame
	await process_frame
	quit()
