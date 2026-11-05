extends Node2D

## Whole-animal bitmap frames. The arena supplies its clock and combat state;
## this renderer owns no AI, hit timing, movement, skeleton or deformed mesh.
var sprite: AnimatedSprite2D
var frames: SpriteFrames
var clips: Dictionary = {}
var textures: Dictionary = {}
var validation_errors: Array[String] = []
var current_clip := ""
var current_state := "chase"
var current_facing := 1.0
var cycle_phase := 0.0
var idle_time := 0.0
var hurt_remaining := 0.0
var death_time := 0.0
var previous_alive := true
var debug_bones := false
var _ready_to_render := false
var _canvas_world := Vector2(158.08, 158.08)
var _has_context := false

func build(art_dir: String) -> bool:
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(art_dir.path_join("sequence.json")))
	if not data is Dictionary or not data.get("clips") is Dictionary:
		validation_errors.append("Invalid whole-wolf sequence manifest")
		return false
	clips = data.clips
	var size: Array = data.get("canvas_world_size", [158.08,158.08])
	_canvas_world = Vector2(float(size[0]), float(size[1]))
	frames = SpriteFrames.new()
	frames.remove_animation("default")
	for name in clips:
		var clip: Dictionary = clips[name]
		var source := art_dir.path_join(str(clip.sheet))
		if not textures.has(source):
			var bitmap := Image.new()
			if bitmap.load(ProjectSettings.globalize_path(source)) != OK:
				validation_errors.append("Cannot load wolf sequence: " + source)
				continue
			bitmap.generate_mipmaps()
			textures[source] = ImageTexture.create_from_image(bitmap)
		var texture: Texture2D = textures[source]
		var entries: Array = clip.get("frames", [])
		if entries.size() != int(clip.get("frame_count", entries.size())) or entries.is_empty():
			validation_errors.append("Wrong frame count for " + str(name))
			continue
		frames.add_animation(name)
		frames.set_animation_speed(name, float(clip.get("fps", 12)))
		frames.set_animation_loop(name, bool(clip.get("loop", false)))
		var expected_size := Vector2.ZERO
		for entry in entries:
			var rect_data: Array = entry.rect
			var rect := Rect2(float(rect_data[0]),float(rect_data[1]),float(rect_data[2]),float(rect_data[3]))
			if not Rect2(Vector2.ZERO, texture.get_size()).encloses(rect):
				validation_errors.append("Frame outside source sheet: " + str(name))
			if expected_size == Vector2.ZERO:
				expected_size = rect.size
			if rect.size != expected_size:
				validation_errors.append("Per-frame scaling is forbidden: " + str(name))
			var atlas := AtlasTexture.new()
			atlas.atlas = texture
			atlas.region = rect
			atlas.filter_clip = true
			frames.add_frame(name, atlas)
	for required in ["idle","walk","trot","windup","lunge","recover","hurt","death"]:
		if not frames.has_animation(required):
			validation_errors.append("Missing animation: " + required)
	if not validation_errors.is_empty():
		return false
	sprite = AnimatedSprite2D.new()
	sprite.name = "WholeWolfFrames"
	sprite.sprite_frames = frames
	sprite.centered = false
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	add_child(sprite)
	_ready_to_render = true
	reset_pose()
	return true

func reset_pose() -> void:
	cycle_phase = 0.0
	idle_time = 0.0
	hurt_remaining = 0.0
	death_time = 0.0
	previous_alive = true
	_has_context = false
	current_state = "chase"
	if _ready_to_render:
		_show_frame("idle", 0)

func trigger_hurt() -> void:
	if previous_alive:
		hurt_remaining = 0.28

func _show_frame(name: String, index: int) -> void:
	var clip: Dictionary = clips[name]
	var entries: Array = clip.frames
	index = clampi(index, 0, entries.size() - 1)
	var rect: Array = entries[index].rect
	var anchor: Array = clip.anchor
	var world_size: Array = clip.get("canvas_world_size", [_canvas_world.x,_canvas_world.y])
	sprite.animation = name
	sprite.stop()
	sprite.frame = index
	sprite.scale = Vector2(float(world_size[0]),float(world_size[1])) / Vector2(float(rect[2]), float(rect[3]))
	sprite.offset = -Vector2(float(anchor[0]),float(anchor[1]))
	if entries[index].has("offset"):
		var offset: Array = entries[index].offset
		sprite.offset += Vector2(float(offset[0]),float(offset[1]))
	sprite.flip_h = current_facing < 0.0
	current_clip = name

func _at_progress(name: String, progress: float) -> void:
	var count: int = frames.get_frame_count(name)
	_show_frame(name, mini(floori(clampf(progress, 0, 1) * count), count - 1))

func set_motion_context(delta: float, velocity: Vector2, state: String, clock: float, facing: float, alive: bool) -> void:
	if not _ready_to_render:
		return
	var step := maxf(delta, 0)
	if step == 0.0 and _has_context and state == current_state and (-1.0 if facing < 0 else 1.0) == current_facing and alive == previous_alive:
		return
	current_facing = -1.0 if facing < 0 else 1.0
	current_state = state
	if not alive:
		if previous_alive: death_time = 0.0
		death_time += step
		_at_progress("death", death_time / float(clips.death.duration))
	elif hurt_remaining > 0:
		hurt_remaining = maxf(0, hurt_remaining - step)
		_at_progress("hurt", (0.28-hurt_remaining)/0.28)
	elif state == "windup":
		_at_progress("windup", 1.0-clock/0.8)
	elif state == "lunge":
		_at_progress("lunge", 1.0-clock/0.2)
	elif state in ["recover","recovery"]:
		_at_progress("recover", 1.0-clock/1.5)
	elif velocity.length() > 0.6:
		var name := "trot" if velocity.length() > 55 else "walk"
		var distance := float(clips[name].get("cycle_distance_local", 50.0)) * maxf(absf(global_scale.x),0.001)
		cycle_phase = fposmod(cycle_phase + velocity.length()*step/distance, 1.0)
		_at_progress(name, cycle_phase)
	else:
		idle_time += step
		_at_progress("idle", fposmod(idle_time/float(clips.idle.duration),1.0))
	previous_alive = alive
	_has_context = true

func get_runtime_summary() -> Dictionary:
	var counts := {}
	if frames != null:
		for name in frames.get_animation_names(): counts[name] = frames.get_frame_count(name)
	return {"backend":"whole_animal_sequence","bone_count":0,"mesh_count":0,
		"frame_counts":counts,"clip":current_clip,"frame":sprite.frame if sprite != null else -1,
		"state":current_state,"validation_errors":validation_errors.duplicate()}
