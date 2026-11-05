extends SceneTree

func _initialize() -> void:
	call_deferred("verify")

func resources(arena: Node2D) -> Array:
	return [arena.hp, arena.enemy_hp, arena.stamina, arena.mana, arena.qi, arena.qi_blood,
		arena.skill_cooldowns.duplicate(true), arena.pending_self_skill, arena.self_cast_remaining]

func verify() -> void:
	var view := SubViewport.new()
	view.size = Vector2i(1280, 720)
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	var arena: Node2D = load("res://capture_self_vfx_arena.gd").new()
	arena.original_wanderer_enabled = true
	arena.original_wanderer_art_folder = "yuxu_fullbody_v15"
	view.add_child(arena)
	arena.set_process(false)
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_MINIMIZED)
	var output := "E:/tmp/stacklands_self_follow_20261104"
	DirAccess.make_dir_recursive_absolute(output)
	var records: Array[Dictionary] = []
	var base := Vector2(1500, 950)
	for kind in ["催血", "聚炁"]:
		for stage in ["cast", "tail"]:
			arena.restart_battle()
			arena.player = base
			arena.enemy = base + Vector2(210, 0)
			arena.wolf_state = "recover"
			arena.wolf_clock = 100
			arena._update_trial_camera(0.0, true)
			arena.use_skill(kind)
			arena.update_skill_timers(0.50 if stage == "cast" else 1.01)
			if stage == "tail": arena.update_strike_fx(0.16)
			var old_anchor := Vector2.ZERO
			var poses := [base, base + Vector2(180, -85), base + Vector2(-130, 80)]
			for pose in range(3):
				arena.player = poses[pose]
				arena.facing = -1.0 if pose == 2 else 1.0
				arena.visual_motion_delta = 0.0
				arena.update_skinned_player()
				var anchor: Vector2 = arena.skill_vfx.self_effect_anchor(kind)
				var screen_anchor: Vector2 = arena.get_global_transform_with_canvas() * anchor
				if pose == 0: old_anchor = screen_anchor
				var before := resources(arena)
				var prefix := "%s_%s_%d" % ["blood" if kind == "催血" else "qi", stage, pose]
				for visible_fx in [false, true]:
					arena.skill_vfx.visible = visible_fx
					arena.queue_redraw()
					await process_frame
					RenderingServer.force_draw(false)
					var shot := view.get_texture().get_image()
					assert(shot.save_png(output.path_join(prefix + ("_fx.png" if visible_fx else "_base.png"))) == OK)
				assert(resources(arena) == before, "绘制跟随不改变资源、运功与冷却")
				records.append({"prefix":prefix, "kind":kind, "stage":stage, "pose":pose,
					"anchor":[screen_anchor.x, screen_anchor.y], "old_anchor":[old_anchor.x, old_anchor.y]})
	var file := FileAccess.open(output.path_join("anchors.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(records))
	file.close()
	print("SELF_FOLLOW_CAPTURE 12 poses / 24 rendered pairs; resource invariance passed")
	arena.queue_free()
	await process_frame
	quit()
