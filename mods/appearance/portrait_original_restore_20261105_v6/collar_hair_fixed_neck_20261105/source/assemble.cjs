const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),W=1024,H=1536;
const clamp=(x,a=0,b=1)=>Math.min(b,Math.max(a,x)),smooth=x=>{x=clamp(x);return x*x*(3-2*x)};
async function read(p){const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});if(info.width!==W||info.height!==H)throw Error('Wrong canvas '+p);return data}
async function write(data,p){fs.mkdirSync(path.dirname(p),{recursive:true});await sharp(data,{raw:{width:W,height:H,channels:4}}).png().toFile(p)}
function sample(data,x,y){if(x<0||y<0||x>=W-1||y>=H-1)return[0,0,0,0];let x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0,out=[0,0,0,0];for(const [dx,dy,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){let k=((y0+dy)*W+x0+dx)*4,a=data[k+3]/255;out[3]+=a*w;for(let c=0;c<3;c++)out[c]+=data[k+c]*a*w;}if(out[3]>0)for(let c=0;c<3;c++)out[c]/=out[3];out[3]*=255;return out}
function bbox(data){let box=[W,H,0,0];for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(data[(y*W+x)*4+3]>230){box[0]=Math.min(box[0],x);box[1]=Math.min(box[1],y);box[2]=Math.max(box[2],x+1);box[3]=Math.max(box[3],y+1)}return box}
(async()=>{
const frozen=JSON.parse(fs.readFileSync(path.join(R,'source/frozen_head_neck.json'))),measures=JSON.parse(fs.readFileSync(path.join(R,'source/collar_measured.json'))),records=[];
if(sha(path.join(R,'before/male/face_shape/01.png'))!==frozen.sha256)throw Error('Head changed');
for(const m of measures){let n=String(m.index).padStart(2,'0'),old=await read(path.join(R,'before/male/clothing',n+'.png')),raw=await read(path.join(R,'source','clothing'+n+'_raw.png')),out=Buffer.from(raw);
 let sx=183/(m.front_tips[1][0]-m.front_tips[0][0]),tx=424-sx*m.front_tips[0][0],midY=(m.front_tips[0][1]+m.front_tips[1][1])/2,sy=(m.v_points.before[1]-530)/(m.v_points.raw[1]-midY),shear=(m.v_points.before[0]-(sx*m.v_points.raw[0]+tx))/(m.v_points.raw[1]-midY);
 // One continuous generated cloth painting, locally registered to the frozen neck.
 // A smooth displacement field avoids blending two different lapel lines together.
 for(let y=480;y<930;y++)for(let x=300;x<730;x++){
 let weight=smooth((x-300)/105)*smooth((730-x)/105)*smooth((930-y)/190),k=(y*W+x)*4;
 if(weight<=0)continue;let sourceY=(y-530)/sy+midY,sourceX=(x-tx-shear*(sourceY-midY))/sx;
 let s=sample(raw,x+weight*(sourceX-x),y+weight*(sourceY-y));for(let c=0;c<4;c++)out[k+c]=Math.round(s[c]);
 }
 for(let y=0;y<H;y++)for(let x=0;x<W;x++){let k=(y*W+x)*4;if(y<495||out[k+3]<=3)out[k+3]=0;}
 await write(out,path.join(R,'male/clothing',n+'.png'));
 let rb=await read(path.join(R,'source','collarback'+n+'_raw.png')),b=m.rear_core_bbox,target=[382,508,650,630],back=Buffer.alloc(W*H*4),sx2=(target[2]-target[0])/(b[2]-b[0]),sy2=(target[3]-target[1])/(b[3]-b[1]);
 for(let y=503;y<636;y++)for(let x=377;x<656;x++){let s=sample(rb,(x-target[0])/sx2+b[0],(y-target[1])/sy2+b[1]),k=(y*W+x)*4;for(let c=0;c<4;c++)back[k+c]=Math.round(s[c]);if(back[k+3]<=3)back[k+3]=0;}
 await write(back,path.join(R,'male/clothing_back',n+'.png'));
 records.push({index:m.index,front_source_tips:m.front_tips,front_target_tips:[[424,530],[607,530]],v_points:m.v_points,front_transform:{sx,tx,sy,shear,midY},registration_box:[300,480,730,930],rear_source_bbox:b,rear_target_bbox:target,source_mode:'Continuous native generated garment with local collar displacement; no old/new lapel crossfade',generated_body_below_930_unchanged:true});
}
for(const part of ['hair_front','hair_back'])for(let i=1;i<=3;i++){
 let n=String(i).padStart(2,'0'),name=part==='hair_front'?'front'+n+'moderate_raw.png':'back'+n+'_raw.png',data=await read(path.join(R,'source',name));
 // Keep fine edge antialiasing near real strands; discard distant semitransparent background patches.
 let distance=new Uint8Array(W*H);distance.fill(255);let queue=new Int32Array(W*H),start=0,end=0;
 for(let p=0;p<W*H;p++)if(data[p*4+3]>=128){distance[p]=0;queue[end++]=p;}
 while(start<end){let p=queue[start++],d=distance[p];if(d>=8)continue;let x=p%W;for(const q of [x>0?p-1:-1,x<W-1?p+1:-1,p>=W?p-W:-1,p<(H-1)*W?p+W:-1])if(q>=0&&distance[q]>d+1){distance[q]=d+1;queue[end++]=q;}}
 for(let p=0;p<W*H;p++){let k=p*4+3;if(data[k]<=3||distance[p]>8)data[k]=0;else if(distance[p]>4)data[k]=Math.round(data[k]*smooth((9-distance[p])/5));}
 await write(data,path.join(R,'male',part,n+'.png'));
}
fs.writeFileSync(path.join(R,'source/registration.json'),JSON.stringify({head_neck_sha256:frozen.sha256,head_changed:false,canvas:[W,H],zero_runtime_offset:true,records},null,2));
console.log('Continuous generated garments registered to fixed neck; six moderate-volume hair layers selected and distant background noise cleared');
})().catch(e=>{console.error(e);process.exitCode=1});
