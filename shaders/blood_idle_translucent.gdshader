shader_type canvas_item;
render_mode unshaded, blend_mix;
uniform float phase = 0.0;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
 vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
 return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
 vec2 uv=UV;
 float n=noise(uv*7.0+vec2(-phase*.22,phase*.15));
 float fine=noise(uv*24.0+vec2(phase*.3,-phase*.24));
 vec4 source=texture(TEXTURE,uv);
 float veil=smoothstep(.14,.65,source.a);
 float stream=pow(.5+.5*sin(uv.x*24.0-uv.y*17.0+n*7.0-phase*2.4),8.0);
 float inner=dot(source.rgb,vec3(.3,.5,.2));
 float density=.43+.19*n+.07*fine+.16*stream;
 // Source supplies silhouette/anatomical planes; flowing colour and alpha are real time.
 vec3 red=mix(vec3(.34,.018,.04),vec3(.91,.12,.16),clamp(inner*1.3+stream*.55,0.0,1.0));
 float erosion=smoothstep(.12,.42,n+source.a*.4);
 COLOR=vec4(red,veil*density*erosion);
}
