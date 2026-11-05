extends "capture_approved_outfit_run_review.gd"

func _actor(viewport: SubViewport, before: bool, zoom: float, point: Vector2) -> Node2D:
	var rig: Node2D = super._actor(viewport,before,zoom,point)
	if before: return rig
	var shader := Shader.new()
	shader.code = """shader_type canvas_item;
render_mode unshaded;
void fragment() {
	vec4 original = texture(TEXTURE,UV);
	float y = UV.y/TEXTURE_PIXEL_SIZE.y;
	vec3 band = y<760.0 ? vec3(1,0,0) : (y<820.0 ? vec3(1,0.4,0) : (y<900.0 ? vec3(1,1,0) : (y<1000.0 ? vec3(0,1,0) : vec3(0,0.3,1))));
	COLOR = vec4(band,original.a*smoothstep(744.0,750.0,y));
}
"""
	for record in rig.wanderer_skin.mesh_records:
		if record.name not in ["near_leg","far_leg"]: continue
		var material := ShaderMaterial.new()
		material.shader = shader
		record.mesh.material = material
	return rig
