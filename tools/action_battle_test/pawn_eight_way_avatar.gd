extends Node2D

## Fixed-hairstyle eight-view art prototype. Ground motion is shared with the
## accepted two-view prototype; the eight views are independent atlas cells.
const DIRECTIONS := ["front", "front_right", "right", "back_right", "back", "back_left", "left", "front_left"]
const BODY_HEIGHT := 55.2
const HEAD_HAIR_HEIGHT := 82.0
const HAND_DIAMETER := 14.0
const STEP_CYCLE_DISTANCE := 150.0
const FOOT_TRAVEL := 4.0
const FOOT_LIFT := 2.0
const TwoView = preload("pawn_two_view_avatar.gd")
const CLEAN_ALPHA := """shader_type canvas_item;
render_mode unshaded;
varying vec4 pawn_tint;
void vertex() { pawn_tint = COLOR; }
void fragment() {
 vec4 pixel = texture(TEXTURE, UV);
 pixel.a = smoothstep(0.78, 0.94, pixel.a);
 COLOR = pixel * pawn_tint;
}
"""
static var _shared_assets: Dictionary = {}
var visual: Node2D
var layers: Dictionary = {}
var assets: Dictionary = {}
var render_registration: Dictionary = {}
var errors: Array[String] = []
var profile: Dictionary = {}
var loaded := false
var supported := true
var direction := "front"
var direction_index := 0
var desired_look := Vector2.DOWN
var front_style := 0
var back_style := 0
var phase := 0.0
var clock := 0.0
var moving := false
var body_width := 42.0
var feet: Node2D
var last_velocity := Vector2.ZERO
var last_acceleration := Vector2.ZERO
var gait_weight := 0.0
var gait_direction := Vector2.ZERO
var motion_lag := Vector2.ZERO
var travelled_distance := 0.0
var sword_pose: Dictionary = {}
var swing_left := 0.0

func _folder() -> String:
	return get_script().resource_path.get_base_dir()

func _scan_bounds(source: Image, region: Rect2i) -> Rect2:
	var low := region.end
	var high := region.position - Vector2i.ONE
	for y in range(region.position.y, region.end.y):
		for x in range(region.position.x, region.end.x):
			if source.get_pixel(x, y).a < 0.88: continue
			low.x = mini(low.x, x)
			low.y = mini(low.y, y)
			high.x = maxi(high.x, x)
			high.y = maxi(high.y, y)
	if high.x < low.x: return Rect2()
	return Rect2(Vector2(low - region.position), Vector2(high - low + Vector2i.ONE))

func _load_views(key: String, path: String) -> bool:
	var digest := FileAccess.get_sha256(path)
	if _shared_assets.has(path) and _shared_assets[path].sha256 == digest:
		assets[key] = _shared_assets[path]
		return true
	var source := Image.load_from_file(path)
	if source == null or source.is_empty():
		errors.append("Missing eight-view art: " + path)
		return false
	var cell := Vector2i(source.get_width() / 4, source.get_height() / 2)
	var views: Array[Dictionary] = []
	for index in range(8):
		var region := Rect2i(Vector2i(index % 4, index / 4) * cell, cell)
		var bounds := _scan_bounds(source, region)
		if bounds.size.x <= 0.0 or bounds.size.y <= 0.0:
			errors.append("Empty eight-view cell: %s/%s" % [key, DIRECTIONS[index]])
			return false
		var item := {"region": region, "bounds": bounds}
		if key == "head": item["chin_anchor"] = _head_anchor(source, region, bounds, index)
		views.append(item)
	source.generate_mipmaps()
	assets[key] = {"texture": ImageTexture.create_from_image(source), "size": source.get_size(), "cell": cell, "views": views, "path": path, "sha256": digest}
	_shared_assets[path] = assets[key]
	return true

func _head_anchor(source: Image, region: Rect2i, bounds: Rect2, index: int) -> Vector2:
	var fallback := Vector2(bounds.get_center().x, bounds.position.y + bounds.size.y * 0.69)
	if index in [3, 4, 5]: return fallback
	# Complete-head atlas cells include asymmetric rear locks. Register the skin
	# silhouette rather than the hair bbox so the profile neck sits on the collar.
	var low_x := region.size.x
	var high_x := -1
	var low_y := region.size.y
	var high_y := -1
	for y in range(int(bounds.position.y + bounds.size.y * 0.30), int(bounds.end.y)):
		for x in range(int(bounds.position.x), int(bounds.end.x)):
			var colour := source.get_pixel(region.position.x + x, region.position.y + y)
			if colour.a < 0.88 or colour.r < 0.58 or colour.g < 0.40: continue
			if colour.r <= colour.g * 1.035 or colour.g <= colour.b * 1.02: continue
			low_x = mini(low_x, x)
			high_x = maxi(high_x, x)
			low_y = mini(low_y, y)
			high_y = maxi(high_y, y)
	if high_x < low_x or high_y - low_y < bounds.size.y * 0.12: return fallback
	return Vector2((low_x + high_x) * 0.5, high_y + 1.0)

