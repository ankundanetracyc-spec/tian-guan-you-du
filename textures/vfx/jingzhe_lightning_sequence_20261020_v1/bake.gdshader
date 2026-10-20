shader_type canvas_item;
render_mode unshaded;
uniform float clock = 0.0;
uniform int mode = 0;

float hash(float n) { return fract(sin(n * 127.13 + 15.79) * 43758.5453); }
float grain(vec2 p) { return fract(sin(dot(floor(p * 1400.0),vec2(12.9898,78.233))) * 43758.5453); }
vec2 segment(vec2 p, vec2 a, vec2 b) {
	vec2 ab=b-a;
	float u=clamp(dot(p-a,ab)/max(dot(ab,ab),.000001),0.0,1.0);
	return vec2(length(p-a-ab*u),u);
}
vec2 trunk(float i) {
	float u=i/22.0;
	float x=.5+sin(u*15.0+.6)*.060*sin(u*3.14159)+(hash(i+14.0)-.5)*.085*sin(u*3.14159);
	return vec2(x,.045+u*.84);
}
vec4 falling_bolt(vec2 uv,float t) {
	float reveal=clamp((t-.075)/.225,0.0,1.0);
	float distance_to_bolt=10.0;
	float branch_distance=10.0;
	for(int j=0;j<22;j++) {
		vec2 d=segment(uv,trunk(float(j)),trunk(float(j+1)));
		if ((float(j)+d.y)/22.0 <= reveal) distance_to_bolt=min(distance_to_bolt,d.x);
	}
	for(int b=0;b<6;b++) {
		float root_index=3.0+floor(float(b)*2.8);
		float start_u=root_index/22.0;
		vec2 root=trunk(root_index);
		float side=b%2==0 ? -1.0 : 1.0;
		vec2 previous=root;
		for(int j=0;j<5;j++) {
			float u=float(j+1)/5.0;
			vec2 next=root+vec2(side*u*(.13+hash(float(b))*.10),u*(.09+hash(float(b)+3.0)*.12));
			next.x+=(hash(float(j+b*7))-.5)*.035*sin(u*3.14159);
			vec2 d=segment(uv,previous,next);
			if(start_u+u*.13<=reveal) branch_distance=min(branch_distance,d.x);
			previous=next;
		}
	}
	float main_flash=smoothstep(.275,.30,t)*(1.0-smoothstep(.37,.53,t));
	float re_strike=exp(-pow((t-.47)/.028,2.0))*.68;
	float tail=(1.0-smoothstep(.49,1.10,t))*.18;
	float energy=t<.30 ? .26 : main_flash+re_strike+tail;
	float sheath=exp(-pow(distance_to_bolt/.013,2.0))*.32;
	float pigment=exp(-pow(distance_to_bolt/.007,2.0))*(.72+grain(uv)*.20);
	float core=exp(-pow(distance_to_bolt/.0024,2.0));
	float forks=exp(-pow(branch_distance/.0042,2.0))*.58;
	float fork_core=exp(-pow(branch_distance/.0014,2.0))*.55;
	float alpha=(sheath+pigment+core*.36+forks)*energy;
	float white=clamp(core*.97+fork_core*.83,0.0,1.0);
	vec3 color=mix(vec3(.32,.47,.69),vec3(.88,.94,.97),white);
	color=mix(color,vec3(.55,.53,.72),sheath*.34);
	// A small charge knot at the top gives the descending leader a clear source.
	float knot=exp(-length((uv-vec2(.5,.045))*vec2(1.0,2.0))/.035);
	alpha+=knot*.14*smoothstep(.03,.12,t)*(1.0-smoothstep(.26,.52,t));
	alpha*=1.0-smoothstep(1.10,1.16,t);
	return vec4(color,clamp(alpha,0.0,.98));
}
vec4 ground_remainder(vec2 uv,float t) {
	float energy=smoothstep(0.0,.035,t)*(1.0-smoothstep(.08,.68,t));
	float distance_to_arc=10.0;
	for(int b=0;b<7;b++) {
		float side=b%2==0 ? -1.0 : 1.0;
		vec2 a=vec2(.5,.57);
		for(int j=0;j<4;j++) {
			float u=float(j+1)/4.0;
			vec2 next=vec2(.5+side*u*(.16+hash(float(b))*.21),.57-u*(.06+hash(float(b)+4.0)*.25));
			next.y+=(hash(float(j+b*9))-.5)*.04;
			distance_to_arc=min(distance_to_arc,segment(uv,a,next).x);
			a=next;
		}
	}
	float core=exp(-pow(distance_to_arc/.0028,2.0));
	float color_edge=exp(-pow(distance_to_arc/.009,2.0));
	float halo=exp(-pow(length((uv-vec2(.5,.57))*vec2(.8,2.5))/.15,2.0))*.11;
	float dust=0.0;
	for(int j=0;j<12;j++) {
		float k=float(j);
		vec2 initial=vec2(.5+(hash(k)-.5)*.25,.55-hash(k+18.0)*.08);
		vec2 p=initial+vec2((hash(k+8.0)-.5)*.23,-.08-hash(k+13.0)*.11)*t+vec2(0.0,t*t*.12);
		dust+=exp(-length((uv-p)*vec2(1.0,1.6))/.0035)*.38;
	}
	return vec4(mix(vec3(.34,.51,.70),vec3(.88,.95,.98),clamp(core,0.0,1.0)),clamp((core+color_edge*.35+halo+dust)*energy,0.0,.85));
}
vec4 finger_charge(vec2 uv,float t) {
	float energy=smoothstep(.01,.09,t)*(1.0-smoothstep(.28,.56,t));
	float closest=10.0;
	for(int b=0;b<3;b++) {
		vec2 a=vec2(.5,.55);
		for(int j=0;j<5;j++) {
			float u=float(j+1)/5.0;
			vec2 p=vec2(.5+sin(u*6.0+float(b)*2.1)*(.05+u*.15),.55-u*.24);
			closest=min(closest,segment(uv,a,p).x);
			a=p;
		}
	}
	float core=exp(-pow(closest/.003,2.0));
	float edge=exp(-pow(closest/.011,2.0))*.30;
	return vec4(mix(vec3(.39,.54,.73),vec3(.9,.96,.98),core),clamp((core+edge)*energy*.8,0.0,.8));
}
void fragment() {
	vec4 output_color=mode==0 ? finger_charge(UV,clock) : (mode==1 ? falling_bolt(UV,clock) : ground_remainder(UV,clock));
	vec2 edge=min(UV,vec2(1.0)-UV);
	output_color.a*=smoothstep(0.0,.018,min(edge.x,edge.y));
	COLOR=output_color;
}
