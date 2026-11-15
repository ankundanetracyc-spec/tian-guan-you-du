extends Node2D
## Presentation only: a precise expanding sector with a world-anchored ink wake.
## Timing is an adjustable art sample, never a source for combat speed / damage.
## Every band is native geometry. No image sheet determines the sector outline.

signal finished

const INK_SHADER := "res://shaders/blood_wave_ink.gdshader"
const InkParticles = preload("res://tools/action_battle_test/ink_particle_layer.gd")
const ANGLE_STEPS := 96
const WAKE_BANDS := 35
@export_range(10, 180, 1) var fan_degrees := 120.0
@export_range(0.2, 1.0, 0.01) var y_projection := 1.0
const ATTACK_RANGE = preload("attack_range_rules.gd")
@export var max_radius := 220.0 * ATTACK_RANGE.MULTIPLIER
@export var visual_scale := 1.0
@export var crest_lift := 0.0
@export var particles_enabled := true
@export var expansion_seconds := 0.78
@export var crest_seconds := 1.03
@export var wake_linger := 0.18
@export var wake_fade := 0.80
@export var auto_advance := true
@export var free_on_finish := false

var age := 0.0
var playing := false
var release_origin := Vector2.ZERO
var release_direction := Vector2.RIGHT
var release_height := 0.0
var _bands: Array[MeshInstance2D] = []
var _shader: Shader
var _ink: Node2D
var _next_particle_tick := 0.06


func _ready() -> void:
	set_process(false)
	_prepare_bands()


func _prepare_bands() -> void:
	if not _bands.is_empty():
		return
	if ResourceLoader.exists(INK_SHADER):
		_shader = load(INK_SHADER)
	for i in range(WAKE_BANDS + 2):
		var band := MeshInstance2D.new()
		band.name = "Wake%d" % i if i < WAKE_BANDS else "Crest%d" % (i - WAKE_BANDS)
		band.mesh = ArrayMesh.new()
		if _shader != null:
			var ink := ShaderMaterial.new()
			ink.shader = _shader
			band.material = ink
		band.visible = false
		add_child(band)
		_bands.append(band)


static func hand_plane_height(fighter: Node2D) -> float:
	if fighter.has_method("attack_plane_height"):
		return maxf(0.0, float(fighter.call("attack_plane_height")))
	var rig: Node2D = fighter.get("original_wanderer")
	if is_instance_valid(rig):
		var skin = rig.get("wanderer_skin")
		if skin != null:
			var bones = skin.get("bones")
			if bones is Dictionary and bones.has("near_hand"):
				var hand: Node2D = bones["near_hand"]
				var foot: Vector2 = fighter.get("player")
				return maxf(0.0, foot.y - fighter.to_local(hand.global_position).y)
	return 58.0 if bool(fighter.get("original_wanderer_enabled")) else 32.0


func play(world_origin: Vector2, direction: Vector2 = Vector2.RIGHT, plane_height: float = 0.0) -> bool:
	_prepare_bands()
	if particles_enabled and _ink == null:
		_ink = InkParticles.new()
		_ink.max_particles = 144
		_ink.z_index = 1
		add_child(_ink)
	if _ink != null:
		_ink.clear_visuals()
		_ink.emitting = particles_enabled
		_ink.visible = particles_enabled
	release_origin = world_origin
	release_direction = direction.normalized() if direction.length_squared() > 0.0001 else Vector2.RIGHT
	release_height = maxf(0.0, plane_height)
	# Rotate in the ground plane before projecting it. Rotating an already
	# flattened sector would make its depth axis turn with the attack direction.
	top_level = true
	# Height shifts the whole expanding plane, including every retained wake.
	# The ground center remains the actor center; hand X never moves this pivot.
	var plane_origin := world_origin - Vector2(0.0, release_height * visual_scale)
	global_transform = Transform2D(0.0, Vector2.ONE * visual_scale, 0.0, plane_origin)
	age = 0.0
	_next_particle_tick = 0.06
	playing = true
	visible = true
	set_process(auto_advance)
	_update_geometry()
	return true


func _process(delta: float) -> void:
	advance(delta)


func advance(delta: float) -> void:
	if not playing:
		return
	var previous_age := age
	age += maxf(delta, 0.0)
	if particles_enabled and _ink != null:
		_advance_ink(previous_age, age)
	if age >= duration():
		stop()
		finished.emit()
		if free_on_finish:
			queue_free()
	else:
		_update_geometry()


func duration() -> float:
	return maxf(crest_seconds, expansion_seconds + wake_linger + wake_fade)


func stop() -> void:
	playing = false
	visible = false
	set_process(false)
	for band in _bands:
		band.visible = false
	if _ink != null:
		_ink.stop(true)
		_ink.visible = false


func clear_visuals() -> void:
	stop()
	age = 0.0


func _radius(at_age: float) -> float:
	var progress := clampf(at_age / maxf(expansion_seconds, 0.001), 0.0, 1.0)
	return lerpf(6.0 * ATTACK_RANGE.MULTIPLIER, max_radius, 1.0 - pow(1.0 - progress, 1.15))


func _advance_ink(from_age: float, to_age: float) -> void:
	# Fixed emission ticks keep the same sample at different rendering rates.
	var cursor := from_age
	while _next_particle_tick <= minf(to_age, expansion_seconds):
		_ink.advance(maxf(0.0, _next_particle_tick - cursor))
		_emit_ink(_next_particle_tick)
		cursor = _next_particle_tick
		_next_particle_tick += 0.06
	_ink.advance(maxf(0.0, to_age - cursor))


