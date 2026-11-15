extends RefCounted

## Ground XY and elevation are independent. Values come from the live human rig,
## rather than treating a foot marker as the entire body or adding a new RPG stat.
static func hand_height(fighter: Node2D) -> float:
 var relative_height := 58.0 # Existing side-view hand presentation fallback.
 var rig: Node2D = fighter.original_wanderer
 if rig != null and rig.wanderer_skin != null:
  var bones: Dictionary = rig.wanderer_skin.bones
  if bones.has("near_hand"):
   var hand: Node2D = bones.near_hand
   relative_height = maxf(0.0, rig.global_position.y - hand.global_position.y)
 return float(fighter.elevation) + relative_height

static func body_height(fighter: Node2D) -> float:
 var rig: Node2D = fighter.original_wanderer
 if rig == null or rig.wanderer_skin == null:
  return maxf(0.0, hand_height(fighter) - float(fighter.elevation))
 var height := 0.0
 for mesh in rig.wanderer_skin.meshes:
  if not str(mesh.name).ends_with("_head"): continue
  var transform: Transform2D = mesh.global_transform
  for vertex in mesh.polygon:
   var point: Vector2 = transform * vertex
   height = maxf(height, rig.global_position.y - point.y)
 return maxf(height, hand_height(fighter) - float(fighter.elevation))

static func intersects_height(fighter: Node2D, attack_height: float) -> bool:
 var bottom := float(fighter.elevation)
 return attack_height >= bottom and attack_height <= bottom + body_height(fighter)

static func pointer_direction(fighter: Node2D, pointer: Vector2) -> Vector2:
 # Interpret the pointer on the horizontal plane through the hand. A pointer
 # over an opponent's torso therefore aims across, without aiming from the feet.
 var ground_target := pointer + Vector2(0, hand_height(fighter))
 var direction: Vector2 = (ground_target - Vector2(fighter.player)).normalized()
 return direction if not direction.is_zero_approx() else Vector2(fighter.facing, 0)
