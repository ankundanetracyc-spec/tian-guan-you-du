shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D ink_noise : repeat_enable, filter_linear_mipmap;
uniform vec4 ink_color : source_color = vec4(0.04, 0.05, 0.045, 0.94);
uniform float progress = 0.0;
uniform float kind = 0.0;
uniform float phase = 0.0;
uniform float seed_value = 0.0;
uniform float critical = 0.0;
uniform float travel_span = 0.75;
uniform float contact_progress = 0.40909;

float ink(vec2 p) {
	vec2 uv = p * 0.36 + vec2(seed_value, seed_value * 0.37);
	vec2 flow = vec2(texture(ink_noise, uv).r, texture(ink_noise, uv.yx + 2.4).r) - 0.5;
	return texture(ink_noise, uv + flow * 0.20).r;
}

float stroke(vec2 p, float tail, float head, float center, float width) {
	float u = clamp((p.x - tail) / max(head - tail, 0.001), 0.0, 1.0);
	float pressure = pow(max(sin(u * 3.14159), 0.0), 0.62) * (0.36 + u * 0.90);
	float grain = ink(vec2(p.x * 1.5, p.y * 0.85));
	float d = abs(p.y - center + (grain - 0.5) * 0.05);
	float body = 1.0 - smoothstep(width * pressure * 0.89, width * pressure + 0.009, d);
	float span = smoothstep(tail, tail + 0.025, p.x) * (1.0 - smoothstep(head - 0.025, head, p.x));
	float fiber = ink(vec2(p.x * 0.17, (p.y - center) * 12.0));
	return body * span * (1.0 - smoothstep(0.64, 0.84, fiber) * 0.68);
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float t = clamp(progress, 0.0, 1.0);
	float grain = ink(p * 2.1);
	float coarse = ink(p * 0.75 + vec2(t * 0.12, -t * 0.045));
	float body = 0.0;
	float wash = 0.0;
	float fade = 1.0 - smoothstep(0.55, 1.0, t);
	if (phase < 0.5) {
		float drive = clamp(t / max(contact_progress, 0.01), 0.0, 1.0);
		// A restrained preparation followed by acceleration into contact.
		float launch = pow(drive, 2.7);
		float head = mix(-travel_span, travel_span, launch);
		float tail = max(-travel_span, head - 0.38);
		float curve = sin((p.x + travel_span) * 4.0 + seed_value) * 0.035;
		float width = 0.10;
		if (kind > 0.5 && kind < 2.5) width = kind < 1.5 ? 0.16 : 0.24;
		if (kind > 2.5 && kind < 3.5) curve += sin(p.x * 8.0 - t * 3.0) * 0.11;
		if (kind > 3.5 && kind < 5.5) {
			tail = max(-travel_span, head - 0.62);
			curve += sin(p.x * 7.0 - t * 3.0) * 0.075;
			width = kind < 4.5 ? 0.13 : 0.22;
		}
		if (kind > 5.5 && kind < 7.5) { width = 0.025; tail = max(-travel_span, head - 0.70); }
		if (kind > 7.5 && kind < 8.5) {
			// Summoned thunder uses a downward axis supplied by the caller.
			float segment = (p.x + 1.0) * 4.5;
			float zig = mix(sin(floor(segment) * 7.1 + seed_value), sin((floor(segment) + 1.0) * 7.1 + seed_value), fract(segment));
			curve = zig * 0.14;
			width = 0.055;
			tail = -travel_span;
		}
		if (kind > 8.5 && kind < 9.5) { width = 0.22; curve += sin(p.x * 6.0 - t * 4.0) * 0.09; }
		if (kind > 11.5 && kind < 12.5) { width = 0.27; curve += 0.06; }
		if (kind > 12.5 && kind < 13.5) width = 0.032;
		body = stroke(p, tail, head + 0.04, curve, width);
		wash = stroke(p, tail - 0.05, head + 0.025, curve + 0.02, width * 1.58) * 0.22;
		if (kind < 3.5) {
			// Staggered dry bristles describe force, without a floating anatomical fist.
			float bristles = stroke(p, tail - 0.12, head - 0.06, curve - width * 0.92 + p.x * 0.045, width * 0.20);
			bristles = max(bristles, stroke(p, tail - 0.06, head - 0.10, curve + width * 0.76, width * 0.14));
			body = max(body, bristles * 0.66);
			wash *= 1.35;
		}
		if (kind > 6.5 && kind < 7.5) {
			float curl = (1.0 - launch) * 0.22;
			body = max(stroke(p, tail, head, curl + curve, 0.045), stroke(p, tail, head, -curl - curve, 0.045));
		}
		if (kind > 9.5 && kind < 10.5) {
			float jaw = (1.0 - launch) * 0.22 + 0.035;
			body = max(stroke(p, tail, head, jaw, 0.055), stroke(p, tail, head, -jaw, 0.055));
		}
		if (kind > 10.5 && kind < 11.5) {
			body = max(body, max(stroke(p, tail + 0.03, head, curve + 0.10, 0.038), stroke(p, tail, head - 0.04, curve - 0.10, 0.035)));
		}
		fade = 1.0 - smoothstep(contact_progress + 0.04, 0.86, t);
	} else if (phase < 1.5) {
		// Brief compression, then a directional release; the hit is not a circular flash.
		float burst = 1.0 - pow(1.0 - clamp((t - 0.045) / 0.28, 0.0, 1.0), 3.0);
		vec2 q = (p - vec2(burst * 0.12, 0.0)) * vec2(0.85, 1.25);
		float radius = 0.12 - min(t / 0.045, 1.0) * 0.035 + burst * 0.30;
		float d = length(q) + (coarse - 0.5) * 0.20 + (grain - 0.5) * 0.065;
		body = (1.0 - smoothstep(radius * 0.78, radius, d)) * (0.72 + grain * 0.28);
		wash = (1.0 - smoothstep(radius * 0.96, radius + 0.072, d)) * 0.24;
		float jet = stroke(p, 0.03, 0.18 + burst * 0.47, sin(p.x * 9.0 + seed_value) * 0.045, 0.075);
		body = max(body, jet * (1.0 - smoothstep(0.22, 0.72, t)));
		if (kind < 3.5) {
			float bend = burst * 0.18;
			float flank = stroke(p, -0.15, 0.26 + burst * 0.40, bend + p.x * 0.18, 0.033);
			flank = max(flank, stroke(p, -0.11, 0.20 + burst * 0.37, -bend - p.x * 0.13, 0.025));
			body = max(body, flank * 0.74 * (1.0 - smoothstep(0.35, 0.8, t)));
		}
		if (kind > 5.5 && kind < 7.5) {
			// Soul attacks fold inward rather than producing an outward explosion.
			float fold = (1.0 - burst) * 0.28;
			body = stroke(p, -0.58, 0.48, fold * sin(p.x * 7.0), kind < 6.5 ? 0.022 : 0.050);
			wash *= 0.42;
		}
		if (kind > 7.5 && kind < 8.5) body = max(body * 0.62, stroke(p, -0.52, 0.56, sin(p.x * 18.0) * 0.025, 0.032));
		if (kind > 8.5 && kind < 9.5) {
			q.y += burst * 0.24;
			float flames = sin(atan(q.y, q.x) * 5.0 + seed_value) * 0.07;
			body = (1.0 - smoothstep(0.15, radius + 0.08, length(q) + flames + (coarse - 0.5) * 0.18)) * (0.50 + grain * 0.40);
			wash *= 1.30;
		}
		if (kind > 9.5 && kind < 10.5) {
			float jaw = (1.0 - burst) * 0.20 + 0.055;
			body = max(stroke(p, -0.40, 0.46, jaw + p.x * 0.12, 0.036), stroke(p, -0.40, 0.46, -jaw - p.x * 0.12, 0.036));
			wash *= 0.60;
		}
		if (kind > 10.5 && kind < 11.5) {
			body = 0.0;
			for (int i = 0; i < 3; i++) body = max(body, stroke(p, -0.48, 0.58, (float(i) - 1.0) * 0.14 + p.x * 0.23, 0.031));
		}
		if (kind > 11.5 && kind < 12.5) { wash *= 1.10; body = max(body, stroke(p, -0.22, 0.60, 0.12, 0.18)); }
		if (kind > 12.5 && kind < 13.5) body = stroke(p, -0.65, 0.70, 0.0, 0.026);
		// Dry islands, not luminous particles or regular radial spokes.
		float erosion = smoothstep(0.30, 0.96, t);
		body *= smoothstep(erosion * 0.72 - 0.12, erosion * 0.72 + 0.10, grain);
	} else if (phase < 2.5) {
		float breathe = sin(t * 3.14159);
		float x = 0.30 + (1.0 - breathe) * 0.10;
		float curve = p.y * p.y * 0.17;
		float a = abs(abs(p.x) - x - curve + (coarse - 0.5) * 0.035);
		body = (1.0 - smoothstep(0.012, 0.048, a)) * (1.0 - smoothstep(0.25, 0.78, abs(p.y))) * (0.40 + grain * 0.35);
		wash = (1.0 - smoothstep(0.01, 0.16, a)) * 0.17 * (1.0 - smoothstep(0.15, 0.70, abs(p.y)));
		if (kind > 14.5) {
			float breath_curve = sin(p.y * 3.8 + t * 2.0) * 0.16;
			float breath = abs(p.x - breath_curve) + (coarse - 0.5) * 0.025;
			body = (1.0 - smoothstep(0.014, 0.065, breath)) * (1.0 - smoothstep(0.18, 0.75, abs(p.y + t * 0.20))) * (0.35 + grain * 0.35);
			wash = (1.0 - smoothstep(0.03, 0.20, breath)) * 0.19 * (1.0 - smoothstep(0.10, 0.70, abs(p.y)));
		}
		fade = breathe;
	} else if (phase < 3.5) {
		vec2 q = p + vec2(0.0, t * 0.17);
		float mass = length(q * vec2(1.30, 0.84)) + (coarse - 0.5) * 0.26;
		float radius = mix(0.57, 0.20, smoothstep(0.0, 0.75, t));
		body = (1.0 - smoothstep(radius * 0.65, radius, mass)) * smoothstep(t * 0.78 - 0.12, t * 0.78 + 0.15, grain);
		wash = (1.0 - smoothstep(radius, radius + 0.13, mass)) * 0.20;
	} else {
		// Evasion is a sparse sliding afterstroke with no contact bloom.
		body = stroke(p, -0.48 + t * 0.28, 0.42 + t * 0.25, 0.07 + sin(p.x * 5.0) * 0.04, 0.025) * 0.34;
	}
	// Pigment pooling, a diluted outer glaze and paper-sized granulation.
	float paper = texture(ink_noise, SCREEN_UV * vec2(18.0, 13.0) + vec2(seed_value)).r;
	float density = clamp(body * 1.2, 0.0, 1.0);
	vec3 glaze = mix(ink_color.rgb, vec3(0.76, 0.72, 0.60), 0.42);
	vec3 deposit = ink_color.rgb * mix(0.52, 1.17, grain);
	vec3 color = mix(glaze, deposit, smoothstep(0.05, 0.80, density));
	color *= mix(0.90, 1.06, paper);
	color = mix(color, vec3(0.31, 0.14, 0.09), critical * 0.20);
	float alpha = (body + wash) * fade * smoothstep(0.0, 0.018, t);
	COLOR = vec4(color, clamp(alpha * ink_color.a, 0.0, 0.95));
}
