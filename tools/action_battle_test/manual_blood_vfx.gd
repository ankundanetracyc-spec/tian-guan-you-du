extends Node2D
## The released basic attack's own silhouette, animation and material, carried
## farther forward as blood ink. No independent force shape or contact clock.
const InkParticles = preload("ink_particle_layer.gd")
const Source = preload("attack_ink_source.gd")
const INK_SHADER = preload("quiet_force.gdshader")
const LIFETIME := 0.38
const MAX_MARKS := 32
var marks: Array[Dictionary] = []
var slots: Array[Sprite2D] = []
var ink: Node2D
var _fallback_textures: Dictionary = {}


func _ready() -> void:
	ink = InkParticles.new()
	ink.max_particles = 96
	add_child(ink)
	set_process(false)


func _acquire_slot() -> int:
	for index in range(slots.size()):
		if not slots[index].visible:
			return index
	if slots.size() >= MAX_MARKS:
		var oldest := int(marks[0].slot)
		marks.pop_front()
		slots[oldest].hide()
		return oldest
	var stamp := Sprite2D.new()
	stamp.name = "BloodQi%d" % slots.size()
	stamp.top_level = true
	stamp.z_as_relative = false
	stamp.centered = false
	stamp.region_enabled = true
	stamp.region_filter_clip_enabled = true
	var pigment := ShaderMaterial.new()
	pigment.shader = INK_SHADER
	pigment.set_shader_parameter("blood_mix", 1.0)
	stamp.material = pigment
	add_child(stamp)
	slots.append(stamp)
	return slots.size() - 1


func _texture(key: String, source: Node2D) -> Texture2D:
	# Use the exact live texture object, including future supported left-button
	# silhouettes. Registered source atlases also support isolated art previews.
	if is_instance_valid(source):
		return source.strike_fx_textures.get(key)
	if not Source.ATLASES.has(key):
		return null
	if not _fallback_textures.has(key):
		var bitmap := Image.new()
		var path := "res://tools/action_battle_test/" + str(Source.ATLASES[key])
		if bitmap.load(ProjectSettings.globalize_path(path)) != OK:
			return null
		_fallback_textures[key] = ImageTexture.create_from_image(bitmap)
	return _fallback_textures[key]


