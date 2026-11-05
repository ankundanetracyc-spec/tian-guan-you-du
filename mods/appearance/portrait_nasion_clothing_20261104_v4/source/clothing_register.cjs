// Keep only newly painted cloth from a narrowly selected collar-side ROI.
// No local warp, pixel clone, synthetic texture or movement of the active face.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const id=String(Number(process.argv[2]||1)).padStart(2,'0');
const oldFile=path.join(project,`mods/appearance/ink_three_variants_20261004/male/clothing/${id}.png`);
const faceFile=path.join(project,'mods/appearance/ink_three_variants_20261004/male/face_shape/01.png');
const rawFile=path.join(__dirname,`clothing${id}_selected_raw.png`);
const outFile=path.join(pack,`male/clothing/${id}.png`);
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)};
function bounds(data,y,x0,x1,threshold=40){let a=-1,b=-1;for(let x=x0;x<=x1;x++)if(data[(y*1024+x)*4+3]>=threshold){if(a<0)a=x;b=x;}return a<0?null:[a,b]}
(async()=>{
 const old=await sharp(oldFile).ensureAlpha().raw().toBuffer(),face=await sharp(faceFile).ensureAlpha().raw().toBuffer();
 const meta=await sharp(rawFile).metadata();
 const painted=await sharp(rawFile).resize({width:250}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 if(painted.info.height!==130)throw Error('The generated framing cannot be uniformly registered into the original crop');
 const output=Buffer.from(old),changes=[];
 for(let y=520;y<620;y++){
  const skin=bounds(face,y,395,645),referenceY=Math.max(y,530);
  const l=bounds(old,referenceY,395,516),r=bounds(old,referenceY,518,645);
  if(!skin||!l||!r)continue;
  const leftLimit=y<=570?Math.max(l[1],skin[0]+2):l[1]+1;
  const rightLimit=y<=570?Math.min(r[0],skin[1]-2):r[0]-1;
  const vertical=smooth((y-520)/7)*smooth((620-y)/18);
  for(let x=395;x<645;x++){
   const leftSide=smooth((x-(l[1]-18))/10)*smooth((leftLimit+1-x)/2);
   const rightSide=smooth(((r[0]+18)-x)/10)*smooth((x-(rightLimit-1))/2);
   const weight=Math.max(leftSide,rightSide)*vertical;
   if(!weight)continue;
   const k=(y*1024+x)*4,s=((y-500)*250+(x-395))*4;
   const a0=old[k+3]/255,a1=painted.data[s+3]/255;
   const a=a0*(1-weight)+a1*weight;
   for(let c=0;c<3;c++)output[k+c]=a?Math.round((old[k+c]*a0*(1-weight)+painted.data[s+c]*a1*weight)/a):old[k+c];
   output[k+3]=Math.round(a*255);
  }
 }
 let changed=0,bbox=[1024,1536,-1,-1],below620=0;
 for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){const k=(y*1024+x)*4;if([0,1,2,3].some(c=>old[k+c]!==output[k+c])){changed++;bbox=[Math.min(bbox[0],x),Math.min(bbox[1],y),Math.max(bbox[2],x),Math.max(bbox[3],y)];if(y>=620)below620++;}}
 if(below620)throw Error('Body pixels changed');
 fs.mkdirSync(path.dirname(outFile),{recursive:true});
 await sharp(output,{raw:{width:1024,height:1536,channels:4}}).png().toFile(outFile);
 const gaps=[];
 for(let y=520;y<=600;y++){
  const skin=bounds(face,y,395,645);
  const left0=bounds(old,y,395,516),right0=bounds(old,y,518,645),left=bounds(output,y,395,516),right=bounds(output,y,518,645);
  if(!skin)continue;
  const gap=(l,r)=>({left:l?Math.max(0,skin[0]-l[1]-1):null,right:r?Math.max(0,r[0]-skin[1]-1):null});
  gaps.push({y,skin,before:{left:left0,right:right0,gap:gap(left0,right0)},after:{left,right,gap:gap(left,right)}});
 }
 const report={index:Number(id),source_sha256:hash(oldFile),raw_sha256:hash(rawFile),output_sha256:hash(outFile),raw_canvas:[meta.width,meta.height],native_crop_origin:[395,500],native_crop_extent:[250,130],uniform_scale:250/meta.width,whole_crop_resize_only:true,selection:'Feathered collar-side ROI; retain only genuinely needed painted cloth, limiting inward support to about two pixels of neck overlap. Replace old edge, do not source-over duplicate it.',changed_pixels:changed,changed_bbox_inclusive:bbox,y_ge620_changed_pixels:below620,face_changed:false,synthetic_texture:false,local_warp:false,rows:gaps};
 fs.writeFileSync(path.join(__dirname,`clothing${id}_registration.json`),JSON.stringify(report,null,2)+'\n');
 for(const [bg,suffix]of[['#e7e0d4','light'],['#252b2e','dark']]){
  const panels=[];for(const [i,input]of[[0,oldFile],[1,outFile]]){const assembled=await sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite([{input:faceFile},{input}]).png().toBuffer();panels.push({input:await sharp(assembled).extract({left:395,top:500,width:250,height:130}).resize(1000,520).png().toBuffer(),left:i*1000,top:0});}
  await sharp({create:{width:2000,height:520,channels:4,background:bg}}).composite(panels).png().toFile(path.join(pack,`qa/clothing${id}_before_after_${suffix}.png`));
 }
 console.log(JSON.stringify({changed,bbox,below620,maxGapAfter:{left:Math.max(...gaps.map(r=>r.after.gap.left??0)),right:Math.max(...gaps.map(r=>r.after.gap.right??0))},examples:gaps.filter(r=>[528,530,540,550,560].includes(r.y))}));
})().catch(e=>{console.error(e);process.exitCode=1});
