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
vec2 breath_curve(float u,int strand) {
	vec2 a=strand==0 ? vec2(.14,.66) : vec2(.84,.59);
	vec2 b=strand==0 ? vec2(.23,.25) : vec2(.75,.83);
	vec2 c=strand==0 ? vec2(.77,.20) : vec2(.32,.75);
	vec2 d=strand==0 ? vec2(.56,.51) : vec2(.48,.53);
	float v=1.0-u;
	return v*v*v*a+3.0*v*v*u*b+3.0*v*u*u*c+u*u*u*d;
}
// Two open, tapered breaths converge at the hands. No circular aura or bloom.
vec4 quiet_breath(vec2 uv,float t) {
	float alpha=0.0;
	vec3 color=vec3(.39,.57,.49);
	for (int strand=0;strand<2;strand++) {
		float phase=clamp((t-float(strand)*.12)/.46,0.0,1.0);
		float head=smoothstep(0.0,1.0,phase);
		float closest=10.0;
		float along=0.0;
		for (int j=0;j<24;j++) {
			float u=float(j)/24.0;
			vec2 a=breath_curve(u,strand), b=breath_curve(u+1.0/24.0,strand);
			vec2 ab=b-a;
			float h=clamp(dot(uv-a,ab)/dot(ab,ab),0.0,1.0);
			float distance_to_path=length(uv-a-ab*h);
			if (distance_to_path<closest) { closest=distance_to_path; along=u+h/24.0; }
		}
		float width=(.006+.019*pow(max(0.0,sin(along*3.14159)),1.4));
		float grain=noise(uv*270.0+float(strand)*23.0);
		width*=.75+.35*noise(uv*49.0);
		float edge=1.0-smoothstep(width*.38,width,closest);
		float dry=mix(.60,1.0,smoothstep(.22,.52,grain));
		float trail=smoothstep(head-.48,head-.22,along)*(1.0-smoothstep(head-.015,head+.02,along));
		float release=1.0-smoothstep(.47+float(strand)*.10,.76+float(strand)*.08,t);
		float value=edge*dry*trail*release*.80*smoothstep(0.0,.08,phase);
		vec3 pigment=mix(vec3(.25,.43,.37),vec3(.71,.83,.70),smoothstep(.2,.9,along));
		float ivory=(1.0-smoothstep(width*.12,width*.42,closest))*.85;
		pigment=mix(pigment,vec3(.98,.96,.83),ivory);
		if (value>alpha) color=pigment;
		alpha=max(alpha,value);
		vec2 tip=breath_curve(head,strand);
		float tip_shape=exp(-dot((uv-tip)/vec2(.013,.010),(uv-tip)/vec2(.013,.010))*2.0);
		float tip_alpha=tip_shape*sin(phase*3.14159)*release*.82;
		if (tip_alpha>alpha) color=vec3(.97,.93,.78);
		alpha=max(alpha,tip_alpha);
	}
	// Short paired accents at the gathering point mark the quiet exhale.
	for (int accent=0;accent<2;accent++) {
		float age=clamp((t-.35-float(accent)*.15)/.25,0.0,1.0);
		float x=.52+(uv.y-.52)*.42+float(accent)*.035;
		float stroke=exp(-pow((uv.x-x)/.007,2.0));
		float span=smoothstep(.47,.49,uv.y)*(1.0-smoothstep(.55,.57,uv.y));
		float value=stroke*span*sin(age*3.14159)*.65;
		if (value>alpha) color=vec3(.86,.87,.68);
		alpha=max(alpha,value);
	}
	// Leaf-like flecks stagger their birth, drift and fade through the return to idle.
	for (int i=0;i<11;i++) {
		float seed=float(i)+4.0;
		float a=hash(vec2(seed,9.0)), b=hash(vec2(seed,21.0));
		float birth=i<7 ? .01+.20*a : .50+.14*a;
		float life=i<7 ? .44+.24*b : .28+.12*b;
		float age=clamp((t-birth)/life,0.0,1.0);
		vec2 start=i<7 ? vec2(.23+.54*a,.69-.25*b) : vec2(.44+.15*a,.52);
		vec2 at=start+vec2((.52-start.x)*.36,-.11)*(1.0-exp(-age*2.6));
		at.x+=sin(age*3.0+seed)*.014*age;
		vec2 p=uv-at;
		float angle=seed+age*.6;
		vec2 local=mat2(vec2(cos(angle),sin(angle)),vec2(-sin(angle),cos(angle)))*p;
		local/=vec2(.005+.003*b,.012+.006*a);
		float shape=1.0-smoothstep(.25,1.0,abs(local.x)+local.y*local.y);
		float fade=smoothstep(0.0,.15,age)*pow(1.0-smoothstep(.25,1.0,age),1.7);
		float value=shape*fade*.78;
		if (value>alpha) color=mix(vec3(.48,.70,.59),vec3(.98,.92,.74),b*.8);
		alpha=max(alpha,value);
	}
	return vec4(color,alpha*smoothstep(0.0,.03,t)*(1.0-smoothstep(.94,1.0,t)));
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
