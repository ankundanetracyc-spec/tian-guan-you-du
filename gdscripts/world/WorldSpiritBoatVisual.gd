extends Node2D
## Presentation only: a vehicle provides a deck for the existing world avatars.
## It neither moves a player nor decides travel speed, ownership or encounters.

const PlayerTraveler = preload("res://gdscripts/world/WorldActionTraveler.gd")
const NpcTraveler = preload("res://gdscripts/world/WorldNpcTraveler.gd")
const WakeVisual = preload("res://gdscripts/world/SpiritBoatWake.gd")
const VisualScale = preload("res://gdscripts/world/WorldMapVisualScale.gd")
const ART_PATH := "res://textures/world/bai_chuan/vehicles/yuxu_boat_layered_20261106/"
const BOAT_WORLD_WIDTH := VisualScale.BOAT_WORLD_WIDTH
const BOAT_LAYOUT_SCALE := BOAT_WORLD_WIDTH / 150.0

var visual_root: Node2D
var hull: Sprite2D
var rail: Sprite2D
var player_avatar: Node2D
var driver_avatar: AnimatedSprite2D
var passenger_avatars: Array[AnimatedSprite2D] = []
var passenger_ids: PackedStringArray = []
var passengers_ready := false
var visual_time := 0.0
var facing := 1.0
var ready_to_draw := false
var flight_height := 0.0
var map_zoom := 1.0
var ground_shadow: Node2D
var wake: Node2D

class FlightGroundShadow extends Node2D:
	func _draw() -> void:
		draw_set_transform(Vector2.ZERO, 0.0, Vector2(1.0, 0.19))
		for i in range(12, 0, -1):
			draw_circle(Vector2.ZERO, 49.0 * BOAT_LAYOUT_SCALE * float(i) / 12.0, Color(0.18, 0.19, 0.15, 0.018), true, -1.0, true)
		draw_set_transform(Vector2.ZERO)

func _texture(path: String) -> Texture2D:
	# Prefer the imported texture in exports; fallback keeps first-run previews
	# usable before the editor has imported the freshly generated files.
	if ResourceLoader.exists(path):
		var imported := ResourceLoader.load(path) as Texture2D
		if imported != null: return imported
	var image := Image.load_from_file(path)
	if image == null or image.is_empty(): return null
	return ImageTexture.create_from_image(image)

func build(driver_id := "preview_yuxu_driver") -> bool:
	if ready_to_draw: return true
	var hull_texture := _texture(ART_PATH + "hull_back.png")
	var rail_texture := _texture(ART_PATH + "rail_front.png")
	if hull_texture == null or rail_texture == null: return false
	ground_shadow = FlightGroundShadow.new()
	ground_shadow.name = "GroundShadow"
	ground_shadow.z_index = -1
	add_child(ground_shadow)
	wake = WakeVisual.new()
	wake.name = "AirborneWake"
	add_child(wake)
	if not wake.configure(): return false
	visual_root = Node2D.new()
	visual_root.name = "BoatAndPassengers"
	add_child(visual_root)
	hull = Sprite2D.new()
	hull.name = "BackHull"
	hull.texture = hull_texture
	hull.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	hull.scale = Vector2.ONE * BOAT_WORLD_WIDTH / hull_texture.get_width()
	hull.z_index = 0
	visual_root.add_child(hull)
	player_avatar = PlayerTraveler.new()
	player_avatar.name = "PlayerOnDeck"
	player_avatar.position = Vector2(-17, 12) * BOAT_LAYOUT_SCALE
	player_avatar.z_index = 1
	visual_root.add_child(player_avatar)
	if not player_avatar.build(VisualScale.PLAYER_WORLD_HEIGHT): return false
	VisualScale.flatten_avatar_layers(player_avatar)
	if not set_passenger_ids(PackedStringArray([driver_id]) if not driver_id.is_empty() else PackedStringArray()): return false
	rail = Sprite2D.new()
	rail.name = "FrontRail"
	rail.texture = rail_texture
	rail.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	# The generated foreground includes decorative tips matching the hull.
	# Draw its central beam only, so the hull's own prow and stern remain single.
	rail.region_enabled = true
	rail.region_rect = Rect2(340, 360, 1492, 175)
	rail.scale = Vector2.ONE * BOAT_WORLD_WIDTH / rail_texture.get_width()
	rail.position.y = (360.0 + 175.0 * 0.5 - rail_texture.get_height() * 0.5) * rail.scale.y + 4.0 * BOAT_LAYOUT_SCALE
	rail.z_index = 2
	visual_root.add_child(rail)
	ready_to_draw = true
	return true

