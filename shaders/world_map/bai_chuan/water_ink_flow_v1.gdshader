shader_type canvas_item;
render_mode unshaded;

uniform sampler2D flow_noise : repeat_enable, filter_linear;
uniform vec4 ink_deep_color : source_color = vec4(0.56, 0.66, 0.69, 1.0);
uniform vec4 ink_light_color : source_color = vec4(0.69, 0.77, 0.79, 1.0);
uniform vec4 ink_glint_color : source_color = vec4(0.80, 0.86, 0.87, 1.0);
uniform float wash_strength : hint_range(0.0, 0.5) = 0.18;
uniform float ripple_strength : hint_range(0.0, 0.2) = 0.045;
uniform float flow_speed : hint_range(0.0, 0.15) = 0.028;

varying vec2 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 water_mask = texture(TEXTURE, UV);
	vec2 broad_uv = world_position * vec2(0.0021, 0.0017);
	vec2 fine_uv = world_position * vec2(0.0043, 0.0035);
	float broad_flow = texture(
		flow_noise,
		broad_uv + vec2(TIME * flow_speed * 0.42, -TIME * flow_speed * 0.21)
	).r;
	float fine_flow = texture(
		flow_noise,
		fine_uv + vec2(-TIME * flow_speed * 0.19, TIME * flow_speed * 0.31)
	).r;

	float wash = clamp(0.5 + (broad_flow - 0.5) * wash_strength + (fine_flow - 0.5) * wash_strength * 0.32, 0.0, 1.0);
	vec3 water_color = mix(ink_deep_color.rgb, ink_light_color.rgb, wash);
	float moving_glint = smoothstep(0.78, 0.9, fine_flow) * ripple_strength;
	water_color = mix(water_color, ink_glint_color.rgb, moving_glint);
	COLOR = vec4(water_color, water_mask.a) * COLOR;
}
