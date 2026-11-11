extends AnimatedSprite2D
## Shared frames rendered from the current player rig; no per-NPC skeleton work.

const CONFIG_PATH := "res://resources/world/bai_chuan/npc_avatar_20261106.json"
static var _shared_frames: SpriteFrames
static var _shared_data: Dictionary = {}
static var _load_attempted := false
var native_height := 128.0
var npc_traveling := false
var facing_left := false
var _sample_ready := false
var _phase := 0.0
var _on_screen := false
var _world_advancing := false
var _world_paused := false
var _map_visible := true

static func _prepare_frames() -> bool:
	if _load_attempted: return _shared_frames != null
	_load_attempted = true
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(CONFIG_PATH))
	if not parsed is Dictionary or int(parsed.get("version", 0)) != 1: return false
	var source := Image.load_from_file(str(parsed.atlas))
	if source == null or source.is_empty(): return false
	var atlas := ImageTexture.create_from_image(source)
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	var size := Vector2(float(parsed.frame_size[0]), float(parsed.frame_size[1]))
	for key: String in parsed.animations:
		var entry: Dictionary = parsed.animations[key]
		frames.add_animation(key)
		frames.set_animation_speed(key, float(entry.fps))
		frames.set_animation_loop(key, true)
		for slot: int in entry.frames:
			var frame_texture := AtlasTexture.new()
			frame_texture.atlas = atlas
			frame_texture.region = Rect2(Vector2(slot % int(parsed.columns), slot / int(parsed.columns)) * size, size)
			frame_texture.filter_clip = true
			frames.add_frame(key, frame_texture)
	_shared_data = parsed
	_shared_frames = frames
	return true

func configure(npc_id: String) -> bool:
	if not _prepare_frames():
		push_error("百川NPC人物图集加载失败")
		return false
	sprite_frames = _shared_frames
	native_height = float(_shared_data.visual_height)
	centered = false
	offset = -Vector2(float(_shared_data.pivot[0]), float(_shared_data.pivot[1]))
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	_phase = float(posmod(npc_id.hash(), 10000)) / 10000.0
	animation = "idle_right"
	set_frame_and_progress(int(_phase * sprite_frames.get_frame_count(animation)), 0.0)
	var notifier := VisibleOnScreenNotifier2D.new()
	notifier.name = "AvatarVisibility"
	notifier.rect = Rect2(offset, Vector2(float(_shared_data.frame_size[0]), float(_shared_data.frame_size[1])))
	notifier.screen_entered.connect(_screen_entered)
	notifier.screen_exited.connect(_screen_exited)
	add_child(notifier)
	return true

func sync_sample(point: Vector2, traveling: bool) -> void:
	if _sample_ready and absf(point.x - position.x) > 0.001: facing_left = point.x < position.x
	position = point
	_sample_ready = true
	npc_traveling = traveling

func update_activity(advancing: bool, world_paused: bool, map_visible: bool) -> void:
	_world_advancing = advancing
	_world_paused = world_paused
	_map_visible = map_visible
	_apply_activity()

func _apply_activity() -> void:
	if not _map_visible or not visible or _world_paused or not _on_screen:
		pause()
		return
	var prefix := "walk_" if npc_traveling and _world_advancing else "idle_"
	var next := prefix + ("left" if facing_left else "right")
	if animation != next:
		var progress := _phase
		if String(animation).begins_with(prefix):
			progress = (float(frame) + frame_progress) / sprite_frames.get_frame_count(animation)
		play(next)
		var pose := progress * sprite_frames.get_frame_count(next)
		set_frame_and_progress(int(pose), fposmod(pose, 1.0))
	elif not is_playing(): play(next)

func _screen_entered() -> void:
	_on_screen = true
	_apply_activity()

func _screen_exited() -> void:
	_on_screen = false
	pause()

func get_visual_summary() -> Dictionary:
	return {"appearance": str(_shared_data.get("appearance", "")), "shared_frames": sprite_frames.get_instance_id() if sprite_frames != null else 0, "animation": String(animation), "frame": frame, "playing": is_playing(), "on_screen": _on_screen, "facing_left": facing_left, "native_height": native_height, "has_live_skeleton": false}
