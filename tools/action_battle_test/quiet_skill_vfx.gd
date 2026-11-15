extends Node2D

## Presentation only. Reads resolved attacks / cast progress; never applies effects,
## changes a resource, consumes gameplay random numbers or advances a skill timer.
var battle: Node2D
var force_shader: Shader
var stamps: Array[Sprite2D] = []
var guard_stamps: Array[Sprite2D] = []
var ink_layer: Node2D
var _seen_strikes: Array[Dictionary] = []
var _seen_guards: Array[Dictionary] = []
var completions: Array[Dictionary] = []
const BLOOD := Color("9b4c43")
const BLOOD_LIGHT := Color("d8a08b")
const MILK := Color("f6f1df")
const FORCE := Color("d9cba8")
const FORCE_DARK := Color("8f7d5b")
const COMPLETION_SECONDS := 0.48
const BLOOD_FINISH_SECONDS := 0.64
var rear_flow: Node2D
var front_flow: Node2D
var hand_cloud: Node2D
var blood_rear_mist: Node2D
var blood_front_mist: Node2D
var mist_texture: GradientTexture2D
var _draw_target: Node2D
var _strip_pools: Dictionary = {}
var _strip_cursors: Dictionary = {}


func setup(owner_battle: Node2D, shader_path: String) -> void:
	battle = owner_battle
	force_shader = load(shader_path)
	var ribbon_ink := ShaderMaterial.new()
	ribbon_ink.shader = load(shader_path.get_base_dir().path_join("quiet_ribbon.gdshader"))
	material = ribbon_ink
	z_index = 6
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	ink_layer = preload("ink_particle_layer.gd").new()
	ink_layer.name = "SharedAttackInk"
	ink_layer.max_particles = 128
	ink_layer.z_index = 1
	add_child(ink_layer)
	rear_flow = Node2D.new()
	rear_flow.material = ribbon_ink
	rear_flow.draw.connect(_draw_self_layer.bind(false))
	add_child(rear_flow)
	hand_cloud = Node2D.new()
	hand_cloud.z_index = 132
	var cloud_ink := ShaderMaterial.new()
	cloud_ink.shader = load(shader_path.get_base_dir().path_join("hand_qi_mist.gdshader"))
	hand_cloud.material = cloud_ink
	hand_cloud.draw.connect(_draw_hand_cloud)
	add_child(hand_cloud)
	front_flow = Node2D.new()
	front_flow.z_index = 140
	front_flow.material = ribbon_ink
	front_flow.draw.connect(_draw_self_layer.bind(true))
	add_child(front_flow)
	blood_rear_mist = Node2D.new()
	blood_rear_mist.draw.connect(_draw_blood_mist.bind(false))
	add_child(blood_rear_mist)
	blood_front_mist = Node2D.new()
	blood_front_mist.z_index = 136
	blood_front_mist.draw.connect(_draw_blood_mist.bind(true))
	add_child(blood_front_mist)
	var gradient := Gradient.new()
	gradient.set_color(0, Color(MILK, 0.72))
	gradient.set_color(1, Color(MILK, 0.0))
	gradient.add_point(0.38, Color(MILK, 0.48))
	mist_texture = GradientTexture2D.new()
	mist_texture.gradient = gradient
	mist_texture.width = 96
	mist_texture.height = 96
	mist_texture.fill = GradientTexture2D.FILL_RADIAL
	mist_texture.fill_from = Vector2(0.5, 0.5)
	mist_texture.fill_to = Vector2(1.0, 0.5)


func advance(delta: float) -> void:
	if ink_layer != null:
		ink_layer.advance(delta)
	for i in range(completions.size() - 1, -1, -1):
		completions[i].age = float(completions[i].age) + delta
		var duration := BLOOD_FINISH_SECONDS if str(completions[i].kind) == "催血" else COMPLETION_SECONDS
		if float(completions[i].age) >= duration:
			completions.remove_at(i)


func complete(kind: String, gained: int, cycle: bool = false) -> void:
	if gained <= 0:
		return
	# Self effects always use the current body / hand anchor, including the tail.
	# Only attack strikes keep their independent release-position snapshots.
	completions.append({"kind": kind, "age": 0.0, "cycle": cycle})


