shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform float effect_time=0.0;
uniform float presence=1.0;
uniform float punch=0.0;
uniform float guard=0.0;
uniform float activity=0.0;
uniform float hand_side=1.0;
uniform float hand_depth=.72;
uniform float hand_yaw=-.96;
uniform float near_plane=1.0;

float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
	vec2 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
	return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
float join_shape(float a,float b,float k) {
	float h=clamp(.5+.5*(b-a)/k,0.0,1.0);
	return mix(b,a,h)-k*h*(1.0-h);
}
float limb(vec2 p,vec2 a,vec2 b,float ra,float rb) {
	vec2 ab=b-a;
	float t=clamp(dot(p-a,ab)/max(dot(ab,ab),.000001),0.0,1.0);
	return length(p-a-ab*t)-mix(ra,rb,t);
}
vec2 segment_info(vec2 p,vec2 a,vec2 b) {
	vec2 v=b-a;
	float t=clamp(dot(p-a,v)/max(dot(v,v),.000001),0.0,1.0);
	return vec2(length(p-a-v*t),t);
}
float palm_block(vec2 p) {
	vec2 corners[5]={vec2(-.067,-.078),vec2(.058,-.073),vec2(.075,.041),vec2(.010,.080),vec2(-.074,.067)};
	float closest=10.0;
	bool inside=true;
	for(int i=0;i<5;i++) {
		vec2 a=corners[i],b=corners[(i+1)%5];
		closest=min(closest,segment_info(p,a,b).x);
		vec2 v=b-a,q=p-a;
		if(v.x*q.y-v.y*q.x<0.0) inside=false;
	}
	return (inside?-closest:closest)-.006;
}
vec2 project_hand(vec3 p) {
	return vec2(p.x*hand_depth+p.z*sin(hand_yaw),p.y-p.z*.17-p.x*.07);
}
vec3 finger_step(vec3 point,float length_f,float angle) {
	return point+vec3(0.0,cos(angle)*length_f,sin(angle)*length_f);
}
// Palm planes and articulated fingers are projected together into the side view.
// The return values are silhouette distance, fine fold distance and root-to-tip path.
vec3 hand_field(vec2 p,vec2 wrist,vec2 palm) {
	vec2 forward=normalize(palm-wrist);
	vec2 right=vec2(forward.y,-forward.x);
	vec2 h=vec2(dot(p-palm,right),dot(p-palm,forward));
	h/=1.75;
	h.x*=hand_side;
	float d=palm_block(vec2(h.x/hand_depth,h.y))*hand_depth;
	float folds=10.0,closest_axis=10.0;
	float path=.70+.10*clamp((h.y+.076)/.15,0.0,1.0);
	for(int finger=0;finger<4;finger++) {
		float i=float(finger);
		float length_f=finger==0?.124:(finger==1?.143:(finger==2?.134:.108));
		float radius=finger==3?.013:(finger==1?.017:.0158);
		float x=-.058+i*.039;
		float y=finger==1?.070:(finger==3?.042:.061);
		float curl1=mix(mix(20.0+i*8.0,6.0+i*6.0,guard),72.0+i*3.0,punch);
		float curl2=mix(mix(50.0+i*14.0,22.0+i*12.0,guard),153.0+i*3.0,punch);
		float curl3=mix(mix(90.0+i*14.0,40.0+i*13.0,guard),217.0+i*2.0,punch);
		float breathe=sin(effect_time*1.1+i*.9)*2.2*(1.0-punch)*(1.0-guard);
		vec3 a=vec3(x,y,0.0);
		vec3 b=finger_step(a,length_f*.46,radians(curl1+breathe));
		vec3 c=finger_step(b,length_f*.33,radians(curl2+breathe));
		vec3 tip=finger_step(c,length_f*.21,radians(curl3+breathe));
		b.x+=(i-1.4)*.003*guard;
		c.x+=(i-1.4)*.006*guard;
		tip.x+=(i-1.4)*.008*guard;
		vec2 joints[4]={project_hand(a),project_hand(b),project_hand(c),project_hand(tip)};
		for(int joint=0;joint<3;joint++) {
			float taper=1.0-float(joint)*.16;
			vec2 info=segment_info(h,joints[joint],joints[joint+1]);
			float f=info.x-radius*(taper-info.y*.10);
			d=join_shape(d,f,.0045);
			if(info.x<closest_axis) { closest_axis=info.x; path=.80+.20*(float(joint)+info.y)/3.0; }
		}
		for(int joint=1;joint<3;joint++) {
			vec2 at=joints[joint];
			folds=min(folds,limb(h,at+vec2(-radius*.55,.0015),at+vec2(radius*.55,-.0015),.0010,.0008));
		}
	}
	vec3 ta=vec3(-.044,-.026,.0);
	vec3 tb=mix(vec3(-.096,.011,.012),vec3(-.072,.026,.040),punch);
	vec3 tc=mix(vec3(-.113,.057,.029),vec3(-.008,.047,.061),punch);
	vec3 td=mix(vec3(-.089,.083,.046),vec3(.032,.028,.046),punch);
	tc.x-=guard*.014; td.x-=guard*.026;
	vec2 thumb[4]={project_hand(ta),project_hand(tb),project_hand(tc),project_hand(td)};
	for(int joint=0;joint<3;joint++) {
		vec2 info=segment_info(h,thumb[joint],thumb[joint+1]);
		float r=.025-float(joint)*.0035;
		d=join_shape(d,info.x-r,.007);
		if(info.x<closest_axis) { closest_axis=info.x; path=.75+.25*(float(joint)+info.y)/3.0; }
	}
	folds=min(folds,limb(h,vec2(-.025,-.020),vec2(-.047,.019),.0011,.0007));
	return vec3(d*1.75,folds*1.75,path);
}
vec3 arm_field(vec2 p) {
	// Right-facing three-quarter side view, shared by both arms. A single
	// character-facing mirror is applied to the entire rig, never one arm alone.
	vec2 shoulder=vec2(.20,.72);
	// Shoulder roots are fixed. Resting elbows spread to opposite sides of the
	// figure, leaving an open gap above the head between the two inward palms.
	// Both hands retain the figure's forward-facing projection.
	vec2 elbow=mix(mix(vec2(-.38,.35),vec2(.66,.51),near_plane),vec2(.44,.74),punch);
	vec2 wrist=mix(mix(mix(vec2(-.40,-.025),vec2(.70,.03),near_plane),vec2(.58,.46),guard),vec2(.66,.70),punch);
	vec2 palm=mix(mix(mix(vec2(-.30,-.10),vec2(.81,-.02),near_plane),vec2(.67,.41),guard),vec2(.74,.69),punch);
	vec2 upper=segment_info(p,shoulder,elbow),lower=segment_info(p,elbow,wrist);
	float d1=upper.x-mix(.121,.093,upper.y),d2=lower.x-mix(.095,.057,lower.y);
	float path=d1<d2?upper.y*.36:.36+lower.y*.34;
	float d=join_shape(d1,d2,.021);
	vec3 hand=hand_field(p,wrist,palm);
	if(hand.x<d) path=hand.z;
	d=join_shape(d,hand.x,.009);
	return vec3(d,hand.y,path);
}
void fragment() {
	// Expand the carrier around the drawing so thick elbows and extended fingertips
	// remain complete instead of being cut at the texture rectangle.
	vec2 drawing_uv=UV*2.20-vec2(.60);
	vec3 field=arm_field(drawing_uv);
	float d=field.x;
	float aa=max(fwidth(d),.0009);
	float inside=1.0-smoothstep(-aa,aa,d);
	float rim=exp(-pow(d/.0037,2.0));
	float inner_rim=exp(-pow((d+.012)/.0055,2.0));
	float n=noise(UV*vec2(21.0,29.0)+vec2(effect_time*.018,-effect_time*.023));
	float current=0.0;
	for(int i=0;i<3;i++) {
		float head=-.14+1.32*fract(effect_time*.27-float(i)/3.0);
		current+=exp(-pow((field.z-head)/.065,2.0));
	}
	float folds=(1.0-smoothstep(.0,.0022,field.y))*inside;
	float surface=(current*.14)*inside;
	float opacity=inside*(.13+n*.025)+rim*(.52+current*.16)+inner_rim*current*.14+surface+folds*.13;
	opacity+=inside*activity*.12;
	vec3 color=mix(vec3(.56,.045,.09),vec3(.79,.14,.20),rim*.65+n*.13);
	color=mix(color,vec3(.95,.42,.42),clamp(current*.31+rim*current*.35+activity*.25,0.0,.72));
	// Condense from the shoulder outward, then break the film away on dismissal.
	float front=presence*1.18-.12;
	float formation=1.0-smoothstep(front-.06,front+.04,field.z+(n-.5)*.035);
	float fade=smoothstep(.0,.12,presence);
	color*=mix(.87,1.0,near_plane);
	COLOR=vec4(color,clamp(opacity*formation*fade*mix(.73,1.0,near_plane),0.0,.72));
}
