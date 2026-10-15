/* Asset export only: all painted pixels come from imagegen source PNGs.
   Run with bundled Node + sharp. No production character resources are written. */
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const OUT = __dirname;
const ROOT = path.resolve(OUT, '../../..');
const W=1024,H=1536;
const transparent={r:0,g:0,b:0,alpha:0};
const write=(p,v)=>fs.writeFileSync(path.join(OUT,p),JSON.stringify(v,null,2)+'\n');
const empty=()=>sharp({create:{width:W,height:H,channels:4,background:transparent}});
const specs=[];
function rect(name,sheet,crop,dest,bone,weights=null){specs.push({name,sheet,crop,dest,bone,weights});}
function limb(name,sheet,crop,from,to,pivots,bone){specs.push({name,sheet,crop,from,to,pivots,bone,thickness:/^(thigh|shin)_/.test(name)?.58:1});}
const P={root:[512,1450],hips:[515,665],lower_abdomen:[517,615],waist:[520,565],spine_mid:[522,520],torso:[525,475],ribcage:[526,435],chest:[527,400],neck:[530,370],head:[548,275]};
for(const [s,sh,el,wr,hip,knee,ankle] of [
 ['near',[420,415],[350,565],[475,580],[470,690],[400,970],[340,1230]],
 ['far',[630,425],[710,510],[770,440],[560,690],[650,955],[715,1205]]]){
 P['scapula_'+s]=[527+(sh[0]-527)*.2,402];P['clavicle_'+s]=[527+(sh[0]-527)*.55,410];
 P['arm_'+s]=sh;P['forearm_'+s]=el;P['hand_'+s]=wr;P['palm_'+s]=[wr[0]+15,wr[1]];
 P['upper_arm_deform_'+s]=sh.map((x,i)=>(x+el[i])/2);P['forearm_deform_'+s]=el.map((x,i)=>(x+wr[i])/2);
 for(const [d,j]of ['thumb','finger','middle','ring','pinky'].map((d,i)=>[d,i]))for(const [suffix,k]of [['',0],['_mid',1],['_tip',2]])P[d+'_'+s+suffix]=[wr[0]+20+k*12,wr[1]+(j-2)*6];
 P['thigh_'+s]=hip;P['shin_'+s]=knee;P['foot_'+s]=ankle;P['toe_'+s]=[ankle[0]+65,ankle[1]+130];
}
P.hair_back=[500,250];P.hair_tip=[395,505];P.ribbon=[495,230];P.robe_near=[460,700];P.robe_far=[565,700];P.robe_center=[515,705];
const old=JSON.parse(fs.readFileSync(path.join(ROOT,'textures/characters/c1_male_puppet_v2/runtime/c1_male_puppet_v2.json')));
const bones=old.bones.map(b=>({name:b.name,...(b.parent?{parent:b.parent}:{})}));
for(const [name,parent] of [['hair_back','head'],['hair_tip','hair_back'],['ribbon','head'],['robe_near','hips'],['robe_far','hips'],['robe_center','hips']])bones.push({name,parent});
for(const b of bones){const p=b.parent?P[b.parent]:[512,1450];b.x=P[b.name][0]-p[0];b.y=p[1]-P[b.name][1];}
const B=Object.fromEntries(bones.map((b,i)=>[b.name,i]));
// Back-to-front order follows this sheet's three-quarter view.
rect('hair_back','correction',[630,630,590,590],[305,200,345,440],'hair_back','hair');
rect('ribbon','body',[745,1180,310,245],[300,220,220,150],'ribbon');
limb('upper_far','correction',[195,690,260,495],P.arm_far,P.forearm_far,[[.50,.12],[.50,.82]],'arm_far');
limb('sleeve_far','limb',[466,375,250,355],P.forearm_far,P.hand_far,[[.38,.1],[.66,.68]],'forearm_far');
limb('hand_far_open','limb',[837,1080,175,332],P.hand_far,[800,360],[[.43,.15],[.53,.85]],'hand_far');
limb('thigh_far','body',[510,1115,205,330],P.thigh_far,P.shin_far,[[.5,.08],[.5,.9]],'thigh_far');
limb('shin_far','limb',[486,730,215,353],P.shin_far,P.foot_far,[[.5,.08],[.5,.9]],'shin_far');
rect('boot_far','limb',[517,1115,225,250],[668,1180,185,205],'foot_far');
limb('thigh_near','body',[100,1110,240,335],P.thigh_near,P.shin_near,[[.5,.08],[.5,.9]],'thigh_near');
limb('shin_near','limb',[115,730,245,351],P.shin_near,P.foot_near,[[.5,.08],[.5,.9]],'shin_near');
rect('boot_near','limb',[153,1085,280,305],[280,1195,215,235],'foot_near');
rect('robe_rear','body',[710,777,375,334],[333,670,410,580],'robe_center','skirt');
rect('robe_far','body',[435,690,280,420],[505,640,305,610],'robe_far','skirt');
rect('robe_center','body',[704,379,382,388],[330,632,400,620],'robe_center','skirt');
rect('robe_near','body',[5,690,395,420],[210,635,375,645],'robe_near','skirt');
rect('torso','correction',[50,55,530,535],[370,350,300,350],'torso','torso');
rect('head','body',[30,0,348,366],[451,148,223,240],'head');
limb('upper_near','correction',[810,65,310,535],P.arm_near,P.forearm_near,[[.56,.12],[.43,.82]],'arm_near');
limb('sleeve_near','limb',[450,0,282,400],P.forearm_near,P.hand_near,[[.38,.1],[.66,.68]],'forearm_near');
limb('hand_near_open','limb',[825,0,157,375],P.hand_near,[550,563],[[.45,.13],[.50,.86]],'hand_near');
rect('belt','body',[425,450,270,315],[370,620,350,235],'hips');
rect('hair_front','body',[780,15,287,395],[460,184,210,292],'head');
// Alternatives occupy the same hand slot and share the same wrist origin.
limb('hand_near_sword','limb',[825,400,160,330],P.hand_near,[550,563],[[.45,.13],[.50,.86]],'hand_near');
limb('hand_near_fist','limb',[835,765,165,265],P.hand_near,[525,568],[[.45,.13],[.5,.85]],'hand_near');
limb('hand_far_fist','limb',[835,765,165,265],P.hand_far,[790,390],[[.45,.13],[.5,.85]],'hand_far');
rect('kneel','kneel',[0,0,1024,1536],[215,490,680,1020],'root');

