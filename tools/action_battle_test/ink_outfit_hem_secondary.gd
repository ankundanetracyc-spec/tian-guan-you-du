extends RefCounted

## Fixed-waist, depth-delayed flowing closed robe. The original male101 has
## two lower cloth branches; this project's continuous closed hem borrows
## their different response across the width, not their slit or source art.
var owner_ref: WeakRef
var skin: RefCounted:
	get: return owner_ref.get_ref() if owner_ref!=null else null
var enabled := true
var visible := true
var body_index := -1
var underlay: Polygon2D
var returned_fold: Polygon2D
var fold_reveal := 0.0
var inner_offset := Vector2.ZERO
var inner_rate := Vector2.ZERO
var maximum_layer_separation := 0.0
var inner_safety_scale := 1.0
var depths := PackedFloat32Array()
var across := PackedFloat32Array()
var offsets: Array[Vector2] = [Vector2.ZERO,Vector2.ZERO,Vector2.ZERO]
var rates: Array[Vector2] = [Vector2.ZERO,Vector2.ZERO,Vector2.ZERO]
var last_velocity := Vector2.ZERO
var last_clock := -INF
var elapsed := 0.0
var safety_scale := 1.0
var maximum_applied_offset := 0.0
var minimum_area_ratio := 1.0
var active := false
var reference_speed := 114.0
var waist_fixed_until_v := 0.46
var response := [17.0,11.5,8.0]
var damping := [0.91,0.78,0.70]
var trail := [2.0,4.1,6.3]
var lift := [0.25,0.70,1.35]
var tip_wave_amplitude := 2.10
var progressive_roll := 0.040

const INNER_CLOTH_SHADER := """shader_type canvas_item;
render_mode unshaded;
void fragment() {
	vec4 source = texture(TEXTURE,UV);
	// Reuse the outfit's cutout, but keep the inner panel plain and quiet.
	// Cloud motifs belong to the outer robe and must not be doubled.
	float tone = dot(source.rgb,vec3(0.299,0.587,0.114));
	vec3 cloth = vec3(0.685,0.722,0.700)+(tone-0.65)*0.025;
	float edge = smoothstep(0.955,0.988,UV.y);
	cloth = mix(cloth,vec3(0.47,0.55,0.55),edge*0.42);
	COLOR = vec4(cloth,source.a*smoothstep(0.46,0.50,UV.y));
}
"""

const RETURNED_FOLD_SHADER := """shader_type canvas_item;
render_mode unshaded;
uniform float reveal = 0.0;
void fragment() {
	vec4 source = texture(TEXTURE,UV);
	float lower_corner = smoothstep(0.67,0.72,UV.x)*smoothstep(0.84,0.90,UV.y);
	float turn = smoothstep(0.80,0.93,UV.y);
	vec3 cloth = mix(vec3(0.74,0.765,0.725),vec3(0.58,0.64,0.62),turn*0.45);
	COLOR = vec4(cloth,source.a*lower_corner*reveal);
}
"""