func clear_visuals() -> void:
	completions.clear()
	for stamp in stamps:
		stamp.visible = false
	for stamp in guard_stamps:
		stamp.visible = false
	_seen_strikes.clear()
	_seen_guards.clear()
	if ink_layer != null:
		ink_layer.clear_visuals()
	queue_redraw()
	_queue_self_redraw()


func force_opacity(age: float, heavy: bool) -> float:
	# Fast readable contact, then a soft echo instead of a stack of bright hands.
	var duration: float = battle.HEAVY_FX_SECONDS if heavy else battle.STRIKE_FX_SECONDS
	var settle := 0.20 if heavy else 0.14
	var strength := lerpf(0.93, 0.24, smoothstep(settle, settle + 0.16, age))
	return strength * (1.0 - smoothstep(duration * 0.55, duration, age))


func sync_visuals() -> void:
	if battle == null:
		return
	for i in range(_seen_strikes.size() - 1, -1, -1):
		if not battle.strike_fx.has(_seen_strikes[i]):
			_seen_strikes.remove_at(i)
	while stamps.size() < battle.strike_fx.size():
		var stamp := Sprite2D.new()
		var ink := ShaderMaterial.new()
		ink.shader = force_shader
		stamp.material = ink
		stamp.centered = false
		stamp.region_enabled = true
		stamp.region_filter_clip_enabled = true
		add_child(stamp)
		stamps.append(stamp)
	for i in range(stamps.size()):
		var stamp := stamps[i]
		stamp.visible = i < battle.strike_fx.size() and battle.hp > 0 and battle.enemy_hp > 0
		if not stamp.visible:
			continue
		var effect: Dictionary = battle.strike_fx[i]
		var kind := str(effect.kind)
		var texture: Texture2D = battle.strike_fx_textures.get(str(effect.get("visual_key", kind)))
		if texture == null:
			stamp.visible = false
			continue
		var age := float(effect.age)
		var heavy := kind == "痛击"
		var duration: float = battle.strike_fx_duration(kind)
		var progress := clampf(age / duration, 0.0, 1.0)
		var frame: Rect2 = battle.strike_fx_frame_rect(texture, battle.strike_fx_frame_index(kind, age))
		var destination: Rect2 = battle.strike_fx_destination_rect(texture, float(effect.reach), progress)
		var visual_height := float(effect.get("visual_height", 1.0))
		destination.position.y *= visual_height
		destination.size.y *= visual_height
		var direction: Vector2 = effect.dir
		var mirrored := direction.x < 0.0
		var angle := direction.angle() + (PI if mirrored else 0.0)
		var basis := Transform2D(angle, Vector2.ZERO).scaled_local(Vector2(-1.0 if mirrored else 1.0, 1.0))
		var hand_height := float(effect.get("attack_height", 58.0 if battle.original_wanderer_enabled else 32.0))
		stamp.texture = texture
		stamp.region_rect = frame
		stamp.transform = Transform2D(basis.x * destination.size.x / frame.size.x,
			basis.y * destination.size.y / frame.size.y,
			effect.origin + Vector2(0, -hand_height) + basis.basis_xform(destination.position))
		var opacity := force_opacity(age, heavy)
		if bool(effect.get("hit",false)) and battle.get("impact_feedback")!=null:
			# Give the initial contact a clear peak, then reveal the victim's recoil.
			opacity *= lerpf(1.0,0.40,smoothstep(0.025,0.085,age))
		stamp.material.set_shader_parameter("opacity", opacity)
		stamp.material.set_shader_parameter("weight", 1.0 if heavy else 0.0)
		stamp.material.set_shader_parameter("dissolve", smoothstep(duration * 0.40, duration, age))
		stamp.material.set_shader_parameter("effect_age", age)
		if not _seen_strikes.has(effect):
			_seen_strikes.append(effect)
			_emit_strike_ink(effect)
	sync_guard_visuals()
	queue_redraw()
	_queue_self_redraw()


func actor_hand_height() -> float:
	return maxf(0.0, float(battle.player.y) - _hand_anchor().y)


