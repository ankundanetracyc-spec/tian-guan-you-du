extends RefCounted
## Shared world-space ruler. The camera projects these dimensions together with
## the landscape; these values never change map distance or travel speed.
const AVATAR_WORLD_HEIGHT := 52.0
const PLAYER_WORLD_HEIGHT := AVATAR_WORLD_HEIGHT
const NPC_WORLD_HEIGHT := AVATAR_WORLD_HEIGHT
const COMPANION_WORLD_HEIGHT := AVATAR_WORLD_HEIGHT
const BOAT_WORLD_WIDTH := 208.0
const PARTY_WORLD_SPACING := 32.0
const AVATAR_HIT_WORLD_SIZE := Vector2(36.0, 60.0)
const PLAYER_HALO_WORLD_RADIUS := AVATAR_WORLD_HEIGHT * 0.325
# Compatibility names also denote world units, never screen pixels.
const AVATAR_HEIGHT := AVATAR_WORLD_HEIGHT
const PLAYER_HEIGHT := PLAYER_WORLD_HEIGHT
const NPC_HEIGHT := NPC_WORLD_HEIGHT
const COMPANION_HEIGHT := COMPANION_WORLD_HEIGHT
const BOAT_WIDTH := BOAT_WORLD_WIDTH
const DEPTH_BASE := 128
const LABEL_DEPTH := 4060
const INPUT_DEPTH := 4070
const FLIGHT_DEPTH := 3970
const HUD_DEPTH := 4090

static func ground_depth(y: float) -> int:
	return clampi(DEPTH_BASE + floori(y * 0.5), DEPTH_BASE, 3900)

static func place_on_ground(item: CanvasItem, y: float) -> void:
	item.z_as_relative = false
	item.z_index = ground_depth(y)

static func overlay(item: CanvasItem, depth: int = HUD_DEPTH) -> void:
	item.z_as_relative = false
	item.z_index = depth

static func flatten_avatar_layers(root: Node) -> void:
	# The native rig's original 30..36 layers are already in painter order.
	# Keep that order within one actor so an adjacent tree cannot cut its body
	# into different world depths. This changes only map-owned instances.
	if root.has_meta("map_atomic_layers"): return
	_flatten_children(root)
	root.set_meta("map_atomic_layers", true)

static func _flatten_children(root: Node) -> void:
	for child in root.get_children():
		if child is CanvasItem:
			child.z_as_relative = true
			child.z_index = -1 if child.name == "NativeFootAnchor" else 0
		_flatten_children(child)
