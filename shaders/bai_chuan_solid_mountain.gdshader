shader_type canvas_item;
render_mode unshaded;
uniform float grounding_strength : hint_range(0.0, 1.0) = 0.0;
uniform vec4 grounding_region = vec4(0.0, 0.0, 1.0, 1.0);
uniform vec3 grounding_earth_tint = vec3(0.715, 0.622, 0.353);
varying vec4 modulation;
void vertex() {
	modulation = COLOR;
}
void fragment() {
	vec4 paint = texture(TEXTURE, UV);
	float native_alpha = paint.a;
	vec2 local_uv = (UV - grounding_region.xy) / grounding_region.zw;
	float base_blend = smoothstep(0.62, 0.96, local_uv.y) * grounding_strength;
	// Restore the native lower-edge washes without cutting a silhouette or
	// fading its opaque core. All other mountains keep the original alpha.
	paint.a = mix(smoothstep(0.18, 0.85, native_alpha), native_alpha, base_blend);
	paint.rgb = mix(paint.rgb, paint.rgb * vec3(1.015, 1.0, 0.97), base_blend * 0.12);
	if (grounding_strength > 0.0) {
		// Soften only the real lower silhouette's ink rim into the local earth
		// colour. Keep the original alpha/core and avoid a horizontal cutline.
		vec2 lower_uv = clamp(UV + vec2(0.0, grounding_region.w * 0.045),
			grounding_region.xy + TEXTURE_PIXEL_SIZE * 0.5,
			grounding_region.xy + grounding_region.zw - TEXTURE_PIXEL_SIZE * 0.5);
		float lower_edge = 1.0 - smoothstep(0.2, 0.9, texture(TEXTURE, lower_uv).a);
		paint.rgb = mix(paint.rgb, grounding_earth_tint, base_blend * lower_edge * 0.42);
	}
	COLOR = paint * modulation;
}
