shader_type canvas_item;
render_mode unshaded;
uniform float effect_time = 0.0;
uniform float presence = 1.0;
uniform float activity = 0.0;
uniform vec2 flow_path[6];
void fragment() {
    vec4 art = texture(TEXTURE,UV);
    float nearest = 100.0;
    float distance_along = 0.0;
    float total = 0.0;
    for (int i=0; i<5; i++) {
        vec2 ab=flow_path[i+1]-flow_path[i];
        float length_ab=length(ab);
        float t=clamp(dot(UV-flow_path[i],ab)/max(dot(ab,ab),0.000001),0.0,1.0);
        float d=length(UV-flow_path[i]-t*ab);
        if(d<nearest) {nearest=d; distance_along=total+t*length_ab;}
        total+=length_ab;
    }
    float u=distance_along/max(total,0.00001);
    float wave=fract(u-effect_time*.23);
    float pulse=exp(-pow((wave-.28)*15.0,2.0));
    // Ruby membrane, with travelling reflections from the shoulder end to fingers.
    // No screen bloom or opaque pale fill; preserve the approved painted anatomy.
    vec3 color=art.rgb*vec3(.88,.60,.66);
    color=mix(color,vec3(.96,.13,.17),pulse*.18+activity*.10);
    color+=vec3(.10,.021,.018)*pulse;
    float alpha=art.a*presence*(.76+.09*pulse);
    COLOR=vec4(color,alpha);
}
