shader_type canvas_item;
render_mode unshaded;
uniform float reveal : hint_range(0.0, 1.0) = 0.0;
uniform float heat : hint_range(0.0, 1.0) = 0.0;
varying vec4 item_tint;

void vertex() {
    // Fragment COLOR already contains the texture; keep only the node tint here.
    item_tint = COLOR;
}

float grain(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float wash(vec2 p) {
    vec2 cell = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(grain(cell), grain(cell + vec2(1.0, 0.0)), f.x),
               mix(grain(cell + vec2(0.0, 1.0)), grain(cell + vec2(1.0)), f.x), f.y);
}

void fragment() {
    vec4 ink = texture(TEXTURE, UV);
    float paper = grain(floor(UV * vec2(110.0, 560.0)));
    float pigment = wash(UV * vec2(7.0, 19.0)) * .7 + wash(UV * vec2(15.0, 39.0)) * .3;
    float edge = abs(UV.y - .51) * 1.96 + (pigment - .5) * .12;
    float formed = 1.0 - smoothstep(reveal - .11, reveal + .07, edge);
    formed = mix(formed, 1.0, smoothstep(.97, 1.0, reveal));
    ink.rgb = mix(ink.rgb, vec3(.48, .28, .16) * (.78 + paper * .2), heat * .68);
    COLOR = vec4(ink.rgb, ink.a * formed) * item_tint;
}
