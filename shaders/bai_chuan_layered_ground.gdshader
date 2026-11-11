shader_type canvas_item;
render_mode unshaded;

uniform sampler2D feature_data : filter_linear, repeat_disable;
uniform sampler2D ground_tone : source_color, filter_linear, repeat_disable;
uniform sampler2D ground_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec2 tile_origin;
uniform float tile_world_size = 2457.6;
uniform float world_size = 7372.8;
uniform vec3 ground_average = vec3(0.6);

void fragment() {
	vec2 point = tile_origin + UV * tile_world_size;
	vec2 map_uv = point / world_size;
	// Mirrored, differently oriented samples avoid a visible repeating grid.
	vec2 uv_a = abs(fract(point / 305.0) * 2.0 - 1.0);
	vec2 uv_b = abs(fract(vec2(point.y, -point.x) / 487.0 + vec2(0.31, 0.67)) * 2.0 - 1.0);
	vec3 detail = mix(texture(ground_texture, uv_a).rgb, texture(ground_texture, uv_b).rgb, 0.38);
	vec3 tone = texture(ground_tone, map_uv).rgb;
	vec3 land = tone * mix(vec3(1.0), detail / max(ground_average, vec3(0.02)), 0.5);
	float water = texture(feature_data, map_uv).r;
	COLOR = vec4(mix(land, texture(TEXTURE, UV).rgb, water), 1.0);
}
