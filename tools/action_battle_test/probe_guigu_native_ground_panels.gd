extends Node2D

## Read-only rendered evidence of the existing actor/shadow coordinate contract.
const NativeRuntime = preload("guigu_native_first_runtime.gd")
const ZOOM := 3.25
var labels: Array[Dictionary] = []
var font := SystemFont.new()
var record: Dictionary = {}

func _draw() -> void:
	draw_rect(Rect2(0,0,1320,1140),Color("cbd0c6"))
	for index in range(6):
		var origin := Vector2(20+(index%3)*440,20+(index/3)*560)
		draw_rect(Rect2(origin,Vector2(420,540)),Color("e4e4d9"))
		var floor_y := origin.y+510
		draw_line(Vector2(origin.x+20,floor_y),Vector2(origin.x+400,floor_y),Color("9a4539"),1.0)
		for item in labels:
			draw_string(font,item.position,item.text,HORIZONTAL_ALIGNMENT_LEFT,-1,17,Color("263633"))

func _ready() -> void:
	font.font_names = PackedStringArray(["Microsoft YaHei"])
	get_window().size = Vector2i(1320,1140)
	get_window().content_scale_size = Vector2i(1320,1140)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().position = Vector2i(-7000,-7000)
	get_window().unfocusable = true
	call_deferred("_capture")

func _capture() -> void:
	var source_path: String = ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().get_base_dir().get_base_dir().path_join("output/guigu_native_ground_audit_20261105/source_alpha_ground_probe.json")
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(source_path))
	var cases := [["Idle",0.0],["Run_New1",float(source.clips.Run_New1.best_phase)],["Run_New1",float(source.clips.Run_New1.worst_phase)],["Run_New2",float(source.clips.Run_New2.worst_phase)],["AttackRun",0.0],["Step1",0.0]]
	var panels := []
	for index in range(cases.size()):
		var case: Array = cases[index]
		var clip: String = case[0]
		var phase: float = case[1]
		var origin := Vector2(20+(index%3)*440,20+(index/3)*560)
		var host := Node2D.new()
		host.position = origin+Vector2(210,510)
		host.scale = Vector2.ONE*ZOOM
		add_child(host)
		var shadow := Polygon2D.new()
		var ellipse := PackedVector2Array()
		for vertex in range(48): ellipse.append(Vector2(cos(vertex*TAU/48.0)*14,sin(vertex*TAU/48.0)*3.5+4.0))
		shadow.polygon = ellipse
		shadow.color = Color(0,0,0,0.18)
		host.add_child(shadow)
		var actor := NativeRuntime.new()
		host.add_child(actor)
		assert(actor.build(),str(actor.load_errors))
		var skin: Variant = actor.wanderer_skin
		skin.physics_enabled = false
		var time: float = phase*float(skin.clips[clip].duration)
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,time,1.0)
		actor.set_character_pose(clip,time,1.0)
		var frame := mini(int(round(phase*144.0)),143)
		var alpha: Dictionary = source.clips[clip].frames[frame]
		var displayed_clearance: float = float(alpha.core_visible_shoe_clearance)-actor.rig_root_node.position.y
		labels.append({"position":origin+Vector2(12,26),"text":"%s phase %.5f"%[clip,phase]})
		labels.append({"position":origin+Vector2(12,51),"text":"Visible core shoe clearance %.3fpx"%displayed_clearance})
		labels.append({"position":origin+Vector2(12,76),"text":"3.25x native scale; red line = actor foot origin"})
		var legs := []
		for mesh_record in skin.mesh_records:
			if int(mesh_record.sprite_id)!=87557: continue
			var vertices := []
			for point in mesh_record.mesh.polygon: vertices.append([point.x,point.y])
			legs.append({"renderer":mesh_record.renderer_path,"actual_native_vertices":vertices})
		panels.append({"clip":clip,"phase":phase,"source_time":time,"actual_ground_offset":[skin.ground_offset.x,skin.ground_offset.y],"whole_actor_render_translation_y":actor.rig_root_node.position.y,"actual_rig_root_origin":str(skin.world_transforms[skin.rig_data.rigRootPath].origin),"native_height":skin.native_height,"actual_leg_meshes":legs,"predicted_source_alpha_clearance":alpha.core_visible_shoe_clearance,"predicted_displayed_alpha_clearance":displayed_clearance})
	queue_redraw()
	await get_tree().process_frame
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var directory := source_path.get_base_dir()
	assert(get_viewport().get_texture().get_image().save_png(directory.path_join("native_ground_panels.png"))==OK)
	record = {"scope":"Existing actual native runtime, source-alpha-derived visible shoe clearances. No actor/texture/runtime files changed. Static source3D poses use physics_disabled; cloth/hair physics does not move the shoe chains. Arena fallback shadow ellipse is reproduced at its exact(+0,+4),14x3.5 parameters for coordinate evidence.","zoom":ZOOM,"panels":panels}
	FileAccess.open(directory.path_join("native_ground_panels.json"),FileAccess.WRITE).store_string(JSON.stringify(record,"\t"))
	print("READ-ONLY NATIVE GROUND PANELS SAVED ",directory)
	get_tree().quit()
