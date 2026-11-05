extends Node2D

## Visual feedback only. No audio, gameplay RNG, pose sampling or time scaling.
const BODY_SHADER = preload("combat_impact_body.gdshader")
const ContactMark = preload("combat_contact_mark.gd")
const MAX_MARKS := 16
const MAX_HISTORY := 128
var battle: Node2D
var actors: Dictionary = {}
var pending: Array[Dictionary] = []
var marks: Array[Node2D] = []
var paused := false
var clock := 0.0
var camera_age := 1.0
var camera_duration := 0.16
var camera_strength := 0.0
var camera_direction := Vector2.RIGHT
var _history: Array[Dictionary] = []
var committed_hits := 0
var committed_guards := 0
var committed_evades := 0
var committed_groups := 0
var _batch := 0

func setup(owner_battle: Node2D) -> void:
	battle = owner_battle
	name = "CombatImpactFeedback"
	process_priority = 100

func _process(delta: float) -> void:
	advance(minf(delta,0.05), battle != null and battle.combat_paused())

func register_actor(fighter: Node2D) -> void:
	if fighter == null or actors.has(fighter.get_instance_id()): return
	var rig: Node2D = fighter.get("original_wanderer")
	if rig == null: return
	var skin = rig.get("wanderer_skin")
	if skin == null: return
	var body_meshes = skin.get("meshes")
	if not body_meshes is Array: return
	var material := ShaderMaterial.new()
	material.shader = BODY_SHADER
	var meshes: Array[Polygon2D] = []
	for mesh in body_meshes:
		# A custom material may carry its own alpha/lighting contract; leave it intact.
		if mesh is Polygon2D and mesh.material == null and not mesh.use_parent_material:
			mesh.material = material
			meshes.append(mesh)
	if meshes.is_empty(): return
	actors[fighter.get_instance_id()] = {"fighter":weakref(fighter), "rig":weakref(rig), "meshes":meshes,
		"material":material, "age":1.0, "duration":0.34, "attack":0.022, "hold":0.045,
		"priority":0, "strength":0.0, "start":_neutral_pose(), "peak":_neutral_pose()}
	_apply_actor(actors[fighter.get_instance_id()])

func _is_heavy(source: Node2D, skill: String, segment: String) -> bool:
	if skill == "痛击" or segment == "血浪": return true
	if source != null:
		var combo = source.get("fist_combo")
		if combo != null and combo.active and combo.stage == 3: return true
	return false

func report(source: Node2D, target: Node2D, result: Dictionary, skill: String, segment := "本体", height := -1.0) -> void:
	if target == null: return
	var origin: Vector2 = target.to_global(target.player)
	var from: Vector2 = source.to_global(source.player) if source != null else origin + Vector2(1,0)
	var direction := (origin-from).normalized()
	if direction.is_zero_approx(): direction = Vector2.RIGHT
	if height < 0.0:
		height = float(source.attack_plane_height()) if source != null and source.has_method("attack_plane_height") else 58.0
	report_world(source,target,result,skill,segment,origin-Vector2(0,height)-direction*6.0,direction,origin.y)

func report_world(source: Node2D, target: Node2D, result: Dictionary, skill: String, segment: String,
	point: Vector2, direction: Vector2, depth: float) -> void:
	if bool(result.get("inactive", false)): return
	var evaded := bool(result.get("evaded", false))
	var damage := maxi(0,int(result.get("damage",0))) if not evaded else 0
	var blocked := maxi(0,int(result.get("blocked",0))) if not evaded else 0
	# An active guard or a geometric overlap alone does not constitute a contact.
	if not evaded and damage == 0 and blocked == 0: return
	pending.append({"source":weakref(source) if source != null else null,
		"target":weakref(target) if target != null else null,
		"source_id":source.get_instance_id() if source != null else 0,
		"target_id":target.get_instance_id() if target != null else -1,
		"evaded":evaded, "damage":damage, "blocked":blocked,
		"critical":bool(result.get("critical",false)), "heavy":_is_heavy(source,skill,segment),
		"blood":segment in ["血气","血浪"], "segments":[segment], "skill":skill,
		"point":to_local(point), "direction":direction.normalized(), "depth":depth})

func advance(delta: float, is_paused := false) -> void:
	paused = is_paused
	if not paused:
		clock += maxf(delta,0.0)
		camera_age += maxf(delta,0.0)
		for actor in actors.values(): actor.age = float(actor.age)+maxf(delta,0.0)
		for i in range(marks.size()-1,-1,-1):
			if not is_instance_valid(marks[i]) or not marks[i].advance(delta):
				if is_instance_valid(marks[i]): marks[i].queue_free()
				marks.remove_at(i)
		commit_pending()
	for key in actors.keys():
		var actor: Dictionary = actors[key]
		if actor.fighter.get_ref() == null or actor.rig.get_ref() == null:
			actors.erase(key)
		else: _apply_actor(actor)
	_apply_camera()

