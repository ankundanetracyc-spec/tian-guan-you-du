extends Node2D

const PHASES := {
	"Idle": [0.0, 0.408, 0.817, 1.225],
	"Run_New1": [0.0, 0.049, 0.097, 0.146, 0.194, 0.243, 0.292, 0.340, 0.389, 0.438, 0.486, 0.535],
	"BackstepWalk": [0.0, 0.117, 0.233, 0.350, 0.467, 0.583],
	"AttackIdle": [0.0, 0.033, 0.067, 0.100, 0.133, 0.160],
	"Step1": [0.0, 0.033, 0.067, 0.100, 0.133, 0.160],
}
var battle: Node2D

func _ready() -> void:
	battle = load("res://ActionBattleFeelTest.tscn").instantiate()
	battle.original_wanderer_enabled = true
	add_child(battle)
	await get_tree().process_frame
	battle.set_process(false)
	battle.set_physics_process(false)
	battle.player = Vector2(1600, 950)
	battle.enemy = Vector2(2150, 1020)
	battle._update_trial_camera()
	battle.update_skinned_player()
	var output := ProjectSettings.globalize_path("res://output/yuxu_hands_v11")
	var continuous := OS.get_environment("YUXU_CAPTURE_CONTINUOUS") == "1"
	if continuous:
		output += "_cycle"
	DirAccess.make_dir_recursive_absolute(output)
	# A second actual renderer at larger scale; this is not an enlarged raster crop.
	var detail := SubViewport.new()
	detail.size = Vector2i(440, 500)
	detail.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	detail.transparent_bg = false
	add_child(detail)
	var backdrop := ColorRect.new()
	backdrop.color = Color("58646a")
	backdrop.size = Vector2(440, 500)
	detail.add_child(backdrop)
	var rig = load("res://guigu_rig_runtime.gd").new()
	detail.add_child(rig)
	assert(rig.load_runtime("res://guigu_rig/rig.json", "res://guigu_rig/skins.json", "res://guigu_rig/animations.json", "res://art", "original_wanderer"))
	rig.set_skin_mode("experiment")
	rig.position = Vector2(220, 420)
	for facing in [1.0, -1.0]:
		for clip in PHASES:
			var selected := OS.get_environment("YUXU_CAPTURE_CLIP")
			if not selected.is_empty() and selected != clip:
				continue
			var phases: Array = PHASES[clip]
			if continuous and clip == "Run_New1":
				phases = []
				for index in range(48):
					phases.append(float(rig.animation_meta.Run_New1.duration) * index / 48.0)
			for phase in phases:
				battle.facing = facing
				battle.walk_amount = 1.0 if clip in ["Run_New1", "BackstepWalk"] else 0.0
				battle.moving_backward = clip == "BackstepWalk"
				battle.motion_time = phase
				battle.visual_attack_time = phase if clip == "AttackIdle" else -1.0
				battle.dash_move = 0.18 - phase if clip == "Step1" else 0.0
				battle.attack_effect = 0.0
				battle.update_skinned_player()
				battle.queue_redraw()
				rig.set_character_pose(clip, phase, facing)
				rig.scale *= 4.0
				await get_tree().process_frame
				await RenderingServer.frame_post_draw
				var name := "%s_%03d_%s" % [clip, roundi(phase * 1000), "right" if facing > 0 else "left"]
				if not continuous:
					# JPEG avoids this Windows build's intermittent full-viewport
					# PNG allocation failure; detail renders remain lossless PNG.
					if get_viewport().get_texture().get_image().save_jpg(output.path_join(name + "_camera.jpg"), 0.98) != OK:
						get_tree().quit(1)
						return
				if detail.get_texture().get_image().save_png(output.path_join(name + "_detail.png")) != OK:
					get_tree().quit(1)
					return
	print("HAND REVIEW COMPLETE: ", output)
	get_tree().quit()
