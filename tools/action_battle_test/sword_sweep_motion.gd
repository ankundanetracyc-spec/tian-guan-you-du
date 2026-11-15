extends RefCounted
## One visual clock shared by the sword tip and the ink front. The real contact
## remains the runtime's release; this never advances an action or applies a hit.
const VIEW_DEPTH := 0.24

static func project_ground(point: Vector2, depth: float = VIEW_DEPTH) -> Vector2:
	return Vector2(point.x,point.y*depth)

static func unproject_ground(point: Vector2, depth: float = VIEW_DEPTH) -> Vector2:
	return Vector2(point.x,point.y/maxf(depth,0.001))

static func head(age: float, reveal: float) -> float:
	var t := clampf(age/maxf(reveal,0.0001),0.0,1.0)
	# At contact the sword crosses the aim axis, with the already swept half
	# behind it. It carries on through the front arc without braking at contact.
	return 0.5+0.5*(1.0-pow(1.0-t,3.0))

static func tail(age: float, reveal: float, life: float) -> float:
	return 0.94*smoothstep(reveal*0.35,life,maxf(age,0.0))

static func angle(stage: int, fan: float, age: float, reveal: float) -> float:
	var half := deg_to_rad(fan*0.5)
	return lerpf(half,-half,head(age,reveal)) if stage==1 else lerpf(-half,half,head(age,reveal))

static func birth(u: float, reveal: float) -> float:
	if u<=0.5:
		return (u-0.5)*reveal
	return reveal*(1.0-pow(maxf(0.0,2.0-2.0*u),1.0/3.0))
