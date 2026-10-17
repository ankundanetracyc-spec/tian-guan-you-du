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
vec4 drift_fragments(vec2 uv, vec2 focus, float t) {
	float alpha = 0.0;
	for (int i=0; i<14; i++) {
		if (!impact && i>=8) break;
		float seed=float(i)+3.0;
		float a=hash(vec2(seed,5.0)), b=hash(vec2(seed,13.0));
		float angle=impact ? a*6.283185 : mix(-2.3,1.3,a);
		vec2 direction=vec2(cos(angle),sin(angle));
		vec2 start=focus+direction*(impact ? mix(.06,.25,b) : mix(.36,.43,b));
		float source_alpha=texture(brush,start).a;
		float birth=.20+.25*hash(vec2(seed,23.0));
		float life=.28+.42*hash(vec2(seed,37.0));
		float age=clamp((t-birth)/life,0.0,1.0);
		vec2 velocity=direction*mix(.035,.10,b)+vec2(.015,-.012);
		vec2 at=start+velocity*(1.0-exp(-age*3.0))+vec2(0,.018*age*age);
		vec2 delta=uv-at;
		vec2 local=vec2(dot(delta,direction),dot(delta,vec2(-direction.y,direction.x)));
		local/=vec2(.005+.008*b,.003+.004*a);
		float shape=exp(-dot(local,local)*2.3);
		float fade=smoothstep(0.0,.12,age)*pow(1.0-smoothstep(.18,1.0,age),1.4);
		alpha=max(alpha,shape*fade*source_alpha*.78);
	}
	return vec4(.15,.27,.23,alpha);
}
void fragment() {
	float t = progress;
	vec2 uv = UV;
	vec2 focus = impact ? vec2(.45,.45) : vec2(.45,.5);
	float spread = impact ? mix(.30, 1.15, pow(min(t / .65, 1.0), .35)) : 1.0;
	vec2 sample_uv = focus + (uv - focus) / spread;
	float erosion = smoothstep(.30, 1.0, t) * .50;
	sample_uv += vec2(noise(uv * 10.0 + 7.0)-.5, noise(uv * 11.0 + 31.0)-.5) * erosion * .009;
	sample_uv += vec2(-.009,.006)*smoothstep(.3,1.0,t);
	vec4 stamp = texture(brush, sample_uv);
	float inside = step(0.0, sample_uv.x) * step(sample_uv.x, 1.0) * step(0.0, sample_uv.y) * step(sample_uv.y, 1.0);
	float a = stamp.a * inside;
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
	float fade_start=.28+.25*noise(uv*7.0+19.0);
	a *= reveal * (1.0-smoothstep(fade_start,min(.98,fade_start+.43),t));
	float dry = noise(uv * 190.0);
	vec3 ink = mix(vec3(.035,.065,.061), vec3(.22,.40,.35), dry * .55 + erosion * .3);
	ink = mix(ink, vec3(.93,.91,.79), front * .92);
	vec4 fragments=drift_fragments(uv,focus,t);
	ink=mix(ink,fragments.rgb,fragments.a/(a+fragments.a+.0001));
	a=max(a,fragments.a)*(1.0-smoothstep(.95,1.0,t));
	float edge = smoothstep(.005,.025,uv.x) * smoothstep(.005,.025,uv.y) * smoothstep(.005,.025,1.0-uv.x) * smoothstep(.005,.025,1.0-uv.y);
	COLOR = vec4(ink, a * edge);
}
