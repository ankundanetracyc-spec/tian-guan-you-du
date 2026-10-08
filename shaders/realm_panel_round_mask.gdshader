shader_type canvas_item;

// 仅遮去背景纹理的四角；内容仍保持在 Paper 的矩形裁切范围内。
uniform float corner_radius = 0.042;
uniform float feather = 0.003;

void fragment() {
    vec2 p = UV - vec2(0.5);
    vec2 half_size = vec2(0.5 - corner_radius);
    float distance_to_corner = length(max(abs(p) - half_size, vec2(0.0))) - corner_radius;
    vec4 paper = texture(TEXTURE, UV);
    paper.a *= 1.0 - smoothstep(0.0, feather, distance_to_corner);
    COLOR = paper;
}
