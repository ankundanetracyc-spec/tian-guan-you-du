shader_type canvas_item;
render_mode unshaded;

uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float phase = 0.0;
uniform vec2 pull_direction = vec2(1.0, 0.0);

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float wash(vec2 p) {
    vec2 cell = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(cell), hash(cell + vec2(1.0, 0.0)), f.x),
               mix(hash(cell + vec2(0.0, 1.0)), hash(cell + vec2(1.0)), f.x), f.y);
}

void fragment() {
    vec2 across = vec2(-pull_direction.y, pull_direction.x);
    float curl = sin(dot(UV - .5, pull_direction) * 9.0 + phase + dissolve * 2.4);
    vec2 uv = UV + across * curl * dissolve * .024;
    vec4 pigment = texture(TEXTURE, clamp(uv, vec2(.001), vec2(.999)));
    float grain = wash(UV * 8.0 + phase) * .66 + wash(UV * 19.0 - phase) * .34;
    // Static pigment islands wash away continuously, preserving the source's actual color.
    float threshold = .15 + grain * .70;
    float retained = 1.0 - smoothstep(threshold - .15, threshold + .15, dissolve);
    COLOR = vec4(pigment.rgb, pigment.a * retained) * COLOR;
}
