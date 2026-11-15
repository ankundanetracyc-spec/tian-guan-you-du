shader_type canvas_item;
render_mode unshaded;

void fragment() {
	vec4 hand = texture(TEXTURE,UV);
	// Generated transparent cutouts can contain faint disconnected residue.
	// Retain the opaque painted silhouette while suppressing that residue.
	hand.a = smoothstep(0.08,0.9,hand.a);
	COLOR = hand;
}
