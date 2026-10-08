shader_type canvas_item;
render_mode blend_mix;

uniform float time : hint_range(0.0, 100.0) = 0.0;
uniform float diffusion_str : hint_range(0.0, 0.01) = 0.003;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i+vec2(1,0)), f.x),
	           mix(hash(i+vec2(0,1)), hash(i+vec2(1,1)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0; float a = 0.5; float fr = 1.0;
	for (int i = 0; i < 4; i++) {
		v += a * noise(p * fr + time * 0.15 * float(i));
		fr *= 2.1; a *= 0.5;
	}
	return v;
}

void fragment() {
	float step_sz = 0.004;
	// 直接用 TEXTURE（SubViewport 纹理）
	float c  = texture(TEXTURE, UV).a;
	float n  = texture(TEXTURE, UV + vec2(0, -step_sz)).a;
	float s  = texture(TEXTURE, UV + vec2(0, step_sz)).a;
	float e  = texture(TEXTURE, UV + vec2(step_sz, 0)).a;
	float w  = texture(TEXTURE, UV + vec2(-step_sz, 0)).a;
	float mx = max(max(max(n, s), max(e, w)), c);
	float edge = mx - c;
	float ef = smoothstep(0.0, 0.3, edge);

	vec2 nd = vec2(fbm(UV * 50.0 + 0.5) - 0.5, fbm(UV * 40.0 + 1.3) - 0.5);
	float ds = texture(TEXTURE, UV + nd * step_sz * 3.0).a;

	float d = c + (mx - c) * diffusion_str * 80.0;
	d += ef * (ds - c) * diffusion_str * 60.0;
	d += fbm(UV * 80.0) * diffusion_str * 30.0 * ef * (mx - c);

	float blur = c * 0.5;
	float bs = step_sz * 2.0 * (0.5 + ef);
	blur += texture(TEXTURE, UV + vec2(bs, 0)).a * 0.1;
	blur += texture(TEXTURE, UV + vec2(-bs, 0)).a * 0.1;
	blur += texture(TEXTURE, UV + vec2(0, bs)).a * 0.1;
	blur += texture(TEXTURE, UV + vec2(0, -bs)).a * 0.1;

	float fa = clamp(mix(d, blur, 0.3), 0.0, 1.0);
	COLOR = vec4(0.04, 0.04, 0.09, fa);
}
