extends Node2D

## Independent original evaluated vertices are the pose oracle. This smoke does
## not create art, enter MainScene, or write a personal save.
var checks := 0
var failures: Array[String] = []
var details: Dictionary = {}
var resource_dir := ""
var project_directory := ""
var poses := 0
var vertices := 0
var maximum_source_vertex_error_pixels := 0.0
var maximum_full_uv_error_pixels := 0.0
var maximum_physics_segment_length_error_units := 0.0
var maximum_bone_projection_error_pixels := 0.0
var maximum_hair_rigid_vertex_error_pixels := 0.0
var maximum_hair_head_mount_error_pixels := 0.0
var maximum_reference_hair_source_error_pixels := 0.0
var hair_samples := 0
var nonfinite_values := 0

func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		push_error("NATIVE SOURCE SMOKE: " + label)

func _ready() -> void:
	resource_dir = get_script().resource_path.get_base_dir()
	project_directory = ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().get_base_dir().get_base_dir()
	call_deferred("_run")

func _read(path: String) -> Dictionary:
	var value: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	_check(value is Dictionary, "JSON loads: " + path.get_file())
	return value if value is Dictionary else {}

func _has_property(object: Object, key: String) -> bool:
	for property in object.get_property_list():
		if str(property.name) == key: return true
	return false

func _set_physics(skin: RefCounted, enabled: bool) -> bool:
	for key in ["physics_enabled", "source_physics_enabled", "dynamic_bone_enabled", "use_source_physics"]:
		if _has_property(skin, key):
			skin.set(key, enabled)
			details.physics_switch = key
			return true
	return false

func _record_source(record: Dictionary) -> Dictionary:
	for key in ["source", "record"]:
		if record.get(key) is Dictionary: return record[key]
	return record

func _mesh_by_renderer(skin: RefCounted) -> Dictionary:
	var result := {}
	if not _has_property(skin, "mesh_records"): return result
	for record in skin.mesh_records:
		var source := _record_source(record)
		var path: String = str(source.get("rendererPath", record.get("rendererPath", "")))
		if not path.is_empty() and record.get("mesh") is Polygon2D:
			result[path] = record.mesh
	return result

func _pose_snapshot(skin: RefCounted, rig: Node2D, include_hair: bool = true) -> Array:
	var result: Array = [rig.transform]
	for record in skin.mesh_records:
		var source := _record_source(record)
		if not include_hair and int(source.get("sourceSpriteId", source.get("spriteId", 0))) == 87679: continue
		result.append(record.mesh.polygon.duplicate())
	return result