func sync_guard_visuals() -> void:
	if battle == null:
		return
	for i in range(_seen_guards.size() - 1, -1, -1):
		if not battle.guard_fx.has(_seen_guards[i]):
			_seen_guards.remove_at(i)
	while guard_stamps.size() < battle.guard_fx.size():
		var stamp := Sprite2D.new()
		stamp.centered = false
		stamp.region_enabled = true
		stamp.region_filter_clip_enabled = true
		var ink := ShaderMaterial.new()
		ink.shader = force_shader
		stamp.material = ink
		add_child(stamp)
		guard_stamps.append(stamp)
	for i in range(guard_stamps.size()):
		var stamp := guard_stamps[i]
		stamp.visible = i < battle.guard_fx.size() and battle.hp > 0 and battle.enemy_hp > 0
		if not stamp.visible:
			continue
		var texture: Texture2D = battle.strike_fx_textures.get("架招")
		if texture == null:
			stamp.visible = false
			continue
		var effect: Dictionary = battle.guard_fx[i]
		var success := bool(effect.success)
		var age := float(effect.age)
		var duration: float = battle.GUARD_SUCCESS_FX_SECONDS if success else battle.GUARD_FX_SECONDS
		var progress := clampf(age / duration, 0.0, 1.0)
		var frame: Rect2 = battle.strike_fx_frame_rect(texture, battle.guard_fx_frame_index(success, progress))
		var width := 100.0 if battle.original_wanderer_enabled else 82.0
		var destination := Rect2(-width * 0.30, -width * frame.size.y / frame.size.x * 0.5,
			width, width * frame.size.y / frame.size.x)
		var direction: Vector2 = effect.dir
		var mirrored := direction.x < 0.0
		var angle := direction.angle() + (PI if mirrored else 0.0)
		var basis := Transform2D(angle, Vector2.ZERO).scaled_local(Vector2(-1.0 if mirrored else 1.0, 1.0))
		var origin: Vector2 = effect.get("origin", battle.player)
		var height := float(effect.get("attack_height", actor_hand_height()))
		stamp.texture = texture
		stamp.region_rect = frame
		stamp.transform = Transform2D(basis.x * destination.size.x / frame.size.x,
			basis.y * destination.size.y / frame.size.y,
			origin - Vector2(0, height) + basis.basis_xform(destination.position))
		var opacity := 0.86 * (1.0 - smoothstep(0.18, 1.0, progress)) if success else (0.72 if age <= 0.20 else 0.18 * (1.0 - smoothstep(0.20, duration, age)))
		stamp.material.set_shader_parameter("opacity", opacity)
		stamp.material.set_shader_parameter("weight", 0.58 if success else 0.32)
		stamp.material.set_shader_parameter("dissolve", smoothstep(0.42, 1.0, progress))
		stamp.material.set_shader_parameter("effect_age", age)
		if not _seen_guards.has(effect):
			_seen_guards.append(effect)
			if success and age <= 0.20:
				_emit_guard_ink(origin - Vector2(0, height), direction)


func _emit_strike_ink(effect: Dictionary) -> void:
	var age := float(effect.age)
	var heavy := str(effect.kind) == "痛击"
	if age > 0.26 or ink_layer == null:
		return
	var direction: Vector2 = effect.dir
	var origin: Vector2 = effect.origin - Vector2(0, float(effect.get("attack_height", actor_hand_height())))
	var tint := Color("d1bd92") if heavy else Color("c6c6b8")
	ink_layer.trail_emit(battle.to_global(origin + direction * float(effect.reach) * 0.36), direction, "mist", {
		"count": 2 if heavy else 1, "projection": 1.0, "color": tint,
		"size": 13.0 if heavy else 9.0, "life": 0.36 if heavy else 0.25,
		"speed": 5.0, "scatter": 2.0, "height": 0.0, "rise_speed": 3.0,
		"grow": 0.22, "opacity": 0.13 if heavy else 0.10})
	if not bool(effect.get("hit", false)) or age > (0.20 if heavy else 0.14):
		return
	# Resolved-contact feedback owns this layer, including blood-qi coalescing.
	if battle.get("impact_feedback") != null: return
	for point in effect.get("impact_positions", [effect.impact]):
		var contact: Vector2 = Vector2(point) - Vector2(0, float(effect.get("attack_height", actor_hand_height()))) - direction * 9.0
		ink_layer.burst(battle.to_global(contact), direction, "fleck", {
			"count": 4 if heavy else 2, "projection": 1.0, "color": tint,
			"size": 4.5 if heavy else 3.2, "life": 0.22 if heavy else 0.16,
			"speed": 15.0 if heavy else 10.0, "scatter": 2.0,
			"height": 0.0, "rise_speed": 4.0, "gravity": 14.0,
			"opacity": 0.52 if heavy else 0.40})


