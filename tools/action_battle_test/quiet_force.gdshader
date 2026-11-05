shader_type canvas_item;
render_mode unshaded;

// Original silhouettes remain the outer mask. The pigment is a continuous ink
// wash, not independently disappearing pixel noise. The owner supplies its clock.
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float weight : hint_range(0.0, 1.0) = 0.0;
uniform float effect_age = 0.0;
uniform float blood_mix : hint_range(0.0, 1.0) = 0.0;
varying vec2 ink_position;

void vertex() {
	// Region sprites have the same local canvas in every atlas cell. Keeping the
	// pigment in that canvas prevents the grain from jumping between cells.
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
	float value = value_noise(p) * 0.57;
	value += value_noise(p * 2.07 + vec2(19.7, 7.3)) * 0.29;
	value += value_noise(p * 4.13 + vec2(8.2, 31.1)) * 0.14;
	return value;
}

void fragment() {
	vec4 source = texture(TEXTURE, UV);
	float light = dot(source.rgb, vec3(0.299, 0.587, 0.114));
	vec2 flow = ink_position * vec2(0.025, 0.032) - vec2(effect_age * 0.13, effect_age * 0.06);
	float wash = ink_noise(flow);
	float dry_brush = ink_noise(ink_position * vec2(0.014, 0.17) + vec2(23.1, 8.4));
	// Grey-white ordinary force and warmer mineral-gold heavy force share the
	// same restrained highlights, dry streaks and soft wet pigment islands.
	vec3 shadow = mix(vec3(0.30, 0.33, 0.31), vec3(0.32, 0.28, 0.22), weight);
	vec3 middle = mix(vec3(0.67, 0.70, 0.65), vec3(0.75, 0.67, 0.51), weight);
	shadow = mix(shadow, vec3(0.28, 0.085, 0.12), blood_mix);
	middle = mix(middle, vec3(0.61, 0.22, 0.24), blood_mix);
	vec3 ink = mix(shadow, middle, smoothstep(0.14, 0.67, light));
	vec3 highlight = mix(vec3(0.88, 0.90, 0.83), vec3(0.93, 0.87, 0.73), weight);
	highlight = mix(highlight, vec3(0.79, 0.38, 0.34), blood_mix);
	ink = mix(ink, highlight, smoothstep(0.68, 0.98, light));
	ink *= mix(0.90, 1.04, wash);
	float pigment = mix(0.70, 1.07, wash) * mix(0.76, 1.0, smoothstep(0.20, 0.58, dry_brush));
	// Broad, feathered gaps open toward the end instead of peppering the hand
	// with fine binary holes. The owner's existing lifetime still ends the effect.
	float breakup_level = mix(-0.35, 0.95, dissolve);
	float breakup = smoothstep(breakup_level - 0.16, breakup_level + 0.16, wash + dry_brush * 0.14);
	// Neighbour alpha only softens inside the original mask; it cannot extend
	// the silhouette into another atlas cell or enlarge the visible attack range.
	vec2 px = TEXTURE_PIXEL_SIZE * 1.2;
	float soft_alpha = (source.a * 2.0 + texture(TEXTURE, UV + vec2(px.x, 0.0)).a
		+ texture(TEXTURE, UV - vec2(px.x, 0.0)).a + texture(TEXTURE, UV + vec2(0.0, px.y)).a
		+ texture(TEXTURE, UV - vec2(0.0, px.y)).a) / 6.0;
	float mask = min(source.a, soft_alpha * 1.08);
	COLOR = vec4(ink, clamp(mask * opacity * pigment * breakup, 0.0, 1.0));
}
