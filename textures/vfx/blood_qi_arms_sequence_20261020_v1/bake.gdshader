shader_type canvas_item;
render_mode unshaded;
uniform sampler2D idle_art : filter_linear;
uniform sampler2D punch_art : filter_linear;
uniform sampler2D guard_art : filter_linear;
uniform float progress=0.0;
uniform int mode=0;

float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
	vec2 i=floor(p), f=fract(p);
	f=f*f*(3.0-2.0*f);
	return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
	float t=clamp(progress,0.0,1.0);
	float pose=smoothstep(.02,.18,t)*(1.0-smoothstep(.52,.94,t));
	vec4 base=texture(idle_art,UV);
	if(mode==2) base=mix(base,texture(punch_art,UV),pose);
	if(mode==3) base=mix(base,texture(guard_art,UV),pose);
	float n=noise(UV*vec2(46.0,67.0));
	float small=noise(UV*vec2(130.0,160.0));
	float front=1.0;
	if(mode==0) {
		float edge=mix(1.15,-.15,t);
		front=smoothstep(edge-.08,edge+.06,UV.y+(n-.5)*.10)*smoothstep(0.0,.10,t);
	}
	if(mode==4) {
		float edge=t*1.2-.1;
		front=smoothstep(edge-.10,edge+.10,n)*pow(1.0-t,1.15);
	}
	// A slow pigment current, not a screen-filling bloom. The loop is periodic.
	float cycle=mode==1 ? t*6.283185 : t*3.2;
	float current=pow(max(0.0,sin(UV.y*16.0+UV.x*8.0+cycle)),12.0);
	float ink_density=.88+small*.12;
	float pulse=mode==1 ? .95+.05*sin(cycle) : 1.0;
	float accent=(mode==2||mode==3) ? pose*.16 : .0;
	vec3 color=mix(base.rgb,vec3(.80,.23,.19),current*.17+accent);
	color*=.95+small*.06;
	float alpha=base.a*.76*front*ink_density*pulse;
	vec2 bounds=min(UV,vec2(1.0)-UV);
	alpha*=smoothstep(.0,.012,min(bounds.x,bounds.y));
	COLOR=vec4(color,clamp(alpha,0.0,.86));
}
