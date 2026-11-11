shader_type canvas_item;
render_mode unshaded;
uniform int tier = 0;
uniform vec4 band_tint = vec4(.45, .53, .61, 1.0);
uniform bool open_bottle = false;
uniform float body_cutoff_uv = .19140625;
uniform vec2 mouth_uv = vec2(.5, .198);

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    float band_x = smoothstep(.33, .36, UV.x) * (1.0 - smoothstep(.64, .67, UV.x));
    float band_y = smoothstep(.249, .255, UV.y) * (1.0 - smoothstep(.278, .286, UV.y));
    float band = band_x * band_y;
    vec3 colored = mix(tex.rgb, band_tint.rgb, .62);
    vec3 base = mix(tex.rgb, colored, band);
    float marks = 0.0;
    for (int i = 0; i < 4; i++) {
        float k = float(i);
        vec2 mark_center = vec2(.5 + (k - 1.5) * .055, .267);
        float dot_shape = 1.0 - smoothstep(.006, .010, length((UV - mark_center) * vec2(1.0, 2.0)));
        marks = max(marks, dot_shape * step(k + .5, float(tier)));
    }
    vec3 matte = mix(base, vec3(.13, .20, .22), marks * .82);
    if (open_bottle) {
        tex.a *= step(body_cutoff_uv, UV.y);
        // The lip and body retain the exact pixels of the current closed inventory bottle.
        float opening = 1.0 - smoothstep(.86, 1.0, length((UV - mouth_uv) / vec2(.17, .011)));
        matte = mix(matte, vec3(.22, .24, .23), opening * .94);
    }
    COLOR = vec4(matte, tex.a) * COLOR;
}
