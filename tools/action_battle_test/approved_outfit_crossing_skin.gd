extends "approved_outfit_stride_skin.gd"

## Independent preview of the approved v15 drawing. A single closed surface
## shares softly delayed front/rear loads. Cloth is carried from the waist
## into the lower hem, preserving layers without a hard bulge at the hip.
var approved_closed_robe_mesh: Polygon2D
var approved_closed_robe_record: Dictionary = {}
var approved_original_panels_hidden := false
var approved_cross_rear_load := 0.0
var approved_cross_front_load := 0.0
var approved_cross_clock := -INF
var approved_cross_samples := 0
var approved_cross_order_changes := 0
var approved_cross_last_order := 0.0
var approved_cross_tip_gap := 0.0
var approved_hands: RefCounted
var approved_sword: RefCounted
var approved_upper_source := ""

func build(owner_runtime: Node2D, art_dir: String) -> bool:
	if not super.build(owner_runtime,art_dir): return false
	var resource_dir: String = get_script().resource_path.get_base_dir()
	var male_profile: Variant = JSON.parse_string(FileAccess.get_file_as_string(resource_dir.path_join("approved_guigu_run_profile_20261105.json")))
	if not male_profile is Dictionary or not male_profile.has("samples"):
		validation_errors.append("Missing inspected male run profile")
		return false
	var upper_samples: Array = male_profile.samples.duplicate(true)
	for sample in upper_samples:
		# The same male zero phase drives arms, torso and both legs. The
		# previous female frontmost-foot offset must not be added a second time.
		if not sample.has("cloth_root_angle"): sample.cloth_root_angle = 0.79
	guigu_pose_profile.clips.Run_New1.samples = upper_samples
	guigu_pose_profile.clips.Run_New1.nearFrontmostPhase = 0.0
	guigu_pose_profile.clips.Run_New1.duration = male_profile.duration
	guigu_pose_profile.clips.Run_New1.sampleCount = upper_samples.size()
	guigu_pose_profile.clips.Run_New1.retargetingAlignment = "male101 original zero phase, shared with both legs"
	approved_upper_source = "local male101 Run_New1; zero-phase torso/arms synchronized with fixed-length legs"
	var rect := [275,310,390,1030]
	var vertices := PackedVector2Array()
	var sets: Array = []
	for index in range(8): sets.append(PackedFloat32Array())
	for row in range(ROWS+1):
		var source_y := float(rect[1])+float(rect[3])*row/ROWS
		var torso_share := 1.0-smoothstep(660.0,790.0,source_y)
		# All free cloth lies below its source820 root pivot. Above-pivot
		# weights previously pushed the hip outward while the hem trailed.
		var cloth_share := smoothstep(835.0,1090.0,source_y)
		var hem_share := smoothstep(1045.0,1240.0,source_y)
		var edge_share := smoothstep(1230.0,1340.0,source_y)
		for column in range(COLUMNS+1):
			var source_x := float(rect[0])+float(rect[2])*column/COLUMNS
			var front_share := smoothstep(365.0,575.0,source_x)
			var loose := (1.0-torso_share)*cloth_share
			# One native chart, no seam split and no floor-length extension.
			vertices.append(_world(Vector2(source_x,source_y)))
			sets[0].append(torso_share)
			sets[1].append((1.0-torso_share)*(1.0-cloth_share))
			sets[2].append(loose*(1.0-hem_share)*front_share)
			sets[3].append(loose*hem_share*(1.0-edge_share)*front_share)
			sets[4].append(loose*(1.0-hem_share)*(1.0-front_share))
			sets[5].append(loose*hem_share*(1.0-edge_share)*(1.0-front_share))
			sets[6].append(loose*hem_share*edge_share*front_share)
			sets[7].append(loose*hem_share*edge_share*(1.0-front_share))
	texture = textures.body
	_create_mesh("robe_continuous",rect,vertices,["torso","pelvis","front_cloth","front_hem","back_cloth","back_hem","front_edge","back_edge"],sets,60)
	approved_closed_robe_mesh = meshes.back()
	approved_closed_robe_record = mesh_records.back()
	# Existing floor checks must measure the visible surface, not hidden
	# split-panel descendants. Their original meshes remain for provenance.
	cape_body_records.clear()
	cape_body_records.append(approved_closed_robe_record)
	_hide_original_panels()
	approved_hands = load(resource_dir.path_join("approved_outfit_full_arm_layer.gd")).new()
	if not approved_hands.build(self):
		for error in approved_hands.errors: validation_errors.append(error)
	approved_sword = load(resource_dir.path_join("approved_outfit_sword_layer.gd")).new()
	if not approved_sword.build(self,approved_hands,resource_dir.path_join("art/approved_outfit_sword_20261105")):
		validation_errors.append("Sword attachment could not load")
	return validation_errors.is_empty()

