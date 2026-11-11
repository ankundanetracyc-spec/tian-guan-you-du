// Technical reimport of existing source/native pixels; no painting, warping, or installation.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const P=path.resolve(__dirname,'..'),ROOT=path.resolve(P,'../../..');
const L=path.join(ROOT,'mods/appearance/male_loose_style_install_20261106');
const BASE=path.join(P,'reference/current_male/eyes/04.png');
const MASTER=path.join(L,'source/approved_male.png');
const GEO=path.join(L,'source/feature_import.json');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t);};
async function read(p){const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});return{data,w:info.width,h:info.height};}
function inv(m,x,y){const dx=x-m.tx,dy=y-m.ty,d=m.a*m.a+m.b*m.b;return[(m.a*dx+m.b*dy)/d,(-m.b*dx+m.a*dy)/d];}
function sample(im,x,y){if(x<0||y<0||x>=im.w-1||y>=im.h-1)return[0,0,0,0];const xi=Math.floor(x),yi=Math.floor(y),fx=x-xi,fy=y-yi,o=[0,0,0,0];for(const[i,j,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){const k=((yi+j)*im.w+xi+i)*4,a=im.data[k+3]/255;o[3]+=w*a;for(let c=0;c<3;c++)o[c]+=im.data[k+c]*a*w;}if(o[3])for(let c=0;c<3;c++)o[c]/=o[3];o[3]*=255;return o;}
(async()=>{
 if(sha(BASE)!=='6c3f023df0d845a979727f4fab210c3ad95a03a881a40f3ea493d19dc0252f14')throw Error('Frozen v6 baseline mismatch');
 if(sha(MASTER)!=='5ca3f4cc0ea0410c22bcf07dfef278a9496a0fea968c38008dfe8f7dc5cf6ee3')throw Error('Approved loose source mismatch');
 const geo=JSON.parse(fs.readFileSync(GEO,'utf8')),rec=geo.records.find(r=>r.part==='eyes'),F=rec.shared_source_to_frame;
 const frame={a:F[0][0],b:F[1][0],tx:F[0][2],ty:F[1][2]},combined=rec.combined_native_to_frame;
 const rawPath=path.join(L,rec.raw_source),[base,master,native]=await Promise.all([read(BASE),read(MASTER),read(rawPath)]);
 if(sha(rawPath)!==rec.raw_sha256)throw Error('Frozen native extraction changed');
 fs.mkdirSync(path.join(P,'candidate'),{recursive:true});const reports=[];
 const variants=[{name:'symmetric_8px',left:436,right:444},{name:'native_corner_6px',left:444,right:450}];
 for(const v of variants){
  const out=Buffer.from(base.data);let count=0,alphaDiff=0,rightDiff=0,bbox=[1024,1536,0,0];
  for(let y=0;y<1536;y++)for(let x=0;x<512;x++){
   const k=(y*1024+x)*4;if(!base.data[k+3])continue;const[sx,sy]=inv(frame,x,y);
   // Changes only the former native/source switch neighborhood. Keep baseline RGB outside.
   const limitLeft=Math.min(440,v.left),limitRight=Math.max(440,v.right);
   if(sx<limitLeft||sx>limitRight)continue;
   const[nx,ny]=inv(combined,x,y),n=sample(native,nx,ny),a=sample(master,sx,sy),nativeWeight=1-smooth((sx-v.left)/(v.right-v.left));
   // Both source images are sampled with the recorded original whole-pair transform.
   // Native Alpha is NOT imported: the production v6 Alpha is immutable.
   for(let c=0;c<3;c++)out[k+c]=Math.round(nativeWeight*n[c]+(1-nativeWeight)*a[c]);
  }
  for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){
   const k=(y*1024+x)*4;let diff=false;for(let c=0;c<4;c++)if(out[k+c]!==base.data[k+c])diff=true;
   if(diff){count++;bbox=[Math.min(bbox[0],x),Math.min(bbox[1],y),Math.max(bbox[2],x+1),Math.max(bbox[3],y+1)];if(x>=512)rightDiff++;}
   if(out[k+3]!==base.data[k+3])alphaDiff++;
  }
  if(alphaDiff||rightDiff)throw Error('Frozen Alpha/right RGBA changed');
  const dest=path.join(P,`candidate/left_eye_${v.name}.png`);await sharp(out,{raw:{width:1024,height:1536,channels:4}}).png().toFile(dest);
  reports.push({variant:v.name,file:path.relative(P,dest).replaceAll('\\','/'),candidate_sha256:sha(dest),mix:'native*(1-smoothstep(source_x,left,right)) + approved*smoothstep; existing pixels only',source_x_transition:[v.left,v.right],diff_bbox_xyxy:bbox,rgb_changed_pixels:count,alpha_differing_pixels:alphaDiff,right_half_rgba_differing_pixels:rightDiff});
 }
 const report={schema:'left-eye-seam-continuous-reimport/v1',status:'two_technical_candidates_for_visual_selection',before_sha256:sha(BASE),base:BASE,approved_source:{path:MASTER,sha256:sha(MASTER)},registered_native:{path:rawPath,sha256:sha(rawPath)},geometry_source:GEO,shared_source_to_frame:F,combined_native_to_frame:combined,geometry_changes:false,alpha_changes:false,right_half_changes:false,source_of_defect:'Original import hidden() used fixed sx<440 nativeRGB vs approvedRGB, a binary boundary at currentx439.46; this location has no hairguard. Different existing lash/fold RGB curves produce the vertical step even with continuous Alpha.',scope:'Only left RGB near original switch; no pupil/iris center pixels or right-half RGBA change. No image generation, painting or local warp.',variants:reports};
 fs.writeFileSync(path.join(P,'source/seam_reimport_variants.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(reports));
})();