func _emit_guard_ink(origin: Vector2, direction: Vector2) -> void:
	if battle.get("impact_feedback") != null: return
	if ink_layer == null:
		return
	var source := battle.to_global(origin + direction * 14.0)
	ink_layer.burst(source, direction, "fleck", {"count": 4, "projection": 1.0,
		"color": Color("d8cca9"), "size": 4.0, "life": 0.20, "speed": 13.0,
		"scatter": 2.0, "height": 0.0, "rise_speed": 5.0, "gravity": 18.0, "opacity": 0.46})
	ink_layer.burst(source, direction, "mist", {"count": 1, "projection": 1.0,
		"color": Color("c9c5ad"), "size": 12.0, "life": 0.30, "speed": 3.0,
		"scatter": 2.0, "height": 0.0, "rise_speed": 2.0, "opacity": 0.10})


func _queue_self_redraw() -> void:
	if rear_flow != null: rear_flow.queue_redraw()
	if front_flow != null: front_flow.queue_redraw()
	if hand_cloud != null: hand_cloud.queue_redraw()
	if blood_rear_mist != null: blood_rear_mist.queue_redraw()
	if blood_front_mist != null: blood_front_mist.queue_redraw()


func _draw() -> void:
	if battle == null:
		return
	if battle.hp <= 0 or battle.enemy_hp <= 0:
		return
	_begin_strips(self)
	_draw_anticipation()
	for effect in battle.strike_fx:
		_draw_wake(effect)
		if bool(effect.get("hit", false)) and battle.get("impact_feedback") == null:
			for point in effect.get("impact_positions", [effect.impact]):
				var contact: Dictionary = effect.duplicate()
				contact.impact = point
				_draw_contact(contact)
	draw_set_transform(Vector2.ZERO)


func _draw_anticipation() -> void:
	if battle.pending_attack != "痛击" or battle.hp <= 0 or battle.enemy_hp <= 0:
		return
	var delay := maxf(float(battle.SKILL_RULES["痛击"].delay), 0.001)
	var p := 1.0 - clampf(float(battle.release_timer) / delay, 0.0, 1.0)
	var height := float(battle.attack_plane_height()) if battle.has_method("attack_plane_height") else (58.0 if battle.original_wanderer_enabled else 32.0)
	var hand := Vector2(0, -height)
	draw_set_transform(battle.player + hand, battle.attack_direction.angle())
	for side in [-1.0, 1.0]:
		_ribbon(Vector2(7, side * 14), Vector2(13, side * 5), Vector2(24, side * 2), 2.0, Color(FORCE_DARK, p * 0.34))
	draw_set_transform(Vector2.ZERO)


func _draw_wake(effect: Dictionary) -> void:
	var age := float(effect.age)
	if age > 0.26:
		return
	var heavy := str(effect.kind) == "痛击"
	var fade := (1.0 - smoothstep(0.04, 0.26, age)) * (0.52 if heavy else 0.34)
	var hand := Vector2(0, -float(effect.get("attack_height", 58.0 if battle.original_wanderer_enabled else 32.0)))
	var direction: Vector2 = effect.dir
	draw_set_transform(effect.origin + hand, direction.angle())
	var reach := float(effect.reach)
	var advance_by := minf(age / 0.09, 1.0) * 7.0
	for side in [-1.0, 1.0]:
		_ribbon(Vector2(5 + advance_by, side * 5), Vector2(reach * 0.38, side * (17 if heavy else 11)),
			Vector2(reach * 0.70, side * 9), 4.6 if heavy else 3.1, Color(FORCE, fade))
	draw_set_transform(Vector2.ZERO)


