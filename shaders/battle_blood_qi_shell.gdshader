shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform float effect_time=0.0;
uniform float presence=1.0;
uniform float activity=0.0;
uniform float near_plane=1.0;
varying vec3 shell_normal;
varying vec2 shell_position;

void vertex() {
	shell_normal=COLOR.rgb*2.0-1.0;
	shell_position=VERTEX;
	COLOR=vec4(1.0);
}

float grain(vec2 p) {
	return .5+.5*sin(p.x*.061+sin(p.y*.043)*1.4)*sin(p.y*.082-p.x*.017);
}

void fragment() {
	vec3 n=normalize(shell_normal);
	float facing=clamp(n.z,0.0,1.0);
	float rim=pow(1.0-facing,1.25);
	float grazed=pow(max(0.0,dot(n,normalize(vec3(-.47,-.58,.67)))),9.0);
	float fiber=grain(shell_position+vec2(effect_time*2.2,-effect_time*1.7));
	float current=0.0;
	for(int i=0;i<3;i++) {
		float head=-.15+1.32*fract(effect_time*.22-float(i)/3.0);
		current+=exp(-pow((UV.x-head)/.069,2.0));
	}
	float filament=pow(.5+.5*cos(UV.y*18.8496+UV.x*4.0-effect_time*.24),13.0);
	float flowing_edge=rim*(.55+.45*current);
	float alpha=.13+fiber*.020+flowing_edge*.48+grazed*.18;
	alpha+=current*(.09+filament*.09)+activity*.09;
	vec3 color=mix(vec3(.58,.012,.045),vec3(.88,.085,.14),clamp(.25+rim*.50+current*.15,0.0,1.0));
	color=mix(color,vec3(1.0,.53,.56),clamp(grazed*.60+current*rim*.43+filament*current*.15,0.0,.78));
	float formation=1.0-smoothstep(presence*1.20-.14,presence*1.20-.04,UV.x+(fiber-.5)*.025);
	float root_fade=smoothstep(.005,.105,UV.x);
	float clear=smoothstep(0.0,.10,presence);
	COLOR=vec4(color,clamp(alpha*formation*root_fade*clear*mix(.94,1.0,near_plane),0.0,.72));
}
