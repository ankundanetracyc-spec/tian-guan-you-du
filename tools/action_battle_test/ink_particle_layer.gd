extends Node2D
## Small reusable visual-only ink pool. advance() is the sole particle clock;
## it never reads TIME, changes combat state, or consumes a gameplay RNG.
## Emit origins are world positions. Moving / turning this node cannot drag
## already emitted ink away from its original path.
##
## Profiles: ground (flat ink mark), fleck (raised torn ink), mist (soft haze).
## Override keys: count, color, size, life, speed, drift (Vector2), spread_degrees,
## scatter, projection, height, rise_speed, gravity, spin, aspect, grow, opacity.
## Direction / speed / drift use the unprojected ground plane. projection only
## compresses their world-Y motion; height is drawn vertically above that plane.

const PROFILES := {
	"ground": {"count": 5, "size": 13.0, "life": 0.86, "speed": 5.0,
		"spread_degrees": 360.0, "scatter": 7.0, "height": 0.0, "rise_speed": 0.0,
		"gravity": 0.0, "spin": 0.10, "aspect": 1.0, "grow": 0.10, "opacity": 0.36},
	"fleck": {"count": 9, "size": 7.5, "life": 0.68, "speed": 52.0,
		"spread_degrees": 100.0, "scatter": 4.0, "height": 6.0, "rise_speed": 28.0,
		"gravity": 74.0, "spin": 1.0, "aspect": 0.46, "grow": -0.15, "opacity": 0.66},
	"mist": {"count": 4, "size": 37.0, "life": 1.18, "speed": 11.0,
		"spread_degrees": 150.0, "scatter": 10.0, "height": 7.0, "rise_speed": 13.0,
		"gravity": 0.0, "spin": 0.16, "aspect": 0.84, "grow": 0.55, "opacity": 0.16},
}

@export_range(1, 2048, 1) var max_particles := 192
@export var fixed_seed := 871031
@export var color := Color("8f343d")
@export_range(0.2, 1.0, 0.01) var ground_projection := 0.46
@export var auto_advance := false

var clock := 0.0
var emitting := true
var _pool: Array[Dictionary] = []
var _cursor := 0
var _rng := RandomNumberGenerator.new()
var _initialized := false
static var _textures: Dictionary = {}


func _ready() -> void:
	_ensure_pool()
	set_notify_transform(true)
	set_process(auto_advance)
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR


func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSFORM_CHANGED:
		queue_redraw()


func _ensure_pool() -> void:
	if not _initialized:
		_rng.seed = fixed_seed
		_initialized = true
	var capacity := maxi(1, max_particles)
	while _pool.size() < capacity:
		_pool.append({"alive": false})
	if _pool.size() > capacity:
		_pool.resize(capacity)
	_cursor %= capacity
	if _textures.is_empty():
		for profile in ["ground", "fleck", "mist"]:
			_textures[profile] = _make_ink_texture(profile)


func play(world_position: Vector2, direction := Vector2.RIGHT, profile := "mist", settings: Dictionary = {}) -> int:
	clear_visuals()
	emitting = true
	return burst(world_position, direction, profile, settings)


func burst(world_position: Vector2, direction := Vector2.RIGHT, profile := "fleck", settings: Dictionary = {}) -> int:
	if not emitting:
		return 0
	_ensure_pool()
	var kind := profile if PROFILES.has(profile) else "mist"
	var options: Dictionary = PROFILES[kind].duplicate()
	options.merge(settings, true)
	var count := clampi(int(options.count), 0, maxi(1, max_particles))
	var base_angle := direction.angle() if direction.length_squared() > 0.0001 else 0.0
	var projection := clampf(float(options.get("projection", ground_projection)), 0.01, 1.0)
	var tint: Color = options.get("color", color)
	for i in range(count):
		var slot := _acquire_slot()
		var angle := base_angle + deg_to_rad(float(options.spread_degrees)) * _rng.randf_range(-0.5, 0.5)
		var scatter := Vector2.from_angle(_rng.randf() * TAU) * sqrt(_rng.randf()) * float(options.scatter)
		scatter.y *= projection
		var speed := float(options.speed) * _rng.randf_range(0.58, 1.18)
		var drift: Vector2 = options.get("drift", Vector2.ZERO)
		_pool[slot] = {
			"alive": true, "kind": kind, "origin": world_position + scatter, "age": 0.0,
			"velocity": Vector2.from_angle(angle) * speed, "drift": drift,
			"projection": projection, "color": tint.darkened(_rng.randf_range(0.0, 0.13)),
			"size": maxf(0.5, float(options.size) * _rng.randf_range(0.73, 1.25)),
			"life": maxf(0.01, float(options.life) * _rng.randf_range(0.82, 1.14)),
			"height": maxf(0.0, float(options.height)), "rise_speed": float(options.rise_speed),
			"gravity": maxf(0.0, float(options.gravity)), "angle": _rng.randf() * TAU,
			"spin": float(options.spin) * _rng.randf_range(-1.0, 1.0),
			"aspect": maxf(0.05, float(options.aspect)), "grow": float(options.grow),
			"opacity": clampf(float(options.opacity), 0.0, 1.0),
		}
	queue_redraw()
	return count