async function exportPart(s){
 const file=path.join(OUT,'source',s.sheet==='body'?'body_sheet.png':s.sheet==='limb'?'limb_sheet.png':s.sheet+'.png');
 // Isolate the intended disconnected sprite, excluding adjacent cells which
 // intrude into the rectangular selection. Keep original pixels and edge alpha.
 const raw=await sharp(file).extract({left:s.crop[0],top:s.crop[1],width:s.crop[2],height:s.crop[3]}).ensureAlpha().raw().toBuffer();
 const cw=s.crop[2],ch=s.crop[3],labels=new Int32Array(cw*ch);let id=0,big=0,max=0;
 for(let p=0;p<labels.length;p++)if(!labels[p]&&raw[p*4+3]>16){id++;const q=[p];labels[p]=id;for(let j=0;j<q.length;j++){const v=q[j],x=v%cw,y=Math.floor(v/cw);for(const n of [x>0?v-1:-1,x<cw-1?v+1:-1,y>0?v-cw:-1,y<ch-1?v+cw:-1])if(n>=0&&!labels[n]&&raw[n*4+3]>16){labels[n]=id;q.push(n);}}if(q.length>max){max=q.length;big=id;}}
 const keep=new Uint8Array(cw*ch);for(let p=0;p<labels.length;p++)if(labels[p]===big){const x=p%cw,y=Math.floor(p/cw);for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++)if(x+dx>=0&&x+dx<cw&&y+dy>=0&&y+dy<ch)keep[(y+dy)*cw+x+dx]=1;}
 for(let p=0;p<keep.length;p++)if(!keep[p])raw[p*4+3]=0;
 let img=sharp(raw,{raw:{width:cw,height:ch,channels:4}});
 let left,top,buf;
 if(s.dest){[left,top]=s.dest;buf=await img.resize(s.dest[2],s.dest[3],{fit:'fill'}).png().toBuffer();}
 else {
   const [a,b]=s.pivots;const dx=(b[0]-a[0])*s.crop[2]*s.thickness,dy=(b[1]-a[1])*s.crop[3];
   const tx=s.to[0]-s.from[0],ty=s.to[1]-s.from[1];const scale=Math.hypot(tx,ty)/Math.hypot(dx,dy);
   const rw=Math.round(s.crop[2]*scale*s.thickness),rh=Math.round(s.crop[3]*scale);
   const theta=Math.atan2(ty,tx)-Math.atan2(dy,dx);const deg=theta*180/Math.PI;
   const r=await img.resize(rw,rh).rotate(deg,{background:transparent}).png().toBuffer({resolveWithObject:true});buf=r.data;
   const px=a[0]*rw-rw/2,py=a[1]*rh-rh/2;
   left=Math.round(s.from[0]-(r.info.width/2+px*Math.cos(theta)-py*Math.sin(theta)));
   top=Math.round(s.from[1]-(r.info.height/2+px*Math.sin(theta)+py*Math.cos(theta)));
 }
 const m=await sharp(buf).metadata();
 // Retain full RGBA and native antialiasing; only clip transparent off-canvas padding.
 const l=Math.max(0,-left),t=Math.max(0,-top);const width=Math.min(m.width-l,W-Math.max(0,left)),height=Math.min(m.height-t,H-Math.max(0,top));
 buf=await sharp(buf).extract({left:l,top:t,width,height}).png().toBuffer();left=Math.max(0,left);top=Math.max(0,top);
 const layer=await empty().composite([{input:buf,left,top}]).png().toBuffer();
 fs.writeFileSync(path.join(OUT,'layers',s.name+'.png'),layer);
 // Determine exact nontransparent bounding box for runtime upload, without altering pixels.
 const {data,info}=await sharp(layer).raw().toBuffer({resolveWithObject:true});
 let x0=W,y0=H,x1=0,y1=0,count=0;for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(data[(y*W+x)*4+3]>0){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);count++;}
 s.bounds=[x0,y0,x1-x0+1,y1-y0+1];s.alpha_pixels=count;
 s.buffer=await sharp(layer).extract({left:x0,top:y0,width:s.bounds[2],height:s.bounds[3]}).png().toBuffer();
 fs.mkdirSync(path.join(OUT,'images'),{recursive:true});
 fs.writeFileSync(path.join(OUT,'images',s.name+'.png'),s.buffer);
 return layer;
}
function influences(s,x,y){
 if(s.weights==='torso'){
   const joints=['hips','lower_abdomen','waist','spine_mid','torso','ribcage','chest'];
   for(let i=0;i<joints.length-1;i++){const a=P[joints[i]][1],b=P[joints[i+1]][1];if(y<=a&&y>=b){const f=(a-y)/(a-b);return [[joints[i],1-f],[joints[i+1],f]];}}
   return [[y>P.hips[1]?'hips':'chest',1]];
 }
 if(s.weights==='hair'){const f=Math.max(0,Math.min(1,(y-300)/300));return [['head',1-f],['hair_tip',f]];}
 if(s.weights==='skirt'){const f=Math.max(0,Math.min(1,(y-665)/180));return [['hips',1-f],[s.bone,f]];}
 return [[s.bone,1]];
}
function attachment(s){
 const [x0,y0,w,h]=s.bounds;
 if(!s.weights)return {type:'region',path:s.name,x:x0+w/2-P[s.bone][0],y:P[s.bone][1]-y0-h/2,width:w,height:h};
 const uvs=[],vertices=[],triangles=[];const nx=4,ny=s.weights==='torso'?9:7;
 for(let j=0;j<=ny;j++)for(let i=0;i<=nx;i++){
   const x=x0+w*i/nx,y=y0+h*j/ny;uvs.push(i/nx,j/ny);
   const inf=influences(s,x,y).filter(a=>a[1]>0.00001);vertices.push(inf.length);
   for(const [b,weight]of inf)vertices.push(B[b],+(x-P[b][0]).toFixed(4),+(P[b][1]-y).toFixed(4),+weight.toFixed(6));
 }
 for(let j=0;j<ny;j++)for(let i=0;i<nx;i++){const a=j*(nx+1)+i;triangles.push(a,a+1,a+nx+1,a+1,a+nx+2,a+nx+1);}
 return {type:'mesh',path:s.name,uvs,vertices,triangles,hull:0,width:w,height:h};
}
const R=(a)=>a.map(([time,angle],i)=>({time,angle,...(i<a.length-1?{curve:[.25,0,.75,1]}:{})}));
const T=(a)=>a.map(([time,x,y],i)=>({time,x,y,...(i<a.length-1?{curve:[.25,0,.75,1]}:{})}));
const animations={};
function anim(name,duration,poses,moves={},gesture='open'){
 const bt={};
 // Explicit reset tracks prevent stale action poses or attachments after interruption.
 for(const b of bones)bt[b.name]={rotate:R([[0,0],[duration,0]]),translate:T([[0,0,0],[duration,0,0]]),scale:[{time:0,x:1,y:1},{time:duration,x:1,y:1}]};
 for(const [b,keys]of Object.entries(poses))bt[b]={rotate:R(keys)};
 for(const [b,keys]of Object.entries(moves))bt[b].translate=T(keys);
 const st={};for(const s of specs.filter(s=>!s.name.startsWith('hand_')||s.name.endsWith('_open'))){st['slot_'+s.name]={attachment:[{time:0,name:s.name==='kneel'?null:s.name}]};}
 st.slot_hand_near_open={attachment:[{time:0,name:'hand_near_'+gesture}]};st.slot_hand_far_open={attachment:[{time:0,name:['attack','attack_rising','attack_leap','hurt','knockback_return'].includes(name)?'hand_far_fist':'hand_far_open'}]};
 animations[name]={bones:bt,slots:st};
}
anim('idle',2.4,{head:[[0,0],[1.2,-.7],[2.4,0]],hair_tip:[[0,-1],[1.2,1.5],[2.4,-1]],ribbon:[[0,-2],[1.2,2],[2.4,-2]],forearm_near:[[0,0],[1.2,1],[2.4,0]]},{hips:[[0,0,0],[1.2,0,3],[2.4,0,0]]});
anim('ready_cast',2.4,{forearm_near:[[0,20],[1.2,21],[2.4,20]],hand_near:[[0,8],[2.4,8]]},{},'sword');
anim('ready_sword',2.4,{arm_near:[[0,-8],[2.4,-8]],forearm_near:[[0,15],[2.4,15]]},{},'sword');
anim('cast',1.25,{arm_near:[[0,0],[.25,-16],[.65,32],[.9,25],[1.25,0]],forearm_near:[[0,0],[.25,40],[.65,25],[.9,20],[1.25,0]],hand_near:[[0,0],[.65,30],[1.25,0]],arm_far:[[0,0],[.65,18],[1.25,0]],spine_mid:[[0,0],[.25,3],[.65,-4],[1.25,0]],head:[[0,0],[.65,-5],[1.25,0]],ribbon:[[0,0],[.8,-10],[1.25,0]]},{},'sword');
anim('attack',.95,{waist:[[0,0],[.18,8],[.4,-14],[.6,-8],[.95,0]],arm_near:[[0,0],[.18,-35],[.4,65],[.6,35],[.95,0]],forearm_near:[[0,0],[.18,32],[.4,-25],[.6,-10],[.95,0]],hand_near:[[0,0],[.4,-15],[.95,0]],arm_far:[[0,0],[.4,-20],[.95,0]],robe_near:[[0,0],[.45,12],[.95,0]]},{root:[[0,0,0],[.18,-14,-6],[.4,70,0],[.6,65,0],[.95,0,0]]},'fist');
anim('attack_rising',1.05,{waist:[[0,0],[.2,6],[.5,-7],[1.05,0]],arm_near:[[0,0],[.2,-25],[.5,100],[.7,85],[1.05,0]],forearm_near:[[0,0],[.2,-10],[.5,30],[.7,25],[1.05,0]],arm_far:[[0,0],[.5,-25],[1.05,0]],thigh_near:[[0,0],[.2,8],[.5,-6],[1.05,0]],shin_near:[[0,0],[.2,-12],[.5,6],[1.05,0]],robe_center:[[0,0],[.5,-5],[1.05,0]]},{root:[[0,0,0],[.2,0,-26],[.5,24,30],[.7,24,10],[1.05,0,0]]},'fist');
anim('attack_leap',1.2,{waist:[[0,0],[.2,5],[.52,-12],[.75,-6],[1.2,0]],arm_near:[[0,0],[.2,65],[.52,135],[.75,55],[1.2,0]],forearm_near:[[0,0],[.2,35],[.52,10],[.75,-20],[1.2,0]],arm_far:[[0,0],[.52,50],[1.2,0]],thigh_near:[[0,0],[.2,8],[.52,35],[.8,-5],[1.2,0]],shin_near:[[0,0],[.52,-60],[.8,8],[1.2,0]],thigh_far:[[0,0],[.52,-25],[1.2,0]],shin_far:[[0,0],[.52,-25],[1.2,0]],robe_near:[[0,0],[.52,-12],[1.2,0]],robe_far:[[0,0],[.52,12],[1.2,0]]},{root:[[0,0,0],[.2,-8,-14],[.52,90,120],[.75,105,25],[.88,65,-10],[1.2,0,0]]},'fist');
anim('hurt',.6,{waist:[[0,0],[.09,8],[.2,5],[.6,0]],head:[[0,0],[.09,10],[.6,0]],arm_near:[[0,0],[.09,-15],[.6,0]],forearm_far:[[0,0],[.09,-12],[.6,0]]},{root:[[0,0,0],[.09,-38,0],[.2,-30,0],[.6,0,0]]});
anim('knockback_return',1.25,{hips:[[0,0],[.35,18],[.6,12],[.9,-3],[1.25,0]],head:[[0,0],[.35,12],[1.25,0]],arm_near:[[0,0],[.35,-35],[1.25,0]],arm_far:[[0,0],[.35,-20],[1.25,0]],thigh_near:[[0,0],[.35,18],[1.25,0]],shin_near:[[0,0],[.35,-35],[1.25,0]],thigh_far:[[0,0],[.35,-12],[1.25,0]],robe_near:[[0,0],[.35,-14],[1.25,0]]},{root:[[0,0,0],[.12,-35,20],[.35,-200,100],[.6,-225,45],[.85,-130,-12],[1.25,0,0]]});
anim('rest',1.2,{arm_near:[[0,0],[.5,-10],[1.2,0]],head:[[0,0],[.5,-4],[1.2,0]]});
anim('breath',1.2,{chest:[[0,0],[.5,2],[1.2,0]]},{hips:[[0,0,0],[.5,0,7],[1.2,0,0]]});
anim('death_kneel',1.5,{waist:[[0,0],[.4,-7],[.82,-15],[1.5,-15]],head:[[0,0],[.4,-8],[.82,-20],[1.5,-20]],arm_near:[[0,0],[.82,-25],[1.5,-25]],thigh_near:[[0,0],[.82,45],[1.5,45]],shin_near:[[0,0],[.82,-80],[1.5,-80]],thigh_far:[[0,0],[.82,-15],[1.5,-15]],shin_far:[[0,0],[.82,-65],[1.5,-65]]},{hips:[[0,0,0],[.4,0,-85],[.82,0,-230],[1.5,0,-230]]});
for(const [slot,track]of Object.entries(animations.death_kneel.slots))track.attachment.push({time:.82,name:slot==='slot_kneel'?'kneel':null});
// Solve authored collapse keys against fixed ankle targets. The initial leg
// chains are retained; only action keyframes change, with no added IK bones.
const collapseControls=[[0,0],[.2,30],[.4,115],[.6,280],[.82,425],[1.5,425]];
const collapse=[];
for(let frame=0;frame<=50;frame++){
 const time=Math.min(.82,frame/60);let i=0;while(i<collapseControls.length-2&&time>collapseControls[i+1][0])i++;
 const [ta,da]=collapseControls[i],[tb,db]=collapseControls[i+1],f=(time-ta)/(tb-ta),ease=f*f*(3-2*f);
 collapse.push([time,da+(db-da)*ease]);
}
collapse.push([1.5,425]);
animations.death_kneel.bones.hips.translate=collapse.map(([time,d])=>({time,x:0,y:-d}));
for(const side of ['near','far']){
 const h=P['thigh_'+side],k=P['shin_'+side],f=P['foot_'+side];
 const l1=Math.hypot(k[0]-h[0],k[1]-h[1]),l2=Math.hypot(f[0]-k[0],f[1]-k[1]);
 const a0=Math.atan2(k[1]-h[1],k[0]-h[0]),b0=Math.atan2(f[1]-k[1],f[0]-k[0]);
 // Choose the bend which smoothly continues each setup knee orientation.
 const setupTheta=Math.atan2(f[1]-h[1],f[0]-h[0]);
 const branch=a0<setupTheta?-1:1;
 const upper=[],lower=[],foot=[];
 for(const [time,drop]of collapse){
   const dx=f[0]-h[0],dy=f[1]-h[1]-drop,d=Math.hypot(dx,dy);
   const theta=Math.atan2(dy,dx),alpha=Math.acos(Math.max(-1,Math.min(1,(l1*l1+d*d-l2*l2)/(2*l1*d))));
   const a=theta+branch*alpha,ky=h[1]+drop+Math.sin(a)*l1,kx=h[0]+Math.cos(a)*l1;
   const b=Math.atan2(f[1]-ky,f[0]-kx),r1=(a0-a)*180/Math.PI,r2=(b0-b)*180/Math.PI-r1;
   upper.push([time,r1]);lower.push([time,r2]);foot.push([time,-r1-r2]);
 }
 animations.death_kneel.bones['thigh_'+side].rotate=upper.map(([time,angle])=>({time,angle}));
 animations.death_kneel.bones['shin_'+side].rotate=lower.map(([time,angle])=>({time,angle}));
 animations.death_kneel.bones['foot_'+side].rotate=foot.map(([time,angle])=>({time,angle}));
}
for(const b of ['robe_near','robe_far','robe_center'])animations.death_kneel.bones[b].translate=collapse.map(([time,d])=>({time,x:0,y:d}));
for(const [b,angle]of Object.entries({arm_near:45,forearm_near:-110,hand_near:-38,arm_far:-45,forearm_far:-105,hand_far:-9}))animations.death_kneel.bones[b].rotate=R([[0,0],[.4,angle*.35],[.82,angle],[1.5,angle]]);
for(const [b,factor]of [['arm_near',1.1],['arm_far',1.3],['forearm_far',1.15]])animations.death_kneel.bones[b].scale=[{time:0,x:1,y:1},{time:.4,x:1,y:1},{time:.82,x:factor,y:factor},{time:1.5,x:factor,y:factor}];
for(const side of ['near','far'])for(const segment of ['thigh','shin'])animations.death_kneel.slots['slot_'+segment+'_'+side].attachment.splice(1,0,{time:.45,name:null});