func _load_round_hand() -> bool:
	var path := _folder().path_join("art/pawn_four_way_20261105/base_parts.png")
	var digest := FileAccess.get_sha256(path)
	if _shared_assets.has(path) and _shared_assets[path].sha256 == digest:
		assets["hand"] = _shared_assets[path]
		return true
	var source := Image.load_from_file(path)
	if source == null:
		errors.append("Missing approved round hand")
		return false
	var region := Rect2i(0, 660, source.get_width() / 4, 300)
	var bounds := _scan_bounds(source, region)
	if bounds.size.x <= 0.0:
		errors.append("Empty approved round hand")
		return false
	source.generate_mipmaps()
	assets["hand"] = {"texture": ImageTexture.create_from_image(source), "path": path, "sha256": digest, "size": source.get_size(), "region": region, "bounds": bounds}
	_shared_assets[path] = assets["hand"]
	return true

func build() -> bool:
	if loaded: return true
	var good := true
	for item in [["body", "body.png"], ["head", "head_hair.png"]]:
		if not _load_views(item[0], _folder().path_join("art/pawn_eight_way_20261105").path_join(item[1])): good = false
	if not _load_round_hand(): good = false
	if not good: return false
	visual = Node2D.new()
	visual.name = "PawnLayers"
	add_child(visual)
	var shader := Shader.new()
	shader.code = CLEAN_ALPHA
	for part in ["Body", "HeadHair", "HandLeft", "HandRight"]:
		var sprite := Sprite2D.new()
		sprite.name = part
		sprite.centered = false
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		var material := ShaderMaterial.new()
		material.shader = shader
		sprite.material = material
		visual.add_child(sprite)
		layers[part] = sprite
	feet = TwoView.RoundFeet.new()
	feet.name = "RoundFeet"
	feet.z_index = 15
	add_child(feet)
	loaded = true
	_render()
	return true

func _put(part: String, key: String, view: int, at: Vector2, anchor: Vector2, scale_value: float, z: int) -> void:
	var record: Dictionary = assets[key]
	var region: Rect2i = record.region if key == "hand" else record.views[view].region
	var atlas := AtlasTexture.new()
	atlas.atlas = record.texture
	atlas.region = region
	atlas.filter_clip = true
	var sprite: Sprite2D = layers[part]
	sprite.texture = atlas
	sprite.offset = -anchor
	sprite.position = at
	sprite.scale = Vector2.ONE * scale_value
	sprite.z_index = z
	render_registration[part] = {"sheet": key, "index": view, "direction": DIRECTIONS[view] if key != "hand" else "shared_round_hand", "region": region, "anchor": anchor, "at": at, "scale": scale_value, "z": z}

func _render() -> void:
	if not loaded: return
	var body_bounds: Rect2 = assets.body.views[direction_index].bounds
	var body_scale := BODY_HEIGHT / body_bounds.size.y
	# Preserve the accepted torso volume while registering the slightly longer
	# source garment. This is a fixed atlas placement, never an attack stretch.
	body_width = 42.0 - 1.5 * absf(_facing_vector().x)
	_put("Body", "body", direction_index, Vector2.ZERO, Vector2(body_bounds.get_center().x, body_bounds.end.y), body_scale, 20)
	layers.Body.scale = Vector2(body_width / body_bounds.size.x, body_scale)
	render_registration.Body["scale_xy"] = layers.Body.scale
	var head_bounds: Rect2 = assets.head.views[direction_index].bounds
	var head_scale := HEAD_HAIR_HEIGHT / head_bounds.size.y
	# Long locks extend below the chin; the chin registers to the robe neckline.
	var head_at := Vector2(_facing_vector().x * 2.6, -BODY_HEIGHT + 3.0)
	var head_anchor: Vector2 = assets.head.views[direction_index].chin_anchor
	_put("HeadHair", "head", direction_index, head_at, head_anchor, head_scale, 40)
	var hand_bounds: Rect2 = assets.hand.bounds
	var hand_scale := HAND_DIAMETER / maxf(hand_bounds.size.x, hand_bounds.size.y)
	var default_hands := get_default_hand_pose()
	_put("HandLeft", "hand", 0, default_hands.left_hand, hand_bounds.get_center(), hand_scale, default_hands.left_hand_z)
	_put("HandRight", "hand", 0, default_hands.right_hand, hand_bounds.get_center(), hand_scale, default_hands.right_hand_z)
	_update_feet()
	_apply_motion()
	queue_redraw()