func _run() -> void:
	var runtime_path := resource_dir.path_join("guigu_native_first_runtime.gd")
	_check(ResourceLoader.exists(runtime_path), "new source-native runtime exists")
	if not ResourceLoader.exists(runtime_path):
		_finish()
		return
	var runtime_script: Variant = load(runtime_path)
	_check(runtime_script != null and runtime_script.can_instantiate(), "new source-native runtime compiles")
	if runtime_script == null or not runtime_script.can_instantiate():
		_finish()
		return
	var rig: Node2D = runtime_script.new()
	add_child(rig)
	var built: bool = rig.build()
	_check(built and rig.loaded, "new source-native actor builds through its actual runtime")
	if not built or not rig.loaded:
		details.build_errors = rig.get_runtime_summary()
		rig.queue_free()
		await get_tree().process_frame
		_finish()
		return
	var skin: RefCounted = rig.wanderer_skin
	# This oracle samples every untouched source clip at its exact requested
	# phase, independently of the new actual-game locomotion/attack composition.
	skin.locomotion_layer_enabled = false
	var audit := _read(project_directory.path_join("resources/appearance/guigu_native_first_20261105/source_contract_audit.json"))
	var reference := _read(project_directory.path_join("resources/appearance/guigu_male101/model_frames_20261104.json"))
	var meshes := _mesh_by_renderer(skin)
	_check(meshes.size() == 7, "actual actor retains seven character renderers with the user-approved own-hair exception")
	_check(skin.rig_data.rigRootPath == "/HumanRootMan/Root" and skin.rig_data.nodes.size() == 51 and skin.rig_data.boneNodes.size() == 24, "actual actor uses original male101 hierarchy and24 skin bones")
	_check(skin.rig_data.rigRootPath != "/10002/Root", "actual source actor does not reuse female10002 bone hierarchy")
	_check(is_equal_approx(skin.PIXELS_PER_UNIT, 100.0) and is_equal_approx(skin.display_scale, 1.0), "source actor keeps original100PPU and unmodified uniform native display ruler")
	for name in ["rig.json", "skins.json", "animations.json"]:
		_check(FileAccess.get_sha256(project_directory.path_join("resources/appearance/guigu_native_first_20261105/" + name)) == audit.input_sha256[name], "native data is the exact audited original male source " + name)
	var can_disable := _set_physics(skin, false)
	_check(can_disable, "native source actor provides an explicit physics switch for an independent animation/LBS comparison")
	if can_disable and meshes.size() == 7 and not reference.is_empty():
		_sample_original_poses(rig, skin, meshes, reference)
		_check(maximum_source_vertex_error_pixels < 0.003, "all six non-hair body meshes match independent source3D evaluated data without added fitting or stretch")
		_check(nonfinite_values == 0, "no non-finite positions in source pose sampling")
	if not audit.is_empty(): _check_drawing_contract(meshes, reference, audit)
	if can_disable: _check_visible_sole_grounding(rig, skin)
	if can_disable:
		_set_physics(skin, true)
		var clock := 0.0
		for facing in [1.0, -1.0]:
			for frame in range(180):
				clock += 1.0 / 60.0
				rig.position += Vector2(facing * 1.9, 0.0)
				skin.set_motion_context(1.0 / 60.0, Vector2(facing * 114.0, 0.0), clock, facing)
				rig.set_character_pose("Run_New1", clock, facing)
				for mesh in meshes.values():
					for point in mesh.polygon:
						if not point.is_finite(): nonfinite_values += 1
				_measure_physics_segments(skin)
				_measure_rigid_hair(skin)
			_check(signf(rig.scale.x) == facing, "actual facade mirrors the source-native actor without resizing " + str(facing))
		var snapshot := _pose_snapshot(skin, rig)
		for frame in range(20):
			skin.set_motion_context(0.0, Vector2.ZERO, clock, -1.0)
			rig.set_character_pose("Run_New1", clock, -1.0)
		_check(_pose_snapshot(skin, rig) == snapshot, "zero simulation time freezes actual native physics and mesh positions")
		_check(nonfinite_values == 0, "source physics stays finite during actual movement and mirrored turning")
		_check(maximum_physics_segment_length_error_units < 0.00001, "source physics preserves actual animated source3D segment lengths in both facings")
		_check(maximum_hair_rigid_vertex_error_pixels < 0.001, "own bundled hair remains rigid on the original renderer/head transform during source physics")
		_check(maximum_hair_head_mount_error_pixels < 0.001, "own hair retains the original full-canvas head/neck mount during movement and turning")
	details.runtime_summary = rig.get_runtime_summary()
	details.diagnostics = skin.get_garment_diagnostics()
	rig.queue_free()
	await get_tree().process_frame
	var legacy: Node2D = runtime_script.new()
	add_child(legacy)
	var data_directory := project_directory.path_join("resources/appearance/guigu_native_first_20261105")
	var legacy_loaded: bool = legacy.load_runtime(data_directory.path_join("rig.json"), data_directory.path_join("skins.json"), data_directory.path_join("animations.json"), resource_dir.path_join("art"), "guigu_native_first_20261105")
	_check(legacy_loaded and legacy.loaded, "actual old load_runtime signature builds the new native actor")
	if legacy_loaded:
		_check(legacy.wanderer_skin.rig_data.rigRootPath == "/HumanRootMan/Root" and legacy.wanderer_skin.mesh_records.size() == 7, "old loader signature retains source male101 and seven meshes")
		legacy.set_skin_mode("reference")
		_check(legacy.wanderer_skin.use_original_textures, "old consumer reference mode selects original full textures")
		legacy.set_skin_mode("experiment")
		_check(not legacy.wanderer_skin.use_original_textures, "old consumer experiment mode selects the new painted full textures")
	legacy.queue_free()
	await get_tree().process_frame
	_finish()

