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
    float n = wash(p*7.0+vec2(13,27));
    float grain = noise(UV*500.0);
    float a = mod(atan(p.y,p.x)+1.570796327+6.283185307,6.283185307);
    float sector = a/0.785398163 + (n-0.5)*0.25;
    float i = floor(sector);
    vec3 color = mix(pigment(mod(i,8.0)),pigment(mod(i+1.0,8.0)),smoothstep(0.16,0.84,fract(sector)));
    // 径向浓淡始终由距离主导；纹理不能推歪渐变、挖出一圈留白洞。
    // 从圆心连续增加至圆周，中心没有截断平台，外缘只留很窄的渗纸边。
    float density = pow(clamp(r,0.0,1.0),1.65);
    float edge = 1.0-smoothstep(0.97,1.0,r+(n-0.5)*0.015);
    float mottled = 0.92+(n-0.5)*0.45+(grain-0.5)*0.08;
    // 保留颜料沉积的轻微云纹；不再通过扭曲半径制造斑块。
    color *= 0.94+n*0.10+grain*0.03;
    float selected = 1.0-smoothstep(0.15,0.65,abs(mod(sector-hover_sector+4.0,8.0)-4.0));
    float alpha = edge*density*mottled*0.76;
    if (hover_sector >= 0.0) alpha *= 1.0+selected*0.16;
    COLOR = vec4(color,alpha);
}
