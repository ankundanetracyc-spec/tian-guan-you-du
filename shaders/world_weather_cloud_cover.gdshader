shader_type canvas_item;

uniform float weather_darkness : hint_range(0.0, 0.4) = 0.0;
uniform float clear_sunlight : hint_range(0.0, 0.2) = 0.0;
uniform float cloud_time = 0.0;

void fragment() {
	vec2 uv = UV;
	float broad_bands = sin(uv.x * 5.2 + sin(uv.y * 3.5 + cloud_time * 0.014) * 1.3 + cloud_time * 0.02);
	float cloud_detail = sin(uv.x * 10.0 + uv.y * 3.0 + cloud_time * 0.013);
	float cloud_shape = smoothstep(-0.62, 0.82, broad_bands * 0.78 + cloud_detail * 0.22);
	float shade = weather_darkness * mix(0.26, 1.0, cloud_shape);
	float sun_distance = distance(uv, vec2(0.84, 0.12));
	float sun_glow = clear_sunlight * (1.0 - smoothstep(0.04, 0.92, sun_distance));
	vec3 weather_color = mix(vec3(0.19, 0.24, 0.29), vec3(0.94, 0.78, 0.48), clamp(sun_glow * 5.0, 0.0, 0.72));
	COLOR = vec4(weather_color, max(shade, sun_glow));
}
