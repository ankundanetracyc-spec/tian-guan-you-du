shader_type canvas_item;
render_mode unshaded, blend_mix;

// Pigment sits in a broad brush stroke. Its alpha never expands the geometry;
// all reveal, pause and drying clocks belong to the released sword effect.
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float effect_age = 0.0;
uniform float layer = 1.0;
uniform float effect_life = 0.30;
uniform float reveal_seconds = 0.09;

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash21(cell), hash21(cell + vec2(1, 0)), f.x),
		mix(hash21(cell + vec2(0, 1)), hash21(cell + vec2(1, 1)), f.x), f.y);
}

float wash_noise(vec2 p) {
	return noise(p) * 0.58 + noise(p * 2.07 + vec2(19.7, 7.3)) * 0.28
		+ noise(p * 4.13 + vec2(8.2, 31.1)) * 0.14;
}

void fragment() {
	vec4 source = texture(TEXTURE, UV);
	// Each point has its own birth and drying age. The half already swept at
	// contact sits behind the blade; new pigment joins it as the tip carries on.
	float birth = UV.x <= 0.5 ? (UV.x - 0.5) * reveal_seconds
		: reveal_seconds * (1.0 - pow(max(0.0, 2.0 - 2.0 * UV.x), 0.33333333));
	float local_age = max(0.0, effect_age - birth);
	float local_progress = clamp(local_age / max(0.001, effect_life - birth), 0.0, 1.0);
	float drift = effect_age / max(0.001, effect_life);
	// One broad tonal wash, no fibre bands, holes or peppered dissolve. Depth
	// is read as a quiet edge-to-face gradient rather than many tiny ink pieces.
	float wash = noise(vec2(UV.x * 2.1 - drift * 0.07, UV.y * 1.2));
	vec3 pale = vec3(0.88, 0.92, 0.87);
	vec3 ink = vec3(0.41, 0.49, 0.46);
	vec3 pigment = mix(ink, pale, smoothstep(0.0, 0.36, UV.y));
	pigment *= mix(0.89, 1.0, wash);
	float alpha = source.a * mix(0.88,1.0,wash);
	if (layer < 0.5) {
		pigment = vec3(0.59, 0.69, 0.65);
		alpha *= smoothstep(0.015, 0.045, local_age);
	}
	alpha *= smoothstep(0.0, 0.035, effect_age);
	alpha *= smoothstep(-0.008, 0.016, effect_age - birth);
	alpha *= 1.0 - smoothstep(0.42, 1.0, local_progress);
	COLOR = vec4(pigment, clamp(alpha * opacity * (1.0-dissolve), 0.0, 1.0));
}
