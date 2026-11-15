extends SceneTree

const OUTPUT:="res://docs/美术规范/法力护罩配色与尺寸_20261019/"
var keep_open:=false
var scene: Node

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
	await create_timer(.6,false).timeout
	_press_shield()
	await create_timer(.38,false).timeout
	await _shot("shield_paper_idle.png")
	await create_timer(.35,false).timeout
	await _shot("shield_paper_contact.png")
	var shield: Node=scene.find_child("ShieldPreview",true,false)
	var motion_dir:="res://tmp/mana_shield_motion_20261019/"
	DirAccess.make_dir_recursive_absolute(motion_dir)
	Engine.max_fps=30
	_press_shield()
	for index in range(96):
		if index==70: shield.call("set_preview_shield",0)
		await process_frame
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(motion_dir+"motion_%03d.png"%index)
	shield.call("set_preview_shield",8)
	await create_timer(.4,false).timeout
	var moving_dir:="res://tmp/mana_shield_follow_20261019/"
	DirAccess.make_dir_recursive_absolute(moving_dir)
	_press_shield("战斗·带盾出手")
	for index in range(72):
		await process_frame
		await RenderingServer.frame_post_draw
		var moving_image:=root.get_texture().get_image()
		moving_image.save_png(moving_dir+"motion_%03d.png"%index)
		if index==13: moving_image.save_png(OUTPUT+"shield_attack_follow.png")
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
	var units: Array=manager.get("all_units")
	units[0].set("shield",8)
	units[3].set("shield",8)
	await create_timer(.4,false).timeout
	await _shot("shield_battle_idle.png")
	# Show the shield attached to a real moving unit through SkillExecutor's approach and return.
	var executor: Node
	for child in manager.get_children():
		if child.has_method("execute_skill"): executor=child
	units[0].set("precision",100)
	units[3].set("dodge",0)
	executor.call("execute_skill",units[0],load("res://resources/activeskills/应敌.tres"),units[3],true)
	await create_timer(.40,false).timeout
	await _shot("shield_battle_attack.png")
	await create_timer(1.5,false).timeout
	await _shot("shield_battle_return.png")
	units[3].set("shield",8)
	await create_timer(.3,false).timeout
	units[0].get_node("BattleShieldAura").call("PlayImpact",Vector2.LEFT)
	units[3].get_node("BattleShieldAura").call("PlayImpact",Vector2.RIGHT)
	await create_timer(.12,false).timeout
	await _shot("shield_battle_contact.png")
	units[0].set("shield",0)
	units[3].set("shield",0)
	await create_timer(.14,false).timeout
	await _shot("shield_dissolve.png")
	await create_timer(.3,false).timeout
	await _shot("shield_cleared.png")
	print("MANA_SHIELD_CAPTURE_COMPLETE: current laboratory + real battle scene, body fit, directional contact and dissolve; no save writes")
	if keep_open:
		scene.queue_free()
		await process_frame
		scene=load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
		root.add_child(scene)
		current_scene=scene
		await create_timer(.5,false).timeout
		_press_shield()
		root.gui_disable_input=false
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position=Vector2i(100,60)
		return
	scene.queue_free()
	await process_frame
	await process_frame
	quit()

func _press_shield(label: String="战斗·常驻护盾") -> void:
	for button in scene.find_children("*","Button",true,false):
		if button.text==label:
			button.emit_signal("pressed")
			return

func _shot(filename: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+filename)
