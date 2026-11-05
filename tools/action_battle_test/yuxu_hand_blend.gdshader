shader_type canvas_item;
uniform sampler2D open_hand : source_color, filter_linear_mipmap;
uniform sampler2D sword_hand : source_color, filter_linear_mipmap;
uniform float open_amount : hint_range(0.0, 1.0) = 0.0;
uniform float sword_amount : hint_range(0.0, 1.0) = 0.0;
void fragment() {
	vec4 rest = texture(TEXTURE, UV);
	vec4 palm = texture(open_hand, UV);
	vec4 sword = texture(sword_hand, UV);
	// Restrict transitions to the hand and wrist. Approved shoulder, sleeve,
	// cloud drawing and cuff remain precisely the original texture.
	float hand_mask = smoothstep(0.574, 0.612, UV.x);
	float a = open_amount * hand_mask;
	float b = sword_amount * hand_mask * (1.0-a);
	float r = max(0.0, 1.0-a-b);
	float opacity = rest.a*r + palm.a*a + sword.a*b;
	vec3 pigment = (rest.rgb*rest.a*r + palm.rgb*palm.a*a + sword.rgb*sword.a*b) / max(opacity, 0.00001);
	COLOR = vec4(pigment, opacity);
}
