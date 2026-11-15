extends Node2D

const PreviewRuntime = preload("approved_outfit_fitting_runtime.gd")
var rig: Node2D
var skin: RefCounted
var clock := 0.0
var checks := 0
var failures: Array[String] = []
var tip_min := INF
var tip_max := -INF
var motion_samples := []

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value: failures.append(message)

func _tick(clip: String, velocity: Vector2, facing: float = 1.0, delta: float = 1.0/60.0) -> void:
	clock += delta
	skin.set_motion_context(delta,velocity,clock,facing)
	rig.set_character_pose(clip,clock,facing)
	tip_min = minf(tip_min,float(skin.approved_cross_tip_gap))
	tip_max = maxf(tip_max,float(skin.approved_cross_tip_gap))
	if int(clock*60.0)%12==0: motion_samples.append({"clock":clock,"clip":clip,"facing":facing,"crossing":skin.get_garment_diagnostics().approved_crossing})

func _ready() -> void:
	rig = PreviewRuntime.new()
	add_child(rig)
	_check(rig.build(),"approved original outfit runtime builds")
	if not rig.loaded:
		_finish()
		return
	skin = rig.wanderer_skin
	var panel_layers := {}
	for record in skin.mesh_records:
		if record.name in ["robe_back","robe_front"]: panel_layers[record.name] = record.mesh.z_index
	_check(panel_layers.size()==2 and skin.cape_body_records.size()==1 and skin.approved_original_panels_hidden,"legacy split panels are hidden and actual floor checks use one closed robe")
	_check(skin.approved_closed_robe_mesh!=null and skin.approved_closed_robe_mesh.visible,"a single continuous original chart is the only active garment surface")
	for frame in range(60): _tick("Idle",Vector2.ZERO)
	_check(skin.garment_angles.length()<0.002 and skin.reference_front_angles.length()<0.002,"rest panels settle at the approved drawing")
	for frame in range(120): _tick("Run_New1",Vector2(114,0))
	_check(skin.approved_cross_rear_load>0.8 and skin.approved_cross_front_load>0.8,"both real panels maintain trailing load in steady running")
	_check((skin.garment_angles-skin.reference_front_angles).length()>0.005,"front and rear retain a restrained layer difference through the connected surface")
	var order_changes: int = skin.approved_cross_order_changes
	var rear_before: Vector3 = skin.garment_angles
	for frame in range(3): _tick("Idle",Vector2.ZERO)
	_check(skin.garment_angles.length()>0.05,"rear panel retains visible inertia after stopping")
	_check(absf(skin.approved_cross_rear_load)>absf(skin.approved_cross_front_load),"rear load releases later than sheltered front facing")
	for frame in range(240): _tick("Idle",Vector2.ZERO)
	_check(skin.garment_angles.length()<0.01 and skin.reference_front_angles.length()<0.01,"both panels return to the original hanging state")
	# Latest direction preserves layered elastic motion without requiring
	# the old conspicuous cross-over or a fixed sign reversal of the tips.
	_check(tip_max-tip_min>0.15,"relative cloth tips respond through startup and braking without compulsory cross-over")
	for frame in range(90): _tick("Run_New1",Vector2(114,0))
	var previous_rear_load: float = skin.approved_cross_rear_load
	_tick("Run_New1",Vector2(-114,0),-1.0)
	_check(previous_rear_load*skin.approved_cross_rear_load<0.0,"turn mirrors stored local cloth load to retain world-side inertia")
	for frame in range(90): _tick("Run_New1",Vector2(-114,0),-1.0)
	_check(skin.approved_cross_rear_load>0.70,"reversed actor eventually takes the new local trailing load")
	var frozen: Dictionary = skin.get_garment_diagnostics().duplicate(true)
	var angles: Vector3 = skin.garment_angles
	var front_angles: Vector3 = skin.reference_front_angles
	for frame in range(12): _tick("Idle",Vector2.ZERO,-1.0,0.0)
	_check(frozen==skin.get_garment_diagnostics() and angles==skin.garment_angles and front_angles==skin.reference_front_angles,"pause freezes every cloth panel and crossing state")
	for record in skin.mesh_records:
		if panel_layers.has(record.name): _check(not record.mesh.visible and record.mesh.z_index==panel_layers[record.name],record.name+" stays hidden without layer swapping")
	var finite := true
	for key in skin.bones:
		finite = finite and skin.bones[key].position.is_finite() and is_finite(skin.bones[key].rotation)
	_check(finite,"all original skeleton transforms remain finite")
	_finish()

func _finish() -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"tip_gap_range":[tip_min,tip_max],"samples":motion_samples,"scope":"One continuous approved artwork surface with independent front/rear controls; startup/steady/stop/turn/pause; hidden legacy split panels stay hidden. Does not constitute final art approval"}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output"))
	FileAccess.open("res://output/approved_outfit_crossing_smoke.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("APPROVED OUTFIT CROSSING ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks; ",JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