## Reuse the actual party identities; an unaccompanied player has no phantom driver.
func set_passenger_ids(ids: PackedStringArray) -> bool:
	if passengers_ready and ids == passenger_ids: return true
	for avatar in passenger_avatars:
		if is_instance_valid(avatar): avatar.queue_free()
	passenger_avatars.clear()
	passenger_ids = ids.duplicate()
	passengers_ready = true
	driver_avatar = null
	var total := ids.size() + 1
	# Expand the occupied deck as more people board so a full vessel
	# remains readable without changing the shared world-avatar scale.
	var half_span := minf(60.0, 42.0 + 3.0 * maxf(0.0, float(total - 2)))
	player_avatar.position = Vector2(0.0 if total == 1 else -half_span, 12.0) * BOAT_LAYOUT_SCALE
	for i in ids.size():
		var avatar := NpcTraveler.new()
		avatar.name = "Passenger_%d" % i
		avatar.z_index = 1
		visual_root.add_child(avatar)
		if not avatar.configure(ids[i]):
			avatar.queue_free()
			return false
		avatar.scale = Vector2.ONE * VisualScale.NPC_WORLD_HEIGHT / avatar.native_height
		var x := -half_span + 2.0 * half_span * float(i + 1) / float(total - 1)
		avatar.sync_sample(Vector2(x, 12.0) * BOAT_LAYOUT_SCALE, false)
		passenger_avatars.append(avatar)
		if i == 0: driver_avatar = avatar
	return true

func set_facing(direction: float) -> void:
	facing = -1.0 if direction < 0.0 else 1.0
	if is_instance_valid(visual_root): visual_root.scale.x = facing

## X/Y are continuous map coordinates. Z is a separate, positive visual height;
## its gameplay scale and attainable altitude are intentionally not defined here.
func set_flight_position(point: Vector3) -> bool:
	if not point.is_finite() or point.z < 0.0: return false
	position = Vector2(point.x, point.y)
	flight_height = point.z
	_update_height_projection()
	return true

func get_flight_position() -> Vector3:
	return Vector3(position.x, position.y, flight_height)

func set_map_zoom(zoom: float) -> void:
	map_zoom = maxf(zoom, 0.01)
	# Hull, passengers, shadow and wake share the landscape's world transform.
	scale = Vector2.ONE
	_update_height_projection()

func _update_height_projection() -> void:
	if not is_instance_valid(visual_root): return
	var floating := sin(visual_time * 1.8) * 0.8 if flight_height > 0.0 else 0.0
	visual_root.position.y = -flight_height + floating
	if is_instance_valid(ground_shadow):
		VisualScale.place_on_ground(ground_shadow, position.y)
		ground_shadow.modulate.a = lerpf(0.8, 0.35, clampf(flight_height / 200.0, 0.0, 1.0))

func _process(delta: float) -> void:
	if not ready_to_draw: return
	var active := is_visible_in_tree()
	var clock := get_node_or_null("/root/GameTimeManager")
	var paused := clock != null and bool(clock.get("is_paused"))
	if active and not paused: visual_time += minf(delta, 0.05)
	_update_height_projection()
	wake.update_flight(0.0 if paused or not active else delta, position, visual_root.position, map_zoom, facing, flight_height > 0.0 and active and not paused)
	if active and not paused: player_avatar.update_motion(delta, Vector2.ZERO)
	for avatar in passenger_avatars: avatar.update_activity(false, paused, active)
