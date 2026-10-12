shader_type canvas_item;
render_mode unshaded;

// Presentation only. The web preview uses this same fragment shader.
uniform float animation_time = 0.0;
uniform float rotation_phase = 0.0;
uniform float inner_rotation_phase = 0.0;
uniform float cloud_radius = 0.30;
uniform float cloud_density = 0.58;
uniform float core_amount = 0.0;
uniform float core_heat = 0.0;
uniform float inward_pull = 0.0;
uniform float rebound = 0.0;
uniform float aspect_ratio = 1.0;
uniform sampler2D swirl_texture;
uniform float path_visibility = 0.0;
uniform float path_progress = 0.0;
uniform float path_surge = 0.0;
uniform float breath_count = 0.0;
uniform float breath_previous_count = 9.0;
uniform float breath_reveal = 1.0;

float qi_hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float qi_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(qi_hash(i), qi_hash(i + vec2(1.0, 0.0)), f.x),
               mix(qi_hash(i + vec2(0.0, 1.0)), qi_hash(i + vec2(1.0)), f.x), f.y);
}

float qi_fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    mat2 turn = mat2(vec2(0.80, -0.60), vec2(0.60, 0.80));
    for (int i = 0; i < 4; i++) {
        v += a * qi_noise(p);
        p = turn * p * 2.03 + vec2(11.7, 8.3);
        a *= 0.5;
    }
    return v;
}

