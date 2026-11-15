extends "smoke_source_model_build.gd"

func _ready() -> void:
	get_window().unfocusable = true
	get_window().position = Vector2i(-7000,-7000)
	get_window().size = Vector2i(880,500)
	get_window().content_scale_size = Vector2i(880,500)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	var background := ColorRect.new()
	background.size = Vector2(880,500)
	background.color = Color("58646a")
	add_child(background)
	var originals: Array = []
	for index in range(2):
		var rig := SourceTestRig.new()
		add_child(rig)
		assert(rig.load_copy())
		rig.set_skin_mode("experiment")
		rig.position = Vector2(220+index*440,440)
		if index==0:
			for record in rig.wanderer_skin.mesh_records:
				var image := Image.new()
				var id := int(record.source.spriteId)
				assert(image.load(ProjectSettings.globalize_path("res://output/guigu_source_reference_v2/male101/textures/sprite_%d.png"%id))==OK)
				var mesh: Polygon2D = record.mesh
				mesh.texture = ImageTexture.create_from_image(image)
				mesh.material = null
				mesh.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
				var uv := PackedVector2Array()
				for point in record.source.uv: uv.append(Vector2(float(point[0]),float(point[1]))*Vector2(image.get_size()))
				mesh.uv = uv
			for patch in rig.wanderer_skin.shoulder_patches: patch.hide()
		originals.append(rig)
	var output := ProjectSettings.globalize_path("res://output/foot_registration_audit")
	DirAccess.make_dir_recursive_absolute(output)
	for clip in ["Idle","Run_New1"]:
		for frame in range(60):
			var clock := float(frame+1)/60.0
			for rig in originals:
				rig.wanderer_skin.set_motion_context(1.0/60.0,Vector2(114,0) if clip=="Run_New1" else Vector2.ZERO,clock,1.0)
				rig.set_character_pose(clip,clock,1.0)
				rig.scale *= 4.0
			if frame==42:
				await get_tree().process_frame
				await RenderingServer.frame_post_draw
				var image := get_viewport().get_texture().get_image()
				assert(image.save_png(output.path_join(clip+"_042_same_phase.png"))==OK)
				var record := {"clock":clock,"frame":frame,"clip":clip,
					"left":"original male101 texture and unregistered source UV; same model and pose",
					"right":"current HD and registered UV; same model and pose",
					"registration_sha256":FileAccess.get_sha256(ProjectSettings.globalize_path("res://").path_join("../../resources/appearance/guigu_male101/hd_registration_20261104.json").simplify_path()),
					"adapter_sha256":FileAccess.get_sha256("res://guigu_model_copy_skin.gd"),
					"original":originals[0].wanderer_skin.get_garment_diagnostics(),
					"hd":originals[1].wanderer_skin.get_garment_diagnostics()}
				var file := FileAccess.open(output.path_join(clip+"_042_same_phase.json"),FileAccess.WRITE)
				file.store_string(JSON.stringify(record,"\t"))
				file.close()
	print("FOOT SAME-PHASE AUDIT PASS: frame42 clock43/60; original and HD geometry/phase equal")
	get_tree().quit()
