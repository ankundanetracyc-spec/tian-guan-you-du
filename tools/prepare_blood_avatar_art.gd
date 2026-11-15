extends SceneTree

const PACK := "res://textures/vfx/blood_qi_avatar_20261020_v4/"
const OUTPUT := "res://docs/美术规范/气血法相主体与环绕血气_20261020/"
const DEFINITIONS := [
	{"name":"left_idle", "source_root":Vector2i(590,755), "root":Vector2i(720,1184), "world_root":Vector2(-400,-1000), "path":[Vector2(590,755),Vector2(370,720),Vector2(185,615),Vector2(225,450),Vector2(365,310),Vector2(560,230)]},
	{"name":"right_idle", "source_root":Vector2i(700,750), "root":Vector2i(900,1178), "world_root":Vector2(305,-940), "path":[Vector2(700,750),Vector2(915,725),Vector2(1050,635),Vector2(1000,455),Vector2(900,310),Vector2(800,230)]},
	{"name":"left_punch", "source_root":Vector2i(176,563), "root":Vector2i(720,1184), "world_root":Vector2(-400,-1000), "path":[Vector2(176,563),Vector2(370,563),Vector2(620,640),Vector2(810,655),Vector2(980,670),Vector2(1125,690)]},
	{"name":"right_punch", "source_root":Vector2i(313,559), "root":Vector2i(900,1178), "world_root":Vector2(305,-940), "path":[Vector2(313,559),Vector2(530,650),Vector2(725,715),Vector2(900,720),Vector2(1019,715),Vector2(1117,754)]},
	{"name":"right_guard", "source_root":Vector2i(715,813), "root":Vector2i(900,1178), "world_root":Vector2(305,-940), "path":[Vector2(715,813),Vector2(900,850),Vector2(1060,815),Vector2(1030,650),Vector2(1000,500),Vector2(950,300)]}
]
var textures: Array[Texture2D] = []

func _initialize() -> void:
	call_deferred("_prepare")

func _prepare() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	root.position=Vector2i(-4096,-4096)
	var records: Array[Dictionary] = []
	for definition in DEFINITIONS:
		var art := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/"+definition.name+".png"))
		art.convert(Image.FORMAT_RGBA8)
		var source_size := art.get_size()
		var scaled_size := Vector2i(Vector2(source_size)*1.25)
		art.resize(scaled_size.x,scaled_size.y,Image.INTERPOLATE_LANCZOS)
		var canvas := Image.create(2048,2048,false,Image.FORMAT_RGBA8)
		canvas.fill(Color.TRANSPARENT)
		# Preserve the complete isolated body with a fixed safety border.
		# The source's transparent half is retained, not independently auto-cropped.
		# All poses share 1.25 source-pixel scale. Only translate each artist-marked
		# shoulder root to the fixed per-arm canvas anchor, never fit visible bounds.
		var offset := Vector2i(Vector2(definition.root)-Vector2(definition.source_root)*1.25)
		canvas.blit_rect(art,Rect2i(Vector2i.ZERO,art.get_size()),offset)
		canvas.save_png(PACK+definition.name+".png")
		textures.append(ImageTexture.create_from_image(canvas))
		var bounds := canvas.get_used_rect()
		var partial := 0
		var opaque := 0
		var transparent := 0
		var bytes := canvas.get_data()
		for index in range(3,bytes.size(),4):
			if bytes[index]==0: transparent+=1
			elif bytes[index]==255: opaque+=1
			else: partial+=1
		var path_length := 0.0
		for segment in range(1,definition.path.size()):
			path_length+=definition.path[segment-1].distance_to(definition.path[segment])*1.25
		records.append({"name":definition.name,"size":[2048,2048],"format":"RGBA8", "root":[definition.root.x,definition.root.y],"world_root":[definition.world_root.x,definition.world_root.y],"bounds":[bounds.position.x,bounds.position.y,bounds.size.x,bounds.size.y],"alpha":{"transparent":transparent,"partial":partial,"opaque":opaque},"registration_scale":1.25,"registration_offset":[offset.x,offset.y],"anatomical_centerline_approx_px":path_length})
	FileAccess.open(PACK+"asset_manifest.json",FileAccess.WRITE).store_string(JSON.stringify({"approval":"two_arms_v4.png","first_arm":"left_idle","reference_ground":[1024,1900],"reference_height":1375,"assets":records,"scope":"Authored idle and three action keys. One arm: left punches. Two arms: right punches or guards, left stays idle. Both behind the character; presentation only."},"\t"))
	var viewport := SubViewport.new()
	viewport.size=Vector2i(1200,1000)
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	for dark in [false,true]:
		for count in [1,2]:
			var stage := Node2D.new()
			viewport.add_child(stage)
			var background := ColorRect.new()
			background.size=Vector2(1200,1000)
			background.color=Color("23302f") if dark else Color("e0d6bd")
			stage.add_child(background)
			var actor := Node2D.new()
			actor.position=Vector2(600,900)
			actor.scale=Vector2.ONE*.38
			stage.add_child(actor)
			var figure := Image.load_from_file(ProjectSettings.globalize_path("res://textures/characters/side_ink_portrait_outfits_20261015_v3/male_idle.png"))
			var portrait := Sprite2D.new()
			portrait.texture=ImageTexture.create_from_image(figure)
			portrait.centered=false
			portrait.position=-Vector2(1024,1900)
			portrait.z_index=1
			actor.add_child(portrait)
			for index in range(count):
				var arm := Sprite2D.new()
				arm.texture=textures[index]
				arm.centered=false
				arm.offset=-Vector2(DEFINITIONS[index].root)
				arm.position=DEFINITIONS[index].world_root
				arm.scale=Vector2.ONE*(1.0 if index==0 else .94)
				arm.modulate=Color(1,1,1,.68 if index==0 else .72)
				arm.z_index=0
				actor.add_child(arm)
			await process_frame
			await RenderingServer.frame_post_draw
			var shot := viewport.get_texture().get_image()
			shot.save_png(OUTPUT+("single" if count==1 else "double")+("_dark" if dark else "_paper")+".png")
			stage.queue_free()
			await process_frame
	print("BLOOD_AVATAR_ART_PREPARED: two registered RGBA sprites and four native composition renders; no combat data writes.")
	quit()
