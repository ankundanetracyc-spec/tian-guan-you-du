shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform vec4 ink_color : source_color = vec4(0.012, 0.012, 0.012, 0.98);
uniform sampler2D brush_texture : source_color, filter_linear;
uniform vec4 brush_rect = vec4(0.0, 0.045, 1.0, 0.46);
uniform float reveal_direction : hint_range(-1.0, 1.0) = 1.0;
uniform float vertical_flip : hint_range(0.0, 1.0) = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float seed_value = 0.0;

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

void fragment() {
	float t = clamp(progress, 0.0, 1.0);
	float seed = seed_value * 0.017;
	float living_motion = sin(t * 3.14159);
	vec2 brush_uv = UV;
	brush_uv.y += sin(UV.x * 7.0 + seed + t * 2.6) * 0.010 * living_motion;
	brush_uv.y = mix(brush_uv.y, 1.0 - brush_uv.y, vertical_flip);
	brush_uv = clamp(brush_uv, vec2(0.002), vec2(0.998));
	vec2 sample_uv = brush_rect.xy + brush_uv * brush_rect.zw;
	float pigment = texture(brush_texture, sample_uv).a;

	// A broken, uneven reveal gives the brush tip a hand-drawn arrival rather than a hard wipe.
	float front = mix(-0.12, 1.12, smoothstep(0.0, 1.0, t));
	float reveal_x = reveal_direction >= 0.0 ? UV.x : 1.0 - UV.x;
	float edge_noise = value_noise(vec2(reveal_x * 8.0 + seed, UV.y * 5.0 + seed * 1.7)) - 0.5;
	front += edge_noise * 0.035 * (1.0 - smoothstep(0.0, 0.86, t));
	float reveal = 1.0 - smoothstep(front - 0.045, front + 0.035, reveal_x);
	float alpha = pigment * reveal * ink_color.a;
	COLOR = vec4(ink_color.rgb, alpha);
}
