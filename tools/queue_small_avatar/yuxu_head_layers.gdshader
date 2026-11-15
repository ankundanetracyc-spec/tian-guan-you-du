shader_type canvas_item;

uniform vec4 neck_pixels;
uniform float anchor_x = 518.0;
// 0: neck, 1: fixed face and face-framing locks, 2: rear hair, 3: crown.
uniform int layer = 1;

void fragment() {
	vec2 pixel = UV / TEXTURE_PIXEL_SIZE;
	float x = pixel.x - anchor_x + 518.0;
	float y = pixel.y;
	bool neck = pixel.x >= neck_pixels.x && pixel.x < neck_pixels.z
		&& pixel.y >= neck_pixels.y && pixel.y < neck_pixels.w;
	// Keep the original face, ear and lower fringe together at their ink boundaries.
	float front_edge = mix(435.0, 354.0, clamp((y - 265.0) / 160.0, 0.0, 1.0));
	bool fixed_front = y >= 265.0 && x >= front_edge;
	bool crown = y < 265.0 || (y < 405.0 && x >= 274.0);
	if ((layer == 0 && !neck)
		|| (layer == 1 && (neck || !fixed_front))
		|| (layer == 2 && (fixed_front || neck || crown))
		|| (layer == 3 && (fixed_front || neck || !crown))) {
		discard;
	}
}
