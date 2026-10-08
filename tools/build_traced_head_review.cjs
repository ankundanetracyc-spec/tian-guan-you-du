// Single male/female modular review, registered to the user's traced skull.
// Reuse the female independent skin source; the male skin is newly painted.
const fs=require('fs');
const path=require('path');
const sharp=require('sharp');
const {PNG}=require('pngjs');
const root=path.resolve(__dirname,'..'),W=1024,H=1536,CX=512;
const refs=path.join(root,'mods/appearance/head_contour_reference_20261001');
const packs={male:'腰上男修拆件演示_v2',female:'腰上女修拆件演示_v1'};
const read=p=>PNG.sync.read(fs.readFileSync(p));
const blank=()=>new PNG({width:W,height:H});
const clamp=v=>Math.max(0,Math.min(255,Math.round(v)));
const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)};
function sample(im,x,y){
 if(x<0||x>=im.width-1||y<0||y>=im.height-1)return [0,0,0,0];
 const ix=Math.floor(x),iy=Math.floor(y),tx=x-ix,ty=y-iy,v=[0,0,0,0];
 for(let dy=0;dy<=1;dy++)for(let dx=0;dx<=1;dx++){
  const k=((iy+dy)*im.width+ix+dx)*4,w=(dx?tx:1-tx)*(dy?ty:1-ty),a=im.data[k+3]/255;
  for(let c=0;c<3;c++)v[c]+=im.data[k+c]*w*a;v[3]+=w*a;
 }
 if(v[3])for(let c=0;c<3;c++)v[c]/=v[3];v[3]*=255;return v;
}
function mirrorSample(im,cx,d,y){
 const l=sample(im,cx-d,y),r=sample(im,cx+d,y),a=(l[3]+r[3])/2;
 return [0,1,2].map(c=>a?(l[c]*l[3]+r[c]*r[3])/(2*a):0).concat(a);
}
function writePixel(im,x,y,v,alpha=v[3]){
 const k=(y*W+x)*4;for(let c=0;c<3;c++)im.data[k+c]=clamp(v[c]);im.data[k+3]=clamp(alpha);
}
function rowRadius(im,y,cx=512){
 let l=W,r=-1;for(let x=330;x<=695;x++)if(im.data[(Math.round(y)*W+x)*4+3]>180){l=Math.min(l,x);r=Math.max(r,x)}
 return r<l?0:(r-l)/2;
}
function interpolate(points,y){
 if(y<=points[0][0])return points[0][1];
 for(let i=1;i<points.length;i++)if(y<=points[i][0]){
  const [a,b]=points[i-1],[c,d]=points[i],t=(y-a)/(c-a);return b+(d-b)*t;
 }return points.at(-1)[1];
}
const traced=read(path.join(refs,'female_traced_reference.png'));
const radius=[];
for(let y=198;y<=548;y++){
 const runs=[];let run=[];
 for(let x=300;x<=750;x++){
  const k=(y*W+x)*4,red=traced.data[k]>200&&traced.data[k+1]<80&&traced.data[k+2]<80;
  if(red)run.push(x);else if(run.length){runs.push(run);run=[]}
 }if(run.length)runs.push(run);
 if(runs.length>=2){const l=runs[0],r=runs.at(-1);radius[y]=((r[0]+r.at(-1))-(l[0]+l.at(-1)))/4;}
 else radius[y]=runs.length?(runs[0].at(-1)-runs[0][0])/2:0;
}
function tracedRadius(y){
 if(y<198||y>548)return 0;
 const d=(y-344)/146,ellipse=125*Math.sqrt(Math.max(0,1-d*d));
 let sum=0,n=0;for(let yy=Math.max(198,Math.round(y)-3);yy<=Math.min(548,Math.round(y)+3);yy++){sum+=radius[yy]||0;n++}
 let raw=sum/n;
 const chinDepth=Math.max(0,548-y),chinArc=Math.sqrt(Math.max(0,60*chinDepth-chinDepth*chinDepth));
 const chinBlend=smooth((y-537)/6);raw=raw*(1-chinBlend)+chinArc*chinBlend;
 const blend=smooth((y-210)/30);return ellipse*(1-blend)+raw*blend;
}
function colorMatch(layer,reference){
 const diff=[[],[],[]];
 for(const [x0,x1,y0,y1] of [[475,549,310,332],[467,480,410,429],[544,557,410,429]])
  for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++){
   const k=(y*W+x)*4;if(layer.data[k+3]<240||reference.data[k+3]<240)continue;
   for(let c=0;c<3;c++)diff[c].push(reference.data[k+c]-layer.data[k+c]);
  }
 const offset=diff.map(a=>{a.sort((a,b)=>a-b);return a[Math.floor(a.length/2)]||0});
 for(let k=0;k<layer.data.length;k+=4)if(layer.data[k+3])for(let c=0;c<3;c++)layer.data[k+c]=clamp(layer.data[k+c]+offset[c]);
 return offset;
}
function femaleFace(source){
 const result=blank();
 // Keep the complete independent neck underneath the new head contour.
 for(let y=480;y<H;y++)for(let x=350;x<=674;x++){
  const sy=y+4+14*smooth((y-490)/20)*(1-smooth((y-540)/60));
  const v=mirrorSample(source,511.5,Math.abs(x-CX),sy);if(v[3]>3)writePixel(result,x,y,v);
 }
 const jaw=[[480,73.5],[490,67.5],[500,61],[510,53],[520,44],[530,34],[540,24],[545,18],[550,10],[552,1]];
 for(let y=198;y<=548;y++){
  const sy=y<380?255+(y-198)*(380-255)/(380-198):380+(y-380)*(552-380)/(548-380);
  const sr=sy<480?rowRadius(source,sy):interpolate(jaw,sy),tr=tracedRadius(y);
  for(let x=Math.floor(CX-tr-1);x<=Math.ceil(CX+tr+1);x++){
   const coverage=Math.max(0,Math.min(1,tr-Math.abs(x-CX)+.5));if(!coverage)continue;
   const v=mirrorSample(source,511.5,Math.abs(x-CX)*sr/Math.max(1,tr),sy);
   const k=(y*W+x)*4,headA=coverage,neckA=result.data[k+3]/255,combined=headA+neckA*(1-headA);
   for(let c=0;c<3;c++)result.data[k+c]=clamp((v[c]*headA+result.data[k+c]*neckA*(1-headA))/combined);
   result.data[k+3]=clamp(combined*255);
  }
 }
 return result;
}
function maleFace(source){
 const result=blank(),top=198,chin=547,sourceTop=257,sourceChin=693,scaleX=.855;
 for(let y=top;y<=727;y++)for(let x=350;x<=674;x++){
  const sy=y<=chin?sourceTop+(y-top)*(sourceChin-sourceTop)/(chin-top):sourceChin+(y-chin)*(914-sourceChin)/(727-chin);
  const v=mirrorSample(source,512.5,Math.abs(x-CX)/scaleX,sy);
  if(v[3]<3)continue;writePixel(result,x,y,v,v[3]>245?255:v[3]);
 }
 return result;
}
function symmetricPart(source,sourceCx=512,earShift=0,eyeSex=null){
 const result=blank();
 for(let y=0;y<H;y++)for(let x=330;x<=694;x++){
  const d=Math.abs(x-CX)-earShift;if(d<0)continue;
  // Mirror one complete painted side. Averaging unequal eyelids/nasal edges
  // produces double contours, even when their alpha centers agree.
  const sx=eyeSex?sourceCx+d:sourceCx-d,v=sample(source,sx,y);
  if(eyeSex){
   // Keep the unoccluded right eye and its actual tilted lid envelope. The
   // full-portrait eye patches also included a vertical hair strand beyond
   // the outer canthus; that strand belongs exclusively to frontal hair.
   const ex=eyeSex==='male'?567:562,ey=eyeSex==='male'?381:387;
   const theta=eyeSex==='male'?-.1745:-.09,dx=sx-ex,dy=y-ey;
   const u=Math.cos(theta)*dx+Math.sin(theta)*dy,vv=-Math.sin(theta)*dx+Math.cos(theta)*dy;
   const ry=eyeSex==='male'?17:21,edge=Math.sqrt((u/43)**2+(vv/ry)**2);
   v[3]*=smooth((1.03-edge)/.08);
  }
  if(v[3]>3)writePixel(result,x,y,v);
 }return result;
}
async function main(){
 for(const [sex,name] of Object.entries(packs)){
  const pack=path.join(root,'mods/appearance',name),active=path.join(pack,'test_assets_green_reference_v1');
  const out=path.join(pack,'test_sources/traced_head_v2'),preview=path.join(pack,'preview/traced_head_v2');
  fs.mkdirSync(preview,{recursive:true});fs.mkdirSync(path.join(out,'before'),{recursive:true});
  const parts=['face','ear','eye','brow','nose','mouth','hair_front','hair_back'];
  for(const part of parts){const b=path.join(out,'before',`${part}_0.png`);if(!fs.existsSync(b))fs.copyFileSync(path.join(active,`${part}_0.png`),b)}
  const reference=read(path.join(sex==='male'?refs:path.join(pack,'test_sources/green_master_single_set'),sex==='male'?'male_portrait_reference.png':'female_master.png'));
  const skin=sex==='male'?maleFace(read(path.join(out,'generated_head.png'))):femaleFace(read(path.join(pack,'source/anatomy_v11/before/face_0.png')));
  const offset=colorMatch(skin,reference);
  fs.writeFileSync(path.join(out,'face_0.png'),PNG.sync.write(skin));
  for(const part of ['ear','eye','brow','nose','mouth']){
   const source=read(path.join(out,'before',`${part}_0.png`));
   const center=part==='nose'?(sex==='male'?513:512):part==='mouth'?(sex==='male'?514:513):512;
   const layer=symmetricPart(source,center,part==='ear'?14:0,part==='eye'?sex:null);
   fs.writeFileSync(path.join(out,`${part}_0.png`),PNG.sync.write(layer));
  }
  // Female frontal hair reuses a complete independent crown, never the old
  // horizontal extraction branch. Shorten cheek-origin locks by warping the
  // painted tails rather than truncating them at a row.
  if(sex==='female'){
   const clean=read(path.join(pack,'test_sources/hair_integrity_v1/hair_front_0.png')),front=blank();
   for(let y=0;y<H;y++)for(let x=0;x<W;x++){
    const sy=y<=500?y:500+(y-500)*(1500-500)/(1260-500);
    const v=sample(clean,x,sy);if(v[3]>3)writePixel(front,x,y,v);
   }
   fs.writeFileSync(path.join(out,'hair_front_0.png'),PNG.sync.write(front));
   const completeRear=read(path.join(pack,'test_sources/hair_integrity_v1/hair_back_0.png')),rear=blank();
   for(let y=0;y<H;y++)for(let x=0;x<W;x++){
    const v=sample(completeRear,CX+(x-CX)/.84,y);if(v[3]>3)writePixel(rear,x,y,v);
   }
   fs.writeFileSync(path.join(out,'hair_back_0.png'),PNG.sync.write(rear));
  }
  const order=['hair_back','face','ear','clothes','eye','brow','nose','mouth',sex==='male'?'beard':'makeup','hair_front'];
  const file=part=>fs.existsSync(path.join(out,`${part}_0.png`))?path.join(out,`${part}_0.png`):path.join(active,`${part}_0.png`);
  for(const [bg,label] of [['#cbc8bd','light'],['#202526','dark']]){
   for(const [mode,visible] of [['full',order],['bald',order.filter(x=>!x.startsWith('hair_'))],['skin',['face']]])
    await sharp({create:{width:W,height:H,channels:4,background:bg}}).composite(visible.map(part=>({input:file(part)}))).png().toFile(path.join(preview,`${mode}_${label}.png`));
  }
  // Show the exact female red guide over the repaired skin for comparison.
  if(sex==='female'){
   const line=blank();for(let k=0;k<line.data.length;k+=4)if(traced.data[k]>200&&traced.data[k+1]<80&&traced.data[k+2]<80){line.data[k]=255;line.data[k+3]=255}
   await sharp(path.join(preview,'skin_light.png')).composite([{input:PNG.sync.write(line),left:-5,top:0}]).png().toFile(path.join(preview,'skin_vs_trace.png'));
  }
  fs.writeFileSync(path.join(out,'placement.json'),JSON.stringify({sex,canvas:[W,H],center_axis_x:CX,skull_top_y:198,chin_y:547,face_source:sex==='female'?'source/anatomy_v11/before/face_0.png':'generated_head.png',skin_color_offset:offset,ear_shift_outward_px:14,status:'one_set_visual_review'},null,2)+'\n');
  console.log(JSON.stringify({sex,preview,offset}));
 }
}
main().catch(e=>{console.error(e);process.exitCode=1});