func _sample_original_poses(rig: Node2D, skin: RefCounted, meshes: Dictionary, reference: Dictionary) -> void:
	var start_scale := Vector2.ONE
	for facing in [1.0, -1.0]:
		for clip in reference.clips:
			var data: Dictionary = reference.clips[clip]
			for frame in range(data.frames.size()):
				var time: float = float(data.duration) * float(frame) / float(data.frames.size())
				skin.set_motion_context(1.0 / 60.0, Vector2.ZERO, time, facing)
				rig.set_character_pose(str(clip), time, facing)
				if poses == 0: start_scale = rig.scale.abs()
				if not rig.scale.abs().is_equal_approx(start_scale): _check(false, "native actor display scale changed with source pose")
				for index in range(reference.meshes.size()):
					var source: Dictionary = reference.meshes[index]
					if int(source.spriteId) == 87679: continue
					if not meshes.has(source.rendererPath):
						_check(false, "actual source mesh missing: " + str(source.rendererPath))
						return
					var mesh: Polygon2D = meshes[source.rendererPath]
					var expected: Array = data.frames[frame][index]
					if mesh.polygon.size() != expected.size():
						_check(false, "source mesh vertex count changed: " + str(source.rendererPath))
						return
					for vertex in range(expected.size()):
						var actual: Vector2 = mesh.polygon[vertex] + skin.ground_offset
						if not actual.is_finite(): nonfinite_values += 1
						var point := Vector2(float(expected[vertex][0]), float(expected[vertex][1]))
						maximum_source_vertex_error_pixels = maxf(maximum_source_vertex_error_pixels, actual.distance_to(point))
						vertices += 1
				_measure_rigid_hair(skin)
				for bone_index in range(reference.bonePaths.size()):
					var world: Transform3D = skin.world_transforms[reference.bonePaths[bone_index]]
					var actual := Vector2(world.origin.x, -world.origin.y) * 100.0
					var before: Array = data.boneFrames[frame][bone_index]
					maximum_bone_projection_error_pixels = maxf(maximum_bone_projection_error_pixels, actual.distance_to(Vector2(before[0], before[1])))
				poses += 1
			var painted_pose := _pose_snapshot(skin, rig, false)
			rig.set_skin_mode("reference")
			_check(_pose_snapshot(skin, rig, false) == painted_pose, "original and newly painted non-hair textures share exact geometry " + str(clip) + "/" + str(facing))
			for index in range(reference.meshes.size()):
				var source: Dictionary = reference.meshes[index]
				if int(source.spriteId) != 87679: continue
				var original_hair: Polygon2D = meshes[source.rendererPath]
				_check(original_hair.polygon.size() == 41 and original_hair.polygons.size() == 54, "reference mode retains source101 long-hair mesh " + str(clip) + "/" + str(facing))
				if original_hair.polygon.size() == 41:
					var expected: Array = data.frames.back()[index]
					for vertex in range(expected.size()):
						var point: Vector2 = original_hair.polygon[vertex] + skin.ground_offset
						maximum_reference_hair_source_error_pixels = maxf(maximum_reference_hair_source_error_pixels, point.distance_to(Vector2(expected[vertex][0], expected[vertex][1])))
			rig.set_skin_mode("experiment")
			_check(_pose_snapshot(skin, rig, false) == painted_pose, "return to new non-hair paint keeps exact source geometry " + str(clip) + "/" + str(facing))
	_check(maximum_bone_projection_error_pixels < 0.003, "all51 actual3D source transforms match independent source projected bone poses")
	_check(maximum_reference_hair_source_error_pixels < 0.003, "reference101 long-hair geometry remains preserved beside the own-hair exception")
	details.source_pose_oracle = "Six non-hair active meshes: independent male101 source world3D*bindpose*vertices, original weights,144 original phases in each of six clips. Own-hair shape is a declared separate renderer/head-mounted exception. secondaryFrames surrogate is never sampled."
	details.original_display_scale = str(start_scale)

func _measure_physics_segments(skin: RefCounted) -> void:
	for component in skin.native_physics.components:
		for particle in component.particles:
			var parent_index := int(particle.parent_index)
			if parent_index < 0 or str(particle.path).is_empty(): continue
			var parent: Dictionary = component.particles[parent_index]
			if str(parent.path).is_empty(): continue
			var world: Transform3D = skin.world_transforms[particle.path]
			var parent_world: Transform3D = skin.world_transforms[parent.path]
			var source: Transform3D = skin.animated_world[particle.path]
			var source_parent: Transform3D = skin.animated_world[parent.path]
			maximum_physics_segment_length_error_units = maxf(maximum_physics_segment_length_error_units, absf(world.origin.distance_to(parent_world.origin) - source.origin.distance_to(source_parent.origin)))

