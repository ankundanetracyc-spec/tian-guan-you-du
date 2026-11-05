extends Node2D

## Native source-pixel contact shading. The caller supplies the real ground
## offset independently of the body's clip translation or visual elevation.
## This node belongs under runtime, never under the translated rig_root.
const INK := Color(0.10,0.12,0.12)
const SEGMENTS := 40
var contact_data: Array[Dictionary] = []
var drawn: Array[Dictionary] = []
var ground_offset_y := 0.0
var rejected_contacts := 0

func update_contacts(contacts: Array[Dictionary], ground_y: float = 0.0) -> void:
	ground_offset_y = ground_y if is_finite(ground_y) else 0.0
	position = Vector2(0.0,ground_offset_y)
	contact_data.clear()
	drawn.clear()
	rejected_contacts = 0
	var nearest_height := INF
	for value in contacts:
		var x := float(value.get("x",0.0))
		var sole_y := float(value.get("sole_y",0.0))
		var width := float(value.get("width",8.0))
		if not is_finite(x) or not is_finite(sole_y) or not is_finite(width):
			rejected_contacts += 1
			continue
		var height := maxf(0.0,-sole_y)
		var strength := 1.0-smoothstep(0.35,5.5,height)
		nearest_height = minf(nearest_height,height)
		contact_data.append({"side":str(value.get("side","")),"x":x,"sole_y":sole_y,"width":maxf(0.0,width),"height":height,"contact_strength":strength})
	if not contact_data.is_empty():
		# A faint floor reference stays at the actor's world foot anchor even
		# during the original run's airborne phase. Its centre never follows
		# the raised sole, the robe's bounce, or a +4px fallback offset.
		var ambient_strength := 1.0-0.65*smoothstep(0.0,12.0,nearest_height)
		_add_ellipse("ambient","",Vector2.ZERO,Vector2(13.0,1.25),0.010*ambient_strength)
		_add_ellipse("ambient","",Vector2.ZERO,Vector2(9.5,0.75),0.012*ambient_strength)
		for contact in contact_data:
			var strength: float = contact.contact_strength
			if strength<=0.0001: continue
			var radius := clampf(float(contact.width)*0.5,2.2,8.0)
			var softness := smoothstep(0.0,5.5,float(contact.height))
			var at := Vector2(float(contact.x),0.0)
			var spread := lerpf(1.0,1.15,softness)
			# Layered low alpha creates a short soft footprint, with its dark
			# part touching a grounded sole rather than a detached black oval.
			_add_ellipse("contact",contact.side,at,Vector2(radius*spread,lerpf(1.35,1.8,softness)),0.018*strength)
			_add_ellipse("contact",contact.side,at,Vector2(radius*0.80,1.0),0.026*strength)
			_add_ellipse("contact",contact.side,at,Vector2(radius*0.58,0.65),0.034*strength)
			_add_ellipse("contact",contact.side,at,Vector2(radius*0.43,0.34),0.038*strength)
	queue_redraw()

func _add_ellipse(kind: String, side: String, at: Vector2, radius: Vector2, opacity: float) -> void:
	drawn.append({"kind":kind,"side":side,"local_position":at,"radius":radius,"opacity":opacity})

func _draw() -> void:
	for item in drawn:
		var points := PackedVector2Array()
		var at: Vector2 = item.local_position
		var radius: Vector2 = item.radius
		for index in range(SEGMENTS):
			var angle := float(index)*TAU/float(SEGMENTS)
			points.append(at+Vector2(cos(angle)*radius.x,sin(angle)*radius.y))
		draw_colored_polygon(points,Color(INK.r,INK.g,INK.b,float(item.opacity)))

func get_diagnostics() -> Dictionary:
	var positions: Array[Dictionary] = []
	for item in drawn:
		var record := item.duplicate()
		var at: Vector2 = item.local_position
		record.position_in_runtime = transform*at
		record.world_position = to_global(at)
		positions.append(record)
	return {"contacts":contact_data.duplicate(true),"drawn":positions,"ground_y":ground_offset_y,"node_position":position,"all_shadow_centres_on_local_ground":true,"rejected_contacts":rejected_contacts,"source_pixel_units":true,"ambient_anchor_x":0.0,"contact_fade_height_pixels":Vector2(0.35,5.5),"scope":"source-alpha sole contacts on caller-supplied world ground; no body/bone, clip, physics, sword or gameplay changes"}
