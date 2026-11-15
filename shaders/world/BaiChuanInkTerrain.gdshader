shader_type canvas_item;
render_mode unshaded;

// These textures contain scalar data. Do not add source_color here.
uniform sampler2D terrain_data : filter_linear, repeat_disable;
uniform sampler2D regional_data : filter_linear, repeat_disable;
uniform sampler2D water_geometry_data : filter_linear, repeat_disable;
uniform vec2 tile_origin = vec2(0.0);
uniform float tile_size = 2457.6;
uniform float map_size = 7372.8;

uniform float plain_cleanup : hint_range(0.0, 1.0) = 0.68;
uniform float landmark_cleanup : hint_range(0.0, 1.0) = 0.74;
uniform float regional_strength : hint_range(0.0, 1.0) = 0.80;
uniform float plain_region_strength : hint_range(0.0, 0.35) = 0.0;
uniform vec4 plain_region_green_color : source_color = vec4(0.553, 0.667, 0.439, 1.0);
uniform vec4 plain_region_earth_color : source_color = vec4(0.702, 0.612, 0.467, 1.0);
uniform vec4 plain_region_cool_color : source_color = vec4(0.608, 0.678, 0.682, 1.0);
uniform float forest_strength : hint_range(0.0, 1.0) = 0.72;
uniform float rock_strength : hint_range(0.0, 1.0) = 0.66;
uniform float water_strength : hint_range(0.0, 1.0) = 0.84;
uniform float water_detail : hint_range(0.0, 1.0) = 0.32;
uniform float water_native_mode : hint_range(0.0, 1.0) = 0.0;
// Optional diagnostic-reviewed correction; zero preserves the native drawing.
uniform float water_geometry_lock : hint_range(0.0, 1.0) = 0.0;
uniform float water_geometry_max_repair_world = 24.0;
uniform float water_geometry_sdf_range_world = 96.0;
uniform vec4 paper_color : source_color = vec4(0.916, 0.901, 0.850, 1.0);
uniform vec4 forest_color : source_color = vec4(0.405, 0.505, 0.355, 1.0);
uniform vec4 rock_color : source_color = vec4(0.550, 0.509, 0.427, 1.0);
uniform vec4 water_color : source_color = vec4(0.618, 0.694, 0.688, 1.0);

float luma(vec3 rgb) {
	return dot(rgb, vec3(0.2126, 0.7152, 0.0722));
}

vec3 at_luminance(vec3 tint, float value) {
	return tint * (value / max(luma(tint), 0.01));
}

void fragment() {
	vec4 source = texture(TEXTURE, UV);
	// Authored map coordinates are independent of all canvas/camera transforms.
	vec2 world_uv = clamp((tile_origin + UV * tile_size) / max(map_size, 1.0), vec2(0.0), vec2(1.0));
	vec4 terrain = texture(terrain_data, world_uv);
	vec4 regional = texture(regional_data, world_uv);
	float value = luma(source.rgb);
	float native_water = smoothstep(0.035, 0.085, source.b - source.r)
		* smoothstep(0.008, 0.030, source.b - source.g);
	float native_teal = smoothstep(0.022, 0.035, source.g - source.r)
		* smoothstep(-0.040, -0.025, source.b - source.r)
		* smoothstep(-0.130, -0.105, source.b - source.g)
		* (1.0 - smoothstep(0.80, 0.87, value));
	native_water = mix(native_water, max(native_water, native_teal), water_native_mode);
	float water = terrain.r * native_water;
	float dry = 1.0 - water;
	float strong_ink = 1.0 - smoothstep(0.54, 0.73, value);
	float preserve = max(terrain.g, strong_ink);
	float woodland = regional.a * (1.0 - native_water);
	float green = regional.r;
	float earth = regional.g;
	float cool = regional.b;

	// Quiet paper keeps local broad shading while compressing only faint filler.
	// No source texture blur is applied to mountains, trees, outlines or shores.
	vec3 local_paper = paper_color.rgb;
	local_paper += regional_strength * (green * vec3(-0.019, 0.008, -0.027)
		+ earth * vec3(0.011, -0.013, -0.034) + cool * vec3(-0.021, 0.002, 0.006));
	float paper_value = clamp(terrain.b * 0.36 + 0.575, 0.84, 0.930);
	vec3 quiet_paper = at_luminance(local_paper, paper_value + (value - terrain.b) * 0.23);
	float flat_ground = (1.0 - preserve) * smoothstep(0.68, 0.84, value) * dry;
	float clearing = terrain.a * (1.0 - preserve * 0.94) * dry;
	float quiet = 1.0 - (1.0 - flat_ground * plain_cleanup) * (1.0 - clearing * landmark_cleanup);
	vec3 result = mix(source.rgb, quiet_paper, quiet);

	// Colour follows surviving source masses; it does not create new landforms.
	float graded_value = luma(result);
	vec3 forest_tint = at_luminance(forest_color.rgb, graded_value);
	float forest_mix = woodland * forest_strength * (0.38 + green * 0.62);
	result = mix(result, forest_tint, forest_mix * regional_strength);
	vec3 rock_tint = at_luminance(rock_color.rgb, luma(result));
	float rock_mix = preserve * (1.0 - woodland) * earth * rock_strength * dry;
	result = mix(result, rock_tint, rock_mix * regional_strength);
	// Pale exposed shoulders carry a much lighter version of their region wash.
	vec3 shoulder_tint = at_luminance(local_paper, luma(result));
	result = mix(result, shoulder_tint, (1.0 - preserve) * dry * regional_strength * 0.17);
	// The new painted terrain gets a separate faint, luminance-preserving plain
	// colour control. Rock masses and water are excluded by the existing masks.
	float region_total = green + earth + cool;
	vec3 plain_region = (plain_region_green_color.rgb * green + plain_region_earth_color.rgb * earth
		+ plain_region_cool_color.rgb * cool) / max(region_total, 0.001);
	vec3 plain_region_tint = at_luminance(plain_region, luma(result));
	result = mix(result, plain_region_tint, flat_ground * plain_region_strength * min(region_total, 1.0));

	// Calm mineral grey-green water retains native banks and restrained currents.
	float water_value = luma(water_color.rgb) + (value - 0.65) * water_detail;
	vec3 quiet_water = at_luminance(water_color.rgb, water_value);
	result = mix(result, quiet_water, water * water_strength);
	if (water_geometry_lock > 0.001) {
		vec4 geometry = texture(water_geometry_data, world_uv);
		float target_distance = (geometry.g - 0.5) * 2.0 * water_geometry_sdf_range_world;
		float candidate_distance = (geometry.a - 0.5) * 2.0 * water_geometry_sdf_range_world;
		float max_repair = max(water_geometry_max_repair_world, 1.0);
		float target_band = 1.0 - smoothstep(max_repair * 0.60, max_repair, abs(target_distance));
		float candidate_band = 1.0 - smoothstep(max_repair * 0.60, max_repair, abs(candidate_distance));
		float correction = min(target_band, candidate_band) * water_geometry_lock;
		float missing_channel = geometry.r * (1.0 - geometry.b);
		float stray_water = (1.0 - geometry.r) * geometry.b;
		// Only mismatched narrow strips are mixed, without outlines or displaced UVs.
		// Native colours and broad shapes outside these strips pass through.
		vec3 channel_tone = at_luminance(water_color.rgb, luma(water_color.rgb));
		vec3 bank_tone = at_luminance(local_paper, luma(local_paper));
		result = mix(result, channel_tone, missing_channel * correction);
		result = mix(result, bank_tone, stray_water * correction);
	}
	COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), source.a);
}
