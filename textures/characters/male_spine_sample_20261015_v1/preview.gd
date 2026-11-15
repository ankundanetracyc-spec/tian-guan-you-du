extends SceneTree

const PACKAGE = "res://textures/characters/male_spine_sample_20261015_v1/"
const CLIPS = ["idle", "ready_cast", "ready_sword", "cast", "attack", "attack_rising", "attack_leap", "hurt", "knockback_return", "rest", "breath", "death_kneel"]
const TITLES = ["防守待机", "施法准备", "剑指准备", "施法", "横击", "上挑", "跃击", "受击打断", "倒飞回位", "调息", "吐纳", "跪地终姿"]
var spine: Node2D
var stage: Node2D
var ui: Control
var status: Label
var clip := "idle"
var sample_paused := false
var bones_visible := false
var frame_time := 0.0
var audit_running := false
var rig: Dictionary
var bone_overlay: Node2D

class Inputs extends Node:
	signal key(event)
	func _unhandled_key_input(event: InputEvent) -> void:
		key.emit(event)

class Overlay extends Node2D:
	var sample: SceneTree
	func _draw() -> void:
		if not sample.bones_visible or sample.clip == "death_kneel" and sample.frame_time >= 0.82:
			return
		for b in sample.rig.bones:
			if not b.has("parent") or b.name.contains("finger") or b.name.contains("thumb") or b.name.contains("pinky") or b.name.contains("middle") or b.name.contains("ring"):
				continue
			var child: Dictionary = sample.spine.call("get_bone", b.name)
			var parent: Dictionary = sample.spine.call("get_bone", b.parent)
			if not child.has("worldX") or not parent.has("worldX"):
				continue
			var a := Vector2(parent.worldX, -parent.worldY)
			var c := Vector2(child.worldX, -child.worldY)
			draw_line(a, c, Color(0.9, 0.5, 0.15, 0.8), 3.0)
			draw_circle(c, 5.0, Color(0.2, 0.9, 0.8, 0.9))

func _initialize() -> void:
	_setup.call_deferred()

func _setup() -> void:
	root.title = "男修 Spine 样片 · 20261015 v1 · Godot 4.7.2 / Spine 3.8"
	root.mode = Window.MODE_WINDOWED
	root.size = Vector2i(1440, 960)
	root.content_scale_size = Vector2i(1440, 960)
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	root.min_size = Vector2i(1000, 700)
	root.position = Vector2i(140, 30)
	root.transparent_bg = false
	root.world_2d = World2D.new()
	RenderingServer.set_default_clear_color(Color("e9e1d3"))
	var input := Inputs.new()
	root.add_child(input)
	input.key.connect(_key)
	rig = JSON.parse_string(FileAccess.get_file_as_string(PACKAGE + "runtime/male_sample.json"))
	if not ClassDB.can_instantiate("Spine"):
		push_error("This preview requires Godot 4.7.2 with Spine 3.8")
		quit(2)
		return
	stage = Node2D.new()
	root.add_child(stage)
	spine = ClassDB.instantiate("Spine")
	stage.add_child(spine)
	spine.position = Vector2(820, 850)
	spine.scale = Vector2(0.52, 0.52)
	# Native binary texture avoids scanning/importing the user's entire workspace.
	# The portable PNG/atlas remains in the package for Spine authoring.
	var atlas_image := Image.load_from_file(PACKAGE + "runtime/male_sample.png")
	var atlas_texture := ImageTexture.create_from_image(atlas_image)
	var saved := ResourceSaver.save(atlas_texture, PACKAGE + "runtime/male_sample_texture.res", ResourceSaver.FLAG_COMPRESS)
	if saved != OK:
		push_error("Unable to prepare native atlas texture")
		quit(5)
		return
	spine.call("set_resource", load(PACKAGE + "runtime/male_sample.spinejson"))
	spine.call("set_active", false)
	bone_overlay = Overlay.new()
	bone_overlay.sample = self
	bone_overlay.position = spine.position
	bone_overlay.scale = spine.scale
	stage.add_child(bone_overlay)
	var floor := Line2D.new()
	floor.points = PackedVector2Array([Vector2(420,850),Vector2(1330,850)])
	floor.default_color = Color("b4ada1")
	floor.width = 1
	stage.add_child(floor)
	ui = Control.new()
	root.add_child(ui)
	ui.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var title := Label.new()
	title.text = "男修 · 可动素材样片"
	title.position = Vector2(38,28)
	title.add_theme_font_size_override("font_size",32)
	ui.add_child(title)
	var sub := Label.new()
	sub.text = "70 骨 / 23 槽 / 26 附件 / 12 动作\n独立测试包 · 未接正式战斗 · 无剑"
	sub.position = Vector2(38,78)
	sub.add_theme_font_size_override("font_size",17)
	ui.add_child(sub)
	for i in range(CLIPS.size()):
		var name: String = CLIPS[i]
		_button(TITLES[i],Vector2(38,155+i*45),func(): play_clip(name))
	_button("暂停 / 继续 [空格]",Vector2(38,715),func(): sample_paused = not sample_paused)
	_button("后退一帧 [←]",Vector2(38,760),func(): step_frame(-1))
	_button("前进一帧 [→]",Vector2(38,805),func(): step_frame(1))
	_button("骨骼 [B]",Vector2(260,715),func(): bones_visible = not bones_visible)
	_button("浅 / 深底",Vector2(260,760),_background)
	_button("保存截图 [F12]",Vector2(260,805),capture)
	status = Label.new()
	status.position = Vector2(420,900)
	status.add_theme_color_override("font_color",Color("293e38"))
	status.add_theme_font_size_override("font_size",19)
	ui.add_child(status)
	for control in ui.get_children():
		if control is Label:
			control.add_theme_color_override("font_color",Color("293e38"))
	play_clip("idle",0.0)
	print("[male-sample] Engine=", Engine.get_version_info().string, " bones=",rig.bones.size())
	for name in CLIPS:
		var length: float = spine.call("get_animation_length", name)
		if length <= 0:
			push_error("Missing clip: " + name)
			quit(3)
			return
	if "--sample-audit" in OS.get_cmdline_user_args():
		audit_running = true
		_audit.call_deferred()