func _check_visible_sole_grounding(rig: Node2D, skin: RefCounted) -> void:
	var profile := _read(project_directory.path_join("resources/appearance/guigu_native_first_20261105/grounding_profile.json"))
	if profile.is_empty(): return
	_set_physics(skin, false)
	var legs: Array = []
	for record in skin.mesh_records:
		if int(record.sprite_id) == 87557: legs.append(record)
	var samples: Array = profile.source_solid_sole_sample_hull
	_check(legs.size() == 2 and samples.size() > 0, "grounding uses actual two leg meshes and source-alpha sole sample hulls")
	var reports := {}
	for clip_name in profile.clips:
		var clip: Dictionary = skin.clips[clip_name]
		var most_down := -INF
		var most_up := INF
		var shift_min := INF
		var shift_max := -INF
		var contact_frames := 0
		var airborne_frames := 0
		for frame in range(144):
			var time: float = float(clip.duration) * float(frame) / 144.0
			skin.set_motion_context(1.0 / 60.0, Vector2.ZERO, time, 1.0)
			rig.set_character_pose(str(clip_name), time, 1.0)
			var lowest := -INF
			for record in legs:
				var mesh: Polygon2D = record.mesh
				for sample in samples:
					var ids: Array = sample.triangle_vertices
					var weights: Array = sample.barycentric_weights
					var point: Vector2 = mesh.polygon[int(ids[0])] * float(weights[0]) + mesh.polygon[int(ids[1])] * float(weights[1]) + mesh.polygon[int(ids[2])] * float(weights[2])
					# Include only the actual common display transform. Source-local
					# vertices are checked independently above and remain unchanged.
					point = rig.rig_root_node.transform * point
					lowest = maxf(lowest, point.y)
			most_down = maxf(most_down, lowest)
			most_up = minf(most_up, lowest)
			shift_min = minf(shift_min, rig.rig_root_node.position.y)
			shift_max = maxf(shift_max, rig.rig_root_node.position.y)
			if absf(lowest) <= 0.15: contact_frames += 1
			if lowest < -0.15: airborne_frames += 1
		_check(absf(most_down) <= 0.15, "actual visible solid sole reaches ground within0.15px: " + str(clip_name))
		_check(most_down <= 0.15, "actual visible solid sole does not penetrate ground: " + str(clip_name))
		_check(absf(shift_max-shift_min) < 0.0001 and absf(shift_min-float(profile.clips[clip_name].render_offset_y)) < 0.0001, "one fixed common translation per clip preserves original foot lift: " + str(clip_name))
		if clip_name in ["Idle", "AttackIdle"]:
			_check(maxf(absf(most_up),absf(most_down)) <= 0.15, "standing/standing attack visible soles stay grounded: " + str(clip_name))
		if clip_name == "Run_New1":
			_check(contact_frames > 0 and airborne_frames > 0, "actual original Run1 retains both visible contact and flight phases")
		reports[clip_name] = {"visible_sole_y_range_relative_to_actor_foot":[most_up,most_down], "fixed_render_offset_y":shift_min, "contact_frames_within_0_15px":contact_frames, "airborne_frames_over_0_15px":airborne_frames, "samples":144}
	details.visible_sole_grounding = {"alpha_threshold":128, "source_texture_subpixel_step":0.125, "source_alpha_sample_hull_count":samples.size(), "scope":"Actual rendered-mesh triangle positions plus common rig-root display transform. Source opaque-alpha foot samples are checked separately from transparent mesh bounding vertices.", "clips":reports}

