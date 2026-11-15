shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D flame_atlas : source_color, filter_linear, repeat_disable;
uniform float poses_per_second : hint_range(1.0, 24.0) = 12.0;
uniform bool motion_enabled = true;
varying vec4 item_tint;

void vertex() {
    item_tint = COLOR;
}

vec4 sample_pose(vec2 uv, float frame, float seconds) {
    float tip = clamp((0.82 - uv.y) / 0.62, 0.0, 1.0);
    vec2 p = uv;
    p.x += tip * tip * (sin(seconds * 9.5 - uv.y * 13.0) * 0.014
        + sin(seconds * 6.3 + uv.y * 21.0) * 0.007);
    p.y += tip * sin(seconds * 11.0 - uv.y * 17.0) * 0.006;
    vec2 cell = vec2(mod(frame, 2.0), floor(frame / 2.0));
    vec4 ink = texture(flame_atlas, (cell + clamp(p, vec2(0.002), vec2(0.998))) * 0.5);
    return vec4(ink.rgb * ink.a, ink.a);
}

void fragment() {
    float seconds = motion_enabled ? TIME : 0.0;
    float position = mod(seconds * poses_per_second, 4.0);
    float current = floor(position);
    vec4 mixed = mix(sample_pose(UV, current, seconds),
        sample_pose(UV, mod(current + 1.0, 4.0), seconds), fract(position));
    COLOR = vec4(mixed.rgb / max(mixed.a, 0.00001), mixed.a) * item_tint;
}