func build(owner_skin: RefCounted) -> bool:
	owner_ref = weakref(owner_skin)
	for index in range(skin.mesh_records.size()):
		if skin.mesh_records[index].category=="body": body_index = index
	if body_index<0: return false
	var component: Dictionary = skin.template_document.components.body
	var matrix: Array = component.cropUvToFullCanvasNormalized
	for uv in skin.mesh_records[body_index].source.uv:
		var full_u := float(matrix[0][0])*float(uv[0])+float(matrix[0][1])*float(uv[1])+float(matrix[0][2])
		var full_v := float(matrix[1][0])*float(uv[0])+float(matrix[1][1])*float(uv[1])+float(matrix[1][2])
		depths.append(clampf((full_v-waist_fixed_until_v)/(0.995-waist_fixed_until_v),0.0,1.0))
		# Actual lower body chart spans roughly U0.18..0.83. This is a smooth
		# phase mask across one continuous fabric, not a new separation/slit.
		across.append(smoothstep(0.18,0.83,full_u))
	var body: Polygon2D = skin.meshes[body_index]
	underlay = Polygon2D.new()
	underlay.name = "SeparateInnerRobeHem"
	underlay.texture = body.texture
	underlay.texture_filter = body.texture_filter
	underlay.uv = body.uv.duplicate()
	underlay.polygons = body.polygons.duplicate()
	underlay.z_index = body.z_index-1
	var shader := Shader.new()
	shader.code = INNER_CLOTH_SHADER
	var material := ShaderMaterial.new()
	material.shader = shader
	underlay.material = material
	skin.runtime.rig_root_node.add_child(underlay)
	returned_fold = Polygon2D.new()
	returned_fold.name = "SmallReturningHemCorner"
	returned_fold.texture = body.texture
	returned_fold.texture_filter = body.texture_filter
	returned_fold.uv = body.uv.duplicate()
	returned_fold.polygons = body.polygons.duplicate()
	returned_fold.z_index = body.z_index
	var fold_shader := Shader.new()
	fold_shader.code = RETURNED_FOLD_SHADER
	var fold_material := ShaderMaterial.new()
	fold_material.shader = fold_shader
	returned_fold.material = fold_material
	skin.runtime.rig_root_node.add_child(returned_fold)
	_update_underlay(skin.current_vertices[body_index])
	return true

func reset(velocity: Vector2 = Vector2.ZERO, clock: float = -INF) -> void:
	for index in range(3):
		offsets[index] = Vector2.ZERO
		rates[index] = Vector2.ZERO
	last_velocity = velocity
	last_clock = clock
	elapsed = 0.0
	maximum_applied_offset = 0.0
	safety_scale = 1.0
	minimum_area_ratio = 1.0
	active = false
	inner_offset = Vector2.ZERO
	inner_rate = Vector2.ZERO
	maximum_layer_separation = 0.0
	inner_safety_scale = 1.0
	fold_reveal = 0.0

func set_enabled(value: bool) -> void:
	if enabled==value: return
	enabled = value
	reset()
	if not enabled and body_index>=0:
		skin.meshes[body_index].polygon = skin.current_vertices[body_index]
	if underlay!=null: underlay.visible = visible and enabled
	if returned_fold!=null: returned_fold.visible = visible and enabled and fold_reveal>0.001

func set_visible(value: bool) -> void:
	visible = value
	if underlay!=null: underlay.visible = value and enabled
	if returned_fold!=null: returned_fold.visible = value and enabled and fold_reveal>0.001

func _offset_at(depth: float) -> Vector2:
	if depth<=0.0: return Vector2.ZERO
	if depth<0.34: return offsets[0]*smoothstep(0.0,0.34,depth)
	if depth<0.68: return offsets[0].lerp(offsets[1],smoothstep(0.34,0.68,depth))
	return offsets[1].lerp(offsets[2],smoothstep(0.68,1.0,depth))

func _candidate(base: PackedVector2Array, amount: float) -> PackedVector2Array:
	var result := base.duplicate()
	var facing := -1.0 if float(skin.motion_facing)<0.0 else 1.0
	var waist: Vector2 = skin.bones.pelvis.position
	var flowing := minf(1.6,offsets[2].length()/float(trail[2]))
	for index in range(result.size()):
		var depth: float = depths[index]
		if depth<=0.0: continue
		var section := _offset_at(depth)
		var side: float = across[index]
		# A small rotation that grows down the cloth conserves each point's
		# waist radius before the trailing offset, unlike stretching the tail.
		# Different edge timing gives the rear border a curl, not a rigid flag.
		var lower_weight := smoothstep(0.25,1.0,depth)
		var angle := -section.y*progressive_roll*lower_weight*lerpf(1.15,0.72,side)
		var rotated := waist+(base[index]-waist).rotated(angle*amount)
		var edge_wave := sin(elapsed*5.4-depth*2.5+side*1.2)*tip_wave_amplitude*flowing*depth*depth*lerpf(1.15,0.65,side)
		# The two source lower branches don't retreat as one slab. Keep the
		# front overlap nearer the leg while the free rear edge trails farther.
		var branch_trail := lerpf(1.22,0.68,side)
		var world_offset := Vector2(section.x*facing*branch_trail,section.y+edge_wave)*amount
		result[index] = rotated+world_offset
	return result