func commit_pending() -> void:
	if paused: return
	_batch += 1
	var groups: Dictionary = {}
	for event in pending:
		var key := "%d:%d" % [event.source_id,event.target_id]
		if not groups.has(key): groups[key] = event.duplicate(true)
		else:
			var group: Dictionary = groups[key]
			group.damage = int(group.damage)+int(event.damage)
			group.blocked = int(group.blocked)+int(event.blocked)
			group.evaded = bool(group.evaded) and bool(event.evaded)
			group.heavy = bool(group.heavy) or bool(event.heavy)
			group.blood = bool(group.blood) or bool(event.blood)
			group.critical = bool(group.critical) or bool(event.critical)
			for segment in event.segments:
				if not group.segments.has(segment): group.segments.append(segment)
	pending.clear()
	var ranked: Array = groups.values()
	ranked.sort_custom(func(a: Dictionary,b: Dictionary)->bool: return _priority(a)>_priority(b))
	for event in ranked:
		if bool(event.evaded):
			committed_evades += 1
			_record(event,"evade")
			continue
		committed_groups += 1
		if int(event.damage)>0: committed_hits += 1
		if int(event.blocked)>0: committed_guards += 1
		var family := "guard" if int(event.blocked)>0 else ("heavy" if bool(event.heavy) else "light")
		_record(event,family)
		_add_mark(event)
		var guarded := int(event.blocked)>0
		var target: Node2D = event.target.get_ref() if event.target != null else null
		var source: Node2D = event.source.get_ref() if event.source != null else null
		var profile := "guard" if int(event.damage)<=0 else ("heavy" if bool(event.heavy) else "light")
		_react(target,event.direction,profile,0.70 if guarded and int(event.damage)>0 else 1.0)
		_react(source,-event.direction,"source",1.4 if bool(event.heavy) else 0.8)
		var strength := 0.55 if guarded else (1.65 if bool(event.heavy) else 0.70)
		if strength>=camera_offset().length():
			camera_strength = strength
			camera_direction = event.direction
			camera_age = 0.0
			camera_duration = 0.19 if bool(event.heavy) else 0.14

func _priority(event: Dictionary) -> int:
	return (1000 if not bool(event.evaded) else 0)+(100 if bool(event.heavy) else 0)+(50 if int(event.blocked)>0 else 0)+mini(40,int(event.damage))

func _record(event: Dictionary, family: String) -> void:
	_history.append({"batch":_batch,"time":clock,"source":event.source_id,"target":event.target_id,
		"damage":event.damage,"blocked":event.blocked,"segments":event.segments.duplicate(),
		"family":family,"skill":event.skill,"point":event.point})
	if _history.size()>MAX_HISTORY: _history.pop_front()

func _add_mark(event: Dictionary) -> void:
	if marks.size()>=MAX_MARKS:
		marks.pop_front().queue_free()
	var mark := ContactMark.new()
	add_child(mark)
	mark.configure(event)
	marks.append(mark)

func _neutral_pose() -> Dictionary:
	return {"recoil":Vector2.ZERO,"lean":0.0,"compression":0.0}

func _reaction_pose(actor: Dictionary) -> Dictionary:
	var age: float = actor.age
	var attack: float = actor.attack
	var peak: Dictionary = actor.peak
	if age >= float(actor.duration): return _neutral_pose()
	if age < attack:
		var t := clampf(age/maxf(attack,0.001),0.0,1.0)
		var fast := 1.0-pow(1.0-t,3.0)
		var start: Dictionary = actor.start
		return {"recoil":Vector2(start.recoil).lerp(peak.recoil,fast),
			"lean":lerpf(float(start.lean),float(peak.lean),fast),
			"compression":lerpf(float(start.compression),float(peak.compression),fast)}
	var return_start := attack+float(actor.hold)
	var weight := 1.0-smoothstep(return_start,float(actor.duration),age)
	return {"recoil":Vector2(peak.recoil)*weight,"lean":float(peak.lean)*weight,
		"compression":float(peak.compression)*weight}

