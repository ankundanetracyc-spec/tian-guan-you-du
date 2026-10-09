shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D ink_noise : repeat_enable, filter_linear_mipmap;
uniform vec4 shield_tint : source_color = vec4(0.105, 0.21, 0.19, 1.0);
uniform vec4 edge_tint : source_color = vec4(0.07, 0.14, 0.12, 1.0);
uniform vec4 impact_tint : source_color = vec4(0.025, 0.038, 0.031, 1.0);
uniform float shield_strength = 0.86;
uniform vec2 impact_origin = vec2(0.18, 0.5);
uniform float impact_progress = -1.0;
uniform float effect_time = 0.0;

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float n = texture(ink_noise, p * 0.38 + vec2(effect_time * 0.012, -effect_time * 0.018)).r;
	float fiber = texture(ink_noise, p * 0.85 + vec2(1.9, effect_time * 0.007)).r;
	float r = length(p * vec2(1.0, 0.95));
	float contour = 0.81 + (n - 0.5) * 0.072;
	float wet = 1.0 - smoothstep(0.032, 0.11, abs(r - contour));
	float dense = (1.0 - smoothstep(0.012, 0.031, abs(r - contour))) * smoothstep(0.24, 0.67, fiber);
	float inner = (1.0 - smoothstep(0.34, 0.86, r)) * n * 0.07;
	float alpha = (wet * 0.12 + dense * 0.35 + inner) * shield_strength;
	vec3 color = mix(shield_tint.rgb, edge_tint.rgb, dense);
	if (impact_progress >= 0.0) {
		float t = clamp(impact_progress, 0.0, 1.0);
		float d = distance(UV, impact_origin) + (n - 0.5) * 0.026;
		float radius = 0.018 + (1.0 - pow(1.0 - t, 3.0)) * 0.22;
		float deposit = (1.0 - smoothstep(radius * 0.43, radius, d)) * (1.0 - smoothstep(0.23, 1.0, t));
		deposit *= (1.0 - smoothstep(0.86, 0.98, r)) * (0.6 + fiber * 0.4);
		alpha += deposit * 0.57;
		color = mix(color, impact_tint.rgb, deposit);
	}
	alpha *= 1.0 - smoothstep(0.91, 0.99, r);
	COLOR = vec4(color, clamp(alpha, 0.0, 0.78));
}
