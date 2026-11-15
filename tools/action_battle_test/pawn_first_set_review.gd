extends SceneTree

const Avatar = preload("pawn_four_way_avatar.gd")
const Adapter = preload("pawn_appearance_profile.gd")
var stage: Node2D

func _initialize() -> void:
	call_deferred("_capture")

func _label(value: String, position_value: Vector2, width: float, font_size: int) -> void:
	var label := Label.new()
	label.text=value
	label.position=position_value
	label.size=Vector2(width,50)
	label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER
	var font := SystemFont.new()
	font.font_names=PackedStringArray(["Microsoft YaHei"])
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",font_size)
	label.add_theme_color_override("font_color",Color("46534c"))
	stage.add_child(label)

func _capture() -> void:
	root.size=Vector2i(1280,650)
	root.position=Vector2i(-7000,-7000)
	stage=Node2D.new()
	root.add_child(stage)
	var background := ColorRect.new()
	background.color=Color("eeeae0")
	background.size=Vector2(1280,650)
	stage.add_child(background)
	_label("第一套 · 四方向效果",Vector2(40,28),1200,28)
	var adapter := Adapter.new()
	adapter.build(stage)
	var profile: Dictionary = adapter.get_demo_sample_profile()
	profile=adapter.with_field(profile,"appearance_hair_style",0)
	profile=adapter.with_field(profile,"appearance_hair_back_style",0)
	var directions := [Vector2.DOWN,Vector2.UP,Vector2.LEFT,Vector2.RIGHT]
	var names := ["正面","背面","左侧","右侧"]
	var diagnostics := []
	for index in range(4):
		var pawn := Avatar.new()
		stage.add_child(pawn)
		pawn.build()
		pawn.set_appearance(profile)
		pawn.set_direction(directions[index])
		pawn.position=Vector2(176+index*304,510)
		pawn.scale=Vector2.ONE*3.3
		diagnostics.append(pawn.get_diagnostics())
		_label(names[index],Vector2(40+index*304,94),272,21)
	_label("前后发独立 · 以耳朵划分",Vector2(40,580),1200,18)
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	var output := "res://output/pawn_four_way_20261105/approved_base_ear_split"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
	root.get_texture().get_image().save_png(output.path_join("four_directions.png"))
	FileAccess.open(output.path_join("render_record.json"),FileAccess.WRITE).store_string(JSON.stringify({"actual_renderer":true,"profile":profile,"directions":diagnostics,"approved_base_sha256":FileAccess.get_sha256("res://tools/action_battle_test/art/pawn_four_way_20261105/base_parts.png")},"\t"))
	adapter.unbind()
	print("PAWN FIRST SET FOUR DIRECTION CAPTURE PASS")
	quit()