func _react(fighter: Node2D, direction: Vector2, profile: String, scale := 1.0) -> void:
	if fighter == null: return
	register_actor(fighter)
	if not actors.has(fighter.get_instance_id()): return
	var actor: Dictionary = actors[fighter.get_instance_id()]
	var priority: int = {"source":1,"guard":2,"light":3,"heavy":4}[profile]
	var current := _reaction_pose(actor)
	var peak := _neutral_pose()
	var attack := 0.022
	var hold := 0.045
	var duration := 0.34
	if profile in ["source","guard"]:
		var amount := (1.7 if profile=="guard" else 1.0)*scale
		peak.recoil = Vector2(direction.x,direction.y*0.45)*amount
		peak.compression = (0.012 if profile=="guard" else 0.004)*scale
		attack = 0.016
		hold = 0.026
		duration = 0.19 if profile=="guard" else 0.17
	else:
		var heavy := profile=="heavy"
		var front_back := absf(direction.y)
		var facing: float = signf(float(fighter.get("facing")))
		if is_zero_approx(facing): facing = 1.0
		# Pitch shortens the visible torso for north/south hits; a small shoulder turn
		# keeps those reactions readable without pretending the actor moved on the ground.
		peak.recoil = Vector2(direction.x*(5.5 if heavy else 3.5),
			direction.y*(3.0 if heavy else 2.0)+front_back*(10.0 if heavy else 6.0))*scale
		peak.lean = (direction.x*(0.23 if heavy else 0.13)+facing*front_back*(0.045 if heavy else 0.025))*scale
		peak.compression = (0.030 if heavy else 0.018)*scale
		if heavy:
			attack = 0.028
			hold = 0.072
			duration = 0.48
	var strength := Vector2(peak.recoil).length()+absf(float(peak.lean))*40.0+float(peak.compression)*80.0
	var active := float(actor.age)<float(actor.duration)
	# Receiving damage wins over the attacker's small counter-recoil. Repeated hits
	# start at the current displayed pose, rather than snapping to a fresh maximum.
	if active and priority<int(actor.priority): return
	if active and priority==int(actor.priority) and float(actor.age)<float(actor.attack)+float(actor.hold) and strength<=float(actor.strength): return
	actor.start = current
	actor.peak = peak
	actor.age = 0.0
	actor.attack = attack
	actor.hold = hold
	actor.duration = duration
	actor.priority = priority
	actor.strength = strength

func reaction_snapshot(fighter: Node2D) -> Dictionary:
	if fighter==null or not actors.has(fighter.get_instance_id()): return {}
	var actor: Dictionary = actors[fighter.get_instance_id()]
	var pose := _reaction_pose(actor)
	pose.merge({"age":actor.age,"duration":actor.duration,"priority":actor.priority,"peak":actor.peak.duplicate(true)})
	return pose

func _apply_actor(actor: Dictionary) -> void:
	var rig: Node2D = actor.rig.get_ref()
	var foot := rig.global_position
	var height := 1.0
	for mesh in actor.meshes:
		if not is_instance_valid(mesh): continue
		for point in mesh.polygon:
			height = maxf(height,foot.y-(mesh.global_transform*point).y)
	var pose := _reaction_pose(actor)
	var material: ShaderMaterial = actor.material
	material.set_shader_parameter("foot_world",foot)
	material.set_shader_parameter("body_height",height)
	material.set_shader_parameter("recoil",pose.recoil)
	material.set_shader_parameter("compression",pose.compression)
	material.set_shader_parameter("lean",pose.lean)

func camera_offset() -> Vector2:
	var t := clampf(camera_age/maxf(camera_duration,0.001),0.0,1.0)
	return camera_direction*camera_strength*cos(t*PI*3.0)*(1.0-t)*(1.0-t)

func _apply_camera() -> void:
	if battle == null: return
	var camera: Camera2D = battle.get("trial_camera")
	if camera != null:
		camera.offset = camera_offset()
		camera.force_update_scroll()

func event_history() -> Array[Dictionary]:
	return _history.duplicate(true)

func debug_snapshot() -> Dictionary:
	var reactions := 0
	for actor in actors.values():
		if float(actor.age)<float(actor.duration): reactions += 1
	return {"pending":pending.size(),"groups":committed_groups,"marks":marks.size(),"actors":actors.size(),
		"active_reactions":reactions,
		"paused":paused,"camera":camera_offset(),"committed_hits":committed_hits,"committed_guards":committed_guards,
		"committed_evades":committed_evades}

func reset() -> void:
	pending.clear()
	for mark in marks:
		if is_instance_valid(mark): mark.queue_free()
	marks.clear()
	for actor in actors.values():
		actor.age = 1.0
		actor.material.set_shader_parameter("recoil",Vector2.ZERO)
		actor.material.set_shader_parameter("compression",0.0)
		actor.material.set_shader_parameter("lean",0.0)
	camera_age = 1.0
	camera_strength = 0.0
	paused = false
	_history.clear()
	committed_hits = 0
	committed_guards = 0
	committed_evades = 0
	committed_groups = 0
	clock = 0.0
	_apply_camera()

func _exit_tree() -> void:
	reset()
	for actor in actors.values():
		for mesh in actor.meshes:
			if is_instance_valid(mesh) and mesh.material == actor.material: mesh.material = null