func _emit_ink(at_age: float) -> void:
	var tick := int(round(at_age / 0.06))
	var half_angle := deg_to_rad(fan_degrees * 0.5)
	for index in range(3):
		# Emit inside the fan and behind its front; flecks never define a hit.
		var along := 0.15 + float(index) * 0.35 + sin(float(tick * 3 + index)) * 0.06
		var theta := release_direction.angle() + lerpf(-half_angle, half_angle, along)
		var outward := Vector2.from_angle(theta)
		var radius := maxf(2.0, _radius(at_age) - 11.0)
		var projected := Vector2(outward.x * radius, outward.y * radius * y_projection)
		var source: Vector2 = global_transform * projected
		var settings := {"projection": y_projection, "color": Color("90383c"),
			"scatter": 2.0 * visual_scale, "life": 0.48, "speed": 7.0 * visual_scale,
			"size": 5.0 * visual_scale, "height": crest_lift * 0.35 * visual_scale,
			"rise_speed": 13.0 * visual_scale, "gravity": 35.0 * visual_scale,
			"count": 1, "opacity": 0.44, "spread_degrees": 28.0}
		_ink.burst(source, outward, "fleck", settings)
		if tick % 2 == 0:
			_ink.trail_emit(source, outward, "mist", {"count": 1,
				"projection": y_projection, "color": Color("9b4c43"),
				"size": 17.0 * visual_scale, "life": 0.60, "speed": 1.8 * visual_scale,
				"scatter": 2.0 * visual_scale, "opacity": 0.12,
				"height": 0.0, "rise_speed": 0.0, "grow": 0.10})
		if index == 1 and tick % 3 == 0:
			_ink.burst(source, outward, "mist", {"count": 1,
				"projection": y_projection, "color": Color("a25a51"),
				"size": 27.0 * visual_scale, "life": 0.64, "speed": 3.0 * visual_scale,
				"scatter": 2.0 * visual_scale, "height": 3.0 * visual_scale,
				"rise_speed": 7.0 * visual_scale, "opacity": 0.10})


func _update_geometry() -> void:
	for i in range(WAKE_BANDS):
		var along := float(i + 1) / float(WAKE_BANDS)
		var birth := expansion_seconds * along
		var elapsed := age - birth
		var band := _bands[i]
		band.visible = elapsed >= 0.0 and elapsed < wake_linger + wake_fade
		if not band.visible:
			continue
		var fade := 1.0 - smoothstep(wake_linger, wake_linger + wake_fade, elapsed)
		var appear := smoothstep(0.0, 0.08, elapsed)
		var radius := _radius(birth)
		# Fixed radii retain the swept path; only opacity and fine ink grain age.
		_set_band(band, radius, 32.0, fade * appear * 0.22, float(i) * 0.73, false)
	var crest_opacity := smoothstep(0.0, 0.065, age) * (1.0 - smoothstep(expansion_seconds, crest_seconds, age))
	var radius := _radius(age)
	_set_band(_bands[WAKE_BANDS], radius, 38.0, crest_opacity * 0.30, 3.4, false)
	_set_band(_bands[WAKE_BANDS + 1], radius, 26.0, crest_opacity * 0.96, 3.4, true)


func _set_band(band: MeshInstance2D, radius: float, width: float, opacity: float, seed: float, crest: bool) -> void:
	band.visible = opacity > 0.001
	if not band.visible:
		return
	var vertices := PackedVector3Array()
	var colors := PackedColorArray()
	var uvs := PackedVector2Array()
	var indices := PackedInt32Array()
	var half_angle := deg_to_rad(fan_degrees) * 0.5
	var grain_strength := smoothstep(4.0, 45.0, radius)
	for side in range(2):
		for step in range(ANGLE_STEPS + 1):
			var along := float(step) / float(ANGLE_STEPS)
			var theta := release_direction.angle() + lerpf(-half_angle, half_angle, along)
			var coarse_edge := 4.0 if crest else 1.6
			var fine_edge := 1.8 if crest else 0.9
			var broken_edge := (sin(theta * 13.7 + seed) * coarse_edge + sin(theta * 37.0 + seed * 4.1) * fine_edge) * grain_strength
			var radial_width := width * (0.79 + sin(theta * 8.3 + seed * 0.3) * 0.13)
			var radial := clampf(radius + broken_edge + (float(side) - 0.5) * radial_width, 0.6, max_radius)
			var lift := crest_lift * float(side) * sin(PI * along) if crest else 0.0
			vertices.append(Vector3(cos(theta) * radial, sin(theta) * radial * y_projection - lift, 0))
			uvs.append(Vector2(along, float(side)))
			var tip_fade := smoothstep(0.0, 0.035, along) * (1.0 - smoothstep(0.965, 1.0, along))
			var color := Color("8f343d") if crest else Color("9b4c43")
			color.a = opacity * tip_fade
			colors.append(color)
	for step in range(ANGLE_STEPS):
		var inner := step
		var outer := ANGLE_STEPS + 1 + step
		indices.append_array(PackedInt32Array([inner, inner + 1, outer, inner + 1, outer + 1, outer]))
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := band.mesh as ArrayMesh
	mesh.clear_surfaces()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	if band.material is ShaderMaterial:
		band.material.set_shader_parameter("phase", age)
		band.material.set_shader_parameter("band_seed", seed)