func _facing_vector() -> Vector2:
	var angle := float(direction_index) * PI / 4.0
	return Vector2(sin(angle), cos(angle))

func set_appearance(value: Dictionary) -> void:
	profile = value.duplicate(true)
	# This review is deliberately one fixed hairstyle and one fixed outfit.
	front_style = 0
	back_style = 0
	supported = true
	if loaded: _render()

func set_direction(value: Vector2) -> void:
	if value.is_zero_approx(): return
	desired_look = value.normalized()
	var next := posmod(int(round(atan2(desired_look.x, desired_look.y) / (PI / 4.0))), 8)
	if next != direction_index:
		direction_index = next
		direction = DIRECTIONS[next]
		_render()
	else:
		_apply_motion()

func advance(delta: float, velocity: Vector2) -> void:
	if not loaded or delta <= 0.0: return
	var previous_velocity := last_velocity
	last_velocity = velocity
	clock += delta
	swing_left = maxf(0.0, swing_left - delta)
	moving = velocity.length_squared() > 0.01
	var distance := velocity.length() * delta
	travelled_distance += distance
	phase += distance / STEP_CYCLE_DISTANCE * TAU
	var target_weight := smoothstep(0.0, 95.0, velocity.length())
	gait_weight = move_toward(gait_weight, target_weight, delta / (0.075 if target_weight > gait_weight else 0.10))
	if moving: gait_direction = gait_direction.lerp(velocity.normalized(), 1.0 - exp(-delta * 22.0))
	elif is_zero_approx(gait_weight): gait_direction = Vector2.ZERO
	last_acceleration = (velocity - previous_velocity) / delta
	var target_lag := (-last_acceleration / 1800.0 * 0.8).limit_length(0.8)
	motion_lag = motion_lag.lerp(target_lag, 1.0 - exp(-delta * 14.0))
	_update_feet()
	_apply_motion()
	queue_redraw()

func _update_feet() -> void:
	if feet == null: return
	feet.phase = phase
	feet.velocity = last_velocity
	var facing := _facing_vector()
	# Shallow depth projection keeps both circles readable in side-facing views.
	var lateral := Vector2(facing.y * 8.0, -facing.x * 2.6)
	if absf(facing.x) > 0.9: lateral.x += facing.x * 4.0
	for index in range(2):
		var side := -1.0 if index == 0 else 1.0
		var stride := sin(phase + float(index) * PI)
		var travel := gait_direction * stride * FOOT_TRAVEL * gait_weight
		var lift := maxf(0.0, stride) * FOOT_LIFT * gait_weight
		var base := lateral * side + Vector2(0, -5.8)
		var extra: Vector2 = sword_pose.get("foot_left_offset" if index == 0 else "foot_right_offset", Vector2.ZERO)
		feet.centres[index] = base + travel - Vector2(0, lift) + extra
		feet.ground_centres[index] = base + Vector2(0, 6.8) + travel + extra
	feet.queue_redraw()

func get_default_hand_pose() -> Dictionary:
	var facing := _facing_vector()
	var waist_y := -BODY_HEIGHT * 0.49
	var spread := clampf(body_width * 0.39 + HAND_DIAMETER * 0.5 + 1.2, 21.0, 28.5)
	var right_lateral := Vector2(-facing.y * spread, facing.x * 6.0)
	var forward_separation := facing * absf(facing.x) * 7.5
	var right := Vector2(0, waist_y) + right_lateral + forward_separation
	var left := Vector2(0, waist_y) - right_lateral - forward_separation
	var right_z := 80 if facing.x >= -0.1 else 12
	var left_z := 80 if facing.x <= 0.1 else 12
	if facing.y < -0.8: right_z = 12; left_z = 12
	return {"right_hand": right, "left_hand": left, "right_hand_z": right_z, "left_hand_z": left_z}

func set_sword_pose(pose: Dictionary) -> void:
	sword_pose = pose.duplicate()
	if loaded:
		_update_feet()
		_apply_motion()