// Concentric circles on three separate, slowly turning spatial planes.
// Orthographic projection of a circle rotated about its horizontal axis.
vec4 qi_ring_shape(float index) {
    float radius = 0.115 + index * 0.093;
    float inclination = (index < 0.5 ? 0.86 : index < 1.5 ? -0.74 : 0.62)
                      + 0.25 * sin(animation_time * (0.18-index*0.025) + index*1.7);
    float roll = -0.78 + index * 0.83 + animation_time * (0.035-index*0.009)
               + 0.16 * sin(animation_time * 0.12 + index*1.9);
    return vec4(radius, radius * cos(inclination), roll, inclination);
}
vec2 qi_ring_center(float index) { return vec2(0.0); }
vec2 qi_ring_point(float index, float theta) {
    vec4 shape = qi_ring_shape(index);
    mat2 forward = mat2(vec2(cos(shape.z), sin(shape.z)), vec2(-sin(shape.z), cos(shape.z)));
    return forward * (vec2(cos(theta), sin(theta)) * shape.xy) + qi_ring_center(index);
}
float qi_bridge(vec2 p, vec2 start, vec2 end, float visible) {
    vec2 bend = (start + end) * 0.5 + vec2(-(end-start).y, (end-start).x) * 0.26;
    float distance_to_stream = 1.0;
    float location = 0.0;
    vec2 previous = start;
    for (int j = 1; j <= 8; j++) {
        float u = float(j) / 8.0;
        vec2 next_point = mix(mix(start, bend, u), mix(bend, end, u), u);
        vec2 segment = next_point - previous;
        float portion = clamp(dot(p-previous, segment) / max(dot(segment,segment),0.000001),0.0,1.0);
        float d = length(p - previous - segment * portion);
        if (d < distance_to_stream) { distance_to_stream = d; location = (float(j-1)+portion)/8.0; }
        previous = next_point;
    }
    float revealed = 1.0 - smoothstep(visible-0.08,visible,location);
    float pulse = 0.6 + 0.4 * pow(max(0.0,cos(location*12.0-animation_time*2.0)),4.0);
    return exp(-pow(distance_to_stream/0.0025,2.0)) * revealed * pulse * 0.34;
}
vec4 qi_path(vec2 p) {
    if (path_visibility < 0.001) return vec4(0.0);
    vec4 ring0 = vec4(0.0), ring1 = vec4(0.0), ring2 = vec4(0.0);
    float z0 = 0.0, z1 = 0.0, z2 = 0.0;
    for (int i = 0; i < 3; i++) {
        float index = float(i);
        float completion = clamp(path_progress * 3.0 - index, 0.0, 1.0);
        vec4 shape = qi_ring_shape(index);
        mat2 undo = mat2(vec2(cos(shape.z), -sin(shape.z)), vec2(sin(shape.z), cos(shape.z)));
        vec2 ellipse = (undo * (p - qi_ring_center(index))) / shape.xy;
        float theta = atan(ellipse.y,ellipse.x);
        float entry_angle = -2.55 + index * 2.1;
        float along = mod(theta-entry_angle+12.5663706,6.2831853)/6.2831853;
        float active = (1.0-smoothstep(completion-0.018,completion,along))*step(0.001,completion);
        active = mix(active,1.0,step(0.999,completion));
        float depth = 0.5 + 0.5*sin(theta)*sin(shape.w);
        float grain = qi_fbm(vec2(theta*2.4-animation_time*0.23,index*3.7));
        float radial_scale = length(vec2(cos(theta)/shape.x,sin(theta)/shape.y));
        float deviation = (length(ellipse)-1.0)/max(radial_scale,0.001);
        float width = (0.0021+grain*0.0022)*(0.48+depth*1.10);
        float ribbon = exp(-pow(deviation/width,2.0));
        float mist = exp(-pow(deviation/0.009,2.0));
        float strand = exp(-pow((deviation+sin(theta*9.0-animation_time*0.65+index)*0.002)/0.0009,2.0));
        float head = fract(animation_time*0.085-index*0.19);
        float trail = exp(-mod(head-along+1.0,1.0)*32.0);
        float bead = pow(max(0.0,cos((along-head)*18.849556)),72.0);
        float density = (ribbon*(0.28+grain*0.23+trail*0.42+bead*0.22)+strand*0.14+mist*0.075)*active*(0.20+depth*0.95);
        vec2 end = qi_ring_point(index,entry_angle);
        vec2 start = vec2(0.0);
        if (i > 0) start = qi_ring_point(index-1.0,entry_angle);
        float connector = qi_bridge(p,start,end,smoothstep(0.0,0.15,completion));
        if (i == 0) connector *= smoothstep(0.035,0.06,length(p));
        float alpha = clamp(density+connector,0.0,0.88);
        vec3 ink = mix(vec3(0.48,0.60,0.58),vec3(0.92,0.91,0.80),depth*0.65+trail*0.25);
        vec4 sample_ring = vec4(ink*alpha,alpha);
        float spatial_depth = sin(theta)*sin(shape.w)*shape.x;
        if (i == 0) { ring0 = sample_ring; z0 = spatial_depth; }
        else if (i == 1) { ring1 = sample_ring; z1 = spatial_depth; }
        else { ring2 = sample_ring; z2 = spatial_depth; }
    }
    // Sort the three translucent samples by their actual depth at this pixel.
    vec4 swap_ring; float swap_z;
    if (z0 > z1) { swap_ring=ring0;ring0=ring1;ring1=swap_ring;swap_z=z0;z0=z1;z1=swap_z; }
    if (z1 > z2) { swap_ring=ring1;ring1=ring2;ring2=swap_ring;swap_z=z1;z1=z2;z2=swap_z; }
    if (z0 > z1) { swap_ring=ring0;ring0=ring1;ring1=swap_ring; }
    vec4 combined = ring1 + ring0*(1.0-ring1.a);
    combined = ring2 + combined*(1.0-ring2.a);
    return vec4(combined.rgb/max(combined.a,0.0001),combined.a*path_visibility);
}

