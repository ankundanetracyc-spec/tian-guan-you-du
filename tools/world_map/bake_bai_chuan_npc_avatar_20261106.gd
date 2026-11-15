extends SceneTree

const Traveler = preload("res://gdscripts/world/WorldActionTraveler.gd")
const TravelerSkin = preload("res://gdscripts/world/WorldTravelerSkin.gd")
const OUTPUT := "res://textures/world/bai_chuan/traveler_rig_20261115/"
const CONFIG := preload("res://gdscripts/world/WorldNpcTraveler.gd").CONFIG_PATH
const FRAME_SIZE := Vector2i(192, 160)
const PIVOT := Vector2(96, 144)
const HEIGHT := TravelerSkin.HEIGHT
const COLUMNS := 8
const DT := 1.0 / 120.0
const CLIPS := {
	"idle": {"duration": TravelerSkin.IDLE_PERIOD, "frames": 24},
	"walk": {"duration": TravelerSkin.RUN_PERIOD, "frames": 16},
	"flight": {"duration": TravelerSkin.IDLE_PERIOD, "frames": 24},
}
const JOINTS := ["Head", "RobeWaist", "Nape", "NearShoulder", "NearElbow", "NearWrist",
	"FarShoulder", "FarElbow", "FarWrist", "NearThigh", "NearShin", "NearBoot",
	"FarThigh", "FarShin", "FarBoot"]

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
	var frame_count := 0
	for clip in CLIPS.values(): frame_count += 2 * int(clip.frames)
	var atlas := Image.create(FRAME_SIZE.x * COLUMNS, FRAME_SIZE.y * ceili(float(frame_count) / COLUMNS), false, Image.FORMAT_RGBA8)
	atlas.fill(Color.TRANSPARENT)
	var animations := {}
	var audit: Array[Dictionary] = []
	var slot := 0
	for direction: String in ["right", "left"]:
		for clip: String in CLIPS:
			var avatar := Traveler.new()
			viewport.add_child(avatar)
			avatar.position = PIVOT
			if not avatar.build(HEIGHT):
				push_error("NPC avatar source failed")
				quit(1)
				return
			var duration: float = CLIPS[clip].duration
			var frames: int = CLIPS[clip].frames
			avatar.facing = -1.0 if direction == "left" else 1.0
			var velocity := Vector2(100.0 * avatar.facing, 0.0) if clip == "walk" else Vector2.ZERO
			var elapsed := 0.0
			var warmup := duration * 4.0
			while elapsed < warmup - 0.0000001:
				var step := minf(DT, warmup - elapsed)
				_advance(avatar, clip, step, velocity)
				elapsed += step
			var indices: Array[int] = []
			for index: int in frames:
				var target := warmup + duration * float(index) / float(frames)
				while elapsed < target - 0.0000001:
					var step := minf(DT, target - elapsed)
					_advance(avatar, clip, step, velocity)
					elapsed += step
				await process_frame
				await RenderingServer.frame_post_draw
				var source := viewport.get_texture().get_image()
				var used := source.get_used_rect()
				if used.size.x == 0 or used.position.x <= 0 or used.position.y <= 0 or used.end.x >= FRAME_SIZE.x or used.end.y >= FRAME_SIZE.y:
					push_error("NPC avatar clipped/empty frame: " + str([direction, clip, index, used]))
					quit(1)
					return
				var opacity := _joint_opacity(source, avatar.rig.skeleton)
				if opacity < 0.9:
					push_error("NPC avatar exposed a joint: " + str([direction, clip, index, opacity]))
					quit(1)
					return
				var destination := Vector2i((slot % COLUMNS) * FRAME_SIZE.x, floori(float(slot) / COLUMNS) * FRAME_SIZE.y)
				atlas.blit_rect(source, Rect2i(Vector2i.ZERO, FRAME_SIZE), destination)
				indices.append(slot)
				audit.append({"slot": slot, "clip": clip, "direction": direction, "phase": float(index) / frames,
					"used_rect": [used.position.x, used.position.y, used.size.x, used.size.y], "joint_opacity": opacity})
				slot += 1
			animations[clip + "_" + direction] = {"frames": indices, "duration": duration, "fps": frames / duration}
			avatar.queue_free()
			await process_frame
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT))
	if atlas.save_png(OUTPUT + "avatar_atlas.png") != OK:
		push_error("Cannot write NPC atlas")
		quit(1)
		return
	var sources := {}
	for path: String in ["res://gdscripts/world/WorldActionTraveler.gd", "res://gdscripts/world/WorldTravelerSkin.gd", TravelerSkin.PARTS_TEXTURE]:
		sources[path] = FileAccess.get_sha256(path)
	var metadata := {"version": 1, "appearance": TravelerSkin.APPEARANCE, "atlas": OUTPUT + "avatar_atlas.png", "atlas_sha256": FileAccess.get_sha256(OUTPUT + "avatar_atlas.png"), "frame_size": [FRAME_SIZE.x, FRAME_SIZE.y], "pivot": [PIVOT.x, PIVOT.y], "visual_height": HEIGHT, "columns": COLUMNS, "animations": animations, "source_files": sources, "frame_audit": audit}
	var file := FileAccess.open(CONFIG, FileAccess.WRITE)
	file.store_string(JSON.stringify(metadata, "\t") + "\n")
	file.close()
	print("NPC_AVATAR_BAKED frames=", slot, " atlas=", atlas.get_size(), " config=", CONFIG)
	viewport.queue_free()
	await process_frame
	quit()

func _joint_opacity(frame: Image, skeleton: Skeleton2D) -> float:
	var minimum := 1.0
	var found := 0
	for i in skeleton.get_bone_count():
		var bone := skeleton.get_bone(i)
		if String(bone.name) not in JOINTS:
			continue
		var point := Vector2i(bone.get_global_transform_with_canvas().origin.floor())
		if not Rect2i(Vector2i.ZERO, frame.get_size()).has_point(point):
			return 0.0
		minimum = minf(minimum, frame.get_pixelv(point).a)
		found += 1
	return minimum if found == JOINTS.size() else 0.0

func _advance(avatar: Node2D, clip: String, step: float, velocity: Vector2) -> void:
	if clip == "flight":
		avatar.update_flight(step, 0.74)
	else:
		avatar.update_motion(step, velocity)