func _hide_original_panels() -> void:
	for record in mesh_records:
		if record.name in ["robe_back","robe_front"]: record.mesh.visible = false
	approved_original_panels_hidden = true

func set_visible(value: bool) -> void:
	super.set_visible(value)
	_hide_original_panels()
	if approved_hands!=null: approved_hands.set_visible(value)
	if approved_sword!=null: approved_sword.set_visible(value)

func set_motion_context(delta: float, velocity: Vector2, clock: float, facing: float) -> void:
	var was_facing := motion_facing
	var reset := is_finite(approved_cross_clock) and (clock<approved_cross_clock or clock-approved_cross_clock>0.25)
	super.set_motion_context(delta,velocity,clock,facing)
	if delta<=0.0: return
	if reset or pending_gait_reset:
		approved_cross_rear_load = 0.0
		approved_cross_front_load = 0.0
		approved_cross_samples = 0
		approved_cross_order_changes = 0
		approved_cross_last_order = 0.0
	elif facing!=was_facing:
		approved_cross_rear_load *= -1.0
		approved_cross_front_load *= -1.0
	approved_cross_clock = clock

func _integrate_garment() -> void:
	var anchor_drive := _sample_anchor_drive()
	var source_cloth: float = _source_sample("Run_New1",_source_phase(gait_phase),"cloth_root_angle")
	reference_cloth_base = clampf(0.64+(source_cloth-0.79)*0.52,0.54,0.72)
	reference_target_wind_load = signf(cape_wind)*sqrt(absf(cape_wind))
	var target := reference_target_wind_load
	var rear_unload := absf(target)<absf(approved_cross_rear_load)
	var front_unload := absf(target)<absf(approved_cross_front_load)
	var rear_time := 0.22 if target*approved_cross_rear_load<0.0 else (0.40 if rear_unload else 0.11)
	var front_time := 0.14 if target*approved_cross_front_load<0.0 else (0.23 if front_unload else 0.075)
	approved_cross_rear_load = lerpf(approved_cross_rear_load,target,1.0-exp(-motion_delta/rear_time))
	approved_cross_front_load = lerpf(approved_cross_front_load,target,1.0-exp(-motion_delta/front_time))
	reference_trailing_wind_load = approved_cross_rear_load
	# Travel continues to excite the loose lower cloth after the initial
	# acceleration. A fixed wind target alone settled into a lifeless pose.
	var flutter_clock := motion_clock*TAU*0.95
	# One solver also handles casts and weapon previews. No return to the
	# old large root rotation when a non-locomotion clip starts.
	var dt := motion_delta/8.0
	for substep in range(8):
		for front in [false,true]:
			var angles: Vector3 = reference_front_angles if front else garment_angles
			var speeds: Vector3 = reference_front_angular_velocity if front else garment_angular_velocity
			var load: float = approved_cross_front_load if front else approved_cross_rear_load
			var parent_angle := 0.0
			var parent_speed := 0.0
			for segment in range(3):
				var stiffness: float = [70.0,40.0,22.0][segment]*(1.10 if front else 1.0)
				var gravity: float = [26.0,16.0,8.0][segment]
				var damping: float = [9.0,4.8,2.5][segment]*(1.15 if front else 1.0)
				var exposure: float = [0.78,0.68,0.58][segment] if front else 1.0
				var flutter := sin(flutter_clock-float(segment)*0.85+(0.55 if front else 0.0))*float([0.02,0.12,0.18][segment])
				var desired: float = load*(float([reference_cloth_base,0.38,0.34][segment])+flutter)*exposure
				var angle_target := desired+gravity/stiffness*sin(parent_angle+desired)
				var angle: float = angles[segment]
				var torque := (angle_target-angle)*stiffness-gravity*sin(parent_angle+angle)
				torque += garment_drive*float([2.4,5.8,9.2][segment])*cos(parent_angle+angle)-parent_speed*1.10
				torque += anchor_drive*float([0.15,0.80,1.70][segment])
				speeds[segment] += (torque-speeds[segment]*damping)*dt
				angle += speeds[segment]*dt
				var limit: float = [0.95,0.58,0.58][segment]
				if absf(angle)>limit:
					angle = clampf(angle,-limit,limit)
					speeds[segment] *= 0.30
				angles[segment] = angle
				parent_angle += angle
				parent_speed += speeds[segment]
			if front:
				reference_front_angles = angles
				reference_front_angular_velocity = speeds
			else:
				garment_angles = angles
				garment_angular_velocity = speeds
	garment_simulation_steps += 1

