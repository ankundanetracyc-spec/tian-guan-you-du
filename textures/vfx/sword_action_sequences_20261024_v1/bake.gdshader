shader_type canvas_item;
render_mode unshaded;
uniform float progress=0.0;
uniform int kind=0;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 a=floor(p),b=fract(p);b=b*b*(3.-2.*b);return mix(mix(hash(a),hash(a+vec2(1,0)),b.x),mix(hash(a+vec2(0,1)),hash(a+vec2(1,1)),b.x),b.y);}
float line(vec2 p,vec2 a,vec2 b,float w){vec2 v=b-a;float h=clamp(dot(p-a,v)/max(dot(v,v),.00001),0.,1.);return 1.-smoothstep(w*.5,w,length(p-a-v*h));}
float ray(vec2 p,vec2 c,vec2 dir,float reach,float width){vec2 d=p-c;float h=dot(d,dir)/max(reach,.0001);float across=abs(dot(d,vec2(-dir.y,dir.x)));float w=width*pow(max(1.-h,0.),1.6);return (1.-smoothstep(w*.45,max(w,.0005),across))*smoothstep(-.01,.01,h)*(1.-smoothstep(.96,1.,h));}
void coat(inout vec3 rgb,inout float alpha,vec3 color,float a){a=clamp(a,0.,1.);rgb=mix(rgb,color,a/max(alpha+a*(1.-alpha),.0001));alpha=alpha+a*(1.-alpha);}
void fragment(){
 vec2 p=UV;float t=progress;
 float grain=noise(p*180.)*.35+noise(p*68.)*.4+noise(p*19.)*.25;
 float dry=smoothstep(.20,.70,grain);
 float dissolve=1.-smoothstep(.32+.23*noise(p*12.),1.0,t);
 vec3 rgb=vec3(0.);float a=0.;
 vec3 jade=vec3(.14,.34,.35),silver=vec3(.48,.70,.65),ivory=vec3(.98,.95,.82),ochre=vec3(.67,.45,.23);
 if(kind==0){
   vec2 q=(p-vec2(.20,.55))/vec2(.64,.40);float r=length(q),ang=atan(q.y,q.x);
   float lead=mix(-1.48,1.05,1.-pow(1.-clamp(t/.34,0.,1.),2.));
   float span=smoothstep(-1.55,-1.30,ang)*(1.-smoothstep(lead-.035,lead+.035,ang));
   float tail=smoothstep(lead-2.4,lead-.2,ang)*(.6+.4*dry);
   float taper=pow(max(sin(clamp((ang+1.48)/2.53,0.,1.)*3.14159),0.),.65);
   float width=.003+.065*taper;
   float wet=1.-smoothstep(width*.4,width*1.45,abs(r-1.+(noise(p*22.)-.5)*.018));
   float band=(1.-smoothstep(width*.25,width,abs(r-1.)))*span*tail*dissolve;
   coat(rgb,a,jade,wet*span*tail*dissolve*.80);
   coat(rgb,a,silver,band*(.68+.30*dry));
   float rim=exp(-pow((r-1.-width*.45)*170.,2.))*span*tail*dissolve*taper;
   coat(rgb,a,ivory,rim*.94);
   float nib=exp(-pow((ang-lead)*9.,2.))*band*(1.-smoothstep(.28,.48,t));
   coat(rgb,a,ivory,nib*.92);
   float echo=exp(-pow((r-.91)*85.,2.))*span*dry*(1.-smoothstep(.25,.63,t));
   coat(rgb,a,jade,echo*.30);
 }else if(kind==1||kind==4){
   vec2 c=vec2(.5);float age=clamp(t,0.,1.);float hit=exp(-age*8.)*smoothstep(0.,.025,age);
   float pressure=ray(p,c,normalize(vec2(.045,.078)),.11,.006)*(1.-smoothstep(.08,.42,t));
   coat(rgb,a,jade,pressure*.96);
   for(int i=0;i<6;i++){
     float seed=float(i);float theta=seed*2.399+.26;
     vec2 dir=vec2(cos(theta),sin(theta));
     float reach=(kind==1?.24:.17)*mix(.65,1.,hash(vec2(seed,8.)));
     float front=reach*(1.-exp(-t*24.));
     float width=mix(.008,.018,hash(vec2(seed,4.)))*(1.-smoothstep(.1,.6,t));
     float spike=ray(p,c,dir,front,max(width,.001))*exp(-t*5.5)*(.4+.6*dry);
     coat(rgb,a,mix(silver,ochre,hash(vec2(seed,9.))),spike*.98);
     coat(rgb,a,ivory,ray(p,c,dir,front*.85,max(width*.25,.0005))*exp(-t*7.));
   }
   float dist=length((p-c)*vec2(1.,1.1));
   float ring=exp(-pow((dist-(.02+.21*(1.-exp(-t*12.))))*170.,2.));
   float theta=atan(p.y-c.y,p.x-c.x);
   float arc=smoothstep(-.8,-.65,theta)*(1.-smoothstep(-.25,-.10,theta))+smoothstep(2.0,2.1,theta)*(1.-smoothstep(2.5,2.65,theta));
   coat(rgb,a,silver,ring*arc*exp(-t*5.)*.24*dry);
   coat(rgb,a,ivory,exp(-dot(p-c,p-c)*900.)*hit*.9);
 }else if(kind==2){
   float lead=.17+.69*smoothstep(.04,.78,t);
   float in_blade=smoothstep(.15,.20,p.x)*(1.-smoothstep(.83,.89,p.x));
   float head=exp(-pow((p.x-lead)*22.,2.));
   float quiet=smoothstep(.0,.12,t)*(1.-smoothstep(.58,1.,t));
   float track=exp(-pow((p.y-.52)*70.,2.))*in_blade;
   coat(rgb,a,silver,track*quiet*.46);
   coat(rgb,a,ivory,exp(-pow((p.y-.52)*85.,2.))*head*quiet*.94);
   for(int j=0;j<2;j++){
     float s=float(j)*2.-1.;
     float y=.52+s*(.025+.025*sin(p.x*7.+float(j)*1.8+t));
     float partial=smoothstep(.23+float(j)*.18,.33+float(j)*.18,p.x)*(1.-smoothstep(.62+float(j)*.13,.75+float(j)*.13,p.x));
     float ribbon=exp(-pow((p.y-y)*240.,2.))*partial*(.3+.7*head)*quiet;
     coat(rgb,a,jade,ribbon*dry*.26);
   }
 }else{
   float lead=.12+.79*(1.-pow(1.-clamp(t/.28,0.,1.),2.));
   float tail=.12+.65*smoothstep(.38,.92,t);
   float span=smoothstep(tail-.02,tail+.025,p.x)*(1.-smoothstep(lead-.02,lead+.012,p.x));
   float along=clamp((p.x-tail)/max(lead-tail,.01),0.,1.);
   float profile=pow(max(sin(along*3.14159),0.),.8)*pow(1.-along,.3);
   float w=.001+.033*profile;
   float y=.51+.003*sin(p.x*24.);
   float body=(1.-smoothstep(w*.35,w,abs(p.y-y)))*span*dissolve;
   coat(rgb,a,jade,(1.-smoothstep(w,w*2.3,abs(p.y-y)))*span*dissolve*.38*dry);
   coat(rgb,a,silver,body*(.7+.3*dry));
   coat(rgb,a,ivory,exp(-pow((p.y-y)*210.,2.))*span*dissolve*.98);
   for(int j=0;j<2;j++){
     float s=float(j)*2.-1.;float ridge=.51+s*(.025+.035*sin(p.x*3.14159));
     float echo=exp(-pow((p.y-ridge)*160.,2.))*span*dry*(1.-smoothstep(.24,.65,t));
     coat(rgb,a,silver,echo*.36);
   }
 }
 // Staggered births and decelerating curved drift, baked into every frame.
 for(int i=0;i<16;i++){
   if(kind==2&&i>=7)break;
   float n=float(i),u=hash(vec2(n,3.)),v=hash(vec2(n,11.));
   float birth=.10+.24*hash(vec2(n,29.));float life=.35+.53*hash(vec2(n,41.));
   float age=clamp((t-birth)/life,0.,1.);
   vec2 origin;vec2 vel;
   if(kind==0){float theta=mix(-1.25,.9,u);origin=vec2(.20,.55)+vec2(cos(theta)*.64,sin(theta)*.4);vel=vec2(.08,.08)*(v+.4);}
   else if(kind==2){origin=vec2(.22+.58*u,.50+.045*(v-.5));vel=vec2(.012,-.06)*(v+.5);}
   else if(kind==3){origin=vec2(.20+.64*u,.51+.04*(v-.5));vel=vec2(.07,.06*(v-.5));}
   else{float theta=u*6.28318;origin=vec2(.5)+vec2(cos(theta),sin(theta))*.04;vel=vec2(cos(theta),sin(theta))*(kind==1?.17:.12);}
   vec2 at=origin+vel*(1.-exp(-age*2.4))+vec2(.008*sin(age*4.+n),.025*age*age);
   vec2 delta=(p-at)/(kind==2?vec2(.0035+.0035*v,.009+.005*u):vec2(.0018+.0028*v,.0014+.0016*u));
   float mote=exp(-dot(delta,delta))*smoothstep(0.,.10,age)*pow(1.-smoothstep(.25,1.,age),1.5);
   coat(rgb,a,mix(jade,ivory,v),mote*(kind==2?.46:.82));
 }
 float edge=smoothstep(.015,.04,p.x)*smoothstep(.015,.04,p.y)*smoothstep(.015,.04,1.-p.x)*smoothstep(.015,.04,1.-p.y);
 a*=edge*smoothstep(0.,.018,t)*(1.-smoothstep(.94,1.,t));
 COLOR=vec4(rgb,a);
}
