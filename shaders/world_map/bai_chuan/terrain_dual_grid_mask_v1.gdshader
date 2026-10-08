shader_type canvas_item;
render_mode unshaded;

uniform sampler2D paper_texture : source_color, repeat_enable;
uniform vec2 paper_period = vec2(8192.0, 8192.0);
uniform bool use_terrain_mask = true;

void fragment() {
	float terrain_alpha = 1.0;
	if (use_terrain_mask) {
		terrain_alpha = texture(TEXTURE, UV).a;
	}
	vec2 world_position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	vec4 paper = texture(paper_texture, world_position / paper_period);
	COLOR = vec4(paper.rgb, paper.a * terrain_alpha);
}
