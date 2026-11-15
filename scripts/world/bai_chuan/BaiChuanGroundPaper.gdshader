shader_type canvas_item;
render_mode unshaded;
uniform sampler2D paper : source_color, filter_linear, repeat_disable;
uniform float world_size = 6144.0;
uniform float paper_size = 1254.0;
void fragment() {
	vec2 p = UV * world_size / paper_size;
	vec2 q = vec2(1.0) - abs(mod(p, vec2(2.0)) - vec2(1.0));
	vec3 ground = mix(vec3(0.82,0.75,0.62),texture(paper,q).rgb,0.82);
	vec2 pos = UV;
	float south_valley = exp(-dot((pos-vec2(0.35,0.81))/vec2(0.27,0.22),(pos-vec2(0.35,0.81))/vec2(0.27,0.22)));
	float river_plain = exp(-dot((pos-vec2(0.57,0.50))/vec2(0.31,0.15),(pos-vec2(0.57,0.50))/vec2(0.31,0.15)));
	float old_salt = exp(-dot((pos-vec2(0.89,0.87))/vec2(0.20,0.18),(pos-vec2(0.89,0.87))/vec2(0.20,0.18)));
	ground = mix(ground,ground*vec3(0.93,0.99,0.92),0.52*south_valley+0.26*river_plain);
	ground = mix(ground,ground*vec3(0.98,0.96,1.01),0.48*old_salt);
	COLOR = vec4(ground,1.0);
}
