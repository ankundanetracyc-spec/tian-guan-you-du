shader_type canvas_item;
render_mode unshaded, blend_mix;

// Geometry, pigment and ballistic droplets are generated here; no brush atlas.
// 0: sword sweep, 1: sword contact, 2: impact, 3: fist contact.
uniform float effect_mode = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float seed_value = 0.0;
uniform float critical = 0.0;
uniform float evaded = 0.0;
uniform vec4 ink_color : source_color = vec4(0.04, 0.055, 0.052, 0.94);
uniform vec4 accent_color : source_color = vec4(0.25, 0.32, 0.28, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), f.x), f.y);
}

float pigment(vec2 p) {
	return noise(p) * 0.64 + noise(p * 2.07 + 13.4) * 0.25
		+ noise(p * 4.13 - 7.8) * 0.11;
}

float brush(vec2 p, float t, float seed, float arc, float thickness, float delay) {
	float u = clamp((p.x + 0.87) / 1.74, 0.0, 1.0);
	float clock = max(t - delay, 0.0);
	float head = 1.0 - pow(1.0 - clamp(clock / 0.36, 0.0, 1.0), 3.0);
	float reveal = 1.0 - smoothstep(head - 0.045, head + 0.005, u);
	float age = max(clock - u * 0.18, 0.0);
	float curve = -arc * (1.0 - p.x * p.x) + 0.028 * sin(p.x * 4.0 + seed);
	float grain = pigment(vec2(p.x * 9.0 + seed, p.y * 21.0));
	float edge = (grain - 0.5) * 0.018;
	float pressure = pow(max(sin(u * 3.141593), 0.0), 0.65);
	float width = thickness * pressure * (0.76 + grain * 0.48);
	float dist = abs(p.y - curve + edge);
	float feather = 0.007 + age * 0.018;
	float dense = 1.0 - smoothstep(width * 0.65, width + feather, dist);
	float wet = (1.0 - smoothstep(width, width + 0.028 + age * 0.038, dist)) * 0.22;
	// Long bristle gaps follow the curve; the paper remains visible through them.
	float bristles = noise(vec2(p.x * 7.0 + seed, (p.y - curve) * 180.0 + seed));
	float dry = 1.0 - smoothstep(0.62, 0.84, bristles);
	float dissolve = 1.0 - smoothstep(0.46 + grain * 0.28, 0.85 + grain * 0.12, age);
	float ends = smoothstep(-0.89, -0.82, p.x) * (1.0 - smoothstep(0.82, 0.89, p.x));
	return (dense * mix(0.24, 0.96, dry) + wet) * reveal * ends * dissolve
		* smoothstep(0.0, 0.018, clock) * (1.0 - smoothstep(0.84, 1.0, t));
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float t = clamp(progress, 0.0, 1.0);
	float seed = seed_value * 0.037;
	float wash = 0.0;
	float body = 0.0;
	float sweep = 1.0 - step(0.5, effect_mode);
	float sword_hit = step(0.5, effect_mode) * (1.0 - step(1.5, effect_mode));
	if (effect_mode < 0.5) {
		// Three independent strokes, delayed and curved rather than scaled images.
		body = brush(p, t, seed, 0.40, 0.115, 0.0);
		body = max(body, brush(p + vec2(0.025, -0.07), t, seed + 6.3, 0.33, 0.032, 0.055) * 0.67);
		body = max(body, brush(p + vec2(-0.018, 0.05), t, seed + 11.7, 0.49, 0.017, 0.09) * 0.57);
	} else if (effect_mode < 1.5) {
		vec2 cut = vec2(p.x, p.y - p.x * 0.19);
		body = brush(cut, t, seed, 0.035, 0.066, 0.0);
		vec2 cross = vec2(p.x * 0.94 - p.y * 0.34, p.x * 0.34 + p.y * 0.94);
		body = max(body, brush(cross + vec2(0.0, 0.06), t, seed + 4.0, 0.045, 0.022, 0.05) * 0.64);
	} else {
		// Uneven compressed ink bloom and broken pressure wave, no perfect circle.
		vec2 q = p * (effect_mode > 2.5 ? vec2(0.83, 1.27) : vec2(0.95, 1.12));
		float r = length(q);
		float grain = pigment(q * 9.0 + seed);
		float burst = 1.0 - pow(1.0 - min(t / 0.48, 1.0), 3.0);
		float theta = atan(q.y, q.x);
		float lobes = sin(theta * 5.0 + seed) * 0.033 + sin(theta * 9.0 - seed) * 0.023;
		float radius = 0.05 + burst * 0.56;
		float wave = 1.0 - smoothstep(0.012, 0.04, abs(r - radius - lobes - (grain - 0.5) * 0.07));
		wave *= smoothstep(0.32, 0.66, noise(q * 12.0 + seed)) * (1.0 - smoothstep(0.22, 0.67, t));
		float bloom = 1.0 - smoothstep(0.03, 0.24 + burst * 0.07, r + (grain - 0.5) * 0.14);
		body = bloom * (0.35 + grain * 0.60) * (1.0 - smoothstep(0.12, 0.61, t));
		wash = wave * 0.48;
		for (int i = 0; i < 7; i++) {
			float f = float(i);
			float a = f * 0.8976 + hash(vec2(f, seed)) * 0.70;
			vec2 dir = vec2(cos(a), sin(a));
			float along = dot(q, dir);
			float across = dot(q, vec2(-dir.y, dir.x));
			float end = 0.17 + burst * (0.23 + hash(vec2(seed, f)) * 0.30);
			float taper = max(1.0 - along / max(end, 0.01), 0.0);
			float line = 1.0 - smoothstep(0.008 + taper * 0.025, 0.025 + taper * 0.027, abs(across + sin(along * 12.0 + f) * 0.012));
			line *= smoothstep(0.025, 0.10, along) * (1.0 - smoothstep(end - 0.07, end, along));
			body = max(body, line * (0.35 + grain * 0.45) * (1.0 - smoothstep(0.25, 0.78, t)));
		}
	}
	float drops = 0.0;
	for (int i = 0; i < 4; i++) {
		float f = float(i);
		float random = hash(vec2(f + 19.0, seed));
		float delay = 0.035 + random * 0.13;
		float age = max(t - delay, 0.0);
		float a = f * 2.39996 + seed;
		vec2 dir = vec2(cos(a), sin(a));
		vec2 origin = vec2(0.0);
		if (effect_mode < 0.5) {
			origin.x = -0.67 + random * 1.30;
			origin.y = -0.40 * (1.0 - origin.x * origin.x);
		} else if (effect_mode < 1.5) {
			origin.x = (random - 0.5) * 0.85;
			origin.y = origin.x * 0.19;
		}
		vec2 velocity = dir * (0.38 + random * 0.42) + vec2(sweep * 0.25 + sword_hit * 0.12, 0.0);
		vec2 center = origin + velocity * (1.0 - exp(-age * 3.1)) + vec2(0.0, age * age * 0.23);
		vec2 delta = p - center;
		float size = (0.012 + random * 0.022) * (1.0 - age * 0.50);
		vec2 tangent = vec2(-dir.y, dir.x);
		float droplet = length(vec2(dot(delta, dir) / (1.55 - age * 0.5), dot(delta, tangent)));
		drops = max(drops, (1.0 - smoothstep(size * 0.55, size, droplet))
			* smoothstep(delay, delay + 0.025, t) * (1.0 - smoothstep(0.34 + random * 0.22, 0.86, t)));
	}
	float paper = noise(p * vec2(72.0, 113.0) + seed);
	float density = clamp(body + wash * 0.50, 0.0, 1.0);
	vec3 color = mix(accent_color.rgb * 0.65, ink_color.rgb * 0.78, smoothstep(0.05, 0.66, density));
	color = mix(color, vec3(0.48, 0.15, 0.095), critical * 0.32);
	float alpha = max(body + wash * 0.32, drops * 0.87) * (0.89 + paper * 0.11);
	alpha *= smoothstep(0.0, 0.018, t) * (1.0 - smoothstep(0.86, 1.0, t));
	COLOR = vec4(color, clamp(alpha * ink_color.a * (1.0 - evaded * 0.66), 0.0, 0.96));
}
