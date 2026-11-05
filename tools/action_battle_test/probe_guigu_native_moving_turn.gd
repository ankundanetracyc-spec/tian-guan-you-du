extends "probe_guigu_native_turn_integrity.gd"

## Reuses the alpha-visible triangle/control comparisons with the new runtime
## movement layer enabled and honest matching world travel/velocity contexts.
func _new_actor() -> Node2D:
	var actor := super._new_actor()
	_check(actor.wanderer_skin.locomotion_layer_enabled, "new movement composition layer is enabled on the actual shared actor")
	return actor

func _step(actor: Node2D, clip: String, pose_time: float, clock: float, facing: float) -> void:
	var velocity := Vector2(float(actor.wanderer_skin.ROBED_MOVE_SPEED) * facing, 0.0) if clip in ["Run_New1", "Run_New2", "AttackIdle", "AttackRun"] else Vector2.ZERO
	# Each facing-isolation actor travels forward in its own local frame.
	# Comparing identical world velocity across opposite facings would instead
	# compare forward cloth inertia with backward inertia, a different action.
	actor.position += velocity / 60.0
	actor.wanderer_skin.set_motion_context(1.0 / 60.0, velocity, clock, facing)
	actor.set_character_pose(clip, pose_time, facing)

func _run() -> void:
	_static_case(1.0)
	_static_case(-1.0)
	_turn_case("static")
	_turn_case("run")
	_turn_case("clip_transitions")
	_far_placement_and_stop()
	var project_root := ProjectSettings.globalize_path(get_script().resource_path).get_base_dir().get_base_dir().get_base_dir()
	var suffix := "root" if ProjectSettings.globalize_path("res://").simplify_path().trim_suffix("/") == project_root else "nested"
	report_path = project_root.path_join("output/guigu_native_first_20261105/moving_attack/turn_integrity_composed_" + suffix + ".json")
	var report := {"passed":failures.is_empty(), "checks":checks, "samples":samples, "failures":failures, "cases":cases, "duplicate_same_clock_cloth_error_px":duplicate_pose_vertex_error, "locomotion_composition_enabled":true, "actual_movement_speed_pixels_per_second":float(alive[0].wanderer_skin.ROBED_MOVE_SPEED), "scope":"Actual native source actor with new movement composition enabled. Control/turning actors each advance at the public native gait-ruler speed in their own facing direction, preserving comparable local-forward inertia during Run and Attack. Original43 alpha-visible cloth triangle tests include static left/right,35 repeated turns per600-frame static/run/clip-transition case, moving-attack transitions and far placement plus move/stop/recovery.", "limits":"Control-relative extra flips and retained stopped folds are measured separately. Backward and forward world travel must not be called the same physical control. This is bounded visible-geometry QA, not a proof that all source clips or every combat moment have zero cloth overlap."}
	DirAccess.make_dir_recursive_absolute(report_path.get_base_dir())
	FileAccess.open(report_path, FileAccess.WRITE).store_string(JSON.stringify(report, "\t") + "\n")
	print("NATIVE MOVING TURN ", suffix, " ", "PASS" if failures.is_empty() else "FAIL", ": ", checks, " checks; ", JSON.stringify(failures))
	for actor in alive: actor.queue_free()
	get_tree().quit(0 if failures.is_empty() else 1)
