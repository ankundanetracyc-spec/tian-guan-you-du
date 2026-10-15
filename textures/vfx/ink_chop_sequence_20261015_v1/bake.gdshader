shader_type canvas_item;
render_mode unshaded;

uniform sampler2D brush : filter_linear, repeat_disable;
uniform float progress = 0.0;
uniform bool impact = false;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1,0)), f.x), mix(hash(i + vec2(0,1)), hash(i + vec2(1)), f.x), f.y);
}
void fragment() {
	float t = progress;
	vec2 uv = UV;
	vec2 focus = impact ? vec2(.45,.45) : vec2(.45,.5);
	float spread = impact ? mix(.30, 1.15, pow(min(t / .65, 1.0), .35)) : 1.0;
	vec2 sample_uv = focus + (uv - focus) / spread;
	float erosion = smoothstep(.30, 1.0, t) * .78;
	sample_uv += vec2(noise(uv * 10.0 + 7.0), noise(uv * 11.0 + 31.0)) * erosion * .035;
	vec4 stamp = texture(brush, sample_uv);
	float inside = step(0.0, sample_uv.x) * step(sample_uv.x, 1.0) * step(0.0, sample_uv.y) * step(sample_uv.y, 1.0);
	float a = stamp.a * inside;
	float fleck = noise(uv * 50.0) * .65 + noise(uv * 160.0) * .35;
	float reveal = 1.0;
	float front = 0.0;
	if (!impact) {
		float angle = atan(uv.y - .5, uv.x - .45);
		float arc = clamp((angle + 2.60) / 4.0, 0.0, 1.0);
		float lead = min(t / .30, 1.18);
		reveal = 1.0 - smoothstep(lead - .05, lead + .015, arc);
		front = (1.0 - smoothstep(.015, .08, abs(arc - lead))) * (1.0 - smoothstep(.35,.55,t));
		float rim = exp(-pow((length(uv-focus)-.35)*48.0,2.0));
		front = max(front, rim * smoothstep(.10,.22,t) * (1.0-smoothstep(.38,.60,t)));
	} else {
		reveal = smoothstep(0.0, .055, t);
		front = (1.0 - smoothstep(.04, .17, t)) * (1.0 - smoothstep(.03,.25,length(uv-focus)));
	}
	a *= reveal * (1.0 - smoothstep(fleck - .12, fleck + .12, erosion * .85));
	a *= 1.0 - smoothstep(.76, 1.0, t);
	float dry = noise(uv * 190.0);
	vec3 ink = mix(vec3(.035,.065,.061), vec3(.22,.40,.35), dry * .55 + erosion * .3);
	ink = mix(ink, vec3(.93,.91,.79), front * .92);
	float edge = smoothstep(.005,.025,uv.x) * smoothstep(.005,.025,uv.y) * smoothstep(.005,.025,1.0-uv.x) * smoothstep(.005,.025,1.0-uv.y);
	COLOR = vec4(ink, a * edge);
}