func _valid_area(base: PackedVector2Array, candidate: PackedVector2Array) -> bool:
	minimum_area_ratio = INF
	for triangle in skin.mesh_records[body_index].source.triangles:
		var a: int = triangle[0]
		var b: int = triangle[1]
		var c: int = triangle[2]
		var before := (base[b]-base[a]).cross(base[c]-base[a])
		var after := (candidate[b]-candidate[a]).cross(candidate[c]-candidate[a])
		if absf(before)<0.00001: continue
		var ratio := after/before
		minimum_area_ratio = minf(minimum_area_ratio,ratio)
		if ratio<0.18: return false
	return true

func _under_candidate(base: PackedVector2Array, amount: float) -> PackedVector2Array:
	var result := base.duplicate()
	var waist: Vector2 = skin.bones.pelvis.position
	var facing := -1.0 if float(skin.motion_facing)<0.0 else 1.0
	for index in range(base.size()):
		var depth: float = depths[index]
		if depth<=0.0: continue
		var weight := smoothstep(0.0,1.0,depth)
		# Shorter at rest, tucked beneath the outer robe. Motion exposes the
		# separate overlap, without cutting a slit in the closed outer cloth.
		var radial_scale := 1.0-0.055*weight*amount
		var offset := Vector2(inner_offset.x*facing,inner_offset.y)*weight*amount
		result[index] = waist+(base[index]-waist)*radial_scale+offset
	return result

func _update_underlay(base: PackedVector2Array) -> void:
	if underlay==null: return
	var outer_minimum := minimum_area_ratio
	inner_safety_scale = 1.0
	var candidate := _under_candidate(base,inner_safety_scale)
	for attempt in range(9):
		if _valid_area(base,candidate): break
		inner_safety_scale *= 0.70
		candidate = _under_candidate(base,inner_safety_scale)
	if not _valid_area(base,candidate):
		candidate = base.duplicate()
		inner_safety_scale = 0.0
	minimum_area_ratio = minf(outer_minimum,minimum_area_ratio)
	underlay.polygon = candidate
	underlay.visible = visible and enabled
	maximum_layer_separation = 0.0
	var outer: PackedVector2Array = skin.meshes[body_index].polygon
	for index in range(candidate.size()):
		if depths[index]>0.5: maximum_layer_separation = maxf(maximum_layer_separation,candidate[index].distance_to(outer[index]))
	var flowing := minf(1.0,offsets[2].length()/float(trail[2]))
	# A small turned-back corner crosses in front of the outer edge while the
	# inner panel trails behind it. Only this local corner changes coverage;
	# the complete garment never changes depth or opens a slit.
	fold_reveal = flowing*smoothstep(-0.35,0.65,sin(elapsed*4.9-1.3))
	var fold_points := candidate.duplicate()
	var facing := -1.0 if float(skin.motion_facing)<0.0 else 1.0
	for index in range(fold_points.size()):
		var weight := smoothstep(0.45,1.0,depths[index])
		fold_points[index] += Vector2(-1.4*facing,-3.6)*flowing*weight
	returned_fold.polygon = fold_points
	returned_fold.material.set_shader_parameter("reveal",fold_reveal)
	returned_fold.visible = visible and enabled and fold_reveal>0.001

