extends SceneTree

const OUTPUT := "res://docs/美术规范/局内水墨动作正式接入_20261016/"
var scene: Node2D
var executor: Node

func _initialize() -> void:
	call_deferred("_capture")

func _capture() -> void:
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,810)
	root.position=Vector2i(-4096,-4096)
	root.gui_disable_input=true
	scene=load("res://tscn/battle/BattleSandbox.tscn").instantiate()
	root.add_child(scene)
	current_scene=scene
	await create_timer(.8,false).timeout
	var manager: Node=scene.get_node("BattleMgr")
	manager.set_process(false)
	manager.set_process_unhandled_input(false)
	var queue_toggle:=scene.find_child("PlanningToggle",true,false)
	if queue_toggle:
		queue_toggle.emit_signal("pressed")
	var units: Array=manager.get("all_units")
	if units.size()<4:
		push_error("Formal scene did not create its battle actors")
		quit(1)
		return
	for child in manager.get_children():
		if child.has_method("execute_skill"):
			executor=child
	var male: Node2D=units[0]
	var female: Node2D=units[3]
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	await _shot("formal_idle.png")
	male.set("precision",100)
	female.set("dodge",0)
	executor.call("execute_skill",male,load("res://resources/activeskills/应敌.tres"),female,true)
	await create_timer(.43,false).timeout
	await _shot("formal_attack.png")
	await create_timer(.17,false).timeout
	await _shot("formal_hit.png")
	await create_timer(1.4,false).timeout
	executor.call("execute_response",female,male,load("res://resources/activeskills/架招.tres"),false)
	await create_timer(.27,false).timeout
	await _shot("formal_guard.png")
	await create_timer(1.2,false).timeout
	executor.call("present_recovery",male)
	executor.call("present_recovery",female)
	await create_timer(.46,false).timeout
	await _shot("formal_restore.png")
	await create_timer(1.5,false).timeout
	await _shot("formal_return_idle.png")
	print("FORMAL_INK_CAPTURE_COMPLETE: real BattleSandbox, SkillExecutor attack/response/recovery, male/female, six rendered snapshots")
	quit()

func _shot(filename: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	if root.get_texture().get_image().save_png(OUTPUT+filename)!=OK:
		push_error("Could not save formal battle capture: "+filename)
		quit(1)
