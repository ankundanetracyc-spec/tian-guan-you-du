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
void fragment() {
	float t=progress;
	vec2 uv=UV;
	vec2 focus = mode==0 ? vec2(.85,.50) : (mode==1 ? vec2(.75,.50) : vec2(.5,.54));
	float spread=1.0;
	if (mode==0) spread=mix(.24,1.06,pow(clamp(t/.28,0.0,1.0),.33));
	if (mode==1) spread=.98 + sin(t*18.0)*.012*(1.0-t);
	if (mode==2) spread=mix(.82,1.02,smoothstep(0.0,.35,t))*(1.0-.25*smoothstep(.55,1.0,t));
	vec2 source_uv=focus+(uv-focus)/spread;
	float erosion=smoothstep(.30,1.0,t)*.70;
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
	a*=1.0-smoothstep(grain-.14,grain+.14,erosion);
	a*=1.0-smoothstep(.72,1.0,t);
	vec3 color=stamp.rgb;
	float glint=0.0;
	if (mode==0) glint=(1.0-smoothstep(.04,.16,t))*exp(-pow(length(uv-focus)*18.0,2.0));
	if (mode==1) glint=exp(-pow((t-.28)*18.0,2.0))*exp(-pow(length(uv-focus)*14.0,2.0));
	if (mode==2) glint=.25*sin(t*3.14159)*exp(-pow(length(uv-focus)*7.0,2.0));
	color=mix(color,vec3(.99,.94,.77),glint);
	float border=smoothstep(.005,.025,uv.x)*smoothstep(.005,.025,uv.y)*smoothstep(.005,.025,1.0-uv.x)*smoothstep(.005,.025,1.0-uv.y);
	COLOR=vec4(color,a*border);
}
