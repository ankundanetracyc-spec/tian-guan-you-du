const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),W=1024,H=1536,sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),smooth=v=>{v=clamp(v);return v*v*(3-2*v)};
async function read(p){let{data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});if(info.width!==W||info.height!==H)throw Error('Wrong canvas: '+p);return data}
async function save(d,p){fs.mkdirSync(path.dirname(p),{recursive:true});await sharp(d,{raw:{width:W,height:H,channels:4}}).png().toFile(p)}
function sample(d,x,y){if(x<0||y<0||x>=W-1||y>=H-1)return[0,0,0,0];let x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0,o=[0,0,0,0];for(const[dx,dy,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){let k=((y0+dy)*W+x0+dx)*4,a=d[k+3]/255;o[3]+=a*w;for(let c=0;c<3;c++)o[c]+=d[k+c]*a*w;}if(o[3])for(let c=0;c<3;c++)o[c]/=o[3];o[3]*=255;return o}
function nearest(d,threshold=200,maxDistance=0){let near=new Int32Array(W*H);near.fill(-1);let dist=new Uint16Array(W*H);dist.fill(65535);let q=new Int32Array(W*H),s=0,e=0;for(let p=0;p<W*H;p++)if(d[p*4+3]>=threshold){near[p]=p;dist[p]=0;q[e++]=p;}while(s<e){let p=q[s++];if(maxDistance&&dist[p]>=maxDistance)continue;let x=p%W;for(const n of[x>0?p-1:-1,x<W-1?p+1:-1,p>=W?p-W:-1,p<(H-1)*W?p+W:-1])if(n>=0&&near[n]<0){near[n]=near[p];dist[n]=dist[p]+1;q[e++]=n;}}return{near,dist}}
function clean(d){let{dist}=nearest(d,128,8);for(let p=0;p<W*H;p++){let k=p*4+3;if(d[k]<=3||dist[p]>8)d[k]=0;else if(dist[p]>4)d[k]=Math.round(d[k]*smooth((9-dist[p])/5));}return d}
const quantile=(v,q)=>v.sort((a,b)=>a-b)[Math.floor((v.length-1)*q)];
(async()=>{
let native=await read(path.join(R,'source/head_blank_native.png')),old=await read(path.join(R,'before/male/face_shape/01.png')),head=Buffer.from(native),{near}=nearest(native,230);
for(let p=0;p<W*H;p++){let k=p*4;head[k+3]=old[k+3];if(old[k+3]&&native[k+3]<230){let source=near[p]*4;for(let c=0;c<3;c++)head[k+c]=native[source+c];}}
await save(head,path.join(R,'male/face_shape/01.png'));
let raw=clean(await read(path.join(R,'source/hair_front_native.png'))),front=Buffer.alloc(W*H*4),s=.72,tx=519-s*516,ty=264-s*299;
for(let y=0;y<H;y++)for(let x=0;x<W;x++){let c=sample(raw,(x-tx)/s,(y-ty)/s),k=(y*W+x)*4;for(let j=0;j<4;j++)front[k+j]=Math.round(c[j]);}
front=clean(front);
let master=await read(path.join(R,'../male_whole_01.png')),sourceLum=[],targetLum=[];
for(let y=145;y<330;y++)for(let x=325;x<715;x++){let k=(y*W+x)*4,lum=.2126*master[k]+.7152*master[k+1]+.0722*master[k+2];if(lum<140&&Math.max(master[k],master[k+1],master[k+2])-Math.min(master[k],master[k+1],master[k+2])<32)targetLum.push(lum);if(front[k+3]>230)sourceLum.push(.2126*front[k]+.7152*front[k+1]+.0722*front[k+2]);}
let a=quantile(sourceLum,.5),b=quantile(sourceLum,.95),ta=quantile(targetLum,.5),tb=quantile(targetLum,.95);
let frontApprovedRgb=0;
for(let k=0;k<front.length;k+=4){if(!front[k+3])continue;let v=.2126*front[k]+.7152*front[k+1]+.0722*front[k+2],target=v<a?v*ta/a:ta+(v-a)*(tb-ta)/(b-a);for(let c=0;c<3;c++)front[k+c]=Math.round(clamp(front[k+c]+target-v,0,255));let original=.2126*master[k]+.7152*master[k+1]+.0722*master[k+2],chroma=Math.max(master[k],master[k+1],master[k+2])-Math.min(master[k],master[k+1],master[k+2]);if(front[k+3]>230&&original<140&&chroma<32){for(let c=0;c<3;c++)front[k+c]=master[k+c];frontApprovedRgb++;}}
await save(front,path.join(R,'male/hair_front/01.png'));
let back=clean(await read(path.join(R,'source/hair_back_native.png'))),cap=[];
for(let y=180;y<530;y++){let xs=[],actual=[];for(let x=150;x<875;x++){if(front[(y*W+x)*4+3]>128)xs.push(x);if(back[(y*W+x)*4+3]>128)actual.push(x);}if(xs.length&&actual.length)cap.push({y,front:[xs[0],xs.at(-1)],rear:[actual[0],actual.at(-1)]});}
await save(back,path.join(R,'male/hair_back/01.png'));
let alphaChanged=0;for(let k=3;k<head.length;k+=4)if(head[k]!==old[k])alphaChanged++;if(alphaChanged)throw Error('Head/neck geometry changed');
fs.writeFileSync(path.join(R,'source/root_parts_registration.json'),JSON.stringify({canvas:[W,H],head:{source:'head_blank_native.png',old_alpha_sha256:crypto.createHash('sha256').update(Buffer.from(old.filter((_,i)=>i%4===3))).digest('hex'),head_neck_alpha_changed_pixels:alphaChanged,mode:'New approved-mother skin painting; exact current alpha retained, edge colors extended from nearby solid skin'},front:{s,tx,ty,source_landmark:[516,299],target_landmark:[519,264],luminance_before:[a,b],luminance_master_target:[ta,tb],approved_visible_neutral_hair_rgb_pixels:frontApprovedRgb},back:{mode:'Native continuous bun/pin/long hair, no silhouette clipping; measured rear cap narrower than front at y250-350',cap_rows:cap}},null,2));console.log('Head/neck Alpha frozen exactly; front registered to master hairline; native rear cap measured');
})().catch(e=>{console.error(e);process.exitCode=1});
