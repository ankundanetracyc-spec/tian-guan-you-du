shader_type canvas_item;
render_mode unshaded;

// Static painted texture only: no chamber fill, glow, UV motion or flicker.
uniform sampler2D flame_texture : source_color, filter_linear, repeat_disable;
void fragment() {
    vec2 p = vec2((UV.x - 0.5) / 1.10 + 0.5, UV.y);
    vec4 painted = texture(flame_texture, p);
    // Soften chroma and contrast toward warm paper tones, without multiplying by black.
    float luminance = dot(painted.rgb, vec3(0.299, 0.587, 0.114));
    vec3 muted = mix(painted.rgb, vec3(luminance), 0.28);
    vec3 soft = mix(muted, vec3(0.78, 0.69, 0.51), 0.14);
    COLOR = vec4(soft, painted.a);
}