func trail_emit(world_position: Vector2, direction := Vector2.RIGHT, profile := "ground", settings: Dictionary = {}) -> int:
	var options := {"count": 2}
	options.merge(settings, true)
	return burst(world_position, direction, profile, options)


func _acquire_slot() -> int:
	# A full pool reuses the next existing data slot. It never creates a node.
	for offset in range(_pool.size()):
		var candidate := (_cursor + offset) % _pool.size()
		if not bool(_pool[candidate].alive):
			_cursor = (candidate + 1) % _pool.size()
			return candidate
	var result := _cursor
	_cursor = (_cursor + 1) % _pool.size()
	return result


func _process(delta: float) -> void:
	advance(delta)


func advance(delta: float) -> void:
	var dt := maxf(0.0, delta)
	if dt <= 0.0:
		return
	clock += dt
	for particle in _pool:
		if not bool(particle.alive):
			continue
		particle.age = float(particle.age) + dt
		if float(particle.age) >= float(particle.life):
			particle.alive = false
	queue_redraw()


func stop(clear_existing := false) -> void:
	# Stop emission while existing ink finishes fading through advance().
	emitting = false
	if clear_existing:
		clear_visuals()


func clear_visuals() -> void:
	for particle in _pool:
		particle.alive = false
	clock = 0.0
	_cursor = 0
	_rng.seed = fixed_seed
	queue_redraw()


func active_count() -> int:
	var count := 0
	for particle in _pool:
		if bool(particle.alive):
			count += 1
	return count


func _draw() -> void:
	# Cancel only this CanvasItem's world transform. The real arena camera still
	# applies normally, while node / parent moves leave old world samples fixed.
	var inverse := global_transform.affine_inverse()
	for particle in _pool:
		if not bool(particle.alive):
			continue
		var t := float(particle.age)
		var progress := clampf(t / float(particle.life), 0.0, 1.0)
		var displacement: Vector2 = particle.velocity * t + particle.drift * (0.5 * t * t)
		displacement.y *= float(particle.projection)
		var height := maxf(0.0, float(particle.height) + float(particle.rise_speed) * t - float(particle.gravity) * 0.5 * t * t)
		var center: Vector2 = particle.origin + displacement + Vector2(0, -height)
		var size := float(particle.size) * maxf(0.1, 1.0 + float(particle.grow) * progress)
		var angle := float(particle.angle) + float(particle.spin) * t
		var tx := Transform2D(angle, center)
		if str(particle.kind) == "ground":
			# Project a ground-plane ink shape after rotation, in world axes.
			tx.x.y *= float(particle.projection)
			tx.y.y *= float(particle.projection)
		draw_set_transform_matrix(inverse * tx)
		var opacity := smoothstep(0.0, 0.055, t) * pow(1.0 - progress, 0.65) * float(particle.opacity)
		var tint: Color = particle.color
		tint.a *= opacity
		var dimensions := Vector2(size, size * float(particle.aspect))
		draw_texture_rect(_textures[str(particle.kind)], Rect2(-dimensions * 0.5, dimensions), false, tint)
	draw_set_transform_matrix(Transform2D.IDENTITY)


static func _make_ink_texture(profile: String) -> Texture2D:
	const EDGE := 64
	var image := Image.create(EDGE, EDGE, false, Image.FORMAT_RGBA8)
	var noise := FastNoiseLite.new()
	noise.seed = 17031
	noise.frequency = 0.095
	noise.fractal_octaves = 3
	for y in range(EDGE):
		for x in range(EDGE):
			var point := (Vector2(x, y) + Vector2(0.5, 0.5)) / float(EDGE) * 2.0 - Vector2.ONE
			var radius := point.length()
			var grain := noise.get_noise_2d(float(x), float(y))
			var contour := radius + grain * (0.10 if profile == "mist" else 0.24)
			var alpha := 0.0
			if profile == "mist":
				alpha = exp(-radius * radius * 3.7) * (1.0 - smoothstep(0.70, 1.0, contour))
				alpha *= 0.84 + grain * 0.16
			else:
				alpha = (1.0 - smoothstep(0.46, 1.0, contour)) * (0.74 + grain * 0.26)
				if profile == "fleck":
					alpha *= smoothstep(-0.40, -0.12, grain)
			image.set_pixel(x, y, Color(1, 1, 1, clampf(alpha, 0.0, 1.0)))
	return ImageTexture.create_from_image(image)