// Independent wandering in a bounded volume, unrelated to the ring rotation.
vec3 qi_breath_waypoint(float seed, float step_index) {
    float angle = qi_hash(vec2(seed*7.13+31.0,step_index+83.0))*6.2831853;
    float radius = 0.09+qi_hash(vec2(seed+57.0,step_index*3.17+19.0))*0.20;
    float depth = qi_hash(vec2(seed*4.3+11.0,step_index+127.0))*1.6-0.8;
    return vec3(cos(angle)*radius,sin(angle)*radius*0.86,depth);
}
vec3 qi_breath_position(float seed, float time) {
    float travel = time*(0.22+0.045*fract(seed*0.618))+seed*2.71;
    float segment = floor(travel);
    float u = fract(travel);
    vec3 a = qi_breath_waypoint(seed,segment-1.0);
    vec3 b = qi_breath_waypoint(seed,segment);
    vec3 c = qi_breath_waypoint(seed,segment+1.0);
    vec3 d = qi_breath_waypoint(seed,segment+2.0);
    return 0.5*((2.0*b)+(-a+c)*u+(2.0*a-5.0*b+4.0*c-d)*u*u+(-a+3.0*b-3.0*c+d)*u*u*u);
}
// Soft, irregular vapor clusters. No beads, shells or extra orbit lines.
vec4 qi_breaths(vec2 p, float front_layer) {
    if (breath_count < 0.5) return vec4(0.0);
    vec3 color_sum = vec3(0.0);
    float alpha_sum = 0.0;
    for (int i = 0; i < 9; i++) {
        float seed = float(i);
        if (seed + 0.5 > breath_count) continue;
        vec3 position = qi_breath_position(seed,animation_time);
        float reveal = seed + 0.5 > breath_previous_count ? breath_reveal : 1.0;
        position *= mix(0.18, 1.0, reveal);
        float z = position.z;
        float near_side = clamp(0.5+0.5*z,0.0,1.0);
        if ((z >= 0.0 && front_layer < 0.5) || (z < 0.0 && front_layer > 0.5)) continue;
        vec2 center = position.xy*(1.0+z*0.10);
        vec2 velocity = qi_breath_position(seed,animation_time+0.035).xy-position.xy;
        vec2 tangent = velocity/max(length(velocity),0.00001);
        float breathing_phase = mix(seed*1.3,0.0,step(8.5,breath_count));
        float size = (0.018+near_side*0.009)*(1.0+0.12*sin(animation_time*0.9+breathing_phase));
        vec2 offset = p-center;
        if (dot(offset,offset) > 0.012) continue;
        vec2 local = vec2(dot(offset,tangent),dot(offset,vec2(-tangent.y,tangent.x)))/size;
        float gas = qi_fbm(local*2.0+vec2(seed*4.1,animation_time*0.25));
        float body = exp(-dot(local,local)*(1.45+gas*0.9));
        vec2 tail_local = local+vec2(1.1,0.12*sin(animation_time+seed));
        float tail = exp(-tail_local.x*tail_local.x*0.38-tail_local.y*tail_local.y*3.5);
        float fine = exp(-pow(local.y+sin(local.x*3.0+animation_time*0.6+seed)*0.15,2.0)*25.0)*exp(-local.x*local.x*1.3);
        float alpha = clamp((body*(0.50+gas*0.35)+tail*0.18+fine*0.12)*(0.52+near_side*0.48),0.0,0.8);
        alpha *= reveal;
        vec3 ink = mix(vec3(0.53,0.66,0.63),vec3(0.95,0.94,0.82),body*0.65+near_side*0.20);
        color_sum += ink*alpha*(1.0-alpha_sum);
        alpha_sum += alpha*(1.0-alpha_sum);
    }
    return vec4(color_sum/max(alpha_sum,0.0001),alpha_sum);
}

