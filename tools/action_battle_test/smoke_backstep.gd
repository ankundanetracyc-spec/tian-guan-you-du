extends SceneTree
func _initialize() -> void:
 call_deferred("run")
func run() -> void:
 var rig = load("res://guigu_rig_runtime.gd").new()
 root.add_child(rig)
 assert(rig.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art", "original_wanderer"))
 rig.set_skin_mode("experiment")
 var hip = "/10002/Root/Body/Foot/zuotui/zuotui/tui1"
 var knee = hip + "/tui2"
 rig.set_character_pose("BackstepWalk", 0.0, 1.0)
 var contact_knee: float = rig.node_by_rig_path[knee].rotation
 var first: Transform2D = rig.node_by_rig_path[hip].transform
 rig.set_character_pose("BackstepWalk", 0.56, 1.0)
 assert(rig.node_by_rig_path[knee].rotation - contact_knee > 0.45, "swing knee flexes")
 rig.set_character_pose("BackstepWalk", 0.7, 1.0)
 assert(rig.node_by_rig_path[hip].transform.is_equal_approx(first), "cycle closes")
 rig.set_character_pose("BackstepWalk", 0.0, -1.0)
 assert(rig.node_by_rig_path[hip].transform.is_equal_approx(first) and rig.scale.x < 0, "facing mirrors without changing phase")
 print("BACKSTEP_GAIT_OK swing knee, loop and facing")
 quit(0)
