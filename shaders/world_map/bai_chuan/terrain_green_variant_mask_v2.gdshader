shader_type canvas_item;
render_mode unshaded;

uniform sampler2D terrain_field : repeat_disable, filter_linear;
uniform vec2 map_size = vec2(200.0, 200.0);
uniform float logical_tile_size = 256.0;
uniform vec2 map_origin = vec2(128.0, 128.0);

varying vec2 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 corner_mask = texture(TEXTURE, UV);
	vec2 map_uv = (world_position - map_origin) / (map_size * logical_tile_size);
	map_uv = clamp(map_uv, vec2(0.0), vec2(1.0));
	vec4 terrain_pixel = texture(terrain_field, map_uv);
	vec3 wash_color = terrain_pixel.rgb;
	if (min(wash_color.r, min(wash_color.g, wash_color.b)) > 0.985) {
		wash_color = vec3(1.0);
	}
	COLOR = vec4(wash_color, terrain_pixel.a * corner_mask.a) * COLOR;
}
