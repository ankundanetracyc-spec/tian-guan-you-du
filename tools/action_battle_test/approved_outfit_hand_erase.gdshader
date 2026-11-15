shader_type canvas_item;
render_mode unshaded;
uniform sampler2D open_hand : source_color, filter_linear_mipmap;
uniform sampler2D sword_hand : source_color, filter_linear_mipmap;
uniform float open_amount = 0.0;
uniform float sword_amount = 0.0;
uniform bool keep_original_sword = false;

float cuff_front(float side) {
	// Measured blue cuff's fore-edge on the unchanged v15 arm chart.
	// The old hand is removed outside this curved cuff, never by image row.
	if (side < -25.0) return mix(8.5,16.9,clamp((side+35.0)/10.0,0.0,1.0));
	if (side < -15.0) return mix(16.9,21.4,(side+25.0)/10.0);
	if (side < -5.0) return mix(21.4,24.5,(side+15.0)/10.0);
	if (side < 5.0) return mix(24.5,25.9,(side+5.0)/10.0);
	if (side < 15.0) return mix(25.9,25.8,(side-5.0)/10.0);
	if (side < 25.0) return mix(25.8,25.2,(side-15.0)/10.0);
	if (side < 35.0) return mix(25.2,23.3,(side-25.0)/10.0);
	return mix(23.3,20.2,clamp((side-35.0)/10.0,0.0,1.0));
}

void fragment() {
	vec4 rest = texture(TEXTURE,UV);
	if (keep_original_sword) {
		// Preserve the approved old sword-fingers drawing and legacy blend
		// boundary. No new sword sprite, mirror or alternate anatomy is used.
		vec4 sword = texture(sword_hand,UV);
		float b = smoothstep(0.574,0.612,UV.x);
		float opacity = mix(rest.a,sword.a,b);
		COLOR = vec4((rest.rgb*rest.a*(1.0-b)+sword.rgb*sword.a*b)/max(opacity,0.00001),opacity);
	} else {
		vec2 point = UV/ TEXTURE_PIXEL_SIZE;
		vec2 direction = normalize(vec2(143.0,-89.0));
		vec2 normal = vec2(-direction.y,direction.x);
		vec2 relative = point-vec2(609.0,473.0);
		float fore = dot(relative,direction);
		float side = dot(relative,normal);
		float erase = smoothstep(cuff_front(side)-0.8,cuff_front(side)+0.8,fore);
		COLOR = vec4(rest.rgb,rest.a*(1.0-erase));
	}
}
