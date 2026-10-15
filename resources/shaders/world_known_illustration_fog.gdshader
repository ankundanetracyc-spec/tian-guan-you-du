shader_type canvas_item;
render_mode unshaded;

uniform sampler2D world_fog : filter_nearest, repeat_disable;
uniform vec2 map_origin = vec2(0.0);
uniform vec2 map_scale = vec2(1.0);
uniform vec2 map_size = vec2(51200.0);
uniform vec2 fog_origin = vec2(0.0);
varying vec2 map_position;

void vertex() {
    map_position = ((MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy - map_origin) / map_scale;
}

void fragment() {
    vec2 fog_uv = (map_position - fog_origin) / map_size;
    float fog_alpha = texture(world_fog, fog_uv).a;
    // The ordinary copy is Y-sorted below fog. Restore only the known artwork
    // outside clear sight; labels and badges remain on their strategic layer.
    COLOR.a *= step(0.001, fog_alpha);
}
