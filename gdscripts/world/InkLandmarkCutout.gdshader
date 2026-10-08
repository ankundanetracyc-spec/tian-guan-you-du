shader_type canvas_item;

uniform float paper_luma_start : hint_range(0.0, 1.0) = 0.68;
uniform float paper_luma_end : hint_range(0.0, 1.0) = 0.88;
uniform float paper_chroma_start : hint_range(0.0, 1.0) = 0.22;
uniform float paper_chroma_end : hint_range(0.0, 1.0) = 0.55;

void fragment() {
	vec4 sample_color = texture(TEXTURE, UV) * COLOR;
	float highest = max(sample_color.r, max(sample_color.g, sample_color.b));
	float lowest = min(sample_color.r, min(sample_color.g, sample_color.b));
	float chroma = highest - lowest;
	float luminance = dot(sample_color.rgb, vec3(0.299, 0.587, 0.114));
	float paper_by_light = smoothstep(paper_luma_start, paper_luma_end, luminance);
	float paper_by_chroma = 1.0 - smoothstep(paper_chroma_start, paper_chroma_end, chroma);
	sample_color.a *= 1.0 - paper_by_light * paper_by_chroma;
	COLOR = sample_color;
}