func _draw_contact(effect: Dictionary) -> void:
	var age := float(effect.age)
	var heavy := str(effect.kind) == "痛击"
	var length_seconds := 0.20 if heavy else 0.14
	if age > length_seconds:
		return
	var p := clampf(age / length_seconds, 0.0, 1.0)
	var direction: Vector2 = effect.dir
	var origin: Vector2 = effect.impact + Vector2(0, -float(effect.get("attack_height", 40.0))) - direction * 14.0
	var radius := (17.0 if heavy else 11.0) * (0.6 + p * 0.65)
	var tint := Color(FORCE, 0.82 * (1.0 - p))
	draw_set_transform(origin, direction.angle())
	# Open, short brush accents, never a radial spell circle.
	for i in range(5 if heavy else 3):
		var angle := -1.5 + i * (0.75 if heavy else 1.5)
		var ray := Vector2.from_angle(angle)
		_ribbon(ray * radius * 0.28, ray * radius * 0.70 + Vector2(0, 2), ray * radius, 4.2 if heavy else 2.8, tint)
	draw_set_transform(Vector2.ZERO)


func _hand_anchor() -> Vector2:
	if battle.original_wanderer != null:
		var skin = battle.original_wanderer.wanderer_skin
		if skin != null:
			var bones = skin.get("bones")
			if bones is Dictionary and bones.has("near_hand"):
				var hand: Node2D = bones["near_hand"]
				return battle.to_local(hand.global_position)
	return battle.player + Vector2(12 * battle.facing, -58 if battle.original_wanderer_enabled else -32)


func self_effect_anchor(kind: String) -> Vector2:
	if kind != "催血": return _hand_anchor()
	return battle.body_effect_origin() if battle.has_method("body_effect_origin") else battle.player


func _draw_self_layer(front: bool) -> void:
	var target := front_flow if front else rear_flow
	_begin_strips(target)
	if battle == null or battle.hp <= 0 or battle.enemy_hp <= 0: return
	if battle.pending_self_skill == "催血":
		var p: float = battle.self_cast_progress()
		_draw_blood_flow(p, self_effect_anchor("催血"), front, smoothstep(0.0, 0.12, p))
	elif battle.pending_self_skill == "聚炁" and front:
		_draw_qi_threads(battle.self_cast_progress(), _hand_anchor(), 1.0)
	for effect in completions:
		var age := float(effect.age) / (BLOOD_FINISH_SECONDS if str(effect.kind) == "催血" else COMPLETION_SECONDS)
		var fade := 1.0 - smoothstep(0.05, 1.0, age)
		if str(effect.kind) == "催血":
			_draw_blood_flow(1.0, self_effect_anchor("催血"), front, 1.0 - smoothstep(0.0, 0.32, age))
			if front: _draw_blood_finish(age, self_effect_anchor("催血"))
		elif front:
			_draw_qi_threads(1.0 + age * 0.3, self_effect_anchor("聚炁"), fade, age)
	target.draw_set_transform(Vector2.ZERO)


func blood_head(p: float, strand: int = 0) -> Vector3:
	var travel := maxf(0.0, p - strand * 0.21)
	var angle := -1.7 + travel * TAU * 1.28 - strand * 1.3
	return Vector3(sin(angle) * 29.0, -8.0 - travel * 59.0 + cos(angle) * 8.0 + strand * 7.0, cos(angle))


