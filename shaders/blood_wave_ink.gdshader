shader_type canvas_item;
render_mode unshaded, blend_mix;

// Geometry owns the sector and its radius. UV.x follows the arc; UV.y crosses
// each ink band from its inner to outer edge. All motion uses the caller's clock.
uniform float phase = 0.0;
uniform float band_seed = 0.0;
uniform float ink_density : hint_range(0.0, 2.0) = 1.0;
varying vec2 ink_position;

void vertex() {
	ink_position = VERTEX;
}

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash21(cell), hash21(cell + vec2(1.0, 0.0)), f.x),
		mix(hash21(cell + vec2(0.0, 1.0)), hash21(cell + vec2(1.0, 1.0)), f.x), f.y);
}

float ink_noise(vec2 p) {
	float result = value_noise(p) * 0.52;
	result += value_noise(p * 2.07 + vec2(19.7, 7.3)) * 0.28;
	result += value_noise(p * 4.13 + vec2(8.2, 31.1)) * 0.14;
	result += value_noise(p * 8.21 + vec2(43.1, 11.4)) * 0.06;
	return result;
}

void fragment() {
	vec4 pigment = COLOR;
	// The same pigment field spans all wake bands, avoiding a striped fan.
	vec2 flow = ink_position * vec2(0.061, 0.084) - vec2(phase * 0.12, phase * 0.07);
	float wash = ink_noise(flow);
	float grain = value_noise(flow * vec2(2.7, 3.1));
	float fleck = value_noise(flow * vec2(5.7, 4.9));
	// Uneven wet edges, followed by fine dry-brush gaps inside the stroke.
	float inner_edge = 0.030 + 0.15 * ink_noise(vec2(UV.x * 23.0, band_seed));
	float outer_edge = 0.045 + 0.20 * ink_noise(vec2(UV.x * 29.0 + 37.0, band_seed + 4.0));
	float edge = smoothstep(0.0, inner_edge + 0.19, UV.y);
	edge *= 1.0 - smoothstep(1.0 - outer_edge - 0.21, 1.0, UV.y);
	float broken_ink = smoothstep(0.22, 0.51, wash + grain * 0.15);
	float dry_pores = mix(0.58, 1.0, smoothstep(0.14, 0.42, grain));
	float paper_flecks = mix(0.70, 1.0, smoothstep(0.10, 0.29, fleck));
	// Broad density changes break the dark rim into connected ink folds. Avoid
	// making a solid outline or an evenly filled translucent sector panel.
	float bleed = ink_noise(ink_position * vec2(0.027, 0.043) + vec2(8.4, 3.1));
	float density = mix(0.48, 1.17, wash) * mix(0.73, 1.06, bleed)
		* broken_ink * dry_pores * paper_flecks;
	// Dense folds stay wine-red; thin areas retain the same pigment without glow.
	vec3 color = pigment.rgb * mix(0.67, 1.02, wash);
	COLOR = vec4(color, clamp(pigment.a * edge * density * ink_density, 0.0, 1.0));
}
