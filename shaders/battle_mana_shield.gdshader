shader_type canvas_item;
render_mode unshaded, blend_mix;

// 低成本伪 3D 法力护盾：用 UV 圆盘反推半球法线，再叠加边缘菲涅耳、流动纹理和局部受击波。
uniform vec4 shield_tint : source_color = vec4(0.06, 0.20, 0.48, 1.0);
uniform vec4 edge_tint : source_color = vec4(0.38, 0.70, 0.98, 1.0);
uniform vec4 impact_tint : source_color = vec4(0.78, 0.92, 1.0, 1.0);
uniform float shield_strength : hint_range(0.0, 1.0) = 0.86;
uniform vec2 impact_origin = vec2(0.18, 0.50);
uniform float impact_progress = -1.0;
uniform float effect_time = 0.0;

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
	float value = 0.0;
	float amplitude = 0.55;
	for (int i = 0; i < 3; i++) {
		value += value_noise(p) * amplitude;
		p = p * 2.04 + vec2(13.7, 8.1);
		amplitude *= 0.5;
	}
	return value;
}

void fragment() {
	vec2 point = (UV - vec2(0.5)) * 2.0;
	float radius = length(point);
	if (radius > 1.0) discard;

	// 将圆盘视作朝向镜头的半球，z 越大代表越接近正面中心。
	float depth = sqrt(max(1.0 - dot(point, point), 0.0));
	vec3 normal = normalize(vec3(point, depth));
	vec3 view_dir = vec3(0.0, 0.0, 1.0);
	float facing = clamp(dot(normal, view_dir), 0.0, 1.0);
	float fresnel = pow(1.0 - facing, 2.35);
	float rim = smoothstep(0.68, 0.98, radius);

	// 方向性的高光让护盾不再是平面圆环；流纹只占很小权重，避免变成霓虹球。
	vec3 light_dir = normalize(vec3(-0.42, -0.58, 0.72));
	float highlight = pow(max(dot(normal, light_dir), 0.0), 15.0);
	vec2 flow_uv = point * vec2(3.2, 5.6);
	float flow = fbm(flow_uv + vec2(effect_time * 0.13, -effect_time * 0.09));
	float scan = 0.5 + 0.5 * sin(point.y * 18.0 + point.x * 5.0 + effect_time * 1.65 + flow * 2.6);
	float ink_break = 0.72 + 0.28 * sin(atan(point.y, point.x) * 8.0 + radius * 24.0 - effect_time * 0.45);

	float interior = (1.0 - smoothstep(0.18, 0.92, radius)) * 0.12;
	float edge_energy = fresnel * 0.82 + rim * 0.42 + highlight * 0.48;
	float flow_energy = smoothstep(0.46, 0.86, flow) * scan * 0.16 * ink_break;
	float alpha = (interior + edge_energy * 0.68 + flow_energy) * shield_strength;

	vec3 color = mix(shield_tint.rgb * 0.48, edge_tint.rgb, clamp(fresnel * 0.76 + highlight * 0.72 + rim * 0.26, 0.0, 1.0));
	color += shield_tint.rgb * flow_energy * 0.75;
	color *= 0.78 + facing * 0.22;

	// 受击波从攻击者一侧展开，短暂提高亮度并留下浅色法力裂纹。
	float impact_active = step(0.0, impact_progress);
	float impact_distance = distance((UV - impact_origin) * vec2(1.0, 0.86), vec2(0.0));
	float wave_radius = impact_progress * 0.88;
	float wave_line = 1.0 - smoothstep(0.0, 0.055, abs(impact_distance - wave_radius));
	float wave_soft = 1.0 - smoothstep(0.0, 0.18, abs(impact_distance - wave_radius));
	float wave_fade = 1.0 - smoothstep(0.58, 1.0, impact_progress);
	float impact = (wave_line * 0.92 + wave_soft * 0.24) * wave_fade * impact_active;
	color = mix(color, impact_tint.rgb, clamp(impact, 0.0, 1.0));
	alpha += impact * 0.62;

	COLOR = vec4(color, clamp(alpha, 0.0, 0.94));
}
