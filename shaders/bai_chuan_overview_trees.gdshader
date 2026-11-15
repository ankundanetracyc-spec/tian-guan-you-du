shader_type canvas_item;
render_mode unshaded;
uniform vec4 atlas_region;
varying vec4 modulation;
void vertex() {
	modulation = COLOR;
}
void fragment() {
	COLOR = texture(TEXTURE, atlas_region.xy + UV * atlas_region.zw) * modulation;
}
