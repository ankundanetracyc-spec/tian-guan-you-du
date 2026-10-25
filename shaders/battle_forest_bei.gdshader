shader_type canvas_item;
render_mode unshaded, blend_mix;
uniform sampler2D ink_noise : repeat_enable, filter_linear_mipmap;
uniform float progress = 0.0;
uniform float phase = 0.0;
uniform float canvas_aspect = 1.0;
uniform float travel_span = 0.7;
uniform float contact_progress = 0.34615;
uniform float seed_value = 0.0;
float pigment(vec2 p) { return texture(ink_noise, p * vec2(.71, 1.19) + vec2(seed_value, .37)).r; }
float crescent(vec2 p, float radius, float width) {
    float n = pigment(p * 1.7);
    float edge = abs(length(p * vec2(1.0, 1.25)) - radius + (n - .5) * .027);
    float arc = 1.0 - smoothstep(width, width + .014, edge);
    float sector = smoothstep(-.08, .16, p.x) * (1.0 - smoothstep(.58, .82, abs(p.y)));
    float fibers = texture(ink_noise, p * vec2(.31, 19.0) + seed_value).r;
    return arc * sector * (1.0 - smoothstep(.62, .83, fibers) * .58);
}
void fragment() {
    float t = clamp(progress, 0.0, 1.0);
    vec2 p = (UV - .5) * 2.0 * vec2(canvas_aspect, 1.0);
    float body = 0.0;
    float rim = 0.0;
    float fade = 1.0 - smoothstep(.48, 1.0, t);
    if (phase < .5) {
        float drive = clamp(t / max(contact_progress, .01), 0.0, 1.0);
        float head = mix(-travel_span * canvas_aspect, travel_span * canvas_aspect, pow(drive, 1.6));
        for (int i = 0; i < 3; i++) {
            float n = float(i);
            vec2 q = p - vec2(head - .33 - n * .16, 0.0);
            body = max(body, crescent(q, .37 + n * .075, .026 + n * .003) * (1.0 - n * .19));
            rim = max(rim, crescent(q + vec2(.012, .012), .37 + n * .075, .009) * .64);
        }
        fade *= smoothstep(.0, .16, drive);
    } else if (phase < 1.5) {
        float release = smoothstep(.06, .80, t);
        body = crescent(p - vec2(-.22 + release * .20, 0), .33 + release * .14, .037);
        body = max(body, crescent(-p + vec2(.16, 0), .28 + release * .12, .025));
        rim = crescent(p - vec2(-.21 + release * .20, .012), .33 + release * .14, .012);
        body *= smoothstep(release * .7 - .12, release * .7 + .16, pigment(p * 2.0));
        fade *= smoothstep(0.0, .025, t);
    } else {
        body = crescent(p - vec2(-.14, 0), .26 + t * .24, .021);
        body = max(body, crescent(-p - vec2(.14, .06), .34 + t * .16, .014) * .65);
        rim = crescent(p - vec2(-.12, .013), .26 + t * .24, .008);
        fade = sin(t * 3.14159) * (1.0 - smoothstep(.55, 1.0, t));
    }
    vec3 base = phase > 1.5 ? vec3(.24, .40, .32) : vec3(.32, .29, .40);
    vec3 color = mix(base, vec3(.83, .84, .73), rim * .74);
    COLOR = vec4(color, clamp(max(body, rim * .7), 0.0, .88) * fade);
}
