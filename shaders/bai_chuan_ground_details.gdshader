shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform vec4 atlas_region = vec4(0.0, 0.0, 1.0, 1.0);
varying vec4 vertex_modulation;

void vertex() {
	// In fragment(), the built-in COLOR already includes the default texture
	// lookup. Capture only vertex/instance modulation before that lookup.
	vertex_modulation = COLOR;
}

void fragment() {
	vec2 atlas_uv = atlas_region.xy + UV * atlas_region.zw;
	vec2 inset = TEXTURE_PIXEL_SIZE * 0.5;
	atlas_uv = clamp(atlas_uv, atlas_region.xy + inset, atlas_region.xy + atlas_region.zw - inset);
	COLOR = texture(TEXTURE, atlas_uv) * vertex_modulation;
}
