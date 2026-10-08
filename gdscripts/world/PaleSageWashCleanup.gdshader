shader_type canvas_item;

uniform float chroma_suppression : hint_range(0.0, 1.0) = 1.0;
uniform float paper_fade_start : hint_range(0.0, 1.0) = 0.88;
uniform float paper_fade_end : hint_range(0.0, 1.0) = 0.98;

void fragment() {
	vec4 sample_color = texture(TEXTURE, UV) * COLOR;
	float highest = max(sample_color.r, max(sample_color.g, sample_color.b));
	float lowest = min(sample_color.r, min(sample_color.g, sample_color.b));
	float chroma = highest - lowest;
	float green_fringe = smoothstep(0.12, 0.42, sample_color.g - max(sample_color.r, sample_color.b));
	float yellow_fringe = smoothstep(0.08, 0.34, sample_color.r - sample_color.b)
		* smoothstep(0.5, 0.78, sample_color.g);
	float paper_halo = smoothstep(paper_fade_start, paper_fade_end, lowest);
	float fringe_alpha = 1.0 - max(green_fringe, yellow_fringe) * chroma_suppression;
	sample_color.a *= fringe_alpha * (1.0 - paper_halo * 0.9);
	COLOR = sample_color;
}
