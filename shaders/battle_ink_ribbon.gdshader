shader_type canvas_item;
render_mode unshaded, blend_mix;

// A moving ribbon carries pigment. UV is the brush's travel/pressure space,
// not a screen-space line. Layers keep separate speed, breadth and drying.
uniform sampler2D ink_noise : repeat_enable, filter_linear_mipmap;
uniform float progress = 0.0;
uniform float seed_value = 0.0;
uniform float layer = 1.0;
uniform float span = 380.0;
uniform float bend = 68.0;
uniform float contact = 0.0;
uniform vec4 ink_color : source_color = vec4(0.04, 0.05, 0.048, 1.0);
varying float path_position;

void vertex() {
	float delay = layer > 1.5 ? 0.04 * (layer - 1.0) : 0.0;
	float t = max(progress - delay, 0.0);
	float drive = clamp(t / 0.30, 0.0, 1.0);
	float head = 0.008 + 0.992 * (1.0 - pow(1.0 - drive, 3.0));
	float tail = smoothstep(0.34, 0.98, t) * 0.78;
	float u = mix(tail, head, UV.x);
	path_position = u;
	float angle = mix(-2.94, -0.20, u);
	float tilt = sin(u * 3.14159 + seed_value) * 0.012;
	vec2 center = vec2(cos(angle) * span * 0.50, sin(angle) * bend);
	vec2 normal = normalize(vec2(cos(angle) * 0.32, sin(angle)));
	float pressure = pow(max(sin(UV.x * 3.14159), 0.0), 0.72) * (0.40 + UV.x * 1.10);
	float width = span * 0.043 * pressure;
	if (contact > 0.5) {
		center = vec2((u - 0.5) * span, sin(u * 3.14159) * bend * 0.16);
		normal = vec2(0.0, 1.0);
		width *= 0.80;
	}
	float n = texture(ink_noise, vec2(u * 1.6 + seed_value, 0.23 + layer * 0.17)).r;
	center.y += sin(u * 3.14159) * sin(u * 4.5 + seed_value) * bend * 0.12;
	width *= 0.76 + n * 0.58;
	if (layer < 0.5) width *= 2.60 + t * 1.30;
	if (layer > 1.5) width *= layer < 2.5 ? 0.34 : 0.12;
	float slip = (layer - 1.0) * (2.2 + t * 5.0);
	center += normal * slip;
	center.x += sin(t * 3.0) * (layer < 0.5 ? 5.0 : 12.0);
	center.y += smoothstep(0.38, 0.90, t) * 10.0;
	VERTEX = center + normal * ((UV.y - 0.5) * 2.0 * width) + vec2(0.0, tilt * span);
}

void fragment() {
	float t = progress;
	vec2 flow = vec2(path_position * 1.8 + seed_value, UV.y * 0.85 + layer * 0.36);
	vec2 warp = vec2(texture(ink_noise, flow + vec2(t * 0.14, 0.0)).r,
		texture(ink_noise, flow.yx + 4.1).r) - 0.5;
	float pigment = texture(ink_noise, flow + warp * 0.25 - vec2(t * 0.13, t * 0.035)).r;
	float fiber = texture(ink_noise, vec2(path_position * 0.18 + seed_value, UV.y * 0.65) + warp * 0.035).r;
	float y = abs((UV.y - 0.5) * 2.0 + warp.y * 0.12);
	float edge = 1.0 - smoothstep(0.52 + pigment * 0.19, 0.84 + pigment * 0.12, y);
	float dry = 1.0 - smoothstep(0.66, 0.79, fiber);
	float erosion = smoothstep(0.38, 0.95, t);
	float remain = smoothstep(erosion * 0.75 - 0.12, erosion * 0.75 + 0.12, pigment);
	float ends = smoothstep(0.0, 0.06, UV.x) * (1.0 - smoothstep(0.86, 1.0, UV.x));
	float alpha = edge * mix(0.25, 0.96, dry) * remain * ends;
	vec3 color = ink_color.rgb * mix(0.70, 1.24, pigment);
	if (layer < 0.5) {
		// Dilute wash floats behind the dense stroke, then breaks into wet islands.
		alpha = pow(edge, 1.4) * smoothstep(0.12, 0.80, pigment) * remain * ends * 0.16;
		color = mix(ink_color.rgb, vec3(0.32, 0.40, 0.36), 0.68);
	} else if (layer > 1.5) {
		alpha *= (layer < 2.5 ? 0.65 : 0.48) * smoothstep(0.25, 0.46, pigment);
		color *= 0.76;
	}
	alpha *= smoothstep(0.0, 0.026, t) * (1.0 - smoothstep(0.70, 1.0, t));
	COLOR = vec4(color, clamp(alpha * ink_color.a, 0.0, 0.96));
}
