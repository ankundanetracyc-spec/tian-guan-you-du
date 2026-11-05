shader_type canvas_item;
render_mode unshaded;

// Same full-canvas deformation for all portrait parts keeps their seams aligned.
uniform float breath : hint_range(0.0, 1.0) = 0.0;
uniform float strength : hint_range(0.0, 2.0) = 1.0;
varying vec4 vertex_tint;

void vertex() {
	vertex_tint = COLOR;
}

void fragment() {
	vec2 uv = UV;
	float amount = breath * strength;
	// The face translates rigidly; movement fades out below the shoulders to a fixed waist.
	float rise = 1.0 - smoothstep(0.36, 0.84, uv.y);
	uv.y += amount * 0.0028 * rise;
	float chest = smoothstep(0.34, 0.46, uv.y) * (1.0 - smoothstep(0.56, 0.84, uv.y));
	uv.x = 0.5 + (uv.x - 0.5) / (1.0 + amount * 0.007 * chest);
	vec4 sample_color = texture(TEXTURE, uv);
	float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
	COLOR = vertex_tint * sample_color * inside;
}
