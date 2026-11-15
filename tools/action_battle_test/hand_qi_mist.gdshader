shader_type canvas_item;
render_mode unshaded;
uniform float phase = 0.0;

float noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	vec2 v = vec2(127.1, 311.7);
	float a = fract(sin(dot(cell, v)) * 43758.5453);
	float b = fract(sin(dot(cell + vec2(1, 0), v)) * 43758.5453);
	float c = fract(sin(dot(cell + vec2(0, 1), v)) * 43758.5453);
	float d = fract(sin(dot(cell + vec2(1, 1), v)) * 43758.5453);
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
	vec2 flow = UV * vec2(4.0, 3.0) + vec2(-phase * 2.0, phase * 0.7);
	float density = noise(flow) * 0.64 + noise(flow * 2.17 + 3.2) * 0.36;
	COLOR.rgb = mix(vec3(0.84, 0.84, 0.79), vec3(0.99, 0.99, 0.95), smoothstep(0.2, 0.8, density));
	COLOR.a *= 0.32 + 0.68 * smoothstep(0.16, 0.68, density);
}