func _button(text: String, pos: Vector2, action: Callable) -> void:
	var button := Button.new()
	button.text = text
	button.position = pos
	button.size = Vector2(205,38)
	button.add_theme_font_size_override("font_size",17)
	button.pressed.connect(action)
	button.focus_mode = Control.FOCUS_NONE
	ui.add_child(button)

func play_clip(name: String, mix := 0.16) -> void:
	# Death remains frozen until the user deliberately selects another clip.
	clip = name
	frame_time = 0.0
	sample_paused = false
	spine.call("play",name,1.0,name in ["idle","ready_cast","ready_sword"],0,mix)
	spine.call("set_active",false)
	spine.call("_animation_process",0.0)

func _process(delta: float) -> bool:
	if not is_instance_valid(spine):
		return false
	if not sample_paused and not audit_running:
		spine.call("_animation_process",delta)
		frame_time = spine.call("tell",0)
		var length: float = spine.call("get_animation_length",clip)
		if frame_time >= length:
			if clip == "death_kneel":
				spine.call("seek",0,length)
				spine.call("_animation_process",0.0)
				frame_time = length
				sample_paused = true
			elif clip not in ["idle","ready_cast","ready_sword"]:
				play_clip("idle",0.22)
	if is_instance_valid(status):
		status.text = "%s  |  %.3f s  |  第 %d 帧  |  %s" % [clip,frame_time,roundi(frame_time*60),"已暂停" if sample_paused else "播放中"]
	bone_overlay.queue_redraw()
	return false

func step_frame(direction: int) -> void:
	sample_paused = true
	var length: float = spine.call("get_animation_length",clip)
	frame_time = clampf(frame_time+direction/60.0,0.0,length)
	spine.call("seek",0,frame_time)
	spine.call("_animation_process",0.0)

var dark := false
func _background() -> void:
	dark = not dark
	RenderingServer.set_default_clear_color(Color("26332e") if dark else Color("e9e1d3"))
	for c in ui.get_children():
		if c is Label:
			c.add_theme_color_override("font_color",Color("e9e1d3") if dark else Color("293e38"))

