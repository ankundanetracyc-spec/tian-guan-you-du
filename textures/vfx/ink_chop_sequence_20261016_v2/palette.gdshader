shader_type canvas_item;
render_mode unshaded;
uniform bool dark_background = false;
void fragment() {
	vec4 pixel = texture(TEXTURE, UV);
	if (dark_background) {
		float core = smoothstep(.35,.85, max(pixel.r,max(pixel.g,pixel.b)));
		float fiber = clamp((dot(pixel.rgb,vec3(.21,.72,.07))-.03)/.20,0.0,1.0);
		vec3 jade = mix(vec3(.20,.32,.29),vec3(.53,.68,.59),fiber);
		pixel.rgb = mix(jade, vec3(.98,.95,.84), core);
	}
	COLOR = pixel;
}