func update_cloth(clip: String, time: float, duration: float, tracks: Dictionary) -> void:
	if motion_context and motion_delta<=0.0 and last_clip!="": return
	super.update_cloth(clip,time,duration,tracks)
	if approved_sword!=null: approved_sword.update_pose(motion_delta,clip,time)
	if approved_hands!=null: approved_hands.update_pose()
	if approved_sword!=null: approved_sword.update_attachment()
	if clip=="BindPose": return
	# These are control-chain tips inside one connected cloth surface. Their
	# different inertia changes internal facing/folds, never opens a gap.
	var front_tip: Vector2 = runtime.rig_root_node.to_local(bones.front_edge.to_global(Vector2(0.0,120.0*PIXEL_SCALE)))
	var rear_tip: Vector2 = runtime.rig_root_node.to_local(bones.back_edge.to_global(Vector2(0.0,120.0*PIXEL_SCALE)))
	approved_cross_tip_gap = rear_tip.x-front_tip.x
	var order := signf(approved_cross_tip_gap) if absf(approved_cross_tip_gap)>0.20 else 0.0
	if order!=0.0 and approved_cross_last_order!=0.0 and order!=approved_cross_last_order: approved_cross_order_changes += 1
	if order!=0.0: approved_cross_last_order = order
	approved_cross_samples += 1

func get_garment_diagnostics() -> Dictionary:
	var result := super.get_garment_diagnostics()
	result.mode = "approved_continuous_closed_robe"
	result.support_model = "continuous waist-to-hem carry with softly delayed front/rear layers"
	result.original_split_panels = false
	result.approved_upper_source = approved_upper_source
	if approved_hands!=null: result.approved_hands = approved_hands.get_diagnostics()
	if approved_sword!=null: result.approved_sword = approved_sword.get_diagnostics()
	result.approved_crossing = {"mode":"one continuous cloth chart; small front/rear response differences retain natural depth","rear_load":approved_cross_rear_load,"front_load":approved_cross_front_load,"tip_horizontal_gap":approved_cross_tip_gap,"tip_order_changes":approved_cross_order_changes,"samples":approved_cross_samples,"depth_order":"one fixed-depth garment; legacy split meshes hidden","new_artwork":false,"rest_drawing_preserved":true,"limb_lengths_changed":false,"continuous_closed_mesh":approved_closed_robe_mesh!=null,"legacy_panels_hidden":approved_original_panels_hidden,"native_chart":[275,310,390,1030],"extra_length_added":0.0}
	result.approved_elastic = {"waist_motion":"retained; smooth carry into freely flying lower cloth, no above-pivot free weights","cloth_root_source_y":820,"free_weights_source_y":[835,1090],"lower_weights_source_y":[1045,1240],"edge_weights_source_y":[1230,1340],"root_target":reference_cloth_base,"segment_angle_limits":[0.95,0.58,0.58],"layering":"distinct delayed front/rear exposure, large trailing lift and free-edge curl","steady_flutter_hz":0.95,"steady_flutter_strength":[0.02,0.12,0.18],"solver_scope":"same elastic response for locomotion, casts and weapon previews"}
	return result

func summary() -> Dictionary:
	var result := super.summary()
	result.source = "approved v15 original drawing / fixed stride limbs / continuous layered hem elasticity"
	return result