func update_pose(delta: float, clip: String) -> void:
	if not enabled or body_index<0: return
	if not skin.motion_context:
		reset()
		_update_underlay(skin.current_vertices[body_index])
		return
	if delta<=0.0: return
	var clock: float = skin.motion_clock
	if clock==last_clock: return
	var velocity: Vector2 = skin.previous_velocity
	if is_finite(last_clock) and (clock<last_clock or clock-last_clock>0.25): reset(velocity)
	# Dedicated actions retain their authored source robe pose.
	if clip not in ["Idle","Run_New1","Run_New2"]:
		reset(velocity,clock)
		_update_underlay(skin.current_vertices[body_index])
		return
	var change := ((velocity-last_velocity)/reference_speed).limit_length(2.0)
	for index in range(3): rates[index] -= change*float([0.65,1.05,1.55][index])
	last_velocity = velocity
	last_clock = clock
	elapsed += delta
	var flow := velocity/reference_speed
	flow = flow.limit_length(1.6)
	var speed := minf(1.6,velocity.length()/reference_speed)
	var steps := maxi(1,ceili(delta/(1.0/120.0)))
	var dt := delta/steps
	for step in range(steps):
		var inner_target := Vector2(-flow.x*3.0,speed*(4.2+0.8*sin(elapsed*4.9-1.3))-flow.y*0.7)
		var inner_acceleration := (inner_target-inner_offset)*7.0*7.0-inner_rate*(2.0*0.84*7.0)
		inner_rate += inner_acceleration*dt
		inner_offset = (inner_offset+inner_rate*dt).limit_length(8.0)
		for index in range(3):
			var target := Vector2(-flow.x*float(trail[index]),-flow.y*float(trail[index])*0.30-speed*float(lift[index]))
			# Quiet at rest; modest moving folds have different lengths/phases.
			target.y += sin(elapsed*5.4-float(index)*1.05)*speed*float([0.12,0.30,0.52][index])
			var frequency: float = response[index]
			var acceleration := (target-offsets[index])*frequency*frequency-rates[index]*(2.0*float(damping[index])*frequency)
			rates[index] += acceleration*dt
			offsets[index] = (offsets[index]+rates[index]*dt).limit_length(10.0)
	var base: PackedVector2Array = skin.current_vertices[body_index]
	safety_scale = 1.0
	var candidate := _candidate(base,safety_scale)
	for attempt in range(9):
		if _valid_area(base,candidate): break
		safety_scale *= 0.70
		candidate = _candidate(base,safety_scale)
	if not _valid_area(base,candidate):
		candidate = base.duplicate()
		safety_scale = 0.0
	skin.meshes[body_index].polygon = candidate
	maximum_applied_offset = 0.0
	for index in range(candidate.size()): maximum_applied_offset = maxf(maximum_applied_offset,candidate[index].distance_to(base[index]))
	active = maximum_applied_offset>0.0001
	_update_underlay(base)

func get_diagnostics() -> Dictionary:
	var controls := []
	for index in range(3): controls.append({"offset_world":[offsets[index].x,offsets[index].y],"rate_world":[rates[index].x,rates[index].y]})
	return {"enabled":enabled,"active":active,"model":"separate quiet inner panel beneath continuous closed outer robe; three delayed outer flow sections; project visual tuning, not original DynamicBone","closed_body_vertices":depths.size(),"waist_fixed_until_full_v":waist_fixed_until_v,"response":response,"damping":damping,"trail_native_pose_pixels":trail,"tip_wave_amplitude":tip_wave_amplitude,"progressive_roll":progressive_roll,"different_edge_response":true,"closed_continuous_hem":true,"separate_inner_layer":{"visible":underlay!=null and underlay.visible,"shorter_ratio":0.945,"independent_damped_offset":[inner_offset.x,inner_offset.y],"maximum_boundary_vertex_separation":maximum_layer_separation,"safety_scale":inner_safety_scale,"z_index":underlay.z_index if underlay!=null else -1,"plain_cloth_no_duplicate_cloud":true},"control_states":controls,"maximum_applied_offset":maximum_applied_offset,"safety_scale":safety_scale,"minimum_triangle_area_ratio":minimum_area_ratio,"stored_in_world_orientation":true,"current_vertices_remain_source_pose":true,"dedicated_actions":"source priority; added offsets reset","pause":"no state/plane advance"}
