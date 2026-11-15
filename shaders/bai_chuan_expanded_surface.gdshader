shader_type canvas_item;
render_mode unshaded;
uniform sampler2D water_distance : filter_linear, repeat_disable;
uniform sampler2D ground_tone : source_color, filter_linear, repeat_disable;
uniform sampler2D ground_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D water_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec2 tile_origin;
uniform float tile_world_size = 7372.8;
uniform float world_size = 22118.4;
uniform float source_scale = 5.8794258373;
uniform vec3 ground_average = vec3(0.6);
uniform float ground_detail_strength : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	highp vec2 point = tile_origin + UV * tile_world_size;
	vec2 map_uv = point / world_size;
	vec2 grain_a = abs(fract(point / 360.0) * 2.0 - 1.0);
	vec2 grain_b = abs(fract(vec2(point.y, -point.x) / 591.0 + vec2(0.31, 0.67)) * 2.0 - 1.0);
	vec3 grain = mix(texture(ground_texture, grain_a).rgb, texture(ground_texture, grain_b).rgb, 0.35);
	vec3 land = texture(ground_tone, map_uv).rgb * mix(vec3(1.0), grain / max(ground_average, vec3(0.02)), 0.45);
	// Stable world-space pigment fields give open ground broad areas of rest
	// between clustered detail sprites. All nine tiles share these coordinates.
	vec2 pigment_uv_a = abs(fract(point / 5500.0 + vec2(0.147, 0.233)) * 2.0 - 1.0);
	vec2 pigment_uv_b = abs(fract(vec2(point.y, -point.x) / 10200.0 + vec2(0.217, 0.619)) * 2.0 - 1.0);
	float broad = dot(textureLod(ground_texture, pigment_uv_a, 5.0).rgb, vec3(0.3333333));
	float local_pigment = dot(textureLod(ground_texture, pigment_uv_b, 5.0).rgb, vec3(0.3333333));
	float meadow = smoothstep(0.545, 0.580, broad * 0.65 + local_pigment * 0.35);
	float dry = smoothstep(0.562, 0.592, local_pigment) * (1.0 - meadow * 0.65);
	vec3 varied_land = land * mix(vec3(1.0), vec3(0.78, 0.985, 0.72), meadow * 0.55);
	varied_land *= mix(vec3(1.0), vec3(1.055, 1.02, 0.94), dry * 0.5);
	land = mix(land, varied_land, ground_detail_strength);
	float distance_world = (texture(water_distance, map_uv).r * 2.0 - 1.0) * 12.0 * source_scale;
	float aa = max(fwidth(distance_world) * 0.65, 0.35);
	float wet = 1.0 - smoothstep(-aa, aa, distance_world);
	float bank = (1.0 - smoothstep(0.0, 9.0, distance_world)) * (1.0 - wet);
	float bank_sand = smoothstep(4.0, 14.0, distance_world) * (1.0 - smoothstep(18.0, 39.0, distance_world));
	land = mix(land, land * vec3(1.09, 1.055, 0.96), bank_sand * 0.32 * ground_detail_strength);
	land = mix(land, land * vec3(0.87, 0.87, 0.83), bank * 0.38);
	vec2 water_uv = abs(fract(point / 680.0) * 2.0 - 1.0);
	vec3 water = texture(water_texture, water_uv).rgb;
	float shallow = smoothstep(-45.0, -2.0, distance_world);
	water = mix(water * vec3(0.84, 0.94, 1.01), water * vec3(1.10, 1.08, 1.05), shallow * 0.5);
	COLOR = vec4(mix(land, water, wet), 1.0);
}
