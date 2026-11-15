extends Node2D

## A compact, two-layer ink contact. It has no collision or gameplay state.
var age := 0.0
var duration := 0.18
var hold := 0.026
var heavy := false
var guarded := false
var blood := false
var direction := Vector2.RIGHT

func configure(event: Dictionary) -> void:
	heavy = bool(event.heavy)
	guarded = int(event.blocked) > 0
	blood = bool(event.blood) and not guarded
	direction = event.direction
	hold = 0.040 if heavy else 0.026
	duration = 0.23 if heavy else 0.17
	position = event.point
	z_as_relative = false
	z_index = clampi(int(event.depth) + 39, -4096, 4095)
	queue_redraw()

func advance(delta: float) -> bool:
	age += delta
	queue_redraw()
	return age < duration

func _project(point: Vector2) -> Vector2:
	var side := Vector2(-direction.y, direction.x)
	var world := direction * point.x + side * point.y
	# A stable oblique plane: rotating through 360 degrees never stands it upright.
	return Vector2(world.x, world.y * 0.58)

func _brush(a: Vector2, b: Vector2, c: Vector2, width: float, color: Color) -> void:
	var left := PackedVector2Array()
	var right := PackedVector2Array()
	for i in range(13):
		var t := i / 12.0
		var p := (1.0-t)*(1.0-t)*a + 2.0*(1.0-t)*t*b + t*t*c
		var tangent := (b-a)*(1.0-t) + (c-b)*t
		var normal := Vector2(-tangent.y, tangent.x).normalized()
		var taper := pow(sin(PI*t), 0.55)
		left.append(_project(p + normal*width*taper*0.5))
		right.append(_project(p - normal*width*taper*0.5))
	right.reverse()
	left.append_array(right)
	draw_colored_polygon(left, color)

func _draw() -> void:
	var p := clampf((age-hold)/maxf(duration-hold, 0.001), 0.0, 1.0)
	var fade := pow(1.0-p, 1.6)
	var size := (1.25 if heavy else 1.0) * (0.88 + 0.30*p)
	var ink := Color("cbb893") if guarded else (Color("af6d5d") if blood else Color("dfd6bf"))
	var dark := Color("665b4b") if not blood else Color("753f39")
	var radius := 12.0*size
	# Broad back stroke and its narrower light edge read as one contact, not debris.
	_brush(Vector2(-4,-radius), Vector2(11*size,0), Vector2(-4,radius), 6.0*size, Color(dark, 0.38*fade))
	_brush(Vector2(-5,-radius), Vector2(7*size,0), Vector2(-5,radius), 3.0*size, Color(ink, 0.90*fade))
	if heavy or guarded:
		_brush(Vector2(-12,-8)*size, Vector2(-6,0)*size, Vector2(-12,8)*size, 2.2*size, Color(ink, 0.50*fade))
	if age < hold + 0.035:
		var flash := 1.0 - smoothstep(hold, hold+0.035, age)
		_brush(Vector2(-7,0)*size, Vector2(-1,-2)*size, Vector2(5,0)*size, 5.0*size, Color("f1e6c9", 0.76*flash))