void fragment() {
    vec2 p = UV - vec2(0.5);
    p.x *= aspect_ratio;
    float breath = 1.0 + 0.018 * sin(animation_time * 0.72);
    float radius = max(0.035, cloud_radius * breath + rebound * 0.032);
    vec2 q = p / radius;
    float r = length(q);
    float angle = rotation_phase + (1.0 - min(r, 1.0)) * inward_pull * 1.35;
    mat2 rotation = mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
    vec2 flow = rotation * q;
    float drift = animation_time * 0.045 + rotation_phase * 0.12;
    vec2 warp = vec2(qi_fbm(flow * 2.2 + vec2(drift, 7.1)),
                     qi_fbm(flow * 2.0 + vec2(3.4, -drift))) - vec2(0.46);
    float body = qi_fbm(flow * 3.6 + warp * 2.0 + vec2(drift * 0.3, -drift));
    float folds = qi_fbm(flow * 6.8 + warp * 2.6 - vec2(drift, 0.0));
    // Reuse the original curved vapor strands, not a round noise cloud.
    // The outer and inner layers turn at different speeds, as in the original panel.
    vec2 outer_uv = flow * 0.5 + warp * 0.014 + vec2(0.5);
    float inner_angle = inner_rotation_phase - 0.38;
    mat2 inner_rotation = mat2(vec2(cos(inner_angle), -sin(inner_angle)), vec2(sin(inner_angle), cos(inner_angle)));
    vec2 inner_uv = inner_rotation * q * 0.68 + warp * 0.010 + vec2(0.5);
    float outer_strands = texture(swirl_texture, clamp(outer_uv, vec2(0.0), vec2(1.0))).a * 2.1;
    float inner_strands = texture(swirl_texture, clamp(inner_uv, vec2(0.0), vec2(1.0))).a * 2.1;
    float edge = 1.0 - smoothstep(0.86, 1.15, r);
    float strands = (outer_strands * 0.76 + inner_strands * 0.50) * edge;
    float vapor_variation = 0.74 + body * 0.38 + folds * 0.22;
    // Fine curved folds bridge the texture layers; their thickness varies along the flow.
    float polar = atan(q.y, q.x + 0.00001);
    float spiral_phase = polar * 3.0 + r * 9.8 + rotation_phase * 3.0 + body * 0.65;
    float fine_strands = pow(max(0.0, cos(spiral_phase)), 18.0);
    fine_strands *= smoothstep(0.14, 0.34, r) * (1.0 - smoothstep(0.72, 1.04, r));
    fine_strands *= (0.16 + folds * 0.38) * (0.6 + cloud_density * 0.4);
    // A translucent rear layer gives the vortex depth without filling its open center.
    float rear_mist = (outer_strands + inner_strands) * (0.08 + body * 0.13) * edge;
    float near_side = clamp(0.45 + q.y * 0.52 - q.x * 0.18, 0.0, 1.0);
    // Only a formed core fills the center, with still moving vapor instead of a light spot.
    float core_vapor = core_amount * (1.0 - smoothstep(0.04, 0.57, r + (body - 0.45) * 0.14));
    // A few faint flecks follow the same spiral inward; no separate orbit or particle ring.
    float flecks = 0.0;
    for (int i = 0; i < 8; i++) {
        float seed = float(i);
        float travel = fract(seed * 0.137 + animation_time * 0.012 + rotation_phase * 0.03);
        float fleck_r = 0.92 - travel * 0.70;
        float fleck_angle = seed * 2.39996 + fleck_r * 4.2 - rotation_phase;
        vec2 center = vec2(cos(fleck_angle), sin(fleck_angle)) * fleck_r;
        float distance_to_fleck = length(q - center);
        flecks += exp(-distance_to_fleck * distance_to_fleck * 7000.0) * sin(travel * 3.14159) * 0.17;
    }
    float alpha = clamp(strands * vapor_variation * (0.53 + cloud_density * 0.43) + rear_mist + fine_strands * 0.30 + flecks + core_vapor * 0.66, 0.0, 0.94);
    float front = clamp(inner_strands * 0.70 + fine_strands + near_side * 0.24 + folds * 0.16, 0.0, 1.0);
    vec3 ink = mix(vec3(0.46, 0.54, 0.52), vec3(0.92, 0.91, 0.81), front);
    ink = mix(ink, vec3(0.94, 0.93, 0.83), clamp(fine_strands * 1.2 + flecks * 2.0, 0.0, 0.6));
    ink = mix(ink, vec3(0.82, 0.85, 0.78), core_vapor * 0.35);
    // The formed vapor core briefly turns warm and bright, then cools back.
    float heat = clamp(core_heat,0.0,1.0)*core_amount;
    float heat_body = exp(-r*r*2.8)*(0.65+body*0.35);
    ink = mix(ink,vec3(1.0,0.93,0.70),heat*0.82);
    alpha = clamp(alpha+heat*heat_body*0.60,0.0,0.99);
    float halo = exp(-r*r*1.05)*heat*0.23;
    alpha = max(alpha,halo);
    vec4 path = qi_path(p);
    float combined_alpha = alpha + path.a * (1.0 - alpha);
    vec3 combined_ink = (ink * alpha + path.rgb * path.a * (1.0 - alpha)) / max(combined_alpha, 0.0001);
    vec4 back_breath = qi_breaths(p,0.0);
    vec4 front_breath = qi_breaths(p,1.0);
    vec3 premultiplied = combined_ink*combined_alpha + back_breath.rgb*back_breath.a*(1.0-combined_alpha);
    float with_back = combined_alpha+back_breath.a*(1.0-combined_alpha);
    premultiplied = front_breath.rgb*front_breath.a+premultiplied*(1.0-front_breath.a);
    float total_alpha = front_breath.a+with_back*(1.0-front_breath.a);
    COLOR = vec4(premultiplied/max(total_alpha,0.0001),total_alpha);
}
