shader_type canvas_item;
render_mode unshaded;

uniform sampler2D tile0 : source_color, filter_linear, repeat_disable;
uniform sampler2D tile1 : source_color, filter_linear, repeat_disable;
uniform sampler2D tile2 : source_color, filter_linear, repeat_disable;
uniform sampler2D tile3 : source_color, filter_linear, repeat_disable;
uniform sampler2D tile4 : source_color, filter_linear, repeat_disable;
uniform sampler2D tile5 : source_color, filter_linear, repeat_disable;
uniform sampler2D tile6 : source_color, filter_linear, repeat_disable;
uniform sampler2D tile7 : source_color, filter_linear, repeat_disable;
uniform sampler2D tile8 : source_color, filter_linear, repeat_disable;
uniform float feather_px = 80.0;
uniform float tile_px = 1254.0;

// Extend the nearest edge context, then normalize four-way overlap at corners.
// This never scales the world or the original texture cores.
vec4 piece(sampler2D source, vec2 local) {
	float b = feather_px / tile_px;
	vec2 w = smoothstep(vec2(-b), vec2(b), local)
		* (vec2(1.0) - smoothstep(vec2(1.0-b), vec2(1.0+b), local));
	float weight = w.x * w.y;
	if (weight < 0.0001) { return vec4(0.0); }
	vec2 extended = clamp(local, vec2(0.0), vec2(1.0));
	return vec4(texture(source, extended).rgb * weight, weight);
}

void fragment() {
	vec2 p = UV * 3.0;
	vec4 c = piece(tile0, p) + piece(tile1, p-vec2(1.0,0.0)) + piece(tile2, p-vec2(2.0,0.0))
		+ piece(tile3, p-vec2(0.0,1.0)) + piece(tile4, p-vec2(1.0,1.0)) + piece(tile5, p-vec2(2.0,1.0))
		+ piece(tile6, p-vec2(0.0,2.0)) + piece(tile7, p-vec2(1.0,2.0)) + piece(tile8, p-vec2(2.0,2.0));
	COLOR = vec4(c.rgb / max(c.a, 0.0001), 1.0);
}