func _draw_blood_flow(p: float, origin: Vector2, front: bool, fade: float) -> void:
	var height := 1.0 if battle.original_wanderer_enabled else 0.73
	_draw_target.draw_set_transform(origin, 0.0, Vector2(1, height))
	# Short asymmetric tails and a visible leading puff let the eye follow one
	# strand around the figure; no level modulo or repeating full hoops.
	for strand in range(2):
		var travel := maxf(0.0, p - strand * 0.21)
		var head_angle := -1.7 + travel * TAU * 1.28 - strand * 1.3
		var head_y := -8.0 - travel * 59.0 + strand * 7.0
		var points := PackedVector2Array()
		var depth := PackedFloat32Array()
		for sample in range(41):
			var t := sample / 40.0
			var angle := head_angle - (1.0 - t) * PI * 0.98
			var radius := 29.0 + sin(t * PI) * 2.0
			points.append(Vector2(sin(angle) * radius, head_y + (1.0 - t) * 25.0 + cos(angle) * 8.0))
			var near_amount := smoothstep(-0.14, 0.14, cos(angle))
			depth.append((near_amount if front else 1.0 - near_amount) * (0.30 + t * 0.70))
		var alpha := fade * smoothstep(0.0, 0.12, travel) * (0.90 if strand == 0 else 0.43)
		_ribbon_path(points, 17.0 if strand == 0 else 8.0, Color(BLOOD, alpha), depth)
		_ribbon_path(points, 2.6, Color(BLOOD_LIGHT, alpha * 0.72), depth)


func _draw_blood_finish(p: float, origin: Vector2) -> void:
	var height := 1.0 if battle.original_wanderer_enabled else 0.73
	_draw_target.draw_set_transform(origin, 0.0, Vector2(1, height))
	var fade := smoothstep(0.0, 0.10, p) * (1.0 - smoothstep(0.40, 1.0, p))
	for side in [-1.0, 1.0]:
		var points := PackedVector2Array()
		for sample in range(25):
			var t := sample / 24.0
			var lift := p * 37.0
			points.append(Vector2(float(side) * (12.0 - t * 7.0) + sin(t * 5.0 + p * 4.0 + float(side)) * 3.0,
				-37.0 - lift - t * 34.0))
		_ribbon_path(points, 13.0 * (1.0 - p * 0.5), Color(BLOOD, fade * 0.82))
		_ribbon_path(points, 2.8, Color(BLOOD_LIGHT, fade * 0.58))


func _draw_blood_mist(front: bool) -> void:
	var target := blood_front_mist if front else blood_rear_mist
	if battle == null or battle.hp <= 0 or battle.enemy_hp <= 0: return
	var height := 1.0 if battle.original_wanderer_enabled else 0.73
	if battle.pending_self_skill == "催血":
		var p: float = battle.self_cast_progress()
		target.draw_set_transform(battle.player, 0, Vector2(1, height))
		for strand in range(2):
			var head := blood_head(p, strand)
			var near_amount := smoothstep(-0.15, 0.15, head.z)
			var alpha := (near_amount if front else 1.0 - near_amount) * smoothstep(0.0, 0.15, p) * (0.65 if strand == 0 else 0.30)
			target.draw_texture_rect(mist_texture, Rect2(Vector2(head.x, head.y) - Vector2(8, 7), Vector2(16, 14)), false, Color(BLOOD, alpha))
	if front:
		for effect in completions:
			if str(effect.kind) != "催血": continue
			var p := float(effect.age) / BLOOD_FINISH_SECONDS
			var fade := smoothstep(0.0, 0.15, p) * (1.0 - smoothstep(0.45, 1.0, p))
			target.draw_set_transform(self_effect_anchor("催血"), 0, Vector2(1, height))
			for side in [-1.0, 1.0]:
				var center := Vector2(float(side) * (9 - p * 4), -54 - p * 43)
				target.draw_texture_rect(mist_texture, Rect2(center - Vector2(10, 17), Vector2(20, 34)), false, Color(BLOOD, fade * 0.48))
	target.draw_set_transform(Vector2.ZERO)


func _draw_qi_threads(p: float, hand: Vector2, fade: float, collapse := 0.0) -> void:
	_draw_target.draw_set_transform(hand)
	var appear := smoothstep(0.0, 0.16, p) * fade
	for i in range(3):
		var angle := i * 2.24 - 0.6
		var start := Vector2(cos(angle) * 28, sin(angle) * 18) * (1 - collapse * 0.7)
		var control := start.rotated(0.85) * 0.78
		_ribbon(start, control, Vector2.ZERO, 2.5, Color(MILK, appear * 0.19))
		var flow := fposmod(p * 1.4 + i * 0.31, 1.0)
		var points := PackedVector2Array()
		for sample in range(21):
			var t := clampf(flow - 0.42 + sample / 20.0 * 0.42, 0.0, 1.0)
			points.append((1-t)*(1-t)*start + 2*(1-t)*t*control)
		_ribbon_path(points, 7.0, Color(MILK, appear * sin(flow * PI) * 0.88))


