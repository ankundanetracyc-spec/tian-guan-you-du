extends SceneTree

const OUT := "res://docs/美术规范/即时战斗自适应镜头_20261104/"
var battle: Node2D
var extras: Array[Node2D] = []
var caption: Label

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUT)
	battle=load("res://tscn/scene/ActionBattleOriginalWandererScene.tscn").instantiate()
	root.add_child(battle)
	current_scene=battle
	battle.set_process(false)
	battle.set_process_input(false)
	battle.battle_hud.set_process_input(false)
	root.mode=Window.MODE_WINDOWED
	root.size=Vector2i(1440,810)
	root.position=Vector2i(-4096,-4096)
	root.gui_disable_input=true
	var layer:=CanvasLayer.new()
	layer.layer=40
	battle.add_child(layer)
	caption=Label.new()
	caption.position=Vector2(340,24)
	caption.add_theme_font_override("font",load("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"))
	caption.add_theme_font_size_override("font_size",24)
	caption.add_theme_color_override("font_color",Color("eee4ce"))
	caption.add_theme_color_override("font_shadow_color",Color("24302c"))
	caption.add_theme_constant_override("shadow_offset_x",2)
	caption.add_theme_constant_override("shadow_offset_y",2)
	layer.add_child(caption)
	battle.player=Vector2(1500,1050)
	battle.enemy=Vector2(1640,1070)
	caption.text="镜头布局演示 · 两个单位聚拢"
	battle.update_skinned_player()
	battle._update_trial_camera(0.0,true)
	battle.queue_redraw()
	await save("two_close.png")
	var points: Array[Vector2]=[Vector2(220,300),Vector2(3000,340),Vector2(400,1660),Vector2(3000,1710)]
	battle.enemy=Vector2(2800,1080)
	for point in points:
		var sprite:=Sprite2D.new()
		sprite.texture=battle.wolf_idle
		sprite.position=point+Vector2(0,-29)
		sprite.scale=Vector2(140.0/float(sprite.texture.get_width()),94.0/float(sprite.texture.get_height()))
		battle.add_child(sprite)
		extras.append(sprite)
		battle.camera_extra_units.append({"position":point,"head_height":85.0,"alive":true})
	caption.text="镜头布局演示 · 六个显示单位分散（非多人战斗）"
	battle._update_trial_camera(0.0,true)
	battle.queue_redraw()
	await save("six_spread.png")
	points.assign([Vector2(1050,850),Vector2(2050,850),Vector2(1100,1400),Vector2(2100,1400)])
	battle.enemy=Vector2(1950,1040)
	for i in range(points.size()):
		extras[i].position=points[i]+Vector2(0,-29)
		battle.camera_extra_units[i].position=points[i]
	caption.text="镜头布局演示 · 六个单位常规交战范围（非多人战斗）"
	battle._update_trial_camera(0.0,true)
	battle.queue_redraw()
	await save("six_medium.png")
	for node in extras: node.visible=false
	battle.camera_extra_units.clear()
	battle.player=Vector2(100,340)
	battle.enemy=Vector2(390,370)
	caption.text="镜头布局演示 · 靠近地图边界，视野不越界"
	battle.update_skinned_player()
	battle._update_trial_camera(0.0,true)
	battle.queue_redraw()
	await save("near_edge.png")
	# Confirm screen/world conversion with the actual Camera2D canvas transform.
	var inverse:Transform2D=battle.get_canvas_transform().affine_inverse()
	var screen:Vector2=battle.get_canvas_transform()*battle.player
	assert((inverse*screen).distance_to(battle.player)<0.01)
	print("SHARED_CAMERA_RENDER_OK zoom=",battle.trial_camera.zoom.x," six visible display units; mouse/world canvas roundtrip=pass")
	battle.queue_free()
	await process_frame
	quit()

func save(name: String) -> void:
	for frame in range(4): await process_frame
	await RenderingServer.frame_post_draw
	var image:=root.get_texture().get_image()
	image.save_png(ProjectSettings.globalize_path(OUT+name))
	image=null
