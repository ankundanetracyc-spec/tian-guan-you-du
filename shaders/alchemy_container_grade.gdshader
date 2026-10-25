shader_type canvas_item;
render_mode unshaded;
uniform int tier = 0;
uniform vec4 band_tint = vec4(.45, .53, .61, 1.0);

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    float band_x = smoothstep(.395, .425, UV.x) * (1.0 - smoothstep(.575, .605, UV.x));
    float band_y = smoothstep(.286, .300, UV.y) * (1.0 - smoothstep(.333, .347, UV.y));
    float band = band_x * band_y;
    vec3 colored = mix(tex.rgb, band_tint.rgb, .62);
    vec3 base = mix(tex.rgb, colored, band);
    float marks = 0.0;
    for (int i = 0; i < 4; i++) {
        float k = float(i);
        vec2 mark_center = vec2(.5 + (k - 1.5) * .035, .318);
        float dot_shape = 1.0 - smoothstep(.009, .016, distance(UV, mark_center));
        marks = max(marks, dot_shape * step(k + .5, float(tier)));
    }
    vec3 matte = mix(base, vec3(.13, .20, .22), marks * .82);
    COLOR = vec4(matte, tex.a) * COLOR;
}
