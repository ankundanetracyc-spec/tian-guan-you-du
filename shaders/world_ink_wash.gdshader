shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
uniform float wash_strength : hint_range(0.0, 1.0) = 0.75;

void fragment() {
    vec3 original = texture(screen_texture, SCREEN_UV).rgb;
    float luminance = dot(original, vec3(0.2126, 0.7152, 0.0722));
    vec3 muted = mix(original, vec3(luminance), 0.75);
    vec3 wash = mix(muted * vec3(0.985, 1.01, 1.05), vec3(0.95, 0.94, 0.89), 0.04);
    COLOR = vec4(mix(original, wash, wash_strength), 1.0);
}
