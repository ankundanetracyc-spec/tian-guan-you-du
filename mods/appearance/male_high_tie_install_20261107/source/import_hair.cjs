// Technical matte/import of image_gen hair. No local geometry warps or painted shapes.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),W=1024,H=1536;
const sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const clamp=(n,lo=0,hi=1)=>Math.max(lo,Math.min(hi,n));
async function read(f){let{data,info}=await sharp(f).ensureAlpha().raw().toBuffer({resolveWithObject:true});if(info.width!==W||info.height!==H)throw Error('Full1024x1536 source required '+f);return data;}
function median(a){a.sort((x,y)=>x-y);return a[Math.floor(a.length/2)];}
function sample(d,x,y){if(x<0||y<0||x>=W-1||y>=H-1)return[0,0,0,0];let x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0,o=[0,0,0,0];for(const[dx,dy,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){let k=((y0+dy)*W+x0+dx)*4,a=d[k+3]/255;o[3]+=a*w;for(let c=0;c<3;c++)o[c]+=d[k+c]*a*w;}if(o[3])for(let c=0;c<3;c++)o[c]/=o[3];o[3]*=255;return o;}
(async()=>{
 const sourcePath=path.join(R,'source/approved_male.png'),source=await read(sourcePath);
 const fixedHead=await read(path.join(R,'reference/current_male/face_shape/01.png'));
 if(sha(sourcePath)!=='fe7b9280cbb19019e76ffd6d451679e5e3ff3dc2b685b9020471b96451312f18')throw Error('Wrong approved source');
 const geom=JSON.parse(fs.readFileSync(path.join(R,'qa/geometry_and_dedup.json'))),frame=geom.joint_source_to_frame;
 if(frame.maximum_residual_px>2)throw Error('Common face-frame registration rejected');
 const [[a,b,tx],[c,d,ty]]=frame.matrix_2x3,det=a*d-b*c;
 const samples=[[],[],[]];for(let y=280;y<310;y++)for(let x=490;x<535;x++){let k=(y*W+x)*4;for(let j=0;j<3;j++)samples[j].push(source[k+j]);}
 const bg=samples.map(median),records=[];
 for(const part of['hair_front','hair_back']){
  const rawPath=path.join(R,'raw',part+'.png'),native=await read(rawPath),matte=Buffer.from(native);
  let noiseRemoved=0,skinSpillSuppressed=0,approvedRgb=0,headProtected=0;
  for(let p=0;p<W*H;p++){
   let k=p*4,x=p%W,y=Math.floor(p/W),alpha=matte[k+3];
   if(alpha<=2){if(alpha)noiseRemoved++;matte[k+3]=0;continue;}
   const sr=source[k],sg=source[k+1],sb=source[k+2],lum=.2126*sr+.7152*sg+.0722*sb,chroma=Math.max(sr,sg,sb)-Math.min(sr,sg,sb);
   const fx=Math.round(a*x+b*y+tx),fy=Math.round(c*x+d*y+ty);
   const behindHead=part==='hair_back'&&fx>=0&&fy>=0&&fx<W&&fy<H&&fixedHead[(fy*W+fx)*4+3]>16;
   if(behindHead)headProtected++;
   if(!behindHead&&alpha>=160 && lum<170 && chroma<35 && sr>=sg-2 && sg>=sb-2){for(let j=0;j<3;j++)matte[k+j]=source[k+j];approvedRgb++;}
   // Continuous source matting across the entire forehead transition avoids a
   // threshold-cut opaque gray edge. Recover the approved foreground against
   // its source skin, never add Alpha outside native generated support.
   if(part==='hair_front'&&y>=220&&y<320&&x>=400&&x<640){
    let numerator=0,denominator=0;for(let j=0;j<3;j++){const v=bg[j]-native[k+j];numerator+=(bg[j]-source[k+j])*v;denominator+=v*v;}
    if(denominator>1){
     const sourceAlpha=clamp(numerator/denominator);
     let limited=sourceAlpha<.015?0:Math.min(alpha,Math.round(sourceAlpha*255));
     if(limited<alpha)skinSpillSuppressed++;
     matte[k+3]=limited;
     if(limited&&sourceAlpha>.001)for(let j=0;j<3;j++)matte[k+j]=Math.round(clamp((source[k+j]-(1-sourceAlpha)*bg[j])/sourceAlpha,0,255));
    }
   }
  }
  const output=Buffer.alloc(W*H*4);
  for(let y=0;y<H;y++)for(let x=0;x<W;x++){
   const px=x-tx,py=y-ty,sx=(d*px-b*py)/det,sy=(-c*px+a*py)/det,v=sample(matte,sx,sy),k=(y*W+x)*4;
   for(let j=0;j<4;j++)output[k+j]=Math.round(clamp(v[j],0,255));
   if(!output[k+3])output[k]=output[k+1]=output[k+2]=0;
  }
  const dest=path.join(R,'male',part,'05.png');fs.mkdirSync(path.dirname(dest),{recursive:true});
  await sharp(output,{raw:{width:W,height:H,channels:4}}).png().toFile(dest);
  records.push({part,raw:path.relative(R,rawPath),raw_sha256:sha(rawPath),output:path.relative(R,dest),sha256:sha(dest),canvas:[W,H],runtime_offset:[0,0],shared_source_to_frame:frame.matrix_2x3,frame_maximum_residual_px:frame.maximum_residual_px,local_geometry_warps:false,approved_visible_hair_core_rgb_pixels:approvedRgb,posterior_head_pixels_kept_native_to_exclude_face_features:headProtected,alpha1to2_noise_pixels_removed:noiseRemoved,source_skin_spill_matte_pixels:skinSpillSuppressed,forehead_matte_background_rgb:bg,hidden_hair:'native image_gen continuous painting',limit:'Source shape and transparent edge fidelity require actual composition review; no whole-pixel identity claim'});
 }
 fs.writeFileSync(path.join(R,'source/hair_import.json'),JSON.stringify({mode:'built-in image_gen hair extraction with approved-source RGB/matte conservation and one common whole-group frame import',approved_sha256:sha(sourcePath),records},null,2)+'\n');
 console.log(JSON.stringify(records.map(x=>({part:x.part,approved_core_pixels:x.approved_visible_hair_core_rgb_pixels,skin_spill_pixels:x.source_skin_spill_matte_pixels,frame_max_residual:x.frame_maximum_residual_px}))));
})().catch(e=>{console.error(e);process.exitCode=1});
