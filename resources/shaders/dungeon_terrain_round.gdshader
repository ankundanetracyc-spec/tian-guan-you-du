shader_type canvas_item;
render_mode unshaded;

uniform vec2 tile_size = vec2(120.0);
varying vec2 tile_uv;

void vertex() {
    // Local position, not atlas UV: the source texture is a sub-region of an atlas.
    tile_uv = VERTEX / tile_size;
}

void fragment() {
    vec2 p = (tile_uv - vec2(0.5)) * tile_size;
    float radius = min(tile_size.x, tile_size.y) * 0.07;
    vec2 q = abs(p) - (tile_size * 0.5 - vec2(radius));
    float distance_to_edge = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius;
    float mask = 1.0 - smoothstep(-0.75, 0.75, distance_to_edge);
    COLOR = texture(TEXTURE, UV) * COLOR;
    COLOR.a *= mask;
}
