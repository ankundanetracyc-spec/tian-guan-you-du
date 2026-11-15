const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const root = __dirname;
const order = ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front'];
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const median = a => a.length ? a.sort((x,y)=>x-y)[Math.floor(a.length/2)] : null;
async function main() {
  const records=[];
  for (const sex of ['male','female']) for (const part of order) {
    const file=path.join(root,'parts',sex,part+'.png');
    const meta=await sharp(file).metadata();
    if (meta.width!==1024 || meta.height!==1536 || meta.channels!==4 || !meta.hasAlpha) throw Error('Invalid canvas/alpha: '+file);
    const {data,info}=await sharp(file).raw().toBuffer({resolveWithObject:true});
    const rgb=[[],[],[]]; let opaque=0, partial=0, visible=0, eyeOverlap=0;
    let minX=1024,minY=1536,maxX=-1,maxY=-1;
    for(let i=0;i<data.length;i+=4) {
      const a=data[i+3], px=i/4, x=px%info.width,y=Math.floor(px/info.width);
      if(a>0){visible++; if(a<255)partial++;}
      if(a>180){opaque++;minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y); for(let c=0;c<3;c++)rgb[c].push(data[i+c]);}
      if(part==='hair_front' && a>16 && y>=369 && y<=385 && ((x>=450&&x<=478)||(x>=555&&x<=588)))eyeOverlap++;
    }
    if(part==='hair_front' && eyeOverlap>0) throw Error(sex+' front hair overlaps central eye region: '+eyeOverlap);
    const centerX=sex==='male'?517:519;
    const centerSegments=[]; let start=null;
    if(part.startsWith('hair_')) {
      for(let y=50;y<=450;y++) {
        let maxAlpha=0;
        for(let x=centerX-2;x<=centerX+2;x++)maxAlpha=Math.max(maxAlpha,data[(y*1024+x)*4+3]);
        if(maxAlpha>180&&start===null)start=y;
        if((maxAlpha<=180||y===450)&&start!==null){centerSegments.push([start,maxAlpha<=180?y-1:y]);start=null;}
      }
    }
    const forehead=[[],[],[]];
    if(part==='face_shape')for(let y=300;y<325;y++)for(let x=490;x<540;x++){const i=(y*1024+x)*4;if(data[i+3]>180)for(let c=0;c<3;c++)forehead[c].push(data[i+c]);}
    const unchanged = !part.startsWith('hair_');
    const baseline=path.join(root,'baseline',sex,part+'.png');
    if(unchanged&&hash(file)!==hash(baseline))throw Error('Retained source changed: '+file);
    records.push({gender:sex,part,file:path.relative(root,file),width:meta.width,height:meta.height,rgba:true,sha256:hash(file),retained_bytes_equal:unchanged?true:null,visible_pixels:visible,partial_alpha_pixels:partial,opaque_gt180_pixels:opaque,opaque_bbox:maxX<0?null:[minX,minY,maxX,maxY],opaque_rgb_median:rgb.map(median),forehead_patch_median:part==='face_shape'?forehead.map(median):null,central_alpha_segments_y:centerSegments,eye_overlap_pixels:part==='hair_front'?eyeOverlap:null});
  }
  const active=path.resolve(root,'../ink_three_variants_20261004');
  const before=JSON.parse(fs.readFileSync(path.join(root,'qa/active_before.json'),'utf8'));
  const changed=Object.keys(before).filter(p=>!fs.existsSync(path.join(active,p))||hash(path.join(active,p))!==before[p]);
  if(changed.length)throw Error('Active resources changed: '+changed.join(', '));
  const result={pass:true,assets:18,new_generated_hair:4,retained_parts:14,active_files_unchanged:Object.keys(before).length,records,limitations:['Geometry sampling and source integrity only; not a three-control-point residual certification.','No complete existing-library cross-product validation or game installation in this candidate.','Visual judgement remains separate from these checks.']};
  fs.writeFileSync(path.join(root,'qa/assets.json'),JSON.stringify(result,null,2));
  console.log(JSON.stringify({pass:true,assets:18,new_generated_hair:4,retained_parts:14,active_files_unchanged:Object.keys(before).length,hair:records.filter(x=>x.part.startsWith('hair_')).map(x=>({gender:x.gender,part:x.part,median:x.opaque_rgb_median,central_segments:x.central_alpha_segments_y}))},null,2));
}
main().catch(e=>{console.error(e);process.exit(1);});
