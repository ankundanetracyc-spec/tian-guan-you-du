extends SceneTree

const OUTPUT:="res://docs/美术规范/惊蛰原地引雷与落雷_20261020/"
const SKILL:="res://resources/activeskills/玄枢含章经/惊蛰.tres"
var scene: Node
var keep_open:=false

func _initialize() -> void:
	keep_open="--keep-open" in OS.get_cmdline_user_args()
	call_deferred("_capture")

func _capture() -> void:
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,810)
	root.position=Vector2i(-4096,-4096)
	root.gui_disable_input=true
	root.get_node("GameplayInputRouter").set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	scene=load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await create_timer(.5,false).timeout
	Engine.max_fps=30
	for female in [false,true]:
		var sex:="female" if female else "male"
		var motion_dir:="res://tmp/jingzhe_motion_20261020/"+sex+"/"
		DirAccess.make_dir_recursive_absolute(motion_dir)
		scene.call("SetPreviewCaster",female)
		scene.call("PreviewSkill",SKILL,1 if female else 0)
		for index in range(84):
			await process_frame
			await RenderingServer.frame_post_draw
			var frame:=root.get_texture().get_image()
			frame.save_png(motion_dir+"motion_%03d.png"%index)
			if index==8: frame.save_png(OUTPUT+sex+"_cast.png")
			if index==17: frame.save_png(OUTPUT+sex+"_strike.png")
			if index==35: frame.save_png(OUTPUT+sex+"_remainder.png")
			if index==75: frame.save_png(OUTPUT+sex+"_idle.png")
	scene.queue_free()
	await process_frame
	scene=load("res://tscn/battle/BattleSandbox.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await create_timer(.8,false).timeout
	var manager: Node=scene.get_node("BattleMgr")
	manager.set_process(false)
	manager.set_process_input(false)
	manager.set_process_unhandled_input(false)
	var toggle:=scene.find_child("PlanningToggle",true,false)
	if toggle: toggle.emit_signal("pressed")
	await create_timer(2.1,false).timeout
	var units: Array=manager.get("all_units")
	var executor: Node
	for child in manager.get_children():
		if child.has_method("execute_skill"): executor=child
	var caster: Node2D=units[0]
	var target: Node2D=units[3]
	caster.set("precision",100)
	target.set("dodge",0)
	var home:=caster.global_position
	executor.call("execute_skill",caster,load(SKILL),target,true,false,false,false)
	await create_timer(.34,false).timeout
	await _shot("battle_cast.png")
	await create_timer(.22,false).timeout
	await _shot("battle_strike.png")
	await create_timer(1.7,false).timeout
	await _shot("battle_return.png")
	print("JINGZHE_CAPTURE_COMPLETE: current lab male/female/mirrored sequences + SkillExecutor battle cast/strike/return; caster ground delta=",caster.global_position-home,"; no save writes")
	if keep_open:
		scene.queue_free()
		await process_frame
		scene=load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
		root.add_child(scene)
		current_scene=scene
		await create_timer(.5,false).timeout
		scene.call("PreviewSkill",SKILL,0)
		root.gui_disable_input=false
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position=Vector2i(100,60)
		return
	scene.queue_free()
	await process_frame
	await process_frame
	quit()

func _shot(filename: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+filename)
