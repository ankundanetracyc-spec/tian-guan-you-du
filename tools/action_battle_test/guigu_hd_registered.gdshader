shader_type canvas_item;

// Original UVs remain linear on every source triangle. Registration happens
// per fragment, so sparse source vertices cannot cut across a control region.
uniform int registration_mode = 0;
uniform int control_count = 0;
uniform int triangle_count = 0;
uniform vec2 source_size = vec2(1.0);
uniform vec2 source_points[32];
uniform vec2 target_points[32];
uniform float source_widths[32];
uniform float target_widths[32];
uniform vec3 control_triangles[64];
uniform vec3 affine_x = vec3(1.0,0.0,0.0);
uniform vec3 affine_y = vec3(0.0,1.0,0.0);
uniform float joint_blend = 0.16;
uniform bool use_sprite_paint = true;
uniform bool paint_enabled = false;
varying vec4 mesh_tint;

float cross2(vec2 a, vec2 b) { return a.x*b.y-a.y*b.x; }
vec2 perpendicular(vec2 a) { return vec2(a.y,-a.x); }

vec2 registered_uv(vec2 uv, vec2 texture_pixel_size) {
    vec2 p = uv*source_size;
    if (registration_mode==3) {
        return vec2(dot(affine_x,vec3(p,1.0)),dot(affine_y,vec3(p,1.0)))*texture_pixel_size;
    }
    if (registration_mode==1) {
        float best_penalty = 1e20;
        vec2 mapped = uv/texture_pixel_size;
        for (int i=0; i<64; i++) {
            if (i>=triangle_count) { break; }
            ivec3 ids = ivec3(control_triangles[i]);
            vec2 a=source_points[ids.x];
            vec2 b=source_points[ids.y];
            vec2 c=source_points[ids.z];
            float determinant=cross2(b-a,c-a);
            if (abs(determinant)<0.000001) { continue; }
            float wb=cross2(p-a,c-a)/determinant;
            float wc=cross2(b-a,p-a)/determinant;
            float wa=1.0-wb-wc;
            float penalty=max(0.0,-wa)+max(0.0,-wb)+max(0.0,-wc);
            if (penalty<best_penalty) {
                best_penalty=penalty;
                mapped=target_points[ids.x]*wa+target_points[ids.y]*wb+target_points[ids.z]*wc;
            }
            if (penalty<0.000001) { break; }
        }
        return mapped*texture_pixel_size;
    }
    if (registration_mode==2 && control_count>=2) {
        int chosen=0;
        float parameter=0.0;
        float best_distance=1e20;
        for (int i=0; i<31; i++) {
            if (i>=control_count-1) { break; }
            vec2 axis=source_points[i+1]-source_points[i];
            float t=dot(p-source_points[i],axis)/max(0.000001,dot(axis,axis));
            float clipped=clamp(t,0.0,1.0);
            vec2 difference=p-(source_points[i]+axis*clipped);
            float distance=dot(difference,difference);
            if (distance<best_distance) {
                best_distance=distance;
                chosen=i;
                parameter=((i==0 && t<0.0)||(i==control_count-2 && t>1.0))?t:clipped;
            }
        }
        vec2 source_direction=normalize(source_points[chosen+1]-source_points[chosen]);
        vec2 target_direction=normalize(target_points[chosen+1]-target_points[chosen]);
        if (chosen>0 && parameter<joint_blend) {
            vec2 source_average=normalize(normalize(source_points[chosen]-source_points[chosen-1])+source_direction);
            vec2 target_average=normalize(normalize(target_points[chosen]-target_points[chosen-1])+target_direction);
            float amount=smoothstep(0.0,joint_blend,parameter);
            source_direction=normalize(mix(source_average,source_direction,amount));
            target_direction=normalize(mix(target_average,target_direction,amount));
        } else if (chosen<control_count-2 && parameter>1.0-joint_blend) {
            vec2 source_average=normalize(normalize(source_points[chosen+2]-source_points[chosen+1])+source_direction);
            vec2 target_average=normalize(normalize(target_points[chosen+2]-target_points[chosen+1])+target_direction);
            float amount=smoothstep(1.0-joint_blend,1.0,parameter);
            source_direction=normalize(mix(source_direction,source_average,amount));
            target_direction=normalize(mix(target_direction,target_average,amount));
        }
        vec2 source_center=mix(source_points[chosen],source_points[chosen+1],parameter);
        vec2 target_center=mix(target_points[chosen],target_points[chosen+1],parameter);
        float sw=mix(source_widths[chosen],source_widths[chosen+1],clamp(parameter,0.0,1.0));
        float tw=mix(target_widths[chosen],target_widths[chosen+1],clamp(parameter,0.0,1.0));
        float transverse=dot(p-source_center,perpendicular(source_direction));
        return (target_center+perpendicular(target_direction)*transverse*tw/max(0.0001,sw))*texture_pixel_size;
    }
    return uv;
}

void vertex() { mesh_tint=COLOR; }
void fragment() { COLOR=texture(TEXTURE,registered_uv(UV,TEXTURE_PIXEL_SIZE))*mesh_tint; }
