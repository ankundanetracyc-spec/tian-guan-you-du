shader_type canvas_item;
render_mode unshaded;
uniform float power : hint_range(0.0,1.0)=0.0;
uniform float residual : hint_range(0.0,1.0)=0.0;
float hash21(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise2(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash21(i),hash21(i+vec2(1.0,0.0)),f.x),mix(hash21(i+vec2(0.0,1.0)),hash21(i+vec2(1.0)),f.x),f.y);}
float wash(vec2 p){float n=0.0,a=0.55;for(int i=0;i<4;i++){n+=a*noise2(p);p=mat2(vec2(1.7,1.2),vec2(-1.2,1.7))*p+vec2(7.1,3.4);a*=0.48;}return n;}
float plume(vec2 p,float center,float phase,float rise,float width){
    float y=1.0-p.y;
    float flow=TIME*(1.6+power*1.8);
    float bend=(wash(vec2(y*3.8+phase,flow*.42))-0.48)*0.24*y;
    bend+=sin(y*8.0-flow*1.1+phase)*0.043*y;
    float curl=wash(vec2(p.x*6.0+phase,p.y*8.0+flow));
    float tip=rise+(curl-.45)*.19+sin(flow*1.25+phase)*.045;
    float spread=width*pow(clamp(1.0-y/max(tip,.01),0.0,1.0),.64);
    float edge=abs(p.x-center-bend)+(curl-.5)*.065;
    return (1.0-smoothstep(spread*.62,spread+.033,edge))*(1.0-smoothstep(tip-.10,tip+.025,y));
}
void fragment(){
    float y=1.0-UV.y;
    float h=.18+power*.88;
    float flame=0.0;
    flame=max(flame,plume(UV,.48,.0,h,.16+power*.14));
    flame=max(flame,plume(UV,.29,2.1,h*.77,.10+power*.08));
    flame=max(flame,plume(UV,.70,4.8,h*.88,.09+power*.09));
    flame=max(flame,plume(UV,.57,8.2,h*.67,.08+power*.08));
    float active=smoothstep(.0,.045,power);
    float detail=wash(vec2(UV.x*9.0,UV.y*12.0+TIME*3.2));
    float inner=plume(UV,.48,1.3,h*.54,.07+power*.12);
    vec3 color=mix(vec3(.55,.19,.065),vec3(.86,.43,.13),clamp(flame*.8+detail*.3,0.0,1.0));
    color=mix(color,vec3(.98,.81,.43),inner*.83*(1.0-smoothstep(.12,.67,y)));
    float grain=hash21(floor(UV*vec2(450.0,300.0)));
    color*=.94+grain*.08;
    float alpha=flame*active*(.82+detail*.16);
    float sparks=0.0;
    for(int i=0;i<6;i++){float seed=float(i);float age=fract(TIME*(.19+seed*.027)+seed*.173);vec2 at=vec2(.24+hash21(vec2(seed,5.0))*.52+sin(age*8.0+seed)*.035,1.0-age*.87);float dot_ink=1.0-smoothstep(.002,.011,length((UV-at)*vec2(1.0,.82)));sparks+=dot_ink*(1.0-age)*smoothstep(.3,.8,power);}
    float ember=residual*smoothstep(.88,1.0,UV.y)*.14;
    color=mix(color,vec3(.91,.61,.23),clamp(sparks,0.0,1.0));
    alpha=max(alpha,max(sparks*.65,ember));
    alpha*=smoothstep(.0,.035,UV.x)*(1.0-smoothstep(.965,1.0,UV.x))*smoothstep(.0,.025,UV.y);
    COLOR=vec4(color,clamp(alpha,0.0,.98));
}
