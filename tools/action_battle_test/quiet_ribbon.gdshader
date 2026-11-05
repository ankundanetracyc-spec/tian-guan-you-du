shader_type canvas_item;
render_mode unshaded;

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
	return value_noise(p) * 0.64 + value_noise(p * 2.07 + vec2(19.7, 7.3)) * 0.36;
}

void fragment() {
	// Irregular wet edges retain an unbroken readable core, including the thin
	// self-cast streams. The moving geometry supplies motion; there is no TIME.
	float wobble = (ink_noise(vec2(UV.x * 13.0, 7.3)) - 0.5) * 0.13;
	float across = UV.y + wobble;
	float feather = smoothstep(0.0, 0.34, across) * (1.0 - smoothstep(0.66, 1.0, across));
	float wash = ink_noise(ink_position * vec2(0.061, 0.084));
	float dry_brush = ink_noise(vec2(UV.x * 18.0, UV.y * 2.3 + 11.0));
	float fibre = mix(0.78, 1.04, wash) * mix(0.79, 1.0, smoothstep(0.20, 0.52, dry_brush));
	COLOR.rgb *= mix(0.93, 1.03, wash);
	COLOR.a *= feather * fibre;
}
