shader_type canvas_item;
render_mode unshaded;
uniform sampler2D ink_noise: repeat_enable, filter_linear;
uniform float effect_time=0.0;
void fragment() {
    vec4 pigment=COLOR;
    float v=abs(UV.y-.5)*2.0;
    float grain=texture(ink_noise,vec2(UV.x*4.0-effect_time*.11,UV.y*.60)).r;
    float edge=1.0-smoothstep(.56+grain*.18,1.0,v);
    float broken_edge=smoothstep(.14,.38,grain+(1.0-v)*.48);
    float light=pow(1.0-v,5.0)*(.3+.7*grain);
    vec3 red=mix(pigment.rgb,vec3(1.0,.20,.20),light*.32);
    COLOR=vec4(red,pigment.a*edge*broken_edge);
}
