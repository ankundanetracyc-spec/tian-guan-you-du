shader_type canvas_item;
render_mode unshaded;
uniform sampler2D brush : filter_linear, repeat_disable;
uniform float progress = 0.0;
uniform int mode = 0;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
	vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
	return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1)),f.x),f.y);
}
vec4 quiet_breath(vec2 uv,float t) {
	float alpha=0.0;
	vec3 color=vec3(.38,.58,.48);
	for (int i=0;i<7;i++) {
		float seed=float(i)+4.0;
		float a=hash(vec2(seed,9.0)), b=hash(vec2(seed,21.0));
		float birth=.03+.30*a, life=.42+.34*b;
		float age=clamp((t-birth)/life,0.0,1.0);
		vec2 start=vec2(.27+.46*a,.68-.13*b);
		vec2 at=start+vec2((.5-start.x)*.18,-.18)*(1.0-exp(-age*2.6));
		at.x+=sin(age*3.0+seed)*.012*age;
		vec2 local=(uv-at)/vec2(.012+.006*b,.014+.010*a);
		float dot_alpha=exp(-dot(local,local)*2.0);
		float fade=smoothstep(0.0,.15,age)*pow(1.0-smoothstep(.25,1.0,age),1.7);
		float value=dot_alpha*fade*.90;
		if (value>alpha) color=mix(vec3(.48,.72,.60),vec3(.99,.94,.78),b*.75);
		alpha=max(alpha,value);
	}
	for (int i=0;i<2;i++) {
		float seed=float(i);
		float phase=clamp((t-.10-seed*.13)/.62,0.0,1.0);
		float x=.37+seed*.22+.032*sin((uv.y-.35)*12.0+phase*1.5);
		float line=exp(-pow((uv.x-x)/.006,2.0));
		float extent=smoothstep(.35,.40,uv.y)*(1.0-smoothstep(.57,.63,uv.y));
		float fade=sin(phase*3.14159)*sin(phase*3.14159);
		alpha=max(alpha,line*extent*fade*.26);
	}
	return vec4(color,alpha*smoothstep(0.0,.03,t)*(1.0-smoothstep(.92,1.0,t)));
}
vec4 drifting_flecks(vec2 uv,float t,vec2 focus) {
	float alpha=0.0; vec3 color=vec3(.5,.58,.49);
	for (int i=0;i<10;i++) {
		float seed=float(i)+7.0;
		float a=hash(vec2(seed,13)), b=hash(vec2(seed,29));
		float angle=a*6.283185;
		vec2 ray=vec2(cos(angle),sin(angle));
		vec2 start=focus+ray*mix(.04,.19,b);
		vec4 sample_ink=texture(brush,start);
		float birth=.23+.20*a, life=.30+.36*b;
		float age=clamp((t-birth)/life,0.0,1.0);
		vec2 velocity=mode==0 ? vec2(-.07-.04*b,-.02+.04*a) : ray*mix(.035,.08,b)+vec2(.02,-.01);
		vec2 at=start+velocity*(1.0-exp(-age*2.4))+vec2(0,.016*age*age);
		vec2 local=(uv-at)/vec2(.004+.005*b,.003+.003*a);
		float fade=smoothstep(0.0,.12,age)*pow(1.0-smoothstep(.18,1.0,age),1.5);
		float value=exp(-dot(local,local)*2.0)*fade*sample_ink.a*.72;
		if (value>alpha) color=sample_ink.rgb;
		alpha=max(alpha,value);
	}
	return vec4(color,alpha);
}
void fragment() {
	float t=progress;
	vec2 uv=UV;
	vec2 focus = mode==0 ? vec2(.85,.50) : (mode==1 ? vec2(.75,.50) : vec2(.5,.54));
	float spread=1.0;
	if (mode==0) spread=mix(.24,1.06,pow(clamp(t/.28,0.0,1.0),.33));
	if (mode==1) spread=.98 + sin(t*18.0)*.012*(1.0-t);
	if (mode==2) spread=mix(.82,1.02,smoothstep(0.0,.35,t))*(1.0-.25*smoothstep(.55,1.0,t));
	vec2 source_uv=focus+(uv-focus)/spread;
	float erosion=smoothstep(.30,1.0,t)*.35;
	if (mode==0) source_uv+=vec2(t*.055,-t*.02);
	if (mode==1) source_uv.x+=sin(uv.y*35.0-t*13.0)*.006*sin(t*3.14159);
	if (mode==2) {
		float angle=-.18*sin(t*3.14159);
		vec2 p=source_uv-focus;
		source_uv=focus+mat2(vec2(cos(angle),sin(angle)),vec2(-sin(angle),cos(angle)))*p;
		source_uv.y+=t*.035;
	}
	vec4 stamp=texture(brush,source_uv);
	float inside=step(0.0,source_uv.x)*step(source_uv.x,1.0)*step(0.0,source_uv.y)*step(source_uv.y,1.0);
	float reveal=smoothstep(0.0,.045,t);
	if (mode==1) reveal*=1.0-smoothstep(min(t/.21,1.1)-.06,min(t/.21,1.1)+.05,uv.y);
	if (mode==2) reveal=smoothstep(0.0,.22,t);
	float grain=noise(uv*45.0)*.68+noise(uv*145.0)*.32;
	float a=stamp.a*inside*reveal;
	float fade_start=.30+.23*noise(uv*7.0+11.0);
	a*=1.0-smoothstep(fade_start,min(.98,fade_start+.43),t);
	vec3 color=stamp.rgb;
	float glint=0.0;
	if (mode==0) glint=(1.0-smoothstep(.04,.16,t))*exp(-pow(length(uv-focus)*18.0,2.0));
	if (mode==1) glint=exp(-pow((t-.28)*18.0,2.0))*exp(-pow(length(uv-focus)*14.0,2.0));
	if (mode==2) glint=.25*sin(t*3.14159)*exp(-pow(length(uv-focus)*7.0,2.0));
	color=mix(color,vec3(.99,.94,.77),glint);
	vec4 flecks=drifting_flecks(uv,t,focus);
	color=mix(color,flecks.rgb,flecks.a/(a+flecks.a+.0001));
	a=max(a,flecks.a)*(1.0-smoothstep(.95,1.0,t));
	float border=smoothstep(.005,.025,uv.x)*smoothstep(.005,.025,uv.y)*smoothstep(.005,.025,1.0-uv.x)*smoothstep(.005,.025,1.0-uv.y);
	COLOR=vec4(color,a*border);
	if (mode==2) {
		vec4 breath=quiet_breath(uv,t);
		COLOR=vec4(breath.rgb,breath.a*border);
	}
}
