extends SceneTree

const Traveler = preload("res://gdscripts/world/WorldActionTraveler.gd")
const OUTPUT := "res://textures/world/bai_chuan/npc_native_20261106/"
const CONFIG := "res://resources/world/bai_chuan/npc_avatar_20261106.json"
const FRAME_SIZE := Vector2i(192, 160)
const PIVOT := Vector2(96, 144)
const HEIGHT := 128.0
const FRAMES := 16
const COLUMNS := 8
const DT := 1.0 / 120.0

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var expected := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--expected-appdata="): expected = arg.trim_prefix("--expected-appdata=")
	if expected.is_empty() or OS.get_environment("APPDATA") != expected:
		push_error("NPC avatar bake requires isolated APPDATA")
		quit(1)
		return
	if DisplayServer.get_name() == "headless":
		push_error("NPC avatar bake requires a rendering device")
		quit(1)
		return
	var viewport := SubViewport.new()
	viewport.size = FRAME_SIZE
	viewport.transparent_bg = true
	viewport.disable_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var atlas := Image.create(FRAME_SIZE.x * COLUMNS, FRAME_SIZE.y * 8, false, Image.FORMAT_RGBA8)
	atlas.fill(Color.TRANSPARENT)
	var animations := {}
	var audit: Array[Dictionary] = []
	var slot := 0
	for direction: String in ["right", "left"]:
		for clip: String in ["Idle", "Run_New1"]:
			var avatar := Traveler.new()
			viewport.add_child(avatar)
			avatar.position = PIVOT
			if not avatar.build(HEIGHT):
				push_error("NPC avatar source failed")
				quit(1)
				return
			var duration: float = avatar.rig.wanderer_skin.clips[clip].duration
			var velocity := Vector2(-100, 0) if direction == "left" else Vector2(100, 0)
			if clip == "Idle":
				velocity = Vector2.ZERO
				avatar.facing = -1.0 if direction == "left" else 1.0
			var elapsed := 0.0
			# Warm the original cloth solver before recording a repeatable loop.
			var warmup := duration * 2.0
			while elapsed < warmup - 0.0000001:
				var step := minf(DT, warmup - elapsed)
				avatar.update_motion(step, velocity)
				elapsed += step
			var indices: Array[int] = []
			for index: int in FRAMES:
				var target := warmup + duration * float(index) / float(FRAMES)
				while elapsed < target - 0.0000001:
					var step := minf(DT, target - elapsed)
					avatar.update_motion(step, velocity)
					elapsed += step
				await process_frame
				await RenderingServer.frame_post_draw
				var source := viewport.get_texture().get_image()
				var used := source.get_used_rect()
				if used.size.x == 0 or used.position.x <= 0 or used.position.y <= 0 or used.end.x >= FRAME_SIZE.x or used.end.y >= FRAME_SIZE.y:
					push_error("NPC avatar clipped/empty frame: " + str([direction, clip, index, used]))
					quit(1)
					return
				var destination := Vector2i((slot % COLUMNS) * FRAME_SIZE.x, (slot / COLUMNS) * FRAME_SIZE.y)
				atlas.blit_rect(source, Rect2i(Vector2i.ZERO, FRAME_SIZE), destination)
				indices.append(slot)
				audit.append({"slot": slot, "clip": clip, "direction": direction, "phase": float(index) / FRAMES, "used_rect": [used.position.x, used.position.y, used.size.x, used.size.y]})
				slot += 1
			animations[("walk_" if clip == "Run_New1" else "idle_") + direction] = {"frames": indices, "duration": duration, "fps": FRAMES / duration, "source_clip": clip}
			avatar.queue_free()
			await process_frame
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	if atlas.save_png(OUTPUT + "avatar_atlas.png") != OK:
		push_error("Cannot write NPC atlas")
		quit(1)
		return
	var sources := {}
	for path: String in ["res://gdscripts/world/WorldActionTraveler.gd", "res://tools/action_battle_test/guigu_rig_runtime.gd", "res://tools/action_battle_test/guigu_native_first_skin.gd", "res://resources/appearance/guigu_native_first_20261105/rig.json", "res://resources/appearance/guigu_native_first_20261105/skins.json", "res://resources/appearance/guigu_native_first_20261105/animations.json", "res://resources/appearance/guigu_native_first_20261105/grounding_profile.json"]:
		sources[path] = FileAccess.get_sha256(path)
	var art := "res://tools/action_battle_test/art/guigu_native_first_20261105/"
	for name: String in DirAccess.get_files_at(art):
		if name.ends_with(".png"): sources[art + name] = FileAccess.get_sha256(art + name)
	var metadata := {"version": 1, "appearance": "current_world_player_native_101", "atlas": OUTPUT + "avatar_atlas.png", "atlas_sha256": FileAccess.get_sha256(OUTPUT + "avatar_atlas.png"), "frame_size": [FRAME_SIZE.x, FRAME_SIZE.y], "pivot": [PIVOT.x, PIVOT.y], "visual_height": HEIGHT, "columns": COLUMNS, "animations": animations, "source_files": sources, "frame_audit": audit}
	var file := FileAccess.open(CONFIG, FileAccess.WRITE)
	file.store_string(JSON.stringify(metadata, "\t") + "\n")
	file.close()
	print("NPC_AVATAR_BAKED frames=", slot, " atlas=", atlas.get_size(), " config=", CONFIG)
	viewport.queue_free()
	await process_frame
	quit()
