shader_type canvas_item;
render_mode unshaded;
varying vec4 paint;
varying vec2 paper_at;
uniform float grain = 0.085;
uniform float ink_age = 0.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float wash(vec2 p) {
	vec2 cell=floor(p), t=fract(p);
	t=t*t*(3.0-2.0*t);
	return mix(mix(hash(cell),hash(cell+vec2(1,0)),t.x),mix(hash(cell+vec2(0,1)),hash(cell+vec2(1,1)),t.x),t.y);
}
void vertex() { paint=COLOR; paper_at=VERTEX; }
void fragment() {
	float pigment=wash(paper_at*0.13+vec2(-ink_age*1.8,ink_age*0.65))*0.70+wash(paper_at*0.85)*0.30;
	COLOR=vec4(paint.rgb*(0.93-grain+pigment*(grain*1.65+0.10)),paint.a*(0.78+pigment*0.22));
}
