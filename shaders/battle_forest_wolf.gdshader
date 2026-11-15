shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D ink_noise : repeat_enable, filter_linear_mipmap;
uniform vec4 ink_color : source_color = vec4(0.04, 0.05, 0.045, 0.94);
uniform float progress = 0.0;
uniform float kind = 10.0;
uniform float phase = 0.0;
uniform float seed_value = 0.0;
uniform float critical = 0.0;
uniform float travel_span = 0.65;
uniform float contact_progress = 0.375;
uniform float canvas_aspect = 1.5;

float grain(vec2 p) {
    return texture(ink_noise, p * vec2(0.61, 1.37) + vec2(seed_value, 0.31)).r;
}

float bristle(vec2 p, float tail, float head, float center, float width) {
    float u = clamp((p.x - tail) / max(head - tail, 0.001), 0.0, 1.0);
    float pressure = pow(max(sin(u * 3.14159), 0.0), 0.65);
    float rag = (grain(p * 2.2) - 0.5) * 0.016;
    float d = abs(p.y - center + rag);
    float shape = 1.0 - smoothstep(width * pressure, width * pressure + 0.012, d);
    float span = smoothstep(tail, tail + 0.025, p.x) * (1.0 - smoothstep(head - 0.025, head, p.x));
    float dry = texture(ink_noise, p * vec2(0.21, 24.0) + seed_value).r;
    return shape * span * (1.0 - smoothstep(0.59, 0.83, dry) * 0.66);
}

float jaws(vec2 q, float gap, float thickness) {
    // Two tapered, asymmetric brush hooks suggest the closing bite.
    float bend = gap + pow(clamp((q.x + 0.48) / 0.92, 0.0, 1.0), 1.8) * 0.19;
    float upper = bristle(q, -0.51, 0.43, -bend + q.x * 0.18, thickness);
    float lower = bristle(q, -0.43, 0.49, bend - q.x * 0.24, thickness * 0.82);
    return max(upper, lower);
}

void fragment() {
    float t = clamp(progress, 0.0, 1.0);
    vec2 p = (UV - 0.5) * 2.0 * vec2(canvas_aspect, 1.0);
    float noise = grain(p * 1.4);
    float body = 0.0;
    float edge = 0.0;
    float flecks = 0.0;
    float fade;
    bool pounce = kind > 10.5;
    if (phase < 0.5) {
        float drive = clamp(t / max(contact_progress, 0.01), 0.0, 1.0);
        float launch = pow(drive, 2.15);
        float tip = mix(-travel_span * canvas_aspect, travel_span * canvas_aspect, launch);
        vec2 q = p - vec2(tip - 0.08, 0.0);
        float release = smoothstep(contact_progress + 0.02, 1.0, t);
        q.x -= release * 0.12;
        for (int i = 0; i < 3; i++) {
            float n = float(i) - 1.0;
            float curve = n * 0.22 + q.x * 0.24 + q.x * q.x * 0.13;
            body = max(body, bristle(q, -0.68 + n * 0.06, 0.28 - abs(n) * 0.04, curve, 0.043));
            edge = max(edge, bristle(q, -0.59 + n * 0.06, 0.24, curve - 0.023, 0.013));
        }
        // A short wake stays behind the mouth; it never covers the wolf's body.
        float wake = bristle(q, -0.96, -0.18, 0.10 + q.x * 0.10, 0.022);
        wake = max(wake, bristle(q, -0.82, -0.25, -0.14 - q.x * 0.05, 0.013));
        body = max(body, wake * 0.25 * smoothstep(0.12, 0.65, drive));
        fade = smoothstep(0.015, 0.13, drive) * (1.0 - smoothstep(contact_progress + 0.05, 1.0, t));
    } else {
        float settle = smoothstep(0.0, 0.12, t);
        float release = smoothstep(0.13, 0.84, t);
        vec2 q = p - vec2(release * 0.21, release * release * 0.09);
        if (!pounce) {
            float gap = mix(0.17, 0.038, settle) + release * 0.06;
            body = jaws(q, gap, 0.050 - release * 0.021);
            edge = jaws(q + vec2(0.0, 0.015), gap, 0.014);
        } else {
            for (int i = 0; i < 3; i++) {
                float n = float(i) - 1.0;
                float sweep = smoothstep(float(i) * 0.025, 0.11 + float(i) * 0.025, t);
                float center = n * 0.30 + q.x * 0.52 + q.x * q.x * 0.15;
                float head = mix(-0.40, 0.49 - abs(n) * 0.065, sweep);
                body = max(body, bristle(q, -0.51 + n * 0.045, head, center, 0.055));
                edge = max(edge, bristle(q, -0.44 + n * 0.045, head - 0.02, center - 0.026, 0.016));
            }
        }
        for (int i = 0; i < 9; i++) {
            float n = float(i);
            float age = clamp((t - 0.07 - fract(n * 0.37) * 0.15) / 0.71, 0.0, 1.0);
            float side = mod(n, 2.0) * 2.0 - 1.0;
            vec2 at = vec2(0.08 + age * (0.24 + fract(n * 0.63) * 0.42), side * (0.10 + fract(n * 0.41) * 0.19 + age * 0.25) + age * age * 0.18);
            vec2 d = (p - at) * vec2(0.67, 1.65);
            float island = 1.0 - smoothstep(0.009, 0.026, length(d));
            flecks = max(flecks, island * sin(age * 3.14159) * 0.68);
        }
        float erosion = smoothstep(0.24, 0.98, t);
        float dry = smoothstep(erosion * 0.82 - 0.09, erosion * 0.82 + 0.12, noise);
        body *= dry;
        edge *= dry;
        fade = smoothstep(0.0, 0.025, t) * (1.0 - smoothstep(0.45, 1.0, t));
    }
    vec3 charcoal = mix(vec3(0.10, 0.16, 0.15), vec3(0.25, 0.36, 0.33), noise);
    vec3 celadon = vec3(0.51, 0.65, 0.58);
    vec3 ivory = vec3(0.87, 0.86, 0.72);
    vec3 color = mix(charcoal, celadon, clamp(edge * 0.85, 0.0, 0.65));
    color = mix(color, ivory, edge * (0.54 + critical * 0.11));
    float coverage = clamp(max(max(body, edge * 0.75), flecks * 0.55), 0.0, 0.94);
    COLOR = vec4(color, coverage * fade);
}