func emit_blood(origin: Vector2, direction: Vector2, reach: float, extension: float, height: float,
		visual_meta: Dictionary = {}, visual_source: Node2D = null) -> void:
	if reach <= 0.0:
		return
	var key := str(visual_meta.get("visual_key", visual_meta.get("kind", "拳掌")))
	var texture := _texture(key, visual_source)
	if texture == null:
		return
	var heading := direction.normalized() if not direction.is_zero_approx() else Vector2.RIGHT
	var world_direction := global_transform.basis_xform(heading).normalized()
	var plane := origin - Vector2(0, height)
	var world_origin := to_global(plane)
	var mirrored := world_direction.x < 0.0
	var angle := world_direction.angle() + (PI if mirrored else 0.0)
	var basis := Transform2D(angle, Vector2.ZERO).scaled_local(Vector2(-1.0 if mirrored else 1.0, 1.0))
	var kind := str(visual_meta.get("kind", "拳掌"))
	var duration := float(visual_source.strike_fx_duration(kind)) if is_instance_valid(visual_source) else 0.65
	var texture_path := "res://tools/action_battle_test/" + str(Source.ATLASES.get(key, ""))
	if not Source.ATLASES.has(key):
		texture_path = str(visual_meta.get("texture_path", ""))
	if not texture.resource_path.is_empty():
		texture_path = texture.resource_path
	var index := _acquire_slot()
	var mark := {"origin": plane, "ground_origin": origin, "world_origin": world_origin,
		"direction": heading, "reach": reach, "extension": maxf(extension, 0.0),
		"height": height, "age": 0.0, "slot": index, "kind": kind,
		"visual_key": key, "combo_stage": int(visual_meta.get("combo_stage", 0)),
		"visual_height": float(visual_meta.get("visual_height", 1.0)),
		"form": "fist" if key == "拳掌" else ("palm" if key == "拳掌_掌" else "source"),
		"source_type": "basic_attack_atlas", "texture_path": texture_path,
		"texture": texture, "source_duration": duration, "basis": basis,
		"source_effect": visual_meta if is_instance_valid(visual_source) and visual_source.strike_fx.has(visual_meta) else {},
		"animation_frame": 0, "visual_extent": reach + maxf(extension, 0.0)}
	marks.append(mark)
	var stamp := slots[index]
	# The blood layer sits behind the original grey-white basic force. Extending
	# its X alone keeps the same rounded fist/palm height instead of scaling a ball.
	stamp.texture = texture
	stamp.z_index = int(to_global(origin).y) + 5
	stamp.show()
	_update_mark(mark)
	var sample := world_origin + world_direction * (reach + float(mark.extension) * 0.65)
	ink.trail_emit(sample, world_direction, "mist", {"count": 2,
		"color": Color("9b4c43"), "size": 9.0, "life": 0.26,
		"opacity": 0.17, "scatter": 2.0, "speed": 4.0,
		"projection": 0.46, "height": 0.0, "rise_speed": 2.0, "grow": 0.18})
	ink.burst(sample, world_direction, "fleck", {"count": 2,
		"color": Color("90383c"), "size": 3.0, "life": 0.18,
		"opacity": 0.24, "scatter": 2.0, "speed": 5.0,
		"projection": 0.46, "height": 0.0, "rise_speed": 3.0,
		"gravity": 14.0, "spread_degrees": 55.0})


func _update_mark(mark: Dictionary) -> void:
	var age := float(mark.age)
	var duration := float(mark.source_duration)
	var texture: Texture2D = mark.texture
	# Real combat borrows the original effect's read-only age, so release within
	# a frame cannot make the blood atlas run one simulation tick ahead of it.
	var source_effect: Dictionary = mark.source_effect
	var source_age := float(source_effect.get("age", age))
	var frame_index := Source.frame_index(source_age, duration)
	mark.animation_frame = frame_index
	var frame := Source.frame_rect(texture, frame_index)
	var destination := Source.destination_rect(texture, float(mark.reach), clampf(source_age / duration, 0.0, 1.0))
	var forward_scale := float(mark.visual_extent) / float(mark.reach)
	destination.position.x *= forward_scale
	destination.size.x *= forward_scale
	destination.position.y *= float(mark.visual_height)
	destination.size.y *= float(mark.visual_height)
	var basis: Transform2D = mark.basis
	var stamp := slots[int(mark.slot)]
	stamp.region_rect = frame
	stamp.global_transform = Transform2D(basis.x * destination.size.x / frame.size.x,
		basis.y * destination.size.y / frame.size.y,
		Vector2(mark.world_origin) + basis.basis_xform(destination.position))
	var fade := 1.0 - smoothstep(0.08, LIFETIME, age)
	stamp.material.set_shader_parameter("opacity", 0.88 * fade)
	stamp.material.set_shader_parameter("dissolve", smoothstep(LIFETIME * 0.4, LIFETIME, age))
	stamp.material.set_shader_parameter("weight", 0.0)
	stamp.material.set_shader_parameter("effect_age", source_age)


func advance(seconds: float) -> void:
	var delta := maxf(0.0, seconds)
	for index in range(marks.size() - 1, -1, -1):
		marks[index].age = float(marks[index].age) + delta
		if float(marks[index].age) >= LIFETIME:
			slots[int(marks[index].slot)].hide()
			marks.remove_at(index)
		else:
			_update_mark(marks[index])
	if ink != null:
		ink.advance(delta)


func clear_visuals() -> void:
	marks.clear()
	for slot in slots:
		slot.hide()
	if ink != null:
		ink.clear_visuals()
