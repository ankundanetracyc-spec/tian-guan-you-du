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
	float x=.5+sin(u*15.0+.6)*.035*sin(u*3.14159)+(hash(i+14.0)-.5)*.052*sin(u*3.14159);
	return vec2(x,-.02+u*.905);
}
vec2 filament(float i,float strand) {
	vec2 p=trunk(i);
	float u=i/22.0;
	p.x+=sin(u*21.0+strand*2.1)*(.014+strand*.005)*sin(u*3.14159);
	p.x+=(strand-1.0)*.019*sin(u*3.14159);
	return p;
}
vec4 falling_bolt(vec2 uv,float t) {
	float reveal=clamp((t-.075)/.225,0.0,1.0);
	float distance_to_bolt=10.0;
	float strand_distance=10.0;
	float branch_distance=10.0;
	for(int j=0;j<22;j++) {
		vec2 d=segment(uv,trunk(float(j)),trunk(float(j+1)));
		if((float(j)+d.y)/22.0<=reveal) {
			distance_to_bolt=min(distance_to_bolt,d.x);
			for(int strand=0;strand<3;strand++)
				strand_distance=min(strand_distance,segment(uv,filament(float(j),float(strand)),filament(float(j+1),float(strand))).x);
		}
	}
	for(int b=0;b<8;b++) {
		float root_index=3.0+floor(float(b)*2.25);
		float start_u=root_index/22.0;
		vec2 root=trunk(root_index);
		float side=b%2==0 ? -1.0 : 1.0;
		vec2 previous=root;
		for(int j=0;j<5;j++) {
			float u=float(j+1)/5.0;
			vec2 next=root+vec2(side*u*(.16+hash(float(b))*.12),u*(.09+hash(float(b)+3.0)*.14));
			next.x+=(hash(float(j+b*7))-.5)*.045*sin(u*3.14159);
			vec2 d=segment(uv,previous,next);
			if(start_u+u*.13<=reveal) branch_distance=min(branch_distance,d.x);
			previous=next;
		}
	}
	// The leader opens into a dense, irregular column. Its edge stays sharp rather
	// than faking thickness with a large gaussian bloom around a thin line.
	float column=smoothstep(.245,.30,t)*(1.0-smoothstep(.44,.64,t));
	float re_strike=exp(-pow((t-.50)/.034,2.0))*.70;
	float tail=(1.0-smoothstep(.55,1.10,t))*.18;
	float energy=t<.30 ? .36 : column+re_strike+tail;
	float width_profile=.071+sin(uv.y*48.0+1.7)*.007;
	float edge_section=uv.y*47.0;
	width_profile+=(mix(hash(floor(edge_section)+31.0),hash(floor(edge_section)+32.0),fract(edge_section))-.5)*.016;
	width_profile*=mix(1.12,.64,smoothstep(.73,.90,uv.y));
	float width=mix(.006,width_profile,clamp(column+re_strike*.56,0.0,1.0));
	float index=clamp((uv.y+.02)/.905,0.0,.9999)*22.0;
	float axis=mix(trunk(floor(index)).x,trunk(floor(index)+1.0).x,fract(index));
	float column_distance=abs(uv.x-axis);
	float ragged=(grain(vec2(uv.x*.45,uv.y*.18))-.5)*.0025;
	float leader_front=-.02+reveal*.905;
	float column_bounds=(1.0-smoothstep(.882,.901,uv.y))
		*(1.0-smoothstep(leader_front-.004,leader_front+.004,uv.y));
	float mantle=(1.0-smoothstep(width-.002,width+.002+ragged,column_distance))*column_bounds;
	float rim=(1.0-smoothstep(width+.002,width+.009,column_distance))*column_bounds;
	float core_width=mix(.0028,.0115,column);
	float core=1.0-smoothstep(core_width*.52,core_width,strand_distance);
	float center_core=1.0-smoothstep(.006,.016,distance_to_bolt);
	// Blue seams remain between the interwoven white discharge channels.
	float seams=.93+.07*grain(vec2(uv.x*.2,uv.y*.1));
	float white=clamp(core*.98+center_core*column*.22,0.0,1.0)*seams;
	float forks=exp(-pow(branch_distance/mix(.003,.0075,column),2.0))*.76;
	float fork_core=exp(-pow(branch_distance/.0022,2.0))*.70;
	float after=1.0-smoothstep(.52,1.10,t);
	float fragments=smoothstep(.30,.66,grain(vec2(uv.x*.2,uv.y*.18)+vec2(0.0,floor(t*18.0)*.03)));
	float breakup=mix(1.0,fragments,smoothstep(.56,.88,t));
	float fibers=.88+.12*grain(uv*.8);
	float alpha=(mantle*.88*fibers+rim*.10+core*.24+forks)*energy*breakup;
	vec3 blue=mix(vec3(.24,.43,.62),vec3(.44,.62,.76),.30+grain(uv*.8)*.40);
	vec3 color=mix(blue,vec3(.91,.97,1.0),clamp(white+fork_core,0.0,1.0));
	color=mix(color,vec3(.51,.52,.74),clamp(rim-mantle,0.0,1.0)*.30);
	float knot=exp(-length((uv-vec2(.5,.045))*vec2(1.0,2.0))/.055);
	alpha+=knot*.22*smoothstep(.03,.12,t)*(1.0-smoothstep(.30,.55,t));
	alpha*=1.0-smoothstep(1.10,1.16,t);
	return vec4(color,clamp(alpha,0.0,.98)*after);
}
vec4 ground_remainder(vec2 uv,float t) {
	float energy=smoothstep(0.0,.035,t)*(1.0-smoothstep(.08,.68,t));
	float distance_to_arc=10.0;
	for(int b=0;b<9;b++) {
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
	float core=exp(-pow(distance_to_arc/.0045,2.0));
	float color_edge=exp(-pow(distance_to_arc/.013,2.0));
	vec2 ground=(uv-vec2(.5,.57))*vec2(1.0,3.2);
	float pressure=(1.0-smoothstep(.025,.14,length(ground)))*(1.0-smoothstep(.045,.18,t));
	float shock_radius=.055+clamp(t/.24,0.0,1.0)*.23;
	float shock=(1.0-smoothstep(.004,.018,abs(length(ground)-shock_radius)))*(1.0-smoothstep(.06,.28,t));
	shock*=.35+.45*grain(uv*.3);
	float dust=0.0;
	for(int j=0;j<16;j++) {
		float k=float(j);
		vec2 initial=vec2(.5+(hash(k)-.5)*.25,.55-hash(k+18.0)*.08);
		vec2 p=initial+vec2((hash(k+8.0)-.5)*.28,-.08-hash(k+13.0)*.14)*t+vec2(0.0,t*t*.12);
		dust+=exp(-length((uv-p)*vec2(1.0,1.6))/.0045)*.42;
	}
	float white=clamp(core+pressure*.85,0.0,1.0);
	return vec4(mix(vec3(.29,.49,.71),vec3(.91,.97,1.0),white),clamp((core+color_edge*.42+pressure*.70+shock+ dust)*energy,0.0,.94));
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
