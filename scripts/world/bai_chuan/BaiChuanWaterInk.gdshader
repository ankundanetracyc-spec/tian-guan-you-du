shader_type canvas_item;
render_mode unshaded;
uniform sampler2D paper : source_color, filter_linear, repeat_disable;
void fragment() {
	vec2 p = FRAGCOORD.xy / 1254.0;
	vec2 q = vec2(1.0) - abs(mod(p, vec2(2.0)) - vec2(1.0));
	float grain = dot(texture(paper,q).rgb,vec3(0.3333)) - 0.70;
	COLOR.rgb += vec3(grain * 0.18);
}