func _draw_hand_cloud() -> void:
	if battle == null or battle.hp <= 0 or battle.enemy_hp <= 0: return
	if battle.pending_self_skill == "聚炁":
		_draw_milk_cloud(_hand_anchor(), battle.self_cast_progress(), 1.0)
	for effect in completions:
		if str(effect.kind) != "聚炁": continue
		var age := float(effect.age) / COMPLETION_SECONDS
		_draw_milk_cloud(self_effect_anchor("聚炁"), 1.0 + age * 0.35, 1.0 - smoothstep(0.0, 1.0, age), age)
	hand_cloud.draw_set_transform(Vector2.ZERO)


func _draw_milk_cloud(hand: Vector2, p: float, fade: float, collapse := 0.0) -> void:
	var appear := smoothstep(0.05, 0.72, p) * fade
	hand_cloud.draw_set_transform(hand)
	hand_cloud.material.set_shader_parameter("phase", p)
	for i in range(4):
		var angle := i * 2.30 + p * 1.8
		var pull := 1.0 - minf(p, 1.0) * 0.74
		var at := Vector2(cos(angle) * 12, sin(angle) * 7) * pull
		var radius := (9.0 + minf(p, 1.0) * 3.0) * (1.0 - collapse * 0.70)
		hand_cloud.draw_texture_rect(mist_texture, Rect2(at - Vector2(radius, radius * 0.64), Vector2(radius * 2.0, radius * 1.28)), false, Color(1, 1, 1, appear * 0.80))


func _begin_strips(target: Node2D) -> void:
	_draw_target = target
	var key := target.get_instance_id()
	if not _strip_pools.has(key): _strip_pools[key] = []
	_strip_cursors[key] = 0


func _ribbon(start: Vector2, control: Vector2, end: Vector2, width: float, tint: Color) -> void:
	var points := PackedVector2Array()
	for step in range(17):
		var t := step / 16.0
		points.append((1.0 - t) * (1.0 - t) * start + 2.0 * (1.0 - t) * t * control + t * t * end)
	_ribbon_path(points, width, tint)


func _ribbon_path(points: PackedVector2Array, width: float, tint: Color, visibility := PackedFloat32Array()) -> void:
	if width < 0.05 or tint.a < 0.005: return
	var vertices := PackedVector3Array()
	var uvs := PackedVector2Array()
	var colors := PackedColorArray()
	var indices := PackedInt32Array()
	for i in range(points.size()):
		var t := float(i) / (points.size() - 1)
		var tangent := (points[mini(i + 1, points.size() - 1)] - points[maxi(0, i - 1)]).normalized()
		var normal := Vector2(-tangent.y, tangent.x)
		var radius := pow(maxf(0.0, sin(PI * t)), 0.85) * width * 0.5
		var alpha := visibility[i] if not visibility.is_empty() else 1.0
		for side in [-1.0, 1.0]:
			var point: Vector2 = points[i] + normal * radius * float(side)
			vertices.append(Vector3(point.x, point.y, 0))
			uvs.append(Vector2(t, (side + 1.0) * 0.5))
			colors.append(Color(tint, tint.a * alpha))
		if i < points.size() - 1:
			indices.append_array(PackedInt32Array([i * 2, i * 2 + 1, i * 2 + 2, i * 2 + 1, i * 2 + 3, i * 2 + 2]))
	# Explicit triangles stay valid even for thick, tightly curved wisps.
	var key := _draw_target.get_instance_id()
	var cursor := int(_strip_cursors[key])
	var pool: Array = _strip_pools[key]
	if cursor == pool.size(): pool.append(ArrayMesh.new())
	var mesh: ArrayMesh = pool[cursor]
	mesh.clear_surfaces()
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = indices
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	_draw_target.draw_mesh(mesh, null)
	_strip_cursors[key] = cursor + 1