async function main(){
 const layers={};for(const s of specs)layers[s.name]=await exportPart(s);
 const order=specs.map(s=>s.name).filter(n=>!['kneel','hand_near_sword','hand_near_fist','hand_far_fist'].includes(n));
 await empty().composite(order.map(n=>({input:layers[n]}))).png().toFile(path.join(OUT,'source/master_1024x1536.png'));
 for(const [name,color]of [['setup_light','#eee6d8'],['setup_dark','#23302e']])await sharp({create:{width:W,height:H,channels:4,background:color}}).composite(order.map(n=>({input:layers[n]}))).png().toFile(path.join(OUT,'preview',name+'.png'));
 const slots=[],attachments={};
 for(const s of specs){let slot='slot_'+s.name;if(['hand_near_sword','hand_near_fist'].includes(s.name))slot='slot_hand_near_open';else if(s.name==='hand_far_fist')slot='slot_hand_far_open';else slots.push({name:slot,bone:s.bone,attachment:s.name==='kneel'?null:s.name});attachments[slot]??={};attachments[slot][s.name]=attachment(s);}
 // Single packed atlas, padded cells. Native alpha retained; no background removal or painting.
 let px=2,py=2,rowh=0;const atlasSize=4096,packed=[];let atlas=`male_sample.png\nsize: ${atlasSize},${atlasSize}\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\n`;
 for(const s of specs){const [,,w,h]=s.bounds;if(px+w+2>atlasSize){px=2;py+=rowh+4;rowh=0;}if(py+h+2>atlasSize)throw Error('Atlas overflow');packed.push({input:s.buffer,left:px,top:py});atlas+=`${s.name}\n  rotate: false\n  xy: ${px}, ${py}\n  size: ${w}, ${h}\n  orig: ${w}, ${h}\n  offset: 0, 0\n  index: -1\n`;px+=w+4;rowh=Math.max(rowh,h);}
 await sharp({create:{width:atlasSize,height:atlasSize,channels:4,background:transparent}}).composite(packed).png().toFile(path.join(OUT,'runtime/male_sample.png'));
 fs.writeFileSync(path.join(OUT,'runtime/male_sample.atlas'),atlas.replace('male_sample.png','male_sample_texture.res'));
 fs.writeFileSync(path.join(OUT,'runtime/male_sample_portable.atlas'),atlas);
 const rig={skeleton:{spine:'3.8.99',hash:'male_sample_20261015_v1',x:-512,y:0,width:1024,height:1536,images:'../images/'},bones,slots,skins:[{name:'default',attachments}],animations};
 write('runtime/male_sample.json',rig);write('runtime/male_sample.spinejson',rig);
 write('alignment.json',{canvas:[W,H],origin:[512,1450],anchors_px:P,order,parts:specs.map(({buffer,...s})=>s)});
 const legacyNames=old.bones.map(b=>b.name);const missing=legacyNames.filter(n=>B[n]===undefined);if(missing.length)throw Error('Lost legacy bones');
 for(const [name,a]of Object.entries(animations))for(const bone of Object.keys(a.bones))if(B[bone]===undefined)throw Error(name+': '+bone);
 write('qa_data.json',{canvas:[W,H],legacy_bones_preserved:legacyNames.length,bones:bones.length,slots:slots.length,attachments:specs.length,weighted_meshes:specs.filter(s=>s.weights).length,animations:Object.fromEntries(Object.entries(animations).map(([n,a])=>[n,Math.max(...Object.values(a.bones).flatMap(t=>Object.values(t).flatMap(k=>k.map(v=>v.time||0))))])),alpha:'Original generated RGBA preserved. All same-origin layers have alpha=0 corners.',checks:{legacy_names:missing.length===0,all_tracks_known:true,production_assets_written:false}});
 console.log('Exported',bones.length,'bones,',slots.length,'slots,',specs.length,'attachments,',Object.keys(animations).length,'animations');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