func _key(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	match event.keycode:
		KEY_SPACE: sample_paused = not sample_paused
		KEY_LEFT: step_frame(-1)
		KEY_RIGHT: step_frame(1)
		KEY_B: bones_visible = not bones_visible
		KEY_F12: capture()
		KEY_C: play_clip("cast")
		KEY_A: play_clip("attack")
		KEY_R: play_clip("attack_rising")
		KEY_L: play_clip("attack_leap")
		KEY_H: play_clip("hurt",0.08)
		KEY_K: play_clip("knockback_return")
		KEY_D: play_clip("death_kneel")
		KEY_I: play_clip("idle")

func capture(filename := "") -> void:
	await RenderingServer.frame_post_draw
	if filename == "":
		filename = "manual_%s_%04d.png" % [clip,roundi(frame_time*60)]
	root.get_texture().get_image().save_png(ProjectSettings.globalize_path(PACKAGE+"preview/"+filename))
	print("[male-sample] screenshot ",filename)

func _audit() -> void:
	await process_frame
	var report := {"engine":Engine.get_version_info().string,"clips":{},"pause_step":false,"death_hold":false,"interrupt_return":false,"grounded_collapse":[]}
	for name in CLIPS:
		play_clip(name,0.0)
		sample_paused = true
		var length: float = spine.call("get_animation_length",name)
		var samples := []
		for fraction in [0.0,0.25,0.5,0.75,1.0]:
			frame_time = length*fraction
			spine.call("seek",0,frame_time)
			spine.call("_animation_process",0.0)
			await process_frame
			samples.append({"time":frame_time,"hips":spine.call("get_bone","hips")})
			await capture("audit_%s_%02d.png" % [name,roundi(fraction*100)])
		report.clips[name] = {"duration":length,"samples":samples}
	play_clip("death_kneel",0.0)
	sample_paused = true
	for t in [0.2,0.4,0.6,0.78,0.81,0.83]:
		frame_time = t
		spine.call("seek",0,t)
		spine.call("_animation_process",0.0)
		await process_frame
		report.grounded_collapse.append({"time":t,"near":spine.call("get_bone","foot_near"),"far":spine.call("get_bone","foot_far")})
		await capture("collapse_%03d.png" % roundi(t*100))
	# Exercise the same preview clock and interruption path as live playback.
	play_clip("attack_leap",0.0)
	spine.call("_animation_process",0.3)
	play_clip("hurt",0.08)
	audit_running = false
	for n in range(60):
		_process(1.0/60.0)
	audit_running = true
	var root_bone: Dictionary = spine.call("get_bone","root")
	report.interrupt_return = clip == "idle" and absf(root_bone.worldX) < 0.1 and absf(root_bone.worldY) < 0.1
	play_clip("attack",0.0)
	step_frame(1)
	var first: float = spine.call("tell",0)
	await process_frame
	var held: float = spine.call("tell",0)
	step_frame(1)
	var second: float = spine.call("tell",0)
	report.pause_step = is_equal_approx(first,held) and is_equal_approx(second-first,1.0/60.0)
	play_clip("death_kneel",0.0)
	frame_time = spine.call("get_animation_length",clip)
	spine.call("seek",0,frame_time)
	spine.call("_animation_process",0.0)
	sample_paused = true
	await process_frame
	await RenderingServer.frame_post_draw
	var before := root.get_texture().get_image().get_data()
	await process_frame
	await RenderingServer.frame_post_draw
	report.death_hold = before == root.get_texture().get_image().get_data()
	var file := FileAccess.open(PACKAGE+"preview/runtime_audit.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	play_clip("idle",0.0)
	sample_paused = true
	_background()
	await process_frame
	await capture("runtime_dark.png")
	_background()
	bones_visible = true
	await process_frame
	await capture("runtime_bones.png")
	bones_visible = false
	if "--sample-motion" in OS.get_cmdline_user_args():
		await _motion()
	print("[male-sample] AUDIT pause_step=",report.pause_step," death_hold=",report.death_hold)
	spine.call("set_resource",null)
	stage.free()
	ui.free()
	await process_frame
	await RenderingServer.frame_post_draw
	quit(0 if report.pause_step and report.death_hold and report.interrupt_return else 4)

func _motion() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(PACKAGE+"preview/motion"))
	var manifest := []
	var index := 0
	for name in ["idle","cast","attack","attack_rising","attack_leap","hurt","knockback_return","death_kneel"]:
		play_clip(name,0.0)
		sample_paused = true
		var length: float = spine.call("get_animation_length",name)
		var count := ceili(length*16.0)
		for f in range(count+1):
			frame_time = minf(length,f/16.0)
			spine.call("seek",0,frame_time)
			spine.call("_animation_process",0.0)
			await process_frame
			await RenderingServer.frame_post_draw
			var shot := root.get_texture().get_image()
			var cropped := shot.get_region(Rect2i(610,120,720,850))
			cropped.resize(360,425,Image.INTERPOLATE_LANCZOS)
			var name_out := "%04d.png" % index
			cropped.save_png(ProjectSettings.globalize_path(PACKAGE+"preview/motion/"+name_out))
			manifest.append({"file":name_out,"clip":name,"time":frame_time,"delay":500 if f==count and name=="death_kneel" else 63})
			index += 1
	var file := FileAccess.open(PACKAGE+"preview/motion/frames.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest,"\t"))
	file.close()
	print("[male-sample] Motion frames=",index)
