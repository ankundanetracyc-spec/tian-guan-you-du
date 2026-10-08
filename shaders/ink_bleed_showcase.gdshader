shader_type canvas_item;
render_mode unshaded, blend_mix;

// 低成本的艺术化墨晕：以程序化墨形的 Alpha 作为主体，
// 通过多尺度噪声、外扩采样和边缘回渗模拟湿墨在纸上的扩散。
uniform vec4 ink_color : source_color = vec4(0.08, 0.11, 0.10, 0.92);
uniform sampler2D mask_texture;
uniform vec2 mask_texel = vec2(0.001953125, 0.003472222);
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float effect_mode : hint_range(0.0, 4.0) = 0.0;
uniform float intensity : hint_range(0.0, 2.0) = 1.0;
uniform float spread : hint_range(0.0, 2.0) = 1.0;
uniform float distortion : hint_range(0.0, 0.15) = 0.035;
uniform float edge_darkness : hint_range(0.0, 2.0) = 0.85;
uniform float paper_variation : hint_range(0.0, 1.0) = 0.22;
uniform float fade_start : hint_range(0.0, 1.0) = 0.70;

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
	for (int i = 0; i < 4; i++) {
		value += value_noise(p) * amplitude;
		p = p * 2.03 + vec2(17.1, 9.7);
		amplitude *= 0.5;
	}
	return value;
}

float sample_mask(vec2 uv) {
	if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
	return texture(mask_texture, uv).a;
}

float dilated_mask(vec2 uv, float radius) {
	vec2 px = mask_texel * radius;
	float result = sample_mask(uv);
	result = max(result, sample_mask(uv + vec2(px.x, 0.0)));
	result = max(result, sample_mask(uv - vec2(px.x, 0.0)));
	result = max(result, sample_mask(uv + vec2(0.0, px.y)));
	result = max(result, sample_mask(uv - vec2(0.0, px.y)));
	result = max(result, sample_mask(uv + vec2(px.x, px.y) * 0.72));
	result = max(result, sample_mask(uv + vec2(-px.x, px.y) * 0.72));
	result = max(result, sample_mask(uv + vec2(px.x, -px.y) * 0.72));
	result = max(result, sample_mask(uv - vec2(px.x, px.y) * 0.72));
	return result;
}

void fragment() {
	vec2 center = vec2(0.5);
	vec2 local_uv = UV - center;
	float t = clamp(progress, 0.0, 1.0);

	// 让墨形随时间变大，但不呈现机械的均匀缩放。
	float expansion = 0.64 + t * (0.54 + spread * 0.16);
	if (effect_mode > 1.5 && effect_mode < 2.5) {
		// 笔锋扫掠：向右移动，留下略微拖尾。
		local_uv.x -= (t - 0.5) * 0.70;
		expansion = 0.72 + t * 0.34;
	}
	if (effect_mode > 2.5 && effect_mode < 3.5) {
		// 墨爆散：扩张更快，并叠加轻微旋拧。
		expansion = 0.52 + t * 0.78;
		float angle = (t - 0.5) * 0.32;
		local_uv = mat2(vec2(cos(angle), sin(angle)), vec2(-sin(angle), cos(angle))) * local_uv;
	}

	vec2 noise_uv = UV * vec2(5.1, 4.2) + vec2(TIME * 0.08, -TIME * 0.045);
	vec2 flow = vec2(
		fbm(noise_uv + vec2(3.4, 1.2)),
		fbm(noise_uv + vec2(-1.8, 5.1))
	) - 0.5;
	float flow_strength = distortion * (0.35 + t * 0.85);
	vec2 source_uv = center + local_uv / expansion + flow * flow_strength;

	float core = sample_mask(source_uv);
	float outer = dilated_mask(source_uv, 2.0 + t * (7.0 + spread * 4.0));
	float wet_outer = dilated_mask(source_uv + flow * mask_texel * 5.0, 5.0 + t * 12.0);
	float inner = smoothstep(0.08, 0.78, core);
	float body = smoothstep(0.03, 0.62, outer);
	float wet = smoothstep(0.05, 0.68, wet_outer);
	float rim = clamp(wet - body * 0.78, 0.0, 1.0);

	// 墨爆散的边缘更碎，笔锋扫掠的边缘更干。
	float grain = fbm(UV * (15.0 + effect_mode * 2.5) + vec2(7.0, -2.0));
	if (effect_mode > 2.5 && effect_mode < 3.5) {
		body *= smoothstep(0.20, 0.80, grain + 0.22);
		rim *= 0.80 + grain * 0.42;
	}
	if (effect_mode > 1.5 && effect_mode < 2.5) {
		rim *= 0.55 + grain * 0.32;
	}

	float ring = clamp(body - inner, 0.0, 1.0);
	float reveal = smoothstep(0.0, 0.10, t);
	float fade = 1.0 - smoothstep(fade_start, 1.0, t);
	float alpha = max(body, rim * 0.72) * reveal * fade * intensity;

	if (effect_mode > 3.5) {
		// 干湿边缘：主体较淡，只保留一圈有重量的墨边。
		alpha = max(ring * 1.35, rim * 0.62) * reveal * fade * intensity;
	}

	float paper_noise = (grain - 0.5) * paper_variation;
	vec3 pigment = ink_color.rgb * (0.72 + inner * 0.27);
	pigment *= 1.0 + paper_noise;
	// 边缘略深、回渗带略淡，模拟颜料沉积和湿边。
	pigment = mix(pigment, pigment * (0.38 + edge_darkness * 0.42), ring * 0.58);
	pigment = mix(pigment, ink_color.rgb * 0.62, rim * 0.25);

	COLOR = vec4(pigment, clamp(alpha * ink_color.a, 0.0, 1.0));
}
