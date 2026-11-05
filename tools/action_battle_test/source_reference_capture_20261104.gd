extends Node2D

const Player := preload("source_reference_player_20261104.gd")
const OUTPUT := "res://output/guigu_source_reference_v2"
var face: SystemFont

func label(text_value: String, point: Vector2, font_size: int) -> void:
	var item := Label.new()
	item.text = text_value
	item.position = point
	item.add_theme_font_override("font",face)
	item.add_theme_font_size_override("font_size",font_size)
	item.modulate = Color("e8ebee")
	add_child(item)

func _ready() -> void:
	get_window().unfocusable = true
	get_window().position = Vector2i(-7000,-7000)
	get_window().size = Vector2i(1000,660)
	get_viewport().content_scale_size = Vector2i(1000,660)
	face = SystemFont.new()
	face.font_names = PackedStringArray(["Microsoft YaHei"])
	var backdrop := ColorRect.new()
	backdrop.size = Vector2(1000,660)
	backdrop.color = Color("4c575e")
	add_child(backdrop)
	var data_file := OUTPUT+"/source_reference_vertex_frames.json"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--data="): data_file = argument.trim_prefix("--data=")
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(data_file))
	var texture_root: String = data_file.get_base_dir().path_join("textures")
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(texture_root)):
		texture_root = data_file.get_base_dir().path_join("readback/textures")
	var exact := Player.new()
	add_child(exact)
	exact.build(data,texture_root)
	assert(exact.draws.size()==data.meshes.size())
	exact.position = Vector2(250,580)
	var secondary := Player.new()
	secondary.secondary = true
	add_child(secondary)
	secondary.build(data,texture_root)
	assert(secondary.draws.size()==data.meshes.size())
	secondary.position = Vector2(750,580)
	label("鬼谷原贴图 · 原网格 · 原动画基形",Vector2(50,22),23)
	label("同骨链 + 示意惯性",Vector2(590,22),23)
	label("已定位曲线与常量；排除未知Body绑定",Vector2(54,61),17)
	label("自拟末段阻尼，非原DynamicBone复现",Vector2(550,61),17)
	var floor_line := Line2D.new()
	floor_line.points = PackedVector2Array([Vector2(40,580),Vector2(960,580)])
	floor_line.width = 1.0
	floor_line.default_color = Color(1,1,1,.2)
	add_child(floor_line)
	var subject := "男修101装配" if "HumanRootMan" in str(data.get("rigRootPath","")) else "女修10002个案"
	label(subject+"；原作状态机／碰撞与运行时混合未复现",Vector2(181,614),18)
	var output := ProjectSettings.globalize_path(data_file.get_base_dir())
	DirAccess.make_dir_recursive_absolute(output)
	for clip_name in ["Run_New1","Run_New2","Idle"]:
		for frame in range(48):
			exact.set_pose(clip_name,frame/48.0)
			secondary.set_pose(clip_name,frame/48.0)
			await RenderingServer.frame_post_draw
			var result := get_viewport().get_texture().get_image()
			assert(result.save_png(output.path_join("source_reference_%s_%03d.png" % [clip_name,frame])) == OK)
			result = null
	print("SOURCE_REFERENCE_CAPTURE_PASS: original texture + source bindpose LBS, 3 clips x 48 phases")
	exact.release()
	secondary.release()
	await get_tree().process_frame
	get_tree().quit()
