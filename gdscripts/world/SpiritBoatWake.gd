extends Node2D
## Visual-only airborne wake. Add beside the boat's altitude-projected visual
## root, not inside it: existing wisps must not turn around when the boat does.
## Call update_flight after updating the boat's position, height and zoom.

const WISP_PATH := "res://textures/world/bai_chuan/vehicles/spirit_boat_wake_20261106/ink_cloud_wisp.png"
const VisualScale = preload("res://gdscripts/world/WorldMapVisualScale.gd")
const BOAT_LAYOUT_SCALE := VisualScale.BOAT_WORLD_WIDTH / 150.0
const EMIT_INTERVAL := 0.13
const MAX_WISPS := 18

@export var wisp_texture: Texture2D

var _wisps: Array[Dictionary] = []
var _rng := RandomNumberGenerator.new()
var _last_ground := Vector2.ZERO
var _has_last_ground := false
var _emit_clock := 0.0


func configure() -> bool:
	# The scene supplies an imported texture for exported builds. This fallback
	# also lets an isolated project preview run before the editor has imported it.
	if wisp_texture == null and ResourceLoader.exists(WISP_PATH):
		wisp_texture = ResourceLoader.load(WISP_PATH) as Texture2D
	if wisp_texture == null:
		var image := Image.load_from_file(WISP_PATH)
		if image != null and not image.is_empty():
			wisp_texture = ImageTexture.create_from_image(image)
	if wisp_texture == null:
		push_warning("Spirit boat wake: cloud texture is unavailable.")
		return false
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	z_index = -1
	_rng.seed = 11162026
	return true


## ground_position: boat's unprojected map XY, in its parent's coordinates.
## visual_offset: projected position of the boat within this node's parent.
## zoom: current world-map scale, retained for callers; all offsets, sizes and
## drift speeds stay in world units under the boat's unscaled parent.
## facing: -1 left, +1 right. Each wisp retains the facing it was emitted with.
## emitting: true only while airborne and travelling. When false, old wisps
## fade out. Pass delta=0 to freeze animation while the map itself is paused.
func update_flight(delta: float, ground_position: Vector2, visual_offset: Vector2, zoom: float, facing: float, emitting: bool) -> void:
	if not ground_position.is_finite() or not visual_offset.is_finite() or not is_finite(zoom) or zoom <= 0.0:
		return
	var valid_delta := maxf(delta, 0.0) if is_finite(delta) else 0.0
	var frame_time := minf(valid_delta, 0.1)
	var change := Vector2.ZERO
	if _has_last_ground:
		change = ground_position - _last_ground
		# A wisp is anchored to the landscape, not to the moving hull.
		for puff in _wisps:
			puff["point"] = (puff["point"] as Vector2) - change
	_last_ground = ground_position
	_has_last_ground = true

	if frame_time > 0.0:
		for index in range(_wisps.size() - 1, -1, -1):
			var puff := _wisps[index]
			puff["age"] = float(puff["age"]) + frame_time
			if float(puff["age"]) >= float(puff["lifetime"]):
				_wisps.remove_at(index)
				continue
			puff["point"] = (puff["point"] as Vector2) + (puff["drift"] as Vector2) * frame_time

		# A held position or a stationary boat must not create an endless cloud.
		if emitting and change.length_squared() > 0.00000001:
			_emit_clock += frame_time
			if _emit_clock >= EMIT_INTERVAL:
				_emit_clock = fmod(_emit_clock, EMIT_INTERVAL)
				_emit_wisp(visual_offset, facing, change)
		else:
			_emit_clock = 0.0
	queue_redraw()


func _emit_wisp(visual_offset: Vector2, facing: float, change: Vector2) -> void:
	if _wisps.size() >= MAX_WISPS:
		_wisps.pop_front()
	var side := -1.0 if facing < 0.0 else 1.0
	# The boat has two painted horizontal facings, while world travel may also
	# move north or south. Rotate each new wisp along that 2D direction and keep
	# the cloud attached to the visible stern; already emitted wisps do not turn.
	var direction := Vector2(side * absf(change.x), change.y).normalized()
	var rotation := atan2(direction.y * side, absf(direction.x))
	_wisps.push_back({
		"point": visual_offset + Vector2(-side * 62.0, _rng.randf_range(0.0, 4.0)) * BOAT_LAYOUT_SCALE,
		"drift": (-direction * _rng.randf_range(7.0, 15.0) + Vector2(0, _rng.randf_range(-2.0, 1.0))) * BOAT_LAYOUT_SCALE,
		"age": 0.0,
		"lifetime": _rng.randf_range(1.25, 1.55),
		"size": _rng.randf_range(88.0, 108.0) * BOAT_LAYOUT_SCALE,
		"side": side,
		"tilt": rotation + _rng.randf_range(-0.035, 0.035),
	})


## Use after teleporting or moving this node under a different map parent.
func clear_wake() -> void:
	_wisps.clear()
	_has_last_ground = false
	_emit_clock = 0.0
	queue_redraw()


func _draw() -> void:
	if wisp_texture == null:
		return
	for puff in _wisps:
		var progress := clampf(float(puff["age"]) / float(puff["lifetime"]), 0.0, 1.0)
		var opacity := minf(progress / 0.13, 1.0) * pow(1.0 - progress, 1.25) * 0.58
		var width := float(puff["size"]) * lerpf(0.80, 1.28, progress)
		var height := width / 3.0
		var side := float(puff["side"])
		draw_set_transform(puff["point"] as Vector2, float(puff["tilt"]), Vector2(side, 1.0))
		draw_texture_rect(wisp_texture, Rect2(-width, -height * 0.5, width, height), false, Color(1.0, 1.0, 1.0, opacity))
	draw_set_transform(Vector2.ZERO)
