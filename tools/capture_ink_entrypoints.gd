extends SceneTree

const OUTPUT := "res://docs/美术规范/局内水墨动作正式接入_20261016/入口统一/"
const ENTRIES := {
	"poses":"res://tscn/scene/BattlePoseLibraryShowcase.tscn",
	"skeleton":"res://tscn/scene/CharacterSequenceShowcase.tscn",
	"puppet":"res://tscn/battle/ShadowPuppetTest.tscn",
	"assembly":"res://tscn/scene/PuppetAssemblyWorkbench.tscn",
	"lab":"res://tscn/scene/InkVfxShowcase.tscn",
	"menu":"res://tscn/scene/StartMenu.tscn"
}
var entry:="lab"
var scene: Node
var keep_open:=false
var capture_motion:=false

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--entry="):
			entry=argument.trim_prefix("--entry=")
		if argument=="--keep-open": keep_open=true
		if argument=="--motion": capture_motion=true
	call_deferred("_capture")

func _capture() -> void:
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,810)
	root.position=Vector2i(-4096,-4096)
	root.gui_disable_input=true
	root.get_node("GameplayInputRouter").set_process_input(false)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	scene=load(ENTRIES[entry]).instantiate()
	root.add_child(scene)
	current_scene=scene
	await create_timer(.8,false).timeout
	if entry=="menu":
		scene.get_node("StartMenuMenu/BattleTest").emit_signal("pressed")
		await create_timer(.2,false).timeout
		_press("开始战斗测试")
		await create_timer(.5,true).timeout
		await _shot("menu_briefing.png")
		_press("开始战斗")
		var deadline:=Time.get_ticks_msec()+20000
		while current_scene.scene_file_path!="res://tscn/battle/BattleScene.tscn" or root.get_node("BattleTransitionManager").call("is_flow_transition_active"):
			await create_timer(.1,true).timeout
			if Time.get_ticks_msec()>deadline:
				push_error("Menu battle transition did not finish")
				quit(1)
				return
		scene=current_scene
		await create_timer(.4,false).timeout
		var toggle:=scene.find_child("PlanningToggle",true,false)
		if toggle: toggle.emit_signal("pressed")
		await create_timer(.3,false).timeout
		await _shot("menu_battle.png")
	elif entry=="lab":
		for item in [["战斗·通用出手",.55,"lab_attack.png"],["战斗·受击",.12,"lab_hurt.png"],["战斗·应对防御",.68,"lab_guard.png"],["战斗·闪避",.68,"lab_dodge.png"],["战斗·休整回气",.46,"lab_restore.png"]]:
			_press(item[0])
			await create_timer(item[1],false).timeout
			await _shot(item[2])
		if capture_motion:
			Engine.max_fps=30
			for item in [["战斗·应对防御","guard"],["战斗·闪避","dodge"]]:
				var motion_dir: String="res://tmp/ink_local_focus_motion/"+item[1]+"/"
				DirAccess.make_dir_recursive_absolute(motion_dir)
				_press(item[0])
				for index in range(84):
					await process_frame
					await RenderingServer.frame_post_draw
					root.get_texture().get_image().save_png(motion_dir+"motion_%03d.png"%index)
		if keep_open:
			_press("战斗·应对防御")
	else:
		scene.set_process(false)
		scene.set_process_unhandled_input(false)
		scene.call("_set_action","restore")
		scene.set("cycle_time",1.45)
		scene.call("_refresh_cycle")
		await _shot(entry+"_restore.png")
	print("INK_ENTRY_CAPTURE_COMPLETE: ",entry)
	if keep_open:
		root.gui_disable_input=false
		root.get_node("GameplayInputRouter").set_process_input(true)
		root.position=Vector2i(100,60)
		return
	scene.queue_free()
	await process_frame
	await process_frame
	quit()

func _press(label: String) -> void:
	for button in root.find_children("*","Button",true,false):
		if button.text==label:
			button.emit_signal("pressed")
			return
	push_error("Missing entry button: "+label)
	quit(1)

func _shot(filename: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var result:=root.get_texture().get_image().save_png(OUTPUT+filename)
	if result!=OK:
		push_error("Entry capture failed: "+filename)
		quit(1)