func _check_drawing_contract(meshes: Dictionary, reference: Dictionary, audit: Dictionary) -> void:
	var by_sprite := {}
	for record in audit.sprites: by_sprite[int(record.sprite_id)] = record
	for source in reference.get("meshes", []):
		if not meshes.has(source.rendererPath): continue
		var mesh: Polygon2D = meshes[source.rendererPath]
		var contract: Dictionary = by_sprite[int(source.spriteId)]
		var image: Image = mesh.texture.get_image()
		_check(image.get_size() == Vector2i(contract.full_png_size[0], contract.full_png_size[1]), "actual full texture retains original canvas " + str(source.spriteId))
		if int(source.spriteId) == 87679:
			_check(mesh.polygon.size() == 4 and mesh.polygons.size() == 2, "own bundled hair uses the approved rigid full-canvas quad")
			var corners := [Vector2(0,0), Vector2(21,0), Vector2(21,56), Vector2(0,56)]
			var complete := mesh.uv.size() == 4
			for corner in corners:
				if not mesh.uv.has(corner): complete = false
			_check(complete, "own hair quad samples the same21x56 full canvas exactly once without crop or mirror")
			var original := Image.load_from_file(project_directory.path_join(contract.full_png))
			var alpha_differences := 0
			for y in range(image.get_height()):
				for x in range(image.get_width()):
					if absf(image.get_pixel(x,y).a - original.get_pixel(x,y).a) > 0.5 / 255.0: alpha_differences += 1
			_check(alpha_differences > 0, "own bundled hairstyle has its authored silhouette instead of original101 long-hair alpha")
			var art_directory := resource_dir.path_join("art/guigu_native_first_20261105")
			var registration := _read(art_directory.path_join("own_hair_registration.json"))
			_check(FileAccess.get_sha256(art_directory.path_join("sprite_87679.png")) == registration.png_sha256, "actual own-hairstyle PNG matches its measured registration SHA")
			_check(registration.native_canvas == [21.0,56.0] and registration.source_pivot == [0.5,0.5] and registration.pixels_per_unit == 100.0, "own hairstyle keeps original21x56 canvas, centered pivot and100PPU")
			var measured: Rect2i = image.get_used_rect()
			var box: Array = registration.native_alpha_bbox
			_check(measured == Rect2i(box[0],box[1],box[2],box[3]), "actual own-hair alpha bbox matches the separately measured shape registration")
			var target: Array = registration.native_visible_target
			var target_rect := Rect2i(target[0],target[1],target[2],target[3])
			var outside := 0
			var source_tail_pixels := 0
			var own_tail_pixels := 0
			for y in range(image.get_height()):
				for x in range(image.get_width()):
					if image.get_pixel(x,y).a > 0.0 and not target_rect.has_point(Vector2i(x,y)): outside += 1
					if y >= target_rect.end.y:
						if original.get_pixel(x,y).a > 0.0: source_tail_pixels += 1
						if image.get_pixel(x,y).a > 0.0: own_tail_pixels += 1
			_check(outside == 0, "authored bundled-hair pixels stay in the explicitly registered visible region")
			_check(target_rect.end.y < 56 and own_tail_pixels == 0 and source_tail_pixels > 0, "own hairstyle removes the source long lower tail beyond its registered bun region")
			var mount: Array = registration.source_neck_anchor_px
			_check(Vector2(mount[0],mount[1]).distance_to(Vector2(12.796864086,20.105417728)) < 0.0001, "own-hair registration preserves the original measured head-neck pixel anchor")
			_check(str(registration.style_reference) == "../yuxu_fullbody_v15/cutout.png" and FileAccess.file_exists(art_directory.path_join(str(registration.style_reference))), "own hairstyle registration points to the approved v15 plain-bun reference")
			_check(FileAccess.file_exists(art_directory.path_join(str(registration.master))), "authored own-hair master remains available for provenance review")
			details.own_hair_alpha_different_pixels = alpha_differences
			details.own_hair_registration = registration
			details.own_hair_removed_tail_source_pixels = source_tail_pixels
			details.alpha_scope = "Five non-hair source PNGs preserve every alpha byte; own hairstyle intentionally changes alpha and quad topology on the original21x56 canvas and head mount."
			continue
		_check(mesh.polygons.size() == source.triangles.size(), "original triangle topology retained " + str(source.rendererPath))
		var expected_uv: Array = contract.source_vertex_uv_full_png_pixels
		_check(mesh.uv.size() == expected_uv.size(), "actual vertex UV count retained " + str(source.rendererPath))
		if mesh.uv.size() == expected_uv.size():
			for index in range(expected_uv.size()):
				maximum_full_uv_error_pixels = maxf(maximum_full_uv_error_pixels, mesh.uv[index].distance_to(Vector2(expected_uv[index][0], expected_uv[index][1])))
		if not by_sprite[int(source.spriteId)].get("alpha_checked", false):
			var original := Image.load_from_file(project_directory.path_join(contract.full_png))
			var differences := 0
			if image.get_size() == original.get_size():
				for y in range(image.get_height()):
					for x in range(image.get_width()):
						if absf(image.get_pixel(x,y).a - original.get_pixel(x,y).a) > 0.5 / 255.0: differences += 1
			else: differences = -1
			_check(differences == 0, "every actual source alpha byte and transparent no-fill pixel is retained " + str(source.spriteId))
			by_sprite[int(source.spriteId)].alpha_checked = true
	_check(maximum_full_uv_error_pixels < 0.0001, "actual full-canvas UVs have exactly one vertical conversion and no crop-offset or mirror distortion")

