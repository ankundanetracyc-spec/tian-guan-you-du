extends SceneTree

## Candidate-only integration check; the live skin selector is unchanged.
class CandidateRig extends "guigu_rig_runtime.gd":
	var selected_profile: StringName = &"A"
	func load_candidate() -> bool:
		custom_art_folder = "yuxu_fullbody_v15"
		var rig_data: Variant = _read_json("res://guigu_rig/rig.json")
		var skins_data: Variant = _read_json("res://guigu_rig/skins.json")
		var animations_data: Variant = _read_json("res://guigu_rig/animations.json")
		if not rig_data is Dictionary or not skins_data is Dictionary or not animations_data is Dictionary: return false
		if not _build_character_hierarchy(rig_data): return false
		if not _load_texture_sets("res://art"): return false
		if not _build_weighted_meshes(skins_data,"res://art"): return false
		wanderer_skin = load("res://closed_robe_options_skin.gd").new()
		wanderer_skin.robe_profile = selected_profile
		bone_bound_art_ready = wanderer_skin.build(self,"res://art/yuxu_fullbody_v15")
		_load_animation_tracks(animations_data)
		loaded = bone_bound_art_ready
		return loaded

var checks := 0
var failures: Array[String] = []

func verify(value: bool, description: String) -> void:
	checks += 1
	if not value:
		failures.append(description)
		if failures.size()<12: push_error(description)

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var profile_results := {}
	for profile in [&"A",&"B",&"C"]:
		var rig := CandidateRig.new()
		rig.selected_profile = profile
		root.add_child(rig)
		verify(rig.load_candidate(),"%s candidate builds with original art"%profile)
		if not rig.loaded: continue
		var skin = rig.wanderer_skin
		rig.set_skin_mode("experiment")
		verify(skin.bones.size()==21 and skin.meshes.size()==6,"%s preserves 21 bones and 6 meshes"%profile)
		verify(skin.garment_body.get_child_count()==0,"%s has one continuous shell"%profile)
		verify(skin.validation_errors.is_empty(),"%s normalized original skin weights"%profile)
		verify(skin.hidden_leg_meshes.size()==2 and skin.sole_profiles.size()==2,"%s retains leg geometry and original contact profiles"%profile)
		for leg in skin.hidden_leg_meshes:
			verify(leg.polygon.size()>0 and not leg.visible,"%s enclosed leg mesh remains present and hidden"%profile)
		rig.set_skin_mode("project")
		rig.set_skin_mode("experiment")
		for leg in skin.hidden_leg_meshes: verify(not leg.visible,"%s visibility toggle preserves zero shoe rendering"%profile)
		var clock := 0.0
		var peak := 0.0
		var peak_stop := 0.0
		var final_settle := 0.0
		var maximum_contact := -INF
		for facing in [1.0,-1.0]:
			for speed in [114.0,235.0]:
				for frame in range(300):
					var velocity := Vector2.ZERO
					var aim: float = facing
					var clip := "Idle"
					var phase := clock
					if frame>=12 and frame<60:
						velocity = Vector2(speed*facing,0.0)
						clip = "Run_New1"
					elif frame>=60 and frame<100:
						velocity = Vector2(-speed*facing,0.0)
						clip = "BackstepWalk"
					elif frame>=100 and frame<135:
						velocity = Vector2(-speed*facing,0.0)
						aim = -facing
						clip = "Run_New1"
					elif frame>=135 and frame<151:
						clip = "AttackIdle"
						phase = float(frame-135)/60.0
					elif frame>=151 and frame<163:
						clip = "Step1"
						velocity = Vector2(600.0*facing,0.0)
						phase = float(frame-151)/60.0
					clock += 1.0/60.0
					skin.set_motion_context(1.0/60.0,velocity,clock,aim)
					rig.set_character_pose(clip,phase,aim)
					var d: Dictionary = skin.get_garment_diagnostics()
					var angles: Vector3 = d.angles
					peak = maxf(peak,angles.length())
					verify(is_finite(angles.length()) and angles.length()<0.40,"%s angular simulation stable"%profile)
					verify(float(d.lowest_cloth_y)>-0.70 and float(d.lowest_cloth_y)<-0.49,"%s opaque hem stays at ground"%profile)
					for leg in skin.hidden_leg_meshes: verify(not leg.visible,"%s movement never renders leg or shoe pixels"%profile)
					for side in ["near","far"]:
						var contact: Dictionary = skin.ground_contacts[side]
						maximum_contact = maxf(maximum_contact,contact.center.y)
						verify(contact.center.is_finite() and contact.center.y<0.10,"%s articulated sole remains above floor"%profile)
						var ankle: Vector2 = rig.rig_root_node.to_local(skin.bones[side+"_foot"].global_position)
						verify(ankle.x>=-4.21 and ankle.x<=3.21,"%s ankle path stays within closed narrow stance"%profile)
					if frame>=163 and frame<180: peak_stop = maxf(peak_stop,angles.length())
					if frame==299: final_settle = maxf(final_settle,angles.length())
				var before := {}
				for key in skin.bones: before[key] = skin.bones[key].transform
				var simulation_before: int = skin.garment_simulation_steps
				for pause in range(6):
					skin.set_motion_context(0.0,Vector2.ZERO,clock,facing)
					rig.set_character_pose("Idle",clock,facing)
				for key in skin.bones: verify(before[key]==skin.bones[key].transform,"%s paused bones freeze"%profile)
				verify(simulation_before==skin.garment_simulation_steps,"%s paused simulation freezes"%profile)
		verify(peak>0.03,"%s acceleration excites real cloth motion"%profile)
		verify(peak_stop>0.012,"%s stop retains inertia"%profile)
		verify(final_settle<0.008,"%s cloth settles after stop"%profile)
		clock += 1.0/60.0
		skin.set_motion_context(1.0/60.0,Vector2(235.0,0.0),clock,1.0)
		rig.set_character_pose("Run_New1",clock,1.0)
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,0.0,1.0)
		rig.set_character_pose("Idle",0.0,1.0)
		verify(skin.garment_angles.length()<0.005 and skin.garment_angular_velocity.length()<0.05,"%s restart clears momentum"%profile)
		rig.position += Vector2(1200.0,0.0)
		skin.set_motion_context(1.0/60.0,Vector2.ZERO,1.0/60.0,1.0)
		rig.set_character_pose("Idle",1.0/60.0,1.0)
		verify(skin.garment_angles.length()<0.005 and skin.garment_drive==0.0,"%s teleport resets motion"%profile)
		verify(skin.has_contact_shadow() and skin.contact_shadow.visible,"%s contact shadow retained"%profile)
		profile_results[str(profile)] = {"peak":peak,"stop_peak":peak_stop,"settled":final_settle,"max_sole_y":maximum_contact}
		rig.queue_free()
	print("CLOSED ROBE OPTIONS CHECKS: ",checks,"; failures: ",failures.size(),"; profiles: ",JSON.stringify(profile_results))
	quit(0 if failures.is_empty() else 1)