func _apply_motion() -> void:
	if not loaded: return
	var body_offset: Vector2 = sword_pose.get("body_offset", sword_pose.get("torso_offset", Vector2.ZERO))
	var body_rotation := float(sword_pose.get("body_rotation", sword_pose.get("torso_rotation", 0.0)))
	visual.position = Vector2(0, -11.8 + (0.28 - absf(sin(phase)) * 0.65) * gait_weight + float(sword_pose.get("torso_bob", 0.0))) + body_offset
	visual.rotation = body_rotation
	var head: Sprite2D = layers.HeadHair
	head.position = render_registration.HeadHair.at + motion_lag + (sword_pose.get("head_offset", Vector2.ZERO) as Vector2)
	head.rotation = float(sword_pose.get("head_rotation", 0.0))
	var defaults := get_default_hand_pose()
	for data in [["HandRight", "right_hand", 1.0], ["HandLeft", "left_hand", -1.0]]:
		var name_value: String = data[0]
		var pose_key: String = data[1]
		var phase_sign: float = data[2]
		var at: Vector2 = sword_pose.get(pose_key, defaults[pose_key])
		at += (sword_pose.get(pose_key + "_offset", Vector2.ZERO) as Vector2)
		at += Vector2(0, sin(phase) * phase_sign * 0.45 * gait_weight) + motion_lag * 0.44
		layers[name_value].position = at
		layers[name_value].z_index = int(sword_pose.get(pose_key + "_z", defaults[pose_key + "_z"]))

func get_hand_positions() -> Dictionary:
	if not loaded: return {}
	return {"right": layers.HandRight.position, "left": layers.HandLeft.position, "right_z": layers.HandRight.z_index, "left_z": layers.HandLeft.z_index}

func get_sword_anchor() -> Dictionary:
	if not loaded: return {"position": Vector2.ZERO, "z": 80}
	return {"position": layers.HandRight.position, "z": layers.HandRight.z_index}

func get_hand_anchor(side: String = "right", avatar_space: bool = false) -> Vector2:
	if not loaded: return Vector2.ZERO
	var sprite: Sprite2D = layers.HandRight if side == "right" else layers.HandLeft
	return visual.transform * sprite.position if avatar_space else sprite.position

func reset_motion() -> void:
	last_velocity = Vector2.ZERO
	last_acceleration = Vector2.ZERO
	gait_weight = 0.0
	gait_direction = Vector2.ZERO
	motion_lag = Vector2.ZERO
	phase = 0.0
	travelled_distance = 0.0
	moving = false
	swing_left = 0.0
	sword_pose.clear()
	_update_feet()
	_apply_motion()
	queue_redraw()

func strike() -> bool:
	if swing_left > 0.0: return false
	swing_left = 0.32
	return true

func _draw() -> void:
	if not loaded: return
	var points := PackedVector2Array()
	for index in range(40):
		var angle := float(index) * TAU / 40.0
		points.append(Vector2(cos(angle) * body_width * 0.48, sin(angle) * 2.0))
	draw_colored_polygon(points, Color(0.12, 0.16, 0.13, 0.09))

func get_diagnostics() -> Dictionary:
	var sheets := {}
	for key in assets: sheets[key] = {"path": assets[key].path, "sha256": assets[key].sha256, "size": assets[key].size}
	return {"loaded": loaded, "supported": supported, "errors": errors.duplicate(), "direction": direction, "direction_index": direction_index, "view_count": 8, "look_direction": desired_look, "fixed_hairstyle": true, "eyes_visible": false, "front_options": 1, "rear_options": 1, "front_style": 0, "back_style": 0, "round_feet": feet != null, "foot_radius": 5.8, "body_height": BODY_HEIGHT, "body_height_multiplier": 1.15, "body_width": body_width, "head_hair_height": HEAD_HAIR_HEIGHT, "hand_diameter": HAND_DIAMETER, "body_rotation": visual.rotation if loaded else 0.0, "body_bob_y": visual.position.y if loaded else 0.0, "phase": phase, "clock": clock, "moving": moving, "step_cycle_distance": STEP_CYCLE_DISTANCE, "gait_weight": gait_weight, "actual_velocity": last_velocity, "actual_acceleration": last_acceleration, "travelled_distance": travelled_distance, "motion_lag": motion_lag, "feet_pose": feet.get_pose() if feet != null else {}, "layers": render_registration.duplicate(true), "atlas_sources": sheets, "sword_pose": sword_pose.duplicate(), "hands": get_hand_positions(), "writes_game_state": false, "scope": "Fixed black hairstyle, eight independently drawn views, taller cone robe, detached round hands and feet; visual-only sword review."}