func _measure_rigid_hair(skin: RefCounted) -> void:
	for record in skin.mesh_records:
		if int(record.sprite_id) != 87679: continue
		var mesh: Polygon2D = record.mesh
		if mesh.polygon.size() != 4 or mesh.uv.size() != 4:
			maximum_hair_rigid_vertex_error_pixels = INF
			return
		var uv: Array = record.source.uvConvention.sourceUvTransform
		var renderer: Transform3D = skin.world_transforms[record.source.rendererPath]
		for index in range(mesh.polygon.size()):
			var pixel: Vector2 = mesh.uv[index]
			var local := Vector3((pixel.x-float(uv[1]))/float(uv[0]), (56.0-pixel.y-float(uv[3]))/float(uv[2]), 0.0)
			var world: Vector3 = renderer * local
			var expected: Vector2 = Vector2(world.x,-world.y) * skin.PIXELS_PER_UNIT - skin.ground_offset
			maximum_hair_rigid_vertex_error_pixels = maxf(maximum_hair_rigid_vertex_error_pixels, mesh.polygon[index].distance_to(expected))
		# Exact external head-neck mount includes the original depth separation;
		# the hair plane itself usesZ0. This is an attachment, not a mesh pivot.
		var local_mount := Vector3((12.796864086-float(uv[1]))/float(uv[0]), (56.0-20.105417728-float(uv[3]))/float(uv[2]), 0.05000000074505806)
		var head: Transform3D = skin.world_transforms[skin.attachment_paths.head]
		var projected_mount: Vector3 = renderer * local_mount
		maximum_hair_head_mount_error_pixels = maxf(maximum_hair_head_mount_error_pixels, projected_mount.distance_to(head.origin) * skin.PIXELS_PER_UNIT)
		hair_samples += 1

func _finish() -> void:
	var directory := project_directory.path_join("output/guigu_native_first_20261105")
	DirAccess.make_dir_recursive_absolute(directory)
	var report := {"passed":failures.is_empty(), "checks":checks, "failures":failures, "sampled_source_poses":poses, "sampled_non_hair_source_vertices":vertices, "maximum_non_hair_source_vertex_error_pixels":maximum_source_vertex_error_pixels, "maximum_source_bone_projection_error_pixels":maximum_bone_projection_error_pixels, "maximum_non_hair_full_texture_uv_error_pixels":maximum_full_uv_error_pixels, "maximum_physics_segment_length_error_source_units":maximum_physics_segment_length_error_units, "own_hair_samples":hair_samples, "maximum_own_hair_rigid_vertex_error_pixels":maximum_hair_rigid_vertex_error_pixels, "maximum_own_hair_head_mount_error_pixels":maximum_hair_head_mount_error_pixels, "maximum_reference101_hair_source_vertex_error_pixels":maximum_reference_hair_source_error_pixels, "nonfinite_values":nonfinite_values, "details":details, "scope":"Five non-hair PNGs and their six active meshes retain original dimensions, exact alpha, full UVs and independently evaluated source3D geometry. Own bundled hairstyle is a declared21x56 rigid-quad shape/alpha exception on the original renderer/head mount. Source DynamicBone fidelity beyond the bounded supplied algorithm remains an original-runtime comparison boundary."}
	var file := FileAccess.open(directory.path_join("native_root_smoke.json" if ProjectSettings.globalize_path("res://").simplify_path().trim_suffix("/") == project_directory else "native_nested_smoke.json"), FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(report, "\t"))
	print("GUIGU NATIVE SOURCE ", "PASS" if failures.is_empty() else "FAIL", ": ", checks, " checks, ", poses, " original poses, ", vertices, " vertices; ", JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
