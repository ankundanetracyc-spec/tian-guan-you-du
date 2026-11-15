shader_type canvas_item;
render_mode unshaded;

uniform float hover_sector = -1.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
    return mix(mix(hash(i), hash(i+vec2(1,0)),f.x), mix(hash(i+vec2(0,1)),hash(i+vec2(1)),f.x),f.y);
}
float wash(vec2 p) { return noise(p)*0.52 + noise(p*2.1)*0.28 + noise(p*4.3)*0.14 + noise(p*8.7)*0.06; }
vec3 pigment(float i) {
    if (i < 0.5) return vec3(0.27,0.47,0.43);
    if (i < 1.5) return vec3(0.72,0.55,0.28);
    if (i < 2.5) return vec3(0.43,0.50,0.31);
    if (i < 3.5) return vec3(0.65,0.44,0.26);
    if (i < 4.5) return vec3(0.34,0.41,0.47);
    if (i < 5.5) return vec3(0.36,0.40,0.55);
    if (i < 6.5) return vec3(0.63,0.31,0.25);
    return vec3(0.46,0.37,0.45);
}
void fragment() {
    vec2 p = (UV-vec2(0.5))*2.0;
    float r = length(p);
    float n = wash(p*3.5+vec2(13,27));
    float grain = noise(UV*360.0);
    float a = mod(atan(p.y,p.x)+1.570796327+6.283185307,6.283185307);
    float mood = a*1.273239545;

    // 八种心境颜色以周期余弦权重混合，形成连续的环绕色场。
    // 权重在整圈连续，包括首尾相接处；不按扇区硬切颜色。
    vec3 color = vec3(0.0);
    float weight_sum = 0.0;
    for (int i=0; i<8; i++) {
        float mood_phase = (mood-float(i))*0.7853981634;
        float weight = exp((cos(mood_phase)-1.0)*3.2);
        color += pigment(float(i))*weight;
        weight_sum += weight;
    }
    color /= max(weight_sum,0.0001);

    // 径向浓淡始终由距离主导；纹理不能推歪渐变、挖出一圈留白洞。
    // 从圆心连续增加至圆周，中心没有截断平台，外缘只留很窄的渗纸边。
    float density = pow(clamp(r,0.0,1.0),1.55);
    float edge = 1.0-smoothstep(0.975,1.0,r+(n-0.5)*0.008);
    float mottled = 0.99+(n-0.5)*0.05+(grain-0.5)*0.012;
    // 只留轻微颜料颗粒，避免大块水彩斑驳被看成色域拼缝。
    color *= 0.985+(n-0.5)*0.04+(grain-0.5)*0.018;

    // 悬停只给对应色域轻微加墨，跨越标签边界时也不会出现扇面硬边。
    if (hover_sector >= 0.0) {
        float hover_delta = abs(mod(mood-hover_sector+4.0,8.0)-4.0);
        float hover_wash = exp(-(hover_delta*hover_delta)/0.34);
        color = mix(color,color*1.06+vec3(0.012),hover_wash*0.10);
        mottled *= 1.0+hover_wash*0.035;
    }
    float alpha = edge*density*mottled*0.76;
    COLOR = vec4(color,alpha);
}
