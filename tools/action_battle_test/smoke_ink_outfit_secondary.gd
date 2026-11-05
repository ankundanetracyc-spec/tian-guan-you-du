extends Node2D

const OutfitRuntime = preload("ink_outfit_fitting_runtime.gd")
var rig: Node2D
var skin: RefCounted
var clock := 0.0
var checks := 0
var failures: Array[String] = []
var minimum_area_ratio := INF
var maximum_waist_drift := 0.0
var maximum_offset := 0.0
var minimum_safety := 1.0
var frames_checked := 0
var samples := []

func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)
		push_error(message)

func _tick(clip: String, velocity: Vector2, facing: float = 1.0, delta: float = 1.0/60.0) -> void:
	clock += delta
	skin.set_motion_context(delta,velocity,clock,facing)
	rig.set_character_pose(clip,clock,facing)
	var hem = skin.hem_secondary
	var base: PackedVector2Array = skin.current_vertices[hem.body_index]
	var drawn: PackedVector2Array = skin.meshes[hem.body_index].polygon
	for index in range(base.size()):
		var drift := drawn[index].distance_to(base[index])
		maximum_offset = maxf(maximum_offset,drift)
		if hem.depths[index]<=0.0: maximum_waist_drift = maxf(maximum_waist_drift,drift)
	for triangle in skin.mesh_records[hem.body_index].source.triangles:
		var a: int = triangle[0]
		var b: int = triangle[1]
		var c: int = triangle[2]
		var before := (base[b]-base[a]).cross(base[c]-base[a])
		if absf(before)<0.00001: continue
		var after := (drawn[b]-drawn[a]).cross(drawn[c]-drawn[a])
		minimum_area_ratio = minf(minimum_area_ratio,after/before)
	minimum_safety = minf(minimum_safety,hem.safety_scale)
	frames_checked += 1
	if frames_checked%12==0: samples.append({"clock":clock,"clip":clip,"velocity":[velocity.x,velocity.y],"facing":facing,"hem":hem.get_diagnostics()})

func _ready() -> void:
	rig = OutfitRuntime.new()
	add_child(rig)
	_check(rig.build(),"current costume builds for active secondary-motion checks")
	if not rig.loaded:
		_finish()
		return
	skin = rig.wanderer_skin
	var hem = skin.hem_secondary
	_check(skin.hem_secondary_enabled and hem.enabled,"hem secondary layer is enabled in the actual ordinary path")
	_check(skin.mesh_records[hem.body_index].source.uv.size()==41 and skin.mesh_records[hem.body_index].source.triangles.size()==46,"secondary motion keeps the closed 41-vertex/46-triangle body carrier")
	for index in range(30): _tick("Idle",Vector2.ZERO)
	_check(hem.maximum_applied_offset<0.0001,"rest without movement has no artificial independent wind")
	for index in range(3): _tick("Run_New1",Vector2(114,0))
	var start_mid: float = hem.offsets[0].length()/2.0
	var start_tip: float = hem.offsets[2].length()/6.3
	_check(start_mid>start_tip,"middle responds sooner than the longer hem")
	for index in range(90): _tick("Run_New1",Vector2(114,0))
	_check(hem.offsets[2].x<-3.0 and hem.maximum_applied_offset>2.0,"continuous forward speed causes a visible trailing lower hem")
	var steady_y := []
	for index in range(60):
		_tick("Run_New1",Vector2(114,0))
		steady_y.append(hem.offsets[2].y)
	_check(float(steady_y.max())-float(steady_y.min())>0.15,"continuous running retains modest moving folds rather than a frozen offset")
	var old_world: Vector2 = hem.offsets[2]
	_tick("Run_New1",Vector2(-114,0),-1.0)
	_check(hem.offsets[2].x<0.0 and hem.offsets[2].distance_to(old_world)<1.5,"turning preserves delayed world-side trailing response on its first frame")
	for index in range(90): _tick("Run_New1",Vector2(-114,0),-1.0)
	_check(hem.offsets[2].x>3.0,"reversed real velocity eventually trails in the opposite world direction")
	for index in range(60): _tick("BackstepWalk",Vector2(-114,0),1.0)
	_check(hem.offsets[2].x>3.0,"backward movement drives the hem behind actual movement")
	for index in range(3): _tick("Idle",Vector2.ZERO)
	var stop_initial: float = hem.maximum_applied_offset
	_check(stop_initial>0.20,"the hem retains motion immediately after stopping")
	for index in range(180): _tick("Idle",Vector2.ZERO)
	_check(hem.maximum_applied_offset<0.03,"stopped motion decays and returns to the authored hanging pose")
	for index in range(90): _tick("Run_New1",Vector2(114,0))
	var frozen_polygon: PackedVector2Array = skin.meshes[hem.body_index].polygon.duplicate()
	var frozen_state: Dictionary = hem.get_diagnostics().duplicate(true)
	for index in range(12): _tick("Idle",Vector2.ZERO,1.0,0.0)
	_check(frozen_polygon==skin.meshes[hem.body_index].polygon and frozen_state==hem.get_diagnostics(),"pause freezes both rendered robe and secondary state")
	_tick("Step1",Vector2.ZERO)
	_check(hem.maximum_applied_offset==0.0 and skin.meshes[hem.body_index].polygon==skin.current_vertices[hem.body_index],"dedicated step pose takes priority over secondary wind")
	_tick("AttackIdle",Vector2.ZERO)
	_check(hem.maximum_applied_offset==0.0,"dedicated attack pose does not inherit running cloth offsets")
	for index in range(60): _tick("Run_New1",Vector2(114,0))
	clock = 0.0
	_tick("Run_New1",Vector2(114,0))
	_check(hem.maximum_applied_offset<0.75,"clock rewind clears stale hem inertia")
	_check(maximum_waist_drift<0.0001,"added secondary motion leaves the entire waist and torso fixed to the source pose")
	_check(minimum_area_ratio>0.0,"no added body triangle inversion occurs across acceleration, turns, stop, pause and actions")
	_check(minimum_safety>=0.25 and maximum_offset>3.0,"area protection does not suppress the visible motion to satisfy checks")
	_finish()

func _finish() -> void:
	var report := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"frames_checked":frames_checked,"maximum_waist_drift":maximum_waist_drift,"minimum_triangle_area_ratio":minimum_area_ratio,"maximum_applied_offset":maximum_offset,"minimum_safety_scale":minimum_safety,"samples":samples,"model":"lightweight project visual secondary layer; not original DynamicBone"}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://output"))
	var file := FileAccess.open("res://output/ink_outfit_secondary_smoke.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("INK OUTFIT SECONDARY ","PASS" if failures.is_empty() else "FAIL",": ",checks," checks; ",JSON.stringify(failures))
	get_tree().quit(0 if failures.is_empty() else 1)
